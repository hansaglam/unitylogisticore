using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("LogistiCore.Tests")]

namespace LogistiCore.Map
{
    /// <summary>
    /// Screen-space grouping for the existing city markers.
    /// A cluster is a set of markers that would draw on top of each other.
    /// It is not a city and it is not tied to one country name.
    /// </summary>
    public static class MapMarkerLod
    {
        public const float CoreScreenPixels = 34f;
        public const float SelectedMultiplier = 1.55f;
        public const float FullGlowScale = 2.8f;
        public const float TightGlowScale = 1.35f;
        public const float JoinPixels = 26f;
        public const float SplitPixels = 44f;
        public const float CountryFrameFactor = 1.08f;
        public const float AggregateScreenPixels = 26f;
        public const float AggregateHitRadius = 22f;
        public const float MarkerHitPixels = 48f;

        public static bool AtCountryScale(float orthographicSize, float turkeyFrameSize)
        {
            return turkeyFrameSize > 0.0001f && orthographicSize <= turkeyFrameSize * CountryFrameFactor;
        }

        public static float UnitsPerPixel(float orthographicSize, float screenHeight)
        {
            var height = screenHeight < 1f ? 1f : screenHeight;
            return orthographicSize * 2f / height;
        }

        public static Vector2 ToScreen(Vector2 world, Vector2 cameraCenter, float unitsPerPixel, float screenWidth, float screenHeight)
        {
            var width = screenWidth < 1f ? 1f : screenWidth;
            var height = screenHeight < 1f ? 1f : screenHeight;
            var scale = unitsPerPixel < 0.000001f ? 1f : unitsPerPixel;
            return new Vector2(
                width * 0.5f + (world.x - cameraCenter.x) / scale,
                height * 0.5f + (world.y - cameraCenter.y) / scale);
        }

        public static float ScaleForPixels(float pixels, float orthographicSize, float screenHeight)
        {
            var height = screenHeight < 1f ? 1f : screenHeight;
            return pixels / height * (orthographicSize * 2f);
        }

        public static float CorePixels(float nearestNeighborPixels)
        {
            var pixels = CoreScreenPixels;
            if (nearestNeighborPixels > 0f && nearestNeighborPixels < 96f)
            {
                var fitted = nearestNeighborPixels * 0.4f;
                if (fitted < 14f)
                    fitted = 14f;
                if (fitted < pixels)
                    pixels = fitted;
            }

            return pixels;
        }

        public static float GlowScale(float corePixels)
        {
            return corePixels >= 30f ? FullGlowScale : TightGlowScale;
        }

        public static float NearestPixels(Vector2[] screen, int count, int index)
        {
            var best = 0f;
            var found = false;
            for (var i = 0; i < count; i++)
            {
                if (i == index)
                    continue;

                var distance = Vector2.Distance(screen[index], screen[i]);
                if (!found || distance < best)
                {
                    best = distance;
                    found = true;
                }
            }

            return found ? best : 0f;
        }

        /// <summary>
        /// Groups markers that overlap on screen. Pairs that were already grouped stay together
        /// until SplitPixels, so a slow zoom does not flicker. Country scale shows every marker.
        /// </summary>
        public static void Cluster(
            Vector2[] screen,
            int count,
            bool forceIndividuals,
            bool[] together,
            int[] parent)
        {
            for (var i = 0; i < count; i++)
                parent[i] = i;

            if (!forceIndividuals)
            {
                for (var i = 0; i < count; i++)
                {
                    for (var j = i + 1; j < count; j++)
                    {
                        var limit = together[i * count + j] ? SplitPixels : JoinPixels;
                        var delta = screen[i] - screen[j];
                        if (delta.sqrMagnitude < limit * limit)
                            Union(parent, i, j);
                    }
                }
            }

            for (var i = 0; i < count; i++)
                parent[i] = Find(parent, i);

            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                    together[i * count + j] = parent[i] == parent[j];
            }
        }

        public static int ClusterSize(int[] parent, int count, int index)
        {
            var root = parent[index];
            var size = 0;
            for (var i = 0; i < count; i++)
            {
                if (parent[i] == root)
                    size++;
            }

            return size;
        }

        public static Vector2 Centroid(Vector2[] world, int[] parent, int count, int index)
        {
            var root = parent[index];
            var sum = Vector2.zero;
            var size = 0;
            for (var i = 0; i < count; i++)
            {
                if (parent[i] != root)
                    continue;

                sum += world[i];
                size++;
            }

            return size == 0 ? world[index] : sum / size;
        }

        public static bool Hits(Vector2 screen, Vector2 target, float radius)
        {
            var delta = screen - target;
            return delta.sqrMagnitude <= radius * radius;
        }

        static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }

            return index;
        }

        static void Union(int[] parent, int a, int b)
        {
            var rootA = Find(parent, a);
            var rootB = Find(parent, b);
            if (rootA == rootB)
                return;

            if (rootA < rootB)
                parent[rootB] = rootA;
            else
                parent[rootA] = rootB;
        }
    }
}
