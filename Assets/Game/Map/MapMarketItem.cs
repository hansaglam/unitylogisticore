namespace LogistiCore.Map
{
    /// <summary>
    /// One getMyMarketState condition. No price/currency on this DTO.
    /// trendBps=0 means trend is not calculated this phase.
    /// </summary>
    public readonly struct MapMarketItem
    {
        public MapMarketItem(
            string cityId,
            string cargoId,
            bool hasDemandIndex,
            int demandIndex,
            bool hasSupplyIndex,
            int supplyIndex,
            bool hasAdjustment,
            int adjustmentBps,
            int trendBps,
            bool hasLatitude = false,
            double latitude = 0d,
            bool hasLongitude = false,
            double longitude = 0d)
        {
            CityId = cityId ?? "";
            CargoId = cargoId ?? "";
            HasDemandIndex = hasDemandIndex;
            DemandIndex = demandIndex;
            HasSupplyIndex = hasSupplyIndex;
            SupplyIndex = supplyIndex;
            HasAdjustment = hasAdjustment;
            AdjustmentBps = adjustmentBps;
            TrendBps = trendBps;
            HasLatitude = hasLatitude;
            Latitude = latitude;
            HasLongitude = hasLongitude;
            Longitude = longitude;
        }

        public string CityId { get; }
        public string CargoId { get; }
        public bool HasDemandIndex { get; }
        public int DemandIndex { get; }
        public bool HasSupplyIndex { get; }
        public int SupplyIndex { get; }
        public bool HasAdjustment { get; }
        public int AdjustmentBps { get; }
        public int TrendBps { get; }
        public bool HasLatitude { get; }
        public double Latitude { get; }
        public bool HasLongitude { get; }
        public double Longitude { get; }

        public string ConditionId => CityId + "/" + CargoId;
        public bool HasPrice => false;
        public bool HasCurrency => false;
        public bool HasTrend => false;
        public bool HasKnownCity => MapCities.TryGet(CityId, out _);
        public bool HasGps
        {
            get
            {
                if (!HasLatitude || !HasLongitude)
                    return false;
                return Latitude >= -90d && Latitude <= 90d && Longitude >= -180d && Longitude <= 180d;
            }
        }

        public string AdjustmentText
        {
            get
            {
                if (!HasAdjustment)
                    return "";
                if (AdjustmentBps > 0)
                    return "+" + AdjustmentBps;
                return AdjustmentBps.ToString();
            }
        }

        public bool InCargo(string cargoFilter)
        {
            if (string.IsNullOrEmpty(cargoFilter))
                return true;
            return CargoId == cargoFilter;
        }
    }
}
