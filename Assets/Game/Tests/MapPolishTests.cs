using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using LogistiCore.EditorTools;
using LogistiCore.Localization;
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
    public sealed class MapPolishTests
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
        public void ClipLine_KeepsIdentityInsideWidth()
        {
            var longName = "Güterverkehr-Sattelzugmaschine";
            var clipped = MapManagementLayout.ClipLine(longName, 12f, 80f);
            Assert.IsTrue(clipped.EndsWith("\u2026") || clipped.Length < longName.Length);
            Assert.LessOrEqual(clipped.Length * 12f * 0.72f, 80f + 1f);
            Assert.AreEqual(longName, MapManagementLayout.ClipLine(longName, 12f, 800f));
        }

        [Test]
        public void JobsMoney_UsesMinorUnitsAndRejectsUnsafeValues()
        {
            string display;
            Assert.IsTrue(MapJobsMoney.TryFormat("6", "TST", 2, ".", out display));
            Assert.AreEqual("0.06 TST", display);
            Assert.IsTrue(MapJobsMoney.TryFormat("6", "TST", 2, ",", out display));
            Assert.AreEqual("0,06 TST", display);
            Assert.IsTrue(MapJobsMoney.TryFormat("1200000", "TST", 2, ".", out display));
            Assert.AreEqual("12,000.00 TST", display);
            Assert.IsTrue(MapJobsMoney.TryFormat("1200000", "TST", 2, ",", out display));
            Assert.AreEqual("12.000,00 TST", display);
            Assert.IsFalse(MapJobsMoney.TryFormat("6.5", "TST", 2, ".", out _));
            Assert.IsFalse(MapJobsMoney.TryFormat("6", "LGCX", 2, ".", out _));
            Assert.IsFalse(MapJobsMoney.TryFormat("6", "TST", 9, ".", out _));
            Assert.IsFalse(MapJobsMoney.TryFormat("6", "", 2, ".", out _));
            Assert.IsFalse(MapJobsMoney.TryFormat("", "TST", 2, ".", out _));
            var mixed = MapJobsFixtures.Mixed();
            mixed.TryGet("no_reward", out var bare);
            Assert.IsFalse(bare.HasSafeReward);
            mixed.TryGet("bad_money", out var bad);
            Assert.IsFalse(bad.HasSafeReward);
        }

        [Test]
        public void FooterCta_StaysDisabledWithoutSyntheticStrip()
        {
            var phone = new Rect(186f, 60f, 2496f, 1200f);
            var summary = MapCommandLayout.Evaluate(2868f, 1320f, phone, false).Summary;
            var need = MapCommandLayout.TouchPixels(2868f, 1320f);
            Assert.Less(summary.height, need);
            var live = MapOperationStrip.FromBackend(
                new LogistiCore.Backend.ClientBackendStatus(
                    LogistiCore.Backend.ClientBackendEnvironment.Unconfigured,
                    LogistiCore.Backend.ClientConnectionDecision.StayOffline,
                    LogistiCore.Backend.ClientBackendPhase.Ready,
                    ""));
            Assert.IsFalse(live.CanOpenJobs);
            Assert.IsFalse(live.Verified);
            Assert.IsFalse(live.TrustedRead);
        }

        [Test]
        public void Polish_CitiesMoneyOverflowMarketAlphaAndCycles()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                shell.Apply(2868f, 1320f, new Rect(186f, 60f, 2496f, 1200f));
                Assert.IsTrue(shell.SelectRegion(1));
                shell.ShowCity("izmir");
                var rig = Find<MapCameraRig>(scene);
                var camera = rig.GetComponent<Camera>();
                var pos = camera.transform.position;
                var size = camera.orthographicSize;
                var mapId = FindNamed(scene, "Map").GetInstanceID();
                var water = Find<MapWaterLabelLayer>(scene);
                var waterId = water != null ? water.GetInstanceID() : 0;
                var presenter = Find<MapRegionalReliefPresenter>(scene);
                var textureId = presenter != null && presenter.RuntimeMaterial != null
                    ? presenter.RuntimeMaterial.GetInstanceID()
                    : 0;

                MapFleetStore.Override(MapFleetFixtures.ReadyThree());
                shell.OpenManagement(MapModule.Fleet);
                AssertContained(shell.Management.Fleet.GetComponentsInChildren<Text>(true));

                MapJobsStore.Override(MapJobsFixtures.Mixed());
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                shell.Management.Jobs.Select("offer_izmir_istanbul");
                StringAssert.DoesNotContain("izmir", shell.Management.Jobs.DetailName);
                StringAssert.Contains("TST", shell.Management.Jobs.DetailBody);
                shell.Management.Jobs.Select("invalid_city");
                StringAssert.DoesNotContain("synthetic_city", shell.Management.Jobs.DetailName);
                shell.Management.Jobs.Select("bad_money");
                StringAssert.DoesNotContain("LGCX", shell.Management.Jobs.DetailBody);
                StringAssert.DoesNotContain("6.5", shell.Management.Jobs.DetailBody);

                MapWarehouseStore.Override(MapWarehouseFixtures.Mixed());
                shell.OpenManagement(MapModule.Warehouses);
                AssertContained(shell.Management.Warehouses.GetComponentsInChildren<Text>(true));

                MapMarketStore.Override(MapMarketFixtures.Mixed());
                shell.Apply(932f, 430f, new Rect(32f, 16f, 868f, 398f));
                shell.OpenManagement(MapModule.Market);
                shell.SetManagementExpanded(true);
                shell.Management.Market.ShowList();
                Assert.AreEqual(0f, shell.Management.Market.ListMinAnchorY);
                shell.Management.Market.Select("izmir/steel");
                Assert.Greater(shell.Management.Market.BackWidth, 44f);
                Assert.IsFalse(shell.Management.Market.GetComponentInChildren<ScrollRect>(true).horizontal);

                var clock = Stopwatch.StartNew();
                var ticks = 0L;
                var stable = 0;
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                for (var i = 0; i < 20; i++)
                {
                    clock.Restart();
                    shell.OpenManagement(MapModule.Fleet);
                    shell.OpenManagement(MapModule.Jobs);
                    shell.OpenManagement(MapModule.Warehouses);
                    shell.OpenManagement(MapModule.Market);
                    shell.SetManagementExpanded(i % 2 == 0);
                    shell.CloseContext();
                    clock.Stop();
                    ticks += clock.ElapsedTicks;
                    Assert.AreEqual(1, Count<MapManagementPanel>(scene), "cycle " + i);
                    Assert.AreEqual(1, Count<EventSystem>(scene), "cycle " + i);
                    var transforms = Count<Transform>(scene);
                    if (i == 0)
                        stable = transforms;
                    else
                        Assert.AreEqual(stable, transforms, "cycle " + i);
                }

                TestContext.WriteLine("Polish cycle avg ms=" + (ticks / 20.0 * 1000.0 / Stopwatch.Frequency).ToString("0.000"));
                TestContext.WriteLine("Transform count=" + stable);
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.01f);
                Assert.AreEqual(size, camera.orthographicSize, 0.01f);
                Assert.AreEqual(1, shell.RegionIndex);
                Assert.AreEqual("izmir", shell.ShownCity);
                Assert.AreEqual(mapId, FindNamed(scene, "Map").GetInstanceID());
                if (water != null)
                    Assert.AreEqual(waterId, water.GetInstanceID());
                if (presenter != null && presenter.RuntimeMaterial != null)
                    Assert.AreEqual(textureId, presenter.RuntimeMaterial.GetInstanceID());
                Assert.IsFalse(shell.StripActionEnabled);
            }
            finally
            {
                MapFleetStore.ClearOverride();
                MapJobsStore.ClearOverride();
                MapWarehouseStore.ClearOverride();
                MapMarketStore.ClearOverride();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        static void AssertContained(Text[] labels)
        {
            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null || !label.gameObject.activeInHierarchy)
                    continue;
                if (label.horizontalOverflow == HorizontalWrapMode.Wrap)
                    continue;
                var width = label.rectTransform.rect.width;
                if (width <= 1f)
                    continue;
                Assert.LessOrEqual(MapCommandLayout.TextWidth(label.text, label.fontSize), width + 8f, label.text);
            }
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
