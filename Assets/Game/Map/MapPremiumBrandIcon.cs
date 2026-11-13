using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// One cached 2D cube insignia for the LogistiCore brand.
    /// Generated from three flat polygons without external art dependencies.
    /// </summary>
    public static class MapPremiumBrandIcon
    {
        const int Size = 64;
        static Sprite _sprite;
        public static Sprite Get()
        {
            if (_sprite == null)
                _sprite = Build();
            return _sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { _sprite = null; }

        static Sprite Build()
        {
            var pixels = new Color32[Size * Size];
            // Top face, front-left face and front-right face.
            FillQuad(pixels, new Vector2(32, 5), new Vector2(57, 18),
                new Vector2(32, 32), new Vector2(7, 18), new Color32(32, 215, 249, 255));
            FillQuad(pixels, new Vector2(7, 21), new Vector2(30, 34),
                new Vector2(30, 60), new Vector2(7, 47), new Color32(21, 130, 221, 255));
            FillQuad(pixels, new Vector2(34, 34), new Vector2(57, 21),
                new Vector2(57, 47), new Vector2(34, 60), new Color32(15, 176, 223, 255));
            // Interior seams add dimensionality without expensive geometry.
            FillQuad(pixels, new Vector2(31, 33), new Vector2(33, 33),
                new Vector2(33, 58), new Vector2(31, 58), new Color32(7, 41, 71, 255));
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size),
                new Vector2(0.5f, 0.5f), Size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static void FillQuad(Color32[] pixels, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (Inside(p, a, b, c) || Inside(p, a, c, d))
                        pixels[y * Size + x] = color;
                }
            }
        }

        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float s1 = Cross(p, a, b);
            float s2 = Cross(p, b, c);
            float s3 = Cross(p, c, a);
            bool negative = s1 < 0 || s2 < 0 || s3 < 0;
            bool positive = s1 > 0 || s2 > 0 || s3 > 0;
            return !(negative && positive);
        }

        static float Cross(Vector2 p, Vector2 a, Vector2 b)
        {
            return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        }
    }
}
