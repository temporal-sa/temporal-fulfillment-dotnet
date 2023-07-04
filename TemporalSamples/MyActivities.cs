namespace TemporalioSamples.ActivitySimple;

using Temporalio.Activities;
using System;

public class MyActivities
{
    // comment out when breaking activity with CustomActivityException
    private readonly MyDatabaseClient dbClient = new();

    // Activities can be static and/or sync
    [Activity]
    public static string DoStaticThing() => "some-static-value";

    // Activities can be methods that can access state
    [Activity]
    public Task<string> SelectFromDatabaseAsync(string table) =>
        // To make it work: dbClient.SelectValueAsync(table);
        // To break it: throw new CustomActivityException("This is a generic exception.");
        dbClient.SelectValueAsync(table);

    [Activity]
    public static int DoRandomThing()
    {
        Random rand = new Random();
        return rand.Next(3, 6);
    }

    [Activity]
    public static List<SubOrder> AllocateToStores(Order order)
    {
        var allocator = new StoreAllocator();
        var subOrders = allocator.Allocate(order);
        return subOrders;
    }

    [Activity]
    public static bool ValidateOrder(Order order)
    {
        Console.WriteLine($"Validating order... {order.OrderId}");
        return true;
    }


    public class MyDatabaseClient
    {
        public Task<string> SelectValueAsync(string table) =>
            Task.FromResult($"some-db-value from table {table}");
    }
}