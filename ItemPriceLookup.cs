using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Resolvers;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;

namespace PriceInsight; 

public class ItemPriceLookup : IDisposable {
    private readonly PriceInsightPlugin plugin;
    private readonly MemoryCache cache = new("itemDb");
    private ExcelResolver<World>? world;
    private string? datacenter;

    public ItemPriceLookup(PriceInsightPlugin plugin) {
        this.plugin = plugin;
    }
        
    public bool IsReady {
        get {
            world ??= plugin.ClientState.LocalPlayer?.HomeWorld;
            datacenter ??= world?.GameData?.DataCenter.Value?.Name.RawString;
            return world != null && datacenter != null;
        }
    }

    public (MarketBoardData? MarketBoardData, bool IsMarketable) Get(ulong itemId) {
        if (world == null || datacenter == null)
            return (null, true);
        var key = itemId.ToString();
        var item = (Task<MarketBoardData?>?)cache[key];
        if (item != null && !(item.IsCanceled || item.IsFaulted) && (!item.IsCompleted || item.Result != null))
            return (item.IsCompleted ? item.Result : null, true);
        var itemData = plugin.DataManager.Excel.GetSheet<Item>()?.GetRow((uint)itemId);
        if (itemData != null && itemData.ItemSearchCategory.Row == 0)
            return (null, false);
        item = Task.Run(() => plugin.UniversalisClient.GetMarketBoardData(datacenter, world.Id, itemId));
        cache.Add(key, item, DateTimeOffset.Now.AddMinutes(90));
        return (null, true);
    }

    public void Prefetch(IEnumerable<uint> items) {
        if (world == null || datacenter == null)
            return;
            
        var itemIds = (from id in items 
                let item = (Task<MarketBoardData?>?)cache[id.ToString()] 
                where item == null || item.IsCanceled || item.IsFaulted || (item.IsCompleted && item.Result == null) 
                let itemData = plugin.DataManager.Excel.GetSheet<Item>()?.GetRow(id) 
                where itemData != null && itemData.ItemSearchCategory.Row != 0 
                select (ulong)id)
            .ToList();

        if (itemIds.Count == 0)
            return;
        var itemTask = Task.Run(() => plugin.UniversalisClient.GetMarketBoardDataList(datacenter, world.Id, itemIds))
            .ContinueWith(task => {
                if (task.Exception != null)
                    PluginLog.Warning(task.Exception, "Error while prefetching items");
                return task.Result;
            });
        foreach (var id in itemIds) {
            cache.Add(id.ToString(), itemTask.ContinueWith(task => task.Result?.GetValueOrDefault(id)), DateTimeOffset.Now.AddMinutes(90));
        }
    }

    public void Dispose() {
        cache.Dispose();
    }
}