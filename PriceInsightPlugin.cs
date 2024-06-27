using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace PriceInsight;

public class PriceInsightPlugin : IDalamudPlugin {
    public Configuration Configuration { get; }
    public ItemPriceTooltip ItemPriceTooltip { get; }
    public Hooks Hooks { get; }
    public ItemPriceLookup ItemPriceLookup { get; private set; }
    public UniversalisClient UniversalisClient { get; }

    private readonly ConfigUI configUi;

    private readonly Dictionary<InventoryType, DateTime> inventoriesToScan = new() {
        { InventoryType.Inventory1, DateTime.UnixEpoch },
        { InventoryType.Inventory2, DateTime.UnixEpoch },
        { InventoryType.Inventory3, DateTime.UnixEpoch },
        { InventoryType.Inventory4, DateTime.UnixEpoch },
        { InventoryType.SaddleBag1, DateTime.UnixEpoch },
        { InventoryType.SaddleBag2, DateTime.UnixEpoch },
        { InventoryType.PremiumSaddleBag1, DateTime.UnixEpoch },
        { InventoryType.PremiumSaddleBag2, DateTime.UnixEpoch },
        { InventoryType.RetainerPage1, DateTime.UnixEpoch },
        { InventoryType.RetainerPage2, DateTime.UnixEpoch },
        { InventoryType.RetainerPage3, DateTime.UnixEpoch },
        { InventoryType.RetainerPage4, DateTime.UnixEpoch },
        { InventoryType.RetainerPage5, DateTime.UnixEpoch },
        { InventoryType.RetainerPage6, DateTime.UnixEpoch },
        { InventoryType.RetainerPage7, DateTime.UnixEpoch },
    };

    public PriceInsightPlugin(DalamudPluginInterface pluginInterface) {
        Service.Initialize(pluginInterface);

        Configuration = Configuration.Get(pluginInterface);

        UniversalisClient = new UniversalisClient(this);
        ItemPriceLookup = new ItemPriceLookup(this);
        ItemPriceTooltip = new ItemPriceTooltip(this);
        Hooks = new Hooks(this);
        configUi = new ConfigUI(this);

        Service.CommandManager.AddHandler("/priceinsight", new CommandInfo((_, _) => OpenConfigUI()) { HelpMessage = "Price Insight Configuration Menu" });

        pluginInterface.UiBuilder.Draw += () => configUi.Draw();
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
        Service.Framework.Update += FrameworkOnUpdate;
        Service.ClientState.Logout += ClearCache;
    }

    public void ClearCache() {
        foreach (var key in inventoriesToScan.Keys) {
            inventoriesToScan[key] = DateTime.UnixEpoch;
        }
        var ipl = ItemPriceLookup;
        ItemPriceLookup = new ItemPriceLookup(this);
        ipl.Dispose();
    }

    private void FrameworkOnUpdate(IFramework framework) {
        if (ItemPriceLookup.NeedsClearing)
            ClearCache();
        if (Service.ClientState.LocalContentId == 0 || !ItemPriceLookup.CheckReady())
            return;
        if(!Configuration.PrefetchInventory)
            return;
        try {
            unsafe {
                var manager = InventoryManager.Instance();
                var items = new HashSet<uint>();
                foreach (var (type, lastUpdate) in inventoriesToScan) {
                    if ((DateTime.Now - lastUpdate).TotalMinutes < 59)
                        continue;
                    var container = manager->GetInventoryContainer(type);
                    if (container == null || container->Loaded == 0)
                        continue;
                    var empty = true;
                    for (var i = 0; i < container->Size; i++) {
                        var item = &container->Items[i];
                        var itemId = item->ItemId;
                        if (itemId == 0) {
                            continue;
                        }

                        items.Add(itemId);
                        empty = false;

                        if (items.Count >= 50) {
                            ItemPriceLookup.Fetch(items);
                            items.Clear();
                        }
                    }

                    if (empty) {
                        // The inventory was completely empty (retainer and companion inventory are empty before they're loaded)
                        inventoriesToScan[type] = DateTime.Now.AddSeconds(-59 * 60 + 10);
                        continue;
                    }

                    inventoriesToScan[type] = DateTime.Now;
                }

                if (items.Count > 0) {
                    ItemPriceLookup.Fetch(items);
                }
            }
        } catch (Exception e) {
            Service.PluginLog.Error(e, "Failed to process update");
        }
    }

    private void OpenConfigUI() {
        configUi.SettingsVisible = true;
    }

    public void Dispose() {
        Service.CommandManager.RemoveHandler("/priceinsight");
        Service.Framework.Update -= FrameworkOnUpdate;
        Service.ClientState.Logout -= ClearCache;
        Hooks.Dispose();
        ItemPriceTooltip.Dispose();
        ItemPriceLookup.Dispose();
        UniversalisClient.Dispose();
        configUi.Dispose();
    }
}
