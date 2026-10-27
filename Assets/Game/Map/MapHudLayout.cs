using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Reference-pixel sizes for the map shell. 1920×1080 with a full safe area
    /// returns the authored scene sizes. Shorter viewports grow those sizes only
    /// while the rail, inspector, and map column still fit.
    /// </summary>
    public static class MapHudLayout
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float Match = 0.5f;

        public const float TopBarHeight = 56f;
        public const float StatusHeight = 36f;
        public const float RailWidth = 148f;
        public const float InspectorWidth = 292f;
        public const float RailButtonHeight = 56f;
        public const float CollapseHeight = 44f;
        public const float CollapseWidth = 168f;
        public const float CollapseMinWidth = 120f;
        public const float ScopeWidth = 220f;
        public const float TitleMinWidth = 160f;
        public const float RailPadding = 8f;
        public const float RailSpacing = 6f;
        public const float InspectorPadding = 14f;
        public const float InspectorSpacing = 6f;
        public const float TopPaddingLeft = 16f;
        public const float TopPaddingRight = 12f;
        public const float TopPaddingVertical = 8f;
        public const float TopSpacing = 12f;
        public const int RailButtonCount = 5;

        public const int TitleSize = 26;
        public const int ScopeSize = 16;
        public const int CollapseSize = 15;
        public const int RailLabelSize = 15;
        public const int ZoomLabelSize = 22;
        public const int InspectorTitleSize = 20;
        public const int InspectorLabelSize = 13;
        public const int InspectorValueSize = 16;
        public const int StatusSize = 16;

        public const float RailLabelInset = 6f;
        public const float IconColumn = 22f;
        public const float EdgeGap = 10f;
        public const float StatusChipWidth = 240f;
        const float TargetButtonFraction = 0.1f;
        const float MinimumMapFraction = 0.5f;
        const float MaxBoost = 2.25f;

        public readonly struct Metrics
        {
            public readonly float CanvasScale;
            public readonly float Boost;
            public readonly bool Compact;
            public readonly float TopBarHeight;
            public readonly float StatusHeight;
            public readonly float RailWidth;
            public readonly float InspectorWidth;
            public readonly float RailButtonHeight;
            public readonly float CollapseHeight;
            public readonly float CollapseWidth;
            public readonly float CollapseMinWidth;
            public readonly float ScopeWidth;
            public readonly float RailPadding;
            public readonly float RailSpacing;
            public readonly float InspectorPadding;
            public readonly float InspectorSpacing;
            public readonly float TopPaddingLeft;
            public readonly float TopPaddingRight;
            public readonly float TopPaddingVertical;
            public readonly float TopSpacing;
            public readonly int TitleSize;
            public readonly int ScopeSize;
            public readonly int CollapseSize;
            public readonly int RailLabelSize;
            public readonly int ZoomLabelSize;
            public readonly int InspectorTitleSize;
            public readonly int InspectorLabelSize;
            public readonly int InspectorValueSize;
            public readonly int StatusSize;

            public Metrics(
                float canvasScale,
                float boost,
                bool compact,
                float topBarHeight,
                float statusHeight,
                float railWidth,
                float inspectorWidth,
                float railButtonHeight,
                float collapseHeight,
                float collapseWidth,
                float collapseMinWidth,
                float scopeWidth,
                float railPadding,
                float railSpacing,
                float inspectorPadding,
                float inspectorSpacing,
                float topPaddingLeft,
                float topPaddingRight,
                float topPaddingVertical,
                float topSpacing,
                int titleSize,
                int scopeSize,
                int collapseSize,
                int railLabelSize,
                int zoomLabelSize,
                int inspectorTitleSize,
                int inspectorLabelSize,
                int inspectorValueSize,
                int statusSize)
            {
                CanvasScale = canvasScale;
                Boost = boost;
                Compact = compact;
                TopBarHeight = topBarHeight;
                StatusHeight = statusHeight;
                RailWidth = railWidth;
                InspectorWidth = inspectorWidth;
                RailButtonHeight = railButtonHeight;
                CollapseHeight = collapseHeight;
                CollapseWidth = collapseWidth;
                CollapseMinWidth = collapseMinWidth;
                ScopeWidth = scopeWidth;
                RailPadding = railPadding;
                RailSpacing = railSpacing;
                InspectorPadding = inspectorPadding;
                InspectorSpacing = inspectorSpacing;
                TopPaddingLeft = topPaddingLeft;
                TopPaddingRight = topPaddingRight;
                TopPaddingVertical = topPaddingVertical;
                TopSpacing = topSpacing;
                TitleSize = titleSize;
                ScopeSize = scopeSize;
                CollapseSize = collapseSize;
                RailLabelSize = railLabelSize;
                ZoomLabelSize = zoomLabelSize;
                InspectorTitleSize = inspectorTitleSize;
                InspectorLabelSize = inspectorLabelSize;
                InspectorValueSize = inspectorValueSize;
                StatusSize = statusSize;
            }

            public float RailContentHeight =>
                RailPadding * 2f + RailButtonHeight * RailButtonCount + RailSpacing * (RailButtonCount - 1);

            public float VerticalChrome => TopBarHeight + StatusHeight + RailContentHeight;

            public float HorizontalChrome(bool inspectorVisible)
            {
                return RailWidth + (inspectorVisible ? InspectorWidth : 0f);
            }

            public float InspectorTitleRow => Mathf.Max(32f * Boost, InspectorTitleSize + 12f);

            public float InspectorLabelRow => Mathf.Max(20f * Boost, InspectorLabelSize + 7f);

            public float InspectorValueRow
            {
                get
                {
                    var lines = InspectorValueLines;
                    var pad = lines > 1 ? 6f : 12f;
                    return Mathf.Max(28f * Boost, lines * (InspectorValueSize + pad));
                }
            }

            public int InspectorValueLines =>
                FitsOneLine(LongestInspectorValue, InspectorValueSize, InspectorWidth - InspectorPadding * 2f) ? 1 : 2;

            public float CoordinatesRow => Mathf.Max(52f * Boost, InspectorLabelRow + InspectorValueRow + 4f);

            public float InspectorStackHeight =>
                InspectorPadding * 2f
                + InspectorTitleRow
                + InspectorLabelRow * 2f
                + InspectorValueRow * 2f
                + CoordinatesRow
                + InspectorSpacing * 5f;

            public float IconSize => Mathf.Min(IconColumn * Boost, Mathf.Max(16f, RailButtonHeight * 0.40f));

            public float Gap => EdgeGap * Boost;

            public float ChipWidth => StatusChipWidth * Boost;

            public float RailTextWidth =>
                RailWidth - RailPadding * 2f - RailLabelInset * 2f - IconSize;

            public float CardHeight(bool coordinatesVisible)
            {
                if (coordinatesVisible)
                    return InspectorStackHeight;

                return Mathf.Max(
                    InspectorPadding * 2f + InspectorTitleRow,
                    InspectorStackHeight - CoordinatesRow - InspectorSpacing);
            }

            public float RailButtonScreenPixels => RailButtonHeight * CanvasScale;

            public float InspectorLabelScreenPixels => InspectorLabelSize * CanvasScale;

            public Metrics WithRail(float railWidth, float railButtonHeight, int railLabel, int zoomLabel)
            {
                return new Metrics(
                    CanvasScale,
                    Boost,
                    Compact,
                    TopBarHeight,
                    StatusHeight,
                    railWidth,
                    InspectorWidth,
                    railButtonHeight,
                    CollapseHeight,
                    CollapseWidth,
                    CollapseMinWidth,
                    ScopeWidth,
                    RailPadding,
                    RailSpacing,
                    InspectorPadding,
                    InspectorSpacing,
                    TopPaddingLeft,
                    TopPaddingRight,
                    TopPaddingVertical,
                    TopSpacing,
                    TitleSize,
                    ScopeSize,
                    CollapseSize,
                    railLabel,
                    zoomLabel,
                    InspectorTitleSize,
                    InspectorLabelSize,
                    InspectorValueSize,
                    StatusSize);
            }

            public Metrics WithFonts(
                int title,
                int scope,
                int collapse,
                int rail,
                int zoom,
                int inspectorTitle,
                int inspectorLabel,
                int inspectorValue,
                int status)
            {
                return new Metrics(
                    CanvasScale,
                    Boost,
                    Compact,
                    TopBarHeight,
                    StatusHeight,
                    RailWidth,
                    InspectorWidth,
                    RailButtonHeight,
                    CollapseHeight,
                    CollapseWidth,
                    CollapseMinWidth,
                    ScopeWidth,
                    RailPadding,
                    RailSpacing,
                    InspectorPadding,
                    InspectorSpacing,
                    TopPaddingLeft,
                    TopPaddingRight,
                    TopPaddingVertical,
                    TopSpacing,
                    title,
                    scope,
                    collapse,
                    rail,
                    zoom,
                    inspectorTitle,
                    inspectorLabel,
                    inspectorValue,
                    status);
            }
        }

        public const string LongestRailLabel = "Türkiye";
        public const string LongestCollapseLabel = "Aufklappen";
        public const string LongestInspectorValue = "Stadt auswählen";

        public static Metrics Baseline()
        {
            return FromBoost(1f, 1f, false);
        }

        public static float CanvasScale(float screenWidth, float screenHeight)
        {
            return CanvasScale(screenWidth, screenHeight, new Vector2(ReferenceWidth, ReferenceHeight), Match);
        }

        public static float CanvasScale(float screenWidth, float screenHeight, Vector2 reference, float match)
        {
            if (screenWidth < 1f || screenHeight < 1f || reference.x < 1f || reference.y < 1f)
                return 1f;

            var logWidth = Mathf.Log(screenWidth / reference.x, 2f);
            var logHeight = Mathf.Log(screenHeight / reference.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, Mathf.Clamp01(match)));
        }

        public static Metrics Evaluate(float screenWidth, float screenHeight, Rect safeArea, bool inspectorVisible)
        {
            return Evaluate(
                screenWidth,
                screenHeight,
                safeArea,
                inspectorVisible,
                new Vector2(ReferenceWidth, ReferenceHeight),
                Match);
        }

        public static Metrics Evaluate(
            float screenWidth,
            float screenHeight,
            Rect safeArea,
            bool inspectorVisible,
            Vector2 reference,
            float match)
        {
            if (screenWidth < 1f || screenHeight < 1f)
                return Baseline();

            var scale = CanvasScale(screenWidth, screenHeight, reference, match);
            if (scale < 0.0001f)
                return Baseline();

            var safe = NormalizeSafe(safeArea, screenWidth, screenHeight);
            var coversScreen = safe.width >= screenWidth - 1f && safe.height >= screenHeight - 1f;
            if (scale >= 0.999f && coversScreen)
                return FromBoost(1f, scale, false);

            var localWidth = safe.width / scale;
            var localHeight = safe.height / scale;
            if (localWidth < 1f || localHeight < 1f)
                return FromBoost(1f, scale, false);

            var screenButton = RailButtonHeight * scale;
            var desired = screenButton < 0.001f
                ? 1f
                : (screenHeight * TargetButtonFraction) / screenButton;
            if (desired < 1f)
                desired = 1f;
            if (desired > MaxBoost)
                desired = MaxBoost;

            Metrics chrome;
            if (Fits(desired, localWidth, localHeight, inspectorVisible))
                chrome = FromBoost(desired, scale, false);
            else if (Fits(1f, localWidth, localHeight, inspectorVisible))
                chrome = FromBoost(LargestFit(1f, desired, localWidth, localHeight, inspectorVisible), scale, false);
            else
                chrome = FromBoost(LargestFit(0.05f, 1f, localWidth, localHeight, inspectorVisible), scale, true);

            chrome = ApplyReadableType(chrome, localHeight);
            return ApplyRailReadability(chrome, localWidth, localHeight, inspectorVisible);
        }

        public static bool FitsOneLine(string text, int fontSize, float width)
        {
            if (string.IsNullOrEmpty(text) || fontSize < 1 || width <= 1f)
                return false;

            return text.Length * fontSize * 0.62f <= width;
        }

        static Metrics ApplyReadableType(Metrics chrome, float localHeight)
        {
            var band = localHeight - chrome.TopBarHeight - chrome.StatusHeight;
            if (band < 48f)
                return chrome;

            var low = 0f;
            var high = 1f;
            var chosen = chrome;
            for (var i = 0; i < 12; i++)
            {
                var mid = (low + high) * 0.5f;
                var sample = TypeAt(chrome, localHeight, mid);
                if (sample.InspectorStackHeight <= band + 0.5f && TypeFitsControls(sample))
                {
                    chosen = sample;
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return chosen;
        }

        static Metrics ApplyRailReadability(Metrics chrome, float localWidth, float localHeight, bool inspectorVisible)
        {
            if (chrome.Boost > 0.999f && chrome.Boost < 1.001f)
                return chrome;

            var stack = chrome.RailPadding * 2f + chrome.RailSpacing * (RailButtonCount - 1);
            var band = localHeight - chrome.TopBarHeight - chrome.StatusHeight - stack;
            var maxButton = band / RailButtonCount;
            if (maxButton < chrome.RailButtonHeight)
                return chrome;

            var button = chrome.RailButtonHeight;
            if (button > maxButton)
                button = maxButton;
            var icon = Mathf.Min(IconColumn * chrome.Boost, Mathf.Max(16f, button * 0.40f));
            var minimumFont = ScaleFont(RailLabelSize, chrome.Boost);
            var targetFont = chrome.RailLabelSize;
            if (targetFont < minimumFont)
                targetFont = minimumFont;

            var reserved = inspectorVisible ? chrome.InspectorWidth : 0f;
            var maxRail = localWidth * (1f - MinimumMapFraction) - reserved;
            if (maxRail < chrome.RailWidth)
                maxRail = chrome.RailWidth;

            var needed = LongestRailLabel.Length * targetFont * 0.62f + chrome.RailPadding * 2f + RailLabelInset * 2f + icon;
            var railWidth = Mathf.Clamp(Mathf.Max(chrome.RailWidth, needed), chrome.RailWidth, maxRail);
            var textWidth = railWidth - chrome.RailPadding * 2f - RailLabelInset * 2f - icon;
            if (textWidth < 8f)
                textWidth = 8f;
            var font = CapToWidth(targetFont, LongestRailLabel, textWidth, minimumFont);
            var fontLimit = Mathf.FloorToInt(button - 12f);
            if (fontLimit < minimumFont)
                fontLimit = minimumFont;
            if (font > fontLimit)
                font = fontLimit;

            var zoomMinimum = ScaleFont(ZoomLabelSize, chrome.Boost);
            var zoom = chrome.ZoomLabelSize;
            if (zoom < zoomMinimum)
                zoom = zoomMinimum;
            if (zoom <= font)
                zoom = font + 1;
            var zoomLimit = Mathf.FloorToInt(button - 8f);
            if (zoom > zoomLimit)
                zoom = zoomLimit;
            if (zoom <= font && font + 1 <= zoomLimit)
                zoom = font + 1;
            if (zoom < 1)
                zoom = 1;

            return chrome.WithRail(railWidth, button, font, zoom);
        }

        static bool TypeFitsControls(Metrics sample)
        {
            return sample.TitleSize + 8f <= sample.TopBarHeight
                && sample.RailLabelSize + 8f <= sample.RailButtonHeight
                && sample.ZoomLabelSize + 8f <= sample.RailButtonHeight
                && sample.CollapseSize + 8f <= sample.CollapseHeight
                && sample.StatusSize + 8f <= sample.StatusHeight;
        }

        static Metrics TypeAt(Metrics chrome, float localHeight, float lift)
        {
            var title = Role(chrome.Boost, MapHudLayout.TitleSize, localHeight, 0.058f, lift, chrome.TopBarHeight - 8f);
            var zoom = Role(chrome.Boost, ZoomLabelSize, localHeight, 0.050f, lift, chrome.RailButtonHeight - 8f);
            var inspectorTitle = Role(chrome.Boost, InspectorTitleSize, localHeight, 0.052f, lift, 96f);
            var value = Role(chrome.Boost, InspectorValueSize, localHeight, 0.046f, lift, 80f);
            var scope = Role(chrome.Boost, ScopeSize, localHeight, 0.046f, lift, chrome.TopBarHeight - 8f);
            var status = Role(chrome.Boost, StatusSize, localHeight, 0.046f, lift, chrome.StatusHeight - 8f);
            var label = Role(chrome.Boost, InspectorLabelSize, localHeight, 0.040f, lift, value);
            var rail = Role(chrome.Boost, RailLabelSize, localHeight, 0.036f, lift, chrome.RailButtonHeight - 8f);
            var collapse = Role(chrome.Boost, CollapseSize, localHeight, 0.036f, lift, chrome.CollapseHeight - 8f);
            rail = CapToWidth(rail, LongestRailLabel, chrome.RailWidth - chrome.RailPadding * 2f, ScaleFont(RailLabelSize, chrome.Boost));
            collapse = CapToWidth(collapse, LongestCollapseLabel, chrome.CollapseWidth - 16f, ScaleFont(CollapseSize, chrome.Boost));
            value = CapToWidth(
                value,
                LongestInspectorValue,
                chrome.InspectorWidth - chrome.InspectorPadding * 2f,
                ScaleFont(InspectorValueSize, chrome.Boost));
            if (label >= value)
                label = Mathf.Max(1, value - 1);
            if (inspectorTitle < value)
                inspectorTitle = value;
            if (title < inspectorTitle)
                title = Mathf.Min(inspectorTitle, Mathf.Max(1, Mathf.FloorToInt(chrome.TopBarHeight - 8f)));

            return chrome.WithFonts(title, scope, collapse, rail, zoom, inspectorTitle, label, value, status);
        }

        static int Role(float boost, int authored, float localHeight, float fraction, float lift, float cap)
        {
            var boosted = ScaleFont(authored, boost);
            var target = Mathf.RoundToInt(localHeight * fraction);
            if (target < boosted)
                target = boosted;

            var size = Mathf.RoundToInt(Mathf.Lerp(boosted, target, Mathf.Clamp01(lift)));
            var limit = Mathf.FloorToInt(cap);
            if (limit < 1)
                limit = 1;
            if (size > limit)
                size = limit;
            if (size < boosted && boosted <= limit)
                size = boosted;
            if (size < 1)
                size = 1;
            return size;
        }

        static int CapToWidth(int size, string text, float width, int minimum)
        {
            if (minimum < 1)
                minimum = 1;
            while (size > minimum && !FitsOneLine(text, size, width))
                size--;

            return size < 1 ? 1 : size;
        }

        public static bool Fits(float boost, float localWidth, float localHeight, bool inspectorVisible)
        {
            if (boost <= 0f || localWidth <= 0f || localHeight <= 0f)
                return false;

            var metrics = FromBoost(boost, 1f, boost < 0.999f);
            if (metrics.VerticalChrome > localHeight + 0.5f)
                return false;

            var map = localWidth - metrics.HorizontalChrome(inspectorVisible);
            if (map < localWidth * MinimumMapFraction)
                return false;

            var topMinimum = metrics.TopPaddingLeft + metrics.TopPaddingRight + metrics.TopSpacing * 2f
                + TitleMinWidth * boost + metrics.CollapseMinWidth + ScopeWidth * 0.45f * boost;
            if (topMinimum > localWidth + 0.5f)
                return false;

            return metrics.RailButtonHeight >= 1f && metrics.TopBarHeight >= 1f;
        }

        static float LargestFit(float low, float high, float localWidth, float localHeight, bool inspectorVisible)
        {
            if (!Fits(low, localWidth, localHeight, inspectorVisible))
                return low;

            for (var i = 0; i < 18; i++)
            {
                var mid = (low + high) * 0.5f;
                if (Fits(mid, localWidth, localHeight, inspectorVisible))
                    low = mid;
                else
                    high = mid;
            }

            return low;
        }

        static Rect NormalizeSafe(Rect safeArea, float screenWidth, float screenHeight)
        {
            if (safeArea.width < 1f || safeArea.height < 1f)
                return new Rect(0f, 0f, screenWidth, screenHeight);

            return safeArea;
        }

        static Metrics FromBoost(float boost, float canvasScale, bool compact)
        {
            if (boost < 0.05f)
                boost = 0.05f;

            return new Metrics(
                canvasScale,
                boost,
                compact,
                TopBarHeight * boost,
                StatusHeight * boost,
                RailWidth * boost,
                InspectorWidth * boost,
                RailButtonHeight * boost,
                CollapseHeight * boost,
                CollapseWidth * boost,
                CollapseMinWidth * boost,
                ScopeWidth * boost,
                RailPadding * boost,
                RailSpacing * boost,
                InspectorPadding * boost,
                InspectorSpacing * boost,
                TopPaddingLeft * boost,
                TopPaddingRight * boost,
                TopPaddingVertical * boost,
                TopSpacing * boost,
                ScaleFont(TitleSize, boost),
                ScaleFont(ScopeSize, boost),
                ScaleFont(CollapseSize, boost),
                ScaleFont(RailLabelSize, boost),
                ScaleFont(ZoomLabelSize, boost),
                ScaleFont(InspectorTitleSize, boost),
                ScaleFont(InspectorLabelSize, boost),
                ScaleFont(InspectorValueSize, boost),
                ScaleFont(StatusSize, boost));
        }

        static int ScaleFont(int authored, float boost)
        {
            if (boost > 0.999f && boost < 1.001f)
                return authored;

            var size = Mathf.RoundToInt(authored * boost);
            return size < 1 ? 1 : size;
        }
    }
}
