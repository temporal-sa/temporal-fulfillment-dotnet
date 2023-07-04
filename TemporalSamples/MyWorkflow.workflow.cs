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
    private bool halted = false;
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
        status = "ALLOCATING";
        var subOrderList = await Workflow.ExecuteActivityAsync(
            () => MyActivities.AllocateToStores(order),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(5),
            });


        // Start 5 workflows
        status = "STARTING SUBORDERS";

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

            // Create a task that will complete when the workflow is done and return the ID and result together
            var resultTask = handle.GetResultAsync().ContinueWith(t =>
                    {
                        Console.WriteLine("Sub-order result is...");
                        // TODO use this to roll back the other suborders (signal) then throw failure
                        Console.WriteLine(t.IsFaulted ? t.Exception.ToString() : "OK");
                        Console.WriteLine(t.Result);  // print the result
                        return t.Result;
                    });

            // Add this task to the list of tasks to wait on
            suborderHandleTasks.Add(resultTask);
        }
        status = "SUBORDERS STARTED";

        // Wait for all workflows to complete and gather their results
        var childResultsTask = Task.WhenAll(suborderHandleTasks);
        var waitHalted = Workflow.WaitConditionAsync(() => halted);

        var finishedWorkflow = await Task.WhenAny(childResultsTask, waitHalted);

        if (finishedWorkflow == childResultsTask)
        {
            status = "SUBORDERS COMPLETED";
            Console.WriteLine("Workflow completed");
            var childResults = childResultsTask.Result;
            return "COMPLETED";
        }
        else
        {
            status = "HALTED";
            Console.WriteLine("Workflow exiting due to halt signal");
            // throw new ApplicationFailureException("Exited due to signal");
            // send cancellations to children
            foreach (var suborderId in GetSubOrderIDs())
            {
                await Workflow.GetExternalWorkflowHandle(suborderId).
                    SignalAsync<SuborderChildWorkflow>(wf => wf.Rollback());
            }
            // todo maybe pause here to allow restarts?
            return "HALTED";
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

    [WorkflowSignal]
    public async Task HaltSignal()
    {
        Console.WriteLine("got halt signal, cancelling/compensating child workflows");
        this.halted = true;
    }

}