using System.IO;
using System.Security.Cryptography;
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
    public sealed class MapIos04aR1Tests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        [Test]
        public void Rail_FirstRowStartsVisibleAndLastRowFitsAfterScroll()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var settings = Sha(ProjectSettingsPath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = FreshShell(scene);
                AssertRailEnds(shell, 2868f, 1320f, Phone(), 132f);
                AssertRailEnds(shell, 1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f), 44f);
                AssertRailEnds(shell, 1280f, 720f, new Rect(0f, 0f, 1280f, 720f), 44f);
                AssertRailEnds(shell, 932f, 430f, Compact(), 44f);
                Assert.AreEqual(1, Count<EventSystem>(scene));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
            Assert.AreEqual(settings, Sha(ProjectSettingsPath));
        }

        [Test]
        public void Rail_ScrollDoesNotFireARowAndPreservesOffsetThroughSoon()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = FreshShell(scene);
                shell.Apply(2868f, 1320f, Phone());
                Canvas.ForceUpdateCanvases();
                Assert.IsTrue(shell.RailScroll.vertical);
                shell.ScrollRail(0f);
                var bottom = shell.RailScroll.verticalNormalizedPosition;
                Assert.Less(bottom, 0.08f);
                var row = Find(shell.transform, "Mode7").GetComponent<EventTrigger>();
                Assert.IsNotNull(row);
                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    dragging = true
                };
                row.OnPointerClick(pointer);
                Assert.IsFalse(shell.SoonOpen);
                pointer.dragging = false;
                row.OnPointerClick(pointer);
                Assert.IsFalse(shell.SoonOpen);
                Assert.IsFalse(shell.ManagementOpen);
                Assert.Less(shell.RailScroll.verticalNormalizedPosition, 0.08f);
                Assert.Less(Mathf.Abs(shell.RailScroll.verticalNormalizedPosition - bottom), 0.05f);
                Assert.AreEqual(0, Count<MapSoonPanel>(scene));
                Assert.AreEqual(1, Count<EventSystem>(scene));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Region_LastLocalizedRowIsSelectableAfterScroll()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = FreshShell(scene);
                var rig = Find<MapCameraRig>(scene);
                shell.Apply(2868f, 1320f, Phone());
                shell.PreviewRegionLabel("Okyanusya");
                if (!shell.RegionMenuOpen)
                    shell.ToggleRegionMenu();
                Canvas.ForceUpdateCanvases();
                var first = Find(shell.transform, "Region0") as RectTransform;
                var last = Find(shell.transform, "Region7") as RectTransform;
                Assert.IsTrue(Inside(shell.RegionScroll.viewport, first), "initial first");
                Assert.IsFalse(Inside(shell.RegionScroll.viewport, last), "initial last may sit below");
                shell.ScrollRegion(0f);
                Assert.IsTrue(Inside(shell.RegionScroll.viewport, last), "bottom last row");
                Assert.IsTrue(Inside(shell.RegionScroll.viewport, last.Find("Name") as RectTransform), "bottom last text");
                Assert.IsTrue(shell.SelectRegion(7));
                Assert.AreEqual(7, shell.RegionIndex);
                Assert.AreEqual(MapScope.Oceania, rig.Scope);
                Assert.IsFalse(shell.RegionMenuOpen);

                shell.Apply(932f, 430f, Compact());
                shell.PreviewRegionLabel("Ozeanien");
                if (!shell.RegionMenuOpen)
                    shell.ToggleRegionMenu();
                Canvas.ForceUpdateCanvases();
                last = Find(shell.transform, "Region7") as RectTransform;
                shell.ScrollRegion(0f);
                Assert.IsTrue(Inside(shell.RegionScroll.viewport, last), "932 last row");
                var held = shell.RegionIndex;
                shell.CloseRegionMenu();
                Assert.AreEqual(held, shell.RegionIndex);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TwentyCycles_KeepScrollReachableAndOneEventSystem()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = FreshShell(scene);
                shell.Apply(2868f, 1320f, Phone());
                var first = Find(shell.transform, "Mode0") as RectTransform;
                var last = Find(shell.transform, "Mode7") as RectTransform;
                for (var i = 0; i < 20; i++)
                {
                    shell.ScrollRail(1f);
                    Assert.IsTrue(Inside(shell.RailScroll.viewport, first), "cycle " + i + " first");
                    shell.ShowModule(0);
                    shell.SetManagementExpanded(true);
                    shell.Management.CloseButton.onClick.Invoke();
                    shell.ScrollRail(0f);
                    Assert.IsTrue(Inside(shell.RailScroll.viewport, last), "cycle " + i + " last");
                    shell.ShowModule(7);
                    shell.CloseSoon();
                    Assert.AreEqual(1, Count<MapCommandShell>(scene), "cycle " + i);
                    Assert.AreEqual(1, Count<EventSystem>(scene), "cycle " + i);
                    Assert.AreEqual(0, Count<MapSoonPanel>(scene), "cycle " + i);
                    Assert.AreEqual(SceneSha, Sha(ScenePath), "cycle " + i);
                }

                shell.ScrollRail(1f);
                Assert.IsTrue(Inside(shell.RailScroll.viewport, first), "final first");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void CloseAndSoon_SurviveRailScrollOffset()
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
                shell.ScrollRail(0f);
                for (var i = 0; i < 4; i++)
                {
                    shell.ShowModule(i);
                    Assert.AreEqual(MapPanelIcons.Get(MapPanelIcons.Close), shell.Management.CloseSprite);
                    shell.SetManagementExpanded(true);
                    shell.Management.CloseButton.onClick.Invoke();
                    Assert.IsFalse(shell.ManagementOpen);
                }

                shell.ScrollRail(0f);
                shell.ShowModule(7);
                Assert.IsFalse(shell.SoonOpen);
                Assert.IsFalse(shell.ManagementOpen);
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.0001f);
                Assert.AreEqual(size, camera.orthographicSize, 0.0001f);
                Assert.AreEqual(1, shell.RegionIndex);
                Assert.AreEqual("trabzon", shell.ShownCity);
                Assert.AreEqual(1, Count<MapCommandShell>(scene));
                Assert.AreEqual(1, Count<EventSystem>(scene));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void AssertRailEnds(MapCommandShell shell, float width, float height, Rect safe, float minHit)
        {
            shell.Apply(width, height, safe);
            Canvas.ForceUpdateCanvases();
            var scale = MapHudLayout.CanvasScale(width, height);
            var scroll = shell.RailScroll;
            var first = Find(shell.transform, "Mode0") as RectTransform;
            var last = Find(shell.transform, "Mode7") as RectTransform;
            Assert.GreaterOrEqual(first.sizeDelta.y * scale, minHit - 1f, width + " first hit");
            Assert.GreaterOrEqual(last.sizeDelta.y * scale, minHit - 1f, width + " last hit");
            Assert.IsTrue(Inside(scroll.viewport, first), width + " initial first");
            Assert.IsTrue(Inside(scroll.viewport, first.Find("Icon") as RectTransform), width + " initial icon");
            Assert.IsTrue(Inside(scroll.viewport, first.Find("Label") as RectTransform), width + " initial label");
            if (!scroll.vertical)
            {
                Assert.IsTrue(Inside(scroll.viewport, last), width + " unscrolled last");
                return;
            }

            Assert.IsFalse(Inside(scroll.viewport, last), width + " last starts below the viewport");
            shell.ScrollRail(0f);
            Assert.IsTrue(Inside(scroll.viewport, last), width + " last after scroll");
            Assert.IsTrue(Inside(scroll.viewport, last.Find("Icon") as RectTransform), width + " last icon");
            Assert.IsTrue(Inside(scroll.viewport, last.Find("Label") as RectTransform), width + " last label");
            shell.ScrollRail(1f);
            Assert.IsTrue(Inside(scroll.viewport, first), width + " first after return");
        }

        static bool Inside(RectTransform viewport, RectTransform item)
        {
            if (viewport == null || item == null)
                return false;
            var view = new Vector3[4];
            var local = new Vector3[4];
            viewport.GetWorldCorners(view);
            item.GetWorldCorners(local);
            for (var i = 0; i < 4; i++)
            {
                if (local[i].x < view[0].x - 1f || local[i].x > view[2].x + 1f || local[i].y < view[0].y - 1f || local[i].y > view[2].y + 1f)
                    return false;
            }

            return true;
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

        static string Sha(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var text = new char[hash.Length * 2];
                for (var i = 0; i < hash.Length; i++)
                {
                    var value = hash[i];
                    text[i * 2] = (char)(value >> 4 < 10 ? '0' + (value >> 4) : 'a' + ((value >> 4) - 10));
                    text[i * 2 + 1] = (char)((value & 15) < 10 ? '0' + (value & 15) : 'a' + ((value & 15) - 10));
                }

                return new string(text);
            }
        }
    }
}
