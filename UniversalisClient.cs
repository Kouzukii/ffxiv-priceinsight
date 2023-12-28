using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Lumina.Excel.GeneratedSheets;

namespace PriceInsight;

public sealed class UniversalisClient : IDisposable {
    private HttpClient httpClient;

    private const string RequiredFields = "lastUploadTime,listings.pricePerUnit,listings.hq,listings.worldID,recentHistory.pricePerUnit,recentHistory.hq,recentHistory.worldID,recentHistory.timestamp,averagePriceNQ,averagePriceHQ,nqSaleVelocity,hqSaleVelocity,regionName,dcName,worldName,worldUploadTimes";
    private const string RequiredFieldsMulti = "items.lastUploadTime,items.listings.pricePerUnit,items.listings.hq,items.listings.worldID,items.recentHistory.pricePerUnit,items.recentHistory.hq,items.recentHistory.worldID,items.recentHistory.timestamp,items.averagePriceNQ,items.averagePriceHQ,items.nqSaleVelocity,items.hqSaleVelocity,items.regionName,items.dcName,items.worldName,items.worldUploadTimes";

    internal static readonly Dictionary<uint, (string Name, uint Dc, string DcName)> WorldLookup = Service.DataManager.GetExcelSheet<World>()!.Where(w => w.IsPublic)
        .ToDictionary(w => w.RowId, w => (w.Name.RawString, w.DataCenter.Row, w.DataCenter.Value!.Name.RawString));

    public UniversalisClient(PriceInsightPlugin plugin) {
        httpClient = CreateHttpClient(plugin.Configuration.ForceIpv4);
    }
    
    private static HttpClient CreateHttpClient(bool forceIpv4) {
        return new HttpClient(new SocketsHttpHandler {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = forceIpv4
                // taken from https://github.com/dotnet/runtime/blob/b4ba5da5a0b8e0c7e3027a695f2acb2d9d19137b/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/HttpConnectionPool.cs#L1621C47-L1621C47
                // with socket fixed to Ipv4
                ? async (context, token) => {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try {
                        await socket.ConnectAsync(context.DnsEndPoint, token).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    } catch {
                        socket.Dispose();
                        throw;
                    }
                }
                : null
        }) { Timeout = TimeSpan.FromSeconds(60) };
    }

    public void ForceIpv4(bool force) {
        var oldHttpClient = httpClient;
        httpClient = CreateHttpClient(force);
        oldHttpClient.Dispose();
    }
    
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
            MinimumPriceNQ = item.listings?.FirstOrDefault(l => !l.hq && l.IsDatacenter(dc))?.WithUploadTime(item),
            MinimumPriceHQ = item.listings?.FirstOrDefault(l => l.hq && l.IsDatacenter(dc))?.WithUploadTime(item),
            OwnMinimumPriceNQ = item.listings?.FirstOrDefault(l => !l.hq && l.IsHomeWorld(worldId))?.WithUploadTime(item),
            OwnMinimumPriceHQ = item.listings?.FirstOrDefault(l => l.hq && l.IsHomeWorld(worldId))?.WithUploadTime(item),
            RegionMinimumPriceNQ = item.listings?.FirstOrDefault(l => !l.hq)?.WithUploadTime(item),
            RegionMinimumPriceHQ = item.listings?.FirstOrDefault(l => l.hq)?.WithUploadTime(item),
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
    public UnixMilliDateTime? lastUploadTime { get; set; }

    public List<ListingData>? listings { get; set; }
    public List<RecentData>? recentHistory { get; set; }
    public double averagePriceNQ { get; set; }
    public double averagePriceHQ { get; set; }
    public double nqSaleVelocity { get; set; }
    public double hqSaleVelocity { get; set; }
    public string? regionName { get; set; }
    public string? dcName { get; set; }
    public string? worldName { get; set; }
    public Dictionary<uint, UnixMilliDateTime>? worldUploadTimes { get; set; }

    public class ListingData {
        public long pricePerUnit { get; set; }
        public bool hq { get; set; }
        public uint? worldID { get; set; }

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

        public Listing WithUploadTime(ItemData data) {
            var world = worldID != null ? UniversalisClient.WorldLookup[worldID.Value] : default;
            var time = worldID != null ? data.worldUploadTimes?[worldID.Value] : data.lastUploadTime;
            return new Listing {
                Price = pricePerUnit,
                Time = time,
                World = world.Name,
                Datacenter = world.DcName
            };
        }
    }

    public class RecentData {
        public bool hq { get; set; }
        public long pricePerUnit { get; set; }
        public uint? worldID { get; set; }
        public UnixSecondDateTime? timestamp { get; set; }

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
