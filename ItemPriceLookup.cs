using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EasyCaching.InMemory;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace PriceInsight;

public class ItemPriceLookup(PriceInsightPlugin plugin) : IDisposable {
    private readonly InMemoryCaching cache = new("prices", new InMemoryCachingOptions { EnableReadDeepClone = false });
    private World? homeWorld;
    private readonly HashSet<uint> requestedItems = [];
    private readonly ConcurrentDictionary<uint, Task> activeTasks = new();
    private DateTime lastRequest = DateTime.UnixEpoch;
    private readonly Dictionary<byte, string> regions = new() { { 1, "Japan" }, { 2, "North-America" }, { 3, "Europe" }, { 4, "Oceania" } };

    public bool CheckReady() {
        if (plugin.Configuration.UseCurrentWorld) {
            homeWorld ??= Service.ClientState.LocalPlayer?.CurrentWorld.GameData;
        } else {
            homeWorld ??= Service.ClientState.LocalPlayer?.HomeWorld.GameData;
        }

        return homeWorld != null;
    }

    public bool NeedsClearing {
        get {
            if (plugin.Configuration.UseCurrentWorld && homeWorld != null) {
                return Service.ClientState.LocalPlayer?.CurrentWorld.Id != homeWorld.RowId;
            }

            return false;
        }
    }

    public (MarketBoardData? MarketBoardData, LookupState State) Get(ulong fullItemId, bool refresh) {
        if (!ToMarketableItemId(fullItemId, out var itemId))
            return (null, LookupState.NonMarketable);

        if (refresh) {
            cache.Remove(itemId.ToString());
        } else {
            if (cache.Get<MarketBoardData>(itemId.ToString()) is { IsNull: false, Value: var mbData })
                return (mbData, LookupState.Marketable);
            if (activeTasks.TryGetValue(itemId, out var task))
                return (null, task.IsFaulted ? LookupState.Faulted : LookupState.Marketable);
        }

        // Don't spam universalis with requests
        if ((DateTime.Now - lastRequest).TotalMilliseconds < 500) {
            lock (requestedItems) {
                if (requestedItems.Add(itemId) && requestedItems.Count == 1)
                    Task.Run(BufferFetch);
            }
        } else {
            FetchInternal(new[] { itemId });
        }

        lastRequest = DateTime.Now;

        return (null, LookupState.Marketable);
    }

    private async void BufferFetch() {
        await Task.Delay(500);
        lock (requestedItems) {
            Fetch(requestedItems);
            requestedItems.Clear();
        }
    }

    private static bool ToMarketableItemId(ulong fullItemId, out uint itemId, ExcelSheet<Item>? sheet = null) {
        itemId = (uint)(fullItemId % 500000);
        if (fullItemId is >= 2000000 or >= 500000 and < 1000000)
            return false;
        sheet ??= Service.DataManager.Excel.GetSheet<Item>();
        return sheet?.GetRow(itemId) is not null and not { ItemSearchCategory.Row: 0 };
    }

    private IEnumerable<uint> FilterItemsToFetch(IEnumerable<uint> items) {
        var itemSheet = Service.DataManager.Excel.GetSheet<Item>();
        foreach (var id in items) {
            if (cache.Get(id.ToString()) != null || (activeTasks.TryGetValue(id, out var task) && !task.IsFaulted))
                continue;

            if (ToMarketableItemId(id, out var itemId, itemSheet))
                yield return itemId;
        }
    }

    public void Fetch(IEnumerable<uint> items) {
        var itemIds = FilterItemsToFetch(items).Distinct().ToList();

        if (itemIds.Count == 0)
            return;

        FetchInternal(itemIds);
    }

    private void FetchInternal(IList<uint> itemIds) {
        var itemTask = FetchItemTask();

        foreach (var id in itemIds) {
            activeTasks[id] = Task.Run(async () => {
                var items = await itemTask;
                if (items != null && items.TryGetValue(id, out var value))
                    cache.Set(id.ToString(), value, TimeSpan.FromMinutes(90));
                activeTasks.TryRemove(id, out _);
            });
        }

        return;

        async Task<Dictionary<uint, MarketBoardData>?> FetchItemTask() {
            if (Scope() is not { } scope || homeWorld?.RowId is not { } homeWorldId)
                return null;
            var fetchStart = DateTime.Now;
            var result = await plugin.UniversalisClient.GetMarketBoardDataList(scope, homeWorldId, itemIds);
            if (result != null)
                plugin.ItemPriceTooltip.Refresh(result);
            else
                plugin.ItemPriceTooltip.FetchFailed(itemIds);
            Service.PluginLog.Debug($"Fetching {itemIds.Count} items took {(DateTime.Now - fetchStart).TotalMilliseconds:F0}ms");
            return result;
        }
    }

    private string? Scope() {
        if (plugin.Configuration.ShowRegion || plugin.Configuration.ShowMostRecentPurchaseRegion) {
            if (homeWorld?.DataCenter?.Value?.Region is { } region)
                return regions[region];
            return null;
        }

        if (plugin.Configuration.ShowDatacenter || plugin.Configuration.ShowMostRecentPurchase) {
            return homeWorld?.DataCenter?.Value?.Name.RawString;
        }

        return homeWorld?.Name.RawString;
    }

    public void Dispose() {
        cache.Clear();
    }
}
