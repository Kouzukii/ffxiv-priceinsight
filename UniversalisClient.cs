using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PriceInsight; 

public class UniversalisClient : IDisposable {
    private readonly HttpClient httpClient;
    private readonly HttpClient prefetchHttpClient;

    public UniversalisClient() {
        httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(5000) };
        prefetchHttpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(30000) };
    }

    public void Dispose() {
        httpClient.Dispose();
        prefetchHttpClient.Dispose();
    }

    public async Task<MarketBoardData?> GetMarketBoardData(string datacenter, uint worldId, ulong itemId) {
        HttpResponseMessage result;
        try {
            result = await httpClient.GetAsync($"https://universalis.app/api/v2/{datacenter}/{itemId}");

            if (result.StatusCode != HttpStatusCode.OK) {
                PluginLog.LogError("Failed to retrieve data from Universalis for itemId {0} / dc {1} with sc {2}.", itemId, datacenter, result.StatusCode);
                return null;
            }

            var item = JsonConvert.DeserializeObject<ItemData>(await result.Content.ReadAsStringAsync());
            if (item == null) {
                PluginLog.LogError("Failed to deserialize Universalis response for itemId {0} / dc {1}.", itemId, datacenter);
                return null;
            }

            return ParseMarketBoardData(worldId, item);
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Failed to retrieve data from Universalis for itemId {0} / dc {1}.", itemId, datacenter);
            return null;
        }
    }

    public async Task<Dictionary<ulong, MarketBoardData>?> GetMarketBoardDataList(string datacenter, uint worldId, List<ulong> itemId) {
        HttpResponseMessage result;
        if (itemId.Count == 1) {
            var dict = new Dictionary<ulong, MarketBoardData>();
            if (await GetMarketBoardData(datacenter, worldId, itemId[0]) is { } data)
                dict.Add(itemId[0], data);
            return dict;
        }

        try {
            result = await prefetchHttpClient.GetAsync($"https://universalis.app/api/{datacenter}/{string.Join(',', itemId.Select(i => i.ToString()))}");

            if (result.StatusCode != HttpStatusCode.OK) {
                PluginLog.LogError("Failed to retrieve data from Universalis for itemId {0} / dc {1} with sc {2}.", itemId, datacenter, result.StatusCode);
                return null;
            }

            var json = JsonConvert.DeserializeObject<UniversalisData>(await result.Content.ReadAsStringAsync());
            if (json == null) {
                PluginLog.LogError("Failed to deserialize Universalis response for itemId {0} / dc {1}.", itemId, datacenter);
                return null;
            }

            var items = new Dictionary<ulong, MarketBoardData>();
            if (json.items != null)
                foreach (var item in json.items) {
                    if (item.itemID is { } id)
                        items.Add(id, ParseMarketBoardData(worldId, item));
                }

            return items;
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Failed to retrieve data from Universalis for itemId {0} / dc {1}.", itemId, datacenter);
            return null;
        }
    }

    private MarketBoardData ParseMarketBoardData(uint worldId, ItemData item) {
        var marketBoardData = new MarketBoardData {
            LastUploadTime = item.lastUploadTime,
            MinimumPriceNQ = item.listings?.FirstOrDefault(l => !l.hq),
            MinimumPriceHQ = item.listings?.FirstOrDefault(l => l.hq),
            OwnMinimumPriceNQ = item.listings?.FirstOrDefault(l => !l.hq && l.worldID == worldId),
            OwnMinimumPriceHQ = item.listings?.FirstOrDefault(l => l.hq && l.worldID == worldId),
            MostRecentPurchaseNQ = item.recentHistory?.FirstOrDefault(l => !l.hq),
            MostRecentPurchaseHQ = item.recentHistory?.FirstOrDefault(l => l.hq),
            OwnMostRecentPurchaseNQ = item.recentHistory?.FirstOrDefault(l => !l.hq && l.worldID == worldId),
            OwnMostRecentPurchaseHQ = item.recentHistory?.FirstOrDefault(l => l.hq && l.worldID == worldId),
        };
        return marketBoardData;
    }
}

// ReSharper disable all
class UniversalisData {
    public List<ItemData>? items { get; set; }
}

class ItemData {
    public ulong? itemID { get; set; }
    public string? dcName { get; set; }
    [JsonConverter(typeof(UnixMilliDateTimeConverter))]
    public DateTime? lastUploadTime { get; set; }
    public List<ListingData>? listings { get; set; }
    public List<RecentData>? recentHistory { get; set; }

    public class ListingData {
        public long pricePerUnit { get; set; }
        public bool hq { get; set; }
        public ulong worldID { get; set; }
        public string worldName { get; set; } = null!;

        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime lastReviewTime { get; set; }

        public static implicit operator Listing?(ListingData? data) {
            if (data == null)
                return null;
            return new Listing { Price = data.pricePerUnit, Time = data.lastReviewTime, World = data.worldName };
        }
    }

    public class RecentData {
        public bool hq { get; set; }
        public long pricePerUnit { get; set; }
        public string worldName { get; set; } = null!;
        public ulong worldID { get; set; }
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime timestamp { get; set; }
            
        public static implicit operator Listing?(RecentData? data) {
            if (data == null)
                return null;
            return new Listing { Price = data.pricePerUnit, Time = data.timestamp, World = data.worldName };
        }
    }
}
// ReSharper restore all