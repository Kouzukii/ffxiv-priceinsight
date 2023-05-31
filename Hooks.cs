using System;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PriceInsight; 

public class Hooks : IDisposable {
    private readonly PriceInsightPlugin plugin;
        
    private unsafe delegate void* AddonOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** nums, StringArrayData** strings);

    private unsafe delegate byte AgentItemDetailOnItemHovered(void* a1, void* a2, void* a3, void* a4, uint a5, uint a6, int* a7);

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 20 4C 8B AA", DetourName = nameof(ItemDetailOnUpdateDetour))]
    private readonly Hook<AddonOnUpdate> itemDetailOnUpdateHook = null!;

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 40 8B 81", DetourName = nameof(AgentItemDetailOnItemHoveredDetour))]
    private readonly Hook<AgentItemDetailOnItemHovered> agentItemDetailOnItemHovered = null!;

    public Hooks(PriceInsightPlugin plugin) {
        this.plugin = plugin;
        SignatureHelper.Initialise(this);
        itemDetailOnUpdateHook.Enable();
        agentItemDetailOnItemHovered.Enable();
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

    private unsafe byte AgentItemDetailOnItemHoveredDetour(void* a1, void* a2, void* a3, void* a4, uint a5, uint a6, int* a7) {
        var ret = agentItemDetailOnItemHovered.Original(a1, a2, a3, a4, a5, a6, a7);
        try {
            plugin.ItemPriceTooltip.LastItemQuantity = a7[3];
        } catch (Exception e) {
            plugin.ItemPriceTooltip.LastItemQuantity = null;
            PluginLog.Log(e , "Failed to read last item quantity");
        }

        return ret;
    }

    public void Dispose() {
        itemDetailOnUpdateHook.Dispose();
        agentItemDetailOnItemHovered.Disable();
    }

}