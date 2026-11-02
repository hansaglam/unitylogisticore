using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LogistiCore.Map;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogistiCore.EditorTools
{
    public readonly struct MapRibbonEdgeKey : IEquatable<MapRibbonEdgeKey>, IComparable<MapRibbonEdgeKey>
    {
        public readonly int Ax;
        public readonly int Ay;
        public readonly int Bx;
        public readonly int By;

        public MapRibbonEdgeKey(double lon0, double lat0, double lon1, double lat1)
        {
            var x0 = Quantize(lon0);
            var y0 = Quantize(lat0);
            var x1 = Quantize(lon1);
            var y1 = Quantize(lat1);
            if (x0 < x1 || (x0 == x1 && y0 <= y1))
            {
                Ax = x0;
                Ay = y0;
                Bx = x1;
                By = y1;
            }
            else
            {
                Ax = x1;
                Ay = y1;
                Bx = x0;
                By = y0;
            }
        }

        public static int Quantize(double value)
        {
            return (int)Math.Round(value * 1000000d, MidpointRounding.ToEven);
        }

        public bool Equals(MapRibbonEdgeKey other)
        {
            return Ax == other.Ax && Ay == other.Ay && Bx == other.Bx && By == other.By;
        }

        public override bool Equals(object obj)
        {
            return obj is MapRibbonEdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Ax;
                hash = (hash * 397) ^ Ay;
                hash = (hash * 397) ^ Bx;
                hash = (hash * 397) ^ By;
                return hash;
            }
        }

        public int CompareTo(MapRibbonEdgeKey other)
        {
            var ax = Ax.CompareTo(other.Ax);
            if (ax != 0)
                return ax;
            var ay = Ay.CompareTo(other.Ay);
            if (ay != 0)
                return ay;
            var bx = Bx.CompareTo(other.Bx);
            if (bx != 0)
                return bx;
            return By.CompareTo(other.By);
        }
    }

    public sealed class MapRibbonPolyline
    {
        public MapRibbonRole Role;
        public bool Closed;
        public float Extent;
        public List<double> Longitude = new List<double>();
        public List<double> Latitude = new List<double>();
    }

    public struct MapRibbonNearPair
    {
        public string CodeA;
        public string CodeB;
        public double Degrees;
        public MapRibbonEdgeKey KeyA;
        public MapRibbonEdgeKey KeyB;
    }

    public sealed class MapRibbonCensus
    {
        public int Features;
        public int Rings;
        public int HoleRings;
        public int SegmentInstances;
        public int UniqueKeys;
        public int TurkeySegments;
        public int CoastSegments;
        public int SharedBorderSegments;
        public int TurkeySharedWithNeighbor;
        public int SameCountryDuplicates;
        public int KeysSharedByThreeOrMore;
        public int UniqueHoleEdges;
        public int NearOverlapPairs;
        public int SmallCoastRings;
        public int MaxShareCount;
        public double MaxSegmentDegrees;
        public double BuildMilliseconds;
    }

    public sealed class MapRibbonModel
    {
        public MapRibbonCensus Census = new MapRibbonCensus();
        public List<MapRibbonPolyline> Coast = new List<MapRibbonPolyline>();
        public List<MapRibbonPolyline> Borders = new List<MapRibbonPolyline>();
        public List<MapRibbonPolyline> Turkey = new List<MapRibbonPolyline>();
        public HashSet<MapRibbonEdgeKey> CoastKeys = new HashSet<MapRibbonEdgeKey>();
        public HashSet<MapRibbonEdgeKey> BorderKeys = new HashSet<MapRibbonEdgeKey>();
        public HashSet<MapRibbonEdgeKey> TurkeyKeys = new HashSet<MapRibbonEdgeKey>();
        public List<MapRibbonNearPair> NearPairs = new List<MapRibbonNearPair>();
    }

    public static class MapRibbonBuilder
    {
        public const string SourceGeo = "Assets/Game/Map/Source/ne_50m_admin_0_countries.geojson";
        public const string ResourceFolder = "Assets/Game/Map/Resources";
        public const string ShaderAsset = "Assets/Game/Map/MapRibbonLine.shader";
        public const string CoastAsset = ResourceFolder + "/MapRibbonCoast.asset";
        public const string TurkeyAsset = ResourceFolder + "/MapRibbonTurkey.asset";
        public const string MaterialAsset = ResourceFolder + "/MapRibbonLine.mat";
        public const double NearOverlapDegrees = 0.002d;

        [MenuItem("LogistiCore/Map/Premium Lines/Generate Ribbon Assets")]
        public static void GenerateFromMenu()
        {
            Generate();
        }

        public static void GenerateRibbonAssets()
        {
            Generate();
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        public static void Generate()
        {
            var model = Build();
            var coastLines = new List<MapRibbonPolyline>(model.Coast.Count + model.Borders.Count);
            coastLines.AddRange(model.Coast);
            coastLines.AddRange(model.Borders);
            var coast = BuildMesh(coastLines, "MapRibbonCoast");
            var turkey = BuildMesh(model.Turkey, "MapRibbonTurkey");
            if (!AssetDatabase.IsValidFolder(ResourceFolder))
                throw new InvalidOperationException("Map Resources folder is missing.");

            SaveMesh(coast, CoastAsset);
            SaveMesh(turkey, TurkeyAsset);
            SaveMaterial();
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log(Describe(model, coast, turkey));
        }

        public static string Describe(MapRibbonModel model, Mesh coast, Mesh turkey)
        {
            var census = model.Census;
            return
                "features " + census.Features + "\n" +
                "rings " + census.Rings + "\n" +
                "holeRings " + census.HoleRings + "\n" +
                "segmentInstances " + census.SegmentInstances + "\n" +
                "uniqueKeys " + census.UniqueKeys + "\n" +
                "turkeySegments " + census.TurkeySegments + "\n" +
                "coastSegments " + census.CoastSegments + "\n" +
                "sharedBorders " + census.SharedBorderSegments + "\n" +
                "turkeyShared " + census.TurkeySharedWithNeighbor + "\n" +
                "sameCountryDuplicates " + census.SameCountryDuplicates + "\n" +
                "keysSharedByThreeOrMore " + census.KeysSharedByThreeOrMore + "\n" +
                "uniqueHoleEdges " + census.UniqueHoleEdges + "\n" +
                "nearOverlapPairs " + census.NearOverlapPairs + "\n" +
                "smallCoastRings " + census.SmallCoastRings + "\n" +
                "maxShareCount " + census.MaxShareCount + "\n" +
                "maxSegmentDegrees " + census.MaxSegmentDegrees.ToString("F4") + "\n" +
                "buildMs " + census.BuildMilliseconds.ToString("F1") + "\n" +
                "coastVertices " + (coast != null ? coast.vertexCount : 0) + "\n" +
                "coastIndices " + (coast != null ? coast.GetIndexCount(0) : 0) + "\n" +
                "turkeyVertices " + (turkey != null ? turkey.vertexCount : 0) + "\n" +
                "turkeyIndices " + (turkey != null ? turkey.GetIndexCount(0) : 0) + "\n" +
                "polylineCoast " + SegmentCount(model.Coast) + "\n" +
                "polylineBorders " + SegmentCount(model.Borders) + "\n" +
                "polylineTurkey " + SegmentCount(model.Turkey) + "\n" +
                NearText(model);

        static string NearText(MapRibbonModel model)
        {
            var text = "";
            for (var i = 0; i < model.NearPairs.Count; i++)
            {
                var pair = model.NearPairs[i];
                text += "near " + pair.CodeA + " " + pair.CodeB + " " + pair.Degrees.ToString("G6") + "\n";
            }

            return text;
        }
        }

        public static int SegmentCount(List<MapRibbonPolyline> lines)
        {
            var count = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                var points = lines[i].Longitude.Count;
                if (points < 2)
                    continue;
                count += lines[i].Closed ? points : points - 1;
            }

            return count;
        }

        public static int Fingerprint(MapRibbonModel model)
        {
            unchecked
            {
                var hash = 17;
                hash = Mix(hash, model.Coast);
                hash = Mix(hash, model.Borders);
                hash = Mix(hash, model.Turkey);
                return hash;
            }
        }

        public static MapRibbonModel Build()
        {
            var clock = Stopwatch.StartNew();
            var model = new MapRibbonModel();
            var root = JObject.Parse(File.ReadAllText(SourceGeo));
            var features = (JArray)root["features"];
            var edges = new Dictionary<MapRibbonEdgeKey, EdgeAcc>(90000);
            var rings = new List<Ring>(2000);

            for (var f = 0; f < features.Count; f++)
            {
                var feature = (JObject)features[f];
                var geometry = feature["geometry"];
                if (geometry == null || geometry.Type == JTokenType.Null)
                    continue;

                model.Census.Features++;
                var properties = feature["properties"] as JObject;
                var code = properties != null ? (string)properties["ADM0_A3"] : "";
                if (string.IsNullOrEmpty(code))
                    code = "?";
                var turkey = code == "TUR";
                var type = (string)geometry["type"];
                var coordinates = geometry["coordinates"] as JArray;
                if (coordinates == null)
                    continue;

                if (type == "Polygon")
                    AddPolygon(coordinates, code, turkey, edges, rings, model.Census);
                else if (type == "MultiPolygon")
                {
                    for (var p = 0; p < coordinates.Count; p++)
                    {
                        if (coordinates[p] is JArray polygon)
                            AddPolygon(polygon, code, turkey, edges, rings, model.Census);
                    }
                }
            }

            var borderEdges = new List<BorderEdge>(20000);
            var once = new List<OnceEdge>(60000);
            foreach (var pair in edges)
            {
                var acc = pair.Value;
                if (acc.Count > model.Census.MaxShareCount)
                    model.Census.MaxShareCount = acc.Count;
                var turkeySide = acc.CodeA == "TUR" || acc.CodeB == "TUR";
                if (acc.Count >= 2 && acc.Codes >= 2 && turkeySide)
                    model.Census.TurkeySharedWithNeighbor++;
                else if (acc.Count >= 2 && acc.Codes >= 2)
                {
                    model.Census.SharedBorderSegments++;
                    borderEdges.Add(new BorderEdge(pair.Key, acc));
                }
                else if (acc.Count >= 2)
                    model.Census.SameCountryDuplicates++;

                if (acc.Count >= 3)
                    model.Census.KeysSharedByThreeOrMore++;
                if (acc.Count == 1 && acc.Hole)
                    model.Census.UniqueHoleEdges++;
                if (acc.Count == 1 && acc.CodeA != "TUR")
                {
                    model.Census.CoastSegments++;
                    model.CoastKeys.Add(pair.Key);
                    once.Add(new OnceEdge(pair.Key, acc));
                }

                if (acc.CodeA == "TUR" || acc.CodeB == "TUR")
                    model.TurkeyKeys.Add(pair.Key);
            }

            model.Census.UniqueKeys = edges.Count;
            FindNearOverlaps(once, model);
            ChainBorders(borderEdges, model);
            EmitRings(rings, model);
            clock.Stop();
            model.Census.BuildMilliseconds = clock.Elapsed.TotalMilliseconds;
            model.Census.TurkeySegments = SegmentCount(model.Turkey);
            return model;
        }

        public static Mesh BuildMesh(List<MapRibbonPolyline> lines, string meshName)
        {
            var vertices = new List<Vector3>(lines.Count * 8);
            var colors = new List<Color32>(lines.Count * 8);
            var sideWidth = new List<Vector2>(lines.Count * 8);
            var prev = new List<Vector2>(lines.Count * 8);
            var next = new List<Vector2>(lines.Count * 8);
            var extentCap = new List<Vector2>(lines.Count * 8);
            var indices = new List<int>(lines.Count * 12);

            for (var i = 0; i < lines.Count; i++)
                Append(lines[i], vertices, colors, sideWidth, prev, next, extentCap, indices);

            var mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, sideWidth);
            mesh.SetUVs(1, prev);
            mesh.SetUVs(2, next);
            mesh.SetUVs(3, extentCap);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.Expand(4f);
            mesh.bounds = bounds;
            return mesh;
        }

        static void SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
                AssetDatabase.CreateAsset(mesh, path);
            else
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        static void SaveMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAsset);
            if (shader == null)
                throw new InvalidOperationException("Ribbon shader is missing.");

            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialAsset);
            if (existing != null)
            {
                if (existing.shader != shader)
                    existing.shader = shader;
                return;
            }

            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialAsset);
        }

        static void AddPolygon(
            JArray polygon,
            string code,
            bool turkey,
            Dictionary<MapRibbonEdgeKey, EdgeAcc> edges,
            List<Ring> rings,
            MapRibbonCensus census)
        {
            for (var r = 0; r < polygon.Count; r++)
            {
                if (polygon[r] is not JArray ring)
                    continue;

                var points = Clean(ring);
                if (points.Count < 2)
                    continue;

                census.Rings++;
                var hole = r > 0;
                if (hole)
                    census.HoleRings++;

                var closed = true;
                var parts = Split(points, closed);
                var extent = ExtentOf(points);
                if (!turkey && extent > 0f && extent < MapRibbonStyle.SmallRingWorld)
                    census.SmallCoastRings++;

                var stored = new Ring
                {
                    Code = code,
                    Turkey = turkey,
                    Extent = extent,
                    Parts = parts
                };
                rings.Add(stored);

                for (var p = 0; p < parts.Count; p++)
                {
                    var part = parts[p];
                    var count = part.Points.Count;
                    var segments = part.Closed ? count : count - 1;
                    for (var s = 0; s < segments; s++)
                    {
                        var a = part.Points[s];
                        var b = part.Points[(s + 1) % count];
                        if (Math.Abs(a.Lon - b.Lon) < 1e-12 && Math.Abs(a.Lat - b.Lat) < 1e-12)
                            continue;

                        var length = Math.Sqrt((a.Lon - b.Lon) * (a.Lon - b.Lon) + (a.Lat - b.Lat) * (a.Lat - b.Lat));
                        if (length > census.MaxSegmentDegrees)
                            census.MaxSegmentDegrees = length;

                        census.SegmentInstances++;
                        var key = new MapRibbonEdgeKey(a.Lon, a.Lat, b.Lon, b.Lat);
                        if (!edges.TryGetValue(key, out var acc))
                        {
                            acc = new EdgeAcc();
                            edges.Add(key, acc);
                        }

                        acc.Add(code, hole, a.Lon, a.Lat, b.Lon, b.Lat);
                    }
                }
            }
        }

        static List<LonLat> Clean(JArray ring)
        {
            var count = ring.Count;
            if (count >= 2 && SameToken(ring[0], ring[count - 1]))
                count--;

            var points = new List<LonLat>(count);
            for (var i = 0; i < count; i++)
            {
                if (ring[i] is not JArray point || point.Count < 2)
                    continue;

                var lon = point[0].Value<double>();
                var lat = point[1].Value<double>();
                if (points.Count > 0)
                {
                    var previous = points[points.Count - 1];
                    if (Math.Abs(previous.Lon - lon) < 1e-9 && Math.Abs(previous.Lat - lat) < 1e-9)
                        continue;
                }

                points.Add(new LonLat(lon, lat));
            }

            return points;
        }

        static bool SameToken(JToken a, JToken b)
        {
            var left = (JArray)a;
            var right = (JArray)b;
            return Math.Abs(left[0].Value<double>() - right[0].Value<double>()) < 1e-9 &&
                   Math.Abs(left[1].Value<double>() - right[1].Value<double>()) < 1e-9;
        }

        static List<Part> Split(List<LonLat> points, bool closed)
        {
            var parts = new List<Part>();
            var current = new List<LonLat> { points[0] };
            var split = false;
            var count = points.Count;
            var segments = closed ? count : count - 1;
            for (var i = 0; i < segments; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % count];
                var delta = b.Lon - a.Lon;
                if (delta > 180d || delta < -180d)
                {
                    split = true;
                    var unwrapped = delta > 180d ? b.Lon - 360d : b.Lon + 360d;
                    var span = unwrapped - a.Lon;
                    if (Math.Abs(span) < 1e-9)
                        continue;

                    var boundary = a.Lon < unwrapped ? 180d : -180d;
                    var t = (boundary - a.Lon) / span;
                    if (t < 0d)
                        t = 0d;
                    if (t > 1d)
                        t = 1d;

                    var latitude = a.Lat + (b.Lat - a.Lat) * t;
                    AddPoint(current, boundary, latitude);
                    if (current.Count >= 2)
                        parts.Add(new Part(current, false));
                    current = new List<LonLat>();
                    AddPoint(current, -boundary, latitude);
                    AddPoint(current, b.Lon, b.Lat);
                }
                else
                {
                    AddPoint(current, b.Lon, b.Lat);
                }
            }

            if (closed && !split && current.Count >= 2)
            {
                var first = current[0];
                var last = current[current.Count - 1];
                if (Math.Abs(first.Lon - last.Lon) < 1e-9 && Math.Abs(first.Lat - last.Lat) < 1e-9)
                    current.RemoveAt(current.Count - 1);
            }

            if (current.Count >= 2)
                parts.Add(new Part(current, closed && !split));
            return parts;
        }

        static void AddPoint(List<LonLat> points, double lon, double lat)
        {
            if (points.Count > 0)
            {
                var last = points[points.Count - 1];
                if (Math.Abs(last.Lon - lon) < 1e-12 && Math.Abs(last.Lat - lat) < 1e-12)
                    return;
            }

            points.Add(new LonLat(lon, lat));
        }

        static float ExtentOf(List<LonLat> points)
        {
            var any = false;
            var minX = 0f;
            var minY = 0f;
            var maxX = 0f;
            var maxY = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                if (!MapProjection.TryProjectUnwrapped(points[i].Lat, points[i].Lon, out var world))
                    continue;
                if (!any)
                {
                    minX = maxX = world.x;
                    minY = maxY = world.y;
                    any = true;
                }
                else
                {
                    if (world.x < minX) minX = world.x;
                    if (world.y < minY) minY = world.y;
                    if (world.x > maxX) maxX = world.x;
                    if (world.y > maxY) maxY = world.y;
                }
            }

            if (!any)
                return 0f;
            var dx = maxX - minX;
            var dy = maxY - minY;
            return dx > dy ? dx : dy;
        }

        static void EmitRings(List<Ring> rings, MapRibbonModel model)
        {
            for (var i = 0; i < rings.Count; i++)
            {
                var ring = rings[i];
                for (var p = 0; p < ring.Parts.Count; p++)
                {
                    var part = ring.Parts[p];
                    if (ring.Turkey)
                    {
                        AddPolyline(model.Turkey, model.TurkeyKeys, part, ring.Extent, MapRibbonRole.Turkey);
                        continue;
                    }

                    EmitKept(model, part, ring.Extent);
                }
            }
        }

        static void EmitKept(MapRibbonModel model, Part part, float extent)
        {
            var count = part.Points.Count;
            var segments = part.Closed ? count : count - 1;
            if (segments <= 0)
                return;

            var keep = new bool[segments];
            var kept = 0;
            for (var s = 0; s < segments; s++)
            {
                var a = part.Points[s];
                var b = part.Points[(s + 1) % count];
                var key = new MapRibbonEdgeKey(a.Lon, a.Lat, b.Lon, b.Lat);
                keep[s] = model.CoastKeys.Contains(key);
                if (keep[s])
                    kept++;
            }

            if (kept == 0)
                return;
            if (part.Closed && kept == segments)
            {
                AddPolyline(model.Coast, null, part, extent, MapRibbonRole.Coast);
                return;
            }

            var start = 0;
            var span = segments;
            if (part.Closed)
            {
                for (var s = 0; s < segments; s++)
                {
                    if (!keep[s])
                    {
                        start = (s + 1) % segments;
                        break;
                    }
                }
            }

            List<LonLat> run = null;
            for (var k = 0; k < span; k++)
            {
                var s = part.Closed ? (start + k) % segments : k;
                if (keep[s])
                {
                    if (run == null)
                    {
                        run = new List<LonLat>();
                        run.Add(part.Points[s]);
                    }

                    run.Add(part.Points[(s + 1) % count]);
                }
                else if (run != null)
                {
                    AddPolyline(model.Coast, null, new Part(run, false), extent, MapRibbonRole.Coast);
                    run = null;
                }
            }

            if (run != null)
                AddPolyline(model.Coast, null, new Part(run, false), extent, MapRibbonRole.Coast);
        }

        static void AddPolyline(
            List<MapRibbonPolyline> destination,
            HashSet<MapRibbonEdgeKey> keys,
            Part part,
            float extent,
            MapRibbonRole role)
        {
            if (part.Points.Count < 2)
                return;

            var line = new MapRibbonPolyline
            {
                Role = role,
                Closed = part.Closed,
                Extent = role == MapRibbonRole.Coast ? extent : 0f
            };
            for (var i = 0; i < part.Points.Count; i++)
            {
                line.Longitude.Add(part.Points[i].Lon);
                line.Latitude.Add(part.Points[i].Lat);
            }

            if (keys != null)
            {
                var count = part.Points.Count;
                var segments = part.Closed ? count : count - 1;
                for (var s = 0; s < segments; s++)
                {
                    var a = part.Points[s];
                    var b = part.Points[(s + 1) % count];
                    keys.Add(new MapRibbonEdgeKey(a.Lon, a.Lat, b.Lon, b.Lat));
                }
            }

            destination.Add(line);
        }

        static void ChainBorders(List<BorderEdge> borders, MapRibbonModel model)
        {
            var adjacency = new Dictionary<long, List<int>>(borders.Count);
            for (var i = 0; i < borders.Count; i++)
            {
                AddAdj(adjacency, borders[i].PackA, i);
                AddAdj(adjacency, borders[i].PackB, i);
                model.BorderKeys.Add(borders[i].Key);
            }

            var order = new int[borders.Count];
            for (var i = 0; i < order.Length; i++)
                order[i] = i;
            Array.Sort(order, (a, b) => borders[a].Key.CompareTo(borders[b].Key));

            var visited = new bool[borders.Count];
            for (var o = 0; o < order.Length; o++)
            {
                var index = order[o];
                if (visited[index])
                    continue;

                var edge = borders[index];
                var degreeA = adjacency[edge.PackA].Count;
                var degreeB = adjacency[edge.PackB].Count;
                List<LonLat> points;
                if (degreeA != 2)
                    points = Walk(borders, adjacency, visited, index, edge.PackA);
                else if (degreeB != 2)
                    points = Walk(borders, adjacency, visited, index, edge.PackB);
                else
                {
                    points = Walk(borders, adjacency, visited, index, edge.PackA);
                    var back = FirstOpen(adjacency, visited, edge.PackA);
                    if (back >= 0)
                    {
                        var other = Walk(borders, adjacency, visited, back, edge.PackA);
                        other.Reverse();
                        if (other.Count > 0)
                            other.RemoveAt(other.Count - 1);
                        other.AddRange(points);
                        points = other;
                    }
                }

                var closed = points.Count >= 4 && Same(points[0], points[points.Count - 1]);
                if (closed)
                    points.RemoveAt(points.Count - 1);
                if (points.Count >= 2)
                    AddPolyline(model.Borders, null, new Part(points, closed), 0f, MapRibbonRole.Border);
            }
        }

        static List<LonLat> Walk(
            List<BorderEdge> borders,
            Dictionary<long, List<int>> adjacency,
            bool[] visited,
            int first,
            long start)
        {
            var points = new List<LonLat>();
            var edgeIndex = first;
            var at = start;
            var guard = 0;
            while (edgeIndex >= 0 && !visited[edgeIndex] && guard++ < borders.Count + 2)
            {
                visited[edgeIndex] = true;
                var edge = borders[edgeIndex];
                var other = edge.PackA == at ? edge.PackB : edge.PackA;
                if (points.Count == 0)
                    points.Add(edge.Point(at));
                points.Add(edge.Point(other));
                if (!adjacency.TryGetValue(other, out var nexts) || nexts.Count != 2)
                    break;

                var next = FirstOpen(adjacency, visited, other);
                if (next < 0)
                    break;
                edgeIndex = next;
                at = other;
            }

            return points;
        }

        static int FirstOpen(Dictionary<long, List<int>> adjacency, bool[] visited, long pack)
        {
            if (!adjacency.TryGetValue(pack, out var list))
                return -1;
            for (var i = 0; i < list.Count; i++)
            {
                if (!visited[list[i]])
                    return list[i];
            }

            return -1;
        }

        static void AddAdj(Dictionary<long, List<int>> adjacency, long pack, int index)
        {
            if (!adjacency.TryGetValue(pack, out var list))
            {
                list = new List<int>(2);
                adjacency.Add(pack, list);
            }

            list.Add(index);
        }

        static void FindNearOverlaps(List<OnceEdge> once, MapRibbonModel model)
        {
            const double cell = 0.5d;
            var grid = new Dictionary<long, List<int>>(once.Count);
            for (var i = 0; i < once.Count; i++)
            {
                AddCell(grid, once[i].Lon0, once[i].Lat0, cell, i);
                AddCell(grid, once[i].Lon1, once[i].Lat1, cell, i);
            }

            var seen = new HashSet<long>();
            for (var i = 0; i < once.Count; i++)
            {
                var edge = once[i];
                var minX = Math.Min(edge.Lon0, edge.Lon1) - cell;
                var maxX = Math.Max(edge.Lon0, edge.Lon1) + cell;
                var minY = Math.Min(edge.Lat0, edge.Lat1) - cell;
                var maxY = Math.Max(edge.Lat0, edge.Lat1) + cell;
                for (var x = Math.Floor(minX / cell); x <= Math.Floor(maxX / cell); x++)
                {
                    for (var y = Math.Floor(minY / cell); y <= Math.Floor(maxY / cell); y++)
                    {
                        if (!grid.TryGetValue(PackCell(x, y), out var bucket))
                            continue;
                        for (var b = 0; b < bucket.Count; b++)
                        {
                            var j = bucket[b];
                            if (j <= i)
                                continue;
                            var other = once[j];
                            if (other.Code == edge.Code)
                                continue;
                            var id = ((long)i << 32) | (uint)j;
                            if (!seen.Add(id))
                                continue;
                            var gap = Separation(edge, other);
                            if (gap >= NearOverlapDegrees)
                                continue;

                            model.NearPairs.Add(new MapRibbonNearPair
                            {
                                CodeA = edge.Code,
                                CodeB = other.Code,
                                Degrees = gap,
                                KeyA = edge.Key,
                                KeyB = other.Key
                            });
                        }
                    }
                }
            }

            model.Census.NearOverlapPairs = model.NearPairs.Count;
        }

        static double Separation(OnceEdge a, OnceEdge b)
        {
            var d0 = DistanceToSegment(a.Lon0, a.Lat0, b.Lon0, b.Lat0, b.Lon1, b.Lat1);
            var d1 = DistanceToSegment(a.Lon1, a.Lat1, b.Lon0, b.Lat0, b.Lon1, b.Lat1);
            var e0 = DistanceToSegment(b.Lon0, b.Lat0, a.Lon0, a.Lat0, a.Lon1, a.Lat1);
            var e1 = DistanceToSegment(b.Lon1, b.Lat1, a.Lon0, a.Lat0, a.Lon1, a.Lat1);
            return Math.Max(Math.Max(d0, d1), Math.Max(e0, e1));
        }

        static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
        {
            var vx = bx - ax;
            var vy = by - ay;
            var length = vx * vx + vy * vy;
            if (length < 1e-18)
                return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));

            var t = ((px - ax) * vx + (py - ay) * vy) / length;
            if (t < 0d)
                t = 0d;
            if (t > 1d)
                t = 1d;
            var x = ax + t * vx;
            var y = ay + t * vy;
            return Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
        }

        static void AddCell(Dictionary<long, List<int>> grid, double lon, double lat, double cell, int index)
        {
            var key = PackCell(Math.Floor(lon / cell), Math.Floor(lat / cell));
            if (!grid.TryGetValue(key, out var list))
            {
                list = new List<int>(4);
                grid.Add(key, list);
            }

            list.Add(index);
        }

        static long PackCell(double x, double y)
        {
            return ((long)x + 10000L) << 32 | (uint)((long)y + 10000L);
        }

        static long PackEndpoint(int x, int y)
        {
            return ((long)(x + 200000000) << 32) | (uint)(y + 200000000);
        }

        static void Append(
            MapRibbonPolyline line,
            List<Vector3> vertices,
            List<Color32> colors,
            List<Vector2> sideWidth,
            List<Vector2> prev,
            List<Vector2> next,
            List<Vector2> extentCap,
            List<int> indices)
        {
            var projected = new List<Vector2>(line.Longitude.Count);
            for (var i = 0; i < line.Longitude.Count; i++)
            {
                if (!MapProjection.TryProjectUnwrapped(line.Latitude[i], line.Longitude[i], out var world))
                    continue;
                if (projected.Count > 0 && projected[projected.Count - 1] == world)
                    continue;
                projected.Add(world);
            }

            if (line.Closed && projected.Count >= 2 && projected[0] == projected[projected.Count - 1])
                projected.RemoveAt(projected.Count - 1);
            if (projected.Count < 2)
                return;

            var count = projected.Count;
            var closed = line.Closed && count >= 3;
            var color = MapRibbonStyle.Color(line.Role);
            var width = MapRibbonStyle.Width(line.Role);
            var extent = line.Role == MapRibbonRole.Coast ? line.Extent : 0f;
            var vertexBase = vertices.Count;
            for (var i = 0; i < count; i++)
            {
                Vector2 previous;
                Vector2 following;
                if (closed)
                {
                    previous = projected[(i - 1 + count) % count];
                    following = projected[(i + 1) % count];
                }
                else if (i == 0)
                {
                    previous = projected[0];
                    following = projected[1];
                }
                else if (i == count - 1)
                {
                    previous = projected[count - 2];
                    following = projected[count - 1];
                }
                else
                {
                    previous = projected[i - 1];
                    following = projected[i + 1];
                }

                var point = projected[i];
                for (var side = -1; side <= 1; side += 2)
                {
                    vertices.Add(new Vector3(point.x, point.y, 0f));
                    colors.Add(color);
                    sideWidth.Add(new Vector2(side, width));
                    prev.Add(previous);
                    next.Add(following);
                    extentCap.Add(new Vector2(extent, (float)line.Role));
                }
            }

            var quads = closed ? count : count - 1;
            for (var s = 0; s < quads; s++)
            {
                var a = vertexBase + s * 2;
                var b = vertexBase + (closed ? (s + 1) % count : s + 1) * 2;
                indices.Add(a);
                indices.Add(a + 1);
                indices.Add(b);
                indices.Add(a + 1);
                indices.Add(b + 1);
                indices.Add(b);
            }
        }

        static int Mix(int hash, List<MapRibbonPolyline> lines)
        {
            unchecked
            {
                for (var i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    hash = hash * 31 + line.Longitude.Count;
                    hash = hash * 31 + (line.Closed ? 1 : 0);
                    hash = hash * 31 + (int)line.Role;
                    if (line.Longitude.Count > 0)
                    {
                        hash = hash * 31 + line.Longitude[0].GetHashCode();
                        hash = hash * 31 + line.Latitude[line.Latitude.Count - 1].GetHashCode();
                    }
                }

                return hash;
            }
        }

        static bool Same(LonLat a, LonLat b)
        {
            return Math.Abs(a.Lon - b.Lon) < 1e-9 && Math.Abs(a.Lat - b.Lat) < 1e-9;
        }

        struct LonLat
        {
            public readonly double Lon;
            public readonly double Lat;

            public LonLat(double lon, double lat)
            {
                Lon = lon;
                Lat = lat;
            }
        }

        sealed class Part
        {
            public readonly List<LonLat> Points;
            public readonly bool Closed;

            public Part(List<LonLat> points, bool closed)
            {
                Points = points;
                Closed = closed;
            }
        }

        sealed class Ring
        {
            public string Code;
            public bool Turkey;
            public float Extent;
            public List<Part> Parts;
        }

        sealed class EdgeAcc
        {
            public int Count;
            public int Codes = 1;
            public string CodeA;
            public string CodeB;
            public bool Hole;
            public double Lon0;
            public double Lat0;
            public double Lon1;
            public double Lat1;

            public void Add(string code, bool hole, double lon0, double lat0, double lon1, double lat1)
            {
                if (Count == 0)
                {
                    CodeA = code;
                    Lon0 = lon0;
                    Lat0 = lat0;
                    Lon1 = lon1;
                    Lat1 = lat1;
                }
                else if (code != CodeA && code != CodeB)
                {
                    CodeB = code;
                    Codes++;
                }

                if (hole)
                    Hole = true;
                Count++;
            }
        }

        readonly struct OnceEdge
        {
            public readonly MapRibbonEdgeKey Key;
            public readonly string Code;
            public readonly double Lon0;
            public readonly double Lat0;
            public readonly double Lon1;
            public readonly double Lat1;

            public OnceEdge(MapRibbonEdgeKey key, EdgeAcc acc)
            {
                Key = key;
                Code = acc.CodeA;
                Lon0 = acc.Lon0;
                Lat0 = acc.Lat0;
                Lon1 = acc.Lon1;
                Lat1 = acc.Lat1;
            }
        }

        readonly struct BorderEdge
        {
            public readonly MapRibbonEdgeKey Key;
            public readonly long PackA;
            public readonly long PackB;
            public readonly double Lon0;
            public readonly double Lat0;
            public readonly double Lon1;
            public readonly double Lat1;

            public BorderEdge(MapRibbonEdgeKey key, EdgeAcc acc)
            {
                Key = key;
                Lon0 = acc.Lon0;
                Lat0 = acc.Lat0;
                Lon1 = acc.Lon1;
                Lat1 = acc.Lat1;
                PackA = PackEndpoint(MapRibbonEdgeKey.Quantize(acc.Lon0), MapRibbonEdgeKey.Quantize(acc.Lat0));
                PackB = PackEndpoint(MapRibbonEdgeKey.Quantize(acc.Lon1), MapRibbonEdgeKey.Quantize(acc.Lat1));
            }

            public LonLat Point(long pack)
            {
                if (pack == PackA)
                    return new LonLat(Lon0, Lat0);
                return new LonLat(Lon1, Lat1);
            }
        }
    }
}
