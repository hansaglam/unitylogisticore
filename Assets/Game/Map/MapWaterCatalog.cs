namespace LogistiCore.Map
{
    /// <summary>
    /// Curated in-water label anchors. Coordinates are WGS84 points chosen inside
    /// open water, not polygon centroids. Sources: IHO Limits of Oceans and Seas
    /// (3rd ed., 1953) for names and basins; NOAA / GEBCO basin outlines for
    /// avoiding land. Türkiye-adjacent seas use the open basin visible at the
    /// Türkiye fit rather than a whole-sea centroid that would sit on land or
    /// outside the frame.
    /// </summary>
    public static class MapWaterCatalog
    {
        public const uint ScopeWorld = 1u << (int)MapScope.World;
        public const uint ScopeTurkey = 1u << (int)MapScope.Turkey;
        public const uint ScopeEurope = 1u << (int)MapScope.Europe;
        public const uint ScopeAsia = 1u << (int)MapScope.Asia;
        public const uint ScopeAfrica = 1u << (int)MapScope.Africa;
        public const uint ScopeNorthAmerica = 1u << (int)MapScope.NorthAmerica;
        public const uint ScopeSouthAmerica = 1u << (int)MapScope.SouthAmerica;
        public const uint ScopeOceania = 1u << (int)MapScope.Oceania;
        public const uint ScopeAll = 0xFFu;

        public readonly struct Entry
        {
            public readonly string Id;
            public readonly string Key;
            public readonly float Longitude;
            public readonly float Latitude;
            public readonly float MinZoom;
            public readonly float MaxZoom;
            public readonly int Rank;
            public readonly uint Scopes;

            public Entry(
                string id,
                string key,
                float longitude,
                float latitude,
                float minZoom,
                float maxZoom,
                int rank,
                uint scopes)
            {
                Id = id;
                Key = key;
                Longitude = longitude;
                Latitude = latitude;
                MinZoom = minZoom;
                MaxZoom = maxZoom;
                Rank = rank;
                Scopes = scopes;
            }
        }

        public static readonly Entry[] All =
        {
            Ocean("atlantic", MapContent.OceanAtlantic, -32f, 8f, 1.35f, 5.8f, 0),
            Ocean("pacific", MapContent.OceanPacific, -150f, 0f, 1.35f, 5.8f, 0),
            Ocean("indian", MapContent.OceanIndian, 80f, -20f, 1.35f, 5.8f, 0),
            Ocean("arctic", MapContent.OceanArctic, 0f, 80f, 1.35f, 4.6f, 0),
            Ocean("southern", MapContent.OceanSouthern, 20f, -55f, 1.35f, 4.8f, 0),
            Sea("mediterranean_sea", MapContent.WaterMediterranean, 32.1f, 36.08f, 2.45f, 11.2f, 1,
                ScopeTurkey | ScopeEurope | ScopeAsia | ScopeAfrica),
            Sea("caribbean_sea", MapContent.WaterCaribbean, -75f, 15f, 2.55f, 8.5f, 1,
                ScopeNorthAmerica | ScopeSouthAmerica),
            Sea("south_china_sea", MapContent.WaterSouthChina, 114f, 12f, 2.55f, 8.5f, 1,
                ScopeAsia | ScopeOceania),
            Sea("arabian_sea", MapContent.WaterArabian, 65f, 16f, 2.55f, 8.5f, 1,
                ScopeAsia | ScopeAfrica),
            Sea("black_sea", MapContent.WaterBlackSea, 33.8f, 41.95f, 3.6f, 11.2f, 2,
                ScopeTurkey | ScopeEurope | ScopeAsia),
            Sea("aegean_sea", MapContent.WaterAegean, 26.55f, 36.82f, 3.6f, 11.2f, 2,
                ScopeTurkey | ScopeEurope),
            Sea("north_sea", MapContent.WaterNorthSea, 2.9f, 56.5f, 2.55f, 8.8f, 2, ScopeEurope),
            Sea("baltic_sea", MapContent.WaterBaltic, 20f, 59f, 2.55f, 8.8f, 2, ScopeEurope),
            Sea("caspian_sea", MapContent.WaterCaspian, 50.5f, 41.8f, 3.2f, 9.5f, 2,
                ScopeTurkey | ScopeAsia | ScopeEurope),
            Sea("adriatic_sea", MapContent.WaterAdriatic, 17f, 42.7f, 2.55f, 8.8f, 2, ScopeEurope),
            Sea("red_sea", MapContent.WaterRedSea, 38f, 22f, 3.0f, 8.8f, 2,
                ScopeAfrica | ScopeAsia | ScopeTurkey),
            Sea("japan_sea", MapContent.WaterJapan, 135.5f, 40f, 2.9f, 8.5f, 2, ScopeAsia),
            Sea("bering_sea", MapContent.WaterBering, -178f, 57f, 2.9f, 8.4f, 2,
                ScopeNorthAmerica | ScopeAsia),
            Sea("coral_sea", MapContent.WaterCoral, 152f, -16f, 2.9f, 8.5f, 2, ScopeOceania),
            Sea("tasman_sea", MapContent.WaterTasman, 157f, -38f, 2.9f, 8.5f, 2, ScopeOceania),
            Sea("marmara_sea", MapContent.WaterMarmara, 27.65f, 40.70f, 6.72f, 12.5f, 3, ScopeTurkey)
        };

        public static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return -1;

            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                    return i;
            }

            return -1;
        }

        public static bool InScope(in Entry entry, MapScope scope)
        {
            var bit = 1u << (int)scope;
            return (entry.Scopes & bit) != 0;
        }

        static Entry Ocean(string id, string key, float longitude, float latitude, float minZoom, float maxZoom, int rank)
        {
            return new Entry(id, key, longitude, latitude, minZoom, maxZoom, rank, ScopeAll);
        }

        static Entry Sea(
            string id,
            string key,
            float longitude,
            float latitude,
            float minZoom,
            float maxZoom,
            int rank,
            uint scopes)
        {
            return new Entry(id, key, longitude, latitude, minZoom, maxZoom, rank, scopes);
        }
    }
}
