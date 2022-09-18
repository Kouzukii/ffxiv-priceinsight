using System;
using Dalamud.Interface;
using ImGuiNET;

namespace PriceInsight; 

class ConfigUI : IDisposable {
    private readonly Configuration configuration;

    private bool settingsVisible = false;

    public bool SettingsVisible {
        get => settingsVisible;
        set => settingsVisible = value;
    }

    public ConfigUI(Configuration configuration) {
        this.configuration = configuration;
    }

    public void Dispose() {
    }

    public void Draw() {
        if (!SettingsVisible) {
            return;
        }

        if (ImGui.Begin("Price Insight Config", ref settingsVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)) {
            var configValue = configuration.RefreshWithAlt;
            if (ImGui.Checkbox("Tap Alt to refresh prices", ref configValue)) {
                configuration.RefreshWithAlt = configValue;
                configuration.Save();
            }
            
            configValue = configuration.ShowDatacenter;
            if (ImGui.Checkbox("Show datacenter price info", ref configValue)) {
                configuration.ShowDatacenter = configValue;
                configuration.Save();
            }

            configValue = configuration.ShowWorld;
            if (ImGui.Checkbox("Show home world price info", ref configValue)) {
                configuration.ShowWorld = configValue;
                configuration.Save();
            }

            configValue = configuration.ShowMostRecentPurchase;
            if (ImGui.Checkbox("Show most recent purchase", ref configValue)) {
                configuration.ShowMostRecentPurchase = configValue;
                configuration.Save();
            }

            configValue = configuration.ShowMostRecentPurchaseWorld;
            if (ImGui.Checkbox("Show most recent purchase on your home world", ref configValue)) {
                configuration.ShowMostRecentPurchaseWorld = configValue;
                configuration.Save();
            }

            configValue = configuration.IgnoreOldData;
            if (ImGui.Checkbox("Ignore data older than 1 month", ref configValue)) {
                configuration.IgnoreOldData = configValue;
                configuration.Save();
            }
        }

        ImGui.End();
    }
}