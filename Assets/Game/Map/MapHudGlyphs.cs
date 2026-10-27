using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Tiny runtime glyphs for the five map commands. One texture set is shared
    /// for the life of the domain. No emoji and no external icon package.
    /// </summary>
    public static class MapHudGlyphs
    {
        public const int World = 0;
        public const int Turkey = 1;
        public const int ZoomIn = 2;
        public const int ZoomOut = 3;
        public const int Restore = 4;

        const int Size = 32;

        static Sprite[] _glyphs;
        static Sprite _rounded;

        public static Sprite Rounded()
        {
            if (_rounded == null)
                _rounded = MakeRounded();
            return _rounded;
        }

        public static Sprite Get(int kind)
        {
            if (_glyphs == null)
            {
                _glyphs = new Sprite[5];
                _glyphs[World] = Make(DrawWorld);
                _glyphs[Turkey] = Make(DrawDiamond);
                _glyphs[ZoomIn] = Make(DrawPlus);
                _glyphs[ZoomOut] = Make(DrawMinus);
                _glyphs[Restore] = Make(DrawFrame);
            }

            if (kind < 0 || kind >= _glyphs.Length)
                return _glyphs[World];
            return _glyphs[kind];
        }

        static Sprite Make(System.Action<Color32[], int> draw)
        {
            var pixels = new Color32[Size * Size];
            draw(pixels, Size);
            return SpriteFrom(pixels, Size, Vector4.zero);
        }

        static Sprite MakeRounded()
        {
            const int n = 64;
            const int radius = 16;
            var pixels = new Color32[n * n];
            var fill = new Color32(255, 255, 255, 255);
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    if (InsideRound(x, y, n, radius))
                        pixels[y * n + x] = fill;
                }
            }

            return SpriteFrom(pixels, n, new Vector4(radius, radius, radius, radius));
        }

        static bool InsideRound(int x, int y, int n, int radius)
        {
            var dx = 0;
            if (x < radius)
                dx = radius - x;
            else if (x >= n - radius)
                dx = x - (n - radius - 1);
            var dy = 0;
            if (y < radius)
                dy = radius - y;
            else if (y >= n - radius)
                dy = y - (n - radius - 1);
            return dx * dx + dy * dy <= radius * radius;
        }

        static Sprite SpriteFrom(Color32[] pixels, int size, Vector4 border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size,
                0,
                SpriteMeshType.FullRect,
                border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static void DrawWorld(Color32[] pixels, int n)
        {
            StrokeCircle(pixels, n, 16, 16, 11, 2);
            StrokeLine(pixels, n, 6, 16, 26, 16, 2);
            StrokeLine(pixels, n, 16, 6, 16, 26, 2);
        }

        static void DrawDiamond(Color32[] pixels, int n)
        {
            StrokeLine(pixels, n, 16, 5, 27, 16, 2);
            StrokeLine(pixels, n, 27, 16, 16, 27, 2);
            StrokeLine(pixels, n, 16, 27, 5, 16, 2);
            StrokeLine(pixels, n, 5, 16, 16, 5, 2);
        }

        static void DrawPlus(Color32[] pixels, int n)
        {
            StrokeLine(pixels, n, 16, 6, 16, 26, 3);
            StrokeLine(pixels, n, 6, 16, 26, 16, 3);
        }

        static void DrawMinus(Color32[] pixels, int n)
        {
            StrokeLine(pixels, n, 6, 16, 26, 16, 3);
        }

        static void DrawFrame(Color32[] pixels, int n)
        {
            StrokeLine(pixels, n, 7, 11, 7, 7, 2);
            StrokeLine(pixels, n, 7, 7, 12, 7, 2);
            StrokeLine(pixels, n, 20, 7, 25, 7, 2);
            StrokeLine(pixels, n, 25, 7, 25, 12, 2);
            StrokeLine(pixels, n, 25, 20, 25, 25, 2);
            StrokeLine(pixels, n, 25, 25, 20, 25, 2);
            StrokeLine(pixels, n, 12, 25, 7, 25, 2);
            StrokeLine(pixels, n, 7, 25, 7, 20, 2);
        }

        static void StrokeCircle(Color32[] pixels, int n, int cx, int cy, int radius, int width)
        {
            var outer = radius * radius;
            var inner = (radius - width) * (radius - width);
            for (var y = cy - radius - 1; y <= cy + radius + 1; y++)
            {
                for (var x = cx - radius - 1; x <= cx + radius + 1; x++)
                {
                    var d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (d <= outer && d >= inner)
                        Plot(pixels, n, x, y);
                }
            }
        }

        static void StrokeLine(Color32[] pixels, int n, int x0, int y0, int x1, int y1, int width)
        {
            var steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            if (steps < 1)
                steps = 1;
            var half = width * 0.5f;
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                var y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (var oy = -1; oy <= width; oy++)
                {
                    for (var ox = -1; ox <= width; ox++)
                    {
                        if (ox * ox + oy * oy <= half * half + 1.5f)
                            Plot(pixels, n, x + ox, y + oy);
                    }
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
