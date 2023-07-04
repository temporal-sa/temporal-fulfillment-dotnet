namespace TemporalioSamples.ActivitySimple;

using Temporalio.Exceptions;
using Temporalio.Workflows;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

[Workflow]
public class SuborderChildWorkflow
{
    private SubOrder subOrder;
    private string id = Workflow.Info.WorkflowID;
    private bool approved = false;
    private bool dispatched = false;
    private bool rollback = false;
    private string status = "RECEIVED";
    private List<string> statusCompensation = new List<string>();

    [WorkflowRun]
    public async Task<string> RunAsync(SubOrder subOrderParam)
    {
        subOrder = subOrderParam;
        var resultList = new List<int>();

        // Wait for signal or timeout in 30 seconds
        var waitRollback = Workflow.WaitConditionAsync(() => rollback);

        SetStatus("PICKING");
        foreach (var item in subOrder.Items)
        {
            await Workflow.DelayAsync(TimeSpan.FromSeconds(2));
            Console.WriteLine($"{id}: Picked item {item.ProductName}");
        }
        Console.WriteLine($"{id}: All items picked: Waiting for dispatch");

        var waitDispatch = Workflow.WaitConditionAsync(() => dispatched, TimeSpan.FromSeconds(30));

        var completedTask = await Task.WhenAny(waitDispatch, waitRollback);
        if (completedTask == waitDispatch)
        {
            if (await waitDispatch)
            {
                SetStatus("DISPATCHED");
            }
            else
            {
                ThrowApplicationErrorAndRollback("TIMEOUT REACHED WHILE WAITING FOR DISPATCH");
            }
        }
        else if (completedTask == waitRollback)
        {
            RunRollback();
            ThrowApplicationErrorAndRollback("Rollback Requested");
        }

        // Delay by 30 seconds to simulate delivery
        await Workflow.DelayAsync(TimeSpan.FromSeconds(30));
        SetStatus("DELIVERED");

        return status;
    }

    [WorkflowSignal]
    public async Task OrderApprove()
    {
        Console.WriteLine($"{id}: Order Approve Signal Received");
        this.approved = true;
    }

    [WorkflowSignal]
    public async Task OrderDeny()
    {
        Console.WriteLine($"{id}: Order Deny Signal Received");
        this.approved = false;
    }

    [WorkflowSignal]
    public async Task Dispatch()
    {
        Console.WriteLine($"{id}: Order Dispatched Signal Received");
        this.dispatched = true;
    }

    [WorkflowQuery]
    public string GetStatus()
    {
        return status;
    }

    [WorkflowQuery]
    public string GetSubOrder()
    {
        return subOrder.ToJsonString();
    }

    [WorkflowSignal]
    public async Task Rollback()
    {
        if(status == "ROLLBACK")
        {
            Console.WriteLine($"{id}: Already in rollback state, ignoring signal");
            return;
        }
        else if(status != "RECEIVED" || status != "PICKING") {
            Console.WriteLine($"{id}: Can't rollback from status {status}");
            return;
        }
        Console.WriteLine($"{id}: Got rollback signal, cancelling/compensating this suborder");
        this.rollback = true;
    }

    private string SetStatus(string newStatus)
    {
        Console.WriteLine($"{id}: Setting status to {newStatus}");
        status = newStatus;
        statusCompensation.Add(newStatus);
        return status;
    }

    private void RunRollback()
    {
        while (statusCompensation.Count > 0)
        {
            string status = statusCompensation[statusCompensation.Count - 1];
            Console.WriteLine($"{id}: Rolling Back: {status}");
            statusCompensation.RemoveAt(statusCompensation.Count - 1);
        }
        SetStatus("ROLLBACK");
    }

    private void ThrowApplicationErrorAndRollback(string message)
    {
        Console.WriteLine($"{id}: Throwing Application Error: {message}");
        RunRollback();
        throw new ApplicationFailureException(message);
    }

}