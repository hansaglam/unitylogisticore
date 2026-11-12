using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using LogistiCore.EditorTools;
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
    public sealed class MapIos04aTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        [Test]
        public void CloseIcon_IsDrawnAndClosesCompactExpandedAndAllModules()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var settingsBefore = Sha(ProjectSettingsPath);
            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                shell.Apply(2868f, 1320f, Phone());
                Assert.IsTrue(shell.SelectRegion(1));
                shell.ShowCity("istanbul");
                var camera = Find<MapCameraRig>(scene).GetComponent<Camera>();
                var pos = camera.transform.position;
                var size = camera.orthographicSize;
                var closeSprite = MapPanelIcons.Get(MapPanelIcons.Close);
                Assert.IsNotNull(closeSprite);
                Assert.Greater(closeSprite.texture.width, 8);

                for (var module = 0; module < 4; module++)
                {
                    shell.ShowModule(module);
                    var panel = shell.Management;
                    Assert.IsTrue(panel.IsOpen, "open " + module);
                    Assert.AreEqual(closeSprite, panel.CloseSprite);
                    Assert.Greater(panel.CloseIconAlpha, 0.9f);
                    Assert.GreaterOrEqual(panel.CloseHitPixels().width, 131f);
                    Assert.GreaterOrEqual(panel.CloseHitPixels().height, 131f);
                    Assert.IsTrue(panel.CloseButton.interactable);
                    panel.CloseButton.onClick.Invoke();
                    Assert.IsFalse(shell.ManagementOpen, "close compact " + module);
                    Assert.AreEqual(MapModule.None, shell.ActiveModule);
                    Assert.AreEqual(MapCommandShell.PanelState.Closed, shell.State);

                    shell.ShowModule(module);
                    shell.SetManagementExpanded(true);
                    Assert.AreEqual(closeSprite, shell.Management.CloseSprite);
                    shell.Management.CloseButton.onClick.Invoke();
                    Assert.IsFalse(shell.ManagementOpen, "close expanded " + module);
                    Assert.AreEqual(pos.x, camera.transform.position.x, 0.0001f);
                    Assert.AreEqual(size, camera.orthographicSize, 0.0001f);
                    Assert.AreEqual(1, shell.RegionIndex);
                    Assert.AreEqual("istanbul", shell.ShownCity);
                }

                Assert.AreEqual(1, Count<EventSystem>(scene));
                Assert.AreEqual(1, Count<MapManagementPanel>(scene));
                Assert.IsFalse(shell.ManagementOpen);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
            Assert.AreEqual(settingsBefore, Sha(ProjectSettingsPath));
        }

        [Test]
        public void TransportEntries_OpenOneComingSoonPopupAndRestoreMap()
        {
            MapSceneSetup.EnsureLocalization();
            var collection = LocalizationEditorSettings.GetStringTableCollection(MapContent.Table);
            Assert.AreEqual("Karayolu", Value(collection, "tr", MapContent.RailRoad));
            Assert.AreEqual("Demiryolu", Value(collection, "tr", MapContent.RailRail));
            Assert.AreEqual("Denizyolu", Value(collection, "tr", MapContent.RailSea));
            Assert.AreEqual("Havayolu", Value(collection, "tr", MapContent.RailAir));
            Assert.AreEqual("Road", Value(collection, "en", MapContent.RailRoad));
            Assert.AreEqual("Straße", Value(collection, "de", MapContent.RailRoad));
            Assert.AreEqual("YAKINDA", Value(collection, "tr", MapContent.ModeSoon));
            Assert.AreEqual("Bu taşımacılık modu henüz kullanıma açık değil.", Value(collection, "tr", MapContent.SoonBody));
            Assert.AreEqual("This transport mode is not available yet.", Value(collection, "en", MapContent.SoonBody));
            Assert.AreEqual("Dieser Transportmodus ist noch nicht verfügbar.", Value(collection, "de", MapContent.SoonBody));

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                shell.Apply(2868f, 1320f, Phone());
                var camera = Find<MapCameraRig>(scene).GetComponent<Camera>();
                var pos = camera.transform.position;
                var size = camera.orthographicSize;
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);

                shell.ShowModule(4);
                Assert.IsFalse(shell.SoonOpen);
                Assert.AreEqual(0, Count<MapSoonPanel>(scene));
                Assert.IsTrue(shell.ManagementOpen);
                Assert.AreEqual(MapModule.Road, shell.ActiveModule);
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.0001f);
                Assert.AreEqual(size, camera.orthographicSize, 0.0001f);

                for (var i = 5; i <= 7; i++)
                {
                    shell.ShowModule(i);
                    Assert.IsFalse(shell.SoonOpen, "soon " + i);
                    Assert.AreEqual(0, Count<MapSoonPanel>(scene));
                    Assert.AreEqual(MapModule.Road, shell.ActiveModule);
                    var status = Find(shell.transform, "Mode" + i).Find("Status").GetComponent<Text>().text;
                    Assert.IsTrue(status == "YAKINDA" || status == "COMING SOON" || status == "DEMNÄCHST", status);
                    Assert.IsFalse(Find(shell.transform, "Mode" + i).GetComponent<Button>().interactable);
                }

                Assert.IsTrue(shell.ManagementOpen);
                Assert.AreEqual(MapModule.Road, shell.ActiveModule);
                Assert.AreEqual(pos.x, camera.transform.position.x, 0.0001f);
                Assert.AreEqual(size, camera.orthographicSize, 0.0001f);

                shell.CloseContext();
                shell.ShowModule(4);
                Assert.IsFalse(shell.SoonOpen);
                Assert.AreEqual(MapModule.Road, shell.ActiveModule);
                shell.CloseContext();
                Assert.IsFalse(shell.ManagementOpen);
                Assert.AreEqual(1, Count<EventSystem>(scene));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void RegionMenu_StaysInsideSafeAreaOnPhoneAndCompactGerman()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                AssertMenuInside(shell, 2868f, 1320f, Phone(), new[]
                {
                    "Dünya", "Türkiye", "Avrupa", "Asya", "Afrika", "Kuzey Amerika", "Güney Amerika", "Okyanusya"
                });
                AssertMenuInside(shell, 932f, 430f, Compact(), new[]
                {
                    "Welt", "Türkei", "Europa", "Asien", "Afrika", "Nordamerika", "Südamerika", "Ozeanien"
                });
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void BlackSea_SitsNorthOfTheTurkishCoastAndYieldsToCities()
        {
            Assert.Greater(Water("black_sea").Latitude, 41.75f);
            Assert.IsTrue(MapCities.TryGet("istanbul", out var istanbul));
            Assert.IsTrue(MapCities.TryGet("trabzon", out var trabzon));
            Assert.Greater(Water("black_sea").Latitude, (float)istanbul.Latitude);
            Assert.Greater(Water("black_sea").Latitude, (float)trabzon.Latitude);
            Assert.AreEqual("Karadeniz", MapWaterLabels.Name(Water("black_sea"), "tr"));
            Assert.AreEqual("Black Sea", MapWaterLabels.Name(Water("black_sea"), "en"));
            Assert.AreEqual("Schwarzes Meer", MapWaterLabels.Name(Water("black_sea"), "de"));

            MapSceneSetup.EnsureLocalization();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var camera = Find<MapCameraRig>(scene).GetComponent<Camera>();
                var shell = MapCommandShell.Attach(scene);
                var countries = MapCountryLabelLayer.Attach(scene);
                var layer = MapWaterLabelLayer.Attach(scene);
                shell.Apply(2868f, 1320f, Phone());
                Assert.IsTrue(shell.SelectRegion(1));
                PoseTurkey(camera, 2868, 1320, Phone());
                countries.Refresh(2868, 1320, Phone(), "tr", false);
                layer.Refresh(2868, 1320, Phone(), "tr", false);
                var placed = new MapWaterLabelLayer.Placed[MapWaterLabels.Capacity];
                var copied = layer.CopyPlaced(placed);
                AssertPlaced(placed, copied, "black_sea", "Karadeniz");
                AssertPlaced(placed, copied, "aegean_sea", "Ege Denizi");
                AssertPlaced(placed, copied, "mediterranean_sea", "Akdeniz");
                AssertWaterClearOfCities(placed, copied, camera, 2868, 1320, Phone(), "black_sea");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TwentyCycles_KeepOneSoonPanelAndRestoreGestures()
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
                    shell.ShowModule(0);
                    shell.SetManagementExpanded(true);
                    shell.ShowModule(4);
                    shell.ShowModule(5);
                    shell.ShowModule(6);
                    shell.ShowModule(7);
                    shell.CloseSoon();
                    shell.CloseContext();
                    clock.Stop();
                    ticks += clock.ElapsedTicks;
                    Assert.AreEqual(1, Count<MapCommandShell>(scene), "cycle " + i);
                    Assert.AreEqual(1, Count<EventSystem>(scene), "cycle " + i);
                    Assert.LessOrEqual(Count<MapManagementPanel>(scene), 1, "cycle " + i);
                    Assert.AreEqual(0, Count<MapSoonPanel>(scene), "cycle " + i);
                    Assert.IsFalse(shell.SoonOpen);
                    Assert.IsFalse(shell.ManagementOpen);
                }

                TestContext.WriteLine("ios04a cycle avg ms=" + (ticks / 20.0 * 1000.0 / Stopwatch.Frequency).ToString("0.000"));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void AssertMenuInside(MapCommandShell shell, float width, float height, Rect safe, string[] labels)
        {
            shell.Apply(width, height, safe);
            Canvas.ForceUpdateCanvases();
            for (var i = 0; i < labels.Length; i++)
            {
                Assert.IsTrue(shell.SelectRegion(i));
                shell.PreviewRegionLabel(labels[i]);
            }

            if (!shell.RegionMenuOpen)
                shell.ToggleRegionMenu();
            Canvas.ForceUpdateCanvases();
            var scale = MapHudLayout.CanvasScale(width, height);
            var menu = Find(shell.transform, "RegionMenu") as RectTransform;
            var left = safe.xMin + menu.anchoredPosition.x * scale;
            var bottom = safe.yMin + menu.anchoredPosition.y * scale;
            var right = left + menu.sizeDelta.x * scale;
            var top = bottom + menu.sizeDelta.y * scale;
            Assert.GreaterOrEqual(left, safe.xMin - 0.5f, width + "x" + height);
            Assert.GreaterOrEqual(bottom, safe.yMin - 0.5f, width + "x" + height);
            Assert.LessOrEqual(right, safe.xMax + 0.5f, width + "x" + height);
            Assert.LessOrEqual(top, safe.yMax + 0.5f, width + "x" + height);
            var row = Find(shell.transform, "Region0") as RectTransform;
            Assert.GreaterOrEqual(row.sizeDelta.y * scale, MapCommandLayout.TouchPixels(width, height) - 1.5f);
            shell.ToggleRegionMenu();
        }

        static void AssertWaterClearOfCities(
            MapWaterLabelLayer.Placed[] placed,
            int count,
            Camera camera,
            int width,
            int height,
            Rect safe,
            string id)
        {
            var aspect = width / (float)height;
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkeyFrame);
            var scale = MapHudLayout.CanvasScale(width, height);
            var together = new bool[MapCities.All.Length * MapCities.All.Length];
            var parent = new int[MapCities.All.Length];
            var obstacles = new Rect[MapCities.All.Length * 2];
            var obstacleCount = MapCountryLabels.CityObstacles(
                new Vector2(camera.transform.position.x, camera.transform.position.y),
                camera.orthographicSize,
                width,
                height,
                turkeyFrame,
                scale,
                together,
                parent,
                obstacles);
            for (var i = 0; i < count; i++)
            {
                if (placed[i].Id != id)
                    continue;
                for (var o = 0; o < obstacleCount; o++)
                    Assert.IsFalse(MapCountryLabels.Overlaps(placed[i].Bounds, obstacles[o]), id + " overlapped a city plate.");
            }
        }

        static void AssertPlaced(MapWaterLabelLayer.Placed[] placed, int count, string id, string text)
        {
            for (var i = 0; i < count; i++)
            {
                if (placed[i].Id == id)
                {
                    Assert.AreEqual(text, placed[i].Text, id);
                    return;
                }
            }

            Assert.Fail("missing water label " + id);
        }

        static void PoseTurkey(Camera camera, int width, int height, Rect safe)
        {
            var aspect = width / (float)height;
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out var center, out var size);
            size = MapViewMath.ClampSize(size, MapFrames.MinimumOrthographicSize(aspect), MapFrames.MaximumOrthographicSize(aspect));
            MapCameraRig.PresentationTravel(MapScope.Turkey, size, width, height, safe, worldMin, worldMax, out var travelMin, out var travelMax);
            center = MapViewMath.ClampCenter(center, size, aspect, travelMin, travelMax);
            MapCameraRig.FitCitiesInside(center, size, aspect, width, height, safe, worldMin, worldMax, out center, out size);
            camera.orthographic = true;
            camera.aspect = aspect;
            camera.orthographicSize = size;
            camera.transform.position = new Vector3(center.x, center.y, MapCameraRig.CameraZ);
        }

        static MapWaterCatalog.Entry Water(string id)
        {
            return MapWaterCatalog.All[MapWaterCatalog.IndexOf(id)];
        }

        static Vector2 CardCenter(MapSoonPanel panel)
        {
            var corners = new Vector3[4];
            panel.CardRect.GetWorldCorners(corners);
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
