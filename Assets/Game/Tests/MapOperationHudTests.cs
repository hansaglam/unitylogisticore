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
    public sealed class MapOperationHudTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        [TearDown]
        public void ClearStrip()
        {
            MapOperationStrip.ClearOverride();
        }

        [Test]
        public void FromBackend_NeverInventsActiveOperations()
        {
            Assert.AreEqual(MapOperationStatus.Unconfigured, MapOperationStrip.FromBackend(Phase(ClientBackendPhase.Idle)).Status);
            Assert.AreEqual(MapOperationStatus.Unconfigured, MapOperationStrip.FromBackend(Phase(ClientBackendPhase.Offline)).Status);
            Assert.AreEqual(MapOperationStatus.Unconfigured, MapOperationStrip.FromBackend(Phase(ClientBackendPhase.Refused)).Status);
            Assert.AreEqual(MapOperationStatus.Loading, MapOperationStrip.FromBackend(Phase(ClientBackendPhase.Starting)).Status);
            Assert.AreEqual(MapOperationStatus.Unconfigured, MapOperationStrip.FromBackend(Phase(ClientBackendPhase.Ready)).Status);
            Assert.AreEqual(MapOperationStatus.Error, MapOperationStrip.FromBackend(Phase(ClientBackendPhase.Failed)).Status);
            var ready = MapOperationStrip.FromBackend(Phase(ClientBackendPhase.Ready));
            Assert.AreEqual(0, ready.ActiveCount);
            Assert.IsFalse(ready.CanOpenJobs);
            Assert.IsFalse(ready.Synthetic);
            Assert.IsFalse(ready.Verified);
            Assert.IsFalse(ready.TrustedRead);
            var live = MapOperationStrip.Current;
            Assert.IsFalse(MapOperationStrip.HasOverride);
            Assert.IsFalse(live.CanOpenJobs);
            Assert.IsFalse(live.Synthetic);
            Assert.IsFalse(live.Verified);
            Assert.IsTrue(
                live.Status == MapOperationStatus.Unconfigured
                || live.Status == MapOperationStatus.Loading
                || live.Status == MapOperationStatus.Error,
                live.Status.ToString());
        }

        [Test]
        public void OpsStrip_HasTrEnDeWithoutInventedCounts()
        {
            MapSceneSetup.EnsureLocalization();
            var collection = LocalizationEditorSettings.GetStringTableCollection(OpsContent.Table);
            Assert.AreEqual("Checking operations…", Value(collection, "en", OpsContent.StripLoading));
            Assert.AreEqual("Operasyonlar kontrol ediliyor…", Value(collection, "tr", OpsContent.StripLoading));
            Assert.AreEqual("Operationen werden geprüft…", Value(collection, "de", OpsContent.StripLoading));
            Assert.AreEqual("Operations could not be loaded.", Value(collection, "en", OpsContent.StripError));
            Assert.AreEqual("Operasyonlar yüklenemedi.", Value(collection, "tr", OpsContent.StripError));
            Assert.AreEqual("Operationen konnten nicht geladen werden.", Value(collection, "de", OpsContent.StripError));
            Assert.AreEqual("No active operations.", Value(collection, "en", OpsContent.StripIdle));
            Assert.AreEqual("Aktif operasyon yok.", Value(collection, "tr", OpsContent.StripIdle));
            Assert.AreEqual("Keine aktiven Operationen.", Value(collection, "de", OpsContent.StripIdle));
            Assert.AreEqual("One active operation.", Value(collection, "en", OpsContent.StripSingle));
            Assert.AreEqual("Bir aktif operasyon.", Value(collection, "tr", OpsContent.StripSingle));
            Assert.AreEqual("Eine aktive Operation.", Value(collection, "de", OpsContent.StripSingle));
            Assert.AreEqual("Several active operations.", Value(collection, "en", OpsContent.StripMultiple));
            Assert.AreEqual("Birden fazla aktif operasyon.", Value(collection, "tr", OpsContent.StripMultiple));
            Assert.AreEqual("Mehrere aktive Operationen.", Value(collection, "de", OpsContent.StripMultiple));
            var keys = new[]
            {
                OpsContent.StripLoading,
                OpsContent.StripError,
                OpsContent.StripIdle,
                OpsContent.StripSingle,
                OpsContent.StripMultiple
            };
            for (var i = 0; i < keys.Length; i++)
            {
                AssertNoDigits(Value(collection, "en", keys[i]));
                AssertNoDigits(Value(collection, "tr", keys[i]));
                AssertNoDigits(Value(collection, "de", keys[i]));
            }
        }

        [Test]
        public void HeaderAndFooter_PreserveBrandRegionAndTruthfulUnconfiguredState()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                shell.Apply(2868f, 1320f, Phone());
                Assert.AreEqual("Logisti", Find(shell.transform, "Brand").GetComponent<Text>().text);
                Assert.AreEqual("Core", Find(shell.transform, "BrandCore").GetComponent<Text>().text);
                Assert.IsNull(Find(shell.transform, "Brand").GetComponent<Button>());
                Assert.IsNotNull(shell.RegionButton);
                Assert.IsTrue(shell.SelectRegion(5));
                Assert.AreEqual(5, shell.RegionIndex);
                Assert.AreEqual(1, Count<MapCommandShell>(scene));
                Assert.AreEqual(1, Count<EventSystem>(scene));
                Assert.IsFalse(MapOperationStrip.HasOverride);
                Assert.AreNotEqual(MapOperationStatus.Idle, shell.StripStatus);
                Assert.AreNotEqual(MapOperationStatus.SingleActive, shell.StripStatus);
                Assert.AreNotEqual(MapOperationStatus.MultipleActive, shell.StripStatus);
                Assert.IsFalse(shell.StripActionEnabled);
                Assert.IsTrue(string.IsNullOrEmpty(shell.StripActionText));
                AssertNoDigits(shell.SummaryText);
                StringAssert.DoesNotContain("Ankara", shell.SummaryText);
                StringAssert.DoesNotContain("$", shell.SummaryText);
                var german = MapCommandLayout.LongestSummary;
                var metrics = MapCommandLayout.Evaluate(2868f, 1320f, Phone(), false);
                Assert.IsTrue(MapCommandLayout.LabelFits(german, metrics.FontPixels, Phone().width - 48f));
                Assert.IsTrue(MapCommandLayout.LabelFits("Nordamerika", metrics.CanvasFont, 280f)
                    || "Nordamerika".Length * metrics.CanvasFont * 0.55f <= 280f);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void FooterStates_UseFixturesOnlyAndJobsCtaOpensExistingPanel()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                Assert.IsTrue(shell.SelectRegion(1));
                shell.ShowCity("izmir");
                var rig = Find<MapCameraRig>(scene);
                var camera = rig.GetComponent<Camera>();
                var pos = camera.transform.position;
                var size = camera.orthographicSize;
                var mapId = FindNamed(scene, "Map").GetInstanceID();

                Pose(shell, MapOperationSnapshot.Fixture(MapOperationStatus.Loading));
                Assert.AreEqual(MapOperationStatus.Loading, shell.StripStatus);
                Assert.IsFalse(shell.StripActionEnabled);
                shell.OpenJobsFromStrip();
                Assert.IsFalse(shell.ManagementOpen);

                Pose(shell, MapOperationSnapshot.Fixture(MapOperationStatus.Error));
                Assert.AreEqual(MapOperationStatus.Error, shell.StripStatus);
                AssertNoDigits(shell.SummaryText);

                Pose(shell, MapOperationSnapshot.Fixture(MapOperationStatus.Idle));
                Assert.AreEqual(MapOperationStatus.Idle, shell.StripStatus);
                Assert.IsFalse(shell.StripActionEnabled);

                Pose(shell, MapOperationSnapshot.Fixture(MapOperationStatus.SingleActive, 1, "Ankara", "İzmir", ""));
                Assert.AreEqual(MapOperationStatus.SingleActive, shell.StripStatus);
                StringAssert.Contains("Ankara", shell.SummaryText);
                StringAssert.Contains("İzmir", shell.SummaryText);
                Assert.IsTrue(shell.StripActionEnabled);
                shell.OpenJobsFromStrip();
                Assert.IsTrue(shell.ManagementOpen);
                Assert.AreEqual(MapModule.Jobs, shell.ActiveModule);
                Assert.AreEqual(ScenePath, scene.path);
                Assert.AreEqual(1, Count<MapManagementPanel>(scene));

                Pose(shell, MapOperationSnapshot.Fixture(MapOperationStatus.MultipleActive, 3));
                Assert.AreEqual(MapOperationStatus.MultipleActive, shell.StripStatus);
                Assert.IsTrue(shell.StripActionEnabled);
                AssertNoDigits(shell.SummaryText);

                shell.CloseContext();
                MapOperationStrip.ClearOverride();
                shell.RefreshStrip();
                Assert.AreNotEqual(MapOperationStatus.SingleActive, shell.StripStatus);
                Assert.AreNotEqual(MapOperationStatus.MultipleActive, shell.StripStatus);
                Assert.IsFalse(shell.StripActionEnabled);
                StringAssert.DoesNotContain("Ankara", shell.SummaryText);

                Assert.AreEqual(1, shell.RegionIndex);
                Assert.AreEqual("izmir", shell.ShownCity);
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.0001f);
                Assert.AreEqual(size, camera.orthographicSize, 0.0001f);
                Assert.AreEqual(mapId, FindNamed(scene, "Map").GetInstanceID());
                Assert.AreEqual(1, Count<EventSystem>(scene));
                Assert.AreEqual(1, Count<MapCommandShell>(scene));
            }
            finally
            {
                MapOperationStrip.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void Footer_DoesNotOverlapPanelAndKeepsSummaryHeightPolicy()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                var frames = new[]
                {
                    new Vector4(2868f, 1320f, Phone().x, Phone().y),
                    new Vector4(1920f, 1080f, 0f, 0f),
                    new Vector4(1280f, 720f, 0f, 0f),
                    new Vector4(932f, 430f, 32f, 16f)
                };
                var safes = new[]
                {
                    Phone(),
                    new Rect(0f, 0f, 1920f, 1080f),
                    new Rect(0f, 0f, 1280f, 720f),
                    new Rect(32f, 16f, 868f, 398f)
                };
                for (var i = 0; i < frames.Length; i++)
                {
                    var width = frames[i].x;
                    var height = frames[i].y;
                    var safe = safes[i];
                    shell.Apply(width, height, safe);
                    Assert.LessOrEqual(shell.SummaryRect.yMax, shell.Viewport.yMin + 0.5f, width + "x" + height);
                    Assert.GreaterOrEqual(shell.HeaderRect.yMin, shell.Viewport.yMax - 0.5f, width + "x" + height);
                    shell.OpenManagement(MapModule.Fleet);
                    var panel = shell.Management.PanelRect;
                    var scale = MapHudLayout.CanvasScale(width, height);
                    var y = safe.yMin + panel.anchoredPosition.y * scale;
                    var h = panel.sizeDelta.y * scale;
                    Assert.GreaterOrEqual(y, shell.SummaryRect.yMax - 1f, width + "x" + height);
                    Assert.LessOrEqual(y + h, shell.HeaderRect.yMin + 1f, width + "x" + height);
                    shell.SetManagementExpanded(true);
                    y = safe.yMin + panel.anchoredPosition.y * scale;
                    h = panel.sizeDelta.y * scale;
                    Assert.GreaterOrEqual(y, shell.SummaryRect.yMax - 1f, width + " expanded");
                    shell.CloseContext();
                    var need = MapCommandLayout.TouchPixels(width, height);
                    var summaryH = shell.SummaryRect.height;
                    TestContext.WriteLine(width + "x" + height + " summaryH=" + summaryH.ToString("0.0") + " 44ptPx=" + need.ToString("0.0"));
                    Assert.Greater(summaryH, 20f);
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void RepeatedStripTransitions_StayCheapAndDoNotSpawnHudRoots()
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
                for (var i = 0; i < 40; i++)
                {
                    clock.Restart();
                    Pose(shell, MapOperationSnapshot.Fixture(MapOperationStatus.Loading));
                    Pose(shell, MapOperationSnapshot.Fixture(MapOperationStatus.Error));
                    Pose(shell, MapOperationSnapshot.Fixture(MapOperationStatus.Idle));
                    Pose(shell, MapOperationSnapshot.Fixture(MapOperationStatus.SingleActive, 1, "A", "B", ""));
                    Pose(shell, MapOperationSnapshot.Fixture(MapOperationStatus.MultipleActive, 2));
                    MapOperationStrip.ClearOverride();
                    shell.RefreshStrip();
                    clock.Stop();
                    ticks += clock.ElapsedTicks;
                    Assert.AreEqual(1, Count<MapCommandShell>(scene));
                    Assert.AreEqual(1, Count<EventSystem>(scene));
                }

                var avgMs = ticks / 40.0 * 1000.0 / Stopwatch.Frequency;
                TestContext.WriteLine("Strip cycle avg ms=" + avgMs.ToString("0.000"));
                Assert.Less(avgMs, 25.0);
            }
            finally
            {
                MapOperationStrip.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
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

        static void AssertNoDigits(string value)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(value));
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

        static Transform Find(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

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
