using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Small HUD glyphs. White pixels, tinted by the control. Not a brand logo.
    /// </summary>
    public static class MapHudIcons
    {
        public const int Wallet = 0;
        public const int Badge = 1;
        public const int Count = 2;
        const int Size = 64;

        static Sprite[] _icons;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _icons = null;
        }

        public static Sprite Get(int kind)
        {
            if (_icons == null)
            {
                _icons = new Sprite[Count];
                _icons[Wallet] = Make(DrawWallet);
                _icons[Badge] = Make(DrawBadge);
            }

            if (kind < 0 || kind >= _icons.Length)
                return _icons[Wallet];
            return _icons[kind];
        }

        static Sprite Make(System.Action<Color32[], int> draw)
        {
            var pixels = new Color32[Size * Size];
            draw(pixels, Size);
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static void DrawWallet(Color32[] pixels, int n)
        {
            Fill(pixels, n, 10, 18, 44, 30);
            Clear(pixels, n, 14, 22, 36, 22);
            Fill(pixels, n, 12, 16, 40, 6);
            Fill(pixels, n, 40, 28, 10, 10);
        }

        static void DrawBadge(Color32[] pixels, int n)
        {
            var cx = n * 0.5f;
            var cy = n * 0.5f;
            var r = n * 0.42f;
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    var dx = x + 0.5f - cx;
                    var dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r)
                        pixels[y * n + x] = new Color32(255, 255, 255, 255);
                }
            }
        }

        static void Fill(Color32[] pixels, int n, int x, int y, int w, int h)
        {
            var white = new Color32(255, 255, 255, 255);
            for (var yy = y; yy < y + h; yy++)
            {
                if (yy < 0 || yy >= n)
                    continue;
                for (var xx = x; xx < x + w; xx++)
                {
                    if (xx < 0 || xx >= n)
                        continue;
                    pixels[yy * n + xx] = white;
                }
            }
        }

        static void Clear(Color32[] pixels, int n, int x, int y, int w, int h)
        {
            var empty = new Color32(0, 0, 0, 0);
            for (var yy = y; yy < y + h; yy++)
            {
                if (yy < 0 || yy >= n)
                    continue;
                for (var xx = x; xx < x + w; xx++)
                {
                    if (xx < 0 || xx >= n)
                        continue;
                    pixels[yy * n + xx] = empty;
                }
            }
        }
    }
}
