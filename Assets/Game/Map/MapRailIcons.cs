using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Nine rail silhouettes drawn once at 64px and shared. White pixels, tinted by the row.
    /// Every glyph stays inside the same 8px margin so the boxes match.
    /// </summary>
    public static class MapRailIcons
    {
        public const int Map = 0;
        public const int Truck = 1;
        public const int Clipboard = 2;
        public const int Warehouse = 3;
        public const int Chart = 4;
        public const int Road = 5;
        public const int Train = 6;
        public const int Ship = 7;
        public const int Plane = 8;
        public const int Count = 9;

        const int Size = 64;

        static Sprite[] _icons;

        public static Sprite Get(int kind)
        {
            if (_icons == null)
            {
                _icons = new Sprite[Count];
                _icons[Map] = Make(DrawMap);
                _icons[Truck] = Make(DrawTruck);
                _icons[Clipboard] = Make(DrawClipboard);
                _icons[Warehouse] = Make(DrawWarehouse);
                _icons[Chart] = Make(DrawChart);
                _icons[Road] = Make(DrawRoad);
                _icons[Train] = Make(DrawTrain);
                _icons[Ship] = Make(DrawShip);
                _icons[Plane] = Make(DrawPlane);
            }

            if (kind < 0 || kind >= _icons.Length)
                return _icons[Map];
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

        static void DrawMap(Color32[] pixels, int n)
        {
            FillRect(pixels, n, 10, 8, 44, 48);
            ClearRect(pixels, n, 15, 13, 34, 38);
            FillCircle(pixels, n, 32, 38, 9);
            for (var y = 20; y <= 34; y++)
            {
                var half = Mathf.Max(2, (34 - y) * 8 / 14);
                FillRect(pixels, n, 32 - half, y, half * 2, 1);
            }
        }

        static void DrawTruck(Color32[] pixels, int n)
        {
            FillRect(pixels, n, 6, 25, 32, 15);
            FillRect(pixels, n, 38, 21, 18, 19);
            ClearRect(pixels, n, 42, 25, 10, 8);
            FillRect(pixels, n, 38, 36, 18, 4);
            FillCircle(pixels, n, 16, 19, 7);
            FillCircle(pixels, n, 46, 19, 7);
            ClearCircle(pixels, n, 16, 19, 3);
            ClearCircle(pixels, n, 46, 19, 3);
            FillRect(pixels, n, 4, 28, 3, 5);
        }

        static void DrawClipboard(Color32[] pixels, int n)
        {
            FillRect(pixels, n, 16, 10, 32, 40);
            FillRect(pixels, n, 24, 46, 16, 8);
            ClearRect(pixels, n, 28, 48, 8, 4);
            ClearRect(pixels, n, 22, 36, 20, 3);
            ClearRect(pixels, n, 22, 28, 20, 3);
            ClearRect(pixels, n, 22, 20, 20, 3);
        }

        static void DrawWarehouse(Color32[] pixels, int n)
        {
            FillRect(pixels, n, 12, 10, 40, 28);
            for (var i = 0; i < 18; i++)
                FillRect(pixels, n, 12 + i, 38 + i, 40 - i * 2, 1);
            ClearRect(pixels, n, 26, 10, 12, 16);
        }

        static void DrawChart(Color32[] pixels, int n)
        {
            FillRect(pixels, n, 12, 12, 10, 16);
            FillRect(pixels, n, 27, 12, 10, 26);
            FillRect(pixels, n, 42, 12, 10, 38);
        }

        static void DrawRoad(Color32[] pixels, int n)
        {
            FillRect(pixels, n, 18, 8, 28, 48);
            ClearRect(pixels, n, 30, 12, 4, 8);
            ClearRect(pixels, n, 30, 28, 4, 8);
            ClearRect(pixels, n, 30, 44, 4, 8);
        }

        static void DrawTrain(Color32[] pixels, int n)
        {
            FillRect(pixels, n, 7, 24, 22, 17);
            FillRect(pixels, n, 32, 24, 25, 17);
            FillRect(pixels, n, 13, 41, 7, 8);
            FillRect(pixels, n, 39, 41, 7, 8);
            FillRect(pixels, n, 15, 17, 8, 7);
            FillRect(pixels, n, 11, 12, 16, 5);
            ClearRect(pixels, n, 37, 29, 7, 5);
            ClearRect(pixels, n, 48, 29, 7, 5);
            FillRect(pixels, n, 7, 50, 50, 3);
            FillRect(pixels, n, 11, 54, 42, 2);
        }

        static void DrawShip(Color32[] pixels, int n)
        {
            FillRect(pixels, n, 8, 16, 48, 12);
            FillRect(pixels, n, 14, 12, 36, 6);
            FillRect(pixels, n, 22, 28, 20, 16);
            FillRect(pixels, n, 40, 36, 8, 12);
            ClearRect(pixels, n, 26, 32, 5, 6);
            ClearRect(pixels, n, 34, 32, 5, 6);
        }

        static void DrawPlane(Color32[] pixels, int n)
        {
            FillRect(pixels, n, 27, 6, 10, 52);
            FillRect(pixels, n, 10, 31, 44, 8);
            FillRect(pixels, n, 20, 13, 24, 6);
            FillRect(pixels, n, 22, 47, 20, 6);
            ClearRect(pixels, n, 30, 11, 4, 7);
            ClearRect(pixels, n, 30, 45, 4, 7);
        }

        static void FillRect(Color32[] pixels, int n, int x, int y, int width, int height)
        {
            for (var yy = y; yy < y + height; yy++)
            {
                for (var xx = x; xx < x + width; xx++)
                    Plot(pixels, n, xx, yy);
            }
        }

        static void ClearRect(Color32[] pixels, int n, int x, int y, int width, int height)
        {
            for (var yy = y; yy < y + height; yy++)
            {
                for (var xx = x; xx < x + width; xx++)
                {
                    if (xx < 0 || yy < 0 || xx >= n || yy >= n)
                        continue;
                    pixels[yy * n + xx] = new Color32(0, 0, 0, 0);
                }
            }
        }

        static void FillCircle(Color32[] pixels, int n, int cx, int cy, int radius)
        {
            var r2 = radius * radius;
            for (var y = cy - radius; y <= cy + radius; y++)
            {
                for (var x = cx - radius; x <= cx + radius; x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                        Plot(pixels, n, x, y);
                }
            }
        }

        static void ClearCircle(Color32[] pixels, int n, int cx, int cy, int radius)
        {
            var r2 = radius * radius;
            for (var y = cy - radius; y <= cy + radius; y++)
            {
                for (var x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || y < 0 || x >= n || y >= n)
                        continue;
                    var dx = x - cx;
                    var dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                        pixels[y * n + x] = new Color32(0, 0, 0, 0);
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
