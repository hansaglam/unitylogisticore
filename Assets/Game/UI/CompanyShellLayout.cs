using UnityEngine;

namespace LogistiCore.UI
{
    /// <summary>
    /// Portrait geometry for the company overview.
    /// The overview frame sits under the shared header. The Home action sits under that frame,
    /// with the remaining tall-phone space left as quiet margin.
    /// </summary>
    public static class CompanyShellLayout
    {
        public readonly struct Metrics
        {
            public readonly float CanvasScale;
            public readonly float Padding;
            public readonly Rect Content;
            public readonly Rect Notice;
            public readonly Rect HomeButton;
            public readonly int BrandSize;
            public readonly int HeadingSize;
            public readonly int BodySize;
            public readonly int StatusSize;
            public readonly int ButtonLabelSize;
            public readonly float ButtonHeight;
            public readonly bool Overflow;

            public Metrics(
                float canvasScale,
                float padding,
                Rect content,
                Rect notice,
                Rect homeButton,
                int brandSize,
                int headingSize,
                int bodySize,
                int statusSize,
                int buttonLabelSize,
                float buttonHeight,
                bool overflow)
            {
                CanvasScale = canvasScale;
                Padding = padding;
                Content = content;
                Notice = notice;
                HomeButton = homeButton;
                BrandSize = brandSize;
                HeadingSize = headingSize;
                BodySize = bodySize;
                StatusSize = statusSize;
                ButtonLabelSize = buttonLabelSize;
                ButtonHeight = buttonHeight;
                Overflow = overflow;
            }
        }

        public static Metrics Evaluate(float screenWidth, float screenHeight, Rect safeArea)
        {
            var home = HomeShellLayout.Evaluate(screenWidth, screenHeight, safeArea);
            var scale = home.CanvasScale < 0.0001f ? 1f : home.CanvasScale;
            var content = home.Content;
            var header = ShellTypography.TitleBlock * scale;
            var buttonHeight = ShellTypography.ActionHeight * scale;
            var gap = ShellTypography.BlockGap * scale;
            var reserved = header + buttonHeight + gap * 2f;
            var room = Mathf.Max(0f, content.height - reserved);
            var overview = Mathf.Clamp(
                content.height * 0.42f,
                Mathf.Min(ShellTypography.OverviewMin * scale, room),
                Mathf.Min(ShellTypography.OverviewMax * scale, room));
            var overflow = overview + reserved > content.height + 1f;
            var top = content.yMax - header - gap;
            var notice = new Rect(content.xMin, top - overview, content.width, overview);
            var button = new Rect(
                content.xMin,
                notice.yMin - gap - buttonHeight,
                content.width,
                buttonHeight);
            return new Metrics(
                home.CanvasScale,
                home.Padding,
                content,
                notice,
                button,
                ShellTypography.Brand,
                ShellTypography.Heading,
                ShellTypography.Body,
                ShellTypography.Section,
                ShellTypography.Button,
                buttonHeight,
                overflow);
        }
    }
}
