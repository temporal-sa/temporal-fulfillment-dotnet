using Temporalio.Client;
using TemporalioSamples.ActivitySimple;
using Microsoft.Extensions.Configuration;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Access the configuration that has been setup by the HostBuilder
var configuration = builder.Configuration;

// Read from environment variables
var temporalAddress = configuration["TEMPORAL_ADDRESS"] ?? "localhost:7233";
var temporalNamespace = configuration["TEMPORAL_NAMESPACE"] ?? "default";
var temporalCertPath = configuration["TEMPORAL_CERT_PATH"];
var temporalKeyPath = configuration["TEMPORAL_KEY_PATH"];

// Setup console logging
builder.Logging.AddSimpleConsole().SetMinimumLevel(LogLevel.Information);

// Set a singleton for the client _task_. Errors will not happen here, only when
// the await is performed.
builder.Services.AddSingleton(ctx =>
    // Create client
        TemporalClient.ConnectAsync(
            new(temporalAddress)
            {
                Namespace = temporalNamespace!,
                // Set TLS options with client certs. Note, more options could
                // be added here for server CA (i.e. "ServerRootCACert") or SNI
                // override (i.e. "Domain") for self-hosted environments with
                // self-signed certificates.
                Tls = new()
                {
                    ClientCert =
                        File.ReadAllBytes(temporalCertPath),
                    ClientPrivateKey =
                        File.ReadAllBytes(temporalKeyPath),
                },
            }));

var app = builder.Build();

app.MapGet("/", async (Task<TemporalClient> clientTask, string? name) =>
{
    var client = await clientTask;
    return await client.ExecuteWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(),
        new(id: $"fulfillment-workflow-{Guid.NewGuid()}", taskQueue: "fulfillment-example"));
});

app.Run();
