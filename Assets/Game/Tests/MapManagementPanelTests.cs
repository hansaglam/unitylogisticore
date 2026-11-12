using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
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
    public sealed class MapManagementPanelTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        [Test]
        public void OpsTable_HasFleetJobsWarehouseMarketCopy()
        {
            MapSceneSetup.EnsureLocalization();
            var collection = LocalizationEditorSettings.GetStringTableCollection(OpsContent.Table);
            Assert.IsNotNull(collection);
            Assert.AreEqual("Filo", Value(collection, "tr", OpsContent.TitleFleet));
            Assert.AreEqual("İşler", Value(collection, "tr", OpsContent.TitleJobs));
            Assert.AreEqual("Depolar", Value(collection, "tr", OpsContent.TitleWarehouses));
            Assert.AreEqual("Pazar", Value(collection, "tr", OpsContent.TitleMarket));
            Assert.AreEqual("Keine Live-Fuhrparkverbindung.", Value(collection, "de", OpsContent.BodyFleet));
            Assert.AreEqual("Keine Live-Auftragsverbindung.", Value(collection, "de", OpsContent.BodyJobs));
            Assert.AreEqual("Lagerhäuser", Value(collection, "de", OpsContent.TitleWarehouses));
            for (var i = 0; i < OpsContent.Keys.Length; i++)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(Value(collection, "en", OpsContent.Keys[i])));
                Assert.IsFalse(string.IsNullOrWhiteSpace(Value(collection, "tr", OpsContent.Keys[i])));
                Assert.IsFalse(string.IsNullOrWhiteSpace(Value(collection, "de", OpsContent.Keys[i])));
            }
        }

        [Test]
        public void ManagementPanels_StayInMapSceneAndPreserveCameraRegionCity()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var beforeSettings = Sha(ProjectSettingsPath);
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                var again = MapCommandShell.Attach(scene);
                Assert.AreSame(shell, again);
                shell.Apply(2868f, 1320f, Phone());
                Assert.IsTrue(shell.SelectRegion(2));
                shell.ShowCity("izmir");
                var rig = Find<MapCameraRig>(scene);
                var camera = rig.GetComponent<Camera>();
                var map = FindNamed(scene, "Map");
                var water = Find<MapWaterLabelLayer>(scene);
                var presenter = Find<MapRegionalReliefPresenter>(scene);
                var mapId = map.GetInstanceID();
                var waterId = water != null ? water.GetInstanceID() : 0;
                var texture = presenter != null ? presenter.RuntimeMaterial : null;
                var textureId = texture != null ? texture.GetInstanceID() : 0;
                var pos = camera.transform.position;
                var size = camera.orthographicSize;
                var events = Count<EventSystem>(scene);
                Assert.AreEqual(1, events);

                OpenAndAssert(shell, 0, MapModule.Fleet, "izmir", pos, size, camera);
                OpenAndAssert(shell, 1, MapModule.Jobs, "izmir", pos, size, camera);
                OpenAndAssert(shell, 2, MapModule.Warehouses, "izmir", pos, size, camera);
                OpenAndAssert(shell, 3, MapModule.Market, "izmir", pos, size, camera);
                OpenAndAssert(shell, 4, MapModule.Road, "izmir", pos, size, camera);
                Assert.AreEqual(1, Count<MapManagementPanel>(scene));

                shell.SetManagementExpanded(true);
                Assert.IsTrue(shell.ManagementExpanded);
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.0001f);
                Assert.AreEqual(size, camera.orthographicSize, 0.0001f);
                shell.SetManagementExpanded(false);
                Assert.IsFalse(shell.ManagementExpanded);

                shell.CloseContext();
                Assert.IsFalse(shell.ManagementOpen);
                Assert.AreEqual(MapCommandShell.PanelState.Closed, shell.State);
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.0001f);
                Assert.AreEqual(size, camera.orthographicSize, 0.0001f);
                Assert.AreEqual(2, shell.RegionIndex);
                Assert.AreEqual("izmir", shell.ShownCity);
                Assert.AreEqual(mapId, FindNamed(scene, "Map").GetInstanceID());
                Assert.AreEqual(1, Count<EventSystem>(scene));
                Assert.AreEqual(1, Count<MapManagementPanel>(scene));
                Assert.AreEqual(1, Count<MapCommandShell>(scene));
                if (water != null)
                    Assert.AreEqual(waterId, Find<MapWaterLabelLayer>(scene).GetInstanceID());
                if (presenter != null && presenter.RuntimeMaterial != null)
                    Assert.AreEqual(textureId, presenter.RuntimeMaterial.GetInstanceID());

                shell.ShowModule(4);
                Assert.IsFalse(shell.SoonOpen);
                Assert.AreEqual(MapModule.Road, shell.ActiveModule);
                Assert.AreEqual(4, shell.ModuleIndex);
                Assert.AreEqual(0, Count<MapSoonPanel>(scene));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
            Assert.AreEqual(beforeSettings, Sha(ProjectSettingsPath));
        }

        [Test]
        public void CompactPhone_ReachesAllFourModulesThroughThePanelSwitcher()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                var compact = new Rect(32f, 16f, 868f, 398f);
                shell.Apply(932f, 430f, compact);
                Assert.IsTrue(shell.FutureModesVisible);
                shell.ShowModule(0);
                Assert.IsTrue(shell.ManagementOpen);
                Assert.AreEqual(MapModule.Fleet, shell.ActiveModule);
                AssertInsideViewport(shell, compact);
                var panel = shell.Management;
                panel.Choose(1);
                shell.OpenManagement(MapModule.Jobs);
                Assert.AreEqual(MapModule.Jobs, shell.ActiveModule);
                panel.Choose(2);
                shell.OpenManagement(MapModule.Warehouses);
                Assert.AreEqual(MapModule.Warehouses, shell.ActiveModule);
                panel.Choose(3);
                shell.OpenManagement(MapModule.Market);
                Assert.AreEqual(MapModule.Market, shell.ActiveModule);
                panel.Choose(4);
                shell.OpenManagement(MapModule.Road);
                Assert.AreEqual(MapModule.Road, shell.ActiveModule);
                shell.SetManagementExpanded(true);
                AssertInsideViewport(shell, compact);
                Assert.GreaterOrEqual(panel.CloseButton.GetComponent<RectTransform>().sizeDelta.x * MapHudLayout.CanvasScale(932f, 430f), 44f - 1f);
                Assert.IsTrue(panel.ContainsScreenPoint(PanelCenter(panel)));
                shell.CloseContext();
                Assert.IsFalse(panel.ContainsScreenPoint(PanelCenter(panel)));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void TwentyCycles_KeepOnePanelAndOneEventSystem()
        {
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                var clock = Stopwatch.StartNew();
                var openTicks = 0L;
                var switchTicks = 0L;
                var closeTicks = 0L;
                for (var i = 0; i < 20; i++)
                {
                    clock.Restart();
                    shell.OpenManagement(MapModule.Fleet);
                    clock.Stop();
                    openTicks += clock.ElapsedTicks;
                    clock.Restart();
                    shell.OpenManagement(MapModule.Jobs);
                    clock.Stop();
                    switchTicks += clock.ElapsedTicks;
                    shell.SetManagementExpanded(true);
                    Assert.AreEqual(MapPanelIcons.Get(MapPanelIcons.Collapse), shell.Management.ExpandSprite);
                    shell.SetManagementExpanded(false);
                    Assert.AreEqual(MapPanelIcons.Get(MapPanelIcons.Expand), shell.Management.ExpandSprite);
                    clock.Restart();
                    shell.CloseContext();
                    clock.Stop();
                    closeTicks += clock.ElapsedTicks;
                    Assert.AreEqual(1, Count<MapManagementPanel>(scene), "cycle " + i);
                    Assert.AreEqual(1, Count<EventSystem>(scene), "cycle " + i);
                    Assert.AreEqual(1, Count<MapCommandShell>(scene), "cycle " + i);
                }

                var tickMs = 1000.0 / Stopwatch.Frequency;
                TestContext.WriteLine(
                    "Open avg ms=" + (openTicks / 20.0 * tickMs).ToString("0.000")
                    + " switch avg ms=" + (switchTicks / 20.0 * tickMs).ToString("0.000")
                    + " close avg ms=" + (closeTicks / 20.0 * tickMs).ToString("0.000")
                    + " freq=" + Stopwatch.Frequency);
                Assert.IsFalse(shell.ManagementOpen);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void NewPanelControls_MeetFortyFourPointsWithoutWrappedExpandText()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                AssertHits(shell, 2868f, 1320f, Phone(), 3f);
                AssertHits(shell, 1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f), 1f);
                AssertHits(shell, 1280f, 720f, new Rect(0f, 0f, 1280f, 720f), 1f);
                AssertHits(shell, 932f, 430f, new Rect(32f, 16f, 868f, 398f), 1f);
                if (Screen.width != 2868 || Screen.height != 1320)
                    TestContext.WriteLine("World-corner 2868 fixture unverified in this Editor. Screen=" + Screen.width + "x" + Screen.height);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void GermanBody_FitsCompactPanel()
        {
            var metrics = MapCommandLayout.Evaluate(2868f, 1320f, Phone(), false);
            var compact = MapManagementLayout.Compact(metrics.Viewport, metrics.FontPixels, MapCommandLayout.TouchPixels(2868f, 1320f));
            Assert.Greater(compact.width, 180f);
            Assert.IsTrue(MapCommandLayout.LabelFits(OpsContent.LongestBody, metrics.CanvasFont, compact.width - 24f)
                || OpsContent.LongestBody.Length * metrics.CanvasFont * 0.45f <= compact.width * 3f);
            var expanded = MapManagementLayout.Expanded(metrics.Viewport, metrics.FontPixels);
            Assert.Greater(expanded.width, compact.width);
            Assert.IsTrue(metrics.Viewport.Contains(new Vector2(compact.center.x, compact.center.y)));
            Assert.IsTrue(metrics.Viewport.Contains(new Vector2(expanded.xMin + 1f, expanded.yMin + 1f)));
        }

        static void AssertHits(MapCommandShell shell, float width, float height, Rect safe, float expectedPointScale)
        {
            Assert.AreEqual(expectedPointScale, MapCommandLayout.PointScale(width, height), width + "x" + height);
            var scale = MapHudLayout.CanvasScale(width, height);
            var need = MapCommandLayout.TouchPixels(width, height);
            Assert.AreEqual(44f * expectedPointScale, need, 0.01f, width + "x" + height);
            shell.Apply(width, height, safe);
            shell.OpenManagement(MapModule.Fleet);
            var panel = shell.Management;
            Assert.IsTrue(string.IsNullOrEmpty(panel.ExpandText));
            var expandLabel = panel.ExpandButton.transform.Find("Access");
            Assert.IsNotNull(expandLabel);
            Assert.IsFalse(expandLabel.GetComponent<Text>().enabled);
            Assert.AreEqual(MapPanelIcons.Get(MapPanelIcons.Expand), panel.ExpandSprite);
            var close = panel.CloseHitPixels();
            var expand = panel.ExpandHitPixels();
            var chip = panel.ChipHitPixels();
            Assert.GreaterOrEqual(close.width, need - 0.5f, width + " close w");
            Assert.GreaterOrEqual(close.height, need - 0.5f, width + " close h");
            Assert.GreaterOrEqual(expand.width, need - 0.5f, width + " expand w");
            Assert.GreaterOrEqual(expand.height, need - 0.5f, width + " expand h");
            Assert.GreaterOrEqual(chip.height, need - 0.5f, width + " chip h");
            Assert.GreaterOrEqual(chip.width, need - 0.5f, width + " chip w");
            Assert.IsFalse(panel.PanelRect.GetComponent<RectMask2D>());
            panel.ToggleMenu();
            Assert.IsTrue(panel.MenuOpen);
            for (var i = 0; i < 4; i++)
            {
                var choice = panel.ChoiceHitPixels(i);
                Assert.GreaterOrEqual(choice.height, need - 0.5f, width + " choice " + i);
            }

            var closeRect = (RectTransform)panel.CloseButton.transform;
            var expandRect = (RectTransform)panel.ExpandButton.transform;
            var chipRect = (RectTransform)panel.SwitchButton.transform;
            var panelW = panel.PanelRect.sizeDelta.x;
            var chipRight = chipRect.anchoredPosition.x + chipRect.sizeDelta.x;
            var expandLeft = panelW + expandRect.anchoredPosition.x - expandRect.sizeDelta.x;
            var expandRight = panelW + expandRect.anchoredPosition.x;
            var closeLeft = panelW + closeRect.anchoredPosition.x - closeRect.sizeDelta.x;
            Assert.LessOrEqual(chipRight, expandLeft + 0.5f, width + " chip/expand overlap");
            Assert.LessOrEqual(expandRight, closeLeft + 0.5f, width + " expand/close overlap");
            shell.SetManagementExpanded(true);
            Assert.AreEqual(MapPanelIcons.Get(MapPanelIcons.Collapse), panel.ExpandSprite);
            Assert.GreaterOrEqual(panel.ExpandHitPixels().width, need - 0.5f);
            shell.CloseContext();
        }

        static void OpenAndAssert(
            MapCommandShell shell,
            int rail,
            MapModule module,
            string city,
            Vector3 pos,
            float size,
            Camera camera)
        {
            shell.ShowModule(rail);
            Assert.IsTrue(shell.ManagementOpen);
            Assert.AreEqual(module, shell.ActiveModule);
            Assert.IsFalse(shell.ContextOpen);
            Assert.AreEqual(city, shell.ShownCity);
            Assert.AreEqual(pos.x, camera.transform.position.x, 0.0001f);
            Assert.AreEqual(pos.y, camera.transform.position.y, 0.0001f);
            Assert.AreEqual(size, camera.orthographicSize, 0.0001f);
            Assert.AreEqual(1, shell.GetComponentsInChildren<MapManagementPanel>(true).Length);
            if (!string.IsNullOrEmpty(shell.ModuleBody))
                Assert.IsFalse(char.IsDigit(shell.ModuleBody[0]));
        }

        static void AssertInsideViewport(MapCommandShell shell, Rect safe)
        {
            var panel = shell.Management.PanelRect;
            Assert.IsNotNull(panel);
            var scale = MapHudLayout.CanvasScale(safe.width + safe.xMin * 0f + 932f, 430f);
            scale = MapHudLayout.CanvasScale(932f, 430f);
            var x = safe.xMin + panel.anchoredPosition.x * scale;
            var y = safe.yMin + panel.anchoredPosition.y * scale;
            var w = panel.sizeDelta.x * scale;
            var h = panel.sizeDelta.y * scale;
            Assert.GreaterOrEqual(x, shell.Viewport.xMin - 1f);
            Assert.LessOrEqual(x + w, shell.Viewport.xMax + 1f);
            Assert.GreaterOrEqual(y, shell.Viewport.yMin - 1f);
            Assert.LessOrEqual(y + h, shell.Viewport.yMax + 1f);
        }

        static Vector2 PanelCenter(MapManagementPanel panel)
        {
            var rect = panel.PanelRect;
            return rect.TransformPoint(rect.rect.center);
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

        static string Value(UnityEditor.Localization.StringTableCollection collection, string code, string key)
        {
            foreach (var table in collection.StringTables)
            {
                if (table.LocaleIdentifier.Code == code)
                    return table.GetEntry(key) != null ? table.GetEntry(key).Value : null;
            }

            return null;
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
