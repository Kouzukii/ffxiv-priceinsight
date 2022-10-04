using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PriceInsight;

public class ItemPriceTooltip : IDisposable {
    private readonly PriceInsightPlugin plugin;
    private const int NodeId = 32612;
    private const char HQIcon = '';
    private const char GilIcon = '';
    private const uint TooltipMovedUp = 0x80000000;

    public ItemPriceTooltip(PriceInsightPlugin plugin) {
        this.plugin = plugin;
    }

    public unsafe void RestoreToNormal(AtkUnitBase* itemTooltip) {
        for (var i = 0; i < itemTooltip->UldManager.NodeListCount; i++) {
            var n = itemTooltip->UldManager.NodeList[i];
            if (n->NodeID != NodeId || !n->IsVisible)
                continue;
            var insertNode = itemTooltip->GetNodeById(2);
            if (insertNode == null)
                return;
            itemTooltip->WindowNode->AtkResNode.SetHeight((ushort)(itemTooltip->WindowNode->AtkResNode.Height - n->Height - 4));
            itemTooltip->WindowNode->Component->UldManager.SearchNodeById(2)->SetHeight(itemTooltip->WindowNode->AtkResNode.Height);
            if ((n->Flags_2 & TooltipMovedUp) == TooltipMovedUp) {
                itemTooltip->SetPosition(itemTooltip->X, (short)(itemTooltip->Y + n->Height));
                n->Flags_2 &= ~TooltipMovedUp;
            }
            insertNode->SetPositionFloat(insertNode->X, insertNode->Y - n->Height - 4);
            break;
        }
    }

    public unsafe void OnItemTooltip(AtkUnitBase* itemTooltip) {
        if (Service.GameGui.HoveredItem >= 2000000) {
            UpdateItemTooltip(itemTooltip, new List<Payload>());
            return;
        }

        var refresh = plugin.Configuration.RefreshWithAlt && Service.KeyState[VirtualKey.MENU];
        var (marketBoardData, isMarketable) = plugin.ItemPriceLookup.Get((uint)(Service.GameGui.HoveredItem % 500000), refresh);
        var payloads = isMarketable ? ParseMbData(Service.GameGui.HoveredItem >= 500000, marketBoardData) : new List<Payload>();

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
        if (ImGuiHelpers.MainViewport.Size.Y - itemTooltip->Y - itemTooltip->WindowNode->AtkResNode.Height < 36) {
            itemTooltip->SetPosition(itemTooltip->X, (short)(itemTooltip->Y - priceNode->AtkResNode.Height));
            priceNode->AtkResNode.Flags_2 |= TooltipMovedUp;
        }
        insertNode->SetPositionFloat(insertNode->X, insertNode->Y + priceNode->AtkResNode.Height + 4);
    }

    private List<Payload> ParseMbData(bool hq, MarketBoardData? marketBoardData) {
        var payloads = new List<Payload>();
        if (marketBoardData == null) {
            payloads.Add(new UIForegroundPayload(20));
            payloads.Add(new IconPayload(BitmapFontIcon.LevelSync));
            payloads.Add(new TextPayload(" Marketboard info is being obtained.."));
            payloads.Add(new UIForegroundPayload(0));
        } else {
            var mb = marketBoardData.Value;
            if (plugin.Configuration.IgnoreOldData && DateTime.Now.Subtract(mb.LastUploadTime ?? DateTime.UnixEpoch).TotalDays > 29)
                return payloads;
            var ownWorld = mb.HomeWorld;
            var ownDc = mb.HomeDatacenter;
            var minWorld = hq ? mb.MinimumPriceHQ?.World ?? mb.MinimumPriceNQ?.World : mb.MinimumPriceNQ?.World ?? mb.MinimumPriceHQ?.World;
            var minDc = hq ? mb.RegionMinimumPriceHQ?.Datacenter ?? mb.RegionMinimumPriceNQ?.Datacenter : mb.RegionMinimumPriceNQ?.Datacenter ?? mb.RegionMinimumPriceHQ?.Datacenter;
            var priceHeader = false;

            if (plugin.Configuration.ShowRegion && minDc != ownDc) {
                payloads.Add(new TextPayload("Marketboard Price:"));
                priceHeader = true;
                
                var minWorldRegion = hq ? mb.RegionMinimumPriceHQ?.World ?? mb.RegionMinimumPriceNQ?.World : mb.RegionMinimumPriceNQ?.World ?? mb.RegionMinimumPriceHQ?.World;

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{minWorldRegion} {minDc}): "));
                if (mb.RegionMinimumPriceNQ != null) {
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{mb.RegionMinimumPriceNQ?.Price:N0}{GilIcon}"));
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                if (mb.RegionMinimumPriceHQ != null) {
                    if (mb.RegionMinimumPriceNQ != null)
                        payloads.Add(new TextPayload("/"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{HQIcon}{mb.RegionMinimumPriceHQ?.Price:N0}{GilIcon}"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                var recentTime = hq ? mb.RegionMinimumPriceHQ?.Time : mb.RegionMinimumPriceNQ?.Time;
                if (recentTime != null) {
                    payloads.Add(new UIForegroundPayload(20));
                    payloads.Add(new TextPayload($" ({PrintDuration(DateTime.Now.Subtract(recentTime.Value))})"));
                    payloads.Add(new UIForegroundPayload(0));
                }
            }
            
            if (minWorld != ownWorld && (plugin.Configuration.ShowDatacenter || (plugin.Configuration.ShowRegion && minDc == ownDc))) {
                if (!priceHeader) {
                    payloads.Add(new TextPayload("Marketboard Price:"));
                    priceHeader = true;
                }

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
                    payloads.Add(new TextPayload($" ({PrintDuration(DateTime.Now.Subtract(recentTime.Value))})"));
                    payloads.Add(new UIForegroundPayload(0));
                }
            }

            if ((mb.OwnMinimumPriceHQ != null || mb.OwnMinimumPriceNQ != null) && (plugin.Configuration.ShowWorld || (plugin.Configuration.ShowDatacenter && minWorld == ownWorld))) {
                if (!priceHeader)
                    payloads.Add(new TextPayload("Marketboard Price:"));
                payloads.Add(new TextPayload($"\n  Home ({ownWorld}): "));
                if (mb.OwnMinimumPriceNQ != null) {
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{mb.OwnMinimumPriceNQ?.Price:N0}{GilIcon}"));
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                if (mb.OwnMinimumPriceHQ != null) {
                    if (mb.OwnMinimumPriceNQ != null)
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
                    payloads.Add(new TextPayload($" ({PrintDuration(DateTime.Now.Subtract(recentTime.Value))})"));
                    payloads.Add(new UIForegroundPayload(0));
                }
            }

            var recentHeader = false;
            var recentWorld = hq ? mb.MostRecentPurchaseHQ?.World : mb.MostRecentPurchaseNQ?.World;
            var recentDc = hq ? mb.RegionMostRecentPurchaseHQ?.Datacenter : mb.RegionMostRecentPurchaseNQ?.Datacenter;
            if (plugin.Configuration.ShowMostRecentPurchaseRegion && recentDc != ownDc) {
                if (payloads.Count > 0)
                    payloads.Add(new TextPayload("\n"));
                payloads.Add(new TextPayload("Most Recent Purchase:"));
                recentHeader = true;
                
                var recentWorldRegion = hq ? mb.RegionMostRecentPurchaseHQ?.World ?? mb.RegionMostRecentPurchaseNQ?.World : mb.RegionMostRecentPurchaseNQ?.World ?? mb.RegionMostRecentPurchaseHQ?.World;
                
                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{recentWorldRegion} {recentDc}): "));
                if (mb.RegionMostRecentPurchaseNQ != null) {
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{mb.RegionMostRecentPurchaseNQ?.Price:N0}{GilIcon}"));
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                if (mb.RegionMostRecentPurchaseHQ != null) {
                    if (mb.RegionMostRecentPurchaseNQ != null)
                        payloads.Add(new TextPayload("/"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{HQIcon}{mb.RegionMostRecentPurchaseHQ?.Price:N0}{GilIcon}"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(0));
                }

                var recentTime = hq ? mb.RegionMostRecentPurchaseHQ?.Time : mb.RegionMostRecentPurchaseNQ?.Time;
                if (recentTime != null) {
                    payloads.Add(new UIForegroundPayload(20));
                    payloads.Add(new TextPayload($" ({PrintDuration(DateTime.Now.Subtract(recentTime.Value))})"));
                    payloads.Add(new UIForegroundPayload(0));
                }
            }
            
            if (recentWorld != null && recentWorld != ownWorld && (plugin.Configuration.ShowMostRecentPurchase || (plugin.Configuration.ShowMostRecentPurchaseRegion && recentDc == ownDc))) {
                if (!recentHeader) {
                    if (payloads.Count > 0)
                        payloads.Add(new TextPayload("\n"));
                    payloads.Add(new TextPayload("Most Recent Purchase:"));
                    recentHeader = true;
                }
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
                    payloads.Add(new TextPayload($" ({PrintDuration(DateTime.Now.Subtract(recentTime.Value))})"));
                    payloads.Add(new UIForegroundPayload(0));
                }
            }

            if ((mb.OwnMostRecentPurchaseHQ != null || mb.OwnMostRecentPurchaseNQ != null) && (plugin.Configuration.ShowMostRecentPurchaseWorld || (plugin.Configuration.ShowMostRecentPurchase && recentWorld == ownWorld))) {
                if (!recentHeader) {
                    if (payloads.Count > 0)
                        payloads.Add(new TextPayload("\n"));
                    payloads.Add(new TextPayload("Most Recent Purchase:"));
                }

                payloads.Add(new TextPayload($"\n  Home ({ownWorld}): "));
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
                    payloads.Add(new TextPayload($" ({PrintDuration(DateTime.Now.Subtract(recentTime.Value))})"));
                    payloads.Add(new UIForegroundPayload(0));
                }
            }
        }

        return payloads;
    }

    public void Refresh(IDictionary<uint,MarketBoardData> mbData) {
        if (Service.GameGui.HoveredItem >= 2000000) return;
        if (mbData.TryGetValue((uint)(Service.GameGui.HoveredItem % 500000), out var data)) {
            var newText = ParseMbData(Service.GameGui.HoveredItem >= 500000, data);
            Service.Framework.RunOnFrameworkThread(() => {
                try {
                    var tooltip = Service.GameGui.GetAddonByName("ItemDetail", 1);
                    unsafe {
                        if (tooltip == IntPtr.Zero || !((AtkUnitBase*)tooltip)->IsVisible)
                            return;
                        RestoreToNormal((AtkUnitBase*)tooltip);
                        UpdateItemTooltip((AtkUnitBase*)tooltip, newText);
                    }
                } catch (Exception e) {
                    PluginLog.Error(e, "Failed to update tooltip");
                }
            });
        }
    }

    private void Cleanup() {
        unsafe {
            var atkUnitBase = (AtkUnitBase*)Service.GameGui.GetAddonByName("ItemDetail", 1);
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
            return $"{span.Days}d ago";
        if (span.Hours > 0)
            return $"{span.Hours}h ago";
        if (span.Minutes > 0)
            return $"{span.Minutes}m ago";
        return "just now";
    }

    public void Dispose() {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    ~ItemPriceTooltip() {
        Cleanup();
    }
}
