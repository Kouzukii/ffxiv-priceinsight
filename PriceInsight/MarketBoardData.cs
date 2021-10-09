namespace PriceInsight {
    public readonly struct MarketBoardData {
        public long LastUploadTime { get; init; }
        public long MinimumPriceNQ { get; init; }
        public long MinimumPriceHQ { get; init; }
        public long OwnMinimumPriceNQ { get; init; }
        public long OwnMinimumPriceHQ { get; init; }
        public long MostRecentPurchaseHQ { get; init; }
        public long MostRecentPurchaseNQ { get; init; }
        public long OwnMostRecentPurchaseHQ { get; init; }
        public long OwnMostRecentPurchaseNQ { get; init; }
        public string? OwnWorld { get; init; }
        public string? MinimumPriceWorldNQ { get; init; }
        public string? MinimumPriceWorldHQ { get; init; }
        public string? MostRecentPurchaseWorldNQ { get; init; }
        public string? MostRecentPurchaseWorldHQ { get; init; }
    }
}