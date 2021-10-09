using System;
using Dalamud.Hooking;
using Dalamud.Logging;

namespace PriceInsight {
    // Taken mostly from https://github.com/Caraxi/SimpleTweaksPlugin under the terms of AGPL3
    public class Hooks : IDisposable {
        private PriceInsightPlugin plugin;

        private Hook<TooltipDelegate> tooltipHook;

        private unsafe delegate IntPtr TooltipDelegate(IntPtr a1, uint** a2, byte*** a3);

        private ItemTooltip tooltip;

        public unsafe Hooks(PriceInsightPlugin plugin) {
            this.plugin = plugin;
            var tooltipAddress = plugin.SigScanner.ScanText("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 50 48 8B 42 ??");
            tooltipHook = new Hook<TooltipDelegate>(tooltipAddress, TooltipDetour);

            tooltipHook?.Enable();
        }

        public void Dispose() {
            tooltipHook?.Dispose();
            tooltip?.Dispose();
        }


        private unsafe IntPtr TooltipDetour(IntPtr a1, uint** a2, byte*** a3) {
            try {
                tooltip ??= new ItemTooltip(plugin);
                tooltip.SetPointer(a3);
                plugin.ItemPriceTooltip.OnItemTooltip(tooltip);

            } catch (Exception ex) {
                PluginLog.LogError(ex, "Failed to handle tooltip detour");
            }

            return tooltipHook.Original(a1, a2, a3);
        }
    }
}