using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PriceInsight;

public partial class ItemPriceTooltip : IDisposable {
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

    [GeneratedRegex("(\\d+)\\/\\d+ \\(Total: \\d+\\)", RegexOptions.Compiled)]
    private static partial Regex TooltipStackRegex();

    public unsafe int GetTooltipStackSize(AtkUnitBase* itemTooltip) {
        var stackSizeNode = itemTooltip->GetTextNodeById(33);
        var text = stackSizeNode->NodeText.ToString();
        var match = TooltipStackRegex().Match(text);
        return match.Success ? int.Parse(match.Groups[1].Value) : 1;
    }

    public unsafe void OnItemTooltip(AtkUnitBase* itemTooltip) {
        if (Service.GameGui.HoveredItem is >= 2000000 or >= 500000 and < 1000000) {
            UpdateItemTooltip(itemTooltip, new List<Payload>());
            return;
        }

        var refresh = plugin.Configuration.RefreshWithAlt && Service.KeyState[VirtualKey.MENU];
        var (marketBoardData, lookupState) = plugin.ItemPriceLookup.Get((uint)(Service.GameGui.HoveredItem % 500000), refresh);
        var payloads = ParseMbData(Service.GameGui.HoveredItem >= 500000, GetTooltipStackSize(itemTooltip), marketBoardData, lookupState);

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
        if (ImGuiHelpers.MainViewport.WorkSize.Y - itemTooltip->Y - itemTooltip->WindowNode->AtkResNode.Height < 36) {
            itemTooltip->SetPosition(itemTooltip->X, (short)(itemTooltip->Y - priceNode->AtkResNode.Height));
            priceNode->AtkResNode.Flags_2 |= TooltipMovedUp;
        }

        insertNode->SetPositionFloat(insertNode->X, insertNode->Y + priceNode->AtkResNode.Height + 4);
    }

    private List<Payload> ParseMbData(bool hq, int stackSize, MarketBoardData? mbData, LookupState lookupState) {
        var payloads = new List<Payload>();
        if (lookupState == LookupState.NonMarketable)
            return payloads;
        if (lookupState == LookupState.Faulted) {
            payloads.Add(new UIForegroundPayload(20));
            payloads.Add(new IconPayload(BitmapFontIcon.Warning));
            payloads.Add(new TextPayload(" Failed to obtain marketboard info.\n        This is likely an issue with Universalis.\n        Press alt to retry or check the /xllog."));
            payloads.Add(new UIForegroundPayload(0));
        } else if (mbData == null) {
            payloads.Add(new UIForegroundPayload(20));
            payloads.Add(new IconPayload(BitmapFontIcon.LevelSync));
            payloads.Add(new TextPayload(" Marketboard info is being obtained.."));
            payloads.Add(new UIForegroundPayload(0));
        } else {
            var ownWorld = mbData.HomeWorld;
            var ownDc = mbData.HomeDatacenter;
            var minWorld = hq 
                ? mbData.MinimumPriceHQ?.World ?? mbData.MinimumPriceNQ?.World 
                : mbData.MinimumPriceNQ?.World ?? mbData.MinimumPriceHQ?.World;
            var minDc = hq
                ? mbData.RegionMinimumPriceHQ?.Datacenter ?? mbData.RegionMinimumPriceNQ?.Datacenter
                : mbData.RegionMinimumPriceNQ?.Datacenter ?? mbData.RegionMinimumPriceHQ?.Datacenter;
            
            var priceHeader = false;
            void PriceHeader() {
                if (priceHeader) return;
                payloads.Add(new TextPayload("Marketboard Price:"));
                priceHeader = true;
            }

            void PrintNqHq(double? nqPrice, double? hqPrice, string format = "N0", bool withGilIcon = true) {
                if (nqPrice != null) {
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{nqPrice.Value.ToString(format, null)}{(withGilIcon ? GilIcon : "")}"));
                    if (plugin.Configuration.ShowStackSalePrice && stackSize > 1)
                        payloads.Add(new TextPayload($" ({(nqPrice.Value * stackSize).ToString(format, null)}{(withGilIcon ? GilIcon : "")})"));
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(0));
                }
                if (hqPrice != null) {
                    if (nqPrice != null)
                        payloads.Add(new TextPayload("/"));

                    if (hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{HQIcon}{hqPrice.Value.ToString(format, null)}{(withGilIcon ? GilIcon : "")}"));
                    if (plugin.Configuration.ShowStackSalePrice && stackSize > 1)
                        payloads.Add(new TextPayload($" ({(hqPrice.Value * stackSize).ToString(format, null)}{(withGilIcon ? GilIcon : "")})"));
                    if (hq)
                        payloads.Add(new UIForegroundPayload(0));
                }
            }

            void PrintTime(DateTime? time) {
                if (time == null) return;
                payloads.Add(new UIForegroundPayload(20));
                payloads.Add(new TextPayload($" ({PrintDuration(DateTime.Now.Subtract(time.Value))})"));
                payloads.Add(new UIForegroundPayload(0));
            }

            if (plugin.Configuration.ShowRegion && minDc != ownDc) {
                PriceHeader();

                var minWorldRegion = hq 
                    ? mbData.RegionMinimumPriceHQ?.World ?? mbData.RegionMinimumPriceNQ?.World 
                    : mbData.RegionMinimumPriceNQ?.World ?? mbData.RegionMinimumPriceHQ?.World;

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{minWorldRegion} {minDc}): "));
                PrintNqHq(mbData.RegionMinimumPriceNQ?.Price, mbData.RegionMinimumPriceHQ?.Price);

                var recentTime = hq ? mbData.RegionMinimumPriceHQ?.Time : mbData.RegionMinimumPriceNQ?.Time;
                PrintTime(recentTime);
            }

            if (minWorld != ownWorld && (plugin.Configuration.ShowDatacenter || (plugin.Configuration.ShowRegion && minDc == ownDc))) {
                PriceHeader();

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{minWorld}): "));
                PrintNqHq(mbData.MinimumPriceNQ?.Price, mbData.MinimumPriceHQ?.Price);

                var recentTime = hq ? mbData.MinimumPriceHQ?.Time : mbData.MinimumPriceNQ?.Time;
                PrintTime(recentTime);
            }

            if ((mbData.OwnMinimumPriceHQ != null || mbData.OwnMinimumPriceNQ != null) && (plugin.Configuration.ShowWorld || (plugin.Configuration.ShowDatacenter && minWorld == ownWorld))) {
                PriceHeader();
                
                payloads.Add(new TextPayload($"\n  Home ({ownWorld}): "));
                PrintNqHq( mbData.OwnMinimumPriceNQ?.Price, mbData.OwnMinimumPriceHQ?.Price);

                var recentTime = hq ? mbData.OwnMinimumPriceHQ?.Time : mbData.OwnMinimumPriceNQ?.Time;
                PrintTime(recentTime);
            }

            var recentHeader = false;
            void RecentHeader() {
                if (recentHeader) return;
                if (payloads.Count > 0)
                    payloads.Add(new TextPayload("\n"));
                payloads.Add(new TextPayload("Most Recent Purchase:"));
                recentHeader = true;
            }
            
            var recentWorld = hq ? mbData.MostRecentPurchaseHQ?.World : mbData.MostRecentPurchaseNQ?.World;
            var recentDc = hq ? mbData.RegionMostRecentPurchaseHQ?.Datacenter : mbData.RegionMostRecentPurchaseNQ?.Datacenter;
            if (plugin.Configuration.ShowMostRecentPurchaseRegion && recentDc != ownDc) {
                RecentHeader();

                var recentWorldRegion = hq
                    ? mbData.RegionMostRecentPurchaseHQ?.World ?? mbData.RegionMostRecentPurchaseNQ?.World
                    : mbData.RegionMostRecentPurchaseNQ?.World ?? mbData.RegionMostRecentPurchaseHQ?.World;

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{recentWorldRegion} {recentDc}): "));
                PrintNqHq(mbData.RegionMostRecentPurchaseNQ?.Price, mbData.RegionMostRecentPurchaseHQ?.Price);

                var recentTime = hq ? mbData.RegionMostRecentPurchaseHQ?.Time : mbData.RegionMostRecentPurchaseNQ?.Time;
                PrintTime(recentTime);
            }

            if (recentWorld != null && recentWorld != ownWorld && (plugin.Configuration.ShowMostRecentPurchase || (plugin.Configuration.ShowMostRecentPurchaseRegion && recentDc == ownDc))) {
                RecentHeader();

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{recentWorld}): "));
                PrintNqHq(mbData.MostRecentPurchaseNQ?.Price, mbData.MostRecentPurchaseHQ?.Price);

                var recentTime = hq ? mbData.MostRecentPurchaseHQ?.Time : mbData.MostRecentPurchaseNQ?.Time;
                PrintTime(recentTime);
            }

            if ((mbData.OwnMostRecentPurchaseHQ != null || mbData.OwnMostRecentPurchaseNQ != null) 
                && (plugin.Configuration.ShowMostRecentPurchaseWorld || (plugin.Configuration.ShowMostRecentPurchase && recentWorld == ownWorld))) {
                RecentHeader();

                payloads.Add(new TextPayload($"\n  Home ({ownWorld}): "));
                PrintNqHq(mbData.OwnMostRecentPurchaseNQ?.Price, mbData.OwnMostRecentPurchaseHQ?.Price);

                var recentTime = hq ? mbData.OwnMostRecentPurchaseHQ?.Time : mbData.OwnMostRecentPurchaseNQ?.Time;
                PrintTime(recentTime);
            }

            if ((mbData.AverageSalePriceNQ != null || mbData.AverageSalePriceHQ != null) && plugin.Configuration.ShowAverageSalePrice) {
                if (payloads.Count > 0)
                    payloads.Add(new TextPayload("\n"));
                payloads.Add(new TextPayload($"Average sale price ({mbData.Scope}): "));
                PrintNqHq(mbData.AverageSalePriceNQ, mbData.AverageSalePriceHQ);
            }

            if ((mbData.DailySaleVelocityNQ != null || mbData.DailySaleVelocityHQ != null) && plugin.Configuration.ShowDailySaleVelocity) {
                if (payloads.Count > 0)
                    payloads.Add(new TextPayload("\n"));
                payloads.Add(new TextPayload($"Sales per day ({mbData.Scope}): "));
                PrintNqHq(mbData.DailySaleVelocityNQ, mbData.DailySaleVelocityHQ, "N1", false);
            }
        }

        return payloads;
    }

    public void Refresh(IDictionary<uint, MarketBoardData> mbData) {
        if (Service.GameGui.HoveredItem >= 2000000) return;
        if (mbData.TryGetValue((uint)(Service.GameGui.HoveredItem % 500000), out var data)) {
            Service.Framework.RunOnFrameworkThread(() => {
                try {
                    var tooltip = Service.GameGui.GetAddonByName("ItemDetail");
                    unsafe {
                        if (tooltip == nint.Zero || !((AtkUnitBase*)tooltip)->IsVisible)
                            return;
                        var newText = ParseMbData(Service.GameGui.HoveredItem >= 500000, GetTooltipStackSize((AtkUnitBase*)tooltip), data, LookupState.Marketable);
                        RestoreToNormal((AtkUnitBase*)tooltip);
                        UpdateItemTooltip((AtkUnitBase*)tooltip, newText);
                    }
                } catch (Exception e) {
                    PluginLog.Error(e, "Failed to update tooltip");
                }
            });
        }
    }

    public void FetchFailed(IList<uint> items) {
        if (!items.Contains((uint)Service.GameGui.HoveredItem % 500000)) return;
        Service.Framework.RunOnFrameworkThread(() => {
            try {
                var tooltip = Service.GameGui.GetAddonByName("ItemDetail");
                unsafe {
                    if (tooltip == nint.Zero || !((AtkUnitBase*)tooltip)->IsVisible)
                        return;
                    var newText = ParseMbData(false, 0, null, LookupState.Faulted);
                    RestoreToNormal((AtkUnitBase*)tooltip);
                    UpdateItemTooltip((AtkUnitBase*)tooltip, newText);
                }
            } catch (Exception e) {
                PluginLog.Error(e, "Failed to update tooltip");
            }
        });
    }

    private void Cleanup() {
        unsafe {
            var atkUnitBase = (AtkUnitBase*)Service.GameGui.GetAddonByName("ItemDetail");
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
