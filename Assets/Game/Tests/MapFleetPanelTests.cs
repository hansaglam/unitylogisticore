using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using LogistiCore.Backend;
using LogistiCore.EditorTools;
using LogistiCore.Localization;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LogistiCore.Tests
{
    public sealed class MapFleetPanelTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        [TearDown]
        public void Clear()
        {
            MapFleetStore.ClearOverride();
        }

        [Test]
        public void FromBackend_NeverInventsVehicles()
        {
            Assert.AreEqual(MapFleetStatus.Unconfigured, MapFleetStore.FromBackend(Phase(ClientBackendPhase.Idle)).Status);
            Assert.AreEqual(MapFleetStatus.Unconfigured, MapFleetStore.FromBackend(Phase(ClientBackendPhase.Ready)).Status);
            Assert.AreEqual(MapFleetStatus.Loading, MapFleetStore.FromBackend(Phase(ClientBackendPhase.Starting)).Status);
            Assert.AreEqual(MapFleetStatus.Error, MapFleetStore.FromBackend(Phase(ClientBackendPhase.Failed)).Status);
            Assert.AreEqual(0, MapFleetStore.FromBackend(Phase(ClientBackendPhase.Ready)).VehicleCount);
            MapFleetStore.Override(MapFleetSnapshot.Unconfigured());
            Assert.IsFalse(MapFleetStore.HasOverride);
            var live = MapFleetStore.Current;
            Assert.IsFalse(live.Synthetic);
            Assert.AreNotEqual(MapFleetStatus.Ready, live.Status);
            Assert.AreNotEqual(MapFleetStatus.Empty, live.Status);
        }

        [Test]
        public void GpsValidation_RejectsOutOfRangeCoordinates()
        {
            var invalid = new MapFleetAsset(
                "bad",
                MapFleetKind.Truck,
                "Bad",
                MapFleetAssetStatus.Operational,
                true,
                "",
                "",
                "",
                true,
                200d,
                true,
                32d);
            Assert.IsFalse(invalid.HasGps);
            var valid = new MapFleetAsset(
                "ok",
                MapFleetKind.Truck,
                "Ok",
                MapFleetAssetStatus.Operational,
                true,
                "",
                "",
                "",
                true,
                39.9d,
                true,
                32.8d);
            Assert.IsTrue(valid.HasGps);
            Assert.IsFalse(valid.HasKnownCity);
        }

        [Test]
        public void OpsFleet_HasTrEnDeWithoutInventedCounts()
        {
            MapSceneSetup.EnsureLocalization();
            var collection = LocalizationEditorSettings.GetStringTableCollection(OpsContent.Table);
            Assert.AreEqual("Müsait", Value(collection, "tr", OpsContent.FleetAvailable));
            Assert.AreEqual("Görevde", Value(collection, "tr", OpsContent.FleetBusy));
            Assert.AreEqual("Verfügbar", Value(collection, "de", OpsContent.FleetAvailable));
            Assert.AreEqual("Außer Betrieb", Value(collection, "de", OpsContent.FleetOutOfService));
            Assert.AreEqual("Filodaten konnten nicht geladen werden.", Value(collection, "de", OpsContent.FleetError));
            Assert.AreEqual("Güterverkehr-Sattelzugmaschine".Length > 20, true);
            for (var i = 0; i < OpsContent.Keys.Length; i++)
            {
                var en = Value(collection, "en", OpsContent.Keys[i]);
                var tr = Value(collection, "tr", OpsContent.Keys[i]);
                var de = Value(collection, "de", OpsContent.Keys[i]);
                Assert.IsFalse(string.IsNullOrWhiteSpace(en), OpsContent.Keys[i]);
                Assert.IsFalse(string.IsNullOrWhiteSpace(tr), OpsContent.Keys[i]);
                Assert.IsFalse(string.IsNullOrWhiteSpace(de), OpsContent.Keys[i]);
                AssertNoDigits(en);
                AssertNoDigits(tr);
                AssertNoDigits(de);
            }
        }

        [Test]
        public void FleetPanel_UnconfiguredAndFixturesStayOnTheMap()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                shell.Apply(2868f, 1320f, Phone());
                Assert.IsTrue(shell.SelectRegion(1));
                shell.ShowCity("izmir");
                var rig = Find<MapCameraRig>(scene);
                var camera = rig.GetComponent<Camera>();
                var pos = camera.transform.position;
                var size = camera.orthographicSize;
                var mapId = FindNamed(scene, "Map").GetInstanceID();
                var presenter = Find<MapRegionalReliefPresenter>(scene);
                var textureId = presenter != null && presenter.RuntimeMaterial != null
                    ? presenter.RuntimeMaterial.GetInstanceID()
                    : 0;
                var water = Find<MapWaterLabelLayer>(scene);
                var waterId = water != null ? water.GetInstanceID() : 0;

                shell.OpenManagement(MapModule.Fleet);
                Assert.IsTrue(shell.ManagementOpen);
                Assert.AreEqual(MapFleetStatus.Unconfigured, shell.Management.Fleet.Status);
                Assert.AreEqual(0, shell.Management.Fleet.ActiveRowCount);
                Assert.IsFalse(shell.Management.Fleet.FocusActive);
                StringAssert.DoesNotContain("starter_truck", shell.Management.Fleet.StatusText);

                Pose(shell, MapFleetFixtures.Loading());
                Assert.AreEqual(MapFleetStatus.Loading, shell.Management.Fleet.Status);
                Assert.AreEqual(0, shell.Management.Fleet.ActiveRowCount);

                Pose(shell, MapFleetFixtures.Error());
                Assert.AreEqual(MapFleetStatus.Error, shell.Management.Fleet.Status);

                Pose(shell, MapFleetFixtures.Empty());
                Assert.AreEqual(MapFleetStatus.Empty, shell.Management.Fleet.Status);
                Assert.AreEqual(0, shell.Management.Fleet.ActiveRowCount);

                Pose(shell, MapFleetFixtures.ReadyThree());
                Assert.AreEqual(MapFleetStatus.Ready, shell.Management.Fleet.Status);
                Assert.AreEqual(3, shell.Management.Fleet.ActiveRowCount);
                Assert.AreEqual("starter_truck", shell.Management.Fleet.SelectedId);
                shell.Management.Fleet.Select("busy_truck");
                Assert.AreEqual("busy_truck", shell.Management.Fleet.SelectedId);
                shell.SetManagementExpanded(true);
                Assert.IsTrue(shell.Management.Fleet.DetailVisible);
                Assert.IsTrue(
                    shell.Management.Fleet.DetailName.Contains("Truck")
                    || shell.Management.Fleet.DetailName.Contains("Kamyon")
                    || shell.Management.Fleet.DetailName.Contains("Lkw"),
                    shell.Management.Fleet.DetailName);
                Assert.IsFalse(shell.Management.Fleet.FocusActive);

                shell.Management.Fleet.Select("starter_truck");
                Assert.IsTrue(shell.TryFocusFleetLocation());
                Assert.AreEqual("izmir", shell.ShownCity);

                Pose(shell, MapFleetFixtures.ReadyGps());
                shell.SetManagementExpanded(true);
                Assert.IsTrue(shell.Management.Fleet.FocusActive);
                var before = rig.TargetCenter;
                Assert.IsTrue(shell.TryFocusFleetLocation());
                Assert.Greater(Vector2.Distance(before, rig.TargetCenter), 0.01f);

                var invalid = MapFleetSnapshot.Fixture(
                    MapFleetStatus.Ready,
                    new[]
                    {
                        new MapFleetAsset(
                            "bad_gps",
                            MapFleetKind.Truck,
                            "Bad Gps",
                            MapFleetAssetStatus.Operational,
                            true,
                            "",
                            "",
                            "",
                            true,
                            200d,
                            true,
                            10d)
                    },
                    "bad_gps");
                Pose(shell, invalid);
                shell.SetManagementExpanded(true);
                Assert.IsFalse(shell.Management.Fleet.FocusActive);
                Assert.IsFalse(shell.TryFocusFleetLocation());

                shell.OpenManagement(MapModule.Jobs);
                Assert.AreEqual(MapModule.Jobs, shell.ActiveModule);
                Assert.IsFalse(shell.Management.Fleet.gameObject.activeSelf);
                shell.CloseContext();
                Assert.AreEqual(1, shell.RegionIndex);
                Assert.AreEqual(mapId, FindNamed(scene, "Map").GetInstanceID());
                Assert.AreEqual(1, Count<EventSystem>(scene));
                Assert.AreEqual(1, Count<MapManagementPanel>(scene));
                if (presenter != null && presenter.RuntimeMaterial != null)
                    Assert.AreEqual(textureId, presenter.RuntimeMaterial.GetInstanceID());
                if (water != null)
                    Assert.AreEqual(waterId, water.GetInstanceID());
                Assert.AreEqual(ScenePath, scene.path);
            }
            finally
            {
                MapFleetStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void FleetRows_MeetTouchPolicyAndDoNotCoverFooter()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                Pose(shell, MapFleetFixtures.ReadyThree());
                AssertHits(shell, 2868f, 1320f, Phone(), 3f);
                AssertHits(shell, 1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f), 1f);
                AssertHits(shell, 1280f, 720f, new Rect(0f, 0f, 1280f, 720f), 1f);
                AssertHits(shell, 932f, 430f, new Rect(32f, 16f, 868f, 398f), 1f);
            }
            finally
            {
                MapFleetStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TwentyFleetCycles_KeepOneShellAndStayCheap()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                var clock = Stopwatch.StartNew();
                var ticks = 0L;
                for (var i = 0; i < 20; i++)
                {
                    clock.Restart();
                    Pose(shell, MapFleetFixtures.ReadyThree());
                    shell.OpenManagement(MapModule.Fleet);
                    shell.SetManagementExpanded(true);
                    shell.Management.Fleet.Select("busy_truck");
                    shell.OpenManagement(MapModule.Jobs);
                    shell.OpenManagement(MapModule.Fleet);
                    shell.CloseContext();
                    MapFleetStore.ClearOverride();
                    clock.Stop();
                    ticks += clock.ElapsedTicks;
                    Assert.AreEqual(1, Count<MapCommandShell>(scene), "cycle " + i);
                    Assert.AreEqual(1, Count<EventSystem>(scene), "cycle " + i);
                    Assert.AreEqual(1, Count<MapManagementPanel>(scene), "cycle " + i);
                }

                var avgMs = ticks / 20.0 * 1000.0 / Stopwatch.Frequency;
                TestContext.WriteLine("Fleet cycle avg ms=" + avgMs.ToString("0.000"));
                Assert.Less(avgMs, 40.0);
            }
            finally
            {
                MapFleetStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void AssertHits(MapCommandShell shell, float width, float height, Rect safe, float pointScale)
        {
            shell.Apply(width, height, safe);
            shell.OpenManagement(MapModule.Fleet);
            shell.SetManagementExpanded(true);
            var need = MapCommandLayout.TouchPixels(width, height);
            Assert.AreEqual(44f * pointScale, need, 0.01f);
            var row = shell.Management.Fleet.RowHitPixels(0);
            Assert.GreaterOrEqual(row.height, need - 0.5f, width + "x" + height);
            Assert.Greater(shell.Management.Fleet.ActiveRowCount, 0);
            var panel = shell.Management.PanelRect;
            var scale = MapHudLayout.CanvasScale(width, height);
            var y = safe.yMin + panel.anchoredPosition.y * scale;
            Assert.GreaterOrEqual(y, shell.SummaryRect.yMax - 1f, width + " footer overlap");
            shell.CloseContext();
        }

        static void Pose(MapCommandShell shell, MapFleetSnapshot snapshot)
        {
            MapFleetStore.Override(snapshot);
            if (!shell.ManagementOpen)
                shell.OpenManagement(MapModule.Fleet);
            else
                shell.Management.Show(MapModule.Fleet);
            shell.Management.Fleet.Refresh();
        }

        static ClientBackendStatus Phase(ClientBackendPhase phase)
        {
            return new ClientBackendStatus(ClientBackendEnvironment.Unconfigured, ClientConnectionDecision.StayOffline, phase, "");
        }

        static void AssertNoDigits(string value)
        {
            for (var i = 0; i < value.Length; i++)
                Assert.IsFalse(char.IsDigit(value[i]), value);
        }

        static Rect Phone()
        {
            return new Rect(186f, 60f, 2496f, 1200f);
        }

        static string Value(StringTableCollection collection, string code, string key)
        {
            foreach (var table in collection.StringTables)
            {
                if (table.LocaleIdentifier.Code == code)
                    return table.GetEntry(key).Value;
            }

            Assert.Fail(code);
            return null;
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
