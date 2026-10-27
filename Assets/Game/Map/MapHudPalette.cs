using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Shared colors for the map shell. Cyan stays on the brand, the active control,
    /// and small rules. Panels stay dark and translucent so the map remains the hero.
    /// </summary>
    public static class MapHudPalette
    {
        public const string Brand = "LOGISTICORE";

        public static readonly Color Panel = new Color(0.027f, 0.055f, 0.094f, 0.94f);
        public static readonly Color Card = new Color(0.035f, 0.067f, 0.110f, 0.86f);
        public static readonly Color Chip = new Color(0.027f, 0.055f, 0.094f, 0.82f);
        public static readonly Color Button = new Color(0.045f, 0.086f, 0.125f, 0.82f);
        public static readonly Color ButtonActive = new Color(0.070f, 0.155f, 0.190f, 0.96f);
        public static readonly Color Text = new Color(0.910f, 0.949f, 0.973f, 1f);
        public static readonly Color TextMuted = new Color(0.620f, 0.725f, 0.780f, 1f);
        public static readonly Color Accent = new Color(0.470f, 0.760f, 0.835f, 1f);
        public static readonly Color AccentQuiet = new Color(0.470f, 0.760f, 0.835f, 0.55f);
        public static readonly Color Rule = new Color(0.470f, 0.760f, 0.835f, 0.40f);
        public static readonly Color Pressed = new Color(0.78f, 0.88f, 0.92f, 1f);
        public static readonly Color Highlighted = new Color(0.88f, 0.93f, 0.95f, 1f);
    }
}
