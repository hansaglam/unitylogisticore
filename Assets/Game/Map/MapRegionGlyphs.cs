using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Small header glyphs drawn once and shared. White pixels, tinted by the chip.
    /// </summary>
    public static class MapRegionGlyphs
    {
        const int Size = 64;

        static Sprite _globe;
        static Sprite _chevron;
        static Sprite _plate;

        public static Sprite Globe => _globe != null ? _globe : (_globe = Make(DrawGlobe));
        public static Sprite Chevron => _chevron != null ? _chevron : (_chevron = Make(DrawChevron));
        public static Sprite Plate => _plate != null ? _plate : (_plate = MakePlate());

        static Sprite Make(System.Action<Color32[], int> draw)
        {
            var pixels = new Color32[Size * Size];
            draw(pixels, Size);
            return SpriteOf(pixels, Size, Vector4.zero);
        }

        static Sprite MakePlate()
        {
            const int n = 32;
            const int radius = 6;
            var pixels = new Color32[n * n];
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    if (Inside(x, y, n, radius))
                        pixels[y * n + x] = new Color32(255, 255, 255, 255);
                }
            }

            return SpriteOf(pixels, n, new Vector4(radius, radius, radius, radius));
        }

        static bool Inside(int x, int y, int n, int radius)
        {
            var left = x < radius;
            var right = x >= n - radius;
            var bottom = y < radius;
            var top = y >= n - radius;
            if (!left && !right && !bottom && !top)
                return true;

            var cx = left ? radius : (right ? n - 1 - radius : x);
            var cy = bottom ? radius : (top ? n - 1 - radius : y);
            var dx = x - cx;
            var dy = y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }

        static Sprite SpriteOf(Color32[] pixels, int n, Vector4 border)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static void DrawGlobe(Color32[] pixels, int n)
        {
            const int cx = 32;
            const int cy = 32;
            Ring(pixels, n, cx, cy, 28, 23);
            for (var y = cy - 23; y <= cy + 23; y++)
            {
                Plot(pixels, n, cx, y);
                Plot(pixels, n, cx + 1, y);
            }

            for (var x = cx - 26; x <= cx + 26; x++)
            {
                Plot(pixels, n, x, cy);
                Plot(pixels, n, x, cy + 1);
            }

            Ring(pixels, n, cx, cy, 15, 11);
        }

        static void DrawChevron(Color32[] pixels, int n)
        {
            for (var i = 0; i < 18; i++)
            {
                for (var t = 0; t < 5; t++)
                {
                    Plot(pixels, n, 14 + i, 40 - i + t);
                    Plot(pixels, n, 50 - i, 40 - i + t);
                }
            }
        }

        static void Ring(Color32[] pixels, int n, int cx, int cy, int outer, int inner)
        {
            var outer2 = outer * outer;
            var inner2 = inner * inner;
            for (var y = cy - outer; y <= cy + outer; y++)
            {
                for (var x = cx - outer; x <= cx + outer; x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    var d = dx * dx + dy * dy;
                    if (d <= outer2 && d >= inner2)
                        Plot(pixels, n, x, y);
                }
            }
        }

        static void Plot(Color32[] pixels, int n, int x, int y)
        {
            if (x < 0 || y < 0 || x >= n || y >= n)
                return;
            pixels[y * n + x] = new Color32(255, 255, 255, 255);
        }
    }
}
