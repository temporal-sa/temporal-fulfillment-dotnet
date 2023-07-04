namespace TemporalioSamples.ActivitySimple;

using Temporalio.Activities;
using System.Text;
using System;

public class MyActivities
{
    [Activity]
    public static List<SubOrder> AllocateToStores(Order order)
    {
        var allocator = new StoreAllocator();
        var subOrders = allocator.Allocate(order);
        return subOrders;
    }

    [Activity]
    public static string PickItems(SubOrder subOrder, string id)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"*** Items for picking in store {subOrder.StoreID} ***");

        foreach (var item in subOrder.Items)
        {
            sb.AppendLine($"\t- {item.ProductName}");
        }

        return sb.ToString();
    }

    [Activity]
    public static string Dispatch()
    {
        return "Dispatch Sent";
    }

    [Activity]
    public static string ConfirmDelivered()
    {
        return "Delivery Confirmed";
    }

}