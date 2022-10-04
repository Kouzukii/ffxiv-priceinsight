using System;
using ImGuiNET;

namespace PriceInsight; 

class ConfigUI : IDisposable {
    private readonly PriceInsightPlugin plugin;

    private bool settingsVisible = false;

    public bool SettingsVisible {
        get => settingsVisible;
        set => settingsVisible = value;
    }

    public ConfigUI(PriceInsightPlugin plugin) {
        this.plugin = plugin;
    }

    public void Dispose() {
    }

    public void Draw() {
        if (!SettingsVisible) {
            return;
        }

        var conf = plugin.Configuration;
        if (ImGui.Begin("Price Insight Config", ref settingsVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)) {
            var configValue = conf.RefreshWithAlt;
            if (ImGui.Checkbox("Tap Alt to refresh prices", ref configValue)) {
                conf.RefreshWithAlt = configValue;
                conf.Save();
            }
            
            configValue = conf.UseCurrentWorld;
            if (ImGui.Checkbox("Use current world as home world", ref configValue)) {
                conf.UseCurrentWorld = configValue;
                conf.Save();
                plugin.ClearCache();
            }
            TooltipUseCurrentWorld();
            
            ImGui.Separator();
            ImGui.PushID(0);
            
            ImGui.Text("Show cheapest price in:");
            
            configValue = conf.ShowRegion;
            if (ImGui.Checkbox("Region", ref configValue)) {
                conf.ShowRegion = configValue;
                conf.Save();
                plugin.ClearCache();
            }
            TooltipRegion();

            configValue = conf.ShowDatacenter;
            if (ImGui.Checkbox("Datacenter", ref configValue)) {
                conf.ShowDatacenter = configValue;
                conf.Save();
                plugin.ClearCache();
            }

            configValue = conf.ShowWorld;
            if (ImGui.Checkbox("Home world", ref configValue)) {
                conf.ShowWorld = configValue;
                conf.Save();
            }
            
            ImGui.PopID();
            ImGui.Separator();
            ImGui.PushID(1);
            
            ImGui.Text("Show most recent purchase in:");

            configValue = conf.ShowMostRecentPurchaseRegion;
            if (ImGui.Checkbox("Region", ref configValue)) {
                conf.ShowMostRecentPurchaseRegion = configValue;
                conf.Save();
                plugin.ClearCache();
            }
            TooltipRegion();

            configValue = conf.ShowMostRecentPurchase;
            if (ImGui.Checkbox("Datacenter", ref configValue)) {
                conf.ShowMostRecentPurchase = configValue;
                conf.Save();
                plugin.ClearCache();
            }

            configValue = conf.ShowMostRecentPurchaseWorld;
            if (ImGui.Checkbox("Home world", ref configValue)) {
                conf.ShowMostRecentPurchaseWorld = configValue;
                conf.Save();
            }
            
            ImGui.PopID();
            ImGui.Separator();

            configValue = conf.IgnoreOldData;
            if (ImGui.Checkbox("Ignore data older than 1 month", ref configValue)) {
                conf.IgnoreOldData = configValue;
                conf.Save();
            }
        }

        ImGui.End();
    }

    private static void TooltipUseCurrentWorld() {
        if (ImGui.IsItemHovered()) {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(
                "The current world you're on will be considered your \"home world\".\nUseful if you're datacenter travelling and want to see prices there.");
            ImGui.EndTooltip();
        }
    }

    private static void TooltipRegion() {
        if (ImGui.IsItemHovered()) {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("Include all datacenters available via datacenter traveling.");
            ImGui.EndTooltip();
        }
    }
}