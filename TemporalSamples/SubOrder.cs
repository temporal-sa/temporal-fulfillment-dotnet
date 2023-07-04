using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

public class SubOrder
{
    [JsonProperty("storeID")]
    public string StoreID { get; set; }

    [JsonProperty("storeName")]
    public string StoreName { get; set; }

    [JsonProperty("items")]
    public List<Item> Items { get; set; }

    [JsonProperty("state")]
    public string State { get; set; }

    public string ToJsonString()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}
