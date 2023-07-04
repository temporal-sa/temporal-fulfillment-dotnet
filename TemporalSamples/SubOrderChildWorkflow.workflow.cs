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
    private bool approval = false;
    private bool denial = false;
    private bool rollback = false;
    private string status = "RECEIVED";
    private List<string> statusCompensation = new List<string>();

    [WorkflowRun]
    public async Task<string> RunAsync(SubOrder subOrderParam)
    {
        subOrder = subOrderParam;
        var resultList = new List<int>();

        var waitRollback = Workflow.WaitConditionAsync(() => rollback);

        // if order over $10 then needs approval
        if(subOrder.SubTotal >= 10)
        {
            // Wait for an approve or deny signal
            // If we get a rollback signal, we'll cancel the wait and start compensating
            Console.WriteLine($"{id}: Waiting for approval");
            var waitApproval = Workflow.WaitConditionAsync(() => approval, TimeSpan.FromSeconds(30));
            var waitDenial = Workflow.WaitConditionAsync(() => denial);
            var approvedOrRollback = await Workflow.WhenAnyAsync(waitApproval, waitDenial, waitRollback);

            if (approvedOrRollback == waitApproval)
            {
                if(await waitApproval)
                {
                    Console.WriteLine($"{id}: SubOrder approved");
                }
                else
                {
                    // timer got triggered
                    ThrowApplicationErrorAndRollback("SubOrder Denied due to timeout");
                }
            }
            else if(approvedOrRollback == waitDenial)
            {
                ThrowApplicationErrorAndRollback("SubOrder Denied");
            }
            else
            {
                ThrowApplicationErrorAndRollback("Rollback Requested");
            }
        }

        SetStatus("PICKING");
        foreach (var item in subOrder.Items)
        {
            Console.WriteLine($"{id}: Picking item {item.ProductName}");
        }
        // Simulate picking an order (can be undone by rollback)
        var waitDispatch = 
            await Workflow.WaitConditionAsync(() => rollback, TimeSpan.FromSeconds(30));
        Console.WriteLine($"{id}: All items picked: Dispatching");

        if (waitDispatch) // rollback requested
        {
            ThrowApplicationErrorAndRollback("Rollback Requested");
        }
        else
        {
            SetStatus("DISPATCHED");
        }

        // Delay by 30 seconds to simulate delivery
        await Workflow.DelayAsync(TimeSpan.FromSeconds(30));
        SetStatus("DELIVERED");

        return status;
    }

    [WorkflowSignal]
    public async Task OrderApprove()
    {
        Console.WriteLine($"{id}: SubOrder Approve Signal Received");
        this.approval = true;
    }

    [WorkflowSignal]
    public async Task OrderDeny()
    {
        Console.WriteLine($"{id}: SubOrder Deny Signal Received");
        this.denial = true;
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
        else if(status != "RECEIVED" && status != "PICKING") {
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