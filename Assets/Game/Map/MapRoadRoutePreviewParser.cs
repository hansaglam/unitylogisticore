using System;
using System.Collections;
using System.Collections.Generic;
using LogistiCore.Backend;

namespace LogistiCore.Map
{
    /// <summary>
    /// Trust-boundary parser for getMyRoadRoutePreview. The caller must obtain
    /// the body from the authenticated, read-only ClientReadSession, never from
    /// user-generated route geometry. Road previews grant no dispatch rights.
    /// </summary>
    public static class MapRoadRoutePreviewParser
    {
        public const string PreviewPurpose = "map_preview_only";
        public const string ProviderName = "local_osrm_driving";

        public static Dictionary<string, object> Request(
            MapBusinessLocation origin, MapBusinessLocation destination)
        {
            if (!origin.IsValid || !destination.IsValid || origin.Equals(destination) ||
                !MapTradeRegionCatalog.Includes(origin) ||
                !MapTradeRegionCatalog.Includes(destination))
                return null;

            return new Dictionary<string, object>
            {
                { "protocolVersion", ClientProtocol.Version },
                { "origin", new Dictionary<string, object>
                    {
                        { "countryCode", origin.CountryCode },
                        { "cityKey", origin.CityKey }
                    } },
                { "destination", new Dictionary<string, object>
                    {
                        { "countryCode", destination.CountryCode },
                        { "cityKey", destination.CityKey }
                    } }
            };
        }

        static bool Number(object raw, out double value)
        {
            switch (raw)
            {
                case double d: value = d; break;
                case float f: value = f; break;
                case int i: value = i; break;
                case long l: value = l; break;
                case decimal m: value = (double)m; break;
                default: value = 0d; return false;
            }
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        static bool Location(object value, out MapBusinessLocation location)
        {
            location = default;
            if (!ClientValue.TryMap(value, out var map) || map.Count != 2 ||
                !ClientValue.TryString(ClientValue.Get(map, "countryCode"), out var country) ||
                !ClientValue.TryString(ClientValue.Get(map, "cityKey"), out var city))
                return false;

            location = new MapBusinessLocation(country, city);
            return location.IsValid && MapTradeRegionCatalog.Includes(location);
        }

        /// <summary>
        /// Returns false for untrusted/malformed server data. For a verified
        /// "no_road_route" response returns true but with route == null.
        /// Never falls back to a straight line between city centers.
        /// </summary>
        public static bool TryParse(object raw, out MapVerifiedRoute route, out bool noRoad)
        {
            route = null;
            noRoad = false;

            if (!ClientValue.TryMap(raw, out var map) ||
                !ClientValue.TryBool(ClientValue.Get(map, "available"), out var available) ||
                !ClientValue.TryBool(ClientValue.Get(map, "dispatchReady"), out var dispatch) ||
                dispatch ||
                !ClientValue.TryString(ClientValue.Get(map, "purpose"), out var purpose) ||
                purpose != PreviewPurpose)
                return false;

            if (!available)
            {
                if (map.Count != 4 ||
                    !ClientValue.TryString(ClientValue.Get(map, "reason"), out var reason) ||
                    reason != "no_road_route")
                    return false;
                noRoad = true;
                return true;
            }

            if (map.Count != 13 ||
                !ClientValue.TryString(ClientValue.Get(map, "verification"), out var verification) ||
                verification != "geometry_only" ||
                !ClientValue.TryString(ClientValue.Get(map, "attribution"), out var attribution) ||
                attribution != "© OpenStreetMap contributors" ||
                !ClientValue.TryString(ClientValue.Get(map, "provider"), out var provider) ||
                provider != ProviderName ||
                !ClientValue.TryString(ClientValue.Get(map, "mode"), out var mode) ||
                mode != "road" ||
                !ClientValue.TryString(ClientValue.Get(map, "routeId"), out var routeId) ||
                routeId.Length != 40 ||
                !Location(ClientValue.Get(map, "origin"), out var origin) ||
                !Location(ClientValue.Get(map, "destination"), out var destination) ||
                origin.Equals(destination) ||
                !Number(ClientValue.Get(map, "distanceMeters"), out var distance) ||
                !Number(ClientValue.Get(map, "durationSeconds"), out var duration) ||
                !ClientValue.TryList(ClientValue.Get(map, "geometry"), out var geometry) ||
                geometry.Count < 2 || geometry.Count > 4096)
                return false;

            for (var i = 0; i < routeId.Length; i++)
            {
                var c = routeId[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            }

            var points = new MapRouteGeoPoint[geometry.Count];
            for (var i = 0; i < geometry.Count; i++)
            {
                if (!ClientValue.TryMap(geometry[i], out var point) ||
                    point.Count != 2 ||
                    !Number(ClientValue.Get(point, "longitude"), out var longitude) ||
                    !Number(ClientValue.Get(point, "latitude"), out var latitude))
                    return false;
                points[i] = new MapRouteGeoPoint(latitude, longitude);
            }

            return MapVerifiedRoute.TryCreate(true, routeId, provider,
                MapTransportMode.Road, origin, destination,
                distance, duration, points, out route);
        }
    }
}
