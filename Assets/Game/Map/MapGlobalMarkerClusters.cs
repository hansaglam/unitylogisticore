using System;
using System.Collections.Generic;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Presentation-only output of viewport culling and screen-space grouping.
    /// Members > 1 means zoom/focus, NOT an authorized business destination.
    /// </summary>
    public readonly struct MapGlobalMarkerGroup
    {
        public readonly MapGlobalCity Representative;
        public readonly Vector2 ScreenPosition;
        public readonly int Members;

        public MapGlobalMarkerGroup(MapGlobalCity representative, Vector2 screenPosition, int members)
        {
            Representative = representative;
            ScreenPosition = screenPosition;
            Members = members;
        }
    }

    /// <summary>
    /// Deterministic MAP-05 groundwork. Camera projection and map selection remain
    /// owned by the caller. This class never creates jobs, roads or unlocks.
    /// Provide positions in screen pixels in the same order as the city catalog.
    /// </summary>
    public static class MapGlobalMarkerClusters
    {
        struct Cell
        {
            public MapGlobalCity Representative;
            public Vector2 Position;
            public int Members;
        }

        /// <summary>
        /// Only points within the padded viewport and at or above the selected
        /// cartographic detail (ScaleRank <= maxScaleRank) are included.
        /// The lowest ScaleRank wins as representative; ties keep source order.
        /// Returns false on malformed inputs rather than showing misleading pins.
        /// </summary>
        public static bool TryBuild(IReadOnlyList<MapGlobalCity> cities,
            IReadOnlyList<Vector2> screenPositions, Rect viewport,
            float cellPixels, int maxScaleRank, List<MapGlobalMarkerGroup> output,
            float paddingPixels = 0f)
        {
            if (cities == null || screenPositions == null || output == null ||
                cities.Count != screenPositions.Count ||
                !IsFinite(cellPixels) || cellPixels <= 0f ||
                !IsFinite(paddingPixels) || paddingPixels < 0f ||
                !IsFinite(viewport.xMin) || !IsFinite(viewport.yMin) ||
                !IsFinite(viewport.width) || !IsFinite(viewport.height) ||
                viewport.width <= 0f || viewport.height <= 0f)
                return false;

            // No mutation on invalid input. On valid input the result is rebuilt
            // in a stable order without a per-city GameObject allocation.
            output.Clear();
            var indices = new Dictionary<long, int>();
            var cells = new List<Cell>();
            var left = (double)viewport.xMin - paddingPixels;
            var right = (double)viewport.xMax + paddingPixels;
            var bottom = (double)viewport.yMin - paddingPixels;
            var top = (double)viewport.yMax + paddingPixels;

            for (var i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                if (city.ScaleRank > maxScaleRank)
                    continue;

                var point = screenPositions[i];
                if (!IsFinite(point.x) || !IsFinite(point.y) ||
                    point.x < left || point.x > right ||
                    point.y < bottom || point.y > top)
                    continue;

                // Absolute pixel grid: consistent results regardless of the
                // viewport's offset (e.g. safe-area and split-panel layouts).
                var gx = Math.Floor((double)point.x / cellPixels);
                var gy = Math.Floor((double)point.y / cellPixels);
                if (gx < int.MinValue || gx > int.MaxValue ||
                    gy < int.MinValue || gy > int.MaxValue)
                    return false;

                var key = ((long)(int)gx << 32) | (uint)(int)gy;
                if (indices.TryGetValue(key, out var index))
                {
                    var current = cells[index];
                    current.Members++;
                    if (city.ScaleRank < current.Representative.ScaleRank)
                    {
                        current.Representative = city;
                        current.Position = point;
                    }
                    cells[index] = current;
                }
                else
                {
                    indices.Add(key, cells.Count);
                    cells.Add(new Cell
                    {
                        Representative = city,
                        Position = point,
                        Members = 1
                    });
                }
            }

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                output.Add(new MapGlobalMarkerGroup(cell.Representative,
                    cell.Position, cell.Members));
            }

            return true;
        }

        static bool IsFinite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);
    }
}
