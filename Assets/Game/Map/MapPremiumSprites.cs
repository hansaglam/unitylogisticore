using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Procedural white rounded 9-slice surface. Generated once, tinted by uGUI Image.
    /// Avoids new external textures and has no per-frame allocations.
    /// </summary>
    public static class MapPremiumSprites
    {
        const int Size = 48;
        const float Radius = 11f;
        const int Slice = 13;
        static Sprite _rounded;

        public static Sprite Rounded
        {
            get
            {
                if (_rounded == null)
                    _rounded = Build();
                return _rounded;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _rounded = null;
        }

        static Sprite Build()
        {
            var pixels = new Color32[Size * Size];
            float half = Size * 0.5f;
            float inside = half - Radius;
            for (int y = 0; y < Size; y++)
            {
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inside, 0f);
                for (int x = 0; x < Size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inside, 0f);
                    float signedDistance = Mathf.Sqrt(dx * dx + dy * dy) - Radius;
                    byte opacity = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(0.5f - signedDistance));
                    pixels[y * Size + x] = new Color32(255, 255, 255, opacity);
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(
                texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f),
                Size, 0, SpriteMeshType.FullRect, new Vector4(Slice, Slice, Slice, Slice));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
