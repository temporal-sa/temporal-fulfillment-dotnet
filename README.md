# Temporal Fulfillment Example

![Screenshot of worker running](./screenshot.png)

### Business Logic
* Order is split into SubOrders then sent to stores for fulfillment
* If an Order can’t be fulfilled by a store, then the workflow will try and roll back the entire order (all suborders)
* Suborders over a set $ amount must explicitly be approved by the store
    * Otherwise compensations are run


### Run

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

Get the suborder's workflow ID and signal it to prematurely fail the workflow
```
dotnet run signal-suborder-approve --workflow-id order-9ad64b2d-0920-434d-8f78-e994805a50dd-001
# or to deny
dotnet run signal-suborder-deny --workflow-id order-9ad64b2d-0920-434d-8f78-e994805a50dd-001
```

Creating schedules:
```
# payload contains capitalized attributes (which is what the data class expects)
temporal schedule create --cron "0/5 * * * *" --workflow-id fulfillment-order --task-queue fulfillment-example-schedule  --workflow-type OrderWorkflow --schedule-id fulfillment-example-schedule-1 --input-file DataSamples/orderSchedulePayload.json

# worker runs on a different task queue
dotnet run run-worker-schedule
```