using UnityEngine;

namespace LogistiCore.Depot
{
    /// <summary>
    /// Small deterministic material-study atlas for the authored 2.5D yard.
    /// Five 64px monochrome tiles (asphalt, concrete, roof, siding, plain).
    /// Multiplying by existing mesh vertex colors keeps the locked palette.
    /// This is a replaceable prototype material, not final licensed artwork.
    /// </summary>
    public static class DepotSurfacePatternAtlas
    {
        public const int TileSize = 64;
        public const int TileCount = 5;
        public const int Width = TileSize * TileCount;
        public const int Height = TileSize;
        public const int Asphalt = 0;
        public const int Concrete = 1;
        public const int Roof = 2;
        public const int Siding = 3;
        public const int Plain = 4;

        public static Texture2D Create()
        {
            var texture = new Texture2D(Width, Height,
                TextureFormat.RGBA32, false, false)
            {
                name = "Depot_MaterialStudy_01",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[Width * Height];
            for (var tile = 0; tile < TileCount; tile++)
            {
                for (var y = 0; y < TileSize; y++)
                {
                    for (var x = 0; x < TileSize; x++)
                    {
                        // Material swatches intentionally avoid RNG or high-
                        // frequency photo noise that looks like AI filler.
                        var hash = ((x * 17 + y * 29 + x * y * 3) % 13) - 6;
                        int tone;
                        switch (tile)
                        {
                            case Asphalt:
                                tone = 236 + hash / 3;
                                break;
                            case Concrete:
                                tone = 242 + hash / 4;
                                if (x == 2 || y == 2) tone -= 10;
                                break;
                            case Roof:
                                tone = 244 - x / 20;
                                if (x == 3 || x == 4) tone -= 11;
                                break;
                            case Siding:
                                tone = 243;
                                if (x % 16 == 0) tone -= 13;
                                else if (x % 16 == 1) tone += 5;
                                break;
                            default:
                                tone = 255;
                                break;
                        }
                        var value = (byte)Mathf.Clamp(tone, 0, 255);
                        pixels[y * Width + tile * TileSize + x] =
                            new Color32(value, value, value, 255);
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        public static void UvRect(int tile, out Vector2 min, out Vector2 max)
        {
            var safe = Mathf.Clamp(tile, 0, TileCount - 1);
            // Half-pixel inset avoids bleeding between material swatches.
            min = new Vector2((safe * TileSize + 0.5f) / Width,
                0.5f / Height);
            max = new Vector2(((safe + 1) * TileSize - 0.5f) / Width,
                (Height - 0.5f) / Height);
        }
    }
}
