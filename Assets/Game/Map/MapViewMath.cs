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

        /// <summary>
        /// Fits a projected region into the open map rectangle. The camera still
        /// covers the whole screen; the center shifts so the region lands in
        /// <paramref name="viewport"/> rather than under the header, rail, or panel.
        /// </summary>
        public static void FitInViewport(
            Vector2 boundsMin,
            Vector2 boundsMax,
            float aspect,
            float padding,
            Rect viewport,
            float screenWidth,
            float screenHeight,
            out Vector2 center,
            out float orthographicSize)
        {
            Fit(boundsMin, boundsMax, aspect, padding, out center, out orthographicSize);
            if (screenWidth < 1f || screenHeight < 1f || viewport.width < 24f || viewport.height < 24f)
                return;

            if (padding < 1f)
                padding = 1f;
            if (aspect < 0.05f)
                aspect = 1f;

            var spanX = Mathf.Max(0.0001f, boundsMax.x - boundsMin.x) * padding;
            var spanY = Mathf.Max(0.0001f, boundsMax.y - boundsMin.y) * padding;
            var widthFraction = Mathf.Clamp(viewport.width / screenWidth, 0.05f, 1f);
            var heightFraction = Mathf.Clamp(viewport.height / screenHeight, 0.05f, 1f);
            var sizeForHeight = spanY * 0.5f / heightFraction;
            var sizeForWidth = spanX * 0.5f / (widthFraction * aspect);
            orthographicSize = sizeForHeight > sizeForWidth ? sizeForHeight : sizeForWidth;
            if (orthographicSize < 0.0001f)
                orthographicSize = 0.0001f;

            var nx = (viewport.xMin + viewport.xMax) * 0.5f / screenWidth;
            var ny = (viewport.yMin + viewport.yMax) * 0.5f / screenHeight;
            var focus = (boundsMin + boundsMax) * 0.5f;
            center = new Vector2(
                focus.x - (nx - 0.5f) * 2f * orthographicSize * aspect,
                focus.y - (ny - 0.5f) * 2f * orthographicSize);
        }

        /// <summary>
        /// Contains a presentation window, then, when a wide viewport would
        /// otherwise zoom out to the window's height, stops at
        /// <paramref name="widthBias"/> times the width fit.
        /// </summary>
        public static void FitFocus(
            Vector2 boundsMin,
            Vector2 boundsMax,
            float aspect,
            float padding,
            Rect viewport,
            float screenWidth,
            float screenHeight,
            float widthBias,
            out Vector2 center,
            out float orthographicSize)
        {
            FitInViewport(
                boundsMin,
                boundsMax,
                aspect,
                padding,
                viewport,
                screenWidth,
                screenHeight,
                out center,
                out orthographicSize);
            if (widthBias <= 1f || screenWidth < 1f || screenHeight < 1f || viewport.width < 24f || viewport.height < 24f)
                return;

            if (aspect < 0.05f)
                aspect = 1f;
            if (padding < 1f)
                padding = 1f;
            var spanX = Mathf.Max(0.0001f, boundsMax.x - boundsMin.x) * padding;
            var widthFraction = Mathf.Clamp(viewport.width / screenWidth, 0.05f, 1f);
            var capped = spanX * 0.5f / (widthFraction * aspect) * widthBias;
            if (orthographicSize <= capped)
                return;

            orthographicSize = capped;
            var nx = (viewport.xMin + viewport.xMax) * 0.5f / screenWidth;
            var ny = (viewport.yMin + viewport.yMax) * 0.5f / screenHeight;
            var focus = (boundsMin + boundsMax) * 0.5f;
            center = new Vector2(
                focus.x - (nx - 0.5f) * 2f * orthographicSize * aspect,
                focus.y - (ny - 0.5f) * 2f * orthographicSize);
        }

        /// <summary>
        /// Fits a region, then, when the world-edge clamp crops it, tries smaller
        /// orthographic sizes down to the other axis. It never zooms out past the
        /// single-axis fit: a near-world zoom would raise a coverage percentage
        /// while turning a continent into a thumbnail. A candidate is kept only
        /// when it does not give back coverage already achieved on either axis.
        /// </summary>
        public static void FitClamped(
            Vector2 boundsMin,
            Vector2 boundsMax,
            float aspect,
            float padding,
            Rect viewport,
            float screenWidth,
            float screenHeight,
            float minSize,
            float maxSize,
            Vector2 worldMin,
            Vector2 worldMax,
            out Vector2 center,
            out float orthographicSize)
        {
            FitInViewport(
                boundsMin,
                boundsMax,
                aspect,
                padding,
                viewport,
                screenWidth,
                screenHeight,
                out center,
                out var fitted);
            fitted = ClampSize(fitted, minSize, maxSize);
            center = ClampCenter(center, fitted, aspect, worldMin, worldMax);
            orthographicSize = fitted;
            if (screenWidth < 1f || screenHeight < 1f || viewport.width < 24f || viewport.height < 24f)
                return;

            var fittedCover = ViewportSpan(boundsMin, boundsMax, center, fitted, aspect, viewport, screenWidth, screenHeight);
            if (fittedCover.x >= 0.995f && fittedCover.y >= 0.995f)
                return;

            if (aspect < 0.05f)
                aspect = 1f;
            if (padding < 1f)
                padding = 1f;
            var spanX = Mathf.Max(0.0001f, boundsMax.x - boundsMin.x) * padding;
            var spanY = Mathf.Max(0.0001f, boundsMax.y - boundsMin.y) * padding;
            var widthFraction = Mathf.Clamp(viewport.width / screenWidth, 0.05f, 1f);
            var heightFraction = Mathf.Clamp(viewport.height / screenHeight, 0.05f, 1f);
            var sizeForWidth = ClampSize(spanX * 0.5f / (widthFraction * aspect), minSize, maxSize);
            var sizeForHeight = ClampSize(spanY * 0.5f / heightFraction, minSize, maxSize);
            var lo = sizeForWidth < sizeForHeight ? sizeForWidth : sizeForHeight;
            var hi = fitted;
            if (lo > hi)
            {
                var swap = lo;
                lo = hi;
                hi = swap;
            }

            var bestScore = Mathf.Min(fittedCover.x, fittedCover.y);
            var bestSum = fittedCover.x + fittedCover.y;
            var focus = (boundsMin + boundsMax) * 0.5f;
            var nx = (viewport.xMin + viewport.xMax) * 0.5f / screenWidth;
            var ny = (viewport.yMin + viewport.yMax) * 0.5f / screenHeight;
            const int steps = 48;
            for (var i = 0; i <= steps; i++)
            {
                var candidate = lo + (hi - lo) * (i / (float)steps);
                var shifted = new Vector2(
                    focus.x - (nx - 0.5f) * 2f * candidate * aspect,
                    focus.y - (ny - 0.5f) * 2f * candidate);
                shifted = ClampCenter(shifted, candidate, aspect, worldMin, worldMax);
                var got = ViewportSpan(boundsMin, boundsMax, shifted, candidate, aspect, viewport, screenWidth, screenHeight);
                if (got.x < fittedCover.x - 0.005f || got.y < fittedCover.y - 0.005f)
                    continue;

                var score = Mathf.Min(got.x, got.y);
                var sum = got.x + got.y;
                if (score > bestScore + 0.0005f || (Mathf.Abs(score - bestScore) <= 0.0005f && sum > bestSum + 0.0005f))
                {
                    bestScore = score;
                    bestSum = sum;
                    center = shifted;
                    orthographicSize = candidate;
                }
            }
        }

        public static Vector2 ViewportSpan(
            Vector2 boundsMin,
            Vector2 boundsMax,
            Vector2 center,
            float orthographicSize,
            float aspect,
            Rect viewport,
            float screenWidth,
            float screenHeight)
        {
            var west = ScreenToWorld(new Vector2(viewport.xMin, viewport.center.y), center, orthographicSize, aspect, screenWidth, screenHeight).x;
            var east = ScreenToWorld(new Vector2(viewport.xMax, viewport.center.y), center, orthographicSize, aspect, screenWidth, screenHeight).x;
            var south = ScreenToWorld(new Vector2(viewport.center.x, viewport.yMin), center, orthographicSize, aspect, screenWidth, screenHeight).y;
            var north = ScreenToWorld(new Vector2(viewport.center.x, viewport.yMax), center, orthographicSize, aspect, screenWidth, screenHeight).y;
            var spanX = Mathf.Max(0.0001f, boundsMax.x - boundsMin.x);
            var spanY = Mathf.Max(0.0001f, boundsMax.y - boundsMin.y);
            return new Vector2(
                Mathf.Max(0f, Mathf.Min(boundsMax.x, east) - Mathf.Max(boundsMin.x, west)) / spanX,
                Mathf.Max(0f, Mathf.Min(boundsMax.y, north) - Mathf.Max(boundsMin.y, south)) / spanY);
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
