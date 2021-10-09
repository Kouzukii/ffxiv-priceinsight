using System;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Resolvers;
using Lumina.Excel.GeneratedSheets;

namespace PriceInsight {
    public class ItemPriceLookup {
        private readonly PriceInsightPlugin plugin;
        private readonly MemoryCache cache = new("itemDb");
        private ExcelResolver<World>? world;
        private string? datacenter;

        public ItemPriceLookup(PriceInsightPlugin plugin) {
            this.plugin = plugin;
        }

        public (MarketBoardData? MarketBoardData, bool IsMarketable) Get(ulong itemid) {
            world ??= plugin.ClientState.LocalPlayer?.HomeWorld;
            datacenter ??= world?.GameData.DataCenter.Value?.Name.RawString;
            if (world == null || datacenter == null) return (null, true);
            var key = itemid.ToString();
            var item = (Task<MarketBoardData?>?)cache[key];
            if (item != null && !(item.IsCanceled || item.IsFaulted) && (!item.IsCompleted || item.Result != null))
                return (item.IsCompleted ? item.Result : null, true);
            var itemData = plugin.DataManager.Excel.GetSheet<Item>()?.GetRow((uint)itemid);
            if (itemData != null && itemData.ItemSearchCategory.Row == 0) return (null, false);
            item = Task.Run(() => plugin.UniversalisClient.GetMarketBoardData(datacenter, world.Id, itemid));
            cache.Add(key, item, DateTimeOffset.Now.AddSeconds(60));
            return (null, true);
        }
    }
}