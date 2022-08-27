using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PriceInsight;

public class ItemPriceTooltip : IDisposable {
    private readonly PriceInsightPlugin plugin;
    private const int NodeId = 32612;
    private const char HQIcon = '';
    private const char GilIcon = '';

    public ItemPriceTooltip(PriceInsightPlugin plugin) {
        this.plugin = plugin;
    }

    public unsafe void RestoreToNormal(AtkUnitBase* itemTooltip) {
        for (var i = 0; i < itemTooltip->UldManager.NodeListCount; i++) {
            var n = itemTooltip->UldManager.NodeList[i];
            if (n->NodeID != NodeId)
                continue;
            var insertNode = itemTooltip->GetNodeById(2);
            if (insertNode == null)
                return;
            itemTooltip->WindowNode->AtkResNode.SetHeight((ushort)(itemTooltip->WindowNode->AtkResNode.Height - n->Height - 4));
            itemTooltip->WindowNode->Component->UldManager.SearchNodeById(2)->SetHeight(itemTooltip->WindowNode->AtkResNode.Height);
            insertNode->SetPositionFloat(insertNode->X, insertNode->Y - n->Height - 4);
            break;
        }
    }

    public unsafe void OnItemTooltip(AtkUnitBase* itemTooltip) {
        var (marketBoardData, isMarketable) = plugin.ItemPriceLookup.Get((uint)(plugin.GameGui.HoveredItem % 500000));
        var payloads = isMarketable ? ParseMbData(plugin.GameGui.HoveredItem >= 500000, marketBoardData) : new List<Payload>();

        UpdateItemTooltip(itemTooltip, payloads);
    }

    private static unsafe void UpdateItemTooltip(AtkUnitBase* itemTooltip, List<Payload> payloads) {
        AtkTextNode* priceNode = null;
        for (var i = 0; i < itemTooltip->UldManager.NodeListCount; i++) {
            var node = itemTooltip->UldManager.NodeList[i];
            if (node == null || node->NodeID != NodeId)
                continue;
            priceNode = (AtkTextNode*)node;
            break;
        }

        if (payloads.Count == 0) {
            if (priceNode != null)
                priceNode->AtkResNode.ToggleVisibility(false);
            return;
        }

        var insertNode = itemTooltip->GetNodeById(2);
        if (insertNode == null)
            return;
        if (priceNode == null) {
            var baseNode = itemTooltip->GetTextNodeById(43);
            if (baseNode == null)
                return;
            priceNode = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
            priceNode->AtkResNode.Type = NodeType.Text;
            priceNode->AtkResNode.NodeID = NodeId;
            priceNode->AtkResNode.Flags = (short)(NodeFlags.AnchorLeft | NodeFlags.AnchorTop);
            priceNode->AtkResNode.DrawFlags = 0;
            priceNode->AtkResNode.SetWidth(50);
            priceNode->AtkResNode.SetHeight(20);
            priceNode->AtkResNode.Color = baseNode->AtkResNode.Color;
            priceNode->TextColor = baseNode->TextColor;
            priceNode->EdgeColor = baseNode->EdgeColor;
            priceNode->LineSpacing = 18;
            priceNode->AlignmentFontType = 0x00;
            priceNode->FontSize = 12;
            priceNode->TextFlags = (byte)((TextFlags)baseNode->TextFlags | TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize);
            priceNode->TextFlags2 = 0;
            var prev = insertNode->PrevSiblingNode;
            priceNode->AtkResNode.ParentNode = insertNode->ParentNode;
            insertNode->PrevSiblingNode = (AtkResNode*)priceNode;
            if (prev != null)
                prev->NextSiblingNode = (AtkResNode*)priceNode;
            priceNode->AtkResNode.PrevSiblingNode = prev;
            priceNode->AtkResNode.NextSiblingNode = insertNode;
            itemTooltip->UldManager.UpdateDrawNodeList();
        }

        priceNode->AtkResNode.ToggleVisibility(true);
        priceNode->SetText(new SeString(payloads).Encode());
        priceNode->ResizeNodeForCurrentText();
        priceNode->AtkResNode.SetPositionFloat(17, itemTooltip->WindowNode->AtkResNode.Height - 8f);
        itemTooltip->WindowNode->AtkResNode.SetHeight((ushort)(itemTooltip->WindowNode->AtkResNode.Height + priceNode->AtkResNode.Height + 4));
        itemTooltip->WindowNode->Component->UldManager.SearchNodeById(2)->SetHeight(itemTooltip->WindowNode->AtkResNode.Height);
        insertNode->SetPositionFloat(insertNode->X, insertNode->Y + priceNode->AtkResNode.Height + 4);
    }

    private List<Payload> ParseMbData(bool hq, MarketBoardData? marketBoardData) {
        var payloads = new List<Payload>();
        if (marketBoardData == null) {
            payloads.Add(new UIForegroundPayload(20));
            payloads.Add(new IconPayload(BitmapFontIcon.LevelSync));
            payloads.Add(new TextPayload(" Marketboard info is being obtained..\n        Tap Ctrl to refresh."));
            payloads.Add(new UIForegroundPayload(0));
        } else {
            var mb = marketBoardData.Value;
            if (plugin.Configuration.IgnoreOldData && DateTime.Now.Subtract(mb.LastUploadTime ?? DateTime.UnixEpoch).TotalDays > 29)
                return payloads;
            var ownWorld = mb.OwnMinimumPriceHQ?.World ?? mb.OwnMinimumPriceNQ?.World;
            var minWorld = hq ? mb.MinimumPriceHQ?.World : mb.MinimumPriceNQ?.World;
            var priceHeader = false;
            var recentHeader = false;

            if (plugin.Configuration.ShowDatacenter && minWorld != ownWorld) {
                payloads.Add(new TextPayload("Marketboard Price:"));
                priceHeader = true;

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{minWorld}): "));
                if (mb.MinimumPriceNQ != null) {
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{mb.MinimumPriceNQ?.Price:N0}{GilIcon}"));
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                if (mb.MinimumPriceHQ != null) {
                    if (mb.MinimumPriceNQ != null)
                        payloads.Add(new TextPayload("/"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{HQIcon}{mb.MinimumPriceHQ?.Price:N0}{GilIcon}"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                var recentTime = hq ? mb.MinimumPriceHQ?.Time : mb.MinimumPriceNQ?.Time;
                if (recentTime != null) {
                    payloads.Add(new UIForegroundPayload(20));
                    payloads.Add(new TextPayload($" ({PrintDuration(DateTime.Now.Subtract(recentTime.Value))} ago)"));
                    payloads.Add(new UIForegroundPayload(0));
                }
            }

            if (ownWorld != null && (plugin.Configuration.ShowWorld || (plugin.Configuration.ShowDatacenter && minWorld == ownWorld))) {
                if (!priceHeader)
                    payloads.Add(new TextPayload("Marketboard Price:"));
                payloads.Add(new TextPayload($"\n  Home ({ownWorld}): "));
                if (!hq)
                    payloads.Add(new UIForegroundPayload(506));
                payloads.Add(new TextPayload($"{mb.OwnMinimumPriceNQ?.Price:N0}{GilIcon}"));
                if (!hq)
                    payloads.Add(new UIForegroundPayload(0));
                if (mb.OwnMinimumPriceHQ != null) {
                    payloads.Add(new TextPayload("/"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{HQIcon}{mb.OwnMinimumPriceHQ?.Price:N0}{GilIcon}"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                var recentTime = hq ? mb.OwnMinimumPriceHQ?.Time : mb.OwnMinimumPriceNQ?.Time;
                if (recentTime != null) {
                    payloads.Add(new UIForegroundPayload(20));
                    payloads.Add(new TextPayload($" ({PrintDuration(DateTime.Now.Subtract(recentTime.Value))} ago)"));
                    payloads.Add(new UIForegroundPayload(0));
                }
            }

            var recentWorld = hq ? mb.MostRecentPurchaseHQ?.World : mb.MostRecentPurchaseNQ?.World;
            var ownRecentWorld = mb.OwnMostRecentPurchaseNQ?.World ?? mb.OwnMostRecentPurchaseHQ?.World;
            if (plugin.Configuration.ShowMostRecentPurchase && recentWorld != ownRecentWorld) {
                if (payloads.Count > 0)
                    payloads.Add(new TextPayload("\n"));
                payloads.Add(new TextPayload("Most Recent Purchase:"));
                recentHeader = true;
                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{recentWorld}): "));
                if (mb.MostRecentPurchaseNQ != null) {
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{mb.MostRecentPurchaseNQ?.Price:N0}{GilIcon}"));
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                if (mb.MostRecentPurchaseHQ != null) {
                    if (mb.MostRecentPurchaseNQ != null)
                        payloads.Add(new TextPayload("/"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{HQIcon}{mb.MostRecentPurchaseHQ?.Price:N0}{GilIcon}"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                var recentTime = hq ? mb.MostRecentPurchaseHQ?.Time : mb.MostRecentPurchaseNQ?.Time;
                if (recentTime != null) {
                    payloads.Add(new UIForegroundPayload(20));
                    payloads.Add(new TextPayload($" ({PrintDuration(DateTime.Now.Subtract(recentTime.Value))} ago)"));
                    payloads.Add(new UIForegroundPayload(0));
                }
            }

            if (ownRecentWorld != null && (plugin.Configuration.ShowMostRecentPurchaseWorld || (plugin.Configuration.ShowMostRecentPurchase && recentWorld == ownRecentWorld))) {
                if (!recentHeader) {
                    if (payloads.Count > 0)
                        payloads.Add(new TextPayload("\n"));
                    payloads.Add(new TextPayload("Most Recent Purchase:"));
                }

                payloads.Add(new TextPayload($"\n  Home ({ownRecentWorld}): "));
                if (mb.OwnMostRecentPurchaseNQ != null) {
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{mb.OwnMostRecentPurchaseNQ?.Price:N0}{GilIcon}"));
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                if (mb.OwnMostRecentPurchaseHQ != null) {
                    if (mb.OwnMostRecentPurchaseNQ != null)
                        payloads.Add(new TextPayload("/"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{HQIcon}{mb.OwnMostRecentPurchaseHQ?.Price:N0}{GilIcon}"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                var recentTime = hq ? mb.OwnMostRecentPurchaseHQ?.Time : mb.OwnMostRecentPurchaseNQ?.Time;
                if (recentTime != null) {
                    payloads.Add(new UIForegroundPayload(20));
                    payloads.Add(new TextPayload($" ({PrintDuration(DateTime.Now.Subtract(recentTime.Value))} ago)"));
                    payloads.Add(new UIForegroundPayload(0));
                }
            }
        }

        return payloads;
    }

    public void Refresh(IDictionary<uint,MarketBoardData> mbData) {
        if (mbData.TryGetValue((uint)(plugin.GameGui.HoveredItem % 500000), out var data)) {
            var tooltip = plugin.GameGui.GetAddonByName("ItemDetail", 1);
            if (tooltip == IntPtr.Zero) return;
            var newText = ParseMbData(plugin.GameGui.HoveredItem >= 500000, data);
            unsafe {
                RestoreToNormal((AtkUnitBase*)tooltip);
                UpdateItemTooltip((AtkUnitBase*)tooltip, newText);
            }
        }
    }

    private void Cleanup() {
        unsafe {
            var atkUnitBase = (AtkUnitBase*)plugin.GameGui.GetAddonByName("ItemDetail", 1);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong!
            if (atkUnitBase == null)
                return;

            for (var n = 0; n < atkUnitBase->UldManager.NodeListCount; n++) {
                var node = atkUnitBase->UldManager.NodeList[n];
                if (node == null)
                    continue;
                if (node->NodeID != NodeId)
                    continue;
                if (node->ParentNode != null && node->ParentNode->ChildNode == node)
                    node->ParentNode->ChildNode = node->PrevSiblingNode;
                if (node->PrevSiblingNode != null)
                    node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;
                if (node->NextSiblingNode != null)
                    node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;
                atkUnitBase->UldManager.UpdateDrawNodeList();
                node->Destroy(true);
                break;
            }
        }
    }

    private static string PrintDuration(TimeSpan span) {
        if (span.Days > 0)
            return $"{span.Days}d";
        if (span.Hours > 0)
            return $"{span.Hours}h";
        if (span.Minutes > 0)
            return $"{span.Minutes}min";
        return "few sec";
    }

    public void Dispose() {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    ~ItemPriceTooltip() {
        Cleanup();
    }
}
