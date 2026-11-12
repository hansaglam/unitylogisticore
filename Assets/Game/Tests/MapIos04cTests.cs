using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using LogistiCore.EditorTools;
using LogistiCore.Localization;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace LogistiCore.Tests
{
    public sealed class MapIos04cTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        [TearDown]
        public void Clear()
        {
            MapRoadDeliveryStore.ClearOverride();
            MapJobsStore.ClearOverride();
        }

        [Test]
        public void Road_OpensManagementNotComingSoon()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = FreshShell(scene);
                shell.Apply(2868f, 1320f, Phone());
                shell.ShowModule(4);
                Assert.IsTrue(shell.ManagementOpen);
                Assert.AreEqual(MapModule.Road, shell.ActiveModule);
                Assert.IsFalse(shell.SoonOpen);
                Assert.AreEqual(0, Count<MapSoonPanel>(scene));
                Assert.AreEqual(MapRoadDeliveryStatus.Unconfigured, shell.Management.Road.Status);
                Assert.IsFalse(shell.Management.Road.FixtureVisible);
                Assert.AreEqual(0, shell.Management.Road.ActiveRowCount);
                Assert.IsFalse(string.IsNullOrWhiteSpace(shell.Management.Road.StatusText));
                Assert.AreEqual(MapRoadDeliveryStatus.Unconfigured, shell.Management.Road.Status);
                Assert.AreEqual(0, shell.Management.Road.RouteGraphicCount);
                shell.Management.CloseButton.onClick.Invoke();
                Assert.IsFalse(shell.ManagementOpen);
                Assert.AreEqual(1, Count<EventSystem>(scene));
            }
            finally
            {
                Close(scene);
            }
        }

        [Test]
        public void TransportRows_ShowComingSoonWithoutPopupOrBlocker()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = FreshShell(scene);
                shell.Apply(2868f, 1320f, Phone());
                shell.OpenManagement(MapModule.Warehouses);
                shell.SetManagementExpanded(true);
                for (var i = 5; i <= 7; i++)
                {
                    var button = Find(shell.transform, "Mode" + i).GetComponent<Button>();
                    Assert.IsFalse(button.interactable, "mode " + i);
                    var soon = Find(shell.transform, "Mode" + i).Find("Status").GetComponent<Text>().text;
                    Assert.IsTrue(soon == "YAKINDA" || soon == "COMING SOON" || soon == "DEMNÄCHST", soon);
                    shell.ShowModule(i);
                    button.onClick.Invoke();
                    Assert.IsFalse(shell.SoonOpen);
                    Assert.AreEqual(0, Count<MapSoonPanel>(scene));
                    Assert.AreEqual(MapModule.Warehouses, shell.ActiveModule);
                }

                Assert.IsTrue(shell.Management.CloseButton.interactable);
                shell.Management.CloseButton.onClick.Invoke();
                Assert.IsFalse(shell.ManagementOpen);
                Assert.IsNull(Object.FindAnyObjectByType<MapSoonPanel>());
            }
            finally
            {
                Close(scene);
            }
        }

        [Test]
        public void Road_FixturesStayEditorOnlyAndSupportListDetail()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = FreshShell(scene);
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                MapRoadDeliveryStore.Override(MapRoadFixtures.Many());
                shell.ShowModule(4);
                shell.SetManagementExpanded(true);
                var view = shell.Management.Road;
                Assert.IsTrue(view.FixtureVisible);
                Assert.AreEqual(3, view.ActiveRowCount);
                view.Select("d2");
                Assert.IsTrue(view.DetailVisible);
                Assert.IsTrue(view.OriginActive);
                Assert.IsTrue(view.DestinationActive);
                MapRoadDeliveryStore.ClearOverride();
                view.Refresh();
                Assert.AreEqual(MapRoadDeliveryStatus.Unconfigured, view.Status);
                Assert.IsFalse(view.FixtureVisible);
                Assert.AreEqual(0, view.ActiveRowCount);
            }
            finally
            {
                Close(scene);
            }
        }

        [Test]
        public void Header_BrandStaysClearAcrossRegionCycle()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = FreshShell(scene);
                var rig = Find<MapCameraRig>(scene);
                AssertRegions(shell, 2868f, 1320f, Phone(), new[] { 0, 1, 5, 6, 0, 2, 1 });
                AssertRegions(shell, 932f, 430f, Compact(), new[] { 5, 6, 0, 7, 1 });
                shell.Apply(2868f, 1320f, Phone());
                var held = rig.Scope;
                if (!shell.RegionMenuOpen)
                    shell.ToggleRegionMenu();
                shell.CloseRegionMenu();
                Assert.AreEqual(held, rig.Scope);
                for (var i = 0; i < 20; i++)
                {
                    Assert.IsTrue(shell.SelectRegion(i % 8));
                    Assert.IsFalse(shell.RegionChipOverlapsBrand(), "cycle " + i);
                    Assert.AreEqual(1, Count<EventSystem>(scene));
                    Assert.AreEqual(SceneSha, Sha(ScenePath));
                }
            }
            finally
            {
                Close(scene);
            }
        }

        [Test]
        public void Modules_SwitchWithRoadAndRestoreGestures()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = FreshShell(scene);
                var camera = Find<MapCameraRig>(scene).GetComponent<Camera>();
                shell.Apply(2868f, 1320f, Phone());
                Assert.IsTrue(shell.SelectRegion(1));
                shell.ShowCity("trabzon");
                var pos = camera.transform.position;
                var size = camera.orthographicSize;
                shell.ShowModule(0);
                shell.ShowModule(4);
                Assert.AreEqual(MapModule.Road, shell.ActiveModule);
                shell.ShowModule(1);
                Assert.AreEqual(MapModule.Jobs, shell.ActiveModule);
                shell.ShowModule(4);
                shell.ShowModule(2);
                Assert.AreEqual(MapModule.Warehouses, shell.ActiveModule);
                shell.ShowModule(4);
                shell.ShowModule(3);
                Assert.AreEqual(MapModule.Market, shell.ActiveModule);
                shell.SetManagementExpanded(true);
                shell.Management.CloseButton.onClick.Invoke();
                Assert.IsFalse(shell.ManagementOpen);
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.0001f);
                Assert.AreEqual(size, camera.orthographicSize, 0.0001f);
                Assert.AreEqual(1, shell.RegionIndex);
                Assert.AreEqual("trabzon", shell.ShownCity);
                Assert.AreEqual(1, Count<MapManagementPanel>(scene));
                Assert.AreEqual(0, Count<MapSoonPanel>(scene));
            }
            finally
            {
                Close(scene);
            }
        }

        [Test]
        public void TwentyCycles_KeepOnePanelNoSoonAndNoReload()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = FreshShell(scene);
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                var clock = Stopwatch.StartNew();
                var ticks = 0L;
                for (var i = 0; i < 20; i++)
                {
                    clock.Restart();
                    shell.ShowModule(4);
                    shell.SetManagementExpanded(true);
                    shell.ShowModule(5);
                    shell.Management.CloseButton.onClick.Invoke();
                    shell.ShowModule(1);
                    shell.CloseContext();
                    clock.Stop();
                    ticks += clock.ElapsedTicks;
                    Assert.AreEqual(1, Count<MapCommandShell>(scene), "cycle " + i);
                    Assert.AreEqual(1, Count<EventSystem>(scene), "cycle " + i);
                    Assert.LessOrEqual(Count<MapManagementPanel>(scene), 1, "cycle " + i);
                    Assert.AreEqual(0, Count<MapSoonPanel>(scene), "cycle " + i);
                    Assert.AreEqual(SceneSha, Sha(ScenePath), "cycle " + i);
                }

                TestContext.WriteLine("ios04c cycle avg ms=" + (ticks / 20.0 * 1000.0 / Stopwatch.Frequency).ToString("0.000"));
            }
            finally
            {
                Close(scene);
            }
        }

        [Test]
        public void Input_TransportTapDoesNotLeaveABlocker()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = FreshShell(scene);
                shell.Apply(1280f, 720f, new Rect(0f, 0f, 1280f, 720f));
                shell.ScrollRail(0f);
                var row = (RectTransform)Find(shell.transform, "Mode7");
                var system = Object.FindAnyObjectByType<EventSystem>();
                Assert.IsNotNull(system);
                var pointer = new PointerEventData(system) { position = ScreenPoint(row) };
                var hits = new List<RaycastResult>();
                if (system != null)
                    system.RaycastAll(pointer, hits);
                Assert.IsFalse(shell.SoonOpen);
                Assert.AreEqual(0, Count<MapSoonPanel>(scene));
                shell.ShowModule(7);
                Assert.IsFalse(shell.SoonOpen);
                hits.Clear();
                pointer.position = new Vector2(900f, 360f);
                system.RaycastAll(pointer, hits);
                for (var i = 0; i < hits.Count; i++)
                    Assert.IsFalse(hits[i].gameObject.name.Contains("Soon"), hits[i].gameObject.name);
            }
            finally
            {
                Close(scene);
            }
        }

        static void AssertRegions(MapCommandShell shell, float width, float height, Rect safe, int[] order)
        {
            shell.Apply(width, height, safe);
            var brandX = BrandX(shell);
            for (var i = 0; i < order.Length; i++)
            {
                Assert.IsTrue(shell.SelectRegion(order[i]));
                Canvas.ForceUpdateCanvases();
                Assert.IsFalse(shell.RegionChipOverlapsBrand(), width + " region " + order[i]);
                Assert.AreEqual(brandX, BrandX(shell), 1.5f, width + " brand shift");
            }
        }

        static float BrandX(MapCommandShell shell)
        {
            var core = Find(shell.transform, "BrandCore") as RectTransform;
            return core.anchoredPosition.x;
        }

        static Vector2 ScreenPoint(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Vector2((corners[0].x + corners[2].x) * 0.5f, (corners[0].y + corners[2].y) * 0.5f);
        }

        static Rect Phone()
        {
            return new Rect(186f, 60f, 2496f, 1200f);
        }

        static Rect Compact()
        {
            return new Rect(32f, 16f, 868f, 398f);
        }

        static MapCommandShell FreshShell(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var shells = roots[i].GetComponentsInChildren<MapCommandShell>(true);
                for (var s = 0; s < shells.Length; s++)
                    Object.DestroyImmediate(shells[s].gameObject);
            }

            return MapCommandShell.Attach(scene);
        }

        static T Find<T>(Scene scene) where T : Object
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var hit = roots[i].GetComponentInChildren<T>(true);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var hit = Find(root.GetChild(i), name);
                if (hit != null)
                    return hit;
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

        static void Close(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        static string Sha(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var text = new char[hash.Length * 2];
                for (var i = 0; i < hash.Length; i++)
                {
                    var value = hash[i].ToString("x2");
                    text[i * 2] = value[0];
                    text[i * 2 + 1] = value[1];
                }

                return new string(text);
            }
        }
    }
}
