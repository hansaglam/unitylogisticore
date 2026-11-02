using System;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Geographic registration for the Türkiye regional relief.
    /// The window is an exact crop of Natural Earth SR_50M, 30 samples per degree.
    /// Texture v = 0 is south. The global land mesh UV stays equirectangular.
    /// </summary>
    public static class MapRegionalRelief
    {
        public const int Width = 1024;
        public const int Height = 512;
        public const int SourceColumn = 5945;
        public const int SourceRow = 1275;
        public const double DegreesPerPixel = 1d / 30d;
        public const float FeatherDegrees = 2.4f;

        public const double MinLongitude = -180d + SourceColumn * DegreesPerPixel;
        public const double MaxLongitude = -180d + (SourceColumn + Width) * DegreesPerPixel;
        public const double MaxLatitude = 90d - SourceRow * DegreesPerPixel;
        public const double MinLatitude = 90d - (SourceRow + Height) * DegreesPerPixel;

        public static bool Contains(double latitude, double longitude)
        {
            return longitude >= MinLongitude && longitude <= MaxLongitude &&
                   latitude >= MinLatitude && latitude <= MaxLatitude;
        }

        public static bool TryUv(double latitude, double longitude, out Vector2 uv)
        {
            uv = default;
            if (!Contains(latitude, longitude))
                return false;

            var u = (longitude - MinLongitude) / (MaxLongitude - MinLongitude);
            var v = (latitude - MinLatitude) / (MaxLatitude - MinLatitude);
            uv = new Vector2((float)u, (float)v);
            return true;
        }

        public static float EdgeFeather(double latitude, double longitude)
        {
            var edge = Math.Min(
                Math.Min(longitude - MinLongitude, MaxLongitude - longitude),
                Math.Min(latitude - MinLatitude, MaxLatitude - latitude));
            if (edge <= 0d)
                return 0f;
            var t = (float)(edge / FeatherDegrees);
            if (t >= 1f)
                return 1f;
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Smooth zoom blend. Full regional detail at the Türkiye frame, gone by 3.5 times that frame.
        /// </summary>
        public static float DetailWeight(float orthographicSize, float turkeyFrameSize)
        {
            if (turkeyFrameSize < 0.0001f)
                return 0f;

            var full = turkeyFrameSize * 1.15f;
            var none = turkeyFrameSize * 3.5f;
            if (orthographicSize <= full)
                return 1f;
            if (orthographicSize >= none)
                return 0f;

            var t = (orthographicSize - full) / (none - full);
            var smooth = t * t * (3f - 2f * t);
            return 1f - smooth;
        }

        /// <summary>
        /// Zoom weight, suppressed when the camera does not see the region or when comparison mode is global-only.
        /// The zoom term itself stays continuous, so leaving world scale does not pop.
        /// </summary>
        public static float BlendWeight(float orthographicSize, float turkeyFrameSize, bool viewIntersectsRegion, bool globalOnly)
        {
            if (globalOnly || !viewIntersectsRegion)
                return 0f;
            return DetailWeight(orthographicSize, turkeyFrameSize);
        }

        public static bool IntersectsView(Vector2 viewMin, Vector2 viewMax)
        {
            ProjectedBounds(out var min, out var max);
            return viewMax.x >= min.x && viewMin.x <= max.x && viewMax.y >= min.y && viewMin.y <= max.y;
        }

        public static void ProjectedBounds(out Vector2 min, out Vector2 max)
        {
            MapFrames.ProjectedBounds(MinLatitude, MaxLatitude, MinLongitude, MaxLongitude, out min, out max);
        }
    }
}
