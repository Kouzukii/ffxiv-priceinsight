using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PriceInsight; 

public class Hooks : IDisposable {
    private readonly PriceInsightPlugin plugin;
    
    private unsafe delegate byte AgentItemDetailOnItemHovered(void* a1, void* a2, void* a3, void* a4, uint a5, uint a6, int* a7);

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 40 8B 81", DetourName = nameof(AgentItemDetailOnItemHoveredDetour))]
    private readonly Hook<AgentItemDetailOnItemHovered> agentItemDetailOnItemHovered = null!;
    
    [Signature("E8 ?? ?? ?? ?? 45 85 E4 75 68 B2 01 48 8B CF")]
    public readonly unsafe delegate*unmanaged[Thiscall]<AtkUnitBase*, short, short, byte, void> ItemDetailSetPositionPreservingOriginal = null!;

    public unsafe Hooks(PriceInsightPlugin plugin) {
        this.plugin = plugin;
        Service.GameInteropProvider.InitializeFromAttributes(this);
        agentItemDetailOnItemHovered.Enable();
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "ItemDetail", (_, args) => ItemPriceTooltip.RestoreToNormal((AtkUnitBase*)args.Addon));
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "ItemDetail", (_, args) => plugin.ItemPriceTooltip.OnItemTooltip((AtkUnitBase*)args.Addon));
    }

    private unsafe byte AgentItemDetailOnItemHoveredDetour(void* a1, void* a2, void* a3, void* a4, uint a5, uint a6, int* a7) {
        var ret = agentItemDetailOnItemHovered.Original(a1, a2, a3, a4, a5, a6, a7);
        try {
            plugin.ItemPriceTooltip.LastItemQuantity = a7[3];
        } catch (Exception e) {
            plugin.ItemPriceTooltip.LastItemQuantity = null;
            Service.PluginLog.Error(e, "Failed to read last item quantity");
        }

        return ret;
    }

    public void Dispose() {
        agentItemDetailOnItemHovered.Dispose();
    }

}