namespace LogistiCore.Map
{
    /// <summary>
    /// One getMyWarehouseState row. Capacity stays decimal strings. No float math.
    /// Optional lat/lon are not in the current DTO; they exist only so invalid GPS can be rejected.
    /// </summary>
    public readonly struct MapWarehouseItem
    {
        public MapWarehouseItem(
            string warehouseId,
            string catalogId,
            string type,
            string cityId,
            MapWarehouseSiteStatus siteStatus,
            int level,
            string capacityModelId,
            string capacityUnits,
            string usedCapacity,
            bool owned,
            bool hasLatitude = false,
            double latitude = 0d,
            bool hasLongitude = false,
            double longitude = 0d)
        {
            WarehouseId = warehouseId ?? "";
            CatalogId = catalogId ?? "";
            Type = type ?? "";
            CityId = cityId ?? "";
            SiteStatus = siteStatus;
            Level = level;
            CapacityModelId = capacityModelId ?? "";
            CapacityUnits = capacityUnits ?? "";
            UsedCapacity = usedCapacity ?? "";
            Owned = owned;
            HasLatitude = hasLatitude;
            Latitude = latitude;
            HasLongitude = hasLongitude;
            Longitude = longitude;
        }

        public string WarehouseId { get; }
        public string CatalogId { get; }
        public string Type { get; }
        public string CityId { get; }
        public MapWarehouseSiteStatus SiteStatus { get; }
        public int Level { get; }
        public string CapacityModelId { get; }
        public string CapacityUnits { get; }
        public string UsedCapacity { get; }
        public bool Owned { get; }
        public bool HasLatitude { get; }
        public double Latitude { get; }
        public bool HasLongitude { get; }
        public double Longitude { get; }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(CatalogId))
                    return CatalogId;
                if (!string.IsNullOrEmpty(Type))
                    return Type;
                return WarehouseId;
            }
        }

        public bool HasLevel => Level >= 1;
        public bool HasCapacityUnits => !string.IsNullOrEmpty(CapacityUnits);
        public bool HasUsedCapacity => !string.IsNullOrEmpty(UsedCapacity);
        public bool HasCapacityModel => !string.IsNullOrEmpty(CapacityModelId);
        public bool HasMatchingCapacity => HasCapacityUnits && HasUsedCapacity && HasCapacityModel;
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

        public string CapacitySummary
        {
            get
            {
                if (HasMatchingCapacity)
                    return UsedCapacity + " / " + CapacityUnits;
                if (HasCapacityUnits)
                    return CapacityUnits;
                return "";
            }
        }

        public static MapWarehouseSiteStatus ParseStatus(string status)
        {
            if (status == "operational")
                return MapWarehouseSiteStatus.Operational;
            if (status == "out_of_service")
                return MapWarehouseSiteStatus.OutOfService;
            return MapWarehouseSiteStatus.Unknown;
        }
    }
}
