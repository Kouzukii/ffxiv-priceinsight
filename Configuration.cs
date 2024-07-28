using System;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace PriceInsight;

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;

    public bool ShowRegion { get; set; } = false;

    public bool ShowDatacenter { get; set; } = true;

    public bool ShowWorld { get; set; } = true;

    public bool ShowStackSalePrice { get; set; } = false;

    public bool ShowMostRecentPurchase { get; set; } = false;

    public bool ShowMostRecentPurchaseRegion { get; set; } = false;

    public bool ShowMostRecentPurchaseWorld { get; set; } = true;

    public bool ShowDailySaleVelocity { get; set; } = false;

    public bool ShowAverageSalePrice { get; set; } = false;

    public bool UseCurrentWorld { get; set; } = false;

    public bool RefreshWithAlt { get; set; } = true;

    public bool PrefetchInventory { get; set; } = true;

    public bool ShowAge { get; set; } = true;

    public bool ShowDatacenterOnCrossWorlds { get; set; } = true;

    public bool ShowBothNqAndHq { get; set; } = true;

    public bool ForceIpv4 { get; set; } = false;

    public bool UseNewUniversalisApi { get; set; } = true;

    // the below exist just to make saving less cumbersome

    [NonSerialized] private IDalamudPluginInterface pluginInterface = null!;

    public static Configuration Get(IDalamudPluginInterface pluginInterface) {
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.pluginInterface = pluginInterface;
        config.Migrate();
        return config;
    }

    private void Migrate() {
        if (Version == 0) {
            UseNewUniversalisApi = new Random().NextDouble() >= 0.5;
            Version = 1;
            Save();
        }
    }

    public void Save() {
        pluginInterface.SavePluginConfig(this);
    }
}