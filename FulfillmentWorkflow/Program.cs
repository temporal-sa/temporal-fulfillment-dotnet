using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.Fulfillment;
using Temporalio.Workflows;
using Temporalio.Api.Enums.V1;
using Microsoft.Extensions.Configuration;

var rootCommand = new RootCommand("Client mTLS sample");

// Helper for client commands
void AddClientCommand(
    string name,
    string desc,
    Func<ITemporalClient, Option<string>, InvocationContext, CancellationToken, Task> func)
{
    var cmd = new Command(name, desc);
    rootCommand!.AddCommand(cmd);

    // Add options
    // var targetHostOption = new Option<string>("--target-host", "Host:port to connect to");
    // targetHostOption.IsRequired = true;
    // var namespaceOption = new Option<string>("--namespace", "Namespace to connect to");
    // namespaceOption.IsRequired = true;
    // var clientCertOption = new Option<FileInfo>("--client-cert", "Client certificate file for auth");
    // clientCertOption.IsRequired = true;
    // var clientKeyOption = new Option<FileInfo>("--client-key", "Client key file for auth");
    // clientKeyOption.IsRequired = true;

    // Read from environment variables
    var temporalAddress = Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? "localhost:7233";
    var temporalNamespace = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "default";
    var temporalCertPath = Environment.GetEnvironmentVariable("TEMPORAL_CERT_PATH");
    var temporalKeyPath = Environment.GetEnvironmentVariable("TEMPORAL_KEY_PATH");

    var workflowIdOption = new Option<string>("--workflow-id", "Workflow Id to signal"); // Add this line
    workflowIdOption.IsRequired = false; // Not required

    cmd.AddOption(workflowIdOption); // Add this line

    // Set handler
    cmd.SetHandler(async ctx =>
    {
        // Create client
        var clientOptions = new TemporalClientConnectOptions(temporalAddress)
        {
            Namespace = temporalNamespace!,
        };

        if (!string.IsNullOrEmpty(temporalCertPath) && !string.IsNullOrEmpty(temporalKeyPath))
        {
            clientOptions.Tls = new()
            {
                ClientCert = File.ReadAllBytes(temporalCertPath),
                ClientPrivateKey = File.ReadAllBytes(temporalKeyPath),
            };
        }

        var client = await TemporalClient.ConnectAsync(clientOptions);

        // Run
        await func(client, workflowIdOption, ctx, ctx.GetCancellationToken());
    });
}

// Command to run worker
AddClientCommand("run-worker", "Run worker", async (client, workflowIdOption, ctx, cancelToken) =>
{
    // Cancellation token cancelled on ctrl+c
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    // Create an activity instance with some state
    var activities = new FulfillmentActivities();

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "fulfillment-example").
            AddActivity(FulfillmentActivities.AllocateToStores).
            AddActivity(FulfillmentActivities.PickItems).
            AddActivity(FulfillmentActivities.Dispatch).
            AddActivity(FulfillmentActivities.ConfirmDelivered).
            AddWorkflow<OrderWorkflow>().
            AddWorkflow<SuborderChildWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
});

// Command to run worker
// TODO: dedup with the other worker (only task queue name differs)
AddClientCommand("run-worker-schedule", "Run worker (schedule)", async (client, workflowIdOption, ctx, cancelToken) =>
{
    // Cancellation token cancelled on ctrl+c
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    // Create an activity instance with some state
    var activities = new FulfillmentActivities();

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "fulfillment-example-schedule").
            AddActivity(FulfillmentActivities.AllocateToStores).
            AddActivity(FulfillmentActivities.PickItems).
            AddActivity(FulfillmentActivities.Dispatch).
            AddActivity(FulfillmentActivities.ConfirmDelivered).
            AddWorkflow<OrderWorkflow>().
            AddWorkflow<SuborderChildWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
});

// Command to run workflow
AddClientCommand("execute-workflow", "Execute workflow", async (client, workflowIdOption, ctx, cancelToken) =>
{
    var workflowId = $"order-{Guid.NewGuid()}";
    Console.WriteLine("Executing workflow");
    Console.WriteLine(workflowId);
    var order = new Order("DataSamples/order.json");
    await client.StartWorkflowAsync(
        (OrderWorkflow wf) => wf.RunAsync(order),
        new(id: workflowId, taskQueue: "fulfillment-example"));

});

// Command to signal workflow

AddClientCommand("signal-suborder-approve", "Signal workflow", async (client, workflowIdOption, ctx, cancelToken) =>
{
    Console.WriteLine("Sending approve signal to child workflow");

    var workflowId = ctx.ParseResult.GetValueForOption(workflowIdOption) ?? "";
    Console.WriteLine(workflowId);
    var handle = client.GetWorkflowHandle(workflowId);

    await handle.SignalAsync<SuborderChildWorkflow>(wf => wf.OrderApprove());
});

AddClientCommand("signal-suborder-deny", "Signal workflow", async (client, workflowIdOption, ctx, cancelToken) =>
{
    Console.WriteLine("Sending deny signal to child workflow");

    var workflowId = ctx.ParseResult.GetValueForOption(workflowIdOption) ?? "";
    Console.WriteLine(workflowId);
    var handle = client.GetWorkflowHandle(workflowId);

    await handle.SignalAsync<SuborderChildWorkflow>(wf => wf.OrderDeny());
});

// Add a new standalone command named 'scratch'
var scratchCommand = new Command("scratch", "Prints a test statement");

scratchCommand.SetHandler(
 () =>
    {
        Console.WriteLine("*** Allocating order to stores ***");
        var order = new Order("DataSamples/order.json");
        var allocator = new StoreAllocator();
        var subOrders = allocator.Allocate(order);

        foreach (var subOrder in subOrders)
        {
            Console.WriteLine(subOrder.ToJsonString());
        }
    }
);

rootCommand!.AddCommand(scratchCommand);

// Run
await rootCommand.InvokeAsync(args);