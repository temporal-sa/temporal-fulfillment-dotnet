namespace TemporalioSamples.ActivitySimple;

using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Temporalio.Api.History.V1;
using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class MyWorkflow
{
    private List<string> currentResults = new List<string>();
    private bool halted = false;

    [WorkflowRun]
    public async Task<List<string>> RunAsync(Order order)
    {
        var workflowId = Workflow.Info.WorkflowID;
        Console.WriteLine($"Running Workflow ID: {workflowId}");

        // Run sub-order allocation
        var subOrders = await Workflow.ExecuteActivityAsync(
            () => MyActivities.AllocateToStores(order),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(5),
            });

        // Container to hold task handles for all workflows
        var suborderHandles = new List<Task<string>>();

        // Start 5 workflows
        foreach (var subOrder in subOrders)
        {
            var childWorkflowId = $"{workflowId}-{subOrder.StoreID}";
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
                        Console.WriteLine("result is...");
                        Console.WriteLine(t.Result);  // print the result
                        currentResults.Add(t.Result);
                        return t.Result;
                    });

            // Add this task to the list of tasks to wait on
            suborderHandles.Add(resultTask);
        }

        // Wait for all workflows to complete and gather their results
        var childResultsTask = Task.WhenAll(suborderHandles);
        var waitHalted = Workflow.WaitConditionAsync(() => halted);

        var finishedWorkflow = await Task.WhenAny(childResultsTask, waitHalted);

        if (finishedWorkflow == childResultsTask)
        {
            Console.WriteLine("Workflow completed");
            var childResults = childResultsTask.Result;
            return childResults.ToList();
        }
        else
        {
            Console.WriteLine("Workflow exiting due to halt signal");
            throw new ApplicationFailureException("Exited due to signal");
        }
    }

    [WorkflowQuery]
    public string CurrentResults()
    {
        List<string> resultStrings = new List<string>();

        foreach (var innerList in currentResults)
        {
            string innerListString = "[" + String.Join(", ", innerList) + "]";
            resultStrings.Add(innerListString);
        }

        return "[" + String.Join(", ", resultStrings) + "]";
    }

    [WorkflowSignal]
    public async Task HaltSignal()
    {
        Console.WriteLine("got halt signal");
        this.halted = true;
    }
}