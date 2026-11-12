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
    public sealed class MapWarehousePanelTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        [TearDown]
        public void Clear()
        {
            MapWarehouseStore.ClearOverride();
            MapFleetStore.ClearOverride();
            MapJobsStore.ClearOverride();
            MapOperationStrip.ClearOverride();
        }

        [Test]
        public void FromBackend_NeverInventsWarehouses()
        {
            Assert.AreEqual(MapWarehouseStatus.Unconfigured, MapWarehouseStore.FromBackend(Phase(ClientBackendPhase.Idle)).Status);
            Assert.AreEqual(MapWarehouseStatus.Unconfigured, MapWarehouseStore.FromBackend(Phase(ClientBackendPhase.Ready)).Status);
            Assert.AreEqual(MapWarehouseStatus.Loading, MapWarehouseStore.FromBackend(Phase(ClientBackendPhase.Starting)).Status);
            Assert.AreEqual(MapWarehouseStatus.Error, MapWarehouseStore.FromBackend(Phase(ClientBackendPhase.Failed)).Status);
            Assert.AreEqual(0, MapWarehouseStore.FromBackend(Phase(ClientBackendPhase.Ready)).OwnedCount);
            MapWarehouseStore.Override(MapWarehouseSnapshot.Unconfigured());
            Assert.IsFalse(MapWarehouseStore.HasOverride);
            var live = MapWarehouseStore.Current;
            Assert.IsFalse(live.Synthetic);
            Assert.AreNotEqual(MapWarehouseStatus.Ready, live.Status);
            Assert.AreNotEqual(MapWarehouseStatus.Empty, live.Status);
            Assert.IsFalse(live.OffersSupported);
        }

        [Test]
        public void Snapshot_OwnedCapacityAndGpsRules()
        {
            var mixed = MapWarehouseFixtures.Mixed();
            Assert.AreEqual(MapWarehouseStatus.Ready, mixed.Status);
            Assert.IsFalse(mixed.OffersSupported);
            Assert.AreEqual(7, mixed.OwnedCount);
            Assert.IsTrue(mixed.TryGetOwned("wh_izmir", out var izmir));
            Assert.AreEqual("Izmir Depot", izmir.DisplayName);
            Assert.AreEqual("0 / 10", izmir.CapacitySummary);
            Assert.IsTrue(izmir.HasMatchingCapacity);
            Assert.IsFalse(izmir.CapacitySummary.Contains("%"));
            Assert.IsTrue(mixed.TryGet("wh_unknown_own", out var unknownOwn));
            Assert.IsFalse(unknownOwn.Owned);
            Assert.IsFalse(mixed.TryGetOwned("wh_unknown_own", out _));
            mixed.TryGetOwned("wh_mismatch", out var mismatch);
            Assert.IsFalse(mismatch.HasMatchingCapacity);
            Assert.IsTrue(mismatch.HasUsedCapacity);
            Assert.AreEqual("", mismatch.CapacitySummary);
            mixed.TryGetOwned("wh_unknown_city", out var unknownCity);
            Assert.IsFalse(unknownCity.HasKnownCity);
            mixed.TryGetOwned("wh_bad_gps", out var badGps);
            Assert.IsFalse(badGps.HasGps);
            Assert.AreEqual(MapWarehouseSiteStatus.Unknown, MapWarehouseItem.ParseStatus("paused"));
            Assert.AreEqual(MapWarehouseStatus.Empty, MapWarehouseFixtures.Empty().Status);
            Assert.AreEqual(0, MapWarehouseFixtures.Empty().Items.Length);
        }

        [Test]
        public void OpsWarehouses_HasTrEnDeWithoutInventedCounts()
        {
            MapSceneSetup.EnsureLocalization();
            var collection = LocalizationEditorSettings.GetStringTableCollection(OpsContent.Table);
            Assert.AreEqual("Depolar", Value(collection, "tr", OpsContent.TitleWarehouses));
            Assert.AreEqual("Lagerhäuser", Value(collection, "de", OpsContent.TitleWarehouses));
            Assert.AreEqual("Lagerdaten konnten nicht geladen werden.", Value(collection, "de", OpsContent.WarehousesError));
            Assert.AreEqual("Güterverkehrslagerhaus-Verwaltung".Length > 20, true);
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
        public void WarehousePanel_StatesSelectionFocusAndModuleStayOnTheMap()
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

                shell.OpenManagement(MapModule.Warehouses);
                Assert.AreEqual(MapWarehouseStatus.Unconfigured, shell.Management.Warehouses.Status);
                Assert.AreEqual(0, shell.Management.Warehouses.ActiveRowCount);
                Assert.IsFalse(shell.Management.Warehouses.FixtureVisible);
                Assert.IsFalse(shell.Management.Warehouses.OffersVisible);
                Assert.IsFalse(shell.TryFocusWarehouseCity());
                StringAssert.DoesNotContain("wh_izmir", shell.Management.Warehouses.StatusText);

                PoseWh(shell, MapWarehouseFixtures.Loading());
                Assert.AreEqual(MapWarehouseStatus.Loading, shell.Management.Warehouses.Status);
                PoseWh(shell, MapWarehouseFixtures.Error());
                Assert.AreEqual(MapWarehouseStatus.Error, shell.Management.Warehouses.Status);
                PoseWh(shell, MapWarehouseFixtures.Empty());
                Assert.AreEqual(MapWarehouseStatus.Empty, shell.Management.Warehouses.Status);
                Assert.AreEqual(0, shell.Management.Warehouses.ActiveRowCount);
                StringAssert.DoesNotContain("0", shell.Management.Warehouses.StatusText);

                PoseWh(shell, MapWarehouseFixtures.Mixed());
                Assert.AreEqual(7, shell.Management.Warehouses.ActiveRowCount);
                Assert.IsTrue(shell.Management.Warehouses.FixtureVisible);
                Assert.AreEqual("wh_izmir", shell.Management.Warehouses.SelectedId);
                Assert.AreEqual(0, shell.Management.Warehouses.MarkerCount);
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.01f);
                Assert.AreEqual(size, camera.orthographicSize, 0.01f);

                shell.SetManagementExpanded(true);
                shell.Management.Warehouses.Select("wh_izmir");
                Assert.IsTrue(shell.Management.Warehouses.DetailVisible);
                StringAssert.Contains("Izmir Depot", shell.Management.Warehouses.DetailName);
                StringAssert.Contains("0 / 10", shell.Management.Warehouses.DetailBody);
                Assert.IsFalse(shell.Management.Warehouses.DetailBody.Contains("%"));
                Assert.IsFalse(shell.Management.Warehouses.DetailBody.Contains("LGC"));
                Assert.IsTrue(shell.Management.Warehouses.CityActive);
                Assert.IsFalse(shell.Management.Warehouses.GpsActive);

                shell.Management.Warehouses.Select("wh_unknown_city");
                Assert.IsFalse(shell.Management.Warehouses.CityActive);
                Assert.IsFalse(shell.TryFocusWarehouseCity());
                shell.Management.Warehouses.Select("wh_bad_gps");
                Assert.IsFalse(shell.Management.Warehouses.GpsActive);
                Assert.IsFalse(shell.TryFocusWarehouseGps());

                shell.Management.Warehouses.Select("wh_izmir");
                var before = rig.TargetCenter;
                Assert.IsTrue(shell.TryFocusWarehouseCity());
                Assert.AreEqual("izmir", shell.ShownCity);
                Assert.Greater(Vector2.Distance(before, rig.TargetCenter), 0.01f);
                var afterFocus = rig.TargetCenter;

                MapFleetStore.Override(MapFleetFixtures.ReadyThree());
                shell.OpenManagement(MapModule.Fleet);
                Assert.AreEqual(3, shell.Management.Fleet.ActiveRowCount);
                MapJobsStore.Override(MapJobsFixtures.Mixed());
                shell.OpenManagement(MapModule.Jobs);
                Assert.Greater(shell.Management.Jobs.ActiveRowCount, 0);
                shell.OpenManagement(MapModule.Warehouses);
                Assert.IsTrue(shell.Management.Warehouses.gameObject.activeSelf);
                Assert.IsFalse(shell.Management.Fleet.gameObject.activeSelf);
                Assert.IsFalse(shell.Management.Jobs.gameObject.activeSelf);
                shell.OpenManagement(MapModule.Market);
                Assert.IsFalse(shell.Management.Warehouses.gameObject.activeSelf);

                shell.CloseContext();
                Assert.AreEqual(1, shell.RegionIndex);
                Assert.AreEqual("izmir", shell.ShownCity);
                Assert.AreEqual(afterFocus, rig.TargetCenter);
                Assert.AreEqual(mapId, FindNamed(scene, "Map").GetInstanceID());
                Assert.AreEqual(1, Count<EventSystem>(scene));
                Assert.AreEqual(1, Count<MapManagementPanel>(scene));
                if (presenter != null && presenter.RuntimeMaterial != null)
                    Assert.AreEqual(textureId, presenter.RuntimeMaterial.GetInstanceID());
                if (water != null)
                    Assert.AreEqual(waterId, water.GetInstanceID());
            }
            finally
            {
                MapWarehouseStore.ClearOverride();
                MapFleetStore.ClearOverride();
                MapJobsStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void WarehouseRows_MeetTouchPolicyAndStackOnPhone()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                PoseWh(shell, MapWarehouseFixtures.Mixed());
                AssertHits(shell, 2868f, 1320f, Phone(), 3f);
                AssertHits(shell, 1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f), 1f);
                AssertHits(shell, 1280f, 720f, new Rect(0f, 0f, 1280f, 720f), 1f);
                AssertHits(shell, 932f, 430f, new Rect(32f, 16f, 868f, 398f), 1f);
                shell.Apply(932f, 430f, new Rect(32f, 16f, 868f, 398f));
                shell.OpenManagement(MapModule.Warehouses);
                shell.SetManagementExpanded(true);
                shell.Management.Warehouses.Select("wh_long_de");
                Assert.IsTrue(shell.Management.Warehouses.DetailVisible);
                Assert.IsFalse(shell.Management.Warehouses.ListVisible);
                shell.Management.Warehouses.ShowList();
                Assert.IsTrue(shell.Management.Warehouses.ListVisible);
                var list = shell.Management.Warehouses.GetComponentInChildren<ScrollRect>(true);
                Assert.IsNotNull(list);
                Assert.IsFalse(list.horizontal);
            }
            finally
            {
                MapWarehouseStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TwentyWarehouseCycles_KeepOneShellAndStayCheap()
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
                var stable = 0;
                for (var i = 0; i < 20; i++)
                {
                    clock.Restart();
                    PoseWh(shell, MapWarehouseFixtures.Mixed());
                    shell.OpenManagement(MapModule.Warehouses);
                    shell.SetManagementExpanded(true);
                    shell.Management.Warehouses.Select("wh_istanbul");
                    shell.OpenManagement(MapModule.Fleet);
                    shell.OpenManagement(MapModule.Jobs);
                    shell.OpenManagement(MapModule.Warehouses);
                    shell.CloseContext();
                    MapWarehouseStore.ClearOverride();
                    clock.Stop();
                    ticks += clock.ElapsedTicks;
                    Assert.AreEqual(1, Count<MapCommandShell>(scene), "cycle " + i);
                    Assert.AreEqual(1, Count<EventSystem>(scene), "cycle " + i);
                    Assert.AreEqual(1, Count<MapManagementPanel>(scene), "cycle " + i);
                    var transforms = Count<Transform>(scene);
                    if (i == 0)
                        stable = transforms;
                    else
                        Assert.AreEqual(stable, transforms, "cycle " + i);
                }

                var avgMs = ticks / 20.0 * 1000.0 / Stopwatch.Frequency;
                TestContext.WriteLine("Warehouse cycle avg ms=" + avgMs.ToString("0.000"));
                TestContext.WriteLine("Transform count=" + stable);
                Assert.Less(avgMs, 40.0);
            }
            finally
            {
                MapWarehouseStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void AssertHits(MapCommandShell shell, float width, float height, Rect safe, float pointScale)
        {
            shell.Apply(width, height, safe);
            shell.OpenManagement(MapModule.Warehouses);
            shell.SetManagementExpanded(true);
            var need = MapCommandLayout.TouchPixels(width, height);
            Assert.AreEqual(44f * pointScale, need, 0.01f);
            var row = shell.Management.Warehouses.RowHitPixels(0);
            Assert.GreaterOrEqual(row.height, need - 0.5f, width + "x" + height);
            Assert.Greater(shell.Management.Warehouses.ActiveRowCount, 0);
            var panel = shell.Management.PanelRect;
            var scale = MapHudLayout.CanvasScale(width, height);
            var y = safe.yMin + panel.anchoredPosition.y * scale;
            Assert.GreaterOrEqual(y, shell.SummaryRect.yMax - 1f, width + " footer overlap");
            shell.CloseContext();
        }

        static void PoseWh(MapCommandShell shell, MapWarehouseSnapshot snapshot)
        {
            MapWarehouseStore.Override(snapshot);
            if (!shell.ManagementOpen)
                shell.OpenManagement(MapModule.Warehouses);
            else
                shell.Management.Show(MapModule.Warehouses);
            shell.Management.Warehouses.Refresh();
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
