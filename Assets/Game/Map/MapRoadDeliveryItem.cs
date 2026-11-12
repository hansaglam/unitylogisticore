namespace LogistiCore.Map
{
    /// <summary>
    /// One getMyDeliveries row. Transport mode is not a documented field.
    /// </summary>
    public readonly struct MapRoadDeliveryItem
    {
        public MapRoadDeliveryItem(
            string deliveryId,
            string contractId,
            MapRoadLifecycle lifecycle,
            string originCityId,
            string destinationCityId,
            string truckId,
            string expectedArrivalAt,
            bool arrivalReady)
        {
            DeliveryId = deliveryId ?? "";
            ContractId = contractId ?? "";
            Lifecycle = lifecycle;
            OriginCityId = originCityId ?? "";
            DestinationCityId = destinationCityId ?? "";
            TruckId = truckId ?? "";
            ExpectedArrivalAt = expectedArrivalAt ?? "";
            ArrivalReady = arrivalReady;
        }

        public string DeliveryId { get; }
        public string ContractId { get; }
        public MapRoadLifecycle Lifecycle { get; }
        public string OriginCityId { get; }
        public string DestinationCityId { get; }
        public string TruckId { get; }
        public string ExpectedArrivalAt { get; }
        public bool ArrivalReady { get; }

        public bool HasTruck => !string.IsNullOrEmpty(TruckId);
        public bool HasArrival => !string.IsNullOrEmpty(ExpectedArrivalAt);
        public bool HasOriginCity => MapCities.TryGet(OriginCityId, out _);
        public bool HasDestinationCity => MapCities.TryGet(DestinationCityId, out _);

        public static MapRoadLifecycle Parse(string status)
        {
            if (status == "active")
                return MapRoadLifecycle.Active;
            if (status == "completed")
                return MapRoadLifecycle.Completed;
            return MapRoadLifecycle.Unknown;
        }

        public static string TruckFromAssets(string[] kinds, string[] ids)
        {
            if (kinds == null || ids == null)
                return "";
            var n = kinds.Length < ids.Length ? kinds.Length : ids.Length;
            for (var i = 0; i < n; i++)
            {
                if (kinds[i] == "truck")
                    return ids[i] ?? "";
            }

            return "";
        }
    }
}
