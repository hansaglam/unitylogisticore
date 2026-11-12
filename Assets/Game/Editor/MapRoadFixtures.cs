namespace LogistiCore.EditorTools
{
    using LogistiCore.Map;

    /// <summary>
    /// Editor-only synthetic active-truck deliveries. Not a player debug switch.
    /// </summary>
    public static class MapRoadFixtures
    {
        public static MapRoadDeliveryItem Delivery(
            string id,
            MapRoadLifecycle lifecycle,
            string origin,
            string destination,
            string truck = "starter_truck",
            string arrival = "2026-10-20T12:00:01.000Z",
            bool arrivalReady = false,
            string contractId = "contract-a")
        {
            return new MapRoadDeliveryItem(
                id,
                contractId,
                lifecycle,
                origin,
                destination,
                truck,
                arrival,
                arrivalReady);
        }

        public static MapRoadDeliverySnapshot One()
        {
            return MapRoadDeliverySnapshot.Fixture(
                MapRoadDeliveryStatus.Ready,
                new[]
                {
                    Delivery("d1", MapRoadLifecycle.Active, "istanbul", "ankara")
                },
                "d1");
        }

        public static MapRoadDeliverySnapshot Many()
        {
            return MapRoadDeliverySnapshot.Fixture(
                MapRoadDeliveryStatus.Ready,
                new[]
                {
                    Delivery("d1", MapRoadLifecycle.Active, "istanbul", "ankara"),
                    Delivery("d2", MapRoadLifecycle.Active, "izmir", "trabzon", "fleet_truck_2"),
                    Delivery("d3", MapRoadLifecycle.Completed, "adana", "bursa"),
                    Delivery("d4", MapRoadLifecycle.Active, "antalya", "missing_city", "", ""),
                    Delivery("d5", MapRoadLifecycle.Unknown, "istanbul", "izmir"),
                    Delivery("d1", MapRoadLifecycle.Active, "istanbul", "ankara")
                },
                "d1");
        }

        public static MapRoadDeliverySnapshot Empty()
        {
            return MapRoadDeliverySnapshot.Fixture(MapRoadDeliveryStatus.Empty, new MapRoadDeliveryItem[0]);
        }

        public static MapRoadDeliverySnapshot LongCopy()
        {
            return MapRoadDeliverySnapshot.Fixture(
                MapRoadDeliveryStatus.Ready,
                new[]
                {
                    Delivery(
                        "long-delivery-identifier",
                        MapRoadLifecycle.Active,
                        "diyarbakir",
                        "trabzon",
                        "starter_truck_with_a_very_long_identity")
                },
                "long-delivery-identifier");
        }
    }
}
