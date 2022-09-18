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
    private readonly HashSet<uint> requestedItems = new();
    private DateTime lastRequest = DateTime.UnixEpoch;

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

    public (MarketBoardData? MarketBoardData, bool IsMarketable) Get(uint itemId, bool refresh) {
        if (!refresh) {
            var key = itemId.ToString();
            var item = (Task<MarketBoardData?>?)cache[key];
            if (item != null && !(item.IsCanceled || item.IsFaulted) && (!item.IsCompleted || item.Result != null))
                return (item.IsCompleted ? item.Result : null, true);
        }

        var itemData = plugin.DataManager.Excel.GetSheet<Item>()?.GetRow(itemId);
        if (itemData != null && itemData.ItemSearchCategory.Row == 0)
            return (null, false);
        // Don't spam universalis with requests
        if ((DateTime.Now - lastRequest).TotalMilliseconds < 500) {
            lock (requestedItems) {
                if (requestedItems.Add(itemId) && requestedItems.Count == 1)
                    Task.Run(BufferFetch);
            }
        } else {
            Fetch(new[] { itemId }, true);
        }

        lastRequest = DateTime.Now;

        return (null, true);
    }

    private async void BufferFetch() {
        await Task.Delay(500);
        lock (requestedItems) {
            Fetch(requestedItems, false);
            requestedItems.Clear();
        }
    }

    public void Fetch(IEnumerable<uint> items, bool skipCheck) {
        if (world == null || datacenter == null)
            return;

        var itemSheet = plugin.DataManager.Excel.GetSheet<Item>();
        var itemIds = skipCheck
            ? items.ToList()
            : (from id in items
                let item = (Task<MarketBoardData?>?)cache[id.ToString()]
                where item == null || item.IsCanceled || item.IsFaulted || (item.IsCompleted && item.Result == null)
                let itemData = itemSheet?.GetRow(id)
                where itemData != null && itemData.ItemSearchCategory.Row != 0
                select id).ToList();

        if (itemIds.Count == 0)
            return;

        var itemTask = Task.Run(async () => {
            try {
#if DEBUG
                var fetchStart = DateTime.Now;
#endif
                var result = await plugin.UniversalisClient.GetMarketBoardDataList(datacenter, world.Id, itemIds);
                if (result != null)
                    plugin.ItemPriceTooltip.Refresh(result);
#if DEBUG
                PluginLog.Information($"Fetching {itemIds.Count} items took {(DateTime.Now - fetchStart).TotalMilliseconds:F0}ms");
#endif
                return result;
            } catch (Exception e) {
                PluginLog.Warning(e, $"Error while fetching {itemIds.Count} items");
                throw;
            }
        });

        foreach (var id in itemIds) {
            cache.Set(id.ToString(), itemTask.ContinueWith(task => task.Result?.GetValueOrDefault(id), TaskContinuationOptions.OnlyOnRanToCompletion),
                DateTimeOffset.Now.AddMinutes(90));
        }
    }

    public void Dispose() {
        cache.Dispose();
    }
}
