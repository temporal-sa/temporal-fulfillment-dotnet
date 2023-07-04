namespace TemporalioSamples.ActivitySimple;

using Temporalio.Exceptions;
using Temporalio.Workflows;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

[Workflow]
public class SuborderChildWorkflow
{
    private SubOrder subOrder;
    private bool approved = false;
    private bool dispatched = false;
    private bool delivered = false;
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
            Console.WriteLine($"Picked item {item.ProductName}");
        }
        Console.WriteLine("All items picked");

        // todo block on this and have business logic
        //var waitApproved = Workflow.WaitConditionAsync(() => this.approved);

        var waitDispatch = Workflow.WaitConditionAsync(() => dispatched, TimeSpan.FromSeconds(30));

        var completedTask = await Task.WhenAny(waitDispatch, waitRollback);
        Console.WriteLine("Waiting for dispatch");
        if (completedTask == waitDispatch)
        {
            if (await waitDispatch)
            {
                SetStatus("DISPATCHED");
            }
            else
            {
                throwApplicationError("TIMEOUT REACHED WHILE WAITING FOR DISPATCH");
                return "ROLLBACK";
            }
        }
        else if (completedTask == waitRollback)
        {
            RunRollback();
            throwApplicationError("Rollback Requested");
            return "ROLLBACK";
        }

        // Wait for signal or timeout in 30 seconds
        Console.WriteLine("Waiting for delivery confirmation");
        if (await Workflow.WaitConditionAsync(() => delivered, TimeSpan.FromSeconds(60)))
        {
            SetStatus("DELIVERED");
        }
        else
        {
            throwApplicationError("TIMEOUT REACHED WHILE WAITING FOR DELIVERY CONFIRMATION");
            return "ROLLBACK";
        }

        return status;
    }

    [WorkflowSignal]
    public async Task OrderApprove()
    {
        Console.WriteLine("Order Approve Signal Received");
        this.approved = true;
    }

    [WorkflowSignal]
    public async Task OrderDeny()
    {
        Console.WriteLine("Order Deny Signal Received");
        this.approved = false;
    }

    [WorkflowSignal]
    public async Task Dispatch()
    {
        Console.WriteLine("Order Dispatched Signal Received");
        this.dispatched = true;
    }

    [WorkflowSignal]
    public async Task ConfirmDelivery()
    {
        Console.WriteLine("Order Delivered Signal Received");
        this.delivered = true;
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
        Console.WriteLine("got halt signal, cancelling/compensating child workflows");
        this.rollback = true;
    }

    private string SetStatus(string newStatus)
    {
        Console.WriteLine($"Setting status to {newStatus}");
        status = newStatus;
        statusCompensation.Add(newStatus);
        return status;
    }

    private void RunRollback()
    {
        while (statusCompensation.Count > 0)
        {
            string status = statusCompensation[statusCompensation.Count - 1];
            Console.WriteLine($"Rolling Back: {status}");
            statusCompensation.RemoveAt(statusCompensation.Count - 1);
        }
    }

    private void throwApplicationError(string message)
    {
        Console.WriteLine($"Throwing Application Error: {message}");
        RunRollback();
        throw new ApplicationFailureException(message);
    }

}