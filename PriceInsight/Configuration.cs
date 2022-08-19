using System;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace PriceInsight; 

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 0;

    public bool ShowDatacenter { get; set; } = true;

    public bool ShowWorld { get; set; } = true;

    public bool ShowMostRecentPurchase { get; set; } = false;

    public bool ShowMostRecentPurchaseWorld { get; set; } = true;

    public bool IgnoreOldData { get; set; } = true;

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