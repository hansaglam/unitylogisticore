using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace LogistiCore.Map
{
    /// <summary>
    /// Zoom windows and collision for sea and ocean names. Zoom follows the same
    /// log2 orthographic scale as country labels, but it does not use the Türkiye
    /// city-label handoff. Seas around Türkiye must remain eligible at the Türkiye
    /// fit. Water names yield to country names, city plates, and the HUD viewport.
    /// </summary>
    public static class MapWaterLabels
    {
        public const int Capacity = 12;
        public const float WorldZoom = 2f;
        public const float GlyphFactor = 0.70f;
        public const float LineFactor = 1.08f;
        public const float LabelPad = 6f;
        public const float FontScale = 0.88f;
        public const float BlockerPad = 6f;
        public const float ViewportInset = 10f;

        public enum Reject : byte
        {
            Shown = 0,
            Scope = 1,
            Zoom = 2,
            Offscreen = 3,
            Viewport = 4,
            Blocker = 5,
            Occupied = 6,
            Unnamed = 7
        }

        static Reject[] _reasons = new Reject[32];

        public static Reject ReasonOf(int index)
        {
            if (index < 0 || index >= _reasons.Length)
                return Reject.Offscreen;
            return _reasons[index];
        }

        public static float Zoom(float orthographicSize, float worldSize)
        {
            if (orthographicSize < 0.0001f || worldSize < 0.0001f)
                return 0f;

            return WorldZoom + Mathf.Log(worldSize / orthographicSize, 2f);
        }

        public static float HoldZoom(float previousZoom, float previousSize, float size, float rawZoom)
        {
            return MapCountryLabels.HoldZoom(previousZoom, previousSize, size, rawZoom);
        }

        public static bool Shows(in MapWaterCatalog.Entry entry, float zoom, MapScope scope)
        {
            if (!MapWaterCatalog.InScope(entry, scope))
                return false;
            if (zoom + 0.0001f < entry.MinZoom)
                return false;
            return zoom <= entry.MaxZoom + 0.0001f;
        }

        public static string Name(in MapWaterCatalog.Entry entry, string locale)
        {
            if (string.IsNullOrEmpty(entry.Key))
                return string.Empty;

            var database = LocalizationSettings.StringDatabase;
            var available = LocalizationSettings.AvailableLocales;
            if (database == null || available == null)
                return string.Empty;

            var identifier = new LocaleIdentifier(LocaleCodes.NormalizeSelection(locale));
            var selected = available.GetLocale(identifier);
            var table = selected != null ? database.GetTable(MapContent.Table, selected) as StringTable : null;

            if (table == null)
                return string.Empty;

            var item = table.GetEntry(entry.Key);
            if (item == null)
                return string.Empty;

            var text = item.GetLocalizedString();
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
        }

        public static float Width(string name, float glyph)
        {
            if (string.IsNullOrEmpty(name) || glyph < 0.01f)
                return 8f;

            return name.Length * glyph + LabelPad;
        }

        public static int Choose(
            float zoom,
            MapScope scope,
            Vector2[] screen,
            string[] names,
            float glyph,
            float[] measured,
            float line,
            Rect viewport,
            Rect[] blockers,
            int blockerCount,
            int[] chosen,
            Rect[] occupied)
        {
            if (chosen == null || occupied == null)
                return 0;

            var waters = MapWaterCatalog.All;
            if (_reasons.Length < waters.Length)
                _reasons = new Reject[waters.Length];
            for (var i = 0; i < waters.Length; i++)
                _reasons[i] = Reject.Scope;

            var count = 0;
            var limit = chosen.Length < occupied.Length ? chosen.Length : occupied.Length;
            if (limit > Capacity)
                limit = Capacity;

            for (var rank = 0; rank <= 3 && count < limit; rank++)
            {
                for (var i = 0; i < waters.Length && count < limit; i++)
                {
                    var entry = waters[i];
                    if (entry.Rank != rank)
                        continue;
                    if (!Shows(entry, zoom, scope))
                    {
                        _reasons[i] = MapWaterCatalog.InScope(entry, scope) ? Reject.Zoom : Reject.Scope;
                        continue;
                    }
                    if (screen == null || i >= screen.Length)
                    {
                        _reasons[i] = Reject.Offscreen;
                        continue;
                    }

                    var hole = Inset(viewport, ViewportInset);
                    var point = screen[i];
                    if (float.IsNaN(point.x) || float.IsNaN(point.y) || !hole.Contains(point))
                    {
                        _reasons[i] = Reject.Offscreen;
                        continue;
                    }

                    var name = names != null && i < names.Length ? names[i] : null;
                    if (string.IsNullOrEmpty(name))
                    {
                        _reasons[i] = Reject.Unnamed;
                        continue;
                    }

                    var width = measured != null && i < measured.Length && measured[i] > 1f
                        ? measured[i]
                        : Width(name, glyph);
                    var bounds = MapCountryLabels.LabelRect(point, width, line);
                    if (!TryFitViewport(ref bounds, hole, line * 2.4f))
                    {
                        _reasons[i] = Reject.Viewport;
                        continue;
                    }
                    if (Hits(bounds, occupied, count))
                    {
                        _reasons[i] = Reject.Occupied;
                        continue;
                    }
                    if (Hits(bounds, blockers, blockerCount, entry.Rank == 0 ? BlockerPad : 0f))
                    {
                        _reasons[i] = Reject.Blocker;
                        continue;
                    }

                    chosen[count] = i;
                    occupied[count] = bounds;
                    _reasons[i] = Reject.Shown;
                    count++;
                }
            }

            return count;
        }

        static Rect Inset(Rect viewport, float inset)
        {
            var pad = inset < 0f ? 0f : inset;
            var width = viewport.width - pad * 2f;
            if (width < 8f)
                return viewport;
            return new Rect(viewport.xMin + pad, viewport.yMin, width, viewport.height);
        }

        static bool TryFitViewport(ref Rect bounds, Rect viewport, float maxNudge)
        {
            var dx = 0f;
            var dy = 0f;
            if (bounds.xMin < viewport.xMin)
                dx = viewport.xMin - bounds.xMin;
            else if (bounds.xMax > viewport.xMax)
                dx = viewport.xMax - bounds.xMax;
            if (bounds.yMin < viewport.yMin)
                dy = viewport.yMin - bounds.yMin;
            else if (bounds.yMax > viewport.yMax)
                dy = viewport.yMax - bounds.yMax;
            if (dx * dx + dy * dy > maxNudge * maxNudge)
                return false;

            bounds.x += dx;
            bounds.y += dy;
            return bounds.xMin >= viewport.xMin - 0.01f && bounds.xMax <= viewport.xMax + 0.01f
                && bounds.yMin >= viewport.yMin - 0.01f && bounds.yMax <= viewport.yMax + 0.01f;
        }

        static bool Hits(Rect bounds, Rect[] rects, int count)
        {
            return Hits(bounds, rects, count, 0f);
        }

        static bool Hits(Rect bounds, Rect[] rects, int count, float pad)
        {
            if (rects == null)
                return false;

            var limit = count < rects.Length ? count : rects.Length;
            for (var i = 0; i < limit; i++)
            {
                var other = rects[i];
                if (pad > 0.01f)
                    other = new Rect(other.xMin - pad, other.yMin - pad, other.width + pad * 2f, other.height + pad * 2f);
                if (MapCountryLabels.Overlaps(bounds, other))
                    return true;
            }

            return false;
        }
    }
}
