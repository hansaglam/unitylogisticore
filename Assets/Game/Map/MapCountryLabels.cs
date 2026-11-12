using LogistiCore.Localization;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Chooses which Natural Earth country names are readable at the current camera.
    /// The world presentation maps to Natural Earth label zoom 2, the zoom where
    /// Türkiye's authored MIN_LABEL begins. Each step of orthographic zoom follows
    /// log2, the same progression Natural Earth uses. Türkiye city scale hides every
    /// country name so the eight city labels keep the frame.
    /// Anchors stay on LABEL_X/LABEL_Y unless a short nudge is needed to clear a
    /// visible city marker. The nudge is capped in world units so the name cannot
    /// jump to another country. If that cap is not enough, the name is hidden.
    /// </summary>
    public static class MapCountryLabels
    {
        public const int Capacity = 40;
        public const float WorldZoom = 2f;
        public const float CityLabelHandoff = 1.35f;
        public const float HoldFraction = 0.04f;
        public const float GlyphFactor = 0.74f;
        public const float LineFactor = 1.15f;
        public const float LabelPad = 8f;
        public const float CollisionGapX = 5f;
        public const float CollisionGapY = 4f;

        /// <summary>
        /// Hard clearance around the text rectangle, as a fraction of the line.
        /// Horizontal air stays generous. Vertical air is one line when two boxes
        /// just touch (two times this factor). The old 1.50 factor demanded three
        /// lines and hid France, Spain and the United Kingdom even though their
        /// text rectangles were already separated. Phone type still eases toward
        /// the grown factors, which stay large enough to hide names whose text
        /// rectangles nearly touch.
        /// </summary>
        public const float GapXFactor = 1.10f;
        public const float GapYFactor = 0.50f;

        /// <summary>
        /// Line height at the 1920×1080 reference: 16px canvas floor times LineFactor.
        /// Larger phone type eases toward the grown factors so the box stays about
        /// one line clear of the next name instead of swallowing the neighbour.
        /// </summary>
        public const float ReferenceLine = 18.4f;
        public const float GrownGapXFactor = 0.55f;
        public const float GrownGapYFactor = 0.72f;

        /// <summary>
        /// World zoom stays on the fixed gap so the major names keep their places.
        /// Regional frames, which are where the stacked names appear, use the
        /// line-height gap.
        /// </summary>
        public const float RegionalGapZoom = 2.05f;
        public const float MaxWidthFraction = 0.5f;
        public const float MarkerPadding = 6f;
        public const float AnchorReturnPadding = 4f;

        /// <summary>
        /// Largest shift from the Natural Earth anchor, in Web Mercator world units.
        /// About 1.6 degrees. At world scale that is only a few screen pixels, so a
        /// name that sits on the city cluster is hidden instead of being dragged
        /// onto a neighbouring country.
        /// </summary>
        public const float MaxNudgeWorld = 2.8f;

        /// <summary>
        /// Largest extra screen shift used when a centered regional name overlaps
        /// a name already kept. About two degrees. The shifted point must stay
        /// strictly closer to its own anchor than to every other country anchor,
        /// so the name cannot cross onto another country's label.
        /// </summary>
        public const float MaxPresentationWorld = 5.2f;
        public const float PresentationSeparation = 0.75f;

        /// <summary>
        /// Gap between the 34px aggregate badge and the ink of its country caption.
        /// The caption sits outside the 22px tap radius.
        /// </summary>
        public const float AggregateCaptionGap = 8f;

        static readonly Vector2[] NudgeDirections =
        {
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(0f, -1f),
            new Vector2(-1f, 0f),
            new Vector2(0.70710678f, 0.70710678f),
            new Vector2(0.70710678f, -0.70710678f),
            new Vector2(-0.70710678f, -0.70710678f),
            new Vector2(-0.70710678f, 0.70710678f)
        };

        static readonly Vector2[] CaptionSides =
        {
            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, -1f)
        };

        const int EscapeCap = 24;
        static readonly Vector2[] _escape = new Vector2[EscapeCap];
        static readonly float[] _escapeDist = new float[EscapeCap];
        static int[] _order;
        static Vector2[] _cityScreen;
        static Vector2[] _cityWorld;
        static int _turkey = -2;

        public static string Name(in MapCountryCatalog.Entry entry, string locale)
        {
            var code = LocaleCodes.NormalizeSelection(locale);
            if (code == LocaleCodes.Turkish && !string.IsNullOrEmpty(entry.Turkish))
                return entry.Turkish;
            if (code == LocaleCodes.German && !string.IsNullOrEmpty(entry.German))
                return entry.German;
            return string.IsNullOrEmpty(entry.English) ? entry.Code : entry.English;
        }

        public static float Zoom(float orthographicSize, float worldSize, float turkeyFrame)
        {
            if (orthographicSize <= 0.0001f)
                return -1f;
            if (turkeyFrame > 0.0001f && orthographicSize <= turkeyFrame * CityLabelHandoff)
                return -1f;

            var span = worldSize > 0.0001f ? worldSize : orthographicSize;
            var ratio = orthographicSize / span;
            if (ratio > 1f)
                ratio = 1f;
            if (ratio < 0.0001f)
                ratio = 0.0001f;
            return WorldZoom + Mathf.Log(1f / ratio, 2f);
        }

        public static float HoldZoom(float previousZoom, float previousSize, float size, float rawZoom)
        {
            if (rawZoom < 0f || previousZoom < 0f || previousSize <= 0.0001f)
                return rawZoom;

            var delta = Mathf.Abs(size - previousSize) / previousSize;
            if (delta < HoldFraction)
                return previousZoom;
            return rawZoom;
        }

        public static bool Shows(in MapCountryCatalog.Entry entry, float zoom)
        {
            return zoom >= 0f && zoom + 0.0001f >= entry.MinLabel && zoom <= entry.MaxLabel + 0.0001f;
        }

        public static int CountShown(float zoom)
        {
            var count = 0;
            var countries = MapCountryCatalog.All;
            for (var i = 0; i < countries.Length; i++)
            {
                if (Shows(countries[i], zoom))
                    count++;
            }

            return count;
        }

        public static float Width(string name, float glyphPixels)
        {
            var glyphs = glyphPixels > 0.01f ? glyphPixels : 1f;
            var chars = string.IsNullOrEmpty(name) ? 0 : name.Length;
            return chars * glyphs + LabelPad;
        }

        public struct Selection
        {
            public int Candidates;
            public int Visible;
            public int InRegion;
            public int OutOfRegion;
            public int RejectedOverlap;
            public int Shifted;
        }

        public struct Effort
        {
            public int Blocked;
            public int CandidateAttempts;
            public int AnchorComparisons;
        }

        public static Effort LastEffort { get; private set; }
        static int _blocked;
        static int _candidateAttempts;
        static int _anchorComparisons;

        public static Selection LastSelection { get; private set; }

        public static bool TryScopeContinent(MapScope scope, out MapCountryCatalog.MapContinent continent)
        {
            switch (scope)
            {
                case MapScope.Europe:
                    continent = MapCountryCatalog.MapContinent.Europe;
                    return true;
                case MapScope.Asia:
                    continent = MapCountryCatalog.MapContinent.Asia;
                    return true;
                case MapScope.Africa:
                    continent = MapCountryCatalog.MapContinent.Africa;
                    return true;
                case MapScope.NorthAmerica:
                    continent = MapCountryCatalog.MapContinent.NorthAmerica;
                    return true;
                case MapScope.SouthAmerica:
                    continent = MapCountryCatalog.MapContinent.SouthAmerica;
                    return true;
                case MapScope.Oceania:
                    continent = MapCountryCatalog.MapContinent.Oceania;
                    return true;
                default:
                    continent = MapCountryCatalog.MapContinent.None;
                    return false;
            }
        }

        public static int Choose(
            float zoom,
            string locale,
            Vector2[] screenPoints,
            float glyphPixels,
            float[] measuredWidths,
            float lineHeight,
            Rect viewport,
            int[] chosen,
            Rect[] occupied,
            MapScope scope = MapScope.World,
            float maxShiftPixels = 0f,
            Vector2[] slotShift = null,
            Vector2[] heldShift = null)
        {
            if (chosen == null || chosen.Length == 0 || occupied == null || occupied.Length < chosen.Length)
            {
                LastSelection = default;
                return 0;
            }

            if (zoom < 0f || screenPoints == null || viewport.width < 32f || viewport.height < 32f)
            {
                LastSelection = default;
                return 0;
            }

            EnsureOrder();
            var countries = MapCountryCatalog.All;
            var limit = chosen.Length < Capacity ? chosen.Length : Capacity;
            var height = lineHeight > 1f ? lineHeight : 1f;
            var gapX = GapX(height, zoom);
            var gapY = GapY(height, zoom);
            var maxWidth = viewport.width * MaxWidthFraction;
            var hasRegion = TryScopeContinent(scope, out var region);
            var passes = hasRegion ? 2 : 1;
            var count = 0;
            var candidates = 0;
            var rejected = 0;
            var inRegion = 0;
            var outRegion = 0;
            var shifted = 0;
            var allowShift = maxShiftPixels > 1f;
            _blocked = 0;
            _candidateAttempts = 0;
            _anchorComparisons = 0;
            for (var pass = 0; pass < passes; pass++)
            {
                for (var n = 0; n < _order.Length; n++)
                {
                    var index = _order[n];
                    if (index < 0 || index >= countries.Length || index >= screenPoints.Length)
                        continue;
                    if (!Shows(countries[index], zoom))
                        continue;

                    var inside = countries[index].Continent == region;
                    if (hasRegion && pass == 0 && !inside)
                        continue;
                    if (hasRegion && pass == 1 && inside)
                        continue;

                    var name = Name(countries[index], locale);
                    var width = measuredWidths != null && index < measuredWidths.Length && measuredWidths[index] > 1f
                        ? measuredWidths[index]
                        : Width(name, glyphPixels);
                    if (width > maxWidth)
                        continue;

                    var screen = screenPoints[index];
                    if (!float.IsFinite(screen.x) || !float.IsFinite(screen.y))
                        continue;

                    var rect = Inflated(screen, width, height, gapX, gapY);
                    if (!Inside(viewport, rect))
                        continue;

                    candidates++;
                    var point = screen;
                    var shift = Vector2.zero;
                    var blocked = Hits(rect, occupied, count);
                    if (blocked && allowShift && TryPresentation(
                            screen,
                            width,
                            height,
                            gapX,
                            gapY,
                            viewport,
                            occupied,
                            count,
                            screenPoints,
                            index,
                            maxShiftPixels,
                            heldShift,
                            out point))
                    {
                        shift = point - screen;
                        rect = Inflated(point, width, height, gapX, gapY);
                        blocked = false;
                        if (shift.sqrMagnitude > 0.25f)
                            shifted++;
                    }

                    if (blocked)
                    {
                        _blocked++;
                        if (heldShift != null && index < heldShift.Length)
                            heldShift[index] = Vector2.zero;
                        rejected++;
                        continue;
                    }

                    chosen[count] = index;
                    occupied[count] = rect;
                    if (slotShift != null && count < slotShift.Length)
                        slotShift[count] = shift;
                    if (heldShift != null && index < heldShift.Length)
                        heldShift[index] = shift;
                    count++;
                    if (hasRegion && inside)
                        inRegion++;
                    else if (hasRegion)
                        outRegion++;
                    if (count >= limit)
                        break;
                }

                if (count >= limit)
                    break;
            }

            LastSelection = new Selection
            {
                Candidates = candidates,
                Visible = count,
                InRegion = hasRegion ? inRegion : count,
                OutOfRegion = hasRegion ? outRegion : 0,
                RejectedOverlap = rejected,
                Shifted = shifted
            };
            LastEffort = new Effort
            {
                Blocked = _blocked,
                CandidateAttempts = _candidateAttempts,
                AnchorComparisons = _anchorComparisons
            };
            return count;
        }

        public static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
        }

        public static bool CloserToOwnAnchor(Vector2 point, Vector2 own, Vector2[] anchors, int ownIndex)
        {
            var ownDist = (point - own).sqrMagnitude;
            if (ownDist <= 0.01f || anchors == null)
                return true;

            var countries = MapCountryCatalog.All;
            var limit = anchors.Length < countries.Length ? anchors.Length : countries.Length;
            for (var i = 0; i < limit; i++)
            {
                if (i == ownIndex)
                    continue;

                var other = anchors[i];
                if (!float.IsFinite(other.x) || !float.IsFinite(other.y))
                    continue;
                _anchorComparisons++;
                if ((point - other).sqrMagnitude <= ownDist)
                    return false;
            }

            return true;
        }

        static Rect Inflated(Vector2 center, float width, float height, float gapX, float gapY)
        {
            return new Rect(
                center.x - width * 0.5f - gapX,
                center.y - height * 0.5f - gapY,
                width + gapX * 2f,
                height + gapY * 2f);
        }

        static bool Inside(Rect viewport, Rect rect)
        {
            return rect.xMin >= viewport.xMin && rect.yMin >= viewport.yMin && rect.xMax <= viewport.xMax && rect.yMax <= viewport.yMax;
        }

        static bool Hits(Rect rect, Rect[] occupied, int count)
        {
            for (var kept = 0; kept < count; kept++)
            {
                if (Overlaps(rect, occupied[kept]))
                    return true;
            }

            return false;
        }

        static bool TryPresentation(
            Vector2 anchor,
            float width,
            float height,
            float gapX,
            float gapY,
            Rect viewport,
            Rect[] occupied,
            int count,
            Vector2[] anchors,
            int index,
            float maxShift,
            Vector2[] heldShift,
            out Vector2 point)
        {
            point = anchor;
            if (heldShift != null && index < heldShift.Length)
            {
                var held = heldShift[index];
                if (held.sqrMagnitude > 0.25f && held.magnitude <= maxShift + 0.5f && FitsPresentation(
                        anchor + held,
                        anchor,
                        width,
                        height,
                        gapX,
                        gapY,
                        viewport,
                        occupied,
                        count,
                        anchors,
                        index,
                        maxShift))
                {
                    point = anchor + held;
                    return true;
                }
            }

            var centered = Inflated(anchor, width, height, gapX, gapY);
            var n = 0;
            for (var kept = 0; kept < count && n + 2 <= EscapeCap; kept++)
            {
                if (!Overlaps(centered, occupied[kept]))
                    continue;

                var overlapX = Mathf.Min(centered.xMax, occupied[kept].xMax) - Mathf.Max(centered.xMin, occupied[kept].xMin);
                var overlapY = Mathf.Min(centered.yMax, occupied[kept].yMax) - Mathf.Max(centered.yMin, occupied[kept].yMin);
                var awayX = centered.center.x >= occupied[kept].center.x ? 1f : -1f;
                var awayY = centered.center.y >= occupied[kept].center.y ? 1f : -1f;
                var horizontal = overlapX + PresentationSeparation;
                var vertical = overlapY + PresentationSeparation;
                if (n < EscapeCap)
                {
                    _escape[n] = anchor + new Vector2(awayX * horizontal, 0f);
                    _escapeDist[n] = horizontal;
                    n++;
                }

                if (n < EscapeCap)
                {
                    _escape[n] = anchor + new Vector2(0f, awayY * vertical);
                    _escapeDist[n] = vertical;
                    n++;
                }
            }

            for (var d = 0; d < NudgeDirections.Length && n < EscapeCap; d++)
            {
                _escape[n] = anchor + NudgeDirections[d] * maxShift;
                _escapeDist[n] = maxShift;
                n++;
            }

            for (var i = 1; i < n; i++)
            {
                var distance = _escapeDist[i];
                var candidate = _escape[i];
                var j = i - 1;
                while (j >= 0 && Farther(_escape[j], _escapeDist[j], candidate, distance))
                {
                    _escape[j + 1] = _escape[j];
                    _escapeDist[j + 1] = _escapeDist[j];
                    j--;
                }

                _escape[j + 1] = candidate;
                _escapeDist[j + 1] = distance;
            }

            for (var i = 0; i < n; i++)
            {
                _candidateAttempts++;
                if (!FitsPresentation(
                        _escape[i],
                        anchor,
                        width,
                        height,
                        gapX,
                        gapY,
                        viewport,
                        occupied,
                        count,
                        anchors,
                        index,
                        maxShift))
                    continue;

                point = _escape[i];
                return true;
            }

            return false;
        }

        static bool Farther(Vector2 left, float leftDist, Vector2 right, float rightDist)
        {
            if (leftDist > rightDist + 0.01f)
                return true;
            if (rightDist > leftDist + 0.01f)
                return false;
            if (left.x > right.x + 0.01f)
                return true;
            if (right.x > left.x + 0.01f)
                return false;
            return left.y > right.y;
        }

        static bool FitsPresentation(
            Vector2 point,
            Vector2 anchor,
            float width,
            float height,
            float gapX,
            float gapY,
            Rect viewport,
            Rect[] occupied,
            int count,
            Vector2[] anchors,
            int index,
            float maxShift)
        {
            var shift = point - anchor;
            if (shift.magnitude > maxShift + 0.05f)
                return false;

            var rect = Inflated(point, width, height, gapX, gapY);
            if (!Inside(viewport, rect) || Hits(rect, occupied, count))
                return false;

            return CloserToOwnAnchor(point, anchor, anchors, index);
        }

        public static float GapX(float lineHeight, float zoom)
        {
            return RegionalGap(lineHeight, zoom, GapXFactor, GrownGapXFactor, CollisionGapX);
        }

        public static float GapY(float lineHeight, float zoom)
        {
            return RegionalGap(lineHeight, zoom, GapYFactor, GrownGapYFactor, CollisionGapY);
        }

        static float RegionalGap(float lineHeight, float zoom, float authored, float grown, float floor)
        {
            var line = lineHeight > 1f ? lineHeight : 1f;
            if (zoom < RegionalGapZoom)
                return floor;

            var factor = authored;
            if (line > ReferenceLine)
            {
                var blend = (line - ReferenceLine) / ReferenceLine;
                if (blend > 1f)
                    blend = 1f;
                factor = authored + (grown - authored) * blend;
            }

            var scaled = line * factor;
            return scaled > floor ? scaled : floor;
        }

        public static Rect LabelRect(Vector2 center, float width, float lineHeight)
        {
            var height = lineHeight > 1f ? lineHeight : 1f;
            return new Rect(
                center.x - width * 0.5f - CollisionGapX,
                center.y - height * 0.5f - CollisionGapY,
                width + CollisionGapX * 2f,
                height + CollisionGapY * 2f);
        }

        public static int CityObstacles(
            Vector2 cameraCenter,
            float orthographicSize,
            float screenWidth,
            float screenHeight,
            float turkeyFrame,
            float canvasScale,
            bool[] together,
            int[] parent,
            Rect[] destination)
        {
            var cities = MapCities.All;
            if (destination == null || together == null || parent == null || cities.Length == 0)
                return 0;
            if (parent.Length < cities.Length || together.Length < cities.Length * cities.Length)
                return 0;

            var units = MapMarkerLod.UnitsPerPixel(orthographicSize, screenHeight);
            if (_cityScreen == null || _cityScreen.Length != cities.Length)
            {
                _cityScreen = new Vector2[cities.Length];
                _cityWorld = new Vector2[cities.Length];
            }

            var screen = _cityScreen;
            var world = _cityWorld;
            for (var i = 0; i < cities.Length; i++)
            {
                world[i] = MapProjection.Project(cities[i].Latitude, cities[i].Longitude);
                screen[i] = MapMarkerLod.ToScreen(world[i], cameraCenter, units, screenWidth, screenHeight);
            }

            var forceIndividuals = MapMarkerLod.AtCountryScale(orthographicSize, turkeyFrame);
            MapMarkerLod.Cluster(screen, cities.Length, forceIndividuals, together, parent);
            var plates = turkeyFrame > 0.0001f && orthographicSize <= turkeyFrame * CityLabelHandoff;
            var scale = canvasScale < 0.05f ? 1f : canvasScale;
            var font = MapMarkerLayer.LabelFontSize(scale, false);
            var plateWidth = font * scale * 8f;
            var plateHeight = (font + 10f) * scale;
            var view = new Rect(0f, 0f, screenWidth, screenHeight);
            var count = 0;
            for (var i = 0; i < cities.Length && count < destination.Length; i++)
            {
                if (parent[i] != i)
                    continue;

                var members = MapMarkerLod.ClusterSize(parent, cities.Length, i);
                Vector2 center;
                float diameter;
                if (members >= 2)
                {
                    var centroid = MapMarkerLod.Centroid(world, parent, cities.Length, i);
                    center = MapMarkerLod.ToScreen(centroid, cameraCenter, units, screenWidth, screenHeight);
                    diameter = MapMarkerLod.AggregateScreenPixels;
                }
                else
                {
                    center = screen[i];
                    diameter = MapMarkerLod.CorePixels(MapMarkerLod.NearestPixels(screen, cities.Length, i));
                }

                if (AddObstacle(center, diameter + MarkerPadding * 2f, view, destination, ref count))
                {
                    if (plates && members < 2 && count < destination.Length)
                    {
                        var plateCenter = new Vector2(center.x, center.y + 16f + plateHeight * 0.5f + 6f);
                        AddObstacle(plateCenter, plateWidth, plateHeight, view, destination, ref count);
                    }
                }
            }

            return count;
        }

        public static int AvoidObstacles(
            int count,
            int[] chosen,
            Vector2[] anchors,
            string locale,
            float glyphPixels,
            float[] measuredWidths,
            float lineHeight,
            Rect viewport,
            Rect[] occupied,
            Rect[] obstacles,
            int obstacleCount,
            float maxNudgePixels,
            Vector2[] placed,
            Vector2[] heldNudge,
            Vector2[] presentation = null)
        {
            if (count <= 0 || chosen == null || anchors == null || occupied == null || placed == null)
                return 0;

            var countries = MapCountryCatalog.All;
            var kept = 0;
            var cap = maxNudgePixels > 1f ? maxNudgePixels : 0f;
            for (var slot = 0; slot < count; slot++)
            {
                var index = chosen[slot];
                if (index < 0 || index >= anchors.Length || index >= countries.Length)
                    continue;

                var width = Width(Name(countries[index], locale), glyphPixels);
                if (measuredWidths != null && index < measuredWidths.Length && measuredWidths[index] > 1f)
                    width = measuredWidths[index];

                var anchor = anchors[index];
                var shift = presentation != null && slot < presentation.Length ? presentation[slot] : Vector2.zero;
                var presented = shift.sqrMagnitude > 0.25f;
                var anchorRect = LabelRect(presented ? anchor + shift : anchor, width, lineHeight);
                var anchorClear = Fits(anchorRect, viewport, obstacles, obstacleCount, occupied, kept);
                var comfortable = Fits(Expand(anchorRect, AnchorReturnPadding), viewport, obstacles, obstacleCount, occupied, kept);
                var held = heldNudge != null && index < heldNudge.Length ? heldNudge[index] : Vector2.zero;
                var resolved = presented ? anchor + shift : anchor;
                var resolvedRect = anchorRect;
                var found = false;
                if (presented && anchorClear)
                {
                    found = true;
                }
                else if (presented)
                {
                    found = false;
                }

                if (!presented && held.sqrMagnitude > 0.01f && held.magnitude <= cap + 0.5f && !comfortable)
                {
                    var heldCenter = anchor + held;
                    var heldRect = LabelRect(heldCenter, width, lineHeight);
                    if (Fits(heldRect, viewport, obstacles, obstacleCount, occupied, kept))
                    {
                        resolved = heldCenter;
                        resolvedRect = heldRect;
                        found = true;
                    }
                }

                if (!presented && !found && anchorClear)
                {
                    resolved = anchor;
                    resolvedRect = anchorRect;
                    found = true;
                }

                if (!presented && !found && cap > 1f)
                {
                    for (var ring = 0; ring < 2 && !found; ring++)
                    {
                        var distance = cap * (ring == 0 ? 0.55f : 1f);
                        for (var d = 0; d < NudgeDirections.Length; d++)
                        {
                            var candidate = anchor + NudgeDirections[d] * distance;
                            var rect = LabelRect(candidate, width, lineHeight);
                            if (!Fits(rect, viewport, obstacles, obstacleCount, occupied, kept))
                                continue;

                            resolved = candidate;
                            resolvedRect = rect;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    if (heldNudge != null && index < heldNudge.Length)
                        heldNudge[index] = Vector2.zero;
                    continue;
                }

                chosen[kept] = index;
                occupied[kept] = resolvedRect;
                placed[index] = resolved;
                if (heldNudge != null && index < heldNudge.Length)
                    heldNudge[index] = presented ? resolved - (anchor + shift) : resolved - anchor;
                kept++;
            }

            return kept;
        }

        public static int TurkeyIndex
        {
            get
            {
                if (_turkey < -1)
                    _turkey = MapCountryCatalog.IndexOf("TUR");
                return _turkey;
            }
        }

        public static bool IsFullAggregate(int[] parent, int count)
        {
            if (parent == null || count < 2 || parent.Length < count)
                return false;

            var root = parent[0];
            for (var i = 1; i < count; i++)
            {
                if (parent[i] != root)
                    return false;
            }

            return true;
        }

        public static bool TryWorldAggregateScreen(
            Vector2 cameraCenter,
            float unitsPerPixel,
            float screenWidth,
            float screenHeight,
            int[] parent,
            out Vector2 screen)
        {
            screen = Vector2.zero;
            var count = MapCities.All.Length;
            if (!IsFullAggregate(parent, count) || _cityWorld == null || _cityWorld.Length < count)
                return false;

            var centroid = MapMarkerLod.Centroid(_cityWorld, parent, count, 0);
            screen = MapMarkerLod.ToScreen(centroid, cameraCenter, unitsPerPixel, screenWidth, screenHeight);
            return true;
        }

        public static bool AggregateCaptionApplies(bool fullAggregate, bool countryNameVisible)
        {
            return fullAggregate && !countryNameVisible;
        }

        /// <summary>
        /// Places the aggregate's country caption on a fixed side of the badge.
        /// Returns the side that fit, or -1 when every side hits a label, the HUD,
        /// or the badge. The previous side is kept when it still fits.
        /// </summary>
        public static int PlaceAggregateCaption(
            Vector2 badge,
            float labelWidth,
            float lineHeight,
            Rect viewport,
            Rect[] occupied,
            int occupiedCount,
            int heldSide,
            out Vector2 screen,
            out Rect bounds)
        {
            screen = Vector2.zero;
            bounds = new Rect();
            if (labelWidth < 1f || lineHeight < 1f)
                return -1;
            if (badge.x < viewport.xMin || badge.y < viewport.yMin || badge.x > viewport.xMax || badge.y > viewport.yMax)
                return -1;

            if (heldSide >= 0 && heldSide < CaptionSides.Length &&
                CaptionSideFits(heldSide, badge, labelWidth, lineHeight, viewport, occupied, occupiedCount, out screen, out bounds))
                return heldSide;

            for (var side = 0; side < CaptionSides.Length; side++)
            {
                if (side == heldSide)
                    continue;
                if (!CaptionSideFits(side, badge, labelWidth, lineHeight, viewport, occupied, occupiedCount, out screen, out bounds))
                    continue;
                return side;
            }

            screen = Vector2.zero;
            bounds = new Rect();
            return -1;
        }

        static bool CaptionSideFits(
            int side,
            Vector2 badge,
            float width,
            float line,
            Rect viewport,
            Rect[] occupied,
            int occupiedCount,
            out Vector2 screen,
            out Rect bounds)
        {
            var along = (side < 2 ? width : line) * 0.5f
                + MapMarkerLod.AggregateScreenPixels * 0.5f
                + AggregateCaptionGap;
            screen = badge + CaptionSides[side] * along;
            bounds = LabelRect(screen, width, line);
            if (bounds.xMin < viewport.xMin || bounds.yMin < viewport.yMin || bounds.xMax > viewport.xMax || bounds.yMax > viewport.yMax)
                return false;

            var badgeRect = new Rect(
                badge.x - MapMarkerLod.AggregateScreenPixels * 0.5f,
                badge.y - MapMarkerLod.AggregateScreenPixels * 0.5f,
                MapMarkerLod.AggregateScreenPixels,
                MapMarkerLod.AggregateScreenPixels);
            var ink = new Rect(screen.x - width * 0.5f, screen.y - line * 0.5f, width, line);
            if (Overlaps(ink, badgeRect))
                return false;

            if (occupied != null && occupiedCount > 0)
            {
                var limit = occupiedCount < occupied.Length ? occupiedCount : occupied.Length;
                for (var i = 0; i < limit; i++)
                {
                    if (Overlaps(bounds, occupied[i]))
                        return false;
                }
            }

            return true;
        }

        static bool AddObstacle(Vector2 center, float diameter, Rect view, Rect[] destination, ref int count)
        {
            return AddObstacle(center, diameter, diameter, view, destination, ref count);
        }

        static bool AddObstacle(Vector2 center, float width, float height, Rect view, Rect[] destination, ref int count)
        {
            if (count >= destination.Length)
                return false;

            var rect = new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
            if (!Overlaps(rect, view))
                return false;

            destination[count] = rect;
            count++;
            return true;
        }

        static Rect Expand(Rect rect, float padding)
        {
            return new Rect(rect.x - padding, rect.y - padding, rect.width + padding * 2f, rect.height + padding * 2f);
        }

        static bool Fits(Rect rect, Rect viewport, Rect[] obstacles, int obstacleCount, Rect[] accepted, int acceptedCount)
        {
            if (rect.xMin < viewport.xMin || rect.yMin < viewport.yMin || rect.xMax > viewport.xMax || rect.yMax > viewport.yMax)
                return false;
            if (obstacleCount > 0 && obstacles != null)
            {
                var limit = obstacleCount < obstacles.Length ? obstacleCount : obstacles.Length;
                for (var i = 0; i < limit; i++)
                {
                    if (Overlaps(rect, obstacles[i]))
                        return false;
                }
            }

            for (var i = 0; i < acceptedCount; i++)
            {
                if (Overlaps(rect, accepted[i]))
                    return false;
            }

            return true;
        }

        static void EnsureOrder()
        {
            if (_order != null)
                return;

            var countries = MapCountryCatalog.All;
            _order = new int[countries.Length];
            for (var i = 0; i < _order.Length; i++)
                _order[i] = i;

            for (var i = 1; i < _order.Length; i++)
            {
                var key = _order[i];
                var j = i - 1;
                while (j >= 0 && Compare(countries, _order[j], key) > 0)
                {
                    _order[j + 1] = _order[j];
                    j--;
                }

                _order[j + 1] = key;
            }
        }

        static int Compare(MapCountryCatalog.Entry[] countries, int left, int right)
        {
            var rank = countries[left].Rank.CompareTo(countries[right].Rank);
            if (rank != 0)
                return rank;

            var min = countries[left].MinLabel.CompareTo(countries[right].MinLabel);
            if (min != 0)
                return min;

            return string.CompareOrdinal(countries[left].Code, countries[right].Code);
        }
    }
}
