using System.Collections.Generic;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Lightweight, non-photographic city atmosphere for the selected-city card.
    /// All cities reuse one procedural 320x128 art style; individual silhouettes
    /// are generated lazily, cached and never updated per frame.
    /// No city art implies route availability, owned facilities or live economy.
    /// </summary>
    public static class MapPremiumCityArt
    {
        const int Width = 320;
        const int Height = 128;
        const int MaxCachedCities = 24; // ~4 MiB of raw pixel textures on mobile.
        static readonly Dictionary<string, Sprite> CityBanners = new Dictionary<string, Sprite>();
        static readonly Queue<string> CacheOrder = new Queue<string>();
        static Sprite _banner;

        // Retained for existing callers and as the safe unknown-city fallback.
        public static Sprite Banner
        {
            get
            {
                if (_banner == null)
                    _banner = Create(null);
                return _banner;
            }
        }

        // Compatible with the original eight Turkey city markers.
        public static Sprite ForCity(string cityId)
        {
            return ForWorldCity(cityId, null);
        }

        /// <summary>
        /// Future global city catalogs can provide their stable city id and
        /// ISO-3166 alpha-3 country code. Neither a marker nor a city record
        /// is fabricated here: the method creates presentation art only.
        /// </summary>
        public static Sprite ForWorldCity(string cityId, string countryCode)
        {
            if (!MapWorldCityAtmosphereResolver.TryResolve(cityId, countryCode, out var atmosphere))
                return Banner;

            var country = string.IsNullOrWhiteSpace(countryCode)
                ? (MapCityAtmosphereCatalog.TryGet(atmosphere.CityId, out _) ? "TUR" : "")
                : countryCode.Trim().ToUpperInvariant();
            var key = country + "|" + atmosphere.CityId;

            if (CityBanners.TryGetValue(key, out var sprite) && sprite != null)
                return sprite;

            // The map shows one city context at a time. Eviction keeps browsing
            // hundreds of cities from allocating hundreds of retained textures.
            if (CityBanners.Count >= MaxCachedCities && CacheOrder.Count > 0)
            {
                var discarded = CacheOrder.Dequeue();
                if (CityBanners.TryGetValue(discarded, out var previous))
                {
                    CityBanners.Remove(discarded);
                    Release(previous);
                }
            }

            sprite = Create(atmosphere);
            CityBanners[key] = sprite;
            CacheOrder.Enqueue(key);
            return sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            if (_banner != null)
                Release(_banner);
            _banner = null;
            foreach (var pair in CityBanners)
                Release(pair.Value);
            CityBanners.Clear();
            CacheOrder.Clear();
        }

        static void Release(Sprite sprite)
        {
            if (sprite == null)
                return;
            var texture = sprite.texture;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(sprite);
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static Sprite Create(MapCityAtmosphere? selected)
        {
            var pixels = new Color32[Width * Height];
            Color skyLow = selected.HasValue ? selected.Value.SkyLow : new Color32(5, 18, 36, 255);
            Color skyHigh = selected.HasValue ? selected.Value.SkyHigh : new Color32(14, 44, 70, 255);
            Color skyline = selected.HasValue ? selected.Value.Silhouette : new Color32(11, 40, 63, 255);
            Color accent = selected.HasValue ? selected.Value.Accent : new Color32(64, 168, 192, 255);
            int seed = selected.HasValue ? selected.Value.SkylineSeed : 7;

            for (var y = 0; y < Height; y++)
            {
                var t = y / (float)(Height - 1);
                for (var x = 0; x < Width; x++)
                {
                    var glow = Mathf.Clamp01(1f - Mathf.Abs(x - Width * .72f) / (Width * .57f))
                        * Mathf.Clamp01(1f - Mathf.Abs(y - Height * .49f) / (Height * .7f));
                    var color = Color.Lerp(skyLow, skyHigh, t);
                    color = Color.Lerp(color, accent, glow * .12f);
                    pixels[y * Width + x] = color;
                }
            }

            if (selected.HasValue)
                DrawTerrain(pixels, selected.Value);

            // Intentionally deterministic skyline: no visual changes each session.
            var building = (Color32)skyline;
            var windows = new Color32(53, 160, 182, 255);
            for (var i = 0; i < 19; i++)
            {
                var left = i * 19 - 5;
                var width = 11 + (i * 3 + seed) % 7;
                var height = 14 + (i * 29 + seed * 7) % 40;
                Rect(pixels, left, 7, width, height, building);
                for (var wx = left + 3; wx < left + width - 1; wx += 6)
                {
                    for (var wy = 15; wy < 5 + height; wy += 9)
                    {
                        if ((i + wx + wy + seed) % 4 == 0)
                            Rect(pixels, wx, wy, 2, 2, windows);
                    }
                }
            }

            if (selected.HasValue)
                DrawLandmark(pixels, selected.Value);

            Rect(pixels, 0, 10, Width, 1, selected.HasValue
                ? selected.Value.Accent : new Color32(29, 120, 149, 255));
            for (var i = 0; i < 7; i++)
            {
                var x = 15 + i * 45;
                var y = 84 + (i % 3) * 9;
                Rect(pixels, x, y, 2, 2, new Color32(88, 193, 213, 255));
            }

            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = selected.HasValue ? "CityAtmosphere_" + selected.Value.CityId : "CityAtmosphere_Generic",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height),
                new Vector2(.5f, .5f), Width);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static void DrawTerrain(Color32[] pixels, MapCityAtmosphere atmosphere)
        {
            var distant = new Color32(
                (byte)Mathf.Min(atmosphere.Silhouette.r + 20, 255),
                (byte)Mathf.Min(atmosphere.Silhouette.g + 24, 255),
                (byte)Mathf.Min(atmosphere.Silhouette.b + 26, 255), 255);

            if (atmosphere.Horizon == MapCityHorizon.Coast)
            {
                Rect(pixels, 0, 6, Width, 9, distant);
                Rect(pixels, 0, 16, Width, 1, atmosphere.Accent);
                return;
            }

            if (atmosphere.Horizon == MapCityHorizon.Hills)
            {
                for (var x = 0; x < Width; x++)
                {
                    var ridge = 27 + Mathf.RoundToInt(9f * Mathf.Sin(x * .023f + atmosphere.SkylineSeed)
                        + 11f * Mathf.Sin(x * .063f));
                    Rect(pixels, x, 7, 1, ridge, distant);
                }
            }
        }

        static void DrawLandmark(Color32[] pixels, MapCityAtmosphere atmosphere)
        {
            var shape = atmosphere.Accent;
            var dark = atmosphere.Silhouette;
            const int x = 249;

            switch (atmosphere.Landmark)
            {
                case MapCityLandmark.Tower: // Ankara-inspired observation tower.
                    Rect(pixels, x - 3, 25, 6, 67, shape);
                    Disc(pixels, x, 91, 13, shape);
                    Rect(pixels, x - 15, 88, 30, 5, dark);
                    Rect(pixels, x - 1, 103, 2, 14, shape);
                    break;
                case MapCityLandmark.HistoricTower: // Historic tower silhouette.
                    Rect(pixels, x - 10, 23, 20, 65, shape);
                    Rect(pixels, x - 13, 80, 26, 5, dark);
                    Roof(pixels, x, 89, 16, 17, shape);
                    Rect(pixels, x - 2, 106, 4, 8, shape);
                    break;
                case MapCityLandmark.ClockTower: // Coastal clock tower.
                    Rect(pixels, x - 7, 25, 14, 57, shape);
                    Rect(pixels, x - 11, 34, 22, 6, dark);
                    Disc(pixels, x, 70, 6, dark);
                    Disc(pixels, x, 70, 3, shape);
                    Roof(pixels, x, 83, 12, 15, shape);
                    break;
                case MapCityLandmark.Mountain: // Distant highland ridge.
                    Roof(pixels, x - 14, 27, 70, 62, shape);
                    Roof(pixels, x + 27, 20, 43, 47, dark);
                    break;
                case MapCityLandmark.Minaret: // Old-town minaret.
                    Rect(pixels, x - 6, 24, 12, 66, shape);
                    Rect(pixels, x - 10, 73, 20, 5, dark);
                    Roof(pixels, x, 91, 9, 22, shape);
                    Rect(pixels, x - 2, 112, 4, 5, shape);
                    break;
                case MapCityLandmark.BridgeAndDome: // Bridge + domed silhouette.
                    Rect(pixels, x - 38, 29, 78, 5, shape);
                    for (var i = -28; i <= 27; i += 20)
                        Rect(pixels, x + i, 12, 4, 17, shape);
                    Rect(pixels, x - 18, 35, 36, 25, dark);
                    Disc(pixels, x, 60, 18, shape);
                    break;
                case MapCityLandmark.CityWalls: // Historic walls.
                    Rect(pixels, x - 47, 25, 95, 27, shape);
                    for (var i = -45; i < 44; i += 14)
                        Rect(pixels, x + i, 52, 8, 10, shape);
                    Rect(pixels, x - 4, 25, 8, 17, dark);
                    break;
                case MapCityLandmark.CoastalMountains: // Black Sea hills.
                    Roof(pixels, x - 21, 19, 66, 70, shape);
                    Roof(pixels, x + 34, 16, 46, 57, dark);
                    Rect(pixels, x - 50, 12, 100, 5, shape);
                    break;
                case MapCityLandmark.LatticeTower: // Stylized lattice observation tower.
                    for (var level = 0; level < 76; level++)
                    {
                        var half = Mathf.Max(2, Mathf.RoundToInt((76 - level) * .24f));
                        Rect(pixels, x - half, 23 + level, 3, 1, shape);
                        Rect(pixels, x + half - 2, 23 + level, 3, 1, shape);
                        if (level % 9 == 0)
                            Rect(pixels, x - half, 23 + level, half * 2 + 1, 2, shape);
                    }
                    Rect(pixels, x - 2, 98, 4, 18, shape);
                    break;
                case MapCityLandmark.NeedleTower: // Tall broadcast tower.
                    Rect(pixels, x - 3, 20, 6, 76, shape);
                    Rect(pixels, x - 12, 77, 24, 6, dark);
                    Rect(pixels, x - 8, 88, 16, 4, shape);
                    Rect(pixels, x - 2, 96, 4, 24, shape);
                    break;
                case MapCityLandmark.SailRoof: // Sydney-inspired layered sail roof.
                    Rect(pixels, x - 48, 24, 92, 8, dark);
                    Roof(pixels, x - 23, 32, 28, 42, shape);
                    Roof(pixels, x + 9, 32, 24, 56, shape);
                    Roof(pixels, x + 30, 32, 16, 35, dark);
                    break;
                case MapCityLandmark.Skyscrapers: // Dense tall-city profile.
                    Rect(pixels, x - 40, 22, 20, 59, shape);
                    Rect(pixels, x - 17, 22, 20, 94, dark);
                    Rect(pixels, x + 8, 22, 25, 77, shape);
                    Rect(pixels, x + 36, 22, 12, 52, dark);
                    break;
                case MapCityLandmark.Pyramids: // Stylized heritage silhouettes.
                    Roof(pixels, x - 29, 23, 35, 40, shape);
                    Roof(pixels, x + 26, 23, 30, 32, dark);
                    break;
            }
        }

        static void Roof(Color32[] pixels, int centerX, int bottomY, int halfWidth, int height, Color32 color)
        {
            for (var y = 0; y < height; y++)
            {
                var half = Mathf.RoundToInt(halfWidth * (1f - y / (float)height));
                Rect(pixels, centerX - half, bottomY + y, half * 2 + 1, 1, color);
            }
        }

        static void Disc(Color32[] pixels, int centerX, int centerY, int radius, Color32 color)
        {
            for (var y = -radius; y <= radius; y++)
            {
                var half = Mathf.FloorToInt(Mathf.Sqrt(radius * radius - y * y));
                Rect(pixels, centerX - half, centerY + y, 2 * half + 1, 1, color);
            }
        }

        static void Rect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            for (var py = Mathf.Max(y, 0); py < Mathf.Min(y + height, Height); py++)
            {
                for (var px = Mathf.Max(x, 0); px < Mathf.Min(x + width, Width); px++)
                    pixels[py * Width + px] = color;
            }
        }
    }
}
