using System;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Lumina.Excel.GeneratedSheets;
using World = Dalamud.Game.ClientState.Actors.Resolvers.World;

namespace PriceInsight {
    public class ItemPriceLookup {
        private readonly PriceInsightPlugin plugin;
        private readonly MemoryCache cache = new("itemDb");
        private World world;
        private string datacenter;

        public ItemPriceLookup(PriceInsightPlugin plugin) {
            this.plugin = plugin;
        }

        public (MarketBoardData? MarketBoardData, bool IsMarketable) Get(ulong itemid) {
            world ??= plugin.PluginInterface.ClientState.LocalPlayer.HomeWorld;
            datacenter ??= world.GameData.DataCenter.Value.Name;
            var key = itemid.ToString();
            var item = (Task<MarketBoardData?>)cache[key];
            if (item != null && !(item.IsCanceled || item.IsFaulted) && (!item.IsCompleted || item.Result != null)) return (item.IsCompleted ? item.Result : null, true);
            var itemData = plugin.PluginInterface.Data.Excel.GetSheet<Item>().GetRow((uint)itemid);
            if (itemData.ItemSearchCategory.Row == 0) return (null, false);
            item = Task.Run(() => plugin.UniversalisClient.GetMarketBoardData(datacenter, world.Id, itemid));
            cache.Add(key, item, DateTimeOffset.Now.AddSeconds(60));
            return (null, true);
        }
    }
}