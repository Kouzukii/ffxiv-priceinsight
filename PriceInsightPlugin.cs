using System;
using System.Collections.Generic;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace PriceInsight; 

public class PriceInsightPlugin : IDalamudPlugin {
    public string Name => "PriceInsight";

    public CommandManager CommandManager { get; }
    public ClientState ClientState { get; }
    public DataManager DataManager { get; }
    public SigScanner SigScanner { get; }
    public Framework Framework { get; }
    public GameGui GameGui { get; }

    public Configuration Configuration { get; }
    public ItemPriceTooltip ItemPriceTooltip { get; }
    public Hooks Hooks { get; }
    public ItemPriceLookup ItemPriceLookup { get; }
    public UniversalisClient UniversalisClient { get; }

    private readonly ConfigUI ui;

    private readonly Dictionary<InventoryType, DateTime> inventoriesToScan = new() {
        { InventoryType.EquippedItems, DateTime.UnixEpoch },
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

    public PriceInsightPlugin(DalamudPluginInterface pluginInterface, CommandManager commandManager, ClientState clientState, DataManager dataManager,
        SigScanner sigScanner, Framework framework, GameGui gameGui) {
        CommandManager = commandManager;
        ClientState = clientState;
        DataManager = dataManager;
        SigScanner = sigScanner;
        Framework = framework;
        GameGui = gameGui;

        Configuration = Configuration.Get(pluginInterface);

        UniversalisClient = new UniversalisClient();
        ItemPriceLookup = new ItemPriceLookup(this);
        ItemPriceTooltip = new ItemPriceTooltip(this);
        Hooks = new Hooks(this);

        ui = new ConfigUI(Configuration);

        CommandManager.AddHandler("/priceinsight", new CommandInfo((_, _) => OpenConfigUI()) { HelpMessage = "Price Insight Configuration Menu" });

        pluginInterface.UiBuilder.Draw += () => ui.Draw();
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
        framework.Update += FrameworkOnUpdate;
    }

    private void FrameworkOnUpdate(Framework framework) {
        if (ClientState.LocalContentId == 0 || !ItemPriceLookup.IsReady)
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
                    var emptyItems = 0;
                    for (var i = 0; i < container->Size; i++) {
                        var item = &container->Items[i];
                        if (item->ItemID == 0) {
                            emptyItems++;
                            continue;
                        }
                        items.Add(item->ItemID);

                        if (items.Count >= 50) {
#if !DEBUG // Don't spam universalis while debugging
                            ItemPriceLookup.Prefetch(items);
#endif
                            PluginLog.LogInformation($"Prefetching {items.Count} items");
                            items.Clear();
                        }
                    }

                    if (emptyItems == container->Size) {
                        // The inventory was completely empty (retainer and companion inventory are empty before they're loaded)
                        inventoriesToScan[type] = DateTime.Now.AddSeconds(-59 * 60 + 10);
                        continue;
                    }
                    inventoriesToScan[type] = DateTime.Now;
                }

                if (items.Count > 0) {
#if !DEBUG // Don't spam universalis while debugging
                    ItemPriceLookup.Prefetch(items);
#endif
                    PluginLog.LogInformation($"Prefetching {items.Count} items");
                }
            }
        } catch (Exception e) {
            PluginLog.Log(e, "Failed to process update");
        }
    }

    private void OpenConfigUI() {
        ui.SettingsVisible = true;
    }

    public void Dispose() {
        Framework.Update -= FrameworkOnUpdate;
        Hooks.Dispose();
        ItemPriceTooltip.Dispose();
        ItemPriceLookup.Dispose();
        UniversalisClient.Dispose();
        ui.Dispose();
        CommandManager.RemoveHandler("/priceinsight");
    }
}