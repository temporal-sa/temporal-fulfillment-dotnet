namespace TemporalioSamples.ActivitySimple;

using Temporalio.Activities;
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
        Console.WriteLine($"{id}: *** Items for picking in store {subOrder.StoreID} ***");
        foreach (var item in subOrder.Items)
        {
            Console.WriteLine($"{id}: - {item.ProductName}");
        }
        return "SUCCESS";
    }

    [Activity]
    public static string Dispatch()
    {
        return "SUCCESS";
    }

    [Activity]
    public static string ConfirmDelivered()
    {
        return "SUCCESS";
    }

}