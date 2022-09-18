using System;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PriceInsight; 

// Taken mostly from https://github.com/Caraxi/SimpleTweaksPlugin under the terms of AGPL3
public class Hooks : IDisposable {
    private readonly PriceInsightPlugin plugin;
        
    private unsafe delegate void* AddonOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** nums, StringArrayData** strings);

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 20 4C 8B AA", DetourName = nameof(ItemDetailOnUpdateDetour))]
    private readonly Hook<AddonOnUpdate> itemDetailOnUpdateHook = null!;

    public Hooks(PriceInsightPlugin plugin) {
        this.plugin = plugin;
        SignatureHelper.Initialise(this);
        itemDetailOnUpdateHook.Enable();
    }

    private unsafe void* ItemDetailOnUpdateDetour(AtkUnitBase* atkUnitBase, NumberArrayData** nums, StringArrayData** strings) {
        try {
            plugin.ItemPriceTooltip.RestoreToNormal(atkUnitBase);
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Failed to handle item detail detour");
        }
        var ret = itemDetailOnUpdateHook.Original(atkUnitBase, nums, strings);
        try {
            plugin.ItemPriceTooltip.OnItemTooltip(atkUnitBase);
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Failed to handle item detail detour");
        }

        return ret;
    }

    public void Dispose() {
        itemDetailOnUpdateHook.Dispose();
    }

}