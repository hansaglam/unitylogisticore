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
    public sealed class MapJobsPanelTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        [TearDown]
        public void Clear()
        {
            MapJobsStore.ClearOverride();
            MapFleetStore.ClearOverride();
            MapOperationStrip.ClearOverride();
        }

        [Test]
        public void FromBackend_NeverInventsJobs()
        {
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.FromBackend(Phase(ClientBackendPhase.Idle)).Status);
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.FromBackend(Phase(ClientBackendPhase.Ready)).Status);
            Assert.AreEqual(MapJobsStatus.Loading, MapJobsStore.FromBackend(Phase(ClientBackendPhase.Starting)).Status);
            Assert.AreEqual(MapJobsStatus.Error, MapJobsStore.FromBackend(Phase(ClientBackendPhase.Failed)).Status);
            Assert.AreEqual(0, MapJobsStore.FromBackend(Phase(ClientBackendPhase.Ready)).Items.Length);
            MapJobsStore.Override(MapJobsSnapshot.Unconfigured());
            Assert.IsFalse(MapJobsStore.HasOverride);
            var live = MapJobsStore.Current;
            Assert.IsFalse(live.Synthetic);
            Assert.AreNotEqual(MapJobsStatus.Ready, live.Status);
            Assert.AreNotEqual(MapJobsStatus.Empty, live.Status);
        }

        [Test]
        public void Snapshot_DedupsMissingAndUnexpectedWithoutInventingMoney()
        {
            var mixed = MapJobsFixtures.Mixed();
            Assert.AreEqual(MapJobsStatus.Ready, mixed.Status);
            Assert.IsTrue(mixed.TryGet("offer_izmir_istanbul", out var offered));
            Assert.AreEqual(MapJobContractStatus.Offered, offered.ContractStatus);
            Assert.AreEqual("0.06 TST", offered.RewardDisplay);
            Assert.IsTrue(offered.HasSafeReward);
            Assert.AreEqual(2, offered.Scale);
            Assert.AreEqual("6", offered.Reward);
            Assert.AreEqual(6, mixed.CountIn(MapJobCategory.Available));
            Assert.AreEqual(4, mixed.CountIn(MapJobCategory.Active));
            Assert.AreEqual(2, mixed.CountIn(MapJobCategory.History));
            Assert.IsFalse(mixed.TryGet("", out _));
            Assert.AreEqual(MapJobContractStatus.Unknown, MapJobsItem.ParseContract("paused"));
            mixed.TryGet("unexpected_status", out var unknown);
            Assert.AreEqual(MapJobContractStatus.Unknown, unknown.ContractStatus);
            Assert.IsTrue(unknown.InCategory(MapJobCategory.Active));
            mixed.TryGet("no_reward", out var bare);
            Assert.IsFalse(bare.HasReward);
            Assert.AreEqual("", bare.RewardDisplay);
            mixed.TryGet("invalid_city", out var invalid);
            Assert.IsFalse(invalid.HasOriginCity);
            Assert.IsFalse(invalid.HasDestinationCity);
            Assert.AreEqual(MapJobsStatus.Empty, MapJobsFixtures.Empty().Status);
            Assert.AreEqual(0, MapJobsFixtures.Empty().Items.Length);
        }

        [Test]
        public void SetCategory_ClearsSelectionOutsideCategory()
        {
            MapJobsStore.Override(MapJobsFixtures.Mixed());
            Assert.AreEqual("offer_izmir_istanbul", MapJobsStore.Current.SelectedId);
            MapJobsStore.SetCategory(MapJobCategory.History);
            Assert.AreEqual(MapJobCategory.History, MapJobsStore.Current.Category);
            Assert.AreEqual("done_antalya_adana", MapJobsStore.Current.SelectedId);
            Assert.IsTrue(MapJobsStore.Current.TryGet(MapJobsStore.Current.SelectedId, out var history));
            Assert.IsTrue(history.InCategory(MapJobCategory.History));
            MapJobsStore.SetCategory(MapJobCategory.Available);
            Assert.AreEqual("offer_izmir_istanbul", MapJobsStore.Current.SelectedId);
            MapJobsStore.SetCategory(MapJobCategory.Active);
            Assert.AreEqual("active_ankara_bursa", MapJobsStore.Current.SelectedId);
            MapJobsStore.Select("done_antalya_adana");
            Assert.AreEqual("", MapJobsStore.Current.SelectedId);
        }

        [Test]
        public void TryApplyTrusted_DropsMissingSelectionAndKeepsCategory()
        {
            MapJobsStore.ClearTrusted();
            var kept = new MapJobsItem(
                "done_izmir",
                MapJobContractStatus.Completed,
                false,
                "izmir",
                "istanbul",
                "lc_dev_general_freight_v1",
                "1000",
                "kg",
                "1000",
                "LGC",
                2,
                "",
                MapJobDeliveryStatus.None,
                "",
                "");
            var first = new MapJobsSnapshot(
                MapJobsStatus.Ready,
                new[] { kept },
                "done_izmir",
                MapJobCategory.History,
                false);
            Assert.IsTrue(MapJobsStore.TryApplyTrusted(first));
            Assert.AreEqual(MapJobCategory.History, MapJobsStore.Current.Category);
            Assert.AreEqual("done_izmir", MapJobsStore.Current.SelectedId);

            var offered = new MapJobsItem(
                "offer_only",
                MapJobContractStatus.Offered,
                false,
                "ankara",
                "bursa",
                "lc_dev_general_freight_v1",
                "1",
                "kg",
                "1000",
                "LGC",
                2,
                "",
                MapJobDeliveryStatus.None,
                "",
                "");
            var next = new MapJobsSnapshot(
                MapJobsStatus.Ready,
                new[] { offered },
                "done_izmir",
                MapJobCategory.Available,
                false);
            Assert.IsTrue(MapJobsStore.TryApplyTrusted(next));
            Assert.AreEqual(MapJobCategory.History, MapJobsStore.Current.Category);
            Assert.AreEqual("", MapJobsStore.Current.SelectedId);
            Assert.AreEqual(0, MapJobsStore.Current.CountIn(MapJobCategory.History));
            MapJobsStore.ClearTrusted();
        }

        [Test]
        public void RewardAndCargo_UseLocaleNotTechnicalId()
        {
            Assert.IsTrue(MapJobsMoney.TryFormat("1000", "LGC", 2, ",", out var tr));
            Assert.AreEqual("10,00 LGC", tr);
            Assert.IsTrue(MapJobsMoney.TryFormat("1000", "LGC", 2, ".", out var en));
            Assert.AreEqual("10.00 LGC", en);
            Assert.IsTrue(MapJobsMoney.TryGroupDigits("1000", ".", out var groupedTr));
            Assert.AreEqual("1.000", groupedTr);
            Assert.IsTrue(MapJobsMoney.TryGroupDigits("1000", ",", out var groupedEn));
            Assert.AreEqual("1,000", groupedEn);
            Assert.AreEqual(OpsContent.JobsCargoGeneralFreight, OpsContent.CargoKey("lc_dev_general_freight_v1"));
            Assert.AreEqual("", OpsContent.CargoKey("lc_dev_general_freight"));
        }

        [Test]
        public void OpsJobs_HasTrEnDeWithoutInventedCounts()
        {
            MapSceneSetup.EnsureLocalization();
            var collection = LocalizationEditorSettings.GetStringTableCollection(OpsContent.Table);
            Assert.AreEqual("Açık", Value(collection, "tr", OpsContent.JobsAvailable));
            Assert.AreEqual("Aktif", Value(collection, "tr", OpsContent.JobsActive));
            Assert.AreEqual("Verfügbar", Value(collection, "de", OpsContent.JobsAvailable));
            Assert.AreEqual("Auftragsdaten konnten nicht geladen werden.", Value(collection, "de", OpsContent.JobsError));
            Assert.AreEqual("Güterverkehr-Stückgutsendung".Length > 20, true);
            Assert.AreEqual("General Freight", Value(collection, "en", OpsContent.JobsCargoGeneralFreight));
            Assert.AreEqual("Genel Yük", Value(collection, "tr", OpsContent.JobsCargoGeneralFreight));
            Assert.AreEqual("Stückgut", Value(collection, "de", OpsContent.JobsCargoGeneralFreight));
            Assert.AreEqual("Unknown cargo", Value(collection, "en", OpsContent.JobsCargoUnknown));
            Assert.AreEqual("Bilinmeyen yük", Value(collection, "tr", OpsContent.JobsCargoUnknown));
            Assert.AreEqual("Unbekannte Fracht", Value(collection, "de", OpsContent.JobsCargoUnknown));
            Assert.AreEqual("Durum", Value(collection, "tr", OpsContent.JobsStatusField));
            Assert.AreEqual("Accept", Value(collection, "en", OpsContent.JobsAccept));
            Assert.AreEqual("Kabul Et", Value(collection, "tr", OpsContent.JobsAccept));
            Assert.AreEqual("Annehmen", Value(collection, "de", OpsContent.JobsAccept));
            Assert.AreEqual("Acceptance result is not confirmed.", Value(collection, "en", OpsContent.JobsAcceptUncertain));
            Assert.AreEqual("Kabul sonucu doğrulanmadı.", Value(collection, "tr", OpsContent.JobsAcceptUncertain));
            Assert.AreEqual("Annahmeergebnis ist unbestätigt.", Value(collection, "de", OpsContent.JobsAcceptUncertain));
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
        public void JobsPanel_StatesSelectionFocusAndFleetStayOnTheMap()
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

                shell.OpenManagement(MapModule.Jobs);
                Assert.IsTrue(shell.ManagementOpen);
                Assert.AreEqual(MapJobsStatus.Unconfigured, shell.Management.Jobs.Status);
                Assert.AreEqual(0, shell.Management.Jobs.ActiveRowCount);
                Assert.IsFalse(shell.Management.Jobs.FixtureVisible);
                Assert.IsFalse(shell.TryFocusJobOrigin());
                StringAssert.DoesNotContain("offer_izmir", shell.Management.Jobs.StatusText);

                PoseJobs(shell, MapJobsFixtures.Loading());
                Assert.AreEqual(MapJobsStatus.Loading, shell.Management.Jobs.Status);
                Assert.AreEqual(0, shell.Management.Jobs.ActiveRowCount);

                PoseJobs(shell, MapJobsFixtures.Error());
                Assert.AreEqual(MapJobsStatus.Error, shell.Management.Jobs.Status);

                PoseJobs(shell, MapJobsFixtures.Empty());
                Assert.AreEqual(MapJobsStatus.Empty, shell.Management.Jobs.Status);
                Assert.AreEqual(0, shell.Management.Jobs.ActiveRowCount);
                StringAssert.DoesNotContain("0", shell.Management.Jobs.StatusText);

                PoseJobs(shell, MapJobsFixtures.Mixed());
                Assert.AreEqual(MapJobsStatus.Ready, shell.Management.Jobs.Status);
                Assert.IsTrue(shell.Management.Jobs.FixtureVisible);
                Assert.AreEqual(6, shell.Management.Jobs.ActiveRowCount);
                Assert.AreEqual("offer_izmir_istanbul", shell.Management.Jobs.SelectedId);
                Assert.AreEqual(0, shell.Management.Jobs.RouteGraphicCount);
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.01f);
                Assert.AreEqual(size, camera.orthographicSize, 0.01f);

                shell.SetManagementExpanded(true);
                shell.Management.Jobs.Select("offer_izmir_istanbul");
                Assert.IsTrue(shell.Management.Jobs.DetailVisible);
                Assert.IsFalse(shell.Management.Jobs.RowTitleOverlapsMeta(0));
                Assert.IsFalse(shell.Management.Jobs.DetailBlocksOverlap());
                StringAssert.DoesNotContain("ops.jobs", shell.Management.Jobs.DetailBody);
                StringAssert.DoesNotContain("lc_dev", shell.Management.Jobs.RowMetaText(0));
                StringAssert.DoesNotContain("izmir", shell.Management.Jobs.DetailName);
                StringAssert.DoesNotContain("istanbul", shell.Management.Jobs.DetailName);
                StringAssert.Contains("→", shell.Management.Jobs.DetailName);
                var rewardBody = shell.Management.Jobs.DetailBody;
                Assert.IsTrue(rewardBody.Contains("0.06 TST") || rewardBody.Contains("0,06 TST"), rewardBody);
                StringAssert.DoesNotContain("Prämie 6 ", rewardBody);
                Assert.IsFalse(shell.Management.Jobs.DetailBody.Contains("ETA"));
                Assert.IsTrue(shell.Management.Jobs.OriginActive);
                Assert.IsTrue(shell.Management.Jobs.DestinationActive);

                shell.Management.Jobs.SetCategory(MapJobCategory.Active);
                Assert.AreEqual(4, shell.Management.Jobs.ActiveRowCount);
                Assert.AreEqual("active_ankara_bursa", shell.Management.Jobs.SelectedId);
                shell.Management.Jobs.Select("active_ankara_bursa");
                StringAssert.Contains("2026-11-05T20:00:00.000Z", shell.Management.Jobs.DetailBody);

                shell.Management.Jobs.SetCategory(MapJobCategory.History);
                Assert.AreEqual(2, shell.Management.Jobs.ActiveRowCount);
                Assert.AreEqual("done_antalya_adana", shell.Management.Jobs.SelectedId);
                shell.Management.Jobs.Select("done_antalya_adana");
                StringAssert.DoesNotContain("antalya", shell.Management.Jobs.DetailName);
                StringAssert.Contains("→", shell.Management.Jobs.DetailName);

                shell.Management.Jobs.SetCategory(MapJobCategory.Available);
                Assert.AreEqual("offer_izmir_istanbul", shell.Management.Jobs.SelectedId);
                shell.Management.Jobs.Select("invalid_city");
                Assert.IsFalse(shell.Management.Jobs.OriginActive);
                Assert.IsFalse(shell.TryFocusJobOrigin());
                Assert.AreEqual(pos, camera.transform.position);

                shell.Management.Jobs.Select("offer_izmir_istanbul");
                var before = rig.TargetCenter;
                Assert.IsTrue(shell.TryFocusJobOrigin());
                Assert.AreEqual("izmir", shell.ShownCity);
                Assert.Greater(Vector2.Distance(before, rig.TargetCenter), 0.01f);
                var afterFocus = rig.TargetCenter;

                MapFleetStore.Override(MapFleetFixtures.ReadyThree());
                shell.OpenManagement(MapModule.Fleet);
                Assert.IsTrue(shell.Management.Fleet.gameObject.activeSelf);
                Assert.IsFalse(shell.Management.Jobs.gameObject.activeSelf);
                Assert.AreEqual(3, shell.Management.Fleet.ActiveRowCount);
                shell.OpenManagement(MapModule.Jobs);
                Assert.IsFalse(shell.Management.Fleet.gameObject.activeSelf);
                Assert.IsTrue(shell.Management.Jobs.gameObject.activeSelf);

                MapOperationStrip.Override(MapOperationSnapshot.Fixture(MapOperationStatus.SingleActive, 1));
                shell.RefreshStrip();
                shell.CloseContext();
                shell.OpenJobsFromStrip();
                Assert.AreEqual(MapModule.Jobs, shell.ActiveModule);
                Assert.AreEqual(1, Count<MapManagementPanel>(scene));

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
                Assert.AreEqual(ScenePath, scene.path);
            }
            finally
            {
                MapJobsStore.ClearOverride();
                MapFleetStore.ClearOverride();
                MapOperationStrip.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void JobsRows_MeetTouchPolicyAndIsolateScroll()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                PoseJobs(shell, MapJobsFixtures.Mixed());
                AssertHits(shell, 2868f, 1320f, Phone(), 3f);
                AssertHits(shell, 1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f), 1f);
                AssertHits(shell, 1280f, 720f, new Rect(0f, 0f, 1280f, 720f), 1f);
                AssertHits(shell, 932f, 430f, new Rect(32f, 16f, 868f, 398f), 1f);
                shell.Apply(932f, 430f, new Rect(32f, 16f, 868f, 398f));
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                shell.Management.Jobs.Select("long_de_cargo");
                Assert.IsTrue(shell.Management.Jobs.DetailVisible);
                Assert.IsFalse(shell.Management.Jobs.ListVisible);
                shell.Management.Jobs.ShowList();
                Assert.IsTrue(shell.Management.Jobs.ListVisible);
                var list = shell.Management.Jobs.GetComponentInChildren<ScrollRect>(true);
                Assert.IsNotNull(list);
                Assert.IsFalse(list.horizontal);
            }
            finally
            {
                MapJobsStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TwentyJobsCycles_KeepOneShellAndStayCheap()
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
                    PoseJobs(shell, MapJobsFixtures.Mixed());
                    shell.OpenManagement(MapModule.Jobs);
                    shell.SetManagementExpanded(true);
                    shell.Management.Jobs.Select("active_ankara_bursa");
                    shell.OpenManagement(MapModule.Fleet);
                    shell.OpenManagement(MapModule.Jobs);
                    shell.CloseContext();
                    MapJobsStore.ClearOverride();
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
                TestContext.WriteLine("Jobs cycle avg ms=" + avgMs.ToString("0.000"));
                TestContext.WriteLine("Transform count=" + stable);
                Assert.Less(avgMs, 40.0);
            }
            finally
            {
                MapJobsStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void AssertHits(MapCommandShell shell, float width, float height, Rect safe, float pointScale)
        {
            shell.Apply(width, height, safe);
            shell.OpenManagement(MapModule.Jobs);
            shell.SetManagementExpanded(true);
            var need = MapCommandLayout.TouchPixels(width, height);
            Assert.AreEqual(44f * pointScale, need, 0.01f);
            var row = shell.Management.Jobs.RowHitPixels(0);
            Assert.GreaterOrEqual(row.height, need - 0.5f, width + "x" + height);
            var cat = shell.Management.Jobs.CategoryHitPixels(0);
            Assert.GreaterOrEqual(cat.height, need - 0.5f, width + " category");
            Assert.Greater(shell.Management.Jobs.ActiveRowCount, 0);
            var panel = shell.Management.PanelRect;
            var scale = MapHudLayout.CanvasScale(width, height);
            var y = safe.yMin + panel.anchoredPosition.y * scale;
            Assert.GreaterOrEqual(y, shell.SummaryRect.yMax - 1f, width + " footer overlap");
            shell.CloseContext();
        }

        static void PoseJobs(MapCommandShell shell, MapJobsSnapshot snapshot)
        {
            MapJobsStore.Override(snapshot);
            if (!shell.ManagementOpen)
                shell.OpenManagement(MapModule.Jobs);
            else
                shell.Management.Show(MapModule.Jobs);
            shell.Management.Jobs.Refresh();
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
