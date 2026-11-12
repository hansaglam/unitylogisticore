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
    public sealed class MapMarketPanelTests
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
        public void FromBackend_NeverInventsMarketPrices()
        {
            Assert.AreEqual(MapMarketStatus.Unconfigured, MapMarketStore.FromBackend(Phase(ClientBackendPhase.Idle)).Status);
            Assert.AreEqual(MapMarketStatus.Unconfigured, MapMarketStore.FromBackend(Phase(ClientBackendPhase.Ready)).Status);
            Assert.AreEqual(MapMarketStatus.Loading, MapMarketStore.FromBackend(Phase(ClientBackendPhase.Starting)).Status);
            Assert.AreEqual(MapMarketStatus.Error, MapMarketStore.FromBackend(Phase(ClientBackendPhase.Failed)).Status);
            Assert.IsFalse(MapMarketStore.FromBackend(Phase(ClientBackendPhase.Ready)).HasPriceFeed);
            Assert.AreEqual(0, MapMarketStore.FromBackend(Phase(ClientBackendPhase.Ready)).Items.Length);
            MapMarketStore.Override(MapMarketSnapshot.Unconfigured());
            Assert.IsFalse(MapMarketStore.HasOverride);
            var live = MapMarketStore.Current;
            Assert.IsFalse(live.Synthetic);
            Assert.AreNotEqual(MapMarketStatus.Ready, live.Status);
            Assert.AreNotEqual(MapMarketStatus.Empty, live.Status);
            Assert.IsFalse(live.HasPriceFeed);
        }

        [Test]
        public void Snapshot_DocumentedConditionRulesWithoutPriceOrTrend()
        {
            var mixed = MapMarketFixtures.Mixed();
            Assert.AreEqual(MapMarketStatus.Ready, mixed.Status);
            Assert.IsTrue(mixed.Synthetic);
            Assert.IsFalse(mixed.HasPriceFeed);
            Assert.AreEqual(7, mixed.Items.Length);
            Assert.IsTrue(mixed.TryGet("izmir/steel", out var steel));
            Assert.IsTrue(steel.HasKnownCity);
            Assert.IsFalse(steel.HasPrice);
            Assert.IsFalse(steel.HasCurrency);
            Assert.IsFalse(steel.HasTrend);
            Assert.AreEqual("+125", steel.AdjustmentText);
            Assert.IsFalse(mixed.TryGet("izmir/steel-dup", out _));
            mixed.TryGet("synthetic_city/ore", out var unknown);
            Assert.IsFalse(unknown.HasKnownCity);
            mixed.TryGet("bursa/parts", out var missing);
            Assert.IsFalse(missing.HasDemandIndex);
            Assert.IsFalse(missing.HasSupplyIndex);
            Assert.IsFalse(missing.HasAdjustment);
            mixed.TryGet("antalya/mail", out var trend);
            Assert.AreEqual(40, trend.TrendBps);
            Assert.IsFalse(trend.HasTrend);
            mixed.TryGet("trabzon/coal", out var gps);
            Assert.IsFalse(gps.HasGps);
            Assert.AreEqual(MapMarketStatus.Empty, MapMarketFixtures.Empty().Status);
            Assert.AreEqual(0, MapMarketFixtures.Empty().Items.Length);
            var filtered = mixed.WithCargo("steel");
            Assert.AreEqual(1, filtered.CountIn("steel"));
        }

        [Test]
        public void OpsMarket_HasTrEnDeWithoutInventedCounts()
        {
            MapSceneSetup.EnsureLocalization();
            var collection = LocalizationEditorSettings.GetStringTableCollection(OpsContent.Table);
            Assert.AreEqual("Pazar", Value(collection, "tr", OpsContent.TitleMarket));
            Assert.AreEqual("Markt", Value(collection, "de", OpsContent.TitleMarket));
            Assert.AreEqual("Marktdaten konnten nicht geladen werden.", Value(collection, "de", OpsContent.MarketError));
            Assert.AreEqual("Preis ist in diesem Stand nicht enthalten.", Value(collection, "de", OpsContent.MarketPriceUnavailable));
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
        public void MarketPanel_StatesSelectionFocusAndModuleStayOnTheMap()
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

                shell.OpenManagement(MapModule.Market);
                Assert.AreEqual(MapMarketStatus.Unconfigured, shell.Management.Market.Status);
                Assert.AreEqual(0, shell.Management.Market.ActiveRowCount);
                Assert.IsFalse(shell.Management.Market.FixtureVisible);
                Assert.IsFalse(shell.Management.Market.PriceVisible);
                Assert.IsFalse(shell.Management.Market.TrendVisible);
                Assert.IsFalse(shell.TryFocusMarketCity());
                StringAssert.DoesNotContain("izmir/steel", shell.Management.Market.StatusText);

                PoseMk(shell, MapMarketFixtures.Loading());
                Assert.AreEqual(MapMarketStatus.Loading, shell.Management.Market.Status);
                PoseMk(shell, MapMarketFixtures.Error());
                Assert.AreEqual(MapMarketStatus.Error, shell.Management.Market.Status);
                PoseMk(shell, MapMarketFixtures.Empty());
                Assert.AreEqual(MapMarketStatus.Empty, shell.Management.Market.Status);
                Assert.AreEqual(0, shell.Management.Market.ActiveRowCount);
                StringAssert.DoesNotContain("0", shell.Management.Market.StatusText);

                PoseMk(shell, MapMarketFixtures.Mixed());
                Assert.AreEqual(7, shell.Management.Market.ActiveRowCount);
                Assert.IsTrue(shell.Management.Market.FixtureVisible);
                Assert.AreEqual("izmir/steel", shell.Management.Market.SelectedId);
                Assert.AreEqual(0, shell.Management.Market.MarkerCount);
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.01f);
                Assert.AreEqual(size, camera.orthographicSize, 0.01f);

                shell.SetManagementExpanded(true);
                shell.Management.Market.Select("izmir/steel");
                Assert.IsTrue(shell.Management.Market.DetailVisible);
                StringAssert.Contains("steel", shell.Management.Market.DetailName);
                StringAssert.DoesNotContain("LGC", shell.Management.Market.DetailBody);
                Assert.IsFalse(string.IsNullOrEmpty(shell.Management.Market.DetailBody));
                Assert.IsFalse(shell.Management.Market.PriceVisible);
                Assert.IsFalse(shell.Management.Market.TrendVisible);
                StringAssert.DoesNotContain("trend", shell.Management.Market.DetailBody.ToLowerInvariant());
                Assert.IsTrue(shell.Management.Market.CityActive);

                shell.Management.Market.Select("synthetic_city/ore");
                Assert.IsFalse(shell.Management.Market.CityActive);
                Assert.IsFalse(shell.TryFocusMarketCity());
                shell.Management.Market.Select("trabzon/coal");
                Assert.IsFalse(shell.Management.Market.TryFocusGps());

                shell.Management.Market.Select("izmir/steel");
                var before = rig.TargetCenter;
                Assert.IsTrue(shell.TryFocusMarketCity());
                Assert.AreEqual("izmir", shell.ShownCity);
                Assert.Greater(Vector2.Distance(before, rig.TargetCenter), 0.01f);
                var afterFocus = rig.TargetCenter;

                shell.Management.Market.SetCargoFilter("steel");
                Assert.AreEqual(1, shell.Management.Market.ActiveRowCount);

                MapFleetStore.Override(MapFleetFixtures.ReadyThree());
                shell.OpenManagement(MapModule.Fleet);
                Assert.AreEqual(3, shell.Management.Fleet.ActiveRowCount);
                MapJobsStore.Override(MapJobsFixtures.Mixed());
                shell.OpenManagement(MapModule.Jobs);
                Assert.Greater(shell.Management.Jobs.ActiveRowCount, 0);
                MapWarehouseStore.Override(MapWarehouseFixtures.Mixed());
                shell.OpenManagement(MapModule.Warehouses);
                Assert.Greater(shell.Management.Warehouses.ActiveRowCount, 0);
                shell.OpenManagement(MapModule.Market);
                Assert.IsTrue(shell.Management.Market.gameObject.activeSelf);
                Assert.IsFalse(shell.Management.Fleet.gameObject.activeSelf);
                Assert.IsFalse(shell.Management.Jobs.gameObject.activeSelf);
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
                MapMarketStore.ClearOverride();
                MapFleetStore.ClearOverride();
                MapJobsStore.ClearOverride();
                MapWarehouseStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void MarketRows_MeetTouchPolicyAndStackOnPhone()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                PoseMk(shell, MapMarketFixtures.Mixed());
                AssertHits(shell, 2868f, 1320f, Phone(), 3f);
                AssertHits(shell, 1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f), 1f);
                AssertHits(shell, 1280f, 720f, new Rect(0f, 0f, 1280f, 720f), 1f);
                AssertHits(shell, 932f, 430f, new Rect(32f, 16f, 868f, 398f), 1f);
                shell.Apply(932f, 430f, new Rect(32f, 16f, 868f, 398f));
                shell.OpenManagement(MapModule.Market);
                shell.SetManagementExpanded(true);
                shell.Management.Market.Select("ankara/Güterverkehr-Stückgutsendung");
                Assert.IsTrue(shell.Management.Market.DetailVisible);
                Assert.IsFalse(shell.Management.Market.ListVisible);
                StringAssert.Contains("Güterverkehr-Stückgutsendung", shell.Management.Market.DetailName);
                shell.Management.Market.ShowList();
                Assert.IsTrue(shell.Management.Market.ListVisible);
                var list = shell.Management.Market.GetComponentInChildren<ScrollRect>(true);
                Assert.IsNotNull(list);
                Assert.IsFalse(list.horizontal);
            }
            finally
            {
                MapMarketStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TwentyMarketCycles_KeepOneShellAndStayCheap()
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
                    PoseMk(shell, MapMarketFixtures.Mixed());
                    shell.OpenManagement(MapModule.Market);
                    shell.SetManagementExpanded(true);
                    shell.Management.Market.Select("istanbul/grain");
                    shell.OpenManagement(MapModule.Fleet);
                    shell.OpenManagement(MapModule.Jobs);
                    shell.OpenManagement(MapModule.Warehouses);
                    shell.OpenManagement(MapModule.Market);
                    shell.CloseContext();
                    MapMarketStore.ClearOverride();
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
                TestContext.WriteLine("Market cycle avg ms=" + avgMs.ToString("0.000"));
                TestContext.WriteLine("Transform count=" + stable);
                Assert.Less(avgMs, 40.0);
            }
            finally
            {
                MapMarketStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void AssertHits(MapCommandShell shell, float width, float height, Rect safe, float pointScale)
        {
            shell.Apply(width, height, safe);
            shell.OpenManagement(MapModule.Market);
            shell.SetManagementExpanded(true);
            var need = MapCommandLayout.TouchPixels(width, height);
            Assert.AreEqual(44f * pointScale, need, 0.01f);
            var row = shell.Management.Market.RowHitPixels(0);
            Assert.GreaterOrEqual(row.height, need - 0.5f, width + "x" + height);
            Assert.Greater(shell.Management.Market.ActiveRowCount, 0);
            var panel = shell.Management.PanelRect;
            var scale = MapHudLayout.CanvasScale(width, height);
            var y = safe.yMin + panel.anchoredPosition.y * scale;
            Assert.GreaterOrEqual(y, shell.SummaryRect.yMax - 1f, width + " footer overlap");
            shell.CloseContext();
        }

        static void PoseMk(MapCommandShell shell, MapMarketSnapshot snapshot)
        {
            MapMarketStore.Override(snapshot);
            if (!shell.ManagementOpen)
                shell.OpenManagement(MapModule.Market);
            else
                shell.Management.Show(MapModule.Market);
            shell.Management.Market.Refresh();
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
