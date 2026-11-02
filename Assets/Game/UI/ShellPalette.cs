using LogistiCore.Map;
using UnityEngine;

namespace LogistiCore.UI
{
    /// <summary>
    /// Opaque portrait surfaces derived from the map HUD. The map itself stays translucent
    /// because the world shows through; these screens have no map behind them.
    /// </summary>
    public static class ShellPalette
    {
        public static readonly Color Background = new Color(0.027f, 0.055f, 0.094f, 1f);
        public static readonly Color Card = new Color(0.048f, 0.086f, 0.132f, 1f);
        public static readonly Color CardRaised = new Color(0.090f, 0.158f, 0.208f, 1f);
        public static readonly Color Action = new Color(0.078f, 0.145f, 0.196f, 1f);
        public static readonly Color Well = new Color(0.016f, 0.034f, 0.058f, 1f);
        public static readonly Color NoticeFill = new Color(0.034f, 0.062f, 0.098f, 1f);
        public static readonly Color Border = new Color(0.42f, 0.58f, 0.66f, 0.42f);
        public static readonly Color BorderStrong = new Color(0.47f, 0.76f, 0.84f, 0.92f);
        public static readonly Color Pressed = new Color(0.11f, 0.28f, 0.34f, 1f);
        public static readonly Color Highlighted = new Color(0.12f, 0.20f, 0.26f, 1f);
        public static readonly Color EdgeShadow = new Color(0.008f, 0.016f, 0.028f, 0.9f);
        public static readonly Color TextSecondary = new Color(0.76f, 0.84f, 0.88f, 1f);

        public static Color Text => MapHudPalette.Text;
        public static Color TextMuted => MapHudPalette.TextMuted;
        public static Color Accent => MapHudPalette.Accent;
        public static Color AccentQuiet => MapHudPalette.AccentQuiet;
        public static Color Rule => MapHudPalette.Rule;
    }
}
