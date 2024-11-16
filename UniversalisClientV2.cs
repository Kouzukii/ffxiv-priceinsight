using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Networking.Http;
using Dalamud.Utility;
using Lumina.Excel.Sheets;

namespace PriceInsight;

public class UniversalisClientV2 : IDisposable {
    private static readonly Dictionary<byte, string> Regions = new() { { 1, "Japan" }, { 2, "North-America" }, { 3, "Europe" }, { 4, "Oceania" } };
    internal static readonly Dictionary<uint, (string Name, string DcName, string Region)> WorldLookup = Service.DataManager.GetExcelSheet<World>()
        .ToDictionary(w => w.RowId, w => (w.Name.ExtractText(), w.DataCenter.Value.Name.ExtractText(), Regions.GetValueOrDefault(w.DataCenter.Value.Region) ?? "unknown"));

    private readonly HappyEyeballsCallback happyEyeballsCallback;
    private readonly HttpClient httpClient;

    public UniversalisClientV2() {
        happyEyeballsCallback = new HappyEyeballsCallback();
        httpClient = new HttpClient(new SocketsHttpHandler {
            AutomaticDecompression = DecompressionMethods.All, ConnectCallback = happyEyeballsCallback.ConnectCallback
        });
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"PriceInsight/{Assembly.GetExecutingAssembly().GetName().Version} ({Environment.OSVersion}) Dalamud/{Util.AssemblyVersion}");
    }

    public async Task<Dictionary<uint, MarketBoardData>?> GetMarketBoardDataList(
        uint homeWorldId, ICollection<uint> itemId, CancellationToken cancellationToken) {
        try {
            using var result =
                await httpClient.GetAsync($"https://universalis.app/api/v2/aggregated/{homeWorldId}/{string.Join(',', itemId.Select(i => i.ToString()))}",
                    cancellationToken);

            if (result.StatusCode != HttpStatusCode.OK) {
                throw new HttpRequestException("Invalid status code " + result.StatusCode, null, result.StatusCode);
            }

            await using var responseStream = await result.Content.ReadAsStreamAsync(cancellationToken);
            var json = await JsonSerializer.DeserializeAsync<AggregatedMarketBoardData>(responseStream, cancellationToken: cancellationToken);
            if (json == null) {
                throw new HttpRequestException("Universalis returned null response");
            }

            var items = new Dictionary<uint, MarketBoardData>();
            if (json.results != null) {
                foreach (var item in json.results) {
                    items.Add(item.itemId, item.ToMarketBoardData(homeWorldId));
                }
            }

            return items;
        } catch (Exception ex) {
            Service.PluginLog.Error(ex, "Failed to retrieve data from Universalis for itemIds {0}.", itemId);
            return null;
        }
    }

    public void Dispose() {
        httpClient.Dispose();
        happyEyeballsCallback.Dispose();
    }
}

// ReSharper disable all
file class AggregatedMarketBoardData {
    public List<Result>? results { get; set; }
}

file class Result {
    public uint itemId { get; set; }
    public Aggregate? nq { get; set; }
    public Aggregate? hq { get; set; }
    public List<WorldUploadTime>? worldUploadTimes { get; set; }

    public MarketBoardData ToMarketBoardData(uint worldId) {
        var (worldName, dcName, region) = UniversalisClientV2.WorldLookup[worldId];
        var worldUploadTimes = this.worldUploadTimes != null
            ? this.worldUploadTimes.ToDictionary(w => w.worldId, w => (DateTime)w.timestamp)
            : new Dictionary<uint, DateTime>();
        var worldUploadTime = worldUploadTimes.GetValueOrDefault(worldId, DateTime.Now);
        var marketBoardData = new MarketBoardData {
            HomeWorld = worldName,
            Datacenter = dcName,
            Region = region,
            MinimumPrice = new() {
                World = new() {
                    Nq = this.nq?.minListing?.world?.ToListing(worldUploadTime, worldUploadTimes),
                    Hq = this.hq?.minListing?.world?.ToListing(worldUploadTime, worldUploadTimes),
                },
                Datacenter = new() {
                    Nq = this.nq?.minListing?.dc?.ToListing(worldUploadTime, worldUploadTimes),
                    Hq = this.hq?.minListing?.dc?.ToListing(worldUploadTime, worldUploadTimes),
                },
                Region = new() {
                    Nq = this.nq?.minListing?.region?.ToListing(worldUploadTime, worldUploadTimes),
                    Hq = this.hq?.minListing?.region?.ToListing(worldUploadTime, worldUploadTimes),
                }
            },
            MostRecentPurchase = new() {
                World = new() {
                    Nq = this.nq?.recentPurchase?.world,
                    Hq = this.hq?.recentPurchase?.world,
                },
                Datacenter = new() {
                    Nq = this.nq?.recentPurchase?.dc,
                    Hq = this.hq?.recentPurchase?.dc,
                },
                Region = new() {
                    Nq = this.nq?.recentPurchase?.region,
                    Hq = this.hq?.recentPurchase?.region,
                }
            },
            AverageSalePrice = new() {
                World = new() {
                    Nq = this.nq?.averageSalePrice?.world?.price > 0 ? this.nq?.averageSalePrice?.world?.price : null,
                    Hq = this.hq?.averageSalePrice?.world?.price > 0 ? this.hq?.averageSalePrice?.world?.price : null,
                },
                Datacenter = new() {
                    Nq = this.nq?.averageSalePrice?.dc?.price > 0 ? this.nq?.averageSalePrice?.dc?.price : null,
                    Hq = this.hq?.averageSalePrice?.dc?.price > 0 ? this.hq?.averageSalePrice?.dc?.price : null,
                },
                Region = new() {
                    Nq = this.nq?.averageSalePrice?.region?.price > 0 ? this.nq?.averageSalePrice?.region?.price : null,
                    Hq = this.hq?.averageSalePrice?.region?.price > 0 ? this.hq?.averageSalePrice?.region?.price : null,
                }
            },
            DailySaleVelocity = new() {
                World = new() {
                    Nq = this.nq?.dailySaleVelocity?.world?.quantity > 0 ? this.nq?.dailySaleVelocity?.world?.quantity : null,
                    Hq = this.hq?.dailySaleVelocity?.world?.quantity > 0 ? this.hq?.dailySaleVelocity?.world?.quantity : null,
                },
                Datacenter = new() {
                    Nq = this.nq?.dailySaleVelocity?.dc?.quantity > 0 ? this.nq?.dailySaleVelocity?.dc?.quantity : null,
                    Hq = this.hq?.dailySaleVelocity?.dc?.quantity > 0 ? this.hq?.dailySaleVelocity?.dc?.quantity : null,
                },
                Region = new() {
                    Nq = this.nq?.dailySaleVelocity?.region?.quantity > 0 ? this.nq?.dailySaleVelocity?.region?.quantity : null,
                    Hq = this.hq?.dailySaleVelocity?.region?.quantity > 0 ? this.hq?.dailySaleVelocity?.region?.quantity : null,
                },
            }
        };
        return marketBoardData;
    }
}

file class Aggregate {
    public Value? minListing { get; set; }
    public Value? recentPurchase { get; set; }
    public SaleValue? averageSalePrice { get; set; }
    public Value? dailySaleVelocity { get; set; }
}

file class Value {
    public Entry? world { get; set; }
    public Entry? dc { get; set; }
    public Entry? region { get; set; }
}

file class Entry {
    public int? price { get; set; }
    public uint? worldId { get; set; }
    public UnixMilliDateTime? timestamp { get; set; }
    public double? quantity { get; set; }

    public static implicit operator Listing?(Entry? e) {
        if (e == null || e.price == null)
            return null;
        string? world = null, datacenter = null;
        if (e.worldId != null) {
            (world, datacenter, _) = UniversalisClientV2.WorldLookup[e.worldId.Value];
        }

        return new Listing { Price = e.price.Value, Time = e.timestamp, World = world, Datacenter = datacenter };
    }

    public Listing? ToListing(DateTime defaultTime, Dictionary<uint, DateTime> worldUploadTimes) {
        var listing = (Listing?)this;
        if (listing == null)
            return null;
        var time = worldId != null ? worldUploadTimes.GetValueOrDefault(worldId.Value, defaultTime) : defaultTime;
        return listing with { Time = time };
    }
}

file class SaleValue {
    public SaleEntry? world { get; set; }
    public SaleEntry? dc { get; set; }
    public SaleEntry? region { get; set; }
}

file class SaleEntry {
    public double? price { get; set; }
}

file class WorldUploadTime {
    public required uint worldId { get; set; }
    public required UnixMilliDateTime timestamp { get; set; }
}
// ReSharper restore all
