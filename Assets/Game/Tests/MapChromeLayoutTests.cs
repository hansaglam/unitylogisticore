using System.IO;
using System.Security.Cryptography;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.Tests
{
    public sealed class MapChromeLayoutTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        static readonly Vector2[] Screens =
        {
            new Vector2(844f, 390f),
            new Vector2(932f, 430f),
            new Vector2(960f, 540f),
            new Vector2(1280f, 720f),
            new Vector2(1920f, 1080f),
            new Vector2(2868f, 1320f)
        };

        static readonly Rect[] Frames =
        {
            new Rect(0f, 0f, 844f, 390f),
            new Rect(32f, 16f, 868f, 398f),
            new Rect(0f, 0f, 960f, 540f),
            new Rect(0f, 0f, 1280f, 720f),
            new Rect(0f, 0f, 1920f, 1080f),
            new Rect(186f, 60f, 2496f, 1200f)
        };

        static readonly string[] Locales = { "tr", "en", "de" };

        [Test]
        public void PhoneRail_ShowsAllFutureModesWithScrollAndNoFooterOverlap()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var beforeSettings = Sha(ProjectSettingsPath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                var phone = new Rect(186f, 60f, 2496f, 1200f);
                shell.Apply(2868f, 1320f, phone);
                Canvas.ForceUpdateCanvases();

                var metrics = MapCommandLayout.Evaluate(2868f, 1320f, phone, true);
                Assert.IsTrue(metrics.FutureModes);
                Assert.AreEqual(8, MapContent.FutureModes.Length);

                var scale = MapHudLayout.CanvasScale(2868f, 1320f);
                var rail = Find(shell.transform, "ModuleRail") as RectTransform;
                var summary = Find(shell.transform, "SummaryBar") as RectTransform;
                var scroll = rail.GetComponent<ScrollRect>();
                var viewport = Find(rail, "RailViewport") as RectTransform;
                var content = Find(rail, "RailContent") as RectTransform;
                Assert.IsTrue(scroll.vertical);
                Assert.IsFalse(scroll.horizontal);
                Assert.Greater(content.sizeDelta.y, viewport.sizeDelta.y + 1f);

                var touch = MapCommandLayout.TouchPixels(2868f, 1320f);
                RectTransform previous = null;
                for (var i = 0; i < 8; i++)
                {
                    var row = Find(shell.transform, "Mode" + i) as RectTransform;
                    Assert.IsTrue(row.gameObject.activeInHierarchy, "Mode" + i);
                    Assert.GreaterOrEqual(row.sizeDelta.y * scale, touch - 1f, "Mode" + i);
                    Assert.GreaterOrEqual(row.sizeDelta.x * scale, touch - 1f, "Mode" + i);

                    var icon = row.Find("Icon") as RectTransform;
                    var label = row.Find("Label") as RectTransform;
                    Assert.Greater(icon.position.y, label.position.y, "Mode" + i);
                    var gap = icon.position.y - label.position.y;
                    Assert.Less(gap, row.sizeDelta.y * scale * 0.72f, "Mode" + i);
                    Assert.Greater(gap, icon.sizeDelta.y * scale * 0.2f, "Mode" + i);

                    if (i == 0)
                    {
                        var bottom = ScreenY(rail, row, row.rect.yMin, phone, scale);
                        var top = ScreenY(rail, row, row.rect.yMax, phone, scale);
                        Assert.GreaterOrEqual(bottom, phone.yMin - 0.5f, "Mode0");
                        Assert.LessOrEqual(top, phone.yMax + 0.5f, "Mode0");
                        var summaryTop = 60f + (summary.anchoredPosition.y + summary.sizeDelta.y) * scale;
                        Assert.GreaterOrEqual(bottom, summaryTop - 0.5f, "Mode0 footer");
                        AssertInsideViewport(viewport, row, new Vector2(0.5f, 0.5f), "Mode0 center");
                    }

                    if (previous != null)
                    {
                        var previousBottom = rail.InverseTransformPoint(previous.TransformPoint(new Vector3(0f, previous.rect.yMin, 0f))).y;
                        var nextTop = rail.InverseTransformPoint(row.TransformPoint(new Vector3(0f, row.rect.yMax, 0f))).y;
                        Assert.LessOrEqual(nextTop, previousBottom + 0.4f, "Mode" + i);
                    }

                    previous = row;
                }

                Assert.IsTrue(shell.SelectRegion(0));
                AssertBrandChip(shell, 2868f, 1320f, phone, null);
                Assert.IsTrue(shell.SelectRegion(1));
                AssertBrandChip(shell, 2868f, 1320f, phone, null);
                Assert.IsTrue(shell.SelectRegion(0));
                AssertBrandChip(shell, 2868f, 1320f, phone, null);

                shell.ShowModule(0);
                Assert.IsTrue(shell.ManagementOpen);
                Assert.IsFalse(shell.ContextOpen);
                AssertBrandChip(shell, 2868f, 1320f, phone, null);
                Assert.IsTrue(rail.GetComponent<ScrollRect>().vertical);
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
        public void BrandAndChip_StayClearAcrossRegionsLocalesAndFixtures()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                var labels = new[]
                {
                    new[] { "Dünya", "Türkiye", "Avrupa", "Asya", "Afrika", "Kuzey Amerika", "Güney Amerika", "Okyanusya" },
                    new[] { "World", "Turkey", "Europe", "Asia", "Africa", "North America", "South America", "Oceania" },
                    new[] { "Welt", "Türkei", "Europa", "Asien", "Afrika", "Nordamerika", "Südamerika", "Ozeanien" }
                };

                for (var frame = 0; frame < Screens.Length; frame++)
                {
                    var screen = Screens[frame];
                    var safe = Frames[frame];
                    shell.Apply(screen.x, screen.y, safe);
                    Canvas.ForceUpdateCanvases();
                    var metrics = MapCommandLayout.Evaluate(screen.x, screen.y, safe, true);
                    for (var locale = 0; locale < labels.Length; locale++)
                    {
                        for (var region = 0; region < 8; region++)
                        {
                            Assert.IsTrue(shell.SelectRegion(region), Locales[locale] + " " + region);
                            shell.PreviewRegionLabel(labels[locale][region]);
                            Canvas.ForceUpdateCanvases();
                            AssertBrandChip(shell, screen.x, screen.y, safe, labels[locale][region]);
                        }
                    }

                    if (!metrics.FutureModes)
                        continue;

                    var rail = Find(shell.transform, "ModuleRail") as RectTransform;
                    var scroll = rail.GetComponent<ScrollRect>();
                    var content = Find(rail, "RailContent") as RectTransform;
                    var viewport = Find(rail, "RailViewport") as RectTransform;
                    var fits = content.sizeDelta.y <= viewport.sizeDelta.y + 1f;
                    Assert.AreEqual(fits, !scroll.vertical, screen.ToString());
                    if (fits)
                    {
                        for (var mode = 0; mode < 8; mode++)
                            Assert.IsTrue(Find(shell.transform, "Mode" + mode).gameObject.activeInHierarchy);
                    }
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        static void AssertBrandChip(MapCommandShell shell, float width, float height, Rect safe, string expectedChip)
        {
            Canvas.ForceUpdateCanvases();
            var scale = MapHudLayout.CanvasScale(width, height);
            var header = Find(shell.transform, "CommandHeader") as RectTransform;
            var brand = Find(shell.transform, "Brand") as RectTransform;
            var core = Find(shell.transform, "BrandCore") as RectTransform;
            var chip = Find(shell.transform, "RegionChip") as RectTransform;
            var brandText = brand.GetComponent<Text>();
            var coreText = core.GetComponent<Text>();
            Assert.AreEqual("Logisti", brandText.text);
            Assert.AreEqual("Core", coreText.text);
            Assert.AreEqual(brandText.fontSize, coreText.fontSize);
            Assert.AreEqual(brand.anchoredPosition.y, core.anchoredPosition.y, 0.05f);
            Assert.AreEqual(brand.sizeDelta.y, core.sizeDelta.y, 0.05f);

            var brandLeft = safe.xMin + (header.anchoredPosition.x + brand.anchoredPosition.x) * scale;
            var coreLeft = safe.xMin + (header.anchoredPosition.x + core.anchoredPosition.x) * scale;
            var coreRight = coreLeft + core.sizeDelta.x * scale;
            Assert.AreEqual(brandLeft + brand.sizeDelta.x * scale, coreLeft, 0.75f);
            var gap = MapCommandLayout.BrandChipGapCanvas(brandText.fontSize);
            Assert.GreaterOrEqual(
                chip.anchoredPosition.x - (core.anchoredPosition.x + core.sizeDelta.x),
                gap - 0.75f,
                width + "x" + height);
            var chipLeft = safe.xMin + (header.anchoredPosition.x + chip.anchoredPosition.x) * scale;
            var chipRight = chipLeft + chip.sizeDelta.x * scale;
            Assert.LessOrEqual(chipRight, safe.xMax + 0.5f);
            Assert.GreaterOrEqual(chipLeft, safe.xMin - 0.5f);
            if (!string.IsNullOrEmpty(expectedChip))
                Assert.AreEqual(expectedChip, Find(chip, "Name").GetComponent<Text>().text);
        }

        static void AssertInsideViewport(RectTransform viewport, RectTransform row, Vector2 normalized, string label)
        {
            var local = new Vector3(
                Mathf.Lerp(row.rect.xMin, row.rect.xMax, normalized.x),
                Mathf.Lerp(row.rect.yMin, row.rect.yMax, normalized.y),
                0f);
            var inViewport = viewport.InverseTransformPoint(row.TransformPoint(local));
            Assert.IsTrue(viewport.rect.Contains(new Vector2(inViewport.x, inViewport.y)), label);
        }

        static float ScreenY(RectTransform rail, RectTransform row, float localY, Rect safe, float scale)
        {
            var point = rail.InverseTransformPoint(row.TransformPoint(new Vector3(0f, localY, 0f)));
            return safe.yMin + (rail.anchoredPosition.y + point.y) * scale;
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
                    text[i * 2] = Hex(value >> 4);
                    text[i * 2 + 1] = Hex(value & 15);
                }

                return new string(text);
            }
        }

        static char Hex(int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + (value - 10));
        }
    }
}
