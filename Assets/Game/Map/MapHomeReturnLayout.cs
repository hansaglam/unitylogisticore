using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Screen-pixel rect for the map's single Home control.
    /// It floats in the upper-left of the safe area, clear of the initial
    /// Türkiye cities and the default World badge. The toolbar, header,
    /// inspector, and status chip are not part of the map anymore, so they
    /// are not reserved.
    /// </summary>
    public static class MapHomeReturnLayout
    {
        public const float MinTouchPixels = 48f;
        public const float GapPixels = 12f;
        public const int LabelCanvasSize = 32;
        public const string LongestLabel = "Startseite";

        public static bool TryPlace(float screenWidth, float screenHeight, Rect safeArea, out Rect button)
        {
            button = default;
            if (screenWidth < 1f || screenHeight < 1f)
                return false;

            var safe = safeArea.width < 1f || safeArea.height < 1f
                ? new Rect(0f, 0f, screenWidth, screenHeight)
                : safeArea;
            var scale = MapHudLayout.CanvasScale(screenWidth, screenHeight);
            if (scale < 0.0001f)
                scale = 1f;

            var touch = Mathf.Max(MinTouchPixels, 44f * screenHeight / 440f);
            var fontScreen = LabelCanvasSize * scale;
            var textWidth = LongestLabel.Length * fontScreen * 0.62f;
            var gap = Mathf.Max(GapPixels, GapPixels * screenHeight / 440f);
            var width = Mathf.Max(touch * 2.6f, textWidth + touch * 0.5f + gap * 2f);
            var height = Mathf.Max(touch, fontScreen + gap);
            var candidate = new Rect(safe.xMin + gap, safe.yMax - gap - height, width, height);
            if (Fits(candidate, safe, gap) && ClearsFrames(candidate, screenWidth, screenHeight, safe))
            {
                button = candidate;
                return true;
            }

            for (var step = 1; step <= 6; step++)
            {
                var nudged = new Rect(
                    safe.xMin + gap + step * gap,
                    safe.yMax - gap - height - step * gap * 0.5f,
                    width,
                    height);
                if (!Fits(nudged, safe, gap) || !ClearsFrames(nudged, screenWidth, screenHeight, safe))
                    continue;

                button = nudged;
                return true;
            }

            if (Fits(candidate, safe, gap))
            {
                button = candidate;
                return true;
            }

            return false;
        }

        public static bool CircleOverlaps(Vector2 point, float radius, Rect rect)
        {
            var x = Mathf.Clamp(point.x, rect.xMin, rect.xMax);
            var y = Mathf.Clamp(point.y, rect.yMin, rect.yMax);
            var dx = point.x - x;
            var dy = point.y - y;
            return dx * dx + dy * dy <= radius * radius;
        }

        static bool Fits(Rect candidate, Rect safe, float gap)
        {
            return candidate.xMin >= safe.xMin + gap - 0.5f &&
                   candidate.yMin >= safe.yMin + gap - 0.5f &&
                   candidate.xMax <= safe.xMax - gap + 0.5f &&
                   candidate.yMax <= safe.yMax - gap + 0.5f &&
                   candidate.width >= MinTouchPixels &&
                   candidate.height >= MinTouchPixels;
        }

        static bool ClearsFrames(Rect button, float screenWidth, float screenHeight, Rect safe)
        {
            var aspect = screenWidth / screenHeight;
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out var center, out var size);
            center = MapCameraRig.CenterClearOfRail(center, size, screenWidth, screenHeight, safe);
            if (OverlapsCities(button, center, size, screenWidth, screenHeight, MapMarkerLod.MarkerHitPixels))
                return false;

            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            MapViewMath.Fit(worldMin, worldMax, aspect, MapFrames.WorldFocusPadding, out var worldCenter, out var worldSize);
            var sum = Vector2.zero;
            for (var i = 0; i < MapCities.All.Length; i++)
            {
                var city = MapCities.All[i];
                sum += MapProjection.Project(city.Latitude, city.Longitude);
            }

            var centroid = sum / MapCities.All.Length;
            var units = MapMarkerLod.UnitsPerPixel(worldSize, screenHeight);
            var badge = MapMarkerLod.ToScreen(centroid, worldCenter, units, screenWidth, screenHeight);
            return !CircleOverlaps(badge, MapMarkerLod.AggregateHitRadius, button);
        }

        static bool OverlapsCities(
            Rect button,
            Vector2 cameraCenter,
            float orthographicSize,
            float screenWidth,
            float screenHeight,
            float radius)
        {
            var units = MapMarkerLod.UnitsPerPixel(orthographicSize, screenHeight);
            for (var i = 0; i < MapCities.All.Length; i++)
            {
                var city = MapCities.All[i];
                var world = MapProjection.Project(city.Latitude, city.Longitude);
                var screen = MapMarkerLod.ToScreen(world, cameraCenter, units, screenWidth, screenHeight);
                if (CircleOverlaps(screen, radius, button))
                    return true;
            }

            return false;
        }
    }
}
