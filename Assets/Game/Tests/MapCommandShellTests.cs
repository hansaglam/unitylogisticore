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

namespace LogistiCore.Tests
{
    public sealed class MapCommandShellTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        static readonly Rect[] Frames =
        {
            new Rect(0f, 0f, 844f, 390f),
            new Rect(0f, 0f, 932f, 430f),
            new Rect(0f, 0f, 960f, 540f),
            new Rect(0f, 0f, 1280f, 720f),
            new Rect(0f, 0f, 1920f, 1080f),
            new Rect(186f, 60f, 2496f, 1200f)
        };

        static readonly Vector2[] Screens =
        {
            new Vector2(844f, 390f),
            new Vector2(932f, 430f),
            new Vector2(960f, 540f),
            new Vector2(1280f, 720f),
            new Vector2(1920f, 1080f),
            new Vector2(2868f, 1320f)
        };

        [Test]
        public void EightCityHits_StayInsideTheCommandViewport()
        {
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame, 0.0001f);
            Assert.AreEqual(8, MapCities.All.Length);
            for (var i = 0; i < Frames.Length; i++)
            {
                var screen = Screens[i];
                var safe = Frames[i];
                var aspect = screen.x / screen.y;
                MapFrames.Turkey(out var min, out var max);
                MapViewMath.Fit(min, max, aspect, MapFrames.TurkeyFocusPadding, out var center, out var size);
                Assert.AreEqual(size * 0.46f, MapFrames.MinimumOrthographicSize(aspect), 0.001f, screen.ToString());
                MapFrames.WorldPresentation(out var worldMin, out var worldMax);
                MapCameraRig.FitCitiesInside(
                    center,
                    size,
                    aspect,
                    screen.x,
                    screen.y,
                    safe,
                    worldMin,
                    worldMax,
                    out var framedCenter,
                    out var framedSize);
                Assert.GreaterOrEqual(framedSize, size - 0.02f, screen.ToString());
                Assert.LessOrEqual(framedSize, MapFrames.MaximumOrthographicSize(aspect) + 0.02f, screen.ToString());

                var metrics = MapCommandLayout.Evaluate(
                    screen.x,
                    screen.y,
                    safe,
                    true);
                if (!metrics.ContextOpen)
                {
                    metrics = MapCommandLayout.Evaluate(screen.x, screen.y, safe, false);
                }
                var units = MapMarkerLod.UnitsPerPixel(framedSize, screen.y);
                var pad = MapMarkerLod.MarkerHitPixels;
                for (var c = 0; c < MapCities.All.Length; c++)
                {
                    var city = MapCities.All[c];
                    var world = MapProjection.Project(city.Latitude, city.Longitude);
                    var point = MapMarkerLod.ToScreen(world, framedCenter, units, screen.x, screen.y);
                    Assert.GreaterOrEqual(point.x - pad, metrics.Viewport.xMin, city.Id + " " + screen);
                    Assert.LessOrEqual(point.x + pad, metrics.Viewport.xMax, city.Id + " " + screen);
                    Assert.GreaterOrEqual(point.y - pad, metrics.Viewport.yMin, city.Id + " " + screen);
                    Assert.LessOrEqual(point.y + pad, metrics.Viewport.yMax, city.Id + " " + screen);
                }
            }
        }

        [Test]
        public void Pan_KeepsTheUsersFrameWhenChromeRefreshes()
        {
            var cameraObject = new GameObject("command-pan");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = 1920f / 1080f;
                var rig = cameraObject.AddComponent<MapCameraRig>();
                rig.ApplyImmediate(MapScope.Turkey);
                rig.PanScreenDelta(new Vector2(36f, -12f));
                var held = rig.TargetCenter;
                var heldSize = rig.TargetOrthographicSize;
                Assert.IsTrue(rig.UserAdjusted);
                rig.ReframeForChrome();
                Assert.AreEqual(held.x, rig.TargetCenter.x, 0.0001f);
                Assert.AreEqual(held.y, rig.TargetCenter.y, 0.0001f);
                Assert.AreEqual(heldSize, rig.TargetOrthographicSize, 0.0001f);
                Assert.GreaterOrEqual(heldSize, MapFrames.MinimumOrthographicSize(camera.aspect) - 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Copy_OmitsInventedOperationsNumbers()
        {
            MapSceneSetup.EnsureLocalization();
            var collection = LocalizationEditorSettings.GetStringTableCollection(MapContent.Table);
            var keys = new[]
            {
                MapContent.SummaryUnconnected,
                MapContent.ModeSoon,
                MapContent.ModeFleet,
                MapContent.ModeJobs,
                MapContent.ModeWarehouses,
                MapContent.ModeMarket,
                MapContent.ModeRoad,
                MapContent.ModeRail,
                MapContent.ModeSea,
                MapContent.ModeAir
            };

            for (var k = 0; k < keys.Length; k++)
            {
                foreach (var table in collection.StringTables)
                {
                    var value = table.GetEntry(keys[k]).Value;
                    Assert.IsFalse(string.IsNullOrWhiteSpace(value), keys[k]);
                    for (var i = 0; i < value.Length; i++)
                        Assert.IsFalse(char.IsDigit(value[i]), keys[k] + " " + value);
                    StringAssert.DoesNotContain("$", value);
                    StringAssert.DoesNotContain("€", value);
                }
            }

            Assert.AreEqual("Flotte", Value(collection, "de", MapContent.ModeFleet));
            Assert.AreEqual("Aufträge", Value(collection, "de", MapContent.ModeJobs));
            Assert.AreEqual("Straße", Value(collection, "de", MapContent.ModeRoad));
            Assert.AreEqual("Demnächst", Value(collection, "de", MapContent.ModeSoon));
            Assert.AreEqual("This section is not available yet.", Value(collection, "en", MapContent.ModuleUnavailable));
            Assert.AreEqual("Bu bölüm henüz kullanıma hazır değil.", Value(collection, "tr", MapContent.ModuleUnavailable));
            Assert.AreEqual("Dieser Bereich ist noch nicht verfügbar.", Value(collection, "de", MapContent.ModuleUnavailable));
            Assert.AreEqual("HARİTA", Value(collection, "tr", MapContent.RailMap));
            Assert.AreEqual("MAP", Value(collection, "en", MapContent.RailMap));
            Assert.AreEqual("KARTE", Value(collection, "de", MapContent.RailMap));
            Assert.AreEqual("Depolar", Value(collection, "tr", MapContent.RailWarehouses));
            Assert.AreEqual("Depots", Value(collection, "en", MapContent.RailWarehouses));
            Assert.AreEqual("Warehouses", Value(collection, "en", MapContent.ModeWarehouses));
            Assert.AreEqual("Kara", Value(collection, "tr", MapContent.ModeRoad));
            Assert.AreEqual("ROAD", Value(collection, "tr", MapContent.RailRoad));
            Assert.AreEqual("RAIL", Value(collection, "en", MapContent.RailRail));
            Assert.AreEqual("SEA", Value(collection, "de", MapContent.RailSea));
            Assert.AreEqual("AIR", Value(collection, "tr", MapContent.RailAir));
            Assert.AreEqual("Close", Value(collection, "en", MapContent.Close));
            Assert.AreEqual("Kapat", Value(collection, "tr", MapContent.Close));
            Assert.AreEqual("Schließen", Value(collection, "de", MapContent.Close));
            for (var i = 0; i < Value(collection, "de", MapContent.ModuleUnavailable).Length; i++)
                Assert.IsFalse(char.IsDigit(Value(collection, "de", MapContent.ModuleUnavailable)[i]));
            Assert.AreNotEqual(Value(collection, "en", MapContent.ModeFleet), Value(collection, "de", MapContent.ModeFleet));
            Assert.AreEqual("İzmir", Value(collection, "tr", "cities.izmir"));
            Assert.AreEqual("İstanbul", Value(collection, "tr", "cities.istanbul"));
            Assert.AreEqual("Diyarbakır", Value(collection, "tr", "cities.diyarbakir"));
        }

        [Test]
        public void Shell_UsesOneHomeAndDoesNotClickFutureModes()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                var again = MapCommandShell.Attach(scene);
                Assert.AreSame(shell, again);
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                Assert.AreEqual(1f, GraphicAlpha(shell.transform, "CommandHeader"), 0.001f);
                Assert.AreEqual(1f, GraphicAlpha(shell.transform, "ModuleRail"), 0.001f);
                Assert.AreEqual(1f, GraphicAlpha(shell.transform, "ContextPanel"), 0.001f);
                Assert.AreEqual(1f, GraphicAlpha(shell.transform, "SummaryBar"), 0.001f);
                Assert.IsNotNull(Find(shell.transform, "BrandMark"));
                Assert.IsNotNull(Find(shell.transform, "Icon"));
                Assert.IsNull(Find(shell.transform, "Motif"));
                Assert.IsNull(Find(shell.transform, "Soon"));
                AssertRailColumn(shell, 1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                Assert.IsNotNull(Find(shell.transform, "CommandHeader"));
                Assert.IsNotNull(Find(shell.transform, "ModuleRail"));
                Assert.IsNotNull(Find(shell.transform, "ContextPanel"));
                Assert.IsNotNull(Find(shell.transform, "SummaryBar"));
                Assert.IsNull(Find(shell.transform, "TopBar"));
                Assert.AreEqual(1, Count<EventSystem>(scene));
                Assert.IsNull(Find(shell.transform, "Collapse"));
                Assert.AreEqual(MapCommandShell.PanelState.Closed, shell.State);
                Assert.IsFalse(shell.CloseButton.gameObject.activeInHierarchy);
                Assert.AreEqual(0, shell.HomeButton.onClick.GetPersistentEventCount());
                var future = Find(shell.transform, "FutureModes");
                Assert.IsNotNull(future);
                Assert.AreEqual(MapContent.FutureModes.Length, future.GetComponentsInChildren<Button>(true).Length);
                var closedWidth = shell.Viewport.width;

                var graphics = shell.GetComponentsInChildren<Graphic>(true);
                for (var i = 0; i < graphics.Length; i++)
                {
                    if (!graphics[i].raycastTarget)
                        continue;

                    var name = graphics[i].gameObject.name;
                    Assert.That(
                        name,
                        Is.EqualTo("CommandHeader")
                            .Or.EqualTo("ModuleRail")
                            .Or.EqualTo("RailViewport")
                            .Or.EqualTo("ContextPanel")
                            .Or.EqualTo("SummaryBar")
                            .Or.EqualTo("Home")
                            .Or.EqualTo("Close")
                            .Or.EqualTo("CurrentMode")
                            .Or.EqualTo("Mode0")
                            .Or.EqualTo("Mode1")
                            .Or.EqualTo("Mode2")
                            .Or.EqualTo("Mode3")
                            .Or.EqualTo("Mode4")
                            .Or.EqualTo("Mode5")
                            .Or.EqualTo("Mode6")
                            .Or.EqualTo("Mode7"),
                        name);
                }

                Assert.IsFalse(Find(shell.transform, "LeftBleed").GetComponent<Image>().raycastTarget);
                Assert.IsFalse(Find(shell.transform, "RightBleed").GetComponent<Image>().raycastTarget);
                shell.ShowCity(null);
                Assert.IsFalse(shell.CoordinatesVisible);
                Assert.IsFalse(shell.ContextOpen);
                shell.ShowCity("izmir");
                Assert.AreEqual(MapCommandShell.PanelState.City, shell.State);
                Assert.IsTrue(shell.CloseButton.gameObject.activeInHierarchy);
                StringAssert.Contains("38", shell.CoordinateText);
                for (var i = 0; i < shell.SummaryText.Length; i++)
                    Assert.IsFalse(char.IsDigit(shell.SummaryText[i]));
                Assert.Less(shell.Viewport.width, closedWidth);
                shell.ShowModule(0);
                Assert.AreEqual(MapCommandShell.PanelState.Module, shell.State);
                Assert.IsFalse(shell.CoordinatesVisible);
                Assert.AreEqual(SurfaceRaised(shell, "Mode0"), true);
                Assert.IsFalse(SurfaceRaised(shell, "Mode1"));
                shell.ShowModule(3);
                Assert.IsFalse(SurfaceRaised(shell, "Mode0"));
                Assert.IsTrue(SurfaceRaised(shell, "Mode3"));
                shell.ShowMapContext();
                Assert.AreEqual(MapCommandShell.PanelState.Module, shell.State);
                Assert.IsTrue(SurfaceRaised(shell, "CurrentMode"));
                Assert.IsFalse(SurfaceRaised(shell, "Mode3"));
                shell.CloseContext();
                Assert.AreEqual(MapCommandShell.PanelState.Closed, shell.State);
                Assert.IsFalse(shell.CloseButton.gameObject.activeInHierarchy);
                Assert.AreEqual(closedWidth, shell.Viewport.width, 0.5f);
                Assert.IsNull(Find(shell.transform, "Collapse"));
                shell.ShowCity("trabzon");
                StringAssert.Contains("40", shell.CoordinateText);

                var homeRect = shell.HomeButton.GetComponent<RectTransform>();
                var homeLabel = homeRect.Find("Label").GetComponent<Text>();
                var homeMark = homeRect.Find("Mark").GetComponent<Text>();
                Assert.AreEqual(0f, homeLabel.color.a, 0.001f);
                Assert.LessOrEqual(homeLabel.rectTransform.rect.width, 1f);
                Assert.AreEqual("\u2190", homeMark.text);
                Assert.Greater(homeMark.color.a, 0.5f);
                Assert.IsFalse(homeMark.raycastTarget);

                var phone = new Rect(186f, 60f, 2496f, 1200f);
                shell.Apply(2868f, 1320f, phone);
                Assert.AreEqual(0f, shell.HeaderRect.xMin, 0.5f);
                Assert.AreEqual(2868f, shell.HeaderRect.xMax, 0.5f);
                Assert.AreEqual(0f, shell.SummaryRect.xMin, 0.5f);
                Assert.AreEqual(2868f, shell.SummaryRect.xMax, 0.5f);
                Assert.AreEqual(0f, shell.RailRect.xMin, 0.5f);
                Assert.AreEqual(1320f, shell.HeaderRect.yMax, 1f);
                Assert.AreEqual(0f, shell.SummaryRect.yMin, 1f);
                Assert.IsTrue(shell.ContextOpen);
                var phoneScale = MapHudLayout.CanvasScale(2868f, 1320f);
                var phoneHeader = Find(shell.transform, "CommandHeader") as RectTransform;
                var phoneSummary = Find(shell.transform, "SummaryBar") as RectTransform;
                var phoneRail = Find(shell.transform, "ModuleRail") as RectTransform;
                var phoneContext = Find(shell.transform, "ContextPanel") as RectTransform;
                Assert.AreEqual(0f, 186f + phoneHeader.anchoredPosition.x * phoneScale, 1f);
                Assert.AreEqual(2868f, 186f + (phoneHeader.anchoredPosition.x + phoneHeader.sizeDelta.x) * phoneScale, 1f);
                Assert.AreEqual(1320f, 60f + (phoneHeader.anchoredPosition.y + phoneHeader.sizeDelta.y) * phoneScale, 1f);
                Assert.AreEqual(0f, 60f + phoneSummary.anchoredPosition.y * phoneScale, 1f);
                Assert.AreEqual(0f, 186f + phoneSummary.anchoredPosition.x * phoneScale, 1f);
                Assert.AreEqual(2868f, 186f + (phoneSummary.anchoredPosition.x + phoneSummary.sizeDelta.x) * phoneScale, 1f);
                Assert.AreEqual(0f, 186f + phoneRail.anchoredPosition.x * phoneScale, 1f);
                AssertRailColumn(shell, 2868f, 1320f, phone);
                Assert.IsTrue(phoneRail.GetComponent<ScrollRect>().vertical);
                var phoneIcon = Find(shell.transform, "CurrentMode").Find("Icon") as RectTransform;
                var phoneLabel = Find(shell.transform, "CurrentMode").Find("Label").GetComponent<Text>();
                Assert.Greater(phoneIcon.sizeDelta.x * phoneScale, 36f);
                Assert.Greater(phoneLabel.fontSize * phoneScale, 15f);
                var safeSlice = shell.RailRect.width - phone.xMin;
                Assert.Less(safeSlice, MapCommandLayout.TextWidth(MapCommandLayout.LongestMode, MapCommandLayout.Evaluate(2868f, 1320f, phone, true).FontPixels));
                Assert.LessOrEqual(safeSlice, MapCommandLayout.RailContentPixels(MapCommandLayout.Evaluate(2868f, 1320f, phone, true).FontPixels) + 0.6f);
                Assert.AreEqual(2868f, 186f + (phoneContext.anchoredPosition.x + phoneContext.sizeDelta.x) * phoneScale, 1f);
                var brand = Find(shell.transform, "Brand") as RectTransform;
                Assert.GreaterOrEqual(phoneHeader.anchoredPosition.x * phoneScale + 186f + brand.anchoredPosition.x * phoneScale, phone.xMin - 0.5f);
                var phoneHome = shell.HomeButton.GetComponent<RectTransform>();
                var homeLeft = 186f + (phoneHeader.anchoredPosition.x + phoneHome.anchoredPosition.x) * phoneScale;
                var homeRight = homeLeft + phoneHome.sizeDelta.x * phoneScale;
                var homeBottom = 60f + (phoneHeader.anchoredPosition.y + phoneHome.anchoredPosition.y) * phoneScale;
                var homeTop = homeBottom + phoneHome.sizeDelta.y * phoneScale;
                Assert.GreaterOrEqual(homeLeft, phone.xMin - 0.5f);
                Assert.LessOrEqual(homeRight, phone.xMax + 0.5f);
                Assert.GreaterOrEqual(homeBottom, phone.yMin - 0.5f);
                Assert.LessOrEqual(homeTop, phone.yMax + 0.5f);
                var close = shell.CloseButton.GetComponent<RectTransform>();
                var closeRight = 186f + (phoneContext.anchoredPosition.x + phoneContext.sizeDelta.x + close.anchoredPosition.x) * phoneScale;
                Assert.LessOrEqual(closeRight, phone.xMax + 0.5f);
                Assert.GreaterOrEqual(close.sizeDelta.x * phoneScale, 40f);

                shell.ShowCity(null);
                Assert.IsFalse(shell.CoordinatesVisible);
                shell.ShowCity("izmir");
                StringAssert.Contains("38", shell.CoordinateText);
                for (var i = 0; i < shell.SummaryText.Length; i++)
                    Assert.IsFalse(char.IsDigit(shell.SummaryText[i]));
                shell.ShowCity("trabzon");
                StringAssert.Contains("40", shell.CoordinateText);
                var compact = new Rect(32f, 16f, 868f, 398f);
                shell.Apply(932f, 430f, compact);
                Assert.IsFalse(shell.FutureModesVisible);
                var rail = Find(shell.transform, "ModuleRail") as RectTransform;
                var scale = MapHudLayout.CanvasScale(932f, 430f);
                Assert.Less(rail.sizeDelta.y * scale, shell.RailRect.height * 0.5f);
                var leftBleed = Find(shell.transform, "LeftBleed") as RectTransform;
                var rightBleed = Find(shell.transform, "RightBleed") as RectTransform;
                Assert.IsFalse(leftBleed.GetComponent<UnityEngine.UI.Image>().raycastTarget);
                Assert.IsFalse(rightBleed.GetComponent<UnityEngine.UI.Image>().raycastTarget);
                Assert.AreEqual(0f, 32f + leftBleed.anchoredPosition.x * scale, 1f);
                Assert.AreEqual(32f, leftBleed.sizeDelta.x * scale, 1f);
                Assert.AreEqual(932f, 32f + (rightBleed.anchoredPosition.x + rightBleed.sizeDelta.x) * scale, 1f);
                Assert.Greater(leftBleed.sizeDelta.y * scale, rail.sizeDelta.y * scale);
                AssertRailColumn(shell, 932f, 430f, compact);
                Assert.AreEqual("izmir", MapCities.All[0].Id);
                Assert.AreEqual(38.438096d, MapCities.All[0].Latitude, 0.0000001d);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
            Assert.AreEqual(0, Object.FindObjectsByType<MapCommandShell>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        }

        static void AssertRailColumn(MapCommandShell shell, float width, float height, Rect safe)
        {
            var scale = MapHudLayout.CanvasScale(width, height);
            var metrics = MapCommandLayout.Evaluate(width, height, safe, true);
            var touch = MapCommandLayout.RailTouchPixels(metrics.FontPixels);
            var iconPixels = MapCommandLayout.RailIconPixels(metrics.FontPixels) / scale;
            var labelPixels = MapCommandLayout.RailLabelPixels(metrics.FontPixels) / scale;
            var rail = Find(shell.transform, "ModuleRail") as RectTransform;
            var scroll = rail.GetComponent<ScrollRect>();
            var viewport = Find(rail, "RailViewport") as RectTransform;
            var content = Find(rail, "RailContent") as RectTransform;
            Assert.IsNotNull(scroll);
            Assert.IsNotNull(viewport.GetComponent<RectMask2D>());
            Assert.IsFalse(rail.GetComponent<Image>().raycastTarget);
            Assert.IsTrue(viewport.GetComponent<Image>().raycastTarget);
            Assert.IsFalse(scroll.horizontal);
            Assert.AreEqual(content.sizeDelta.y > viewport.sizeDelta.y + 1f, scroll.vertical);
            var names = new[] { "CurrentMode", "Mode0", "Mode1", "Mode2", "Mode3", "Mode4", "Mode5", "Mode6", "Mode7" };
            var icons = new Sprite[names.Length];
            RectTransform previous = null;
            for (var i = 0; i < names.Length; i++)
            {
                var row = Find(shell.transform, names[i]) as RectTransform;
                Assert.IsNotNull(row, names[i]);
                var icon = row.Find("Icon") as RectTransform;
                var label = row.Find("Label") as RectTransform;
                Assert.IsNotNull(icon);
                Assert.IsNotNull(label);
                var iconImage = icon.GetComponent<Image>();
                Assert.IsFalse(iconImage.raycastTarget);
                Assert.AreEqual(iconPixels, icon.sizeDelta.x, 0.75f, names[i]);
                Assert.AreEqual(icon.sizeDelta.x, icon.sizeDelta.y, 0.05f, names[i]);
                Assert.AreEqual(MapRailIcons.Get(i), iconImage.sprite);
                icons[i] = iconImage.sprite;
                var text = label.GetComponent<Text>();
                Assert.AreEqual(TextAnchor.MiddleCenter, text.alignment);
                Assert.AreEqual(VerticalWrapMode.Overflow, text.verticalOverflow);
                Assert.GreaterOrEqual(text.fontSize, labelPixels - 1.1f, names[i]);
                Assert.Greater(icon.position.y, label.position.y, names[i]);
                if (!row.gameObject.activeInHierarchy)
                    continue;

                Assert.GreaterOrEqual(row.sizeDelta.x * scale, touch - 1f, names[i]);
                Assert.GreaterOrEqual(row.sizeDelta.y * scale, touch - 1f, names[i]);
                var edge = rail.InverseTransformPoint(row.TransformPoint(new Vector3(row.rect.xMin, 0f, 0f)));
                var left = safe.xMin + (rail.anchoredPosition.x + edge.x) * scale;
                Assert.GreaterOrEqual(left, safe.xMin - 0.5f, names[i]);
                Assert.LessOrEqual(left, safe.xMin + 8f, names[i]);
                var right = left + row.sizeDelta.x * scale;
                Assert.LessOrEqual(right, safe.xMax + 0.5f, names[i]);
                if (previous != null)
                {
                    var previousBottom = rail.InverseTransformPoint(previous.TransformPoint(new Vector3(0f, previous.rect.yMin, 0f))).y;
                    var nextTop = rail.InverseTransformPoint(row.TransformPoint(new Vector3(0f, row.rect.yMax, 0f))).y;
                    Assert.LessOrEqual(nextTop, previousBottom + 0.4f, names[i]);
                }

                previous = row;
            }

            for (var i = 0; i < icons.Length; i++)
            {
                for (var j = i + 1; j < icons.Length; j++)
                    Assert.AreNotSame(icons[i], icons[j]);
            }

            Assert.AreEqual(0, shell.HomeButton.onClick.GetPersistentEventCount());
        }

        static bool SurfaceRaised(MapCommandShell shell, string name)
        {
            var image = Find(shell.transform, name).GetComponent<Image>();
            return image != null && image.color.a > 0.9f;
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

        static float GraphicAlpha(Transform root, string name)
        {
            var found = Find(root, name);
            Assert.IsNotNull(found, name);
            var image = found.GetComponent<Image>();
            Assert.IsNotNull(image, name);
            return image.color.a;
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

        static int Count<T>(Scene scene) where T : Component
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
