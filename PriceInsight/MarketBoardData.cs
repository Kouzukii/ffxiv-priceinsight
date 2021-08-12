namespace PriceInsight {
    public struct MarketBoardData {
        public long LastUploadTime { get; set; }
        public long MinimumPriceNQ { get; set; }
        public long MinimumPriceHQ { get; set; }
        public long OwnMinimumPriceNQ { get; set; }
        public long OwnMinimumPriceHQ { get; set; }
        public long MostRecentPurchaseHQ { get; set; }
        public long MostRecentPurchaseNQ { get; set; }
        public long OwnMostRecentPurchaseHQ { get; set; }
        public long OwnMostRecentPurchaseNQ { get; set; }
        public string OwnWorld { get; set; }
        public string MinimumPriceWorldNQ { get; set; }
        public string MinimumPriceWorldHQ { get; set; }
        public string MostRecentPurchaseWorldNQ { get; set; }
        public string MostRecentPurchaseWorldHQ { get; set; }
    }
}