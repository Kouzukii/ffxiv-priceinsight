using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;

namespace PriceInsight;

public class ItemPriceLookup : IDisposable {
    private readonly PriceInsightPlugin plugin;
    private readonly MemoryCache cache = new("itemDb");
    private World? homeWorld;
    private readonly HashSet<uint> requestedItems = new();
    private DateTime lastRequest = DateTime.UnixEpoch;
    private readonly Dictionary<byte, string> regions = new() { { 1, "Japan" }, { 2, "North-America" }, { 3, "Europe" }, { 4, "Oceania" } };

    public ItemPriceLookup(PriceInsightPlugin plugin) {
        this.plugin = plugin;
    }

    public bool IsReady {
        get {
            if (plugin.Configuration.UseCurrentWorld) {
                homeWorld ??= Service.ClientState.LocalPlayer?.CurrentWorld.GameData;
            } else {
                homeWorld ??= Service.ClientState.LocalPlayer?.HomeWorld.GameData;
            }

            return homeWorld != null;
        }
    }

    public bool NeedsClearing {
        get {
            if (plugin.Configuration.UseCurrentWorld && homeWorld != null) {
                return Service.ClientState.LocalPlayer?.CurrentWorld.Id != homeWorld.RowId;
            }

            return false;
        }
    }

    public (MarketBoardData? MarketBoardData, LookupState State) Get(uint itemId, bool refresh) {
        if (!refresh) {
            var key = itemId.ToString();
            var item = (Task<MarketBoardData?>?)cache[key];
            if (item != null)
                return (
                    item.IsCompleted ? item.Result : null, 
                    item is { IsCompleted: true, Result: null } ? LookupState.Faulted : LookupState.Marketable
                );
        }

        var itemData = Service.DataManager.Excel.GetSheet<Item>()?.GetRow(itemId);
        if (itemData != null && itemData.ItemSearchCategory.Row == 0)
            return (null, LookupState.NonMarketable);
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

        return (null, LookupState.Marketable);
    }

    private async void BufferFetch() {
        await Task.Delay(500);
        lock (requestedItems) {
            Fetch(requestedItems, false);
            requestedItems.Clear();
        }
    }

    public void Fetch(IEnumerable<uint> items, bool skipCheck) {
        if (Scope() is not { } scope || homeWorld?.RowId is not { } homeWorldId)
            return;

        var itemSheet = Service.DataManager.Excel.GetSheet<Item>();
        var itemIds = skipCheck
            ? items.ToList()
            : (from id in items
                let item = (Task<MarketBoardData?>?)cache[id.ToString()]
                where item is null or { IsCompleted: true, Result: null }
                let itemData = itemSheet?.GetRow(id)
                where itemData != null && itemData.ItemSearchCategory.Row != 0
                select id).ToList();

        if (itemIds.Count == 0)
            return;

        var itemTask = Task.Run(async () => {
            var fetchStart = DateTime.Now;
            var result = await plugin.UniversalisClient.GetMarketBoardDataList(scope, homeWorldId, itemIds);
            if (result != null)
                plugin.ItemPriceTooltip.Refresh(result);
            else 
                plugin.ItemPriceTooltip.FetchFailed(itemIds);
            PluginLog.Debug($"Fetching {itemIds.Count} items took {(DateTime.Now - fetchStart).TotalMilliseconds:F0}ms");
            return result;
        });

        foreach (var id in itemIds) {
            cache.Set(id.ToString(), itemTask.ContinueWith(task => task.Result?.GetValueOrDefault(id), TaskContinuationOptions.OnlyOnRanToCompletion),
                DateTimeOffset.Now.AddMinutes(90));
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
        cache.Dispose();
    }
}
