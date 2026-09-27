using System;
using System.Collections.Generic;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace PriceInsight;

/// <summary>An unsellable item that can be turned into a marketable one.</summary>
public sealed record ItemConversion(uint SourceItemId, uint TargetItemId, string Process);

/// <summary>Declares a family of item conversions, ideally derived from game data.</summary>
public interface IItemConversionSource {
    IEnumerable<ItemConversion> Load(ExcelModule excel);
}

/// <summary>Diadem "Grade N Skybuilders' X" -> "Approved Grade N Skybuilders' X", via the Skybuilders' inspection.</summary>
internal sealed class SkybuilderInspectionSource : IItemConversionSource {
    public IEnumerable<ItemConversion> Load(ExcelModule excel) {
        // Each row is a group of inspections (mining/botany, fish); each entry holds the required item (RequiredItem,
        // or FishParameter for fish) and the approved/target item it's exchanged for (ItemReceived).
        foreach (var row in excel.GetSheet<HWDGathererInspection>()) {
            foreach (var entry in row.HWDGathererInspectionData) {
                // The "Approved" item we get back; skip empty slots.
                var targetId = entry.ItemReceived.RowId;
                if (targetId == 0)
                    continue;
                // The item handed in, linked via GatheringItem or, for fish, FishParameter; skip if neither resolves.
                var sourceId = entry.RequiredItem.ValueNullable?.Item.RowId ?? 0;
                if (sourceId == 0)
                    sourceId = entry.FishParameter.ValueNullable?.Item.RowId ?? 0;
                if (sourceId == 0)
                    continue;
                yield return new ItemConversion(sourceId, targetId, "inspection");
            }
        }
    }
}

public class ItemConversions {
    private static readonly IItemConversionSource[] Sources = [
        new SkybuilderInspectionSource(),
    ];

    private readonly Lazy<Dictionary<uint, ItemConversion>> map = new(Build);

    public bool TryGet(uint itemId, out ItemConversion conversion) => map.Value.TryGetValue(itemId, out conversion!);

    private static Dictionary<uint, ItemConversion> Build() {
        var result = new Dictionary<uint, ItemConversion>();
        foreach (var source in Sources) {
            try {
                foreach (var conversion in source.Load(Service.DataManager.Excel)) {
                    if (!result.TryAdd(conversion.SourceItemId, conversion))
                        Service.PluginLog.Debug($"Duplicate item conversion for {conversion.SourceItemId} from {source.GetType().Name}, ignoring");
                }
            } catch (Exception e) {
                Service.PluginLog.Error(e, $"Failed to load item conversions from {source.GetType().Name}");
            }
        }

        Service.PluginLog.Debug($"Loaded {result.Count} item conversions");
        return result;
    }
}
