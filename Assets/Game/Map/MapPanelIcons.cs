using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Expand/collapse glyphs drawn once at 64px and shared. White pixels, tinted by the control.
    /// </summary>
    public static class MapPanelIcons
    {
        public const int Expand = 0;
        public const int Collapse = 1;
        public const int Close = 2;
        public const int Count = 3;
        const int Size = 64;

        static Sprite[] _icons;

        public static Sprite Get(int kind)
        {
            if (_icons == null)
            {
                _icons = new Sprite[Count];
                _icons[Expand] = Make(DrawExpand);
                _icons[Collapse] = Make(DrawCollapse);
                _icons[Close] = Make(DrawClose);
            }

            if (kind < 0 || kind >= _icons.Length)
                return _icons[Expand];
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

        static void DrawExpand(Color32[] pixels, int n)
        {
            Arm(pixels, n, 8, 52, 18, 4);
            Arm(pixels, n, 8, 38, 4, 18);
            Arm(pixels, n, 38, 52, 18, 4);
            Arm(pixels, n, 52, 38, 4, 18);
            Arm(pixels, n, 8, 8, 18, 4);
            Arm(pixels, n, 8, 8, 4, 18);
            Arm(pixels, n, 38, 8, 18, 4);
            Arm(pixels, n, 52, 8, 4, 18);
        }

        static void DrawClose(Color32[] pixels, int n)
        {
            Stroke(pixels, n, 14, 14, 50, 50, 5);
            Stroke(pixels, n, 14, 50, 50, 14, 5);
        }

        static void Stroke(Color32[] pixels, int n, int x0, int y0, int x1, int y1, int thickness)
        {
            var steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            if (steps < 1)
                steps = 1;
            for (var i = 0; i <= steps; i++)
            {
                var x = x0 + (x1 - x0) * i / steps;
                var y = y0 + (y1 - y0) * i / steps;
                FillRect(pixels, n, x - thickness / 2, y - thickness / 2, thickness, thickness);
            }
        }

        static void DrawCollapse(Color32[] pixels, int n)
        {
            FillRect(pixels, n, 18, 42, 12, 4);
            FillRect(pixels, n, 18, 34, 4, 12);
            FillRect(pixels, n, 34, 42, 12, 4);
            FillRect(pixels, n, 42, 34, 4, 12);
            FillRect(pixels, n, 18, 18, 12, 4);
            FillRect(pixels, n, 18, 18, 4, 12);
            FillRect(pixels, n, 34, 18, 12, 4);
            FillRect(pixels, n, 42, 18, 4, 12);
        }

        static void Arm(Color32[] pixels, int n, int x, int y, int width, int height)
        {
            FillRect(pixels, n, x, y, width, height);
        }

        static void FillRect(Color32[] pixels, int n, int x, int y, int width, int height)
        {
            for (var yy = y; yy < y + height; yy++)
            {
                for (var xx = x; xx < x + width; xx++)
                {
                    if (xx < 0 || yy < 0 || xx >= n || yy >= n)
                        continue;
                    pixels[yy * n + xx] = new Color32(255, 255, 255, 255);
                }
            }
        }
    }
}
