using System;

namespace PriceInsight;

public record MarketBoardData {
    public required Group<Listing> MinimumPrice { get; init; }
    public required Group<Listing> MostRecentPurchase { get; init; }
    public required Group<double?> AverageSalePrice { get; init; }
    public required Group<double?> DailySaleVelocity { get; init; }
    public required string HomeWorld { get; init; }
    public required string Datacenter { get; init; }
    public required string Region { get; init; }
}

public record Group<T> {
    public required Quality<T> World { get; init; }
    public required Quality<T> Datacenter { get; init; }
    public required Quality<T> Region { get; init; }
}

public record Quality<T> {
    public required T? Nq { get; init; }
    public required T? Hq { get; init; }
}

public record Listing {
    public required long Price { get; init; }
    public required string? World { get; init; }
    public required string? Datacenter { get; init; }
    public required DateTime? Time { get; init; }
}