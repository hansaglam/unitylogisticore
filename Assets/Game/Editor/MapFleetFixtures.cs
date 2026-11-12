namespace LogistiCore.EditorTools
{
    using LogistiCore.Map;

    /// <summary>
    /// Editor-only synthetic fleet snapshots for captures. Not a player debug switch.
    /// </summary>
    public static class MapFleetFixtures
    {
        public static MapFleetAsset Driver(
            string id,
            string catalog,
            bool available,
            bool assigned,
            MapFleetAssetStatus status = MapFleetAssetStatus.Operational)
        {
            return new MapFleetAsset(
                id,
                MapFleetKind.Driver,
                catalog,
                status,
                available,
                assigned ? "synthetic_assignment" : "",
                "",
                "",
                false,
                0d,
                false,
                0d);
        }

        public static MapFleetAsset Trailer(
            string id,
            string catalog,
            bool available,
            bool assigned,
            MapFleetAssetStatus status = MapFleetAssetStatus.Operational)
        {
            return new MapFleetAsset(
                id,
                MapFleetKind.Trailer,
                catalog,
                status,
                available,
                assigned ? "synthetic_assignment" : "",
                "",
                "",
                false,
                0d,
                false,
                0d);
        }

        public static MapFleetAsset Truck(
            string id,
            string catalog,
            bool available,
            bool assigned,
            MapFleetAssetStatus status,
            string city = "",
            bool gps = false,
            double lat = 0d,
            double lon = 0d,
            string capacity = "")
        {
            return new MapFleetAsset(
                id,
                MapFleetKind.Truck,
                catalog,
                status,
                available,
                assigned ? "synthetic_assignment" : "",
                capacity,
                city,
                gps,
                lat,
                gps,
                lon);
        }

        public static MapFleetSnapshot ReadyThree()
        {
            return MapFleetSnapshot.Fixture(
                MapFleetStatus.Ready,
                new[]
                {
                    Truck("starter_truck", "Starter Truck", true, false, MapFleetAssetStatus.Operational, "izmir"),
                    Truck("busy_truck", "Line Haul", false, true, MapFleetAssetStatus.Operational),
                    Truck(
                        "long_truck",
                        "Güterverkehr-Sattelzugmaschine",
                        false,
                        false,
                        MapFleetAssetStatus.OutOfService)
                },
                "starter_truck");
        }

        public static MapFleetSnapshot ReadySelected()
        {
            return ReadyThree().WithSelected("busy_truck");
        }

        public static MapFleetSnapshot ReadyGps()
        {
            return MapFleetSnapshot.Fixture(
                MapFleetStatus.Ready,
                new[]
                {
                    Truck(
                        "gps_truck",
                        "Istanbul Truck",
                        true,
                        false,
                        MapFleetAssetStatus.Operational,
                        "",
                        true,
                        41.017602d,
                        28.974277d)
                },
                "gps_truck");
        }

        public static MapFleetSnapshot Empty()
        {
            return MapFleetSnapshot.Fixture(MapFleetStatus.Empty, new MapFleetAsset[0]);
        }

        public static MapFleetSnapshot Loading()
        {
            return MapFleetSnapshot.Fixture(MapFleetStatus.Loading, new MapFleetAsset[0]);
        }

        public static MapFleetSnapshot Error()
        {
            return MapFleetSnapshot.Fixture(MapFleetStatus.Error, new MapFleetAsset[0]);
        }
    }
}
