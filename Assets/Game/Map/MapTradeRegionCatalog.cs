using System;
using System.Collections.Generic;
using System.Text;

namespace LogistiCore.Map
{
    /// <summary>
    /// Initial commercial map: Turkey's 81 provincial centers and selected
    /// European cities. Country membership is a PRESENTATION/CONTENT filter,
    /// not a road, border permit or entitlement. Islands and disconnected
    /// countries can be explored but road dispatch requires separate routing.
    /// The full globe catalog remains untouched for future expansions.
    /// </summary>
    public static class MapTradeRegionCatalog
    {
        // European countries plus Turkey; western Russia is geography-clipped.
        static readonly HashSet<string> Countries = new HashSet<string>(StringComparer.Ordinal)
        {
            "TUR", "ALB", "AND", "AUT", "BEL", "BGR", "BIH", "BLR", "CHE",
            "CYP", "CZE", "DEU", "DNK", "ESP", "EST", "FIN", "FRA", "GBR",
            "GRC", "HRV", "HUN", "IRL", "ISL", "ITA", "LIE", "LTU", "LUX",
            "LVA", "MCO", "MDA", "MKD", "MLT", "MNE", "NLD", "NOR", "POL",
            "PRT", "ROU", "RUS", "SMR", "SRB", "SVK", "SVN", "SWE", "UKR",
            "VAT"
        };

        public static bool Includes(MapGlobalCity city)
        {
            if (!city.Location.IsValid || !Countries.Contains(city.Location.CountryCode))
                return false;

            // Russia is transcontinental: only its European western portion is
            // included. Any city outside the initial European view is deferred.
            if (city.Location.CountryCode == "RUS")
                return city.Longitude <= 60d && city.Longitude >= 19d &&
                    city.Latitude >= 41d && city.Latitude <= 72d;

            // Do not accidentally include overseas territories in a European
            // list merely because their country code is a European state.
            if (city.Location.CountryCode != "TUR")
                return city.Latitude >= 30d && city.Latitude <= 72d &&
                    city.Longitude >= -32d && city.Longitude <= 60d;

            return true; // All 81 Turkish provinces, including southeast.
        }

        public static bool Includes(MapBusinessLocation location)
        {
            return MapGlobalCityCatalog.TryGet(location, out var city) && Includes(city);
        }

        /// <summary>
        /// Optional common stylized city-art lookup. This display slug never
        /// serves as the persistent location identifier.
        /// </summary>
        public static string ArtworkKey(MapGlobalCity city)
        {
            var chars = city.DisplayName ?? string.Empty;
            var output = new StringBuilder(chars.Length);
            var inGap = false;
            for (var i = 0; i < chars.Length; i++)
            {
                var c = char.ToLowerInvariant(chars[i]);
                if (c == 'ı') c = 'i';
                if (c == 'ğ') c = 'g';
                if (c == 'ş') c = 's';
                if (c == 'ü') c = 'u';
                if (c == 'ö') c = 'o';
                if (c == 'ç') c = 'c';
                if (c >= 'a' && c <= 'z')
                {
                    output.Append(c);
                    inGap = false;
                }
                else if ((c == ' ' || c == '-') && output.Length > 0 && !inGap)
                {
                    output.Append('_');
                    inGap = true;
                }
            }

            return output.ToString().TrimEnd('_');
        }

        public static int Count
        {
            get
            {
                var count = 0;
                foreach (var city in MapGlobalCityCatalog.All)
                    if (Includes(city)) count++;
                return count;
            }
        }
    }
}
