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
    public UniversalisClientV2 UniversalisClientV2 { get; }

    private readonly ConfigUI configUi;

    private readonly Dictionary<(InventoryType Type, int DelayInMinutes), DateTime> inventoriesToScan = new() {
        { (InventoryType.Inventory1, 5), DateTime.UnixEpoch },
        { (InventoryType.Inventory2, 5), DateTime.UnixEpoch },
        { (InventoryType.Inventory3, 5), DateTime.UnixEpoch },
        { (InventoryType.Inventory4, 5), DateTime.UnixEpoch },
        { (InventoryType.SaddleBag1, 15), DateTime.UnixEpoch },
        { (InventoryType.SaddleBag2, 15), DateTime.UnixEpoch },
        { (InventoryType.PremiumSaddleBag1, 15), DateTime.UnixEpoch },
        { (InventoryType.PremiumSaddleBag2, 15), DateTime.UnixEpoch },
        { (InventoryType.RetainerPage1, 15), DateTime.UnixEpoch },
        { (InventoryType.RetainerPage2, 15), DateTime.UnixEpoch },
        { (InventoryType.RetainerPage3, 15), DateTime.UnixEpoch },
        { (InventoryType.RetainerPage4, 15), DateTime.UnixEpoch },
        { (InventoryType.RetainerPage5, 15), DateTime.UnixEpoch },
        { (InventoryType.RetainerPage6, 15), DateTime.UnixEpoch },
        { (InventoryType.RetainerPage7, 15), DateTime.UnixEpoch },
    };

    public PriceInsightPlugin(IDalamudPluginInterface pluginInterface) {
        Service.Initialize(pluginInterface);

        Configuration = Configuration.Get(pluginInterface);

        UniversalisClient = new UniversalisClient(this);
        UniversalisClientV2 = new UniversalisClientV2();
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
            var items = new HashSet<uint>();
            unsafe {
                var manager = InventoryManager.Instance();
                foreach (var (inv, lastUpdate) in inventoriesToScan) {
                    if ((DateTime.Now - lastUpdate).TotalMinutes < inv.DelayInMinutes)
                        continue;
                    var container = manager->GetInventoryContainer(inv.Type);
                    if (container == null || container->Loaded == 0)
                        continue;
                    for (var i = 0; i < container->Size; i++) {
                        var item = &container->Items[i];
                        var itemId = item->ItemId;
                        if (itemId == 0) {
                            continue;
                        }

                        items.Add(itemId);
                    }
                    inventoriesToScan[inv] = DateTime.Now;
                }
            }
            if (items.Count > 0) {
                ItemPriceLookup.Fetch(items);
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
        UniversalisClientV2.Dispose();
        configUi.Dispose();
    }
}
