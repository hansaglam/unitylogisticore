namespace LogistiCore.EditorTools
{
    using LogistiCore.Map;

    public static class MapMarketFixtures
    {
        public static MapMarketItem Condition(
            string city,
            string cargo,
            int demand,
            int supply,
            int adjustment,
            bool hasDemand = true,
            bool hasSupply = true,
            bool hasAdj = true,
            int trend = 0,
            bool gps = false,
            double lat = 0d,
            double lon = 0d)
        {
            return new MapMarketItem(
                city,
                cargo,
                hasDemand,
                demand,
                hasSupply,
                supply,
                hasAdj,
                adjustment,
                trend,
                gps,
                lat,
                gps,
                lon);
        }

        public static MapMarketSnapshot Mixed()
        {
            return MapMarketSnapshot.Fixture(
                MapMarketStatus.Ready,
                new[]
                {
                    Condition("izmir", "steel", 80, 40, 125),
                    Condition("istanbul", "grain", 20, 70, -80),
                    Condition("ankara", "Güterverkehr-Stückgutsendung", 55, 55, 0),
                    Condition("synthetic_city", "ore", 10, 10, 50),
                    Condition("bursa", "parts", 0, 0, 0, false, false, false),
                    Condition("izmir", "steel", 99, 1, 1500),
                    Condition("", "", 1, 1, 1),
                    Condition("antalya", "mail", 30, 25, 12, true, true, true, 40),
                    Condition("trabzon", "coal", 15, 90, -400, true, true, true, 0, true, 200d, 10d)
                },
                "izmir/steel",
                "",
                "synthetic_fixture",
                "2026-11-05T18:00:00.000Z");
        }

        public static MapMarketSnapshot MissingData()
        {
            return Mixed().WithSelected("synthetic_city/ore");
        }

        public static MapMarketSnapshot Empty()
        {
            return MapMarketSnapshot.Fixture(MapMarketStatus.Empty, new MapMarketItem[0]);
        }

        public static MapMarketSnapshot Loading()
        {
            return MapMarketSnapshot.Fixture(MapMarketStatus.Loading, new MapMarketItem[0]);
        }

        public static MapMarketSnapshot Error()
        {
            return MapMarketSnapshot.Fixture(MapMarketStatus.Error, new MapMarketItem[0]);
        }
    }
}
