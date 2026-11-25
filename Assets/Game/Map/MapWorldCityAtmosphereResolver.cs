using System;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Presentation-only fallback for cities outside the initial Turkey marker set.
    /// Supports any stable city id and optional ISO-3166 alpha-3 country code.
    /// Never invents geographical coordinates, ownership, routes or landmarks:
    /// only explicitly curated cities receive a landmark illustration.
    /// </summary>
    public static class MapWorldCityAtmosphereResolver
    {
        public static bool TryResolve(string cityId, string countryCode, out MapCityAtmosphere atmosphere)
        {
            atmosphere = default;
            if (string.IsNullOrWhiteSpace(cityId))
                return false;

            var id = cityId.Trim().ToLowerInvariant();
            var country = string.IsNullOrWhiteSpace(countryCode)
                ? string.Empty
                : countryCode.Trim().ToUpperInvariant();

            // Preserve existing eight Turkey scenes, including old callers that
            // do not yet pass a country code.
            if ((country.Length == 0 || country == "TUR") &&
                MapCityAtmosphereCatalog.TryGet(id, out atmosphere))
                return true;

            var continent = MapCountryCatalog.MapContinent.None;
            var countryIndex = MapCountryCatalog.IndexOf(country);
            if (countryIndex >= 0)
                continent = MapCountryCatalog.All[countryIndex].Continent;

            GetRegionalPalette(continent, out var low, out var high, out var silhouette, out var accent);

            // A small authored set of skyline cues; other places get a neutral
            // architectural silhouette rather than an inaccurate monument.
            var landmark = CuratedLandmark(country, id);
            var horizon = CuratedHorizon(country, id);

            // FNV-1a gives the same silhouette on all platforms and app launches.
            // Unlike string.GetHashCode(), its output is not runtime-dependent.
            var seed = StableSeed(country + "|" + id);
            atmosphere = new MapCityAtmosphere(id, landmark, horizon,
                low, high, silhouette, accent, seed);
            return true;
        }

        public static int StableSeed(string key)
        {
            unchecked
            {
                uint value = 2166136261;
                if (!string.IsNullOrEmpty(key))
                {
                    for (var i = 0; i < key.Length; i++)
                    {
                        value ^= key[i];
                        value *= 16777619;
                    }
                }

                return 1 + (int)(value % 997u);
            }
        }

        static MapCityLandmark CuratedLandmark(string country, string id)
        {
            if (country == "DEU" && id == "berlin")
                return MapCityLandmark.Tower;
            if (country == "GBR" && id == "london")
                return MapCityLandmark.ClockTower;
            if (country == "FRA" && id == "paris")
                return MapCityLandmark.LatticeTower;
            if (country == "JPN" && id == "tokyo")
                return MapCityLandmark.NeedleTower;
            if (country == "AUS" && id == "sydney")
                return MapCityLandmark.SailRoof;
            if (country == "USA" && id == "new_york")
                return MapCityLandmark.Skyscrapers;
            if (country == "ARE" && id == "dubai")
                return MapCityLandmark.NeedleTower;
            if (country == "EGY" && id == "cairo")
                return MapCityLandmark.Pyramids;
            if (country == "SGP" && id == "singapore")
                return MapCityLandmark.Skyscrapers;
            return MapCityLandmark.None;
        }

        static MapCityHorizon CuratedHorizon(string country, string id)
        {
            if ((country == "AUS" && id == "sydney") ||
                (country == "SGP" && id == "singapore") ||
                (country == "USA" && id == "new_york"))
                return MapCityHorizon.Coast;

            // No geography guesses for an uncurated city.
            return MapCityHorizon.Urban;
        }

        static void GetRegionalPalette(MapCountryCatalog.MapContinent continent,
            out Color32 low, out Color32 high, out Color32 silhouette, out Color32 accent)
        {
            switch (continent)
            {
                case MapCountryCatalog.MapContinent.Europe:
                    low = new Color32(24, 83, 122, 255);
                    high = new Color32(9, 33, 67, 255);
                    silhouette = new Color32(13, 39, 66, 255);
                    accent = new Color32(111, 203, 216, 255);
                    break;
                case MapCountryCatalog.MapContinent.Asia:
                    low = new Color32(32, 93, 117, 255);
                    high = new Color32(13, 38, 73, 255);
                    silhouette = new Color32(18, 45, 68, 255);
                    accent = new Color32(232, 167, 112, 255);
                    break;
                case MapCountryCatalog.MapContinent.Africa:
                    low = new Color32(82, 99, 119, 255);
                    high = new Color32(29, 38, 69, 255);
                    silhouette = new Color32(45, 48, 66, 255);
                    accent = new Color32(238, 170, 108, 255);
                    break;
                case MapCountryCatalog.MapContinent.NorthAmerica:
                    low = new Color32(28, 95, 133, 255);
                    high = new Color32(11, 38, 77, 255);
                    silhouette = new Color32(17, 42, 69, 255);
                    accent = new Color32(101, 205, 219, 255);
                    break;
                case MapCountryCatalog.MapContinent.SouthAmerica:
                    low = new Color32(39, 109, 118, 255);
                    high = new Color32(13, 51, 79, 255);
                    silhouette = new Color32(15, 57, 62, 255);
                    accent = new Color32(139, 213, 185, 255);
                    break;
                case MapCountryCatalog.MapContinent.Oceania:
                    low = new Color32(30, 110, 139, 255);
                    high = new Color32(11, 45, 79, 255);
                    silhouette = new Color32(14, 49, 71, 255);
                    accent = new Color32(107, 206, 216, 255);
                    break;
                default:
                    low = new Color32(23, 83, 117, 255);
                    high = new Color32(11, 40, 77, 255);
                    silhouette = new Color32(14, 45, 70, 255);
                    accent = new Color32(114, 196, 210, 255);
                    break;
            }
        }
    }
}
