using System;
using System.Collections.Generic;
using System.IO;
using LogistiCore.Map;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogistiCore.EditorTools
{
    public static class MapGeographyBaker
    {
        public const int BakeVersion = 5;
        public const string SourceGeo = "Assets/Game/Map/Source/ne_50m_admin_0_countries.geojson";
        public const string SourceRelief = "Assets/Game/Map/Source/SR_50M_4096.png";
        public const string GeneratedFolder = "Assets/Game/Map/Generated";
        public const string ReliefTexturePath = "Assets/Game/Map/Generated/MapRelief.png";
        public const string ReliefMeshPath = "Assets/Game/Map/Generated/MapReliefGrid.asset";
        public const string LandFillMeshPath = "Assets/Game/Map/Generated/MapLandFill.asset";
        public const string CoastMeshPath = "Assets/Game/Map/Generated/MapCoastlines.asset";
        public const string TurkeyMeshPath = "Assets/Game/Map/Generated/MapTurkeyCoast.asset";
        public const string MarkerTexturePath = "Assets/Game/Map/Generated/MapMarker.png";
        public const string MarkerGlowTexturePath = "Assets/Game/Map/Generated/MapMarkerGlow.png";
        public const string VignetteTexturePath = "Assets/Game/Map/Generated/MapVignette.png";
        public const string WhiteTexturePath = "Assets/Game/Map/Generated/MapWhite.png";
        public const string LineMaterialPath = "Assets/Game/Map/Generated/MapLines.mat";
        public const string ReliefMaterialPath = "Assets/Game/Map/Generated/MapRelief.mat";
        public const string OceanMeshPath = "Assets/Game/Map/Generated/MapOcean.asset";
        public const string VersionPath = "Assets/Game/Map/Generated/bake-version.txt";

        public const int ReliefWidth = 4096;
        public const int ReliefHeight = 2048;
        public const int GridColumns = 180;
        public const int GridRows = 90;

        static readonly Color32 Ocean = new Color32(4, 12, 22, 255);
        static readonly Color32 LandDark = new Color32(11, 22, 34, 255);
        static readonly Color32 LandLight = new Color32(62, 90, 108, 255);
        static readonly Color32 TurkeyDark = new Color32(14, 36, 52, 255);
        static readonly Color32 TurkeyLight = new Color32(48, 92, 112, 255);
        static readonly Color32 Coast = new Color32(78, 128, 156, 255);
        static readonly Color32 TurkeyCoast = new Color32(140, 236, 255, 255);
        static List<Edge> _turkeyEdges;

        public static bool IsReady =>
            File.Exists(VersionPath) &&
            File.ReadAllText(VersionPath).Trim() == BakeVersion.ToString() &&
            AssetDatabase.LoadAssetAtPath<Texture2D>(ReliefTexturePath) != null &&
            AssetDatabase.LoadAssetAtPath<Mesh>(CoastMeshPath) != null &&
            AssetDatabase.LoadAssetAtPath<Mesh>(ReliefMeshPath) != null &&
            AssetDatabase.LoadAssetAtPath<Mesh>(LandFillMeshPath) != null;

        public static void EnsureBaked()
        {
            if (IsReady)
                return;

            Bake();
        }

        public static void Bake()
        {
            if (!File.Exists(SourceGeo))
                throw new FileNotFoundException("Natural Earth countries file is missing.", SourceGeo);
            if (!File.Exists(SourceRelief))
                throw new FileNotFoundException("Natural Earth shaded relief is missing.", SourceRelief);

            EnsureFolder(GeneratedFolder);
            try
            {
                EditorUtility.DisplayProgressBar("LogistiCore map", "Reading country geometry", 0.05f);
                var root = JObject.Parse(File.ReadAllText(SourceGeo));
                var features = (JArray)root["features"];
                var edges = new List<Edge>(120000);
                var coast = new List<Vector3>(240000);
                var coastColors = new List<Color32>(240000);
                var turkey = new List<Vector3>(2000);
                var turkeyColors = new List<Color32>(2000);
                var fills = new List<FillPolygon>(1800);
                _turkeyEdges = new List<Edge>(2000);

                for (var i = 0; i < features.Count; i++)
                {
                    var feature = (JObject)features[i];
                    var geometry = feature["geometry"];
                    if (geometry == null || geometry.Type == JTokenType.Null)
                        continue;

                    var properties = feature["properties"] as JObject;
                    var turkeyFeature = properties != null && (string)properties["ADM0_A3"] == "TUR";
                    var type = (string)geometry["type"];
                    var coordinates = geometry["coordinates"] as JArray;
                    if (coordinates == null)
                        continue;

                    if (type == "Polygon")
                        AddPolygon(coordinates, turkeyFeature, edges, coast, coastColors, turkey, turkeyColors, fills);
                    else if (type == "MultiPolygon")
                    {
                        for (var p = 0; p < coordinates.Count; p++)
                        {
                            if (coordinates[p] is JArray polygon)
                                AddPolygon(polygon, turkeyFeature, edges, coast, coastColors, turkey, turkeyColors, fills);
                        }
                    }
                }

                EditorUtility.DisplayProgressBar("LogistiCore map", "Rasterizing land", 0.25f);
                var land = new bool[ReliefWidth * ReliefHeight];
                var turkeyMask = new bool[ReliefWidth * ReliefHeight];
                Rasterize(edges, land, ReliefWidth, ReliefHeight);
                Rasterize(_turkeyEdges, turkeyMask, ReliefWidth, ReliefHeight);
                FillTurkeyHoles(turkeyMask, ReliefWidth, ReliefHeight);

                EditorUtility.DisplayProgressBar("LogistiCore map", "Tinting relief", 0.7f);
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                try
                {
                    if (!source.LoadImage(File.ReadAllBytes(SourceRelief)))
                        throw new InvalidOperationException("Could not read the shaded relief PNG.");

                    var sourcePixels = source.GetPixels32();
                    var tinted = Tint(land, turkeyMask, sourcePixels, source.width, source.height, ReliefWidth, ReliefHeight);
                    var output = new Texture2D(ReliefWidth, ReliefHeight, TextureFormat.RGBA32, false, false);
                    output.SetPixels32(tinted);
                    output.Apply(false, false);
                    File.WriteAllBytes(ReliefTexturePath, output.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(output);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(source);
                }

                EditorUtility.DisplayProgressBar("LogistiCore map", "Writing meshes", 0.88f);
                WriteLineMesh(CoastMeshPath, coast, coastColors);
                WriteLineMesh(TurkeyMeshPath, turkey, turkeyColors);
                WriteLandFill(LandFillMeshPath, fills);
                WriteGridMesh(ReliefMeshPath);
                WriteOceanMesh(OceanMeshPath);
                WriteMarkerTexture(MarkerTexturePath);
                WriteMarkerGlowTexture(MarkerGlowTexturePath);
                WriteVignetteTexture(VignetteTexturePath);
                WriteSolidTexture(WhiteTexturePath, new Color32(255, 255, 255, 255));
                AssetDatabase.Refresh();
                ConfigureTexture(ReliefTexturePath, TextureImporterType.Default, 4096);
                ConfigureTexture(MarkerTexturePath, TextureImporterType.Sprite);
                ConfigureTexture(MarkerGlowTexturePath, TextureImporterType.Sprite);
                ConfigureTexture(VignetteTexturePath, TextureImporterType.Sprite);
                ConfigureTexture(WhiteTexturePath, TextureImporterType.Default);
                WriteMaterials();
                File.WriteAllText(VersionPath, BakeVersion.ToString());
                AssetDatabase.SaveAssets();
                _turkeyEdges = null;
                Debug.Log("[LogistiCore] Map bake: " + edges.Count + " land edges, " + coast.Count + " coast vertices, " + turkey.Count + " Türkiye vertices.");
            }
            finally
            {
                _turkeyEdges = null;
                EditorUtility.ClearProgressBar();
            }
        }

        static void AddPolygon(
            JArray polygon,
            bool turkeyFeature,
            List<Edge> edges,
            List<Vector3> coast,
            List<Color32> coastColors,
            List<Vector3> turkey,
            List<Color32> turkeyColors,
            List<FillPolygon> fills)
        {
            FillPolygon fill = null;
            for (var r = 0; r < polygon.Count; r++)
            {
                if (polygon[r] is not JArray ring)
                    continue;

                AddRing(ring, turkeyFeature, edges, coast, coastColors, turkey, turkeyColors);
                if (r == 0)
                {
                    fill = new FillPolygon { Turkey = turkeyFeature };
                    CopyRing(ring, fill.Longitude, fill.Latitude);
                    if (fill.Longitude.Count >= 3)
                        fills.Add(fill);
                    else
                        fill = null;
                }
                else if (fill != null)
                {
                    var holeLongitude = new List<double>();
                    var holeLatitude = new List<double>();
                    CopyRing(ring, holeLongitude, holeLatitude);
                    if (holeLongitude.Count >= 3)
                    {
                        fill.HoleLongitude.Add(holeLongitude);
                        fill.HoleLatitude.Add(holeLatitude);
                    }
                }
            }
        }

        static void CopyRing(JArray ring, List<double> longitude, List<double> latitude)
        {
            var count = ring.Count;
            if (count >= 2 && SamePoint(ring[0], ring[count - 1]))
                count--;

            for (var i = 0; i < count; i++)
            {
                if (ring[i] is not JArray point || point.Count < 2)
                    continue;

                var lon = point[0].Value<double>();
                var lat = point[1].Value<double>();
                var previous = longitude.Count - 1;
                if (previous >= 0 &&
                    Math.Abs(longitude[previous] - lon) < 1e-9 &&
                    Math.Abs(latitude[previous] - lat) < 1e-9)
                    continue;

                longitude.Add(lon);
                latitude.Add(lat);
            }
        }

        static void AddRing(
            JArray ring,
            bool turkeyFeature,
            List<Edge> edges,
            List<Vector3> coast,
            List<Color32> coastColors,
            List<Vector3> turkey,
            List<Color32> turkeyColors)
        {
            var count = ring.Count;
            if (count < 2)
                return;

            var closed = SamePoint(ring[0], ring[count - 1]);
            var segments = closed ? count - 1 : count;
            for (var i = 0; i < segments; i++)
            {
                var a = (JArray)ring[i];
                var b = (JArray)ring[(i + 1) % count];
                AddSegment(
                    a[0].Value<double>(),
                    a[1].Value<double>(),
                    b[0].Value<double>(),
                    b[1].Value<double>(),
                    turkeyFeature,
                    edges,
                    coast,
                    coastColors,
                    turkey,
                    turkeyColors);
            }
        }

        static void AddSegment(
            double lon0,
            double lat0,
            double lon1,
            double lat1,
            bool turkeyFeature,
            List<Edge> edges,
            List<Vector3> coast,
            List<Color32> coastColors,
            List<Vector3> turkey,
            List<Color32> turkeyColors)
        {
            var delta = lon1 - lon0;
            if (delta > 180d || delta < -180d)
            {
                var lon1Unwrapped = delta > 180d ? lon1 - 360d : lon1 + 360d;
                var span = lon1Unwrapped - lon0;
                if (Math.Abs(span) < 1e-9)
                    return;

                var boundary = lon0 < lon1Unwrapped ? 180d : -180d;
                var t = (boundary - lon0) / span;
                if (t < 0d)
                    t = 0d;
                if (t > 1d)
                    t = 1d;

                var latCross = lat0 + (lat1 - lat0) * t;
                AddStraight(lon0, lat0, boundary, latCross, turkeyFeature, edges, coast, coastColors, turkey, turkeyColors);
                AddStraight(-boundary, latCross, lon1, lat1, turkeyFeature, edges, coast, coastColors, turkey, turkeyColors);
                return;
            }

            AddStraight(lon0, lat0, lon1, lat1, turkeyFeature, edges, coast, coastColors, turkey, turkeyColors);
        }

        static void AddStraight(
            double lon0,
            double lat0,
            double lon1,
            double lat1,
            bool turkeyFeature,
            List<Edge> edges,
            List<Vector3> coast,
            List<Color32> coastColors,
            List<Vector3> turkey,
            List<Color32> turkeyColors)
        {
            if (Math.Abs(lon0 - lon1) < 1e-12 && Math.Abs(lat0 - lat1) < 1e-12)
                return;

            edges.Add(new Edge(lon0, lat0, lon1, lat1));
            if (turkeyFeature && _turkeyEdges != null)
                _turkeyEdges.Add(new Edge(lon0, lat0, lon1, lat1));
            var lines = turkeyFeature ? turkey : coast;
            var colors = turkeyFeature ? turkeyColors : coastColors;
            var color = turkeyFeature ? TurkeyCoast : Coast;
            if (!MapProjection.TryProjectUnwrapped(lat0, lon0, out var a) || !MapProjection.TryProjectUnwrapped(lat1, lon1, out var b))
                return;

            lines.Add(new Vector3(a.x, a.y, 0f));
            lines.Add(new Vector3(b.x, b.y, 0f));
            colors.Add(color);
            colors.Add(color);
        }

        static bool SamePoint(JToken a, JToken b)
        {
            var left = (JArray)a;
            var right = (JArray)b;
            return Math.Abs(left[0].Value<double>() - right[0].Value<double>()) < 1e-9 &&
                   Math.Abs(left[1].Value<double>() - right[1].Value<double>()) < 1e-9;
        }

        static void Rasterize(List<Edge> edges, bool[] land, int width, int height)
        {
            var hits = new double[256];
            var edgeCount = edges.Count;
            for (var y = 0; y < height; y++)
            {
                if ((y & 63) == 0)
                    EditorUtility.DisplayProgressBar("LogistiCore map", "Rasterizing land", 0.25f + 0.4f * y / height);

                var lat = -90d + ((y + 0.5d) / height) * 180d;
                var count = 0;
                for (var i = 0; i < edgeCount; i++)
                {
                    var edge = edges[i];
                    if (!((edge.Lat0 <= lat && lat < edge.Lat1) || (edge.Lat1 <= lat && lat < edge.Lat0)))
                        continue;

                    var dLon = edge.Lon1 - edge.Lon0;
                    var t = (lat - edge.Lat0) / (edge.Lat1 - edge.Lat0);
                    var lon = edge.Lon0 + t * dLon;
                    if (count == hits.Length)
                        Array.Resize(ref hits, hits.Length * 2);
                    hits[count++] = lon;
                }

                if (count < 2)
                    continue;

                Array.Sort(hits, 0, count);
                var row = y * width;
                for (var i = 0; i + 1 < count; i += 2)
                {
                    var x0 = LonToPixel(hits[i], width);
                    var x1 = LonToPixel(hits[i + 1], width);
                    if (x1 < x0)
                    {
                        var swap = x0;
                        x0 = x1;
                        x1 = swap;
                    }

                    if (x0 < 0)
                        x0 = 0;
                    if (x1 > width)
                        x1 = width;
                    for (var x = x0; x < x1; x++)
                        land[row + x] = true;
                }
            }
        }

        static int LonToPixel(double longitude, int width)
        {
            var x = (int)((longitude + 180d) / 360d * width);
            if (x < 0)
                return 0;
            if (x > width)
                return width;
            return x;
        }

        static void WriteLandFill(string path, List<FillPolygon> fills)
        {
            var vertices = new List<Vector3>(120000);
            var uv = new List<Vector2>(120000);
            var triangles = new List<int>(300000);
            var skipped = 0;
            for (var i = 0; i < fills.Count; i++)
            {
                var fill = fills[i];
                var vertexBefore = vertices.Count;
                var uvBefore = uv.Count;
                var triangleBefore = triangles.Count;
                var ok = MapPolygonFill.TryTriangulate(
                    fill.Longitude,
                    fill.Latitude,
                    fill.HoleLongitude,
                    fill.HoleLatitude,
                    vertices,
                    uv,
                    triangles);
                if (ok)
                    continue;

                if (vertices.Count > vertexBefore)
                    vertices.RemoveRange(vertexBefore, vertices.Count - vertexBefore);
                if (uv.Count > uvBefore)
                    uv.RemoveRange(uvBefore, uv.Count - uvBefore);
                if (triangles.Count > triangleBefore)
                    triangles.RemoveRange(triangleBefore, triangles.Count - triangleBefore);
                skipped++;
                Debug.LogWarning(
                    "[LogistiCore] Map fill skipped a polygon at " +
                    fill.Latitude[0].ToString("0.00") + ", " + fill.Longitude[0].ToString("0.00") +
                    " with " + fill.Longitude.Count + " vertices.");
                if (fill.Turkey)
                    throw new InvalidOperationException("Türkiye polygon could not be triangulated.");
            }

            var colors = new Color32[vertices.Count];
            for (var i = 0; i < colors.Length; i++)
                colors[i] = new Color32(255, 255, 255, 255);

            var mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.colors32 = colors;
            mesh.SetTriangles(triangles, 0, false);
            mesh.RecalculateBounds();
            SaveAsset(mesh, path);
            if (skipped > 0)
                Debug.LogWarning("[LogistiCore] Map fill skipped " + skipped + " country polygons.");
        }

        static Color32[] Tint(
            bool[] land,
            bool[] turkey,
            Color32[] source,
            int sourceWidth,
            int sourceHeight,
            int width,
            int height)
        {
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                var lat = -90d + ((y + 0.5d) / height) * 180d;
                var sy = (int)((lat + 90d) / 180d * sourceHeight);
                if (sy < 0)
                    sy = 0;
                if (sy >= sourceHeight)
                    sy = sourceHeight - 1;

                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    if (!land[index] && !turkey[index])
                    {
                        pixels[index] = Ocean;
                        continue;
                    }

                    var lon = -180d + ((x + 0.5d) / width) * 360d;
                    var sx = (int)((lon + 180d) / 360d * sourceWidth);
                    if (sx < 0)
                        sx = 0;
                    if (sx >= sourceWidth)
                        sx = sourceWidth - 1;

                    var shade = ReliefShade(source[sy * sourceWidth + sx].r / 255f);
                    pixels[index] = turkey[index]
                        ? Lerp(TurkeyDark, TurkeyLight, shade)
                        : Lerp(LandDark, LandLight, shade);
                }
            }

            return pixels;
        }

        static float ReliefShade(float raw)
        {
            var t = (raw - 0.55f) / 0.32f;
            if (t < 0f)
                t = 0f;
            if (t > 1f)
                t = 1f;
            return t;
        }

        static void FillTurkeyHoles(bool[] turkey, int width, int height)
        {
            var minX = width;
            var maxX = 0;
            var minY = height;
            var maxY = 0;
            for (var i = 0; i < turkey.Length; i++)
            {
                if (!turkey[i])
                    continue;

                var x = i % width;
                var y = i / width;
                if (x < minX)
                    minX = x;
                if (x > maxX)
                    maxX = x;
                if (y < minY)
                    minY = y;
                if (y > maxY)
                    maxY = y;
            }

            if (maxX < minX)
                return;

            minX = Mathf.Max(0, minX - 1);
            maxX = Mathf.Min(width - 1, maxX + 1);
            minY = Mathf.Max(0, minY - 1);
            maxY = Mathf.Min(height - 1, maxY + 1);
            var outside = new bool[width * height];
            var queue = new Queue<int>();
            void Seed(int x, int y)
            {
                if (x < minX || x > maxX || y < minY || y > maxY)
                    return;

                var index = y * width + x;
                if (outside[index] || turkey[index])
                    return;

                outside[index] = true;
                queue.Enqueue(index);
            }

            for (var x = minX; x <= maxX; x++)
            {
                Seed(x, minY);
                Seed(x, maxY);
            }

            for (var y = minY; y <= maxY; y++)
            {
                Seed(minX, y);
                Seed(maxX, y);
            }

            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                var x = index % width;
                var y = index / width;
                Seed(x - 1, y);
                Seed(x + 1, y);
                Seed(x, y - 1);
                Seed(x, y + 1);
            }

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var index = y * width + x;
                    if (!turkey[index] && !outside[index])
                        turkey[index] = true;
                }
            }
        }

        static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            if (t < 0f)
                t = 0f;
            if (t > 1f)
                t = 1f;

            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                255);
        }

        static void WriteLineMesh(string path, List<Vector3> vertices, List<Color32> colors)
        {
            var mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            var indices = new int[vertices.Count];
            for (var i = 0; i < indices.Length; i++)
                indices[i] = i;
            mesh.SetIndices(indices, MeshTopology.Lines, 0, false);
            mesh.RecalculateBounds();
            SaveAsset(mesh, path);
        }

        static void WriteGridMesh(string path)
        {
            var columns = GridColumns;
            var rows = GridRows;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            for (var row = 0; row <= rows; row++)
            {
                var latitude = -MapProjection.MaxLatitude + (MapProjection.MaxLatitude * 2d) * row / rows;
                for (var column = 0; column <= columns; column++)
                {
                    var longitude = -180d + 360d * column / columns;
                    if (!MapProjection.TryProjectUnwrapped(latitude, longitude, out var projected))
                        throw new InvalidOperationException("Relief grid coordinate could not be projected.");
                    var index = row * (columns + 1) + column;
                    vertices[index] = new Vector3(projected.x, projected.y, 0f);
                    uv[index] = new Vector2((float)((longitude + 180d) / 360d), (float)((latitude + 90d) / 180d));
                    colors[index] = new Color32(255, 255, 255, 255);
                }
            }

            var triangles = new int[columns * rows * 6];
            var t = 0;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var i = row * (columns + 1) + column;
                    triangles[t++] = i;
                    triangles[t++] = i + columns + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + columns + 1;
                    triangles[t++] = i + columns + 2;
                }
            }

            var mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors32 = colors;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            SaveAsset(mesh, path);
        }

        static void WriteOceanMesh(string path)
        {
            MapFrames.World(out var min, out var max);
            var width = max.x - min.x;
            var height = max.y - min.y;
            var worldAspect = width / Mathf.Max(0.001f, height);
            const float widestAspect = 21f / 9f;
            var horizontalCover = widestAspect / Mathf.Max(0.001f, worldAspect);
            var padX = width * Mathf.Max(0.08f, (horizontalCover - 1f) * 0.5f + 0.06f);
            var padY = height * 0.12f;
            min -= new Vector2(padX, padY);
            max += new Vector2(padX, padY);
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(min.x, min.y, 0f),
                new Vector3(max.x, min.y, 0f),
                new Vector3(min.x, max.y, 0f),
                new Vector3(max.x, max.y, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.colors32 = new[] { Ocean, Ocean, Ocean, Ocean };
            mesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            mesh.RecalculateBounds();
            SaveAsset(mesh, path);
        }

        static void WriteMarkerTexture(string path)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = 0;
                    if (distance <= 0.22f)
                        alpha = 255;
                    else if (distance < 0.36f)
                        alpha = (byte)(255f * (1f - Mathf.InverseLerp(0.22f, 0.36f, distance)));
                    else if (distance >= 0.62f && distance <= 0.80f)
                        alpha = 235;
                    else if (distance > 0.50f && distance < 0.62f)
                        alpha = (byte)(235f * Mathf.InverseLerp(0.50f, 0.62f, distance));
                    else if (distance > 0.80f && distance < 0.98f)
                        alpha = (byte)(235f * (1f - Mathf.InverseLerp(0.80f, 0.98f, distance)));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            SaveTexture(texture, pixels, path);
        }

        static void WriteMarkerGlowTexture(string path)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var falloff = Mathf.Clamp01(1f - distance);
                    var alpha = (byte)(255f * falloff * falloff);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            SaveTexture(texture, pixels, path);
        }

        static void WriteVignetteTexture(string path)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var edge = Mathf.InverseLerp(0.42f, 1.15f, distance);
                    edge *= edge;
                    pixels[y * size + x] = new Color32(2, 6, 14, (byte)(Mathf.Clamp01(edge) * 210f));
                }
            }

            SaveTexture(texture, pixels, path);
        }

        static void SaveTexture(Texture2D texture, Color32[] pixels, string path)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static void WriteSolidTexture(string path, Color32 color)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static void WriteMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                throw new InvalidOperationException("URP 2D sprite shader was not found. The map was not given a replacement shader.");

            var white = AssetDatabase.LoadAssetAtPath<Texture2D>(WhiteTexturePath);
            var relief = AssetDatabase.LoadAssetAtPath<Texture2D>(ReliefTexturePath);
            if (white == null || relief == null)
                throw new InvalidOperationException("Map textures were not imported before materials were created.");

            WriteMaterial(LineMaterialPath, shader, white, Color.white);
            WriteMaterial(ReliefMaterialPath, shader, relief, Color.white);
        }

        static void WriteMaterial(string path, Shader shader, Texture texture, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
        }

        static void SaveAsset(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return;
            }

            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
        }

        static void ConfigureTexture(string path, TextureImporterType type, int maxSize = 2048)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Could not configure " + path);

            importer.textureType = type;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = maxSize;
            if (type == TextureImporterType.Sprite)
            {
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
            }

            importer.SaveAndReimport();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var slash = path.LastIndexOf('/');
            var parent = path.Substring(0, slash);
            var name = path.Substring(slash + 1);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        sealed class FillPolygon
        {
            public bool Turkey;
            public readonly List<double> Longitude = new List<double>();
            public readonly List<double> Latitude = new List<double>();
            public readonly List<List<double>> HoleLongitude = new List<List<double>>();
            public readonly List<List<double>> HoleLatitude = new List<List<double>>();
        }

        readonly struct Edge
        {
            public readonly double Lon0;
            public readonly double Lat0;
            public readonly double Lon1;
            public readonly double Lat1;

            public Edge(double lon0, double lat0, double lon1, double lat1)
            {
                Lon0 = lon0;
                Lat0 = lat0;
                Lon1 = lon1;
                Lat1 = lat1;
            }
        }
    }
}
