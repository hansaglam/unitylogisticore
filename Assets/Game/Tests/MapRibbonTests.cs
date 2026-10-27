using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using LogistiCore.EditorTools;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogistiCore.Tests
{
    public sealed class MapRibbonTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "c4f58b0ce718ff18835f6ee6b9232dfd57c71206efdc66990e1657dbdbef979b";
        const string CoastSha = "7ff32b1602c96856d829f6e15d22e53e18990b42dd24dc442b3b3641af5e5d0b";
        const string TurkeyLineSha = "ac3379a85ed41f9d35d2d08ef3d483d6f4abb37818b514fed36b36759d8ba8cb";
        const string LandSha = "3fd92747aa7a3c45772e1377928d6a0b969311a85fa4e75a9efdcfba603be463";
        const string ReliefSha = "41d7f88993bab845c8345b40f23adfbf73e56b563db56b1ffff6d6109734a570";
        const string RegionSha = "ac9c14157f50b3d2516994a5108cdfcf643f88d8f79bb905700795961616266d";
        const string ReliefShaderSha = "bc70b0be5fac1a73aaa28c97cbde1e0d8cb4aad3831908e4e9950ef9869adc8c";
        const double CapeLatitude = -55.784766;
        const double CapeLongitude = -67.325293;

        static MapRibbonModel _model;

        [OneTimeSetUp]
        public void BuildOnce()
        {
            _model = MapRibbonBuilder.Build();
        }

        [Test]
        public void Census_MatchesTheSourceRings()
        {
            var census = _model.Census;
            Assert.AreEqual(97981, census.SegmentInstances);
            Assert.AreEqual(78718, census.UniqueKeys);
            Assert.AreEqual(559, census.TurkeySegments);
            Assert.AreEqual(59151, census.CoastSegments);
            Assert.AreEqual(19008, census.SharedBorderSegments);
            Assert.AreEqual(255, census.TurkeySharedWithNeighbor);
            Assert.AreEqual(0, census.SameCountryDuplicates);
            Assert.AreEqual(0, census.KeysSharedByThreeOrMore);
            Assert.AreEqual(2, census.MaxShareCount);
            Assert.AreEqual(12, census.HoleRings);
            Assert.AreEqual(0, census.UniqueHoleEdges);
            Assert.Less(census.MaxSegmentDegrees, 8d);
            Assert.Greater(_model.Census.SmallCoastRings, 0);
        }

        [Test]
        public void Dedup_IsDeterministic()
        {
            var again = MapRibbonBuilder.Build();
            Assert.AreEqual(MapRibbonBuilder.Fingerprint(_model), MapRibbonBuilder.Fingerprint(again));
            Assert.AreEqual(_model.Census.SharedBorderSegments, again.Census.SharedBorderSegments);
            Assert.AreEqual(_model.Census.CoastSegments, again.Census.CoastSegments);
            Assert.AreEqual(_model.Census.TurkeySegments, again.Census.TurkeySegments);
            Assert.AreEqual(_model.NearPairs.Count, again.NearPairs.Count);
        }

        [Test]
        public void SharedBorders_AreRenderedOnce()
        {
            Assert.AreEqual(_model.BorderKeys.Count, MapRibbonBuilder.SegmentCount(_model.Borders));
            Assert.AreEqual(19008, _model.BorderKeys.Count);
            Assert.AreEqual(0, CountOverlap(_model.BorderKeys, _model.CoastKeys));
            Assert.AreEqual(0, CountOverlap(_model.BorderKeys, _model.TurkeyKeys));
            Assert.AreEqual(0, CountOverlap(_model.CoastKeys, _model.TurkeyKeys));
            Assert.AreEqual(
                _model.CoastKeys.Count + _model.BorderKeys.Count + _model.TurkeyKeys.Count,
                _model.Census.UniqueKeys);
        }

        [Test]
        public void TurkeyOutline_KeepsAll559Segments()
        {
            Assert.AreEqual(559, MapRibbonBuilder.SegmentCount(_model.Turkey));
            Assert.AreEqual(559, _model.TurkeyKeys.Count);
            Assert.AreEqual(59151, MapRibbonBuilder.SegmentCount(_model.Coast));
        }

        [Test]
        public void NearMisses_StayInTheCoastAndAreNotMerged()
        {
            Assert.AreEqual(2, _model.NearPairs.Count);
            var codes = new HashSet<string>();
            for (var i = 0; i < _model.NearPairs.Count; i++)
            {
                var pair = _model.NearPairs[i];
                codes.Add(pair.CodeA);
                codes.Add(pair.CodeB);
                Assert.Less(pair.Degrees, 0.002d);
                Assert.Greater(pair.Degrees, 0d);
                Assert.IsTrue(_model.CoastKeys.Contains(pair.KeyA));
                Assert.IsTrue(_model.CoastKeys.Contains(pair.KeyB));
                Assert.IsFalse(_model.BorderKeys.Contains(pair.KeyA));
            }

            Assert.IsTrue(codes.Contains("SYR"));
            Assert.IsTrue(codes.Contains("ISR"));
            Assert.IsTrue(codes.Contains("LBN"));
        }

        [Test]
        public void Centerline_MatchesProjectedSourceAndTheBakedLines()
        {
            var turkeyMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/Map/Generated/MapTurkeyCoast.asset");
            var coastMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/Map/Generated/MapCoastlines.asset");
            Assert.AreEqual(1118, turkeyMesh.vertexCount);
            Assert.AreEqual(194844, coastMesh.vertexCount);

            var turkeyBaked = Pairs(turkeyMesh.vertices);
            var coastBaked = Pairs(coastMesh.vertices);
            Assert.AreEqual(559, turkeyBaked.Count);
            // 78,414 geographic edges in the baked coast mesh. Seven of them share a
            // projected float pair with another edge, so the unique float pairs are 78,407.
            Assert.AreEqual(78407, coastBaked.Count);
            Assert.AreEqual(559, CountMatched(_model.Turkey, turkeyBaked));
            Assert.AreEqual(59151, CountMatched(_model.Coast, coastBaked));
            Assert.AreEqual(19008, CountMatched(_model.Borders, coastBaked));
            AssertLongestProjectedSegment();
        }

        [Test]
        public void CapeHorn_AndSmallIslands_Remain()
        {
            Assert.IsTrue(ContainsPoint(_model.Coast, CapeLongitude, CapeLatitude, 0.0002d));
            Assert.IsTrue(ContainsPoint(_model.Coast, -67.266d, -55.983d, 0.35d));
            var small = 0;
            for (var i = 0; i < _model.Coast.Count; i++)
            {
                if (_model.Coast[i].Extent > 0f && _model.Coast[i].Extent < MapRibbonStyle.SmallRingWorld)
                    small++;
            }

            Assert.Greater(small, 0);
        }

        [Test]
        public void RibbonMesh_IsTrianglesAndDoesNotMoveWhenZoomChanges()
        {
            var lines = new List<MapRibbonPolyline> { _model.Turkey[0] };
            var mesh = MapRibbonBuilder.BuildMesh(lines, "zoom");
            Assert.AreEqual(MeshTopology.Triangles, mesh.GetTopology(0));
            Assert.AreEqual(IndexFormat.UInt32, mesh.indexFormat);
            var before = mesh.vertices;
            var shader = Shader.Find("LogistiCore/Map/RibbonLine");
            var coastFilter = NewFilter("Coastlines");
            var turkeyFilter = NewFilter("TurkeyCoast");
            var presenter = coastFilter.gameObject.AddComponent<MapRibbonPresenter>();
            var material = new Material(shader);
            presenter.Bind(coastFilter, turkeyFilter, mesh, mesh, material);
            Assert.IsTrue(presenter.ShowPremium());
            presenter.ApplyZoom(12.031f);
            presenter.ApplyZoom(223.928f);
            var after = mesh.vertices;
            Assert.AreEqual(before.Length, after.Length);
            for (var i = 0; i < before.Length; i++)
                Assert.AreEqual(before[i], after[i]);

            presenter.Release();
            UnityEngine.Object.DestroyImmediate(presenter);
            UnityEngine.Object.DestroyImmediate(coastFilter.gameObject);
            UnityEngine.Object.DestroyImmediate(turkeyFilter.gameObject);
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void OrthographicCallback_UsesOnlyTheMapCamera()
        {
            var mapObject = new GameObject("map-camera");
            var mapCamera = mapObject.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = 12.031f;
            var otherObject = new GameObject("scene-camera");
            var otherCamera = otherObject.AddComponent<Camera>();
            otherCamera.orthographic = true;
            otherCamera.orthographicSize = 223.928f;
            var coast = NewFilter("Coastlines");
            var turkey = NewFilter("TurkeyCoast");
            var presenter = mapObject.AddComponent<MapRibbonPresenter>();
            var material = new Material(Shader.Find("LogistiCore/Map/RibbonLine"));
            try
            {
                presenter.Bind(coast, turkey, coast.sharedMesh, turkey.sharedMesh, material);
                Assert.IsTrue(presenter.ShowPremium());
                presenter.ApplyZoomFromCamera(otherCamera);
                presenter.ApplyZoomFromCamera(null);
                Assert.AreEqual(0f, material.GetFloat("_OrthoSize"), 0.0001f);
                presenter.ApplyZoomFromCamera(mapCamera);
                Assert.AreEqual(12.031f, material.GetFloat("_OrthoSize"), 0.0001f);
                otherCamera.orthographicSize = 80f;
                presenter.ApplyZoomFromCamera(otherCamera);
                Assert.AreEqual(12.031f, material.GetFloat("_OrthoSize"), 0.0001f);
            }
            finally
            {
                presenter.Release();
                UnityEngine.Object.DestroyImmediate(mapObject);
                UnityEngine.Object.DestroyImmediate(otherObject);
                UnityEngine.Object.DestroyImmediate(coast.gameObject);
                UnityEngine.Object.DestroyImmediate(turkey.gameObject);
            }
        }

        [Test]
        public void ScreenPixelWidth_DoesNotDependOnAspect()
        {
            var vertical = new Vector2(0f, MapRibbonStyle.CoastWidthPixels);
            var horizontal = new Vector2(MapRibbonStyle.TurkeyWidthPixels, 0f);
            AssertRoundTrip(vertical, 1920f, 1080f);
            AssertRoundTrip(horizontal, 1920f, 1080f);
            AssertRoundTrip(vertical, 844f, 390f);
            AssertRoundTrip(horizontal, 844f, 390f);
            Assert.AreEqual(1.5f, MapRibbonStyle.Width(MapRibbonRole.Coast));
            Assert.AreEqual(1.15f, MapRibbonStyle.Width(MapRibbonRole.Border));
            Assert.AreEqual(2f, MapRibbonStyle.Width(MapRibbonRole.Turkey));
            Assert.AreEqual(0f, MapRibbonStyle.SmallRingAlpha(0.2f, 223.928f), 0.0001f);
            Assert.AreEqual(1f, MapRibbonStyle.SmallRingAlpha(0.2f, 12.031f), 0.0001f);
            Assert.AreEqual(1f, MapRibbonStyle.SmallRingAlpha(10f, 223.928f), 0.0001f);
            Assert.AreEqual(1f, MapRibbonStyle.SmallRingAlpha(0f, 223.928f), 0.0001f);
        }

        [Test]
        public void Shader_CompilesForThe2DRenderer()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Map/MapRibbonLine.shader");
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
            var source = File.ReadAllText("Assets/Game/Map/MapRibbonLine.shader");
            Assert.IsTrue(source.Contains("SRPDefaultUnlit"));
            Assert.IsTrue(source.Contains("_ScreenParams"));
            Assert.IsFalse(source.Contains("#pragma geometry"));
            Assert.IsFalse(source.Contains("#pragma compute"));
            Assert.IsFalse(source.Contains("SV_PrimitiveID"));
        }

        [Test]
        public void HardwareLineFallback_RestoresTheOriginalRenderer()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var coast = FindFilter(scene, "Coastlines");
                var turkey = FindFilter(scene, "TurkeyCoast");
                var coastRenderer = coast.GetComponent<MeshRenderer>();
                var originalCoast = coast.sharedMesh;
                var originalTurkey = turkey.sharedMesh;
                var originalCoastMaterial = coastRenderer.sharedMaterial;
                var ribbon = new Mesh { name = "ribbon-fallback" };
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Map/MapRibbonLine.shader");
                var material = new Material(shader);
                var presenter = coast.gameObject.AddComponent<MapRibbonPresenter>();
                presenter.Bind(coast, turkey, ribbon, ribbon, material);
                Assert.AreEqual(originalCoast, coast.sharedMesh);
                Assert.IsTrue(presenter.ShowPremium());
                Assert.AreEqual(ribbon, coast.sharedMesh);
                Assert.AreEqual(ribbon, turkey.sharedMesh);
                presenter.ShowOriginal();
                Assert.AreEqual(originalCoast, coast.sharedMesh);
                Assert.AreEqual(originalTurkey, turkey.sharedMesh);
                Assert.AreEqual(originalCoastMaterial, coastRenderer.sharedMaterial);
                Assert.IsFalse(presenter.ShowingPremium);
                presenter.Release();
                UnityEngine.Object.DestroyImmediate(presenter);
                UnityEngine.Object.DestroyImmediate(ribbon);
                Assert.AreEqual(originalCoast, coast.sharedMesh);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void PreservedAssets_KeepTheirHashesAndTheCameraContract()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            Assert.AreEqual("8d5f07398f83c446f987c9b2827cff18", SceneGuid());
            Assert.AreEqual(CoastSha, Sha("Assets/Game/Map/Generated/MapCoastlines.asset"));
            Assert.AreEqual(TurkeyLineSha, Sha("Assets/Game/Map/Generated/MapTurkeyCoast.asset"));
            Assert.AreEqual(LandSha, Sha("Assets/Game/Map/Generated/MapLandFill.asset"));
            Assert.AreEqual(ReliefSha, Sha("Assets/Game/Map/Generated/MapRelief.png"));
            Assert.AreEqual(RegionSha, Sha("Assets/Game/Map/Resources/MapReliefRegion.png"));
            Assert.AreEqual(ReliefShaderSha, Sha("Assets/Game/Map/MapRegionalRelief.shader"));
            var land = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/Map/Generated/MapLandFill.asset");
            Assert.AreEqual(97087, land.vertexCount);
            Assert.AreEqual(281523, land.GetIndexCount(0));
            Assert.AreEqual(-58d, MapFrames.WorldPresentationMinLatitude);
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame);
            Assert.AreEqual(26f, MapMarkerLod.JoinPixels);
            Assert.AreEqual(44f, MapMarkerLod.SplitPixels);
            Assert.AreEqual(26f, MapMarkerLod.AggregateScreenPixels);
            MapFrames.World(out var min, out _);
            Assert.IsTrue(MapProjection.TryUnproject(min, out var south, out _));
            Assert.Less(south, -80d);
        }

        [Test]
        public void BorderFade_FollowsCameraScale()
        {
            Assert.AreEqual(1f, MapRibbonStyle.BorderFade(12.031f), 0.001f);
            Assert.AreEqual(1f, MapRibbonStyle.BorderFade(9.883f), 0.001f);
            Assert.AreEqual(MapRibbonStyle.BorderFadeFloor, MapRibbonStyle.BorderFade(223.928f), 0.001f);
            Assert.Greater(MapRibbonStyle.BorderFade(60f), MapRibbonStyle.BorderFade(160f));
            Assert.AreEqual(1f, MapRibbonStyle.SmallRingAlpha(10f, 223.928f), 0.001f);
            Assert.AreEqual(0f, MapRibbonStyle.SmallRingAlpha(0.2f, 223.928f), 0.001f);
            Assert.AreEqual(1f, MapRibbonStyle.SmallRingAlpha(0.2f, 12f), 0.001f);
            var source = File.ReadAllText("Assets/Game/Map/MapRibbonLine.shader");
            Assert.IsTrue(source.Contains("_BorderFloor"));
            Assert.IsTrue(source.Contains("input.role > 0.5 && input.role < 1.5"));
            Assert.IsTrue(source.Contains("input.role > 1.5"));
            Assert.IsTrue(source.Contains("_TurkeyMul"));
            Assert.AreEqual(2f, MapRibbonStyle.TurkeyWidthPixels);
            Assert.AreEqual(104, MapRibbonStyle.Turkey.r);
            Assert.AreEqual(178, MapRibbonStyle.Turkey.g);
            Assert.AreEqual(196, MapRibbonStyle.Turkey.b);
            Assert.Less(MapRibbonStyle.TurkeyPresentation.y, 0.9f);
            Assert.Greater(MapRibbonStyle.TurkeyPresentation.w, 0.45f);
            Assert.Less(MapRibbonStyle.TurkeyPresentation.w, 0.9f);
            var presenter = File.ReadAllText("Assets/Game/Map/MapRibbonPresenter.cs");
            Assert.IsTrue(presenter.Contains("SetVector(\"_TurkeyMul\""));
            Assert.AreEqual(
                "9a05e872c1cf618bb0d475261a6133dee917a1a44d80f32609a6b52e0b24340f",
                Sha("Assets/Game/Map/Resources/MapRibbonTurkey.asset"));
            Assert.AreEqual(
                "caf0d6aee8e0b448822fcc7aff859c14a88ee927816f2bf5ee9397c6a82f84b6",
                Sha("Assets/Game/Map/Resources/MapRibbonCoast.asset"));
        }

        [Test]
        public void RibbonVertex_RecordsBorderAndTurkeyRoles()
        {
            var border = new MapRibbonPolyline { Role = MapRibbonRole.Border };
            border.Longitude.Add(29d);
            border.Latitude.Add(41d);
            border.Longitude.Add(32d);
            border.Latitude.Add(41d);
            var mesh = MapRibbonBuilder.BuildMesh(new System.Collections.Generic.List<MapRibbonPolyline> { border }, "role");
            var uv = new System.Collections.Generic.List<Vector2>();
            mesh.GetUVs(3, uv);
            Assert.Greater(uv.Count, 0);
            Assert.AreEqual(1f, uv[0].y, 0.001f);
            Assert.AreEqual(0f, uv[0].x, 0.001f);
            UnityEngine.Object.DestroyImmediate(mesh);

            Assert.Greater(_model.Turkey.Count, 0);
            Assert.AreEqual(MapRibbonRole.Turkey, _model.Turkey[0].Role);
            Assert.AreEqual(MapRibbonRole.Coast, _model.Coast[0].Role);
            Assert.AreEqual(MapRibbonRole.Border, _model.Borders[0].Role);
        }

        [Test]
        public void Scene_DoesNotReferenceRibbonAssets()
        {
            var text = File.ReadAllText(ScenePath);
            StringAssert.DoesNotContain("guid: b8d4f2c15e304b7f8a9d6e5f4c3b2a19", text);
            StringAssert.DoesNotContain("guid: a7c3e1b04d2f4a6e9b8c7d6e5f4031a2", text);
            StringAssert.DoesNotContain("guid: c4e5f6a7b8c94d01a2b3c4d5e6f70831", text);
            StringAssert.DoesNotContain("guid: d5f6a7b8c9d04e12b3c4d5e6f7a80942", text);
            StringAssert.DoesNotContain("guid: e6a7b8c9d0e14f23c4d5e6f7a8b90a53", text);
            Assert.IsFalse(MapRibbonPresenter.Accepts("Assets/Game/Scenes/FoundationVerification.unity", MapSceneStamp.Current, true, true));
            Assert.IsFalse(MapRibbonPresenter.Accepts(ScenePath, 0, true, true));
        }

        [Test]
        public void Attach_SwapsInTheRibbonAndRestoresHardwareLines()
        {
            var coastRibbon = Resources.Load<Mesh>("MapRibbonCoast");
            var turkeyRibbon = Resources.Load<Mesh>("MapRibbonTurkey");
            Assert.IsNotNull(coastRibbon);
            Assert.IsNotNull(turkeyRibbon);
            Assert.AreEqual(157342, coastRibbon.vertexCount);
            Assert.AreEqual(468906, coastRibbon.GetIndexCount(0));
            Assert.AreEqual(1118, turkeyRibbon.vertexCount);
            Assert.AreEqual(3354, turkeyRibbon.GetIndexCount(0));
            Assert.IsNotNull(Resources.Load<Material>("MapRibbonLine"));

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var presenter = MapRibbonPresenter.Attach(scene);
                Assert.IsNotNull(presenter);
                Assert.AreSame(presenter, MapRibbonPresenter.Attach(scene));
                Assert.IsTrue(presenter.ShowingPremium);
                var coast = FindFilter(scene, "Coastlines");
                var turkey = FindFilter(scene, "TurkeyCoast");
                Assert.AreEqual(coastRibbon, coast.sharedMesh);
                Assert.AreEqual(turkeyRibbon, turkey.sharedMesh);
                var template = Resources.Load<Material>("MapRibbonLine");
                var runtime = coast.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.AreNotSame(template, runtime);
                Assert.AreSame(runtime, turkey.GetComponent<MeshRenderer>().sharedMaterial);
                presenter.ShowOriginal();
                Assert.IsFalse(presenter.ShowingPremium);
                Assert.AreEqual(194844, coast.sharedMesh.vertexCount);
                Assert.AreEqual(1118, turkey.sharedMesh.vertexCount);
                presenter.ShowPremium();
                presenter.ApplyZoom(223.928f);
                presenter.ApplyZoom(12.031f);
                Assert.AreEqual(coastRibbon, coast.sharedMesh);
                presenter.Release();
                UnityEngine.Object.DestroyImmediate(presenter);
                Assert.AreEqual(194844, coast.sharedMesh.vertexCount);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        static void AssertRoundTrip(Vector2 pixels, float width, float height)
        {
            var back = MapRibbonStyle.PixelsFromNdc(MapRibbonStyle.NdcOffsetFromPixels(pixels, width, height), width, height);
            Assert.AreEqual(pixels.x, back.x, 0.0001f);
            Assert.AreEqual(pixels.y, back.y, 0.0001f);
        }

        static void AssertLongestProjectedSegment()
        {
            var longest = 0f;
            Measure(_model.Coast, ref longest);
            Measure(_model.Borders, ref longest);
            Measure(_model.Turkey, ref longest);
            Assert.Less(longest, 25f);
        }

        static void Measure(List<MapRibbonPolyline> lines, ref float longest)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var count = line.Longitude.Count;
                var segments = line.Closed ? count : count - 1;
                Vector2 previous = default;
                var hasPrevious = false;
                for (var s = 0; s < count; s++)
                {
                    Assert.IsTrue(MapProjection.TryProjectUnwrapped(line.Latitude[s], line.Longitude[s], out var world));
                    if (hasPrevious)
                    {
                        var span = Vector2.Distance(previous, world);
                        if (span > longest)
                            longest = span;
                    }

                    previous = world;
                    hasPrevious = true;
                    if (!line.Closed && s >= segments)
                        break;
                }

                if (line.Closed && count >= 2)
                {
                    Assert.IsTrue(MapProjection.TryProjectUnwrapped(line.Latitude[0], line.Longitude[0], out var first));
                    var span = Vector2.Distance(previous, first);
                    if (span > longest)
                        longest = span;
                }
            }
        }

        static int CountMatched(List<MapRibbonPolyline> lines, HashSet<SegmentPair> baked)
        {
            var matched = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var count = line.Longitude.Count;
                var segments = line.Closed ? count : count - 1;
                for (var s = 0; s < segments; s++)
                {
                    var next = (s + 1) % count;
                    Assert.IsTrue(MapProjection.TryProjectUnwrapped(line.Latitude[s], line.Longitude[s], out var a));
                    Assert.IsTrue(MapProjection.TryProjectUnwrapped(line.Latitude[next], line.Longitude[next], out var b));
                    Assert.AreEqual(a, new Vector2(a.x, a.y));
                    if (!baked.Contains(new SegmentPair(a, b)))
                        Assert.Fail("Ribbon segment is not on the baked line mesh.");
                    matched++;
                }
            }

            return matched;
        }

        static bool ContainsPoint(List<MapRibbonPolyline> lines, double longitude, double latitude, double tolerance)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                for (var p = 0; p < line.Longitude.Count; p++)
                {
                    if (Math.Abs(line.Longitude[p] - longitude) <= tolerance &&
                        Math.Abs(line.Latitude[p] - latitude) <= tolerance)
                        return true;
                }
            }

            return false;
        }

        static HashSet<SegmentPair> Pairs(Vector3[] vertices)
        {
            var pairs = new HashSet<SegmentPair>(vertices.Length / 2);
            for (var i = 0; i < vertices.Length; i += 2)
                pairs.Add(new SegmentPair(vertices[i], vertices[i + 1]));
            return pairs;
        }

        static int CountOverlap(HashSet<MapRibbonEdgeKey> left, HashSet<MapRibbonEdgeKey> right)
        {
            var count = 0;
            foreach (var key in left)
            {
                if (right.Contains(key))
                    count++;
            }

            return count;
        }

        static MeshFilter NewFilter(string name)
        {
            var go = new GameObject(name);
            var filter = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            filter.sharedMesh = new Mesh();
            return filter;
        }

        static MeshFilter FindFilter(UnityEngine.SceneManagement.Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var renderers = roots[i].GetComponentsInChildren<MeshRenderer>(true);
                for (var r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r].gameObject.name == name)
                        return renderers[r].GetComponent<MeshFilter>();
                }
            }

            return null;
        }

        static string Sha(string path)
        {
            using var hash = SHA256.Create();
            using var stream = File.OpenRead(path);
            var bytes = hash.ComputeHash(stream);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        static string SceneGuid()
        {
            var text = File.ReadAllText(ScenePath + ".meta");
            var start = text.IndexOf("guid: ", StringComparison.Ordinal);
            return text.Substring(start + 6, 32);
        }

        readonly struct SegmentPair : IEquatable<SegmentPair>
        {
            readonly float _ax;
            readonly float _ay;
            readonly float _bx;
            readonly float _by;

            public SegmentPair(Vector2 a, Vector2 b)
            {
                if (a.x > b.x || (Math.Abs(a.x - b.x) < 0.0000001f && a.y > b.y))
                {
                    var swap = a;
                    a = b;
                    b = swap;
                }

                _ax = a.x;
                _ay = a.y;
                _bx = b.x;
                _by = b.y;
            }

            public bool Equals(SegmentPair other)
            {
                return _ax == other._ax && _ay == other._ay && _bx == other._bx && _by == other._by;
            }

            public override bool Equals(object obj)
            {
                return obj is SegmentPair other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _ax.GetHashCode();
                    hash = (hash * 397) ^ _ay.GetHashCode();
                    hash = (hash * 397) ^ _bx.GetHashCode();
                    hash = (hash * 397) ^ _by.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
