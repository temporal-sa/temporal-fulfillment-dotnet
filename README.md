# Temporal Fulfillment Example

![Screenshot of worker running](./screenshot.png)

### Business Logic
* Order is split into SubOrders then sent to stores for fulfillment
* If an Order can’t be fulfilled by a store, then the workflow will try and roll back the entire order (all suborders)
* Suborders over a set $ amount must explicitly be approved by the store
    * Otherwise compensations are run


## Running the example

The sample is configured by default to connect to a local Temporal Server running on localhost:7233.

To instead connect to Temporal Cloud, set the following environment variables, replacing them with your own Temporal Cloud credentials:

```
TEMPORAL_ADDRESS=testnamespace.sdvdw.tmprl.cloud:7233
TEMPORAL_NAMESPACE=testnamespace.sdvdw
TEMPORAL_CERT_PATH="/path/to/file.pem"
TEMPORAL_KEY_PATH="/path/to/file.key"
```

Run workflows from
```
cd FulfillmentWorkflow
```

First, we have to run a worker. In a separate terminal, run the worker from this directory:
```
dotnet run run-worker
```
This will start a worker. To run against Temporal Cloud, `--target-host` may be something like
`my-namespace.a1b2c.tmprl.cloud:7233` and `--namespace` may be something like `my-namespace.a1b2c`.

With that running, in a separate terminal execute the workflow from this directory:
```
dotnet run execute-workflow
```

### Run a workflow that requires human-in-the-loop approval

Suborders over $500 require human approval (via signal) to proceed.

Change quantities of items in `Order.json` to create a large order (e.g. 200 orange juices). Execute the workflow per instructions above.

Get the suborder's workflow ID and signal it to approve the workflow
```
dotnet run signal-suborder-approve --workflow-id order-9ad64b2d-0920-434d-8f78-e994805a50dd-001
```
This must be done within 20 seconds or all child workflows are rolled back (and all workflows return as failed).

### Run a workflow that simulates a code bug

In `Order.json` change the order ID to 9. This will make the main workflow throw an exception, suspending the workflow execution.

Comment out the exception code in `Order.workflow.cs`, stop the worker then start it again. The workflow will pick up and proceed without issue (as the 'bug' has been fixed).

### Run a workflow that simulates activity retries

In `Activities.cs` comment out the `if(ActivityExecutionContext.Current.Info.Attempt <= 6)` code block (stop and re-run the worker).

This activity will now fail until the 6th time Temporal attempts to execute it. Attempt 6 will succeed and the workflow will proceeed as normal.

### Creating schedules:
```
# Using a different json file to usual as the payload contains capitalized attributes (which is what the data class expects)
temporal schedule create --cron "0/5 * * * *" --workflow-id fulfillment-order --task-queue fulfillment-example-schedule  --workflow-type OrderWorkflow --schedule-id fulfillment-example-schedule-1 --input-file DataSamples/orderSchedulePayload.json

# worker runs on a different task queue
dotnet run run-worker-schedule
```