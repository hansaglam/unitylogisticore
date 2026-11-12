namespace LogistiCore.EditorTools
{
    using LogistiCore.Map;

    /// <summary>
    /// Editor-only synthetic contract snapshots. Not a player debug switch.
    /// </summary>
    public static class MapJobsFixtures
    {
        public static MapJobsItem Contract(
            string id,
            MapJobContractStatus status,
            string origin,
            string destination,
            string cargo = "steel",
            bool expired = false,
            string quantity = "2",
            string unit = "t",
            string reward = "6",
            string currency = "TST",
            int scale = 2,
            string deliveryId = "",
            MapJobDeliveryStatus delivery = MapJobDeliveryStatus.None,
            string arrival = "",
            string vehicle = "",
            string expiresAt = "",
            string truck = "",
            string driver = "",
            string trailer = "",
            MapJobCandidate[] candidates = null,
            bool candidatesPresent = false)
        {
            return new MapJobsItem(
                id,
                status,
                expired,
                origin,
                destination,
                cargo,
                quantity,
                unit,
                reward,
                currency,
                scale,
                deliveryId,
                delivery,
                arrival,
                vehicle,
                expiresAt,
                truck,
                driver,
                trailer,
                candidates,
                candidatesPresent);
        }

        public static MapJobsItem AcceptedDriverTruck(
            string id = "accepted_izmir_istanbul",
            string truckId = "starter_truck",
            string driverId = "starter_driver")
        {
            return Contract(
                id,
                MapJobContractStatus.Accepted,
                "izmir",
                "istanbul",
                "steel",
                false,
                "2",
                "t",
                "6",
                "TST",
                2,
                "",
                MapJobDeliveryStatus.None,
                "",
                "",
                "",
                "",
                "",
                "",
                new[]
                {
                    new MapJobCandidate(driverId, MapFleetKind.Driver),
                    new MapJobCandidate(truckId, MapFleetKind.Truck)
                },
                true);
        }

        public static MapJobsSnapshot Mixed()
        {
            return MapJobsSnapshot.Fixture(
                MapJobsStatus.Ready,
                new[]
                {
                    Contract(
                        "offer_izmir_istanbul",
                        MapJobContractStatus.Offered,
                        "izmir",
                        "istanbul",
                        "steel",
                        false,
                        "2",
                        "t",
                        "6",
                        "TST",
                        2,
                        "",
                        MapJobDeliveryStatus.None,
                        "",
                        "",
                        "2026-11-05T18:00:00.000Z",
                        "starter_truck",
                        "starter_driver",
                        ""),
                    Contract(
                        "active_ankara_bursa",
                        MapJobContractStatus.InProgress,
                        "ankara",
                        "bursa",
                        "grain",
                        false,
                        "4",
                        "t",
                        "12",
                        "TST",
                        2,
                        "delivery_ankara_bursa",
                        MapJobDeliveryStatus.Active,
                        "2026-11-05T20:00:00.000Z",
                        "starter_truck"),
                    Contract(
                        "done_antalya_adana",
                        MapJobContractStatus.Completed,
                        "antalya",
                        "adana",
                        "timber",
                        false,
                        "1",
                        "t",
                        "8",
                        "TST",
                        2,
                        "delivery_antalya_adana",
                        MapJobDeliveryStatus.Completed),
                    Contract(
                        "long_de_cargo",
                        MapJobContractStatus.Offered,
                        "trabzon",
                        "diyarbakir",
                        "Güterverkehr-Stückgutsendung",
                        false,
                        "3",
                        "t",
                        "9",
                        "TST"),
                    Contract(
                        "missing_origin",
                        MapJobContractStatus.Offered,
                        "",
                        "izmir"),
                    Contract(
                        "missing_destination",
                        MapJobContractStatus.Accepted,
                        "istanbul",
                        ""),
                    Contract(
                        "unknown_vehicle",
                        MapJobContractStatus.Accepted,
                        "bursa",
                        "ankara",
                        "ore",
                        false,
                        "1",
                        "t",
                        "4",
                        "TST",
                        2,
                        "",
                        MapJobDeliveryStatus.None,
                        "",
                        "missing_truck"),
                    Contract(
                        "no_reward",
                        MapJobContractStatus.Offered,
                        "adana",
                        "antalya",
                        "parts",
                        false,
                        "2",
                        "t",
                        "",
                        ""),
                    Contract(
                        "bad_money",
                        MapJobContractStatus.Offered,
                        "bursa",
                        "adana",
                        "mail",
                        false,
                        "1",
                        "t",
                        "6.5",
                        "LGCX",
                        9),
                    Contract(
                        "invalid_city",
                        MapJobContractStatus.Offered,
                        "synthetic_city",
                        "synthetic_destination"),
                    Contract(
                        "offer_izmir_istanbul",
                        MapJobContractStatus.Completed,
                        "izmir",
                        "istanbul",
                        "duplicate"),
                    Contract(
                        "unexpected_status",
                        MapJobContractStatus.Unknown,
                        "ankara",
                        "izmir",
                        "mail"),
                    Contract(
                        "expired_offer",
                        MapJobContractStatus.Offered,
                        "trabzon",
                        "istanbul",
                        "coal",
                        true),
                    Contract(
                        "",
                        MapJobContractStatus.Offered,
                        "izmir",
                        "ankara")
                },
                "offer_izmir_istanbul",
                MapJobCategory.Available);
        }

        public static MapJobsSnapshot ActiveSelected()
        {
            return Mixed().WithCategory(MapJobCategory.Active).WithSelected("active_ankara_bursa");
        }

        public static MapJobsSnapshot HistorySelected()
        {
            return Mixed().WithCategory(MapJobCategory.History).WithSelected("done_antalya_adana");
        }

        public static MapJobsSnapshot Empty()
        {
            return MapJobsSnapshot.Fixture(MapJobsStatus.Empty, new MapJobsItem[0]);
        }

        public static MapJobsSnapshot Loading()
        {
            return MapJobsSnapshot.Fixture(MapJobsStatus.Loading, new MapJobsItem[0]);
        }

        public static MapJobsSnapshot Error()
        {
            return MapJobsSnapshot.Fixture(MapJobsStatus.Error, new MapJobsItem[0]);
        }
    }
}
