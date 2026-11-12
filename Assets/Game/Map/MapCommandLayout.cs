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
        public const float MenuTypePoints = 15f;

        /// <summary>
        /// 1920×1080 and 932×430 are already point-sized. The 2868×1320 framebuffer
        /// is the 3x iPhone class, where 44 points are 132 physical pixels.
        /// </summary>
        public static float PointScale(float screenWidth, float screenHeight)
        {
            var shortSide = screenWidth < screenHeight ? screenWidth : screenHeight;
            var longSide = screenWidth > screenHeight ? screenWidth : screenHeight;
            if (shortSide >= 1200f && longSide >= 2400f)
                return 3f;
            return 1f;
        }

        public static float TouchPixels(float screenWidth, float screenHeight)
        {
            return MinTouchPixels * PointScale(screenWidth, screenHeight);
        }
        public const string LongestHome = "Startseite";
        public const string LongestPage = "Harita";
        public const string LongestMode = "Warehouses";
        public const string LongestRailLabel = "Depolar";
        public const string LongestSoon = "Coming soon";
        public const string LongestPrompt = "Stadt auswählen";
        public const string LongestSummary = "Operationen konnten nicht geladen werden.";
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
            var headerH = Mathf.Clamp(font * 2.05f, TouchPixels(screenWidth, screenHeight), safe.height * 0.11f);
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

        /// <summary>
        /// Canvas gap after the measured Core glyph before the region chip.
        /// PreferredWidth alone under-reports bold overhang on device.
        /// </summary>
        public static float BrandChipGapCanvas(int titleSize)
        {
            return Mathf.Max(28f, titleSize * 0.85f);
        }

        public static float BrandGlyphPadCanvas(int titleSize)
        {
            return Mathf.Max(4f, titleSize * 0.18f);
        }

        public static float RailIconPixels(float fontPixels)
        {
            return 24f * LogicalScale(fontPixels);
        }

        public static float RailLabelPixels(float fontPixels)
        {
            return 13f * LogicalScale(fontPixels);
        }

        public static float RailIconLabelGap(float fontPixels)
        {
            return 3f * LogicalScale(fontPixels);
        }

        public static float RailStatusPixels(float fontPixels) => RailLabelPixels(fontPixels) * 0.76f;
        public static float RailStatusBlock(float labelBlock) => labelBlock * 0.84f;
        public static float RailRowGap(float fontPixels) => 6f * LogicalScale(fontPixels);

        public readonly struct RailRowFrames
        {
            public RailRowFrames(Rect icon, Rect status, Rect label) { Icon = icon; Status = status; Label = label; }
            public Rect Icon { get; }
            public Rect Status { get; }
            public Rect Label { get; }
        }

        // Top-down coordinates in the row's canvas units; shared by placement and offline geometry checks.
        public static RailRowFrames RailFrames(float width, float height, float icon, float labelBlock, float gap, bool comingSoon, float verticalBias = 0.5f)
        {
            var glyph = comingSoon ? icon * 0.62f : icon;
            var status = comingSoon ? RailStatusBlock(labelBlock) : 0f;
            var block = glyph + gap + labelBlock + (comingSoon ? status + gap : 0f);
            var top = Mathf.Max(0f, height - block) * Mathf.Clamp01(verticalBias);
            var textWidth = Mathf.Max(0f, width - 8f);
            var statusY = top + glyph + gap;
            var labelY = comingSoon ? statusY + status + gap : statusY;
            return new RailRowFrames(new Rect((width - glyph) * 0.5f, top, glyph, glyph),
                new Rect((width - textWidth) * 0.5f, statusY, textWidth, status),
                new Rect((width - textWidth) * 0.5f, labelY, textWidth, labelBlock));
        }

        public static float RailLabelBlock(float fontPixels)
        {
            return RailLabelPixels(fontPixels) * 1.25f;
        }

        /// <summary>
        /// One rail row in screen pixels. Uses 44 iOS points via <see cref="TouchPixels"/>
        /// when that is larger than the icon+label stack. Eight rows at that size do not
        /// fit the 2868×1320 band; the rail must scroll rather than shrink hits.
        /// </summary>
        public static float RailRowPixels(float fontPixels)
        {
            return RailRowPixels(fontPixels, 1920f, 1080f);
        }

        public static float RailRowPixels(float fontPixels, float screenWidth, float screenHeight)
        {
            var icon = RailIconPixels(fontPixels);
            var gap = RailIconLabelGap(fontPixels);
            var label = RailLabelBlock(fontPixels);
            var stacked = Mathf.Max(icon + gap + label,
                icon * 0.62f + gap * 2f + RailStatusBlock(label) + label);
            return Mathf.Max(TouchPixels(screenWidth, screenHeight), stacked + 8f * LogicalScale(fontPixels));
        }

        public static float RailContentHeight(float fontPixels, int count, float gapPixels)
        {
            return RailContentHeight(fontPixels, count, gapPixels, 1920f, 1080f);
        }

        public static float RailContentHeight(float fontPixels, int count, float gapPixels, float screenWidth, float screenHeight)
        {
            if (count < 1)
                return 0f;

            var row = RailRowPixels(fontPixels, screenWidth, screenHeight);
            return count * row + Mathf.Max(0, count - 1) * gapPixels;
        }

        public static float RailOverflowGap(float viewportHeight, float rowHeight, float baseGap)
        {
            return Mathf.Max(baseGap, Mathf.Min(rowHeight * 0.18f, viewportHeight * 0.018f));
        }

        public static float RailOverflowTopInset(float viewportHeight, float rowHeight, float edgeInset)
        {
            return Mathf.Max(edgeInset, Mathf.Min(rowHeight * 0.3f, viewportHeight * 0.035f));
        }

        public static float RailContentTopInset(float viewportHeight, float rowsHeight, float edgeInset)
        {
            return Mathf.Max(edgeInset, (viewportHeight - rowsHeight) * 0.5f);
        }

        public static float RailContentPixels(float fontPixels)
        {
            var label = RailLabelPixels(fontPixels);
            var pad = 16f * LogicalScale(fontPixels);
            var width = Mathf.Max(RailTouchPixels(fontPixels), TextWidth(LongestRailLabel, label) + pad);
            var mode = TextWidth(LongestMode, fontPixels);
            if (mode > 1f && width >= mode)
                width = Mathf.Max(RailTouchPixels(fontPixels), mode - 1f);
            return width;
        }

        public static float MenuTypePixels(float screenWidth, float screenHeight)
        {
            return MenuTypePoints * PointScale(screenWidth, screenHeight);
        }

        public static int MenuCanvasSize(float screenWidth, float screenHeight, float canvasScale)
        {
            var scale = canvasScale < 0.0001f ? 1f : canvasScale;
            return Mathf.Max(12, Mathf.RoundToInt(MenuTypePixels(screenWidth, screenHeight) / scale));
        }

        public static float RegionMenuWidth(float typePixels, float pointScale, string[] labels)
        {
            var longest = 0f;
            if (labels != null)
            {
                for (var i = 0; i < labels.Length; i++)
                    longest = Mathf.Max(longest, TextWidth(labels[i], typePixels));
            }

            var pad = 20f * Mathf.Max(1f, pointScale);
            return Mathf.Max(longest + pad * 2f, 96f * Mathf.Max(1f, pointScale));
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
