using UnityEngine;

namespace LogistiCore.UI
{
    /// <summary>
    /// Screen-pixel geometry for the portrait Home shell.
    /// Type and card sizes are canvas units. CanvasScaler (1080×1920, match 0.5)
    /// converts them to screen pixels, so a 1320×2868 framebuffer stays readable.
    /// Earlier caps of 22px labels and 64px buttons were physical pixels. On this
    /// phone those caps are about 7pt and 21pt, which left the rest of the screen empty.
    /// </summary>
    public static class HomeShellLayout
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float Match = 0.5f;
        public const float MinButtonPixels = 48f;
        public const float ButtonGapPixels = 12f;

        public readonly struct Metrics
        {
            public readonly float CanvasScale;
            public readonly float Padding;
            public readonly Rect Content;
            public readonly Rect Button;
            public readonly Rect CompanyButton;
            public readonly Rect Notice;
            public readonly int BrandSize;
            public readonly int HeadingSize;
            public readonly int BodySize;
            public readonly int ButtonLabelSize;
            public readonly int CaptionSize;
            public readonly float ButtonHeight;
            public readonly bool Overflow;

            public Metrics(
                float canvasScale,
                float padding,
                Rect content,
                Rect button,
                Rect companyButton,
                Rect notice,
                int brandSize,
                int headingSize,
                int bodySize,
                int buttonLabelSize,
                int captionSize,
                float buttonHeight,
                bool overflow)
            {
                CanvasScale = canvasScale;
                Padding = padding;
                Content = content;
                Button = button;
                CompanyButton = companyButton;
                Notice = notice;
                BrandSize = brandSize;
                HeadingSize = headingSize;
                BodySize = bodySize;
                ButtonLabelSize = buttonLabelSize;
                CaptionSize = captionSize;
                ButtonHeight = buttonHeight;
                Overflow = overflow;
            }
        }

        public static Metrics Evaluate(float screenWidth, float screenHeight, Rect safeArea)
        {
            var safe = safeArea.width < 1f || safeArea.height < 1f
                ? new Rect(0f, 0f, screenWidth, screenHeight)
                : safeArea;
            var scale = CanvasScale(screenWidth, screenHeight);
            if (scale < 0.0001f)
                scale = 1f;

            var padding = ShellTypography.Padding * scale;
            var content = new Rect(
                safe.xMin + padding,
                safe.yMin + padding,
                Mathf.Max(1f, safe.width - padding * 2f),
                Mathf.Max(1f, safe.height - padding * 2f));

            var mapHeight = ShellTypography.MapCard * scale;
            var companyHeight = ShellTypography.CompanyCard * scale;
            var noticeHeight = ShellTypography.NoticeCard * scale;
            var header = ShellTypography.HeaderBlock * scale;
            var gaps = ShellTypography.BlockGap * 3f * scale;
            var floor = MinButtonPixels;
            var overflow = false;
            var spare = content.height - (header + gaps + mapHeight + companyHeight + noticeHeight);
            if (spare < 0f)
            {
                var needed = -spare;
                var mapSlack = Mathf.Max(0f, mapHeight - Mathf.Max(floor * 2f, mapHeight * 0.72f));
                var companySlack = Mathf.Max(0f, companyHeight - Mathf.Max(floor * 1.6f, companyHeight * 0.72f));
                var noticeSlack = Mathf.Max(0f, noticeHeight - floor);
                var take = Mathf.Min(needed, noticeSlack);
                noticeHeight -= take;
                needed -= take;
                take = Mathf.Min(needed, companySlack);
                companyHeight -= take;
                needed -= take;
                take = Mathf.Min(needed, mapSlack);
                mapHeight -= take;
                needed -= take;
                overflow = needed > 1f;
            }

            var y = content.yMax - header - ShellTypography.BlockGap * scale;
            var map = new Rect(content.xMin, y - mapHeight, content.width, mapHeight);
            y = map.yMin - ShellTypography.BlockGap * scale;
            var company = new Rect(content.xMin, y - companyHeight, content.width, companyHeight);
            y = company.yMin - ShellTypography.BlockGap * scale;
            var notice = new Rect(content.xMin, y - noticeHeight, content.width, noticeHeight);
            return new Metrics(
                scale,
                padding,
                content,
                map,
                company,
                notice,
                ShellTypography.Brand,
                ShellTypography.Heading,
                ShellTypography.Body,
                ShellTypography.Button,
                ShellTypography.Caption,
                mapHeight,
                overflow);
        }

        public static float CanvasScale(float screenWidth, float screenHeight)
        {
            return Map.MapHudLayout.CanvasScale(
                screenWidth,
                screenHeight,
                new Vector2(ReferenceWidth, ReferenceHeight),
                Match);
        }

        public static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - 0.5f &&
                   inner.yMin >= outer.yMin - 0.5f &&
                   inner.xMax <= outer.xMax + 0.5f &&
                   inner.yMax <= outer.yMax + 0.5f;
        }

        public static bool LabelFits(string text, int canvasFontSize, float canvasWidth)
        {
            return Map.MapHudLayout.FitsOneLine(text, canvasFontSize, canvasWidth);
        }

        public static float CardTextWidth(float contentWidth, float canvasScale)
        {
            var scale = canvasScale < 0.0001f ? 1f : canvasScale;
            return contentWidth / scale - ShellTypography.Motif - ShellTypography.CardPad * 2f - 16f;
        }
    }
}
