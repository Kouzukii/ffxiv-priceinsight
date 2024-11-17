using System;
using System.Collections.Generic;
using System.Globalization;
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

    private static readonly CultureInfo FormatProvider = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator == "\u2009"
        ? CultureInfo.InvariantCulture
        : CultureInfo.CurrentCulture;

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
            var baseNode = itemTooltip->GetTextNodeById(44);
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
        itemTooltip->WindowNode->SetHeight((ushort)(itemTooltip->WindowNode->AtkResNode.Height + priceNode->AtkResNode.Height + 4));
        itemTooltip->WindowNode->AtkResNode.SetHeight(itemTooltip->WindowNode->Height);
        itemTooltip->WindowNode->Component->UldManager.RootNode->SetHeight(itemTooltip->WindowNode->Height);
        itemTooltip->WindowNode->Component->UldManager.RootNode->PrevSiblingNode->SetHeight(itemTooltip->WindowNode->Height);
        itemTooltip->RootNode->SetHeight(itemTooltip->WindowNode->Height);
        var remainingSpace = ImGuiHelpers.MainViewport.WorkSize.Y - itemTooltip->Y - itemTooltip->GetScaledHeight(true) - 36;
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
            payloads.Add(new TextPayload(" Failed to obtain marketboard info.\n        The Universalis API is likely experiencing issues.\n        Please be patient or check the Universalis discord.\n        Press alt to retry or check the /xllog."));
            payloads.Add(new UIForegroundPayload(0));
        } else if (mbData == null) {
            payloads.Add(new UIForegroundPayload(20));
            payloads.Add(new IconPayload(BitmapFontIcon.LevelSync));
            payloads.Add(new TextPayload(" Marketboard info is being obtained.."));
            payloads.Add(new UIForegroundPayload(0));
        } else {
            var ownWorld = mbData.HomeWorld;
            var ownDc = mbData.Datacenter;
            var minWorld = GetNqHqData(mbData.MinimumPrice.Datacenter.Nq?.World, mbData.MinimumPrice.Datacenter.Hq?.World);
            var minDc = GetNqHqData(mbData.MinimumPrice.Region.Nq?.Datacenter, mbData.MinimumPrice.Region.Hq?.Datacenter);

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
                    payloads.Add(new TextPayload($"{nqPrice.Value.ToString(format, FormatProvider)}{(withGilIcon ? GilIcon : "")}"));
                    if (plugin.Configuration.ShowStackSalePrice && !hq && LastItemQuantity > 1 && withGilIcon)
                        payloads.Add(new TextPayload($" ({(nqPrice.Value * T.CreateChecked(LastItemQuantity.Value)).ToString(format, FormatProvider)}{GilIcon})"));
                    if (!hq)
                        payloads.Add(new UIForegroundPayload(0));
                }
                if (hqPrice != null && (plugin.Configuration.ShowBothNqAndHq || hq)) {
                    if (nqPrice != null && plugin.Configuration.ShowBothNqAndHq)
                        payloads.Add(new TextPayload("/"));

                    if (hq)
                        payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{HQIcon}{hqPrice.Value.ToString(format, FormatProvider)}{(withGilIcon ? GilIcon : "")}"));
                    if (plugin.Configuration.ShowStackSalePrice && hq && LastItemQuantity > 1 && withGilIcon)
                        payloads.Add(new TextPayload($" ({(hqPrice.Value * T.CreateChecked(LastItemQuantity.Value)).ToString(format, FormatProvider)}{GilIcon})"));
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
                if ((hq ? hqData : nqData) is { } result)
                    return result;
                if (plugin.Configuration.ShowBothNqAndHq)
                    return hq ? nqData : hqData;
                return default;
            }

            if (minDc != ownDc && minDc != null && plugin.Configuration.ShowRegion) {
                PriceHeader();

                var minWorldRegion = hq
                    ? mbData.MinimumPrice.Region.Hq?.World ?? mbData.MinimumPrice.Region.Nq?.World
                    : mbData.MinimumPrice.Region.Nq?.World ?? mbData.MinimumPrice.Region.Hq?.World;

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{minWorldRegion}"));
                if (plugin.Configuration.ShowDatacenterOnCrossWorlds)
                    payloads.Add(new TextPayload($" {minDc}"));
                payloads.Add(new TextPayload("): "));
                PrintNqHq(mbData.MinimumPrice.Region.Nq?.Price, mbData.MinimumPrice.Region.Hq?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.MinimumPrice.Region.Hq?.Time : mbData.MinimumPrice.Region.Nq?.Time;
                    PrintTime(recentTime);
                }
            }

            if (minWorld != ownWorld && minWorld != null && (plugin.Configuration.ShowDatacenter || (plugin.Configuration.ShowRegion && minDc == ownDc))) {
                PriceHeader();

                payloads.Add(new TextPayload("\n  Cheapest ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{minWorld}): "));
                PrintNqHq(mbData.MinimumPrice.Datacenter.Nq?.Price, mbData.MinimumPrice.Datacenter.Hq?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.MinimumPrice.Datacenter.Hq?.Time : mbData.MinimumPrice.Datacenter.Nq?.Time;
                    PrintTime(recentTime);
                }
            }

            if (GetNqHqData(mbData.MinimumPrice.World.Nq,  mbData.MinimumPrice.World.Hq) != null && (plugin.Configuration.ShowWorld || (plugin.Configuration.ShowDatacenter && minWorld == ownWorld))) {
                PriceHeader();

                payloads.Add(new TextPayload($"\n  Home ({ownWorld}): "));
                PrintNqHq(mbData.MinimumPrice.World.Nq?.Price, mbData.MinimumPrice.World.Hq?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.MinimumPrice.World.Hq?.Time : mbData.MinimumPrice.World.Nq?.Time;
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

            var recentWorld = GetNqHqData(mbData.MostRecentPurchase.Datacenter.Nq?.World, mbData.MostRecentPurchase.Datacenter.Hq?.World);
            var recentDc = GetNqHqData(mbData.MostRecentPurchase.Region.Nq?.Datacenter, mbData.MostRecentPurchase.Region.Hq?.Datacenter);
            if (recentDc != ownDc && recentDc != null && plugin.Configuration.ShowMostRecentPurchaseRegion) {
                RecentHeader();

                var recentWorldRegion = hq
                    ? mbData.MostRecentPurchase.Region.Hq?.World ?? mbData.MostRecentPurchase.Region.Nq?.World
                    : mbData.MostRecentPurchase.Region.Nq?.World ?? mbData.MostRecentPurchase.Region.Hq?.World;

                payloads.Add(new TextPayload("\n  Region ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{recentWorldRegion} {recentDc}): "));
                PrintNqHq(mbData.MostRecentPurchase.Region.Nq?.Price, mbData.MostRecentPurchase.Region.Hq?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.MostRecentPurchase.Region.Hq?.Time : mbData.MostRecentPurchase.Region.Nq?.Time;
                    PrintTime(recentTime);
                }
            }

            if (recentWorld != ownWorld && recentWorld != null && (plugin.Configuration.ShowMostRecentPurchase || (plugin.Configuration.ShowMostRecentPurchaseRegion && recentDc == ownDc))) {
                RecentHeader();

                payloads.Add(new TextPayload("\n  Datacenter ("));
                payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                payloads.Add(new TextPayload($"{recentWorld}): "));
                PrintNqHq(mbData.MostRecentPurchase.Datacenter.Nq?.Price, mbData.MostRecentPurchase.Datacenter.Hq?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.MostRecentPurchase.Datacenter.Hq?.Time : mbData.MostRecentPurchase.Datacenter.Nq?.Time;
                    PrintTime(recentTime);
                }
            }

            if (GetNqHqData(mbData.MostRecentPurchase.World.Nq, mbData.MostRecentPurchase.World.Hq) != null && (plugin.Configuration.ShowMostRecentPurchaseWorld || (plugin.Configuration.ShowMostRecentPurchase && recentWorld == ownWorld))) {
                RecentHeader();

                payloads.Add(new TextPayload($"\n  Home ({ownWorld}): "));
                PrintNqHq(mbData.MostRecentPurchase.World.Nq?.Price, mbData.MostRecentPurchase.World.Hq?.Price);

                if (plugin.Configuration.ShowAge) {
                    var recentTime = hq ? mbData.MostRecentPurchase.World.Hq?.Time : mbData.MostRecentPurchase.World.Nq?.Time;
                    PrintTime(recentTime);
                }
            }

            if (plugin.Configuration.ShowAverageSalePriceIn > 0) {
                var (salePrice, scope) = plugin.Configuration.ShowAverageSalePriceIn switch {
                    1 => (mbData.AverageSalePrice.World, mbData.HomeWorld),
                    2 => (mbData.AverageSalePrice.Datacenter, mbData.Datacenter),
                    3 => (mbData.AverageSalePrice.Region, mbData.Region),
                    _ => (null, null),
                };
                if (salePrice != null && GetNqHqData(salePrice.Nq, salePrice.Hq) != null) {
                    if (payloads.Count > 0)
                        payloads.Add(new TextPayload("\n"));
                    payloads.Add(new TextPayload($"Average sale price ({scope}): "));
                    PrintNqHq(salePrice.Nq, salePrice.Hq);
                }
            }

            if (plugin.Configuration.ShowDailySaleVelocityIn > 0) {
                var (saleVelocity, scope) = plugin.Configuration.ShowDailySaleVelocityIn switch {
                    1 => (mbData.DailySaleVelocity.World, mbData.HomeWorld),
                    2 => (mbData.DailySaleVelocity.Datacenter, mbData.Datacenter),
                    3 => (mbData.DailySaleVelocity.Region, mbData.Region),
                    _ => (null, null),
                };
                if (saleVelocity != null && GetNqHqData(saleVelocity.Nq, saleVelocity.Hq) != null) {
                    if (payloads.Count > 0)
                        payloads.Add(new TextPayload("\n"));
                    payloads.Add(new TextPayload($"Sales per day ({scope}): "));
                    PrintNqHq(saleVelocity.Nq, saleVelocity.Hq, format: "N1", withGilIcon: false);
                }
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

    public void FetchFailed(ICollection<uint> items) {
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
