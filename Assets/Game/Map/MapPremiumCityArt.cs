using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Generic stylized skyline for the selected-city information panel.
    /// This is decorative artwork, not a photograph or a real landmark.
    /// Generated once in memory and deliberately independent of city or server data.
    /// </summary>
    public static class MapPremiumCityArt
    {
        const int Width = 320;
        const int Height = 128;
        static Sprite _banner;

        public static Sprite Banner
        {
            get
            {
                if (_banner == null)
                    _banner = Create();
                return _banner;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { _banner = null; }

        static Sprite Create()
        {
            var pixels = new Color32[Width * Height];
            var top = new Color(0.035f, 0.115f, 0.18f, 1f);
            var bottom = new Color(0.015f, 0.055f, 0.105f, 1f);
            for (int y = 0; y < Height; y++)
            {
                float t = y / (float)(Height - 1);
                for (int x = 0; x < Width; x++)
                {
                    float glow = Mathf.Clamp01(1f - Mathf.Abs(x - Width * 0.69f) / (Width * 0.6f))
                        * Mathf.Clamp01(1f - Mathf.Abs(y - Height * 0.51f) / (Height * 0.65f));
                    var color = Color.Lerp(bottom, top, t);
                    color += new Color(0.025f, 0.13f, 0.16f, 0f) * glow;
                    pixels[y * Width + x] = color;
                }
            }

            // One crisp horizon and a deterministic, abstract skyline. All of
            // the bars are generic silhouette shapes: no false city landmarks.
            for (int i = 0; i < 19; i++)
            {
                int left = i * 19 - 4;
                int width = 12 + (i % 4) * 2;
                int height = 18 + (i * 29 % 47);
                var shade = i % 3 == 0
                    ? new Color32(11, 44, 68, 255)
                    : new Color32(9, 38, 61, 255);
                Rect(pixels, left, 6, width, height, shade);
                for (int wx = left + 3; wx < left + width - 1; wx += 5)
                {
                    for (int wy = 12; wy < 6 + height - 3; wy += 8)
                    {
                        if ((i + wx + wy) % 5 == 0)
                            Rect(pixels, wx, wy, 2, 3, new Color32(34, 149, 175, 135));
                    }
                }
            }

            // A fine horizon band and sparse cartographic points evoke the
            // operations map without depicting nonexistent active routes.
            Rect(pixels, 0, 11, Width, 1, new Color32(29, 120, 149, 255));
            for (int i = 0; i < 7; i++)
            {
                int x = 15 + i * 45;
                int y = 85 + (i % 3) * 10;
                Rect(pixels, x, y, 2, 2, new Color32(49, 173, 205, 120));
            }

            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "LogistiCoreCityAtmosphere",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height),
                new Vector2(0.5f, 0.5f), Width);
            sprite.name = "LogistiCoreCityAtmosphere";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static void Rect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            for (int py = Mathf.Max(y, 0); py < Mathf.Min(y + height, Height); py++)
            {
                for (int px = Mathf.Max(x, 0); px < Mathf.Min(x + width, Width); px++)
                    pixels[py * Width + px] = color;
            }
        }
    }
}
