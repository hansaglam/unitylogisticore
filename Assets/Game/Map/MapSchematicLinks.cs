using System;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// VISUAL-01: illustrative city-to-city connectivity for the map background.
    /// THESE ARE NOT actual roads, delivery routes, player-owned lanes, traffic,
    /// active contracts or server state. Never use these links for game economics.
    /// </summary>
    public readonly struct MapSchematicLink
    {
        public readonly string From;
        public readonly string To;
        public readonly bool BendLeft;

        public MapSchematicLink(string from, string to, bool bendLeft)
        {
            From = from;
            To = to;
            BendLeft = bendLeft;
        }

        public bool Touches(string cityId)
        {
            return !string.IsNullOrEmpty(cityId) &&
                   (string.Equals(From, cityId, StringComparison.Ordinal) ||
                    string.Equals(To, cityId, StringComparison.Ordinal));
        }
    }

    public static class MapSchematicLinks
    {
        public const bool IsRealRoadGeometry = false;

        // A sparse, authored VISUAL graph connecting the eight existing map cities.
        // This is not a routing catalogue; road paths must come from a separate source.
        static readonly MapSchematicLink[] Graph =
        {
            new MapSchematicLink("istanbul", "bursa", true),
            new MapSchematicLink("istanbul", "ankara", false),
            new MapSchematicLink("bursa", "izmir", false),
            new MapSchematicLink("bursa", "ankara", true),
            new MapSchematicLink("izmir", "antalya", true),
            new MapSchematicLink("antalya", "ankara", false),
            new MapSchematicLink("antalya", "adana", true),
            new MapSchematicLink("ankara", "adana", true),
            new MapSchematicLink("ankara", "trabzon", false),
            new MapSchematicLink("adana", "diyarbakir", false),
            new MapSchematicLink("trabzon", "diyarbakir", true)
        };

        public static int Count => Graph.Length;

        /// <summary>
        /// Number of schematic display links touching one existing city.
        /// Never treat this count as available roads or active contracts.
        /// </summary>
        public static int NeighborCount(string cityId)
        {
            if (!MapCities.TryGet(cityId, out _))
                return 0;
            int count = 0;
            for (int i = 0; i < Graph.Length; i++)
            {
                if (Graph[i].Touches(cityId))
                    count++;
            }
            return count;
        }

        public static MapSchematicLink At(int index)
        {
            return Graph[index];
        }

        public static bool TryEndpoints(MapSchematicLink link, out Vector2 from, out Vector2 to)
        {
            from = default;
            to = default;
            if (!MapCities.TryGet(link.From, out var cityA) ||
                !MapCities.TryGet(link.To, out var cityB))
                return false;
            return MapProjection.TryProject(cityA.Latitude, cityA.Longitude, out from) &&
                   MapProjection.TryProject(cityB.Latitude, cityB.Longitude, out to);
        }

        /// <summary>
        /// Samples a gentle schematic Bezier-like bow. Fills a caller-owned array;
        /// returns false for invalid endpoints or insufficient storage.
        /// Each curve is a visual link only, never exact real-world road geometry.
        /// </summary>
        public static bool Sample(MapSchematicLink link, Vector3[] output)
        {
            if (output == null || output.Length < 2 || !TryEndpoints(link, out var a, out var b))
                return false;

            var segment = b - a;
            var bend = new Vector2(-segment.y, segment.x) * (link.BendLeft ? 0.055f : -0.055f);
            int last = output.Length - 1;
            for (var i = 0; i < output.Length; i++)
            {
                var t = i / (float)last;
                var p = Vector2.Lerp(a, b, t) + bend * Mathf.Sin(Mathf.PI * t);
                output[i] = new Vector3(p.x, p.y, 0f);
            }
            output[0] = new Vector3(a.x, a.y, 0f);
            output[last] = new Vector3(b.x, b.y, 0f);
            return true;
        }
    }
}
