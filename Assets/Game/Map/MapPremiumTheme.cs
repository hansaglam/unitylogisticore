using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>UI-01 semantic tokens for premium 2D landscape UI. No game or backend state.</summary>
    public static class MapPremiumTheme
    {
        public const float MinimumTouchPoints = 44f;
        public const float FastTransitionSeconds = 0.14f;
        public const float PanelTransitionSeconds = 0.20f;
        public const int TitleFont = 18;
        public const int BodyFont = 15;
        public const int CaptionFont = 12;

        public static readonly Color Background = Hex("#071522");
        public static readonly Color Panel = Alpha(Hex("#0E2638"), 0.96f);
        public static readonly Color Card = Alpha(Hex("#17384E"), 0.94f);
        public static readonly Color Chip = Alpha(Hex("#123246"), 0.96f);
        public static readonly Color Button = Hex("#12364A");
        public static readonly Color Selected = Hex("#14516C");
        public static readonly Color TextPrimary = Hex("#E9F2FA");
        public static readonly Color TextSecondary = Hex("#AFCCDB");
        public static readonly Color Cyan = Hex("#18BCEB");
        public static readonly Color Success = Hex("#16D8A1");
        public static readonly Color Warning = Hex("#F4B860");
        public static readonly Color Error = Hex("#F07178");
        public static readonly Color Outline = Alpha(Cyan, 0.32f);

        public static Color Surface(MapPremiumSurface role)
        {
            switch (role)
            {
                case MapPremiumSurface.Card: return Card;
                case MapPremiumSurface.Chip: return Chip;
                case MapPremiumSurface.Button: return Button;
                case MapPremiumSurface.Selected: return Selected;
                default: return Panel;
            }
        }

        public static Color Status(MapPremiumStatus status)
        {
            switch (status)
            {
                case MapPremiumStatus.Success: return Success;
                case MapPremiumStatus.Warning: return Warning;
                case MapPremiumStatus.Error: return Error;
                default: return TextSecondary;
            }
        }

        public static Color Alpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        public static float ContrastRatio(Color first, Color second)
        {
            float a = Luminance(first);
            float b = Luminance(second);
            return (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
        }

        static float Luminance(Color c)
        {
            return 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);
        }

        static float Linear(float value)
        {
            return value <= 0.04045f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        static Color Hex(string value)
        {
            if (ColorUtility.TryParseHtmlString(value, out var color))
                return color;
            return Color.magenta;
        }
    }

    public enum MapPremiumSurface { Panel, Card, Chip, Button, Selected }
    public enum MapPremiumStatus { Neutral, Success, Warning, Error }
    public enum MapPremiumTextRole { Title, Body, Caption }
}
