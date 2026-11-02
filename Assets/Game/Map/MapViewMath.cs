using UnityEngine;

namespace LogistiCore.Map
{
    public static class MapViewMath
    {
        public static void Fit(Vector2 min, Vector2 max, float aspect, float padding, out Vector2 center, out float orthographicSize)
        {
            if (aspect < 0.05f)
                aspect = 1f;
            if (padding < 1f)
                padding = 1f;

            center = (min + max) * 0.5f;
            var halfHeight = (max.y - min.y) * 0.5f * padding;
            var halfWidth = (max.x - min.x) * 0.5f * padding;
            var sizeForWidth = halfWidth / aspect;
            orthographicSize = halfHeight > sizeForWidth ? halfHeight : sizeForWidth;
            if (orthographicSize < 0.0001f)
                orthographicSize = 0.0001f;
        }

        public static float ClampSize(float size, float min, float max)
        {
            if (min > max)
            {
                var swap = min;
                min = max;
                max = swap;
            }

            if (size < min)
                return min;
            if (size > max)
                return max;
            return size;
        }

        public static Vector2 ClampCenter(Vector2 center, float orthographicSize, float aspect, Vector2 min, Vector2 max)
        {
            if (aspect < 0.05f)
                aspect = 1f;

            var halfHeight = orthographicSize;
            var halfWidth = orthographicSize * aspect;
            center.x = ClampAxis(center.x, min.x, max.x, halfWidth);
            center.y = ClampAxis(center.y, min.y, max.y, halfHeight);
            return center;
        }

        public static Vector2 ScreenToWorld(
            Vector2 screen,
            Vector2 center,
            float orthographicSize,
            float aspect,
            float screenWidth,
            float screenHeight)
        {
            if (aspect < 0.05f)
                aspect = 1f;

            var nx = screenWidth <= 1f ? 0.5f : screen.x / screenWidth;
            var ny = screenHeight <= 1f ? 0.5f : screen.y / screenHeight;
            var halfHeight = orthographicSize;
            var halfWidth = orthographicSize * aspect;
            return new Vector2(
                center.x + (nx - 0.5f) * 2f * halfWidth,
                center.y + (ny - 0.5f) * 2f * halfHeight);
        }

        public static void ZoomAbout(
            Vector2 center,
            float size,
            Vector2 screen,
            float zoomInFactor,
            float aspect,
            float screenWidth,
            float screenHeight,
            float minSize,
            float maxSize,
            Vector2 boundsMin,
            Vector2 boundsMax,
            out Vector2 newCenter,
            out float newSize)
        {
            if (zoomInFactor < 0.01f)
                zoomInFactor = 0.01f;

            var before = ScreenToWorld(screen, center, size, aspect, screenWidth, screenHeight);
            newSize = ClampSize(size / zoomInFactor, minSize, maxSize);
            var after = ScreenToWorld(screen, center, newSize, aspect, screenWidth, screenHeight);
            newCenter = ClampCenter(center + (before - after), newSize, aspect, boundsMin, boundsMax);
        }

        static float ClampAxis(float value, float min, float max, float halfExtent)
        {
            var limitMin = min + halfExtent;
            var limitMax = max - halfExtent;
            if (limitMin > limitMax)
                return (min + max) * 0.5f;
            if (value < limitMin)
                return limitMin;
            if (value > limitMax)
                return limitMax;
            return value;
        }
    }
}
