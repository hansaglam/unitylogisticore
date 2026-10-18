using UnityEngine;

namespace LogistiCore.Map
{
    public enum MapScope
    {
        World = 0,
        Turkey = 1
    }

    /// <summary>
    /// Geographic frames. Türkiye limits are the Natural Earth 50m ADM0_A3=TUR polygon extent.
    /// </summary>
    public static class MapFrames
    {
        public const double TurkeyMinLongitude = 25.668945;
        public const double TurkeyMaxLongitude = 44.817188;
        public const double TurkeyMinLatitude = 35.831445;
        public const double TurkeyMaxLatitude = 42.093262;

        public const float TurkeyFocusPadding = 1.28f;
        public const float WorldFocusPadding = 1.02f;

        /// <summary>
        /// Closest orthographic size, as a fraction of the Türkiye frame.
        /// 0.46 is a regional city cluster. It stops zoom short of street scale,
        /// where the 50m relief would only blur.
        /// </summary>
        public const float ClosestRelativeToTurkeyFrame = 0.46f;

        public static void World(out Vector2 min, out Vector2 max)
        {
            ProjectedBounds(-MapProjection.MaxLatitude, MapProjection.MaxLatitude, -180d, 180d, out min, out max);
        }

        public static void Turkey(out Vector2 min, out Vector2 max)
        {
            ProjectedBounds(TurkeyMinLatitude, TurkeyMaxLatitude, TurkeyMinLongitude, TurkeyMaxLongitude, out min, out max);
        }

        public static float MinimumOrthographicSize(float aspect)
        {
            Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, TurkeyFocusPadding, out _, out var turkeyFrame);
            var closest = turkeyFrame * ClosestRelativeToTurkeyFrame;
            if (closest < 0.0001f)
                closest = 0.0001f;
            return closest;
        }

        public static float MaximumOrthographicSize(float aspect)
        {
            World(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, WorldFocusPadding, out _, out var size);
            return size;
        }

        public static void ProjectedBounds(
            double minLatitude,
            double maxLatitude,
            double minLongitude,
            double maxLongitude,
            out Vector2 min,
            out Vector2 max)
        {
            if (!MapProjection.TryProjectUnwrapped(minLatitude, minLongitude, out var southwest) ||
                !MapProjection.TryProjectUnwrapped(maxLatitude, maxLongitude, out var northeast))
            {
                southwest = Vector2.zero;
                northeast = Vector2.zero;
            }

            min = southwest;
            max = northeast;
        }
    }
}
