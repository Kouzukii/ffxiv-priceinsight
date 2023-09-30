using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Lumina.Excel.GeneratedSheets;

namespace PriceInsight;

public sealed class UniversalisClient : IDisposable {
    private readonly HttpClient httpClient =
        new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) {
            Timeout = TimeSpan.FromMilliseconds(60000)
        };

    private const string RequiredFields = "lastUploadTime,listings.pricePerUnit,listings.hq,listings.worldID,listings.lastReviewTime,recentHistory.pricePerUnit,recentHistory.hq,recentHistory.worldID,recentHistory.timestamp,averagePriceNQ,averagePriceHQ,nqSaleVelocity,hqSaleVelocity,regionName,dcName,worldName";
    private const string RequiredFieldsMulti = "items.lastUploadTime,items.listings.pricePerUnit,items.listings.hq,items.listings.worldID,items.listings.lastReviewTime,items.recentHistory.pricePerUnit,items.recentHistory.hq,items.recentHistory.worldID,items.recentHistory.timestamp,items.averagePriceNQ,items.averagePriceHQ,items.nqSaleVelocity,items.hqSaleVelocity,items.regionName,items.dcName,items.worldName";

    internal static readonly Dictionary<uint, (string Name, uint Dc, string DcName)> WorldLookup = Service.DataManager.GetExcelSheet<World>()!.Where(w => w.IsPublic)
        .ToDictionary(w => w.RowId, w => (w.Name.RawString, w.DataCenter.Row, w.DataCenter.Value!.Name.RawString));

    public void Dispose() {
        httpClient.Dispose();
    }

    public async Task<MarketBoardData?> GetMarketBoardData(string scope, uint homeWorldId, ulong itemId) {
        try {
            using var result = await httpClient.GetAsync($"https://universalis.app/api/v2/{scope}/{itemId}?fields={RequiredFields}");
            
            if (result.StatusCode != HttpStatusCode.OK) {
                throw new HttpRequestException("Invalid status code " + result.StatusCode, null, result.StatusCode);
            }

            await using var responseStream = await result.Content.ReadAsStreamAsync();
            var item = await JsonSerializer.DeserializeAsync<ItemData>(responseStream);
            if (item == null) {
                throw new HttpRequestException("Universalis returned null response");
            }

            return ParseMarketBoardData(item, homeWorldId);
        } catch (Exception ex) {
            Service.PluginLog.Error(ex, "Failed to retrieve data from Universalis for itemId {0}, scope {1}.", itemId, scope);
            return null;
        }
    }

    public async Task<Dictionary<uint, MarketBoardData>?> GetMarketBoardDataList(string scope, uint homeWorldId, List<uint> itemId) {
        // when only 1 item is queried, Universalis doesn't respond with an array
        if (itemId.Count == 1) {
            if (await GetMarketBoardData(scope, homeWorldId, itemId[0]) is { } data)
                return new Dictionary<uint, MarketBoardData> {{ itemId[0], data }};
            return null;
        }

        try {
            using var result = await httpClient.GetAsync($"https://universalis.app/api/v2/{scope}/{string.Join(',', itemId.Select(i => i.ToString()))}?fields={RequiredFieldsMulti}");

            if (result.StatusCode != HttpStatusCode.OK) {
                throw new HttpRequestException("Invalid status code " + result.StatusCode, null, result.StatusCode);
            }

            await using var responseStream = await result.Content.ReadAsStreamAsync();
            var json = await JsonSerializer.DeserializeAsync<UniversalisData>(responseStream);
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
            Service.PluginLog.Error(ex, "Failed to retrieve data from Universalis for itemIds {0}, scope {1}.", itemId, scope);
            return null;
        }
    }

    private MarketBoardData ParseMarketBoardData(ItemData item, uint worldId) {
        var dc = WorldLookup[worldId].Dc;
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
            HomeDatacenter = WorldLookup[worldId].DcName,
            Scope = item.regionName ?? item.dcName ?? item.worldName ?? "World",
            AverageSalePriceNQ = item.averagePriceNQ > 0 ? item.averagePriceNQ : null,
            AverageSalePriceHQ = item.averagePriceHQ > 0 ? item.averagePriceHQ : null,
            DailySaleVelocityNQ = item.nqSaleVelocity > 0 ? item.nqSaleVelocity : null,
            DailySaleVelocityHQ = item.hqSaleVelocity > 0 ? item.hqSaleVelocity : null
        };
        return marketBoardData;
    }
}

// ReSharper disable all
class UniversalisData {
    public Dictionary<uint, ItemData>? items { get; set; }
}

class ItemData {
    [JsonConverter(typeof(UnixMilliDateTimeConverter))]
    public DateTime? lastUploadTime { get; set; }

    public List<ListingData>? listings { get; set; }
    public List<RecentData>? recentHistory { get; set; }
    public double averagePriceNQ { get; set; }
    public double averagePriceHQ { get; set; }
    public double nqSaleVelocity { get; set; }
    public double hqSaleVelocity { get; set; }
    public string? regionName { get; set; }
    public string? dcName { get; set; }
    public string? worldName { get; set; }

    public class ListingData {
        public long pricePerUnit { get; set; }
        public bool hq { get; set; }
        public uint? worldID { get; set; }

        [JsonConverter(typeof(UnixSecondsDateTimeConverter))]
        public DateTime lastReviewTime { get; set; }

        public bool IsHomeWorld(uint homeWorld) {
            if (worldID == null) // Entry was obtained by searching for prices in the homeWorld
                return true;
            return homeWorld == worldID;
        }

        public bool IsDatacenter(uint dc) {
            if (worldID == null) // Entry was obtained by searching for prices in the homeWorld
                return true;
            return UniversalisClient.WorldLookup[worldID.Value].Dc == dc;
        }

        public static implicit operator Listing?(ListingData? data) {
            if (data == null)
                return null;
            var world = data.worldID != null ? UniversalisClient.WorldLookup[data.worldID.Value] : default;
            return new Listing {
                Price = data.pricePerUnit,
                Time = data.lastReviewTime,
                World = world.Name,
                Datacenter = world.DcName
            };
        }
    }

    public class RecentData {
        public bool hq { get; set; }
        public long pricePerUnit { get; set; }
        public uint? worldID { get; set; }

        [JsonConverter(typeof(UnixSecondsDateTimeConverter))]
        public DateTime timestamp { get; set; }

        public bool IsHomeWorld(uint homeWorld) {
            if (worldID == null) // Entry was obtained by searching for prices in the homeWorld
                return true;
            return homeWorld == worldID;
        }

        public bool IsDatacenter(uint dc) {
            if (worldID == null) // Entry was obtained by searching for prices in the homeWorld
                return true;
            return UniversalisClient.WorldLookup[worldID.Value].Dc == dc;
        }

        public static implicit operator Listing?(RecentData? data) {
            if (data == null)
                return null;
            var world = data.worldID != null ? UniversalisClient.WorldLookup[data.worldID.Value] : default;
            return new Listing {
                Price = data.pricePerUnit,
                Time = data.timestamp,
                World = world.Name,
                Datacenter = world.DcName
            };
        }
    }
}
// ReSharper restore all
