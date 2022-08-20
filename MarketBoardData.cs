using System;

namespace PriceInsight; 

public readonly struct MarketBoardData {
    public DateTime? LastUploadTime { get; init; }
    public Listing? MinimumPriceNQ { get; init; }
    public Listing? MinimumPriceHQ { get; init; }
    public Listing? OwnMinimumPriceNQ { get; init; }
    public Listing? OwnMinimumPriceHQ { get; init; }
    public Listing? MostRecentPurchaseHQ { get; init; }
    public Listing? MostRecentPurchaseNQ { get; init; }
    public Listing? OwnMostRecentPurchaseHQ { get; init; }
    public Listing? OwnMostRecentPurchaseNQ { get; init; }
}

public readonly struct Listing {
    public long Price { get; init; }
    public string World { get; init; }
    public DateTime Time { get; init; }
}