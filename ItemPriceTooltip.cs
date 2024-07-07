using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PriceInsight;

public class ItemPriceTooltip(PriceInsightPlugin plugin) : IDisposable {
    private const int NodeId = 32612;
    private const char HQIcon = '';
    private const char GilIcon = '';

    public int? LastItemQuantity;

    public static unsafe void RestoreToNormal(AtkUnitBase* itemTooltip) {
        for (var i = 0; i < itemTooltip->UldManager.NodeListCount; i++) {
            var n = itemTooltip->UldManager.NodeList[i];
            if (n->NodeId != NodeId || !n->IsVisible())
                continue;
            n->ToggleVisibility(false);
            var insertNode = itemTooltip->GetNodeById(2);
            if (insertNode == null)
                return;
            itemTooltip->WindowNode->AtkResNode.SetHeight((ushort)(itemTooltip->WindowNode->AtkResNode.Height - n->Height - 4));
            itemTooltip->WindowNode->Component->UldManager.RootNode->SetHeight(itemTooltip->WindowNode->AtkResNode.Height);
            itemTooltip->WindowNode->Component->UldManager.RootNode->PrevSiblingNode->SetHeight(itemTooltip->WindowNode->AtkResNode.Height);
            insertNode->SetYFloat(insertNode->Y - n->Height - 4);
            break;
        }
    }

    public unsafe void OnItemTooltip(AtkUnitBase* itemTooltip) {
        var refresh = plugin.Configuration.RefreshWithAlt && Service.KeyState[VirtualKey.MENU];
        var (marketBoardData, lookupState) = plugin.ItemPriceLookup.Get(Service.GameGui.HoveredItem, refresh);
        var payloads = ParseMbData(Service.GameGui.HoveredItem >= 500000, marketBoardData, lookupState);

        UpdateItemTooltip(itemTooltip, payloads);
    }

    private unsafe void UpdateItemTooltip(AtkUnitBase* itemTooltip, List<Payload> payloads) {
        if (payloads.Count == 0) {
            return;
        }

        AtkTextNode* priceNode = null;
        for (var i = 0; i < itemTooltip->UldManager.NodeListCount; i++) {
            var node = itemTooltip->UldManager.NodeList[i];
            if (node == null || node->NodeId != NodeId)
                continue;
            priceNode = (AtkTextNode*)node;
            break;
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
            priceNode->AtkResNode.NodeId = NodeId;
            priceNode->AtkResNode.NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop;
            priceNode->AtkResNode.X = 16;
            priceNode->AtkResNode.Width = 50;
            priceNode->AtkResNode.Color = baseNode->AtkResNode.Color;
            priceNode->TextColor = baseNode->TextColor;
            priceNode->EdgeColor = baseNode->EdgeColor;
            priceNode->LineSpacing = 18;
            priceNode->FontSize = 12;
            priceNode->TextFlags = (byte)((TextFlags)baseNode->TextFlags | TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize);
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
        priceNode->AtkResNode.SetYFloat(itemTooltip->WindowNode->AtkResNode.Height - 8);
        itemTooltip->WindowNode->AtkResNode.SetHeight((ushort)(itemTooltip->WindowNode->AtkResNode.Height + priceNode->AtkResNode.Height + 4));
        itemTooltip->WindowNode->Component->UldManager.RootNode->SetHeight(itemTooltip->WindowNode->AtkResNode.Height);
        itemTooltip->WindowNode->Component->UldManager.RootNode->PrevSiblingNode->SetHeight(itemTooltip->WindowNode->AtkResNode.Height);
        var remainingSpace = ImGuiHelpers.MainViewport.WorkSize.Y - itemTooltip->Y - (itemTooltip->WindowNode->AtkResNode.Height * plugin.gameUiScale) - 36;
        if (remainingSpace < 0) {
            plugin.Hooks.ItemDetailSetPositionPreservingOriginal(itemTooltip, itemTooltip->X, (short)(itemTooltip->Y + remainingSpace), 1);
        }

        insertNode->SetYFloat(insertNode->Y + priceNode->AtkResNode.Height + 4);
    }

    private List<Payload> ParseMbData(bool hq, MarketBoardData? mbData, LookupState lookupState) {
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
            var minWorld = GetNqHqData(mbData.MinimumPriceNQ?.World, mbData.MinimumPriceHQ?.World);
            var minDc = GetNqHqData(mbData.RegionMinimumPriceNQ?.Datacenter, mbData.RegionMinimumPriceHQ?.Datacenter);

            var priceHeader = false;
            void PriceHeader() {
                if (priceHeader) return;
                payloads.Add(new TextPayload("Marketboard Price:"));
                priceHeader = true;
            }

            void PrintNqHq<T>(T? nqPrice, T? hqPrice, string format = "N0", bool withGilIcon = true) where T : unmanaged, INumberBase<T> {
                if (nqPrice != null && (plugin.Configuration.ShowBothNqAndHq || !hq)) {
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{nqPrice.Value.ToString(format, null)}{(withGilIcon ? GilIcon : "")}"));
                    if (plugin.Configuration.ShowStackSalePrice && !hq && LastItemQuantity > 1 && withGilIcon)
                        payloads.Add(new TextPayload($" ({(nqPrice.Value * T.CreateChecked(LastItemQuantity.Value)).ToString(format, null)}{GilIcon})"));
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(0));
                }
                if (hqPrice != null && (plugin.Configuration.ShowBothNqAndHq || hq)) {
                    if (nqPrice != null && plugin.Configuration.ShowBothNqAndHq)
                        payloads.Add(new TextPayload("/"));

                    if (hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{HQIcon}{hqPrice.Value.ToString(format, null)}{(withGilIcon ? GilIcon : "")}"));
                    if (plugin.Configuration.ShowStackSalePrice && hq && LastItemQuantity > 1 && withGilIcon)
                        payloads.Add(new TextPayload($" ({(hqPrice.Value * T.CreateChecked(LastItemQuantity.Value)).ToString(format, null)}{GilIcon})"));
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

            T? GetNqHqData<T>(T? nqData, T? hqData) {
                var result = hq ? hqData : nqData;
                if (plugin.Configuration.ShowBothNqAndHq)
                    result ??= hq ? nqData : hqData;
                return result;
            }

            if (minDc != ownDc && minDc != null && plugin.Configuration.ShowRegion) {
                PriceHeader();

                var minWorldRegion = hq
                    ? mbData.RegionMinimumPriceHQ?.World ?? mbData.RegionMinimumPriceNQ?.World
                    : mbData.RegionMinimumPriceNQ?.World ?? mbData.RegionMinimumPriceHQ?.World;

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{minWorldRegion}"));
                if (plugin.Configuration.ShowDatacenterOnCrossWorlds)
                    payloads.Add(new TextPayload($" {minDc}"));
                payloads.Add(new TextPayload("): "));
                PrintNqHq(mbData.RegionMinimumPriceNQ?.Price, mbData.RegionMinimumPriceHQ?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.RegionMinimumPriceHQ?.Time : mbData.RegionMinimumPriceNQ?.Time;
                    PrintTime(recentTime);
                }
            }

            if (minWorld != ownWorld && minWorld != null && (plugin.Configuration.ShowDatacenter || (plugin.Configuration.ShowRegion && minDc == ownDc))) {
                PriceHeader();

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{minWorld}): "));
                PrintNqHq(mbData.MinimumPriceNQ?.Price, mbData.MinimumPriceHQ?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.MinimumPriceHQ?.Time : mbData.MinimumPriceNQ?.Time;
                    PrintTime(recentTime);
                }
            }

            if (GetNqHqData(mbData.OwnMinimumPriceNQ,  mbData.OwnMinimumPriceHQ) != null && (plugin.Configuration.ShowWorld || (plugin.Configuration.ShowDatacenter && minWorld == ownWorld))) {
                PriceHeader();

                payloads.Add(new TextPayload($"\n  Home ({ownWorld}): "));
                PrintNqHq(mbData.OwnMinimumPriceNQ?.Price, mbData.OwnMinimumPriceHQ?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.OwnMinimumPriceHQ?.Time : mbData.OwnMinimumPriceNQ?.Time;
                    PrintTime(recentTime);
                }
            }

            var recentHeader = false;
            void RecentHeader() {
                if (recentHeader) return;
                if (payloads.Count > 0)
                    payloads.Add(new TextPayload("\n"));
                payloads.Add(new TextPayload("Most Recent Purchase:"));
                recentHeader = true;
            }

            var recentWorld = GetNqHqData(mbData.MostRecentPurchaseNQ?.World, mbData.MostRecentPurchaseHQ?.World);
            var recentDc = GetNqHqData(mbData.RegionMostRecentPurchaseNQ?.Datacenter, mbData.RegionMostRecentPurchaseHQ?.Datacenter);
            if (recentDc != ownDc && recentDc != null && plugin.Configuration.ShowMostRecentPurchaseRegion) {
                RecentHeader();

                var recentWorldRegion = hq
                    ? mbData.RegionMostRecentPurchaseHQ?.World ?? mbData.RegionMostRecentPurchaseNQ?.World
                    : mbData.RegionMostRecentPurchaseNQ?.World ?? mbData.RegionMostRecentPurchaseHQ?.World;

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{recentWorldRegion} {recentDc}): "));
                PrintNqHq(mbData.RegionMostRecentPurchaseNQ?.Price, mbData.RegionMostRecentPurchaseHQ?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.RegionMostRecentPurchaseHQ?.Time : mbData.RegionMostRecentPurchaseNQ?.Time;
                    PrintTime(recentTime);
                }
            }

            if (recentWorld != ownWorld && recentWorld != null && (plugin.Configuration.ShowMostRecentPurchase || (plugin.Configuration.ShowMostRecentPurchaseRegion && recentDc == ownDc))) {
                RecentHeader();

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{recentWorld}): "));
                PrintNqHq(mbData.MostRecentPurchaseNQ?.Price, mbData.MostRecentPurchaseHQ?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.MostRecentPurchaseHQ?.Time : mbData.MostRecentPurchaseNQ?.Time;
                    PrintTime(recentTime);
                }
            }

            if (GetNqHqData(mbData.OwnMostRecentPurchaseNQ, mbData.OwnMostRecentPurchaseHQ) != null && (plugin.Configuration.ShowMostRecentPurchaseWorld || (plugin.Configuration.ShowMostRecentPurchase && recentWorld == ownWorld))) {
                RecentHeader();

                payloads.Add(new TextPayload($"\n  Home ({ownWorld}): "));
                PrintNqHq(mbData.OwnMostRecentPurchaseNQ?.Price, mbData.OwnMostRecentPurchaseHQ?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.OwnMostRecentPurchaseHQ?.Time : mbData.OwnMostRecentPurchaseNQ?.Time;
                    PrintTime(recentTime);
                }
            }

            if (GetNqHqData(mbData.AverageSalePriceNQ, mbData.AverageSalePriceHQ) != null && plugin.Configuration.ShowAverageSalePrice) {
                if (payloads.Count > 0)
                    payloads.Add(new TextPayload("\n"));
                payloads.Add(new TextPayload($"Average sale price ({mbData.Scope}): "));
                PrintNqHq(mbData.AverageSalePriceNQ, mbData.AverageSalePriceHQ);
            }

            if (GetNqHqData(mbData.DailySaleVelocityNQ, mbData.DailySaleVelocityHQ) != null && plugin.Configuration.ShowDailySaleVelocity) {
                if (payloads.Count > 0)
                    payloads.Add(new TextPayload("\n"));
                payloads.Add(new TextPayload($"Sales per day ({mbData.Scope}): "));
                PrintNqHq(mbData.DailySaleVelocityNQ, mbData.DailySaleVelocityHQ, format: "N1", withGilIcon: false);
            }

            if (payloads.Count == 0) {
                payloads.Add(new UIForegroundPayload(20));
                payloads.Add(new TextPayload("No marketboard info is known for this item.\nTry opening the ingame marketboard."));
                payloads.Add(new UIForegroundPayload(0));
            }
        }

        return payloads;
    }

    public void Refresh(IDictionary<uint, MarketBoardData> mbData) {
        if (Service.GameGui.HoveredItem >= 2000000) return;
        if (mbData.TryGetValue((uint)(Service.GameGui.HoveredItem % 500000), out var data)) {
            var newText = ParseMbData(Service.GameGui.HoveredItem >= 500000, data, LookupState.Marketable);
            Service.Framework.RunOnFrameworkThread(() => {
                try {
                    var tooltip = Service.GameGui.GetAddonByName("ItemDetail");
                    unsafe {
                        if (tooltip == nint.Zero || !((AtkUnitBase*)tooltip)->IsVisible)
                            return;
                        RestoreToNormal((AtkUnitBase*)tooltip);
                        UpdateItemTooltip((AtkUnitBase*)tooltip, newText);
                    }
                } catch (Exception e) {
                    Service.PluginLog.Error(e, "Failed to update tooltip");
                }
            });
        }
    }

    public void FetchFailed(IList<uint> items) {
        if (!items.Contains((uint)Service.GameGui.HoveredItem % 500000)) return;
        var newText = ParseMbData(false, null, LookupState.Faulted);
        Service.Framework.RunOnFrameworkThread(() => {
            try {
                var tooltip = Service.GameGui.GetAddonByName("ItemDetail");
                unsafe {
                    if (tooltip == nint.Zero || !((AtkUnitBase*)tooltip)->IsVisible)
                        return;
                    RestoreToNormal((AtkUnitBase*)tooltip);
                    UpdateItemTooltip((AtkUnitBase*)tooltip, newText);
                }
            } catch (Exception e) {
                Service.PluginLog.Error(e, "Failed to update tooltip");
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
                if (node->NodeId != NodeId)
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
