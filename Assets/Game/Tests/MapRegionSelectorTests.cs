using System.IO;
using System.Security.Cryptography;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.Tests
{
    public sealed class MapRegionSelectorTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

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

        [Test]
        public void RegionChip_TogglesMenuAndKeepsFutureRegionsOffTheCamera()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                Assert.AreSame(shell, MapCommandShell.Attach(scene));
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                Canvas.ForceUpdateCanvases();

                Assert.AreEqual(1, CountNamed(shell.transform, "RegionChip"));
                Assert.AreEqual(1, CountNamed(shell.transform, "RegionMenu"));
                Assert.IsNull(Find(shell.transform, "BrandMark"));
                Assert.IsNull(shell.HomeButton);
                Assert.IsNull(Find(shell.transform, "Home"));
                Assert.AreEqual("Logisti", Find(shell.transform, "Brand").GetComponent<Text>().text);
                Assert.AreEqual("Core", Find(shell.transform, "BrandCore").GetComponent<Text>().text);
                Assert.AreEqual(MapHudPalette.Text, Find(shell.transform, "Brand").GetComponent<Text>().color);
                Assert.AreEqual(MapHudPalette.Accent, Find(shell.transform, "BrandCore").GetComponent<Text>().color);
                Assert.IsNotNull(Find(shell.transform, "RegionChip").Find("Glyph").GetComponent<Image>().sprite);
                Assert.IsNotNull(Find(shell.transform, "RegionChip").Find("Chevron").GetComponent<Image>().sprite);
                Assert.IsFalse(Find(shell.transform, "RegionChip").Find("Glyph").GetComponent<Image>().raycastTarget);
                Assert.AreEqual(1, Count<EventSystem>(scene));

                var menu = Find(shell.transform, "RegionMenu").gameObject;
                Assert.IsFalse(menu.activeSelf);
                var menuGroup = menu.GetComponent<CanvasGroup>();
                Assert.IsNotNull(menuGroup);
                Assert.IsFalse(menuGroup.blocksRaycasts);
                shell.ToggleRegionMenu();
                Assert.IsTrue(shell.RegionMenuOpen);
                Assert.IsTrue(menu.activeSelf);
                Assert.IsTrue(menuGroup.blocksRaycasts);
                shell.ToggleRegionMenu();
                Assert.IsFalse(menu.activeSelf);
                Assert.IsFalse(menu.activeInHierarchy);
                Assert.IsFalse(menuGroup.blocksRaycasts);

                var rig = Rig(scene);
                Assert.IsNotNull(rig);
                shell.ToggleRegionMenu();
                for (var i = 0; i < 8; i++)
                {
                    Assert.IsTrue(Find(shell.transform, "Region" + i).GetComponent<Button>().interactable, "region " + i);
                    Assert.IsFalse(Find(shell.transform, "Region" + i).Find("Soon").gameObject.activeSelf, "region " + i);
                }

                Assert.IsTrue(shell.SelectRegion(2));
                Assert.AreEqual(MapScope.Europe, rig.Scope);
                Assert.AreEqual(2, shell.RegionIndex);
                Assert.IsFalse(shell.RegionMenuOpen);
                var europeCenter = rig.TargetCenter;
                var europeSize = rig.TargetOrthographicSize;
                rig.PanScreenDelta(new Vector2(40f, -20f));
                Assert.IsTrue(rig.UserAdjusted);
                Assert.AreEqual(MapScope.Europe, rig.Scope);
                Assert.AreEqual(2, shell.RegionIndex);
                Assert.AreNotEqual(europeCenter, rig.TargetCenter);
                rig.ZoomAtScreen(new Vector2(960f, 540f), 1.2f);
                Assert.IsTrue(shell.SelectRegion(2));
                Assert.AreEqual(europeCenter, rig.TargetCenter);
                Assert.AreEqual(europeSize, rig.TargetOrthographicSize, 0.001f);
                Assert.IsFalse(rig.UserAdjusted);
                Assert.IsTrue(shell.SelectRegion(0));
                Assert.AreEqual(MapScope.World, rig.Scope);
                Assert.AreEqual(0, shell.RegionIndex);
                Assert.IsFalse(shell.RegionMenuOpen);
                Assert.IsTrue(shell.SelectRegion(1));
                Assert.AreEqual(MapScope.Turkey, rig.Scope);
                Assert.AreEqual(1, shell.RegionIndex);
                Assert.IsFalse(menu.activeSelf);
                var aspect = rig.GetComponent<Camera>().aspect;
                if (aspect < 0.05f)
                    aspect = 16f / 9f;
                var ceiling = MapFrames.MaximumOrthographicSize(aspect);
                for (var i = 0; i < 8; i++)
                {
                    Assert.IsTrue(shell.SelectRegion(i), "focus " + i);
                    Assert.AreEqual((MapScope)i, rig.Scope);
                    Assert.AreEqual(i, shell.RegionIndex);
                    Assert.LessOrEqual(rig.TargetOrthographicSize, ceiling + 0.02f, "size " + i);
                }

                shell.ShowCity("ankara");
                Assert.AreEqual(MapCommandShell.PanelState.City, shell.State);
                StringAssert.Contains("39", shell.CoordinateText);
                Assert.IsTrue(shell.SelectRegion(6));
                Assert.AreEqual(MapScope.SouthAmerica, rig.Scope);
                Assert.AreEqual(MapCommandShell.PanelState.Closed, shell.State);
                Assert.IsFalse(shell.RegionMenuOpen);
                shell.ShowCity("ankara");
                Assert.AreEqual(MapCommandShell.PanelState.City, shell.State);
                var held = rig.TargetCenter;
                rig.PanScreenDelta(new Vector2(30f, 10f));
                Assert.IsTrue(rig.UserAdjusted);
                Assert.AreNotEqual(held, rig.TargetCenter);
                shell.CloseContext();
                Assert.AreEqual(MapCommandShell.PanelState.Closed, shell.State);
                Assert.AreEqual(MapScope.SouthAmerica, rig.Scope);
                Assert.IsTrue(rig.UserAdjusted);

                for (var i = 0; i < Screens.Length; i++)
                    AssertInside(shell, Screens[i], Frames[i]);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        static void AssertInside(MapCommandShell shell, Vector2 screen, Rect safe)
        {
            shell.Apply(screen.x, screen.y, safe);
            Canvas.ForceUpdateCanvases();
            var scale = MapHudLayout.CanvasScale(screen.x, screen.y);
            var header = Find(shell.transform, "CommandHeader") as RectTransform;
            var brand = Find(shell.transform, "Brand") as RectTransform;
            var brandCore = Find(shell.transform, "BrandCore") as RectTransform;
            var chip = Find(shell.transform, "RegionChip") as RectTransform;
            var brandLeft = safe.xMin + (header.anchoredPosition.x + brand.anchoredPosition.x) * scale;
            Assert.GreaterOrEqual(brandLeft, safe.xMin - 0.5f, screen.ToString());
            var coreLeft = safe.xMin + (header.anchoredPosition.x + brandCore.anchoredPosition.x) * scale;
            var coreRight = coreLeft + brandCore.sizeDelta.x * scale;
            Assert.AreEqual(brand.anchoredPosition.y, brandCore.anchoredPosition.y, 0.05f, screen.ToString());
            Assert.AreEqual(brand.sizeDelta.y, brandCore.sizeDelta.y, 0.05f, screen.ToString());
            Assert.GreaterOrEqual(coreLeft, brandLeft + brand.sizeDelta.x * scale - 0.5f, screen.ToString());
            var chipLeft = safe.xMin + (header.anchoredPosition.x + chip.anchoredPosition.x) * scale;
            var chipWidth = chip.sizeDelta.x * scale;
            var chipRight = chipLeft + chipWidth;
            var chipHeight = header.rect.height + chip.sizeDelta.y;
            if (chip.rect.height > 1f)
                chipHeight = chip.rect.height;
            var gap = MapCommandLayout.BrandChipGapCanvas(brand.GetComponent<Text>().fontSize);
            Assert.GreaterOrEqual(
                chip.anchoredPosition.x - (brandCore.anchoredPosition.x + brandCore.sizeDelta.x),
                gap - 0.75f,
                screen.ToString());
            Assert.Greater(chipLeft, coreRight + 0.5f, screen.ToString());
            Assert.GreaterOrEqual(chipLeft, safe.xMin - 0.5f, screen.ToString());
            Assert.LessOrEqual(chipRight, safe.xMax + 0.5f, screen.ToString());
            Assert.GreaterOrEqual(chipHeight * scale, 40f, screen.ToString());
            var required = MapCommandLayout.TouchPixels(screen.x, screen.y);
            Assert.GreaterOrEqual(chip.sizeDelta.y * scale, required - 1f, screen.ToString());
            Assert.GreaterOrEqual(chipWidth, required - 1f, screen.ToString());
            var chipBottom = safe.yMin + (header.anchoredPosition.y + chip.anchoredPosition.y) * scale;
            var chipTop = chipBottom + chip.sizeDelta.y * scale;
            Assert.GreaterOrEqual(chipBottom, safe.yMin - 0.5f, screen.ToString());
            Assert.LessOrEqual(chipTop, safe.yMax + 0.5f, screen.ToString());
            if (screen.x >= 1280f)
                Assert.LessOrEqual(chipRight, safe.xMax - 80f, screen.ToString());

            shell.ToggleRegionMenu();
            var menu = Find(shell.transform, "RegionMenu") as RectTransform;
            var menuLeft = safe.xMin + menu.anchoredPosition.x * scale;
            var menuBottom = safe.yMin + menu.anchoredPosition.y * scale;
            var menuRight = menuLeft + menu.sizeDelta.x * scale;
            var menuTop = menuBottom + menu.sizeDelta.y * scale;
            Assert.GreaterOrEqual(menuLeft, safe.xMin - 0.5f, screen.ToString());
            Assert.GreaterOrEqual(menuBottom, safe.yMin - 0.5f, screen.ToString());
            Assert.LessOrEqual(menuRight, safe.xMax + 0.5f, screen.ToString());
            Assert.LessOrEqual(menuTop, safe.yMax + 0.5f, screen.ToString());
            var option = Find(shell.transform, "Region0") as RectTransform;
            Assert.GreaterOrEqual(option.sizeDelta.y * scale, required - 1.5f, screen.ToString());
            var rowLabel = option.Find("Name").GetComponent<Text>();
            Assert.AreEqual(MapCommandLayout.MenuCanvasSize(screen.x, screen.y, scale), rowLabel.fontSize, screen.ToString());
            var menuImage = menu.GetComponent<Image>();
            Assert.GreaterOrEqual(menuImage.color.a, 0.98f, screen.ToString());
            Assert.LessOrEqual(menu.sizeDelta.x * scale, Mathf.Max(chipWidth + 8f, MapCommandLayout.MenuTypePixels(screen.x, screen.y) * 16f), screen.ToString());
            var active = option.Find("Active");
            Assert.AreEqual(shell.RegionIndex == 0, active.gameObject.activeSelf, screen.ToString());
            var scroll = menu.GetComponent<ScrollRect>();
            var bar = scroll.verticalScrollbar;
            Assert.IsNotNull(bar, screen.ToString());
            Assert.IsFalse(bar.GetComponent<Image>().raycastTarget, screen.ToString());
            if (screen.y <= 430f)
            {
                Assert.IsTrue(scroll.vertical, screen.ToString());
                Assert.IsTrue(bar.gameObject.activeSelf, screen.ToString());
                Canvas.ForceUpdateCanvases();
                Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height + 8f, screen.ToString());
                var last = Find(shell.transform, "Region7") as RectTransform;
                var rowBottom = -last.anchoredPosition.y + last.rect.height;
                Assert.LessOrEqual(rowBottom, scroll.content.rect.height + 1f, screen.ToString());
                Assert.Greater(rowBottom, scroll.viewport.rect.height + 4f, screen.ToString());
            }
            else if (MapCommandLayout.PointScale(screen.x, screen.y) < 2f && screen.y >= 720f)
            {
                Assert.IsFalse(scroll.vertical, screen.ToString());
                Assert.IsFalse(bar.gameObject.activeSelf, screen.ToString());
            }

            shell.ToggleRegionMenu();
            Assert.IsFalse(menu.gameObject.activeSelf, screen.ToString());
        }

        static MapCameraRig Rig(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var rig = roots[i].GetComponentInChildren<MapCameraRig>(true);
                if (rig != null)
                    return rig;
            }

            return null;
        }

        [Test]
        public void RegionMenu_ScrollsTheLastRowFullyIntoTheViewport()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                var rig = Rig(scene);
                var frames = new[]
                {
                    new Vector2(844f, 390f),
                    new Vector2(932f, 430f),
                    new Vector2(932f, 430f),
                    new Vector2(960f, 540f),
                    new Vector2(1280f, 720f),
                    new Vector2(1920f, 1080f),
                    new Vector2(2868f, 1320f)
                };
                var safes = new[]
                {
                    new Rect(0f, 0f, 844f, 390f),
                    new Rect(0f, 0f, 932f, 430f),
                    new Rect(32f, 16f, 868f, 398f),
                    new Rect(0f, 0f, 960f, 540f),
                    new Rect(0f, 0f, 1280f, 720f),
                    new Rect(0f, 0f, 1920f, 1080f),
                    new Rect(186f, 60f, 2496f, 1200f)
                };
                var problems = new System.Text.StringBuilder();
                for (var i = 0; i < frames.Length; i++)
                {
                    shell.Apply(frames[i].x, frames[i].y, safes[i]);
                    if (!shell.RegionMenuOpen)
                        shell.ToggleRegionMenu();
                    Canvas.ForceUpdateCanvases();
                    var menu = Find(shell.transform, "RegionMenu") as RectTransform;
                    var scroll = menu.GetComponent<ScrollRect>();
                    var last = Find(shell.transform, "Region7") as RectTransform;
                    var name = last.Find("Name").GetComponent<Text>();
                    var scale = MapHudLayout.CanvasScale(frames[i].x, frames[i].y);
                    var touch = MapCommandLayout.TouchPixels(frames[i].x, frames[i].y);
                    var rowPixels = last.rect.height * scale;
                    if (rowPixels < touch - 1.5f)
                        problems.AppendLine(frames[i] + " row " + rowPixels.ToString("0.0") + " < touch " + touch.ToString("0.0"));
                    if (name.font == null)
                        problems.AppendLine(frames[i] + " font missing");
                    var content = scroll.content.rect.height;
                    var viewport = scroll.viewport.rect.height;
                    var overflow = content - viewport;
                    var scrolling = scroll.vertical && scroll.verticalScrollbar.gameObject.activeSelf;
                    var topInside = Contains(scroll.viewport, last);
                    var topText = Contains(scroll.viewport, name.rectTransform);
                    var bottomInside = topInside;
                    var bottomText = topText;
                    var topValue = scroll.verticalScrollbar.value;
                    var bottomValue = topValue;
                    var topPos = scroll.content.anchoredPosition.y;
                    var bottomPos = topPos;
                    if (scrolling)
                    {
                        scroll.verticalNormalizedPosition = 1f;
                        Canvas.ForceUpdateCanvases();
                        DriveScrollbar(scroll);
                        topInside = Contains(scroll.viewport, last);
                        topText = Contains(scroll.viewport, name.rectTransform);
                        topValue = scroll.verticalScrollbar.value;
                        topPos = scroll.content.anchoredPosition.y;
                        scroll.verticalNormalizedPosition = 0f;
                        Canvas.ForceUpdateCanvases();
                        scroll.verticalNormalizedPosition = 0f;
                        Canvas.ForceUpdateCanvases();
                        DriveScrollbar(scroll);
                        bottomInside = Contains(scroll.viewport, last);
                        bottomText = Contains(scroll.viewport, name.rectTransform);
                        bottomValue = scroll.verticalScrollbar.value;
                        bottomPos = scroll.content.anchoredPosition.y;
                        if (!bottomInside)
                            problems.AppendLine(frames[i] + " last row stays outside after scrolling to the bottom");
                        if (!bottomText)
                            problems.AppendLine(frames[i] + " last text stays outside after scrolling to the bottom");
                        if (Mathf.Abs(bottomPos - topPos) <= 1f)
                            problems.AppendLine(frames[i] + " scroll position did not move");
                        if (Mathf.Abs(bottomValue - topValue) <= 0.2f)
                            problems.AppendLine(frames[i] + " scrollbar thumb did not move");
                    }
                    else
                    {
                        var glyphPad = (last.rect.height - name.fontSize) * 0.5f;
                        if (overflow > glyphPad + 0.5f)
                            problems.AppendLine(frames[i] + " last glyphs are clipped and the menu does not scroll");
                    }

                    if (frames[i].y >= 720f && MapCommandLayout.PointScale(frames[i].x, frames[i].y) < 2f && scrolling)
                        problems.AppendLine(frames[i] + " desktop menu scrolls");
                    if (frames[i].x > 2800f && topInside)
                        problems.AppendLine("2868 shows the whole last row before scrolling");

                    TestContext.WriteLine(
                        "menu " + frames[i].x.ToString("0") + "x" + frames[i].y.ToString("0")
                        + " safe=" + safes[i].x.ToString("0") + "," + safes[i].y.ToString("0") + "," + safes[i].width.ToString("0") + "," + safes[i].height.ToString("0")
                        + " content=" + content.ToString("0.0")
                        + " viewport=" + viewport.ToString("0.0")
                        + " overflow=" + overflow.ToString("0.0")
                        + " row=" + rowPixels.ToString("0.0")
                        + " scrolling=" + scrolling
                        + " topRow=" + topInside
                        + " topText=" + topText
                        + " bottomRow=" + bottomInside
                        + " bottomText=" + bottomText
                        + " bar=" + topValue.ToString("0.00") + "->" + bottomValue.ToString("0.00")
                        + " pos=" + topPos.ToString("0.0") + "->" + bottomPos.ToString("0.0"));
                }

                Assert.AreEqual(string.Empty, problems.ToString());

                var rowObject = Find(shell.transform, "Region7").gameObject;
                IPointerClickHandler tap = null;
                var handlers = rowObject.GetComponents<IPointerClickHandler>();
                for (var i = 0; i < handlers.Length; i++)
                {
                    if (handlers[i] is Button)
                        continue;
                    tap = handlers[i];
                }

                Assert.IsNotNull(tap);
                var pointer = new PointerEventData(EventSystem.current);
                pointer.button = PointerEventData.InputButton.Left;
                pointer.dragging = true;
                var heldRegion = shell.RegionIndex;
                tap.OnPointerClick(pointer);
                Assert.AreEqual(heldRegion, shell.RegionIndex);
                Assert.IsTrue(shell.RegionMenuOpen);
                pointer.dragging = false;
                tap.OnPointerClick(pointer);
                Assert.AreEqual(7, shell.RegionIndex);
                Assert.AreEqual(MapScope.Oceania, rig.Scope);
                Assert.IsFalse(shell.RegionMenuOpen);
                Assert.IsFalse(Find(shell.transform, "RegionMenu").GetComponent<CanvasGroup>().blocksRaycasts);

                shell.ToggleRegionMenu();
                var group = Find(shell.transform, "RegionMenu").GetComponent<CanvasGroup>();
                var lastLabel = Find(shell.transform, "Region7").Find("Name").GetComponent<Text>().text;
                Assert.IsTrue(group.blocksRaycasts);
                Assert.IsTrue(shell.SelectRegion(7));
                Assert.AreEqual(MapScope.Oceania, rig.Scope);
                Assert.AreEqual(7, shell.RegionIndex);
                Assert.IsFalse(shell.RegionMenuOpen);
                Assert.IsFalse(group.blocksRaycasts);
                Assert.AreEqual(lastLabel, Find(shell.transform, "RegionChip").Find("Name").GetComponent<Text>().text);

                var labels = new[]
                {
                    new[] { "Dünya", "Türkiye", "Avrupa", "Asya", "Afrika", "Kuzey Amerika", "Güney Amerika", "Okyanusya" },
                    new[] { "World", "Turkey", "Europe", "Asia", "Africa", "North America", "South America", "Oceania" },
                    new[] { "Welt", "Türkei", "Europa", "Asien", "Afrika", "Nordamerika", "Südamerika", "Ozeanien" }
                };
                var widths = new[] { new Vector2(1920f, 1080f), new Vector2(2868f, 1320f) };
                var widthSafes = new[] { new Rect(0f, 0f, 1920f, 1080f), new Rect(186f, 60f, 2496f, 1200f) };
                for (var frame = 0; frame < widths.Length; frame++)
                {
                    shell.Apply(widths[frame].x, widths[frame].y, widthSafes[frame]);
                    if (!shell.RegionMenuOpen)
                        shell.ToggleRegionMenu();
                    Canvas.ForceUpdateCanvases();
                    for (var locale = 0; locale < labels.Length; locale++)
                    {
                        for (var i = 0; i < labels[locale].Length; i++)
                            Find(shell.transform, "Region" + i).Find("Name").GetComponent<Text>().text = labels[locale][i];
                        Canvas.ForceUpdateCanvases();
                        var typePixels = MapCommandLayout.MenuTypePixels(widths[frame].x, widths[frame].y);
                        var scale = MapHudLayout.CanvasScale(widths[frame].x, widths[frame].y);
                        for (var i = 0; i < labels[locale].Length; i++)
                        {
                            var rowName = Find(shell.transform, "Region" + i).Find("Name").GetComponent<Text>();
                            var estimate = MapCommandLayout.TextWidth(labels[locale][i], typePixels) / Mathf.Max(0.01f, scale);
                            Assert.Greater(rowName.preferredWidth, 8f, labels[locale][i]);
                            Assert.LessOrEqual(rowName.preferredWidth, estimate + 1f, widths[frame] + " " + labels[locale][i]);
                            TestContext.WriteLine(
                                "fit " + widths[frame].x.ToString("0") + " " + labels[locale][i]
                                + " " + rowName.preferredWidth.ToString("0.0") + "/" + estimate.ToString("0.0"));
                        }
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

        [Test]
        public void RegionMenu_LastRowKeepsAFullTouchTargetWhereItCanBeReached()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                var frames = new[]
                {
                    new Vector2(844f, 390f),
                    new Vector2(932f, 430f),
                    new Vector2(932f, 430f),
                    new Vector2(960f, 540f),
                    new Vector2(1280f, 720f),
                    new Vector2(1920f, 1080f),
                    new Vector2(2868f, 1320f)
                };
                var safes = new[]
                {
                    new Rect(0f, 0f, 844f, 390f),
                    new Rect(0f, 0f, 932f, 430f),
                    new Rect(32f, 16f, 868f, 398f),
                    new Rect(0f, 0f, 960f, 540f),
                    new Rect(0f, 0f, 1280f, 720f),
                    new Rect(0f, 0f, 1920f, 1080f),
                    new Rect(186f, 60f, 2496f, 1200f)
                };
                var problems = new System.Text.StringBuilder();
                for (var i = 0; i < frames.Length; i++)
                {
                    shell.Apply(frames[i].x, frames[i].y, safes[i]);
                    if (!shell.RegionMenuOpen)
                        shell.ToggleRegionMenu();
                    Canvas.ForceUpdateCanvases();
                    var menu = Find(shell.transform, "RegionMenu") as RectTransform;
                    var scroll = menu.GetComponent<ScrollRect>();
                    var viewport = scroll.viewport;
                    var last = Find(shell.transform, "Region7") as RectTransform;
                    var first = Find(shell.transform, "Region0") as RectTransform;
                    var scale = MapHudLayout.CanvasScale(frames[i].x, frames[i].y);
                    var point = MapCommandLayout.PointScale(frames[i].x, frames[i].y);
                    var touch = MapCommandLayout.TouchPixels(frames[i].x, frames[i].y);
                    if (scroll.vertical)
                    {
                        scroll.verticalNormalizedPosition = 1f;
                        Canvas.ForceUpdateCanvases();
                    }

                    var firstPhysical = HitHeight(first, viewport, scale);
                    var topPos = scroll.content.anchoredPosition.y;
                    if (scroll.vertical)
                    {
                        scroll.verticalNormalizedPosition = 0f;
                        Canvas.ForceUpdateCanvases();
                        scroll.verticalNormalizedPosition = 0f;
                        Canvas.ForceUpdateCanvases();
                        DriveScrollbar(scroll);
                    }

                    var rowRect = WorldRect(last);
                    var viewRect = WorldRect(viewport);
                    var visible = Overlap(rowRect, viewRect);
                    var physical = rowRect.height < 0.001f ? 0f : visible.height / rowRect.height * last.rect.height * scale;
                    var points = physical / Mathf.Max(1f, point);
                    var canvas = menu.GetComponentInParent<Canvas>();
                    var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                    var mask = viewport.GetComponent<RectMask2D>();
                    var center = MaskHit(mask, camera, new Vector2(rowRect.center.x, rowRect.center.y));
                    var top = MaskHit(mask, camera, new Vector2(rowRect.center.x, Mathf.Lerp(rowRect.yMax, rowRect.yMin, 0.04f)));
                    var bottom = MaskHit(mask, camera, new Vector2(rowRect.center.x, Mathf.Lerp(rowRect.yMin, rowRect.yMax, 0.04f)));
                    var below = rowRect.yMin < viewRect.yMin - 0.5f && MaskHit(mask, camera, new Vector2(rowRect.center.x, (rowRect.yMin + viewRect.yMin) * 0.5f));
                    var summary = Find(shell.transform, "SummaryBar") as RectTransform;
                    var menuBottom = safes[i].yMin + menu.anchoredPosition.y * scale;
                    var summaryTop = summary == null ? 0f : safes[i].yMin + (summary.anchoredPosition.y + summary.sizeDelta.y) * scale;
                    var clearance = menuBottom - summaryTop;
                    TestContext.WriteLine(
                        "hit " + frames[i].x.ToString("0") + "x" + frames[i].y.ToString("0")
                        + " safe=" + safes[i].height.ToString("0")
                        + " scale=" + scale.ToString("0.000")
                        + " rowPx=" + (last.rect.height * scale).ToString("0.0")
                        + " rowPt=" + ((last.rect.height * scale) / point).ToString("0.0")
                        + " hitPx=" + physical.ToString("0.0")
                        + " hitPt=" + points.ToString("0.0")
                        + " need=" + touch.ToString("0")
                        + " firstPx=" + firstPhysical.ToString("0.0")
                        + " topPos=" + topPos.ToString("0.0")
                        + " content=" + scroll.content.rect.height.ToString("0.0")
                        + " view=" + viewport.rect.height.ToString("0.0")
                        + " scroll=" + scroll.vertical
                        + " center=" + center
                        + " top=" + top
                        + " bottom=" + bottom
                        + " masked=" + below
                        + " clearance=" + clearance.ToString("0.0")
                        + " menuH=" + (menu.sizeDelta.y * scale).ToString("0.0"));
                    if (physical < touch - 1f)
                        problems.AppendLine(frames[i] + " reachable hit " + physical.ToString("0.0") + " < " + touch.ToString("0"));
                    if (!center || !top || !bottom)
                        problems.AppendLine(frames[i] + " mask rejects the reached row center=" + center + " top=" + top + " bottom=" + bottom);
                    if (below)
                        problems.AppendLine(frames[i] + " mask accepts a point outside the viewport");
                    if (firstPhysical < touch - 1f)
                        problems.AppendLine(frames[i] + " first row hit " + firstPhysical.ToString("0.0") + " at " + topPos.ToString("0.0"));
                    if (clearance < 1f)
                        problems.AppendLine(frames[i] + " menu clearance " + clearance.ToString("0.0"));
                }

                Assert.AreEqual(string.Empty, problems.ToString());
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        static float HitHeight(RectTransform row, RectTransform viewport, float scale)
        {
            var rowRect = WorldRect(row);
            var visible = Overlap(rowRect, WorldRect(viewport));
            if (rowRect.height < 0.001f)
                return 0f;
            return visible.height / rowRect.height * row.rect.height * scale;
        }

        static bool MaskHit(RectMask2D mask, Camera camera, Vector2 world)
        {
            if (mask == null)
                return false;
            var screen = RectTransformUtility.WorldToScreenPoint(camera, new Vector3(world.x, world.y, 0f));
            return ((ICanvasRaycastFilter)mask).IsRaycastLocationValid(screen, camera);
        }

        static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static Rect Overlap(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
                return new Rect(xMin, yMin, 0f, 0f);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static void DriveScrollbar(ScrollRect scroll)
        {
            var late = typeof(ScrollRect).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            late.Invoke(scroll, null);
        }

        static bool Contains(RectTransform viewport, RectTransform row)
        {
            var view = new Vector3[4];
            var item = new Vector3[4];
            viewport.GetWorldCorners(view);
            row.GetWorldCorners(item);
            var min = view[0];
            var max = view[2];
            for (var i = 0; i < item.Length; i++)
            {
                if (item[i].x < min.x - 1f || item[i].x > max.x + 1f || item[i].y < min.y - 1f || item[i].y > max.y + 1f)
                    return false;
            }

            return true;
        }

        [Test]
        public void RegionMenu_FitsLongLabelsWithoutTheRetiredSoonColumn()
        {
            var labels = new[]
            {
                "Dünya", "Türkiye", "Avrupa", "Asya", "Afrika", "Kuzey Amerika", "Güney Amerika", "Okyanusya",
                "World", "Turkey", "Europe", "Asia", "Africa", "North America", "South America", "Oceania",
                "Welt", "Türkei", "Europa", "Asien", "Afrika", "Nordamerika", "Südamerika", "Ozeanien"
            };
            var desktop = MapCommandLayout.MenuTypePixels(1920f, 1080f);
            var width = MapCommandLayout.RegionMenuWidth(desktop, 1f, labels);
            Assert.Less(width, 280f);
            Assert.Greater(width, 140f);
            for (var i = 0; i < labels.Length; i++)
                Assert.IsTrue(MapCommandLayout.LabelFits(labels[i], desktop, width - 36f), labels[i]);

            var phone = MapCommandLayout.MenuTypePixels(2868f, 1320f);
            Assert.AreEqual(45f, phone, 0.01f);
            Assert.AreEqual(132f, MapCommandLayout.TouchPixels(2868f, 1320f), 0.01f);
            Assert.Greater(MapCommandLayout.RegionMenuWidth(phone, 3f, labels), width);
        }

        [Test]
        public void RegionTransition_EasesInOutWithoutOvershootOrANewFinalFrame()
        {
            Assert.GreaterOrEqual(MapCameraRig.RegionTransitionSeconds, 0.35f);
            Assert.LessOrEqual(MapCameraRig.RegionTransitionSeconds, 0.60f);
            Assert.AreEqual(0f, MapCameraRig.RegionTransitionEase(0f), 0.0001f);
            Assert.AreEqual(1f, MapCameraRig.RegionTransitionEase(1f), 0.0001f);
            Assert.AreEqual(0.5f, MapCameraRig.RegionTransitionEase(0.5f), 0.0001f);

            var from = new Vector2(-10f, 40f);
            var to = new Vector2(216.18f, -52.13f);
            const float fromSize = 223.93f;
            const float toSize = 51.82f;
            var previous = from;
            var previousSize = fromSize;
            for (var step = 0; step <= 8; step++)
            {
                MapCameraRig.SampleRegionTransition(from, fromSize, to, toSize, step / 8f, out var center, out var size);
                Assert.GreaterOrEqual(center.x, Mathf.Min(from.x, to.x) - 0.001f);
                Assert.LessOrEqual(center.x, Mathf.Max(from.x, to.x) + 0.001f);
                Assert.GreaterOrEqual(center.y, Mathf.Min(from.y, to.y) - 0.001f);
                Assert.LessOrEqual(center.y, Mathf.Max(from.y, to.y) + 0.001f);
                Assert.GreaterOrEqual(size, Mathf.Min(fromSize, toSize) - 0.001f);
                Assert.LessOrEqual(size, Mathf.Max(fromSize, toSize) + 0.001f);
                if (step > 0)
                {
                    Assert.LessOrEqual((center - to).sqrMagnitude, (previous - to).sqrMagnitude + 0.0001f);
                    Assert.LessOrEqual(size, previousSize + 0.0001f);
                }

                previous = center;
                previousSize = size;
            }

            MapCameraRig.SampleRegionTransition(from, fromSize, to, toSize, 1f, out var arrived, out var arrivedSize);
            Assert.AreEqual(to.x, arrived.x, 0.0001f);
            Assert.AreEqual(to.y, arrived.y, 0.0001f);
            Assert.AreEqual(toSize, arrivedSize, 0.0001f);

            MapCameraRig.SampleRegionTransition(from, fromSize, to, toSize, 0.4f, out var mid, out var midSize);
            var redirected = new Vector2(-143.39f, 109.56f);
            const float redirectedSize = 107.29f;
            MapCameraRig.SampleRegionTransition(mid, midSize, redirected, redirectedSize, 0f, out var restarted, out var restartedSize);
            Assert.AreEqual(mid.x, restarted.x, 0.0001f);
            Assert.AreEqual(mid.y, restarted.y, 0.0001f);
            Assert.AreEqual(midSize, restartedSize, 0.0001f);
            MapCameraRig.SampleRegionTransition(mid, midSize, redirected, redirectedSize, 1f, out var retargeted, out var retargetedSize);
            Assert.AreEqual(redirected.x, retargeted.x, 0.0001f);
            Assert.AreEqual(redirected.y, retargeted.y, 0.0001f);
            Assert.AreEqual(redirectedSize, retargetedSize, 0.0001f);
        }

        static int CountNamed(Transform parent, string name)
        {
            var count = parent.name == name ? 1 : 0;
            for (var i = 0; i < parent.childCount; i++)
                count += CountNamed(parent.GetChild(i), name);
            return count;
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
