namespace LogistiCore.EditorTools
{
    using LogistiCore.Map;

    public static class MapWarehouseFixtures
    {
        public static MapWarehouseItem Site(
            string id,
            string catalog,
            string city,
            MapWarehouseSiteStatus status,
            int level = 1,
            string model = "synthetic_slots",
            string units = "10",
            string used = "0",
            string type = "depot",
            bool owned = true,
            bool gps = false,
            double lat = 0d,
            double lon = 0d)
        {
            return new MapWarehouseItem(
                id,
                catalog,
                type,
                city,
                status,
                level,
                model,
                units,
                used,
                owned,
                gps,
                lat,
                gps,
                lon);
        }

        public static MapWarehouseSnapshot Mixed()
        {
            return MapWarehouseSnapshot.Fixture(
                MapWarehouseStatus.Ready,
                new[]
                {
                    Site("wh_izmir", "Izmir Depot", "izmir", MapWarehouseSiteStatus.Operational, 1, "synthetic_slots", "10", "0"),
                    Site("wh_istanbul", "Istanbul Depot", "istanbul", MapWarehouseSiteStatus.OutOfService, 2, "synthetic_slots", "20", "4"),
                    Site(
                        "wh_long_de",
                        "Güterverkehrslagerhaus-Verwaltung",
                        "ankara",
                        MapWarehouseSiteStatus.Operational,
                        1,
                        "synthetic_slots",
                        "8",
                        "2"),
                    Site("wh_unknown_city", "Remote Depot", "synthetic_city", MapWarehouseSiteStatus.Operational, 1, "", "", ""),
                    Site("wh_mismatch", "Partial Depot", "bursa", MapWarehouseSiteStatus.Operational, 1, "", "", "3"),
                    Site("wh_unknown_own", "Not Owned", "izmir", MapWarehouseSiteStatus.Operational, 1, "synthetic_slots", "10", "0", "depot", false),
                    Site("wh_izmir", "Duplicate", "izmir", MapWarehouseSiteStatus.Operational),
                    Site("", "Empty Id", "izmir", MapWarehouseSiteStatus.Operational),
                    Site("wh_bad_status", "Odd Depot", "antalya", MapWarehouseSiteStatus.Unknown, 1, "synthetic_slots", "6", "1"),
                    Site(
                        "wh_bad_gps",
                        "Gps Depot",
                        "trabzon",
                        MapWarehouseSiteStatus.Operational,
                        1,
                        "synthetic_slots",
                        "5",
                        "1",
                        "depot",
                        true,
                        true,
                        200d,
                        10d)
                },
                "wh_izmir");
        }

        public static MapWarehouseSnapshot MissingData()
        {
            return Mixed().WithSelected("wh_unknown_city");
        }

        public static MapWarehouseSnapshot Empty()
        {
            return MapWarehouseSnapshot.Fixture(MapWarehouseStatus.Empty, new MapWarehouseItem[0]);
        }

        public static MapWarehouseSnapshot Loading()
        {
            return MapWarehouseSnapshot.Fixture(MapWarehouseStatus.Loading, new MapWarehouseItem[0]);
        }

        public static MapWarehouseSnapshot Error()
        {
            return MapWarehouseSnapshot.Fixture(MapWarehouseStatus.Error, new MapWarehouseItem[0]);
        }
    }
}
