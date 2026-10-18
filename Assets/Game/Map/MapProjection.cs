using System;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// WGS84 longitude/latitude to Unity world position.
    /// Web Mercator, x = longitude in radians, y = ln(tan(pi/4 + latitude/2)), then multiplied by Scale.
    /// Latitude is clamped to ±85.05112878 degrees. Longitude is wrapped to [-180, 180).
    /// The map lives on the XY plane. Screen position is a camera concern, not a projection concern.
    /// Equirectangular rasters must not be stretched across these bounds. Sample them with
    /// u = (longitude + 180) / 360 and v = (latitude + 90) / 180.
    /// </summary>
    public static class MapProjection
    {
        public const double MaxLatitude = 85.0511287798066;
        public const double Scale = 100.0;

        public static double ClampLatitude(double latitude)
        {
            if (!double.IsFinite(latitude))
                return 0d;

            if (latitude > MaxLatitude)
                return MaxLatitude;

            if (latitude < -MaxLatitude)
                return -MaxLatitude;

            return latitude;
        }

        public static double WrapLongitude(double longitude)
        {
            if (!double.IsFinite(longitude))
                return 0d;

            var wrapped = (longitude + 180d) % 360d;
            if (wrapped < 0d)
                wrapped += 360d;

            return wrapped - 180d;
        }

        public static bool TryProject(double latitude, double longitude, out Vector2 world)
        {
            world = default;
            if (!double.IsFinite(longitude))
                return false;

            return TryProjectUnwrapped(latitude, WrapLongitude(longitude), out world);
        }

        /// <summary>
        /// Projects a longitude already in [-180, 180]. Positive 180 stays on the eastern edge.
        /// Wrapped projection folds 180 onto -180, which is correct for a point but wrong for the
        /// eastern edge of a mesh.
        /// </summary>
        public static bool TryProjectUnwrapped(double latitude, double longitude, out Vector2 world)
        {
            world = default;
            if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
                return false;
            if (longitude < -180d || longitude > 180d)
                return false;

            var lat = ClampLatitude(latitude) * Math.PI / 180d;
            var lon = longitude * Math.PI / 180d;
            var x = lon * Scale;
            var y = Math.Log(Math.Tan(Math.PI / 4d + lat / 2d)) * Scale;
            if (!double.IsFinite(x) || !double.IsFinite(y))
                return false;

            world = new Vector2((float)x, (float)y);
            return true;
        }

        public static Vector2 Project(double latitude, double longitude)
        {
            if (!TryProject(latitude, longitude, out var world))
                return Vector2.zero;

            return world;
        }

        public static bool TryUnproject(Vector2 world, out double latitude, out double longitude)
        {
            latitude = 0d;
            longitude = 0d;
            if (!float.IsFinite(world.x) || !float.IsFinite(world.y))
                return false;

            longitude = WrapLongitude(world.x / Scale * 180d / Math.PI);
            latitude = (2d * Math.Atan(Math.Exp(world.y / Scale)) - Math.PI / 2d) * 180d / Math.PI;
            return double.IsFinite(latitude) && double.IsFinite(longitude);
        }
    }
}
