using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;

namespace PriceInsight;

public sealed class UniversalisClient(PriceInsightPlugin plugin) : IDisposable {
    private HttpClient httpClient = CreateHttpClient(plugin.Configuration.ForceIpv4);

    private const string RequiredFields =
        "lastUploadTime,listings.pricePerUnit,listings.hq,listings.worldID,recentHistory.pricePerUnit,recentHistory.hq,recentHistory.worldID,recentHistory.timestamp,averagePriceNQ,averagePriceHQ,nqSaleVelocity,hqSaleVelocity,regionName,dcName,worldName,worldUploadTimes";

    private const string RequiredFieldsMulti =
        "items.lastUploadTime,items.listings.pricePerUnit,items.listings.hq,items.listings.worldID,items.recentHistory.pricePerUnit,items.recentHistory.hq,items.recentHistory.worldID,items.recentHistory.timestamp,items.averagePriceNQ,items.averagePriceHQ,items.nqSaleVelocity,items.hqSaleVelocity,items.regionName,items.dcName,items.worldName,items.worldUploadTimes";


    internal static readonly Dictionary<uint, (string Name, uint Dc, string DcName)> WorldLookup = Service.DataManager.GetExcelSheet<World>()!
        .ToDictionary(w => w.RowId, w => (w.Name.RawString, w.DataCenter.Row, w.DataCenter.Value?.Name.RawString ?? "unknown"));

    private static HttpClient CreateHttpClient(bool forceIpv4) {
        var client = new HttpClient(new SocketsHttpHandler {
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
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"PriceInsight/{Assembly.GetExecutingAssembly().GetName().Version} ({Environment.OSVersion}) Dalamud/{Util.AssemblyVersion}");
        return client;
    }

    public void ForceIpv4(bool force) {
        var oldHttpClient = httpClient;
        httpClient = CreateHttpClient(force);
        oldHttpClient.Dispose();
    }

    public void Dispose() {
        httpClient.Dispose();
    }

    public async Task<MarketBoardData?> GetMarketBoardData(string scope, uint homeWorldId, ulong itemId, CancellationToken cancellationToken) {
        try {
            using var result = await httpClient.GetAsync($"https://universalis.app/api/v2/{scope}/{itemId}?fields={RequiredFields}", cancellationToken);
            if (result.StatusCode != HttpStatusCode.OK) {
                throw new HttpRequestException("Invalid status code " + result.StatusCode, null, result.StatusCode);
            }

            await using var responseStream = await result.Content.ReadAsStreamAsync(cancellationToken);
            var item = await JsonSerializer.DeserializeAsync<ItemData>(responseStream, cancellationToken: cancellationToken);
            if (item == null) {
                throw new HttpRequestException("Universalis returned null response");
            }

            return item.ToMarketBoardData(homeWorldId);
        } catch (Exception ex) {
            Service.PluginLog.Error(ex, "Failed to retrieve data from Universalis for itemId {0}, scope {1}.", itemId, scope);
            return null;
        }
    }

    public async Task<Dictionary<uint, MarketBoardData>?> GetMarketBoardDataList(
        string scope, uint homeWorldId, ICollection<uint> itemId, CancellationToken cancellationToken) {
        // when only 1 item is queried, Universalis doesn't respond with an array
        if (itemId.Count == 1) {
            if (await GetMarketBoardData(scope, homeWorldId, itemId.First(), cancellationToken) is { } data)
                return new Dictionary<uint, MarketBoardData> { { itemId.First(), data } };
            return null;
        }

        try {
            using var result =
                await httpClient.GetAsync(
                    $"https://universalis.app/api/v2/{scope}/{string.Join(',', itemId.Select(i => i.ToString()))}?fields={RequiredFieldsMulti}",
                    cancellationToken);

            if (result.StatusCode != HttpStatusCode.OK) {
                throw new HttpRequestException("Invalid status code " + result.StatusCode, null, result.StatusCode);
            }

            await using var responseStream = await result.Content.ReadAsStreamAsync(cancellationToken);
            var json = await JsonSerializer.DeserializeAsync<UniversalisData>(responseStream, cancellationToken: cancellationToken);
            if (json == null) {
                throw new HttpRequestException("Universalis returned null response");
            }

            var items = new Dictionary<uint, MarketBoardData>();
            if (json.items != null) {
                foreach (var (id, item) in json.items) {
                    items.Add(id, item.ToMarketBoardData(homeWorldId));
                }
            }

            return items;
        } catch (Exception ex) {
            Service.PluginLog.Error(ex, "Failed to retrieve data from Universalis for itemIds {0}, scope {1}.", itemId, scope);
            return null;
        }
    }
}

// ReSharper disable all
file class UniversalisData {
    public Dictionary<uint, ItemData>? items { get; set; }
}

file class ItemData {
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

    public MarketBoardData ToMarketBoardData(uint worldId) {
        var dc = UniversalisClient.WorldLookup[worldId].Dc;
        var marketBoardData = new MarketBoardData {
            MinimumPriceNQ = this.listings?.FirstOrDefault(l => !l.hq && l.IsDatacenter(dc))?.ToListing(this),
            MinimumPriceHQ = this.listings?.FirstOrDefault(l => l.hq && l.IsDatacenter(dc))?.ToListing(this),
            OwnMinimumPriceNQ = this.listings?.FirstOrDefault(l => !l.hq && l.IsHomeWorld(worldId))?.ToListing(this),
            OwnMinimumPriceHQ = this.listings?.FirstOrDefault(l => l.hq && l.IsHomeWorld(worldId))?.ToListing(this),
            RegionMinimumPriceNQ = this.listings?.FirstOrDefault(l => !l.hq)?.ToListing(this),
            RegionMinimumPriceHQ = this.listings?.FirstOrDefault(l => l.hq)?.ToListing(this),
            MostRecentPurchaseNQ = this.recentHistory?.FirstOrDefault(l => !l.hq && l.IsDatacenter(dc)),
            MostRecentPurchaseHQ = this.recentHistory?.FirstOrDefault(l => l.hq && l.IsDatacenter(dc)),
            OwnMostRecentPurchaseNQ = this.recentHistory?.FirstOrDefault(l => !l.hq && l.IsHomeWorld(worldId)),
            OwnMostRecentPurchaseHQ = this.recentHistory?.FirstOrDefault(l => l.hq && l.IsHomeWorld(worldId)),
            RegionMostRecentPurchaseNQ = this.recentHistory?.FirstOrDefault(l => !l.hq),
            RegionMostRecentPurchaseHQ = this.recentHistory?.FirstOrDefault(l => l.hq),
            HomeWorld = Service.DataManager.GetExcelSheet<World>()!.GetRow(worldId)!.Name,
            HomeDatacenter = UniversalisClient.WorldLookup[worldId].DcName,
            Scope = this.regionName ?? this.dcName ?? this.worldName ?? "World",
            AverageSalePriceNQ = this.averagePriceNQ > 0 ? this.averagePriceNQ : null,
            AverageSalePriceHQ = this.averagePriceHQ > 0 ? this.averagePriceHQ : null,
            DailySaleVelocityNQ = this.nqSaleVelocity > 0 ? this.nqSaleVelocity : null,
            DailySaleVelocityHQ = this.hqSaleVelocity > 0 ? this.hqSaleVelocity : null
        };
        return marketBoardData;
    }
}

file class ListingData {
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

    public Listing ToListing(ItemData data) {
        var world = worldID != null ? UniversalisClient.WorldLookup[worldID.Value] : default;
        var time = worldID != null ? data.worldUploadTimes?[worldID.Value] : data.lastUploadTime;
        return new Listing { Price = pricePerUnit, Time = time, World = world.Name, Datacenter = world.DcName };
    }
}

file class RecentData {
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
        return new Listing { Price = data.pricePerUnit, Time = data.timestamp, World = world.Name, Datacenter = world.DcName };
    }
}
// ReSharper restore all
