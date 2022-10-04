using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PriceInsight;

public class UniversalisClient : IDisposable {
    private readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromMilliseconds(60000) };

    internal static readonly Dictionary<uint, uint> WorldToDc = Service.DataManager.GetExcelSheet<World>()!.Where(w => w.IsPublic)
        .ToDictionary(w => w.RowId, w => w.DataCenter.Row);

    internal static readonly Dictionary<uint, string> WorldToDcName = Service.DataManager.GetExcelSheet<World>()!.Where(w => w.IsPublic)
        .ToDictionary(w => w.RowId, w => w.DataCenter.Value!.Name.RawString);

    public void Dispose() {
        httpClient.Dispose();
    }

    public async Task<MarketBoardData?> GetMarketBoardData(string scope, uint homeWorldId, ulong itemId) {
        try {
            var result = await httpClient.GetAsync($"https://universalis.app/api/v2/{scope}/{itemId}");

            if (result.StatusCode != HttpStatusCode.OK) {
                throw new HttpRequestException("Invalid status code " + result.StatusCode, null, result.StatusCode);
            }

            var item = JsonConvert.DeserializeObject<ItemData>(await result.Content.ReadAsStringAsync());
            if (item == null) {
                throw new HttpRequestException("Universalis returned null response");
            }

            return ParseMarketBoardData(item, homeWorldId);
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Failed to retrieve data from Universalis for itemId {0}, scope {1}.", itemId, scope);
            return null;
        }
    }

    public async Task<Dictionary<uint, MarketBoardData>?> GetMarketBoardDataList(string scope, uint homeWorldId, List<uint> itemId) {
        // when only 1 item is queried, Universalis doesn't respond with an array
        if (itemId.Count == 1) {
            var dict = new Dictionary<uint, MarketBoardData>();
            if (await GetMarketBoardData(scope, homeWorldId, itemId[0]) is { } data)
                dict.Add(itemId[0], data);
            return dict;
        }

        try {
            var result = await httpClient.GetAsync($"https://universalis.app/api/v2/{scope}/{string.Join(',', itemId.Select(i => i.ToString()))}");

            if (result.StatusCode != HttpStatusCode.OK) {
                throw new HttpRequestException("Invalid status code " + result.StatusCode, null, result.StatusCode);
            }

            var json = JsonConvert.DeserializeObject<UniversalisData>(await result.Content.ReadAsStringAsync());
            if (json == null) {
                throw new HttpRequestException("Universalis returned null response");
            }

            var items = new Dictionary<uint, MarketBoardData>();
            if (json.items != null) {
                foreach (var (id, item) in json.items) {
                    items.Add(id, ParseMarketBoardData(item, homeWorldId));
                }
            }

            return items;
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Failed to retrieve data from Universalis for itemIds {0}, scope {1}.", itemId, scope);
            return null;
        }
    }

    private MarketBoardData ParseMarketBoardData(ItemData item, uint worldId) {
        var dc = WorldToDc[worldId];
        var marketBoardData = new MarketBoardData {
            LastUploadTime = item.lastUploadTime,
            MinimumPriceNQ = item.listings?.FirstOrDefault(l => !l.hq && l.IsDatacenter(dc)),
            MinimumPriceHQ = item.listings?.FirstOrDefault(l => l.hq && l.IsDatacenter(dc)),
            OwnMinimumPriceNQ = item.listings?.FirstOrDefault(l => !l.hq && l.IsHomeWorld(worldId)),
            OwnMinimumPriceHQ = item.listings?.FirstOrDefault(l => l.hq && l.IsHomeWorld(worldId)),
            RegionMinimumPriceNQ = item.listings?.FirstOrDefault(l => !l.hq),
            RegionMinimumPriceHQ = item.listings?.FirstOrDefault(l => l.hq),
            MostRecentPurchaseNQ = item.recentHistory?.FirstOrDefault(l => !l.hq && l.IsDatacenter(dc)),
            MostRecentPurchaseHQ = item.recentHistory?.FirstOrDefault(l => l.hq && l.IsDatacenter(dc)),
            OwnMostRecentPurchaseNQ = item.recentHistory?.FirstOrDefault(l => !l.hq && l.IsHomeWorld(worldId)),
            OwnMostRecentPurchaseHQ = item.recentHistory?.FirstOrDefault(l => l.hq && l.IsHomeWorld(worldId)),
            RegionMostRecentPurchaseNQ = item.recentHistory?.FirstOrDefault(l => !l.hq),
            RegionMostRecentPurchaseHQ = item.recentHistory?.FirstOrDefault(l => l.hq),
            HomeWorld = Service.DataManager.GetExcelSheet<World>()!.GetRow(worldId)!.Name,
            HomeDatacenter = WorldToDcName[worldId]
        };
        return marketBoardData;
    }
}

// ReSharper disable all
class UniversalisData {
    public Dictionary<uint, ItemData>? items { get; set; }
}

class ItemData {
    public string? dcName { get; set; }

    [JsonConverter(typeof(UnixMilliDateTimeConverter))]
    public DateTime? lastUploadTime { get; set; }

    public List<ListingData>? listings { get; set; }
    public List<RecentData>? recentHistory { get; set; }

    public class ListingData {
        public long pricePerUnit { get; set; }
        public bool hq { get; set; }
        public uint? worldID { get; set; }
        public string? worldName { get; set; }

        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime lastReviewTime { get; set; }

        public bool IsHomeWorld(uint homeWorld) {
            if (worldID == null) // Entry was obtained by searching for prices in the homeWorld
                return true;
            return homeWorld == worldID;
        }

        public bool IsDatacenter(uint dc) {
            if (worldID == null) // Entry was obtained by searching for prices in the homeWorld
                return true;
            return UniversalisClient.WorldToDc[worldID.Value] == dc;
        }

        public static implicit operator Listing?(ListingData? data) {
            if (data == null)
                return null;
            return new Listing {
                Price = data.pricePerUnit,
                Time = data.lastReviewTime,
                World = data.worldName,
                Datacenter = data.worldID != null ? UniversalisClient.WorldToDcName[data.worldID.Value] : null
            };
        }
    }

    public class RecentData {
        public bool hq { get; set; }
        public long pricePerUnit { get; set; }
        public string? worldName { get; set; }
        public uint? worldID { get; set; }

        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime timestamp { get; set; }

        public bool IsHomeWorld(uint homeWorld) {
            if (worldID == null) // Entry was obtained by searching for prices in the homeWorld
                return true;
            return homeWorld == worldID;
        }

        public bool IsDatacenter(uint dc) {
            if (worldID == null) // Entry was obtained by searching for prices in the homeWorld
                return true;
            return UniversalisClient.WorldToDc[worldID.Value] == dc;
        }

        public static implicit operator Listing?(RecentData? data) {
            if (data == null)
                return null;
            return new Listing {
                Price = data.pricePerUnit,
                Time = data.timestamp,
                World = data.worldName,
                Datacenter = data.worldID != null ? UniversalisClient.WorldToDcName[data.worldID.Value] : null
            };
        }
    }
}
// ReSharper restore all
