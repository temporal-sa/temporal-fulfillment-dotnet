namespace TemporalioSamples.ActivitySimple;

using Temporalio.Exceptions;
using Temporalio.Workflows;
using Microsoft.Extensions.Logging;

[Workflow]
public class SuborderChildWorkflow
{
    private bool approved = false;
    private bool dispatched = false;
    private bool delivered = false;
    private string status = "RECEIVED";

    [WorkflowRun]
    public async Task<string> RunAsync(SubOrder subOrder)
    {
        var resultList = new List<int>();

        // todo block on this and have business logic
        //var waitApproved = Workflow.WaitConditionAsync(() => this.approved);

        // Wait for signal or timeout in 30 seconds
        Console.WriteLine("Waiting for dispatch");
        if (await Workflow.WaitConditionAsync(() => dispatched, TimeSpan.FromSeconds(30)))
        {
            status = "DISPATCHED";
        }
        else
        {
            status = "TIMEOUT REACHED WHILE WAITING FOR DISPATCH";
            return status;
        }

        // Wait for signal or timeout in 30 seconds
        Console.WriteLine("Waiting for delivery confirmation");
        if (await Workflow.WaitConditionAsync(() => delivered, TimeSpan.FromSeconds(30)))
        {
            status = "DELIVERED";
        }
        else
        {
            status = "TIMEOUT REACHED WHILE WAITING FOR DELIVERY";
            return status;
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

}