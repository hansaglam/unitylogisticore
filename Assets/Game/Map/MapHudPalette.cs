using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Compatibility facade for existing panels; all new styling is from
    /// MapPremiumTheme. Keep API stable for current offline/live UI callers.
    /// </summary>
    public static class MapHudPalette
    {
        public const string Brand = "LOGISTICORE";

        public static readonly Color Panel = MapPremiumTheme.Panel;
        public static readonly Color Card = MapPremiumTheme.Card;
        public static readonly Color Chip = MapPremiumTheme.Chip;
        public static readonly Color Button = MapPremiumTheme.Button;
        public static readonly Color ButtonActive = MapPremiumTheme.Selected;
        public static readonly Color Text = MapPremiumTheme.TextPrimary;
        public static readonly Color TextMuted = MapPremiumTheme.TextSecondary;
        public static readonly Color Accent = MapPremiumTheme.Cyan;
        public static readonly Color AccentQuiet = MapPremiumTheme.Alpha(MapPremiumTheme.Cyan, 0.55f);
        public static readonly Color Rule = MapPremiumTheme.Outline;
        public static readonly Color Pressed = new Color(0.78f, 0.89f, 0.94f, 1f);
        public static readonly Color Highlighted = new Color(0.92f, 0.97f, 1f, 1f);
    }
}
