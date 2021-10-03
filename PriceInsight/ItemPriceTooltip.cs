using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace PriceInsight {
    public class ItemPriceTooltip {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);
        private readonly PriceInsightPlugin plugin;

        public ItemPriceTooltip(PriceInsightPlugin plugin) {
            this.plugin = plugin;
        }

        public void OnItemTooltip(ItemTooltip tooltip) {
            var id = plugin.GameGui.HoveredItem;
            var hq = id >= 500000;
            id %= 500000;
            var (marketBoardData, isMarketable) = plugin.ItemPriceLookup.Get(id);
            if (!isMarketable) return;
            var payloads = new List<Payload>();
            if (marketBoardData == null) {
                payloads.Add(new TextPayload("\n"));
                payloads.Add(new IconPayload(BitmapFontIcon.LevelSync));
                payloads.Add(new TextPayload(" Marketboard info is being obtained..\nTap Ctrl to refresh."));
            } else {
                var mb = marketBoardData.Value;
                if (plugin.Configuration.IgnoreOldData && DateTime.Now.Subtract(Epoch.AddMilliseconds(mb.LastUploadTime)).TotalDays > 29) return;
                var minWorld = hq ? mb.MinimumPriceWorldHQ : mb.MinimumPriceWorldNQ;
                if (plugin.Configuration.ShowDatacenter && minWorld != mb.OwnWorld) {
                    payloads.Add(new TextPayload("\n"));
                    payloads.Add(new TextPayload("Marketboard Price ("));
                    payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                    payloads.Add(new TextPayload($"{minWorld}): "));
                    if (!hq) payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{mb.MinimumPriceNQ:N0}"));
                    if (!hq) payloads.Add(new UIForegroundPayload(0));
                    if (mb.MinimumPriceHQ != 0) {
                        payloads.Add(new TextPayload("/"));
                        if (hq) payloads.Add(new UIForegroundPayload(506));
                        payloads.Add(new TextPayload($"{mb.MinimumPriceHQ:N0}"));
                        if (hq) payloads.Add(new UIForegroundPayload(0));
                    }
                }

                if (plugin.Configuration.ShowWorld && mb.OwnWorld != null || minWorld == mb.OwnWorld) {
                    payloads.Add(new TextPayload("\n"));
                    payloads.Add(new TextPayload($"Marketboard Price ({mb.OwnWorld}): "));
                    if (!hq) payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{mb.OwnMinimumPriceNQ:N0}"));
                    if (!hq) payloads.Add(new UIForegroundPayload(0));
                    if (mb.OwnMinimumPriceHQ != 0) {
                        payloads.Add(new TextPayload("/"));
                        if (hq) payloads.Add(new UIForegroundPayload(506));
                        payloads.Add(new TextPayload($"{mb.OwnMinimumPriceHQ:N0}"));
                        if (hq) payloads.Add(new UIForegroundPayload(0));
                    }
                }

                var recentWorld = hq ? mb.MostRecentPurchaseWorldHQ : mb.MostRecentPurchaseWorldNQ;
                if (plugin.Configuration.ShowMostRecentPurchase && recentWorld != mb.OwnWorld) {
                    payloads.Add(new TextPayload("\n"));
                    payloads.Add(new TextPayload("Recent Purchase ("));
                    payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                    payloads.Add(new TextPayload($"{recentWorld}): "));
                    if (!hq) payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{mb.MostRecentPurchaseNQ:N0}"));
                    if (!hq) payloads.Add(new UIForegroundPayload(0));
                    if (mb.MostRecentPurchaseHQ != 0) {
                        payloads.Add(new TextPayload("/"));
                        if (hq) payloads.Add(new UIForegroundPayload(506));
                        payloads.Add(new TextPayload($"{mb.MostRecentPurchaseHQ:N0}"));
                        if (hq) payloads.Add(new UIForegroundPayload(0));
                    }
                }

                if (plugin.Configuration.ShowMostRecentPurchaseWorld && mb.OwnMostRecentPurchaseNQ + mb.OwnMostRecentPurchaseHQ > 0 ||
                    recentWorld == mb.OwnWorld) {
                    payloads.Add(new TextPayload("\n"));
                    payloads.Add(new TextPayload($"Recent Purchase ({mb.OwnWorld}): "));
                    if (!hq) payloads.Add(new UIForegroundPayload(506));
                    payloads.Add(new TextPayload($"{mb.OwnMostRecentPurchaseNQ:N0}"));
                    if (!hq) payloads.Add(new UIForegroundPayload(0));
                    if (mb.OwnMostRecentPurchaseHQ != 0) {
                        payloads.Add(new TextPayload("/"));
                        if (hq) payloads.Add(new UIForegroundPayload(506));
                        payloads.Add(new TextPayload($"{mb.OwnMostRecentPurchaseHQ:N0}"));
                        if (hq) payloads.Add(new UIForegroundPayload(0));
                    }
                }
            }


            if (payloads.Count > 0) {
                var description = tooltip[ItemTooltip.TooltipField.ControlsDisplay];
                tooltip[ItemTooltip.TooltipField.ControlsDisplay] = description.Append(payloads);
                Task.Run(async () => {
                    await Task.Delay(7);
                    Helper.SetControlsSectionHeight(plugin.GameGui, description.TextValue.Count(c => c == '\n') * 18 + 26);
                });
            }
        }
    }
}