using System;

namespace PriceInsight; 

public readonly struct MarketBoardData {
    public DateTime? LastUploadTime { get; init; }
    public Listing? MinimumPriceNQ { get; init; }
    public Listing? MinimumPriceHQ { get; init; }
    public Listing? OwnMinimumPriceNQ { get; init; }
    public Listing? OwnMinimumPriceHQ { get; init; }
    public Listing? RegionMinimumPriceNQ { get; init; }
    public Listing? RegionMinimumPriceHQ { get; init; }
    public Listing? MostRecentPurchaseHQ { get; init; }
    public Listing? MostRecentPurchaseNQ { get; init; }
    public Listing? OwnMostRecentPurchaseHQ { get; init; }
    public Listing? OwnMostRecentPurchaseNQ { get; init; }
    public Listing? RegionMostRecentPurchaseHQ { get; init; }
    public Listing? RegionMostRecentPurchaseNQ { get; init; }
    public string HomeWorld { get; init; }
    public string HomeDatacenter { get; init; }
}

public readonly struct Listing {
    public long Price { get; init; }
    public string World { get; init; }
    public string Datacenter { get; init; }
    public DateTime Time { get; init; }
}