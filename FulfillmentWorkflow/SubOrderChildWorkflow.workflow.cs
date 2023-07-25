namespace TemporalioSamples.Fulfillment;

using Temporalio.Exceptions;
using Temporalio.Workflows;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

[Workflow]
public class SuborderChildWorkflow
{
    private SubOrder? subOrder;
    private string id = Workflow.Info.WorkflowId;
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

        // if order over $500 needs approval
        if (subOrder.SubTotal >= 500)
        {
            // Wait for an approve or deny signal
            // If we get a rollback signal, we'll cancel the wait and start compensating
            Log($"Waiting for approval due to suborder total of ${subOrder.SubTotal}");
            var waitApproval = Workflow.WaitConditionAsync(() => approval, TimeSpan.FromSeconds(15));
            var waitDenial = Workflow.WaitConditionAsync(() => denial);
            var approvedOrRollback = await Workflow.WhenAnyAsync(waitApproval, waitDenial, waitRollback);

            if (approvedOrRollback == waitApproval)
            {
                if (await waitApproval)
                {
                    Log($"SubOrder approved");
                }
                else
                {
                    // timer got triggered
                    ThrowApplicationErrorAndRollback("SubOrder denied due to approval timeout");
                }
            }
            else if (approvedOrRollback == waitDenial)
            {
                ThrowApplicationErrorAndRollback("SubOrder Denied");
            }
            else
            {
                ThrowApplicationErrorAndRollback("Rollback Requested");
            }
        }

        SetStatus("PICKING");
        string ItemPrintout = await Workflow.ExecuteActivityAsync(
        () => FulfillmentActivities.PickItems(subOrder, id),
            OrderWorkflow.DefaultActivityOptions);
        Log(ItemPrintout);
        // Simulate picking an order over 30s (can be undone by rollback)
        var waitDispatch =
            await Workflow.WaitConditionAsync(() => rollback, TimeSpan.FromSeconds(40));
        Log($"All items picked: Dispatching");

        if (waitDispatch) // rollback requested
        {
            Log($"Got rollback signal, cancelling/compensating this suborder");
            ThrowApplicationErrorAndRollback("Rollback Requested");
        }
        else
        {
            await Workflow.DelayAsync(TimeSpan.FromSeconds(2));
            await Workflow.ExecuteActivityAsync(
            () => FulfillmentActivities.Dispatch(),
                OrderWorkflow.DefaultActivityOptions);
            SetStatus("DISPATCHED");
        }

        // Delay by 30 seconds to simulate delivery
        await Workflow.DelayAsync(TimeSpan.FromSeconds(15));
        await Workflow.ExecuteActivityAsync(
        () => FulfillmentActivities.ConfirmDelivered(),
            OrderWorkflow.DefaultActivityOptions);
        SetStatus("DELIVERED");

        return status;
    }

    [WorkflowSignal]
    public async Task OrderApprove()
    {
        Log($"SubOrder Approve Signal Received");
        this.approval = true;
    }

    [WorkflowSignal]
    public async Task OrderDeny()
    {
        Log($"SubOrder Deny Signal Received");
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
        if (status == "ROLLBACK")
        {
            Log($"Already in rollback state, ignoring signal");
            return;
        }
        else if (status != "RECEIVED" && status != "PICKING")
        {
            Log($"Can't rollback from status {status}");
            return;
        }
        rollback = true;
    }

    private string SetStatus(string newStatus)
    {
        Log($"Setting status to {newStatus}");
        status = newStatus;
        statusCompensation.Add(newStatus);
        return status;
    }

    private void RunRollback()
    {
        while (statusCompensation.Count > 0)
        {
            string status = statusCompensation[statusCompensation.Count - 1];
            Log($"Rolling Back: {status}");
            statusCompensation.RemoveAt(statusCompensation.Count - 1);
        }
        SetStatus("ROLLBACK");
    }

    private void ThrowApplicationErrorAndRollback(string message)
    {
        Log($"Throwing Application Error: {message}");
        RunRollback();
        throw new ApplicationFailureException(message);
    }

    private void Log(string message)
    {
        WriteWithColorBasedOnId($"{id}: {message}");
    }

    // dodgy function to select log color based on last character of workflow id
    public void WriteWithColorBasedOnId(string message)
    {
        Console.ForegroundColor = (global::System.Object)id[^1] switch // ^1 gets the last character in the id
        {
            '1' => ConsoleColor.Blue,
            '2' => ConsoleColor.Green,
            '3' => ConsoleColor.Red,
            _ => ConsoleColor.White,
        };
        Console.WriteLine(message);
        Console.ResetColor();
    }


}