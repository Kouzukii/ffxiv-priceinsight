using System;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace PriceInsight; 

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 0;

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

    // the below exist just to make saving less cumbersome

    [NonSerialized] private DalamudPluginInterface pluginInterface = null!;

    public static Configuration Get(DalamudPluginInterface pluginInterface) {
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.pluginInterface = pluginInterface;
        return config;
    }

    public void Save() {
        pluginInterface.SavePluginConfig(this);
    }
}