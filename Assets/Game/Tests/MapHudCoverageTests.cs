using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using LogistiCore.Backend;
using LogistiCore.EditorTools;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LogistiCore.Tests
{
    public sealed class MapHudCoverageTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        [TearDown]
        public void Clear()
        {
            MapMarketStore.ClearOverride();
            MapWarehouseStore.ClearOverride();
            MapFleetStore.ClearOverride();
            MapJobsStore.ClearOverride();
            MapOperationStrip.ClearOverride();
        }

        [Test]
        public void HoleAlpha251_IsOpaqueOnlyAboveTwoFifty()
        {
            Assert.IsFalse(MapHudCoverage.OpaqueUiCover(250, 8, 90));
            Assert.IsTrue(MapHudCoverage.OpaqueUiCover(251, 8, 90));
            Assert.IsTrue(MapHudCoverage.HoleCoverageAllowed(MapHudSampleKind.Map, 250, 8, 90));
            Assert.IsFalse(MapHudCoverage.HoleCoverageAllowed(MapHudSampleKind.Map, 251, 8, 90));
            Assert.IsTrue(MapHudCoverage.HoleCoverageAllowed(MapHudSampleKind.Panel, 251, 8, 90));
            Assert.IsTrue(MapHudCoverage.HoleCoverageAllowed(MapHudSampleKind.Blocker, 251, 8, 90));
            Assert.IsFalse(MapHudCoverage.HoleCoverageAllowed(MapHudSampleKind.Map, 255, 8, 90));
        }

        [Test]
        public void Market932_ExpandedHoleIsIntendedOverlay_CompactAndMapAreMap()
        {
            var compactSafe = CompactSafe();
            var hole = MapHudCoverage.HolePixel(932, 430);
            Assert.AreEqual(466f, hole.x, 0.01f);
            Assert.AreEqual(215f, hole.y, 0.01f);
            var metrics = MapCommandLayout.Evaluate(932f, 430f, compactSafe, false);
            var touch = MapCommandLayout.TouchPixels(932f, 430f);
            Assert.AreEqual(MapHudSampleKind.Map, MapHudCoverage.Classify(metrics, touch, false, false, hole));
            Assert.AreEqual(MapHudSampleKind.Map, MapHudCoverage.Classify(metrics, touch, true, false, hole));
            var expandedKind = MapHudCoverage.Classify(metrics, touch, true, true, hole);
            Assert.IsTrue(MapHudCoverage.IntendedManagement(expandedKind), expandedKind.ToString());
            MapHudCoverage.OverlayRects(metrics.Viewport, metrics.FontPixels, touch, true, true, out var panel, out var blocker);
            Assert.IsTrue(MapHudCoverage.Contains(panel, hole) || MapHudCoverage.Contains(blocker, hole));
            Assert.IsFalse(Overlaps(panel, metrics.Header), "panel vs header");
            Assert.IsFalse(Overlaps(panel, metrics.Summary), "panel vs footer");
            Assert.IsFalse(Overlaps(panel, metrics.Rail), "panel vs rail");
            Assert.IsFalse(Overlaps(blocker, metrics.Header), "blocker vs header");
            Assert.IsFalse(Overlaps(blocker, metrics.Summary), "blocker vs footer");
            Assert.IsFalse(Overlaps(blocker, metrics.Rail), "blocker vs rail");
            Assert.IsTrue(ContainsInclusive(metrics.Viewport, panel));
            Assert.AreEqual(metrics.Viewport, blocker);
            var mapProbe = new Vector2(metrics.Viewport.xMin + 12f, metrics.Viewport.center.y);
            MapHudCoverage.OverlayRects(metrics.Viewport, metrics.FontPixels, touch, true, false, out var compact, out var compactBlocker);
            Assert.AreEqual(0f, compactBlocker.width);
            Assert.IsFalse(MapHudCoverage.Contains(compact, mapProbe));
            Assert.AreEqual(MapHudSampleKind.Map, MapHudCoverage.Classify(metrics, touch, true, false, mapProbe));
            Assert.AreEqual(MapHudSampleKind.Header, MapHudCoverage.Classify(metrics, touch, true, true, metrics.Header.center));
            Assert.AreEqual(MapHudSampleKind.Footer, MapHudCoverage.Classify(metrics, touch, true, true, metrics.Summary.center));
            Assert.AreEqual(MapHudSampleKind.Rail, MapHudCoverage.Classify(metrics, touch, true, true, RailSample(metrics.Rail)));
            TestContext.WriteLine("932 viewport=" + metrics.Viewport + " panel=" + panel + " blocker=" + blocker + " hole=" + hole + " kind=" + expandedKind);
        }

        [Test]
        public void Expanded2868And1920_HoleStaysInsideManagement()
        {
            AssertFrame(2868f, 1320f, Phone(), true);
            AssertFrame(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f), true);
            AssertFrame(932f, 430f, CompactSafe(), true);
        }

        [Test]
        public void FooterCta_TrustedReadContract_KeepsLiveRuntimeClosed()
        {
            var ready = MapOperationStrip.FromBackend(Phase(ClientBackendPhase.Ready));
            Assert.IsFalse(ready.Synthetic);
            Assert.IsFalse(ready.Verified);
            Assert.IsFalse(ready.TrustedRead);
            Assert.IsFalse(ready.CanOpenJobs);
            Assert.IsFalse(MapOperationSnapshot.Unconfigured().CanOpenJobs);
            Assert.IsFalse(MapOperationSnapshot.Fixture(MapOperationStatus.Idle).CanOpenJobs);
            Assert.IsTrue(MapOperationSnapshot.Fixture(MapOperationStatus.SingleActive, 1, "A", "B", "").CanOpenJobs);
            Assert.IsTrue(MapOperationSnapshot.VerifiedRead(MapOperationStatus.SingleActive, 1, "A", "B", "").CanOpenJobs);
            Assert.IsTrue(MapOperationSnapshot.VerifiedRead(MapOperationStatus.SingleActive, 1).Verified);
            Assert.IsFalse(MapOperationSnapshot.VerifiedRead(MapOperationStatus.SingleActive, 1).Synthetic);
            Assert.IsFalse(MapOperationSnapshot.Fixture(MapOperationStatus.SingleActive, 1).Verified);
        }

        [Test]
        public void FooterTouch_StaysBelowFortyFourPointsOnPhoneAndCompact()
        {
            AssertTouchHold(2868f, 1320f, Phone(), true);
            AssertTouchHold(932f, 430f, CompactSafe(), true);
            var desk = MapCommandLayout.Evaluate(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f), false).Summary;
            var deskNeed = MapCommandLayout.TouchPixels(1920f, 1080f);
            TestContext.WriteLine("1920x1080 summaryH=" + desk.height.ToString("0.0") + " need=" + deskNeed.ToString("0.0"));
            Assert.Greater(desk.height, 20f);
        }

        [Test]
        public void OverlayScene_PreservesCameraAndRejectsCompactMapRaycast()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                shell.Apply(932f, 430f, CompactSafe());
                Assert.IsTrue(shell.SelectRegion(1));
                shell.ShowCity("izmir");
                var rig = Find<MapCameraRig>(scene);
                var camera = rig.GetComponent<Camera>();
                var pos = camera.transform.position;
                var size = camera.orthographicSize;
                var mapId = FindNamed(scene, "Map").GetInstanceID();
                MapMarketStore.Override(MapMarketFixtures.Mixed());
                shell.OpenManagement(MapModule.Market);
                shell.SetManagementExpanded(false);
                var metrics = MapCommandLayout.Evaluate(932f, 430f, CompactSafe(), shell.ContextOpen);
                var touch = MapCommandLayout.TouchPixels(932f, 430f);
                var hole = MapHudCoverage.HolePixel(932, 430);
                Assert.AreEqual(MapHudSampleKind.Map, MapHudCoverage.Classify(metrics, touch, true, false, hole));
                var mapProbe = new Vector2(shell.Viewport.xMin + 16f, shell.Viewport.center.y);
                Assert.IsFalse(MapHudCoverage.Contains(MapManagementLayout.Compact(shell.Viewport, metrics.FontPixels, touch), mapProbe));
                Assert.IsFalse(shell.Management.ContainsScreenPoint(new Vector2(-4096f, -4096f)));

                shell.SetManagementExpanded(true);
                Assert.IsTrue(MapHudCoverage.IntendedManagement(MapHudCoverage.Classify(metrics, touch, true, true, hole)));
                Assert.IsTrue(shell.Management.BlockerRect.gameObject.activeSelf);
                Assert.IsTrue(shell.Management.BlockerRect.GetComponent<Image>().raycastTarget);

                shell.OpenManagement(MapModule.Fleet);
                shell.OpenManagement(MapModule.Jobs);
                shell.OpenManagement(MapModule.Warehouses);
                shell.OpenManagement(MapModule.Market);
                var clock = Stopwatch.StartNew();
                var ticks = 0L;
                for (var i = 0; i < 20; i++)
                {
                    clock.Restart();
                    shell.OpenManagement(MapModule.Fleet);
                    shell.SetManagementExpanded(true);
                    shell.OpenManagement(MapModule.Jobs);
                    shell.OpenManagement(MapModule.Warehouses);
                    shell.OpenManagement(MapModule.Market);
                    shell.CloseContext();
                    clock.Stop();
                    ticks += clock.ElapsedTicks;
                    Assert.AreEqual(1, Count<EventSystem>(scene), "cycle " + i);
                    Assert.AreEqual(1, Count<MapManagementPanel>(scene), "cycle " + i);
                }

                TestContext.WriteLine("R1 cycle avg ms=" + (ticks / 20.0 * 1000.0 / Stopwatch.Frequency).ToString("0.000"));
                shell.CloseContext();
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.01f);
                Assert.AreEqual(size, camera.orthographicSize, 0.01f);
                Assert.AreEqual("izmir", shell.ShownCity);
                Assert.AreEqual(1, shell.RegionIndex);
                Assert.AreEqual(mapId, FindNamed(scene, "Map").GetInstanceID());
                Assert.IsFalse(shell.StripActionEnabled);
                Pose(shell, MapOperationSnapshot.Fixture(MapOperationStatus.SingleActive, 1, "Ankara", "İzmir", ""));
                Assert.IsTrue(shell.StripActionEnabled);
                MapOperationStrip.ClearOverride();
                shell.RefreshStrip();
                Assert.IsFalse(shell.StripActionEnabled);
            }
            finally
            {
                MapOperationStrip.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        static void AssertFrame(float width, float height, Rect safe, bool expanded)
        {
            var metrics = MapCommandLayout.Evaluate(width, height, safe, false);
            var touch = MapCommandLayout.TouchPixels(width, height);
            var hole = MapHudCoverage.HolePixel(Mathf.RoundToInt(width), Mathf.RoundToInt(height));
            var kind = MapHudCoverage.Classify(metrics, touch, true, expanded, hole);
            Assert.IsTrue(MapHudCoverage.IntendedManagement(kind), width + "x" + height + " " + kind);
            MapHudCoverage.OverlayRects(metrics.Viewport, metrics.FontPixels, touch, true, expanded, out var panel, out var blocker);
            Assert.IsFalse(Overlaps(panel, metrics.Header));
            Assert.IsFalse(Overlaps(panel, metrics.Summary));
            Assert.IsFalse(Overlaps(panel, metrics.Rail));
            Assert.IsTrue(ContainsInclusive(metrics.Viewport, panel));
            Assert.IsTrue(MapHudCoverage.Contains(panel, hole) || MapHudCoverage.Contains(blocker, hole));
        }

        static void AssertTouchHold(float width, float height, Rect safe, bool hold)
        {
            var summary = MapCommandLayout.Evaluate(width, height, safe, false).Summary;
            var need = MapCommandLayout.TouchPixels(width, height);
            TestContext.WriteLine(width + "x" + height + " summaryH=" + summary.height.ToString("0.0") + " need=" + need.ToString("0.0"));
            if (hold)
                Assert.Less(summary.height, need);
        }

        static void Pose(MapCommandShell shell, MapOperationSnapshot snapshot)
        {
            MapOperationStrip.Override(snapshot);
            shell.RefreshStrip();
        }

        static ClientBackendStatus Phase(ClientBackendPhase phase)
        {
            return new ClientBackendStatus(ClientBackendEnvironment.Unconfigured, ClientConnectionDecision.StayOffline, phase, "");
        }

        static Vector2 RailSample(Rect rail)
        {
            return new Vector2(rail.xMin + Mathf.Min(8f, rail.width * 0.25f), rail.center.y);
        }

        static bool Overlaps(Rect a, Rect b)
        {
            return a.width > 1f && b.width > 1f && a.Overlaps(b);
        }

        static bool ContainsInclusive(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - 0.5f
                && inner.yMin >= outer.yMin - 0.5f
                && inner.xMax <= outer.xMax + 0.5f
                && inner.yMax <= outer.yMax + 0.5f;
        }

        static Rect CompactSafe()
        {
            return new Rect(32f, 16f, 868f, 398f);
        }

        static Rect Phone()
        {
            return new Rect(186f, 60f, 2496f, 1200f);
        }

        static T Find<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }

            return null;
        }

        static GameObject FindNamed(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                    return roots[i];
                var transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (var t = 0; t < transforms.Length; t++)
                {
                    if (transforms[t].name == name)
                        return transforms[t].gameObject;
                }
            }

            return null;
        }

        static int Count<T>(Scene scene) where T : Object
        {
            var count = 0;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                count += roots[i].GetComponentsInChildren<T>(true).Length;
            return count;
        }

        static string Sha(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var text = new System.Text.StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                    text.Append(hash[i].ToString("x2"));
                return text.ToString();
            }
        }
    }
}
