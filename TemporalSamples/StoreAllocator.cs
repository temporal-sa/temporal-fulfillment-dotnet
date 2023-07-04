public class StoreAllocator
{
    public List<SubOrder> Allocate(Order order)
    {
        // Initialize sub-orders
        var subOrders = new List<SubOrder>
        {
            new SubOrder { StoreID = "001", StoreName = "Store One", Items = new List<Item>() },
            new SubOrder { StoreID = "002", StoreName = "Store Two", Items = new List<Item>() }
        };

        // Split items between the two sub-orders
        for (int i = 0; i < order.OrderDetails.Items.Count; i++)
        {
            subOrders[i % 2].Items.Add(order.OrderDetails.Items[i]);
        }

        return subOrders;
    }
}
