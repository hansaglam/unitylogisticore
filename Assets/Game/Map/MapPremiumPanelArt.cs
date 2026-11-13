using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Reusable, entirely decorative header texture for management views.
    /// A command-console grid motif: it is never a real route or live telemetry.
    /// </summary>
    public static class MapPremiumPanelArt
    {
        const int Width = 256;
        const int Height = 88;
        static Sprite _header;

        public static Sprite Header
        {
            get
            {
                if (_header == null)
                    _header = Build();
                return _header;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { _header = null; }

        static Sprite Build()
        {
            var pixels = new Color32[Width * Height];
            for (var y = 0; y < Height; y++)
            {
                var height = y / (float)(Height - 1);
                for (var x = 0; x < Width; x++)
                {
                    var axis = x / (float)(Width - 1);
                    var glow = Mathf.Pow(1f - Mathf.Abs(axis - 0.36f), 3f)
                        * Mathf.Pow(1f - Mathf.Abs(height - 0.45f), 1.5f);
                    var baseColor = Color.Lerp(new Color(0.027f, 0.083f, 0.132f, 1f),
                        new Color(0.05f, 0.15f, 0.23f, 1f), height);
                    baseColor += new Color(0.01f, 0.07f, 0.11f, 0f) * glow;
                    if ((x % 28 == 0 || y % 28 == 0) && x > 24)
                        baseColor += new Color(0.008f, 0.035f, 0.055f, 0f);
                    pixels[y * Width + x] = baseColor;
                }
            }
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "LogistiCorePanelAtlas",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, Width, Height),
                new Vector2(0.5f, 0.5f), Width);
            sprite.name = "LogistiCorePanelAtlas";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
