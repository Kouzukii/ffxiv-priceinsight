using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PriceInsight;

public class Hooks : IDisposable {
    private readonly PriceInsightPlugin plugin;

    private unsafe delegate byte AgentItemDetailOnItemHovered(void* a1, void* a2, void* a3, void* a4, uint a5, uint a6, int* a7);

    [Signature("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 89 9C 24 ?? ?? ?? ?? 4C 89 A4 24", DetourName = nameof(AgentItemDetailOnItemHoveredDetour))]
    private readonly Hook<AgentItemDetailOnItemHovered> agentItemDetailOnItemHovered = null!;

    [Signature("E8 ?? ?? ?? ?? 45 85 ED 4C 8B AC 24")]
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
            plugin.ItemPriceTooltip.LastItemQuantity = a7[5];
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