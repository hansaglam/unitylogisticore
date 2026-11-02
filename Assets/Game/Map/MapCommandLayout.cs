using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Screen-pixel frames for the command shell. The map viewport is what remains
    /// after the header, rail, context panel, and summary. Nothing here invents
    /// operations data or builds a second navigation system.
    /// </summary>
    public static class MapCommandLayout
    {
        public const float MinTouchPixels = 44f;
        public const string LongestHome = "Startseite";
        public const string LongestPage = "Harita";
        public const string LongestMode = "Warehouses";
        public const string LongestRailLabel = "Depolar";
        public const string LongestSoon = "Demnächst";
        public const string LongestPrompt = "Stadt auswählen";
        public const string LongestSummary = "Keine Live-Operationen verbunden.";
        public const string LongestToggle = "Aufklappen";
        public const string LongestUnavailable = "Dieser Bereich ist noch nicht verfügbar.";

        public readonly struct Metrics
        {
            public readonly Rect Header;
            public readonly Rect Rail;
            public readonly Rect Context;
            public readonly Rect Summary;
            public readonly Rect Viewport;
            public readonly Rect Home;
            public readonly bool FutureModes;
            public readonly bool ContextOpen;
            public readonly float Gap;
            public readonly float FontPixels;
            public readonly int CanvasFont;
            public readonly int CanvasTitle;

            public Metrics(
                Rect header,
                Rect rail,
                Rect context,
                Rect summary,
                Rect viewport,
                Rect home,
                bool futureModes,
                bool contextOpen,
                float gap,
                float fontPixels,
                int canvasFont,
                int canvasTitle)
            {
                Header = header;
                Rail = rail;
                Context = context;
                Summary = summary;
                Viewport = viewport;
                Home = home;
                FutureModes = futureModes;
                ContextOpen = contextOpen;
                Gap = gap;
                FontPixels = fontPixels;
                CanvasFont = canvasFont;
                CanvasTitle = canvasTitle;
            }
        }

        public static bool PrefersOpenContext(float screenWidth, float screenHeight, Rect safeArea)
        {
            var safe = Normalize(screenWidth, screenHeight, safeArea);
            return safe.width >= 1100f && safe.height >= 640f;
        }

        public static Metrics Evaluate(float screenWidth, float screenHeight, Rect safeArea, bool contextOpen)
        {
            var safe = Normalize(screenWidth, screenHeight, safeArea);
            var font = FontPixelsFor(safe.height);
            var scale = MapHudLayout.CanvasScale(screenWidth, screenHeight);
            if (scale < 0.0001f)
                scale = 1f;

            var gap = Mathf.Clamp(font * 0.45f, 6f, 14f);
            var headerH = Mathf.Clamp(font * 2.05f, MinTouchPixels, safe.height * 0.11f);
            var summaryH = Mathf.Clamp(font * 1.35f, 22f, safe.height * 0.055f);
            if (headerH + summaryH > safe.height * 0.34f)
            {
                var budget = safe.height * 0.34f;
                headerH = budget * 0.62f;
                summaryH = budget - headerH;
            }

            var future = safe.height >= 640f && safe.width >= 1100f && headerH + summaryH + gap * 2f < safe.height * 0.4f;
            var railW = RailContentPixels(font);
            var cap = safe.width * (future ? 0.18f : 0.3f);
            if (railW > cap)
                railW = Mathf.Max(cap, RailTouchPixels(font));

            var open = contextOpen;
            var contextW = 0f;
            if (open)
            {
                var prompt = TextWidth(LongestPrompt, font) + gap * 2f + 12f;
                var wrapped = TextWidth(LongestUnavailable, font) * 0.45f;
                contextW = Mathf.Max(prompt, wrapped, 200f);
                contextW = Mathf.Min(contextW, safe.width * 0.28f);
            }

            var chrome = railW + contextW + gap * (contextW > 0f ? 3f : 2f);
            if (chrome > safe.width * 0.56f)
            {
                open = false;
                contextW = 0f;
                chrome = railW + gap * 2f;
                if (chrome > safe.width * 0.5f)
                    railW = Mathf.Max(88f, safe.width * 0.34f);
            }

            if (!future)
                open = contextOpen && contextW > 0f;

            var seam = 0f;
            var leftBleed = Mathf.Max(0f, safe.xMin);
            var rightBleed = Mathf.Max(0f, screenWidth - safe.xMax);
            var topBleed = Mathf.Max(0f, screenHeight - safe.yMax);
            var bottomBleed = Mathf.Max(0f, safe.yMin);
            var header = new Rect(0f, safe.yMax - headerH, screenWidth, headerH + topBleed);
            var summary = new Rect(0f, 0f, screenWidth, summaryH + bottomBleed);
            var bandBottom = safe.yMin + summaryH + seam;
            var bandTop = safe.yMax - headerH - seam;
            var bandHeight = Mathf.Max(48f, bandTop - bandBottom);
            var rail = new Rect(0f, bandBottom, leftBleed + railW, bandHeight);
            var left = rail.xMax + seam;
            var rightInset = open ? contextW + seam : 0f;
            var viewport = new Rect(left, bandBottom, Mathf.Max(48f, safe.xMax - rightInset - left), bandHeight);
            var context = open
                ? new Rect(safe.xMax - contextW, bandBottom, contextW + rightBleed, bandHeight)
                : new Rect(safe.xMax, bandBottom, 0f, 0f);

            var homeH = Mathf.Clamp(headerH - gap, MinTouchPixels * 0.8f, headerH - 4f);
            if (homeH > headerH - 4f)
                homeH = Mathf.Max(28f, headerH - 4f);
            var homeW = Mathf.Clamp(Mathf.Max(homeH, MinTouchPixels), MinTouchPixels, Mathf.Max(MinTouchPixels, headerH));
            var home = new Rect(
                safe.xMax - gap - homeW,
                header.y + (headerH - homeH) * 0.5f,
                homeW,
                homeH);

            var canvasFont = Mathf.Max(12, Mathf.RoundToInt(font / scale));
            var canvasTitle = Mathf.Max(canvasFont + 1, Mathf.RoundToInt(font * 1.15f / scale));
            return new Metrics(
                header,
                rail,
                context,
                summary,
                viewport,
                home,
                future,
                open,
                gap,
                font,
                canvasFont,
                canvasTitle);
        }

        public static bool LabelFits(string text, float fontPixels, float width)
        {
            if (string.IsNullOrEmpty(text) || fontPixels < 1f || width <= 1f)
                return false;

            return text.Length * fontPixels * 0.62f <= width;
        }

        public static float LogicalScale(float fontPixels)
        {
            return Mathf.Max(1f, fontPixels / 15f);
        }

        public static float RailTouchPixels(float fontPixels)
        {
            return 44f * LogicalScale(fontPixels);
        }

        public static float RailIconPixels(float fontPixels)
        {
            return 24f * LogicalScale(fontPixels);
        }

        public static float RailLabelPixels(float fontPixels)
        {
            return 13f * LogicalScale(fontPixels);
        }

        public static float RailRowPixels(float fontPixels)
        {
            var scale = LogicalScale(fontPixels);
            var stacked = RailIconPixels(fontPixels) + 6f * scale + RailLabelPixels(fontPixels) * 1.35f + 6f * scale;
            return Mathf.Max(RailTouchPixels(fontPixels), stacked);
        }

        public static float RailContentPixels(float fontPixels)
        {
            var label = RailLabelPixels(fontPixels);
            var pad = 16f * LogicalScale(fontPixels);
            return Mathf.Max(RailTouchPixels(fontPixels), TextWidth(LongestRailLabel, label) + pad);
        }

        public static float TextWidth(string text, float fontPixels)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;

            return text.Length * fontPixels * 0.62f;
        }

        public static float FontPixelsFor(float screenHeight)
        {
            var points = 15f;
            if (screenHeight < 600f)
                return points;

            return points * screenHeight / 440f;
        }

        static Rect Normalize(float screenWidth, float screenHeight, Rect safeArea)
        {
            if (safeArea.width < 1f || safeArea.height < 1f)
                return new Rect(0f, 0f, Mathf.Max(1f, screenWidth), Mathf.Max(1f, screenHeight));

            return safeArea;
        }
    }
}
