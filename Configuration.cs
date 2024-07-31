using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace PriceInsight;

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 2;

    public bool ShowRegion { get; set; } = false;

    public bool ShowDatacenter { get; set; } = true;

    public bool ShowWorld { get; set; } = true;

    public bool ShowStackSalePrice { get; set; } = false;

    public bool ShowMostRecentPurchase { get; set; } = false;

    public bool ShowMostRecentPurchaseRegion { get; set; } = false;

    public bool ShowMostRecentPurchaseWorld { get; set; } = true;

    public int ShowDailySaleVelocityIn { get; set; } = 1;

    public int ShowAverageSalePriceIn { get; set; } = 0;

    public bool UseCurrentWorld { get; set; } = false;

    public bool RefreshWithAlt { get; set; } = true;

    public bool PrefetchInventory { get; set; } = true;

    public bool ShowAge { get; set; } = true;

    public bool ShowDatacenterOnCrossWorlds { get; set; } = true;

    public bool ShowBothNqAndHq { get; set; } = true;

    [JsonExtensionData]
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    // the below exist just to make saving less cumbersome

    [NonSerialized] private IDalamudPluginInterface pluginInterface = null!;

    public static Configuration Get(IDalamudPluginInterface pluginInterface) {
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.pluginInterface = pluginInterface;
        config.Migrate();
        return config;
    }

    private void Migrate() {
        if (Version < 2) {
            ShowAverageSalePriceIn = Equals(AdditionalData.GetValueOrDefault("ShowAverageSalePrice"), true) ? 1 : 0;
            AdditionalData.Clear();
            Version = 2;
            Save();
        }
    }

    public void Save() {
        pluginInterface.SavePluginConfig(this);
    }
}