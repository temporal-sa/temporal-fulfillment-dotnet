using Temporalio.Client;
using Microsoft.Extensions.Configuration;
using System.IO;
using TemporalioSamples.Fulfillment;
using Temporalio.Extensions.Hosting;

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

builder.Services.AddTemporalClient(x =>
{
    x.TargetHost = temporalAddress;
    x.Namespace = temporalNamespace;
    x.Tls = new()
                {
                    ClientCert =
                        File.ReadAllBytes(temporalCertPath),
                    ClientPrivateKey =
                        File.ReadAllBytes(temporalKeyPath),
                };
});

var app = builder.Build();

app.MapGet("/", async (ITemporalClient client) =>
{
    var order = new Order("../FulfillmentWorkflow/DataSamples/order.json");

    return await client.StartWorkflowAsync(
        (OrderWorkflow wf) => wf.RunAsync(order),
        new(id: $"fulfillment-workflow-{Guid.NewGuid()}", taskQueue: "fulfillment-example"));
});

app.Run();