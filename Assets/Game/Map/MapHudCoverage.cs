using UnityEngine;

namespace LogistiCore.Map
{
    public enum MapHudSampleKind
    {
        Map = 0,
        Header = 1,
        Footer = 2,
        Rail = 3,
        Panel = 4,
        Blocker = 5
    }

    /// <summary>
    /// Classifies a capture/screen pixel against intended HUD chrome and the
    /// management overlay. Expanded overlay covering the map viewport is expected.
    /// Opaque UI on a Map sample is leakage.
    /// </summary>
    public static class MapHudCoverage
    {
        public static Vector2 HolePixel(int width, int height)
        {
            return new Vector2(width * 0.5f, height * 0.5f);
        }

        public static bool Contains(Rect rect, Vector2 pixel)
        {
            return pixel.x >= rect.xMin && pixel.x < rect.xMax
                && pixel.y >= rect.yMin && pixel.y < rect.yMax;
        }

        public static bool IntendedChrome(MapHudSampleKind kind)
        {
            return kind == MapHudSampleKind.Header
                || kind == MapHudSampleKind.Footer
                || kind == MapHudSampleKind.Rail;
        }

        public static bool IntendedManagement(MapHudSampleKind kind)
        {
            return kind == MapHudSampleKind.Panel || kind == MapHudSampleKind.Blocker;
        }

        public static bool AllowsOpaqueUi(MapHudSampleKind kind)
        {
            return IntendedChrome(kind) || IntendedManagement(kind);
        }

        public static void OverlayRects(
            Rect viewport,
            float fontPixels,
            float touchPixels,
            bool panelOpen,
            bool expanded,
            out Rect panel,
            out Rect blocker)
        {
            panel = default;
            blocker = default;
            if (!panelOpen)
                return;

            panel = OverlayFrame(viewport, fontPixels, touchPixels, expanded);
            if (expanded)
                blocker = MapManagementLayout.Blocker(viewport);
        }

        public static MapHudSampleKind Classify(
            Rect header,
            Rect footer,
            Rect rail,
            Rect panel,
            Rect blocker,
            bool panelOpen,
            bool expanded,
            Vector2 pixel)
        {
            if (Contains(header, pixel))
                return MapHudSampleKind.Header;
            if (Contains(footer, pixel))
                return MapHudSampleKind.Footer;
            if (Contains(rail, pixel))
                return MapHudSampleKind.Rail;
            if (panelOpen && expanded && blocker.width > 1f && Contains(blocker, pixel))
                return MapHudSampleKind.Blocker;
            if (panelOpen && panel.width > 1f && Contains(panel, pixel))
                return MapHudSampleKind.Panel;
            return MapHudSampleKind.Map;
        }

        public static MapHudSampleKind Classify(
            MapCommandLayout.Metrics metrics,
            float touchPixels,
            bool panelOpen,
            bool expanded,
            Vector2 pixel)
        {
            OverlayRects(metrics.Viewport, metrics.FontPixels, touchPixels, panelOpen, expanded, out var panel, out var blocker);
            return Classify(metrics.Header, metrics.Summary, metrics.Rail, panel, blocker, panelOpen, expanded, pixel);
        }

        public static bool OpaqueUiCover(byte uiAlpha, byte uiRed, byte mapRed)
        {
            return uiAlpha > 250 && Mathf.Abs(uiRed - mapRed) > 40;
        }

        public static bool HoleCoverageAllowed(MapHudSampleKind kind, byte uiAlpha, byte uiRed, byte mapRed)
        {
            if (!OpaqueUiCover(uiAlpha, uiRed, mapRed))
                return true;
            return AllowsOpaqueUi(kind);
        }

        public static Rect OverlayFrame(Rect viewport, float fontPixels, float touchPixels, bool expanded)
        {
            return expanded
                ? MapManagementLayout.Expanded(viewport, fontPixels)
                : MapManagementLayout.Compact(viewport, fontPixels, touchPixels);
        }

        public static Rect TextureRect(RectTransform rect, Camera camera, int width, int height)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy)
                return default;

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 4; i++)
            {
                Vector2 pixel;
                if (camera != null)
                {
                    var viewport = camera.WorldToViewportPoint(corners[i]);
                    pixel = new Vector2(viewport.x * width, viewport.y * height);
                }
                else
                    pixel = new Vector2(corners[i].x, corners[i].y);

                min = Vector2.Min(min, pixel);
                max = Vector2.Max(max, pixel);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
