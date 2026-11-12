using System;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Geographic registration for the Türkiye regional relief.
    /// Runtime texture: Copernicus DEM GLO-30 mosaic, 2048 x 725, linear lon/lat plate.
    /// Texture v = 0 is south. The global land mesh UV stays equirectangular.
    /// MapReliefRegion.png remains in Resources as an unused fallback.
    /// </summary>
    public static class MapRegionalRelief
    {
        public const int Width = 2048;
        public const int Height = 725;
        public const string TextureResource = "MapReliefCopernicus";
        public const string FallbackTextureResource = "MapReliefRegion";
        public const float FeatherDegrees = 0.22f;

        public const double MinLongitude = 25.269d;
        public const double MaxLongitude = 45.217d;
        public const double MinLatitude = 35.431d;
        public const double MaxLatitude = 42.493d;

        public const double HaloMinLongitude = 18.166666666666657d;
        public const double HaloMaxLongitude = 52.3d;
        public const double HaloMinLatitude = 30.43333333333333d;
        public const double HaloMaxLatitude = 47.5d;

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

        public static bool TryPixelCenter(double latitude, double longitude, out int x, out int yFromNorth)
        {
            x = 0;
            yFromNorth = 0;
            if (!TryUv(latitude, longitude, out var uv))
                return false;

            x = Mathf.Clamp(Mathf.RoundToInt(uv.x * (Width - 1)), 0, Width - 1);
            var yFromSouth = Mathf.Clamp(Mathf.RoundToInt(uv.y * (Height - 1)), 0, Height - 1);
            yFromNorth = Height - 1 - yFromSouth;
            return true;
        }

        public static Vector4 RegionMinVector()
        {
            return new Vector4((float)MinLongitude, (float)MinLatitude, 0f, 0f);
        }

        public static Vector4 RegionMaxVector()
        {
            return new Vector4((float)MaxLongitude, (float)MaxLatitude, 0f, 0f);
        }

        public static Vector4 HaloMinVector()
        {
            return new Vector4((float)HaloMinLongitude, (float)HaloMinLatitude, 0f, 0f);
        }

        public static Vector4 HaloMaxVector()
        {
            return new Vector4((float)HaloMaxLongitude, (float)HaloMaxLatitude, 0f, 0f);
        }

        public static bool HaloContains(double latitude, double longitude)
        {
            return longitude >= HaloMinLongitude && longitude <= HaloMaxLongitude &&
                   latitude >= HaloMinLatitude && latitude <= HaloMaxLatitude;
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
