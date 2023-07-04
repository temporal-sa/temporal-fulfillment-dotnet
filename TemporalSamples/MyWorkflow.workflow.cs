namespace TemporalioSamples.ActivitySimple;

using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Temporalio.Api.Filter.V1;
using Temporalio.Api.History.V1;
using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class MyWorkflow
{
    private Order order;
    private bool requestCancel = false;
    private Dictionary<string, SubOrder> subOrders = new Dictionary<string, SubOrder>();
    // Container to hold task handles for all workflows
    private List<ChildWorkflowHandle> suborderHandles = new List<ChildWorkflowHandle>();
    private string status = "RECEIVED";


    [WorkflowRun]
    public async Task<string> RunAsync(Order orderParam)
    {
        var workflowId = Workflow.Info.WorkflowID;
        Console.WriteLine($"Running Workflow ID: {workflowId}");
        order = orderParam;

        // Run sub-order allocation
        SetStatus("ALLOCATING");;
        var subOrderList = await Workflow.ExecuteActivityAsync(
            () => MyActivities.AllocateToStores(order),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(5),
            });


        // Start 5 workflows
        status = SetStatus("STARTING SUBORDERS");

        // so we can wait on them all later
        var suborderHandleTasks = new List<Task<string>>();

        foreach (var subOrder in subOrderList)
        {
            var childWorkflowId = $"{workflowId}-{subOrder.StoreID}";

            // create a dictionary of suborders to track their status
            subOrders[childWorkflowId] = subOrder;

            // start suborders as child workflows (in parallel)
            Console.WriteLine($"Starting workflow for suborder {childWorkflowId}");
            var handle = await Workflow.StartChildWorkflowAsync(
                (SuborderChildWorkflow wf) => wf.RunAsync(subOrder),
                new()
                {
                    ID = childWorkflowId,
                });

            // Create a task that will complete when the subworkflow
            // is done and return the ID and result together
            var resultTask = handle.GetResultAsync().ContinueWith(t =>
                    {
                        // roll back the other suborders (signal) then throw failure
                        if (t.IsFaulted)
                        {
                            // Console.WriteLine(t.Exception.ToString());
                            subOrders[childWorkflowId].State = "FAILED";
                            requestCancel = true;
                            return SetStatus("FAILED");
                        }
                        else
                        {
                            // Console.WriteLine(t.Result);  // print the result
                            subOrders[childWorkflowId].State = t.Result;
                            return t.Result;
                        }

                    });

            // Add this task to the list of tasks to wait on
            suborderHandleTasks.Add(resultTask);
        }
        status = SetStatus("SUBORDERS STARTED");

        // Wait for all workflows to complete and gather their results
        var childResultsTask = Task.WhenAll(suborderHandleTasks);
        var waitCancel = Workflow.WaitConditionAsync(() => requestCancel);

        var finishedWorkflow = await Task.WhenAny(childResultsTask, waitCancel);

        if (finishedWorkflow == childResultsTask)
        {
            Console.WriteLine("Workflow completed");
            return SetStatus("COMPLETED");
        }
        else
        {
            // TODO maybe remove, overcomplicated
            status = "CANCELLED";

            // send rollbacks to children
            await RollbackSubOrders();
            await childResultsTask; // wait for workflows to rollback
            return SetStatus("ROLLBACK");
        }

    }

    [WorkflowQuery]
    public string GetStatus()
    {
        return status;
    }

    [WorkflowQuery]
    public string GetOrder()
    {
        return order.ToJsonString();
    }

    [WorkflowQuery]
    public List<string> GetSubOrderIDs()
    {
        return subOrders.Keys.ToList();
    }

    private async Task<bool> RollbackSubOrders()
    {
        foreach (var suborderId in GetSubOrderIDs())
        {
            if (subOrders[suborderId].State != "FAILED") // don't roll back a closed workflow
            {
                Console.WriteLine($"Sending rollback signal to suborder {suborderId}");

                await Workflow.GetExternalWorkflowHandle(suborderId).
                    SignalAsync<SuborderChildWorkflow>(wf => wf.Rollback());
            }
        }
        return true;
    }

        private string SetStatus(string newStatus)
    {
        Console.WriteLine($"Setting status to {newStatus}");
        status = newStatus;
        return status;
    }

}