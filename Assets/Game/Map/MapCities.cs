namespace LogistiCore.Map
{
    public readonly struct MapCity
    {
        public readonly string Id;
        public readonly double Latitude;
        public readonly double Longitude;

        public MapCity(string id, double latitude, double longitude)
        {
            Id = id;
            Latitude = latitude;
            Longitude = longitude;
        }
    }

    /// <summary>
    /// Presentation markers only. Coordinates are Natural Earth 10m populated-places geometry
    /// (v5.1.2, adm0name Turkey). Istanbul uses the geometry point, not the differing property point.
    /// </summary>
    public static class MapCities
    {
        public static readonly MapCity[] All =
        {
            new MapCity("izmir", 38.438096, 27.149848),
            new MapCity("istanbul", 41.017602, 28.974277),
            new MapCity("ankara", 39.929184, 32.862446),
            new MapCity("bursa", 40.201933, 29.068052),
            new MapCity("antalya", 36.891928, 30.698030),
            new MapCity("adana", 36.996935, 35.318058),
            new MapCity("diyarbakir", 37.920436, 40.230040),
            new MapCity("trabzon", 40.979991, 39.719994)
        };

        public static bool TryGet(string id, out MapCity city)
        {
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    city = All[i];
                    return true;
                }
            }

            city = default;
            return false;
        }
    }
}
