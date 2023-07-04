using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

public class Order
{
    [JsonProperty("orderID")]
    public string OrderId { get; set; }

    [JsonProperty("customer")]
    public Customer Customer { get; set; }

    [JsonProperty("deliveryAddress")]
    public DeliveryAddress DeliveryAddress { get; set; }

    [JsonProperty("orderDetails")]
    public OrderDetails OrderDetails { get; set; }

    public Order() { }

    public Order(string filePath)
    {
        var jsonData = File.ReadAllText(filePath);
        var order = JsonConvert.DeserializeObject<Order>(jsonData);
        OrderId = order.OrderId;
        Customer = order.Customer;
        DeliveryAddress = order.DeliveryAddress;
        OrderDetails = order.OrderDetails;
    }
}

public class Customer
{
    [JsonProperty("firstName")]
    public string FirstName { get; set; }

    [JsonProperty("lastName")]
    public string LastName { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; }

    [JsonProperty("phone")]
    public string Phone { get; set; }
}

public class DeliveryAddress
{
    [JsonProperty("street")]
    public string Street { get; set; }

    [JsonProperty("city")]
    public string City { get; set; }

    [JsonProperty("state")]
    public string State { get; set; }

    [JsonProperty("postalCode")]
    public string PostalCode { get; set; }
}

public class OrderDetails
{
    [JsonProperty("totalCost")]
    public decimal TotalCost { get; set; }

    [JsonProperty("items")]
    public List<Item> Items { get; set; }

    [JsonProperty("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonProperty("shipping")]
    public decimal Shipping { get; set; }
}

public class Item
{
    [JsonProperty("productID")]
    public string ProductId { get; set; }

    [JsonProperty("productName")]
    public string ProductName { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonProperty("totalPrice")]
    public decimal TotalPrice { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    public string ToJsonString()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }

}
