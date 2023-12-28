using System;

namespace PriceInsight; 

public record MarketBoardData {
    public required Listing? MinimumPriceNQ { get; init; }
    public required Listing? MinimumPriceHQ { get; init; }
    public required Listing? OwnMinimumPriceNQ { get; init; }
    public required Listing? OwnMinimumPriceHQ { get; init; }
    public required Listing? RegionMinimumPriceNQ { get; init; }
    public required Listing? RegionMinimumPriceHQ { get; init; }
    public required Listing? MostRecentPurchaseHQ { get; init; }
    public required Listing? MostRecentPurchaseNQ { get; init; }
    public required Listing? OwnMostRecentPurchaseHQ { get; init; }
    public required Listing? OwnMostRecentPurchaseNQ { get; init; }
    public required Listing? RegionMostRecentPurchaseHQ { get; init; }
    public required Listing? RegionMostRecentPurchaseNQ { get; init; }
    public required double? AverageSalePriceNQ { get; init; }
    public required double? AverageSalePriceHQ { get; init; }
    public required double? DailySaleVelocityNQ { get; init; }
    public required double? DailySaleVelocityHQ { get; init; }
    public required string HomeWorld { get; init; }
    public required string HomeDatacenter { get; init; }
    public required string Scope { get; init; }
}

public record Listing {
    public required long Price { get; init; }
    public required string? World { get; init; }
    public required string? Datacenter { get; init; }
    public required DateTime? Time { get; init; }
}