using UnityEngine;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// Screen-pixel frames for the landscape management overlay. Compact keeps the
    /// map largely visible. Expanded fills the existing map viewport. Header, rail,
    /// and summary stay outside the overlay.
    /// </summary>
    public static class MapManagementLayout
    {
        public static Rect Compact(Rect viewport, float fontPixels, float touchPixels)
        {
            if (viewport.width < 8f || viewport.height < 8f)
                return viewport;

            var inset = Mathf.Clamp(fontPixels * 0.35f, 8f, 16f);
            var width = Mathf.Clamp(viewport.width * 0.42f, Mathf.Max(220f, touchPixels * 4f), viewport.width - inset);
            if (width > viewport.width - inset)
                width = Mathf.Max(touchPixels * 3f, viewport.width - inset);
            var height = Mathf.Max(touchPixels * 2.4f, viewport.height - inset * 2f);
            if (height > viewport.height - inset)
                height = Mathf.Max(touchPixels * 2f, viewport.height - inset);
            var x = viewport.xMax - inset - width;
            if (x < viewport.xMin + inset)
                x = viewport.xMin + inset;
            var y = viewport.yMin + (viewport.height - height) * 0.5f;
            return new Rect(x, y, width, height);
        }

        public static Rect Expanded(Rect viewport, float fontPixels)
        {
            if (viewport.width < 8f || viewport.height < 8f)
                return viewport;

            var inset = Mathf.Clamp(fontPixels * 0.25f, 6f, 12f);
            return new Rect(
                viewport.xMin + inset,
                viewport.yMin + inset,
                Mathf.Max(8f, viewport.width - inset * 2f),
                Mathf.Max(8f, viewport.height - inset * 2f));
        }

        public static Rect Blocker(Rect viewport)
        {
            return viewport;
        }

        public static float CanvasTouch(float screenWidth, float screenHeight, float canvasScale)
        {
            var scale = canvasScale < 0.0001f ? 1f : canvasScale;
            return MapCommandLayout.TouchPixels(screenWidth, screenHeight) / scale;
        }

        public static float HitPixels(RectTransform rect, float canvasScale)
        {
            if (rect == null)
                return 0f;

            var scale = canvasScale < 0.0001f ? 1f : canvasScale;
            return Mathf.Min(rect.rect.width, rect.rect.height) * scale;
        }

        public static Rect HitRect(RectTransform rect, float canvasScale)
        {
            if (rect == null)
                return default;

            var scale = canvasScale < 0.0001f ? 1f : canvasScale;
            return new Rect(0f, 0f, rect.rect.width * scale, rect.rect.height * scale);
        }

        public static bool ChromeFits(float panelCanvasWidth, float touchCanvas, float pad)
        {
            return pad * 4f + touchCanvas * 3f <= panelCanvasWidth;
        }

        public static string ClipLine(string text, float fontPixels, float width)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            if (width <= 8f || fontPixels < 1f)
                return text;
            if (LineWidth(text, fontPixels) <= width)
                return text;

            const string ellipsis = "\u2026";
            var budget = width - LineWidth(ellipsis, fontPixels);
            if (budget <= 8f)
                return ellipsis;
            var count = text.Length;
            while (count > 0 && LineWidth(text.Substring(0, count), fontPixels) > budget)
                count--;
            return count <= 0 ? ellipsis : text.Substring(0, count) + ellipsis;
        }

        public static void ClipLabel(Text label)
        {
            if (label == null)
                return;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            var width = label.rectTransform.rect.width;
            label.text = ClipLine(label.text, label.fontSize, width);
        }

        static float LineWidth(string text, float fontPixels)
        {
            if (string.IsNullOrEmpty(text) || fontPixels < 1f)
                return 0f;
            return text.Length * fontPixels * 0.72f;
        }

        public static float ControlWidth(string text, float fontPixels, float touch, float pad)
        {
            var needed = MapCommandLayout.TextWidth(text ?? "", fontPixels) + pad * 2f;
            return Mathf.Max(touch, needed);
        }

        public static void Place(RectTransform rect, Rect screen, Rect origin, float scale)
        {
            if (rect == null)
                return;

            if (scale < 0.0001f)
                scale = 1f;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2((screen.x - origin.xMin) / scale, (screen.y - origin.yMin) / scale);
            rect.sizeDelta = new Vector2(screen.width / scale, screen.height / scale);
        }
    }
}
