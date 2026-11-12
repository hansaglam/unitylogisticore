namespace LogistiCore.Map
{
    /// <summary>
    /// Presentation row for one getMyFleetState item. Location and capacity are optional
    /// and stay empty unless the snapshot actually supplied them.
    /// </summary>
    public readonly struct MapFleetAsset
    {
        public MapFleetAsset(
            string assetId,
            MapFleetKind kind,
            string catalogId,
            MapFleetAssetStatus status,
            bool available,
            string assignmentId,
            string capacity,
            string locationCityId,
            bool hasLatitude,
            double latitude,
            bool hasLongitude,
            double longitude)
        {
            AssetId = assetId ?? "";
            Kind = kind;
            CatalogId = catalogId ?? "";
            Status = status;
            Available = available;
            AssignmentId = assignmentId ?? "";
            Capacity = capacity ?? "";
            LocationCityId = locationCityId ?? "";
            HasLatitude = hasLatitude;
            Latitude = latitude;
            HasLongitude = hasLongitude;
            Longitude = longitude;
        }

        public string AssetId { get; }
        public MapFleetKind Kind { get; }
        public string CatalogId { get; }
        public MapFleetAssetStatus Status { get; }
        public bool Available { get; }
        public string AssignmentId { get; }
        public string Capacity { get; }
        public string LocationCityId { get; }
        public bool HasLatitude { get; }
        public double Latitude { get; }
        public bool HasLongitude { get; }
        public double Longitude { get; }

        public string DisplayName => string.IsNullOrEmpty(CatalogId) ? AssetId : CatalogId;
        public bool IsVehicle => Kind == MapFleetKind.Truck;
        public bool HasAssignment => !string.IsNullOrEmpty(AssignmentId);
        public bool HasCapacity => !string.IsNullOrEmpty(Capacity);
        public bool HasKnownCity => MapCities.TryGet(LocationCityId, out _);
        public bool HasGps
        {
            get
            {
                if (!HasLatitude || !HasLongitude)
                    return false;
                return Latitude >= -90d && Latitude <= 90d && Longitude >= -180d && Longitude <= 180d;
            }
        }

        public static MapFleetKind ParseKind(string kind)
        {
            if (kind == "truck")
                return MapFleetKind.Truck;
            if (kind == "driver")
                return MapFleetKind.Driver;
            if (kind == "trailer")
                return MapFleetKind.Trailer;
            return MapFleetKind.Unknown;
        }

        public static MapFleetAssetStatus ParseStatus(string status)
        {
            if (status == "operational")
                return MapFleetAssetStatus.Operational;
            if (status == "out_of_service")
                return MapFleetAssetStatus.OutOfService;
            return MapFleetAssetStatus.Unknown;
        }
    }
}
