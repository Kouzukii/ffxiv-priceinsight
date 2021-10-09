using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Logging;
using Newtonsoft.Json;

namespace PriceInsight {
    public class UniversalisClient {
        private const string Endpoint = "https://universalis.app/api/";
        private readonly HttpClient httpClient;

        public UniversalisClient() {
            httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(2000) };
        }

        public void Dispose() {
            httpClient.Dispose();
        }

        public async Task<MarketBoardData?>? GetMarketBoardData(string datacenter, uint worldId, ulong itemId) {
            HttpResponseMessage result;
            try {
                result = await httpClient.GetAsync(Endpoint + "/" + datacenter + "/" + itemId);
            } catch (Exception ex) {
                PluginLog.LogError(ex, "Failed to retrieve data from Universalis for itemId {0} / dc {1}.", itemId, datacenter);
                return null;
            }

            if (result.StatusCode != HttpStatusCode.OK) {
                PluginLog.LogError("Failed to retrieve data from Universalis for itemId {0} / dc {1} with sc {2}.", itemId, datacenter, result.StatusCode);
                return null;
            }

            var json = JsonConvert.DeserializeObject<UniversalisData>(await result.Content.ReadAsStringAsync());
            if (json == null) {
                PluginLog.LogError("Failed to deserialize Universalis response for itemId {0} / dc {1}.", itemId, datacenter);
                return null;
            }

            var cheapestNQ = json.listings?.FirstOrDefault(l => !(l.hq ?? true));
            var cheapestHQ = json.listings?.FirstOrDefault(l => l.hq ?? false);
            var ownCheapestNQ = json.listings?.FirstOrDefault(l => !(l.hq ?? true) && l.worldID == worldId);
            var ownCheapestHQ = json.listings?.FirstOrDefault(l => (l.hq ?? false) && l.worldID == worldId);
            var recentNQ = json.recentHistory?.FirstOrDefault(l => !(l.hq ?? true));
            var recentHQ = json.recentHistory?.FirstOrDefault(l => l.hq ?? false);
            var ownRecentNQ = json.recentHistory?.FirstOrDefault(l => !(l.hq ?? true) && l.worldID == worldId);
            var ownRecentHQ = json.recentHistory?.FirstOrDefault(l => (l.hq ?? false) && l.worldID == worldId);
            var marketBoardData = new MarketBoardData {
                LastUploadTime = json.lastUploadTime ?? 0,
                MinimumPriceNQ = cheapestNQ?.pricePerUnit ?? 0,
                MinimumPriceWorldNQ = cheapestNQ?.worldName,
                MinimumPriceHQ = cheapestHQ?.pricePerUnit ?? 0,
                MinimumPriceWorldHQ = cheapestHQ?.worldName,
                OwnMinimumPriceNQ = ownCheapestNQ?.pricePerUnit ?? 0,
                OwnMinimumPriceHQ = ownCheapestHQ?.pricePerUnit ?? 0,
                OwnWorld = ownCheapestNQ?.worldName ?? ownCheapestHQ?.worldName,
                MostRecentPurchaseNQ = recentNQ?.pricePerUnit ?? 0,
                MostRecentPurchaseHQ = recentHQ?.pricePerUnit ?? 0,
                MostRecentPurchaseWorldNQ = recentNQ?.worldName,
                MostRecentPurchaseWorldHQ = recentHQ?.worldName,
                OwnMostRecentPurchaseNQ = ownRecentNQ?.pricePerUnit ?? 0,
                OwnMostRecentPurchaseHQ = ownRecentHQ?.pricePerUnit ?? 0,
            };
            return marketBoardData;
        }
    }

    // ReSharper disable all
    class UniversalisData {
        public string? dcName { get; set; }
        public long? lastUploadTime { get; set; }
        public List<Listing>? listings { get; set; }
        public List<Recent>? recentHistory { get; set; }

        public class Listing {
            public long? pricePerUnit { get; set; }
            public bool? hq { get; set; }
            public ulong? worldID { get; set; }
            public string? worldName { get; set; }
        }

        public class Recent {
            public bool? hq { get; set; }
            public long? pricePerUnit { get; set; }
            public string? worldName { get; set; }
            public ulong? worldID { get; set; }
        }
    }
    // ReSharper restore all
}