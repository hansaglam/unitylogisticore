using System;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Position returned by a vetted route service, in WGS84 degrees. This is
    /// not a synthetic straight-line connection between catalog city points.
    /// </summary>
    public readonly struct MapRouteGeoPoint
    {
        public readonly double Latitude;
        public readonly double Longitude;

        public MapRouteGeoPoint(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public bool IsValid =>
            double.IsFinite(Latitude) && double.IsFinite(Longitude) &&
            Latitude >= -85.0511287798066d && Latitude <= 85.0511287798066d &&
            Longitude >= -180d && Longitude <= 180d;
    }

    public enum MapTransportMode : byte
    {
        Road = 1,
        Rail = 2,
        Sea = 3,
        Air = 4
    }

    /// <summary>
    /// Presentation-only verified route data contract. Clients MUST NOT use
    /// these fields to award money, authorize freight or calculate fuel. Backend
    /// must validate quote source, mode, company entitlements, vehicle, distance
    /// and duration before accepting a shipment.
    /// </summary>
    public sealed class MapVerifiedRoute
    {
        public readonly string RouteId;
        public readonly string Provider;
        public readonly MapTransportMode Mode;
        public readonly MapBusinessLocation Origin;
        public readonly MapBusinessLocation Destination;
        public readonly double DistanceMeters;
        public readonly double DurationSeconds;
        readonly MapRouteGeoPoint[] _path;

        public int PointCount => _path.Length;
        public MapRouteGeoPoint PointAt(int index) => _path[index];

        MapVerifiedRoute(string routeId, string provider, MapTransportMode mode,
            MapBusinessLocation origin, MapBusinessLocation destination,
            double distanceMeters, double durationSeconds, MapRouteGeoPoint[] points)
        {
            RouteId = routeId;
            Provider = provider;
            Mode = mode;
            Origin = origin;
            Destination = destination;
            DistanceMeters = distanceMeters;
            DurationSeconds = durationSeconds;
            _path = new MapRouteGeoPoint[points.Length];
            Array.Copy(points, _path, points.Length);
        }

        public static bool TryCreate(bool serverValidated, string routeId, string provider,
            MapTransportMode mode, MapBusinessLocation origin, MapBusinessLocation destination,
            double distanceMeters, double durationSeconds, MapRouteGeoPoint[] points,
            out MapVerifiedRoute route)
        {
            route = null;
            if (!serverValidated || string.IsNullOrWhiteSpace(routeId) ||
                string.IsNullOrWhiteSpace(provider) || !origin.IsValid || !destination.IsValid ||
                origin.Equals(destination) ||
                !MapGlobalCityCatalog.TryGet(origin, out _) ||
                !MapGlobalCityCatalog.TryGet(destination, out _) ||
                !Enum.IsDefined(typeof(MapTransportMode), mode) ||
                !double.IsFinite(distanceMeters) || distanceMeters <= 0d ||
                !double.IsFinite(durationSeconds) || durationSeconds <= 0d ||
                points == null || points.Length < 2 || points.Length > 4096)
                return false;

            for (var i = 0; i < points.Length; i++)
            {
                if (!points[i].IsValid)
                    return false;
            }

            route = new MapVerifiedRoute(routeId, provider, mode, origin, destination,
                distanceMeters, durationSeconds, points);
            return true;
        }

        /// <summary>
        /// Samples the supplied route coordinates in the same Web Mercator
        /// space as the existing game map. Does NOT attempt routing. Fails
        /// closed at the antimeridian rather than drawing an invalid line
        /// across the entire map; a later renderer will split such paths.
        /// </summary>
        public bool TryProjectForDisplay(out Vector3[] projected)
        {
            projected = null;
            var result = new Vector3[_path.Length];
            for (var i = 0; i < _path.Length; i++)
            {
                if (i > 0 &&
                    Math.Abs(_path[i].Longitude - _path[i - 1].Longitude) > 180d)
                    return false;

                if (!MapProjection.TryProject(_path[i].Latitude, _path[i].Longitude, out var p))
                    return false;
                result[i] = new Vector3(p.x, p.y, 0f);
            }

            projected = result;
            return true;
        }
    }
}
