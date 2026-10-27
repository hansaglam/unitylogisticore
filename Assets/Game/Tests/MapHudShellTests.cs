using System.IO;
using System.Security.Cryptography;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace LogistiCore.Tests
{
    public sealed class MapHudShellTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "c4f58b0ce718ff18835f6ee6b9232dfd57c71206efdc66990e1657dbdbef979b";

        static readonly Vector2[] Viewports =
        {
            new Vector2(1920f, 1080f),
            new Vector2(1280f, 720f),
            new Vector2(960f, 540f),
            new Vector2(844f, 390f),
            new Vector2(932f, 430f)
        };

        [Test]
        public void Scene_IdentityStaysOnTheAuthoredMap()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var meta = File.ReadAllText(ScenePath + ".meta");
            StringAssert.Contains("guid: 8d5f07398f83c446f987c9b2827cff18", meta);
        }

        [Test]
        public void Copy_FitsTheLongestLocalizedLabels()
        {
            foreach (var viewport in Viewports)
            {
                var safe = new Rect(0f, 0f, viewport.x, viewport.y);
                var metrics = MapHudLayout.Evaluate(viewport.x, viewport.y, safe, true);
                Assert.IsTrue(MapHudLayout.FitsOneLine("Türkiye", metrics.RailLabelSize, metrics.RailTextWidth), viewport.ToString());
                Assert.IsTrue(MapHudLayout.FitsOneLine("Sıfırla", metrics.RailLabelSize, metrics.RailTextWidth), viewport.ToString());
                Assert.IsTrue(MapHudLayout.FitsOneLine("Zurück", metrics.RailLabelSize, metrics.RailTextWidth), viewport.ToString());
                Assert.IsTrue(MapHudLayout.FitsOneLine(
                    "Aufklappen",
                    metrics.CollapseSize,
                    metrics.CollapseWidth - 16f));
                Assert.IsTrue(MapHudLayout.FitsOneLine(
                    "Stadt auswählen",
                    metrics.InspectorValueSize,
                    metrics.InspectorWidth - metrics.InspectorPadding * 2f));
                Assert.IsTrue(MapHudLayout.FitsOneLine(
                    "Bir şehir seçin",
                    metrics.InspectorValueSize,
                    metrics.InspectorWidth - metrics.InspectorPadding * 2f));
            }
        }

        [Test]
        public void Shell_UsesACompactCardAndChipWithoutDuplicatingChrome()
        {
            var built = BuildShell();
            try
            {
                var full = new Rect(0f, 0f, 1920f, 1080f);
                built.Shell.ApplyHudLayout(1920f, 1080f, full);
                built.Shell.ApplyHudLayout(1920f, 1080f, full);

                var metrics = MapHudLayout.Evaluate(1920f, 1080f, full, true);
                Assert.AreEqual(1, CountNamed(built.Top, "Brand"));
                Assert.AreEqual(1, CountNamed(built.World, "Glyph"));
                Assert.AreEqual(MapHudPalette.Brand, built.Top.Find("Brand").GetComponent<Text>().text);
                Assert.IsFalse(built.RailImage.raycastTarget);
                Assert.IsTrue(built.World.GetComponent<Image>().raycastTarget);
                Assert.AreEqual(new Vector2(1f, 1f), built.Inspector.anchorMin);
                Assert.AreEqual(new Vector2(1f, 1f), built.Inspector.anchorMax);
                Assert.AreEqual(metrics.InspectorWidth, built.Inspector.sizeDelta.x, 0.1f);
                Assert.AreEqual(metrics.CardHeight(true), built.Inspector.sizeDelta.y, 0.5f);
                Assert.Less(built.Inspector.sizeDelta.y, 1920f * 0.45f);
                Assert.AreEqual(Vector2.zero, built.Status.anchorMax);
                Assert.LessOrEqual(built.Status.sizeDelta.x, metrics.ChipWidth + 0.1f);
                Assert.Less(built.Status.sizeDelta.x, 1920f * 0.5f);
                Assert.GreaterOrEqual(built.Status.anchoredPosition.x, metrics.RailWidth - 0.1f);
                Assert.GreaterOrEqual(built.World.GetComponent<LayoutElement>().preferredHeight, metrics.RailButtonHeight - 0.1f);

                built.Shell.SetInspectorVisible(false);
                Assert.IsFalse(built.Inspector.gameObject.activeSelf);
                built.Shell.SetInspectorVisible(true);
                Assert.IsTrue(built.Inspector.gameObject.activeSelf);
                Assert.AreEqual(1, CountNamed(built.Top, "Brand"));
            }
            finally
            {
                Object.DestroyImmediate(built.Shell.gameObject);
                Object.DestroyImmediate(built.Canvas.gameObject);
            }
        }

        [Test]
        public void Shell_KeepsTheBrandOnOneLineAndOneZoomSymbol()
        {
            var built = BuildShell();
            try
            {
                var scaler = built.Canvas.GetComponent<CanvasScaler>();
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
                Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
                Assert.AreEqual(0.5f, scaler.matchWidthOrHeight, 0.001f);
                Assert.Greater(scaler.scaleFactor, 0f);

                built.Root.anchorMin = Vector2.zero;
                built.Root.anchorMax = Vector2.zero;
                built.Root.pivot = Vector2.zero;
                var title = built.Top.Find("Title").GetComponent<Text>();
                var scope = built.Top.Find("Scope").GetComponent<Text>();
                var hide = built.Top.Find("Collapse/Label").GetComponent<Text>();
                var hideButton = built.Top.Find("Collapse").GetComponent<RectTransform>();
                var hideImage = hideButton.GetComponent<Image>();
                var status = built.Status.Find("StatusLabel").GetComponent<Text>();
                var copies = new[]
                {
                    new[] { "Harita", "Türkiye", "Gizle" },
                    new[] { "Map", "Turkey", "Hide" },
                    new[] { "Karte", "Türkei", "Aufklappen" }
                };
                foreach (var viewport in Viewports)
                {
                    var safe = new Rect(0f, 0f, viewport.x, viewport.y);
                    var metrics = MapHudLayout.Evaluate(viewport.x, viewport.y, safe, true);
                    var scale = metrics.CanvasScale < 0.0001f ? 1f : metrics.CanvasScale;
                    Assert.AreEqual(MapHudLayout.CanvasScale(viewport.x, viewport.y), scale, 0.0001f, viewport.ToString());
                    built.Root.sizeDelta = new Vector2(viewport.x / scale, viewport.y / scale);

                    foreach (var copy in copies)
                    {
                        title.text = copy[0];
                        scope.text = copy[1];
                        hide.text = copy[2];
                        status.text = copy[1] + "  ·  Ankara";
                        built.Shell.ApplyHudLayout(viewport.x, viewport.y, safe);
                        built.Shell.ApplyHudLayout(viewport.x, viewport.y, safe);
                        Canvas.ForceUpdateCanvases();
                        LayoutRebuilder.ForceRebuildLayoutImmediate(built.Root);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(built.Top);

                        var label = viewport + " " + copy[0];
                        var brand = built.Top.Find("Brand").GetComponent<Text>();
                        var brandElement = brand.GetComponent<LayoutElement>();
                        Assert.AreEqual(MapHudPalette.Brand, brand.text, label);
                        Assert.IsTrue(brand.enabled, label);
                        Assert.AreEqual(HorizontalWrapMode.Overflow, brand.horizontalOverflow, label);
                        Assert.GreaterOrEqual(brand.fontSize * scale, 12f, label + " brand screen px");
                        Assert.GreaterOrEqual(title.fontSize * scale, 14f, label + " title screen px");
                        Assert.Greater(title.fontSize, brand.fontSize, label + " title hierarchy");
                        Assert.LessOrEqual(LineCount(brand, brand.text), 1, label + " brand lines");
                        Assert.Less(brandElement.minWidth, built.Top.rect.width * 0.4f, label + " brand slot");
                        Assert.Greater(brand.rectTransform.rect.width, 24f, label + " brand width");

                        Assert.IsTrue(title.enabled, label);
                        Assert.IsTrue(scope.enabled, label);
                        Assert.IsTrue(hide.enabled, label);
                        Assert.Greater(title.rectTransform.rect.width, 24f, label + " title width");
                        Assert.Greater(scope.rectTransform.rect.width, 24f, label + " scope width");
                        Assert.Less(scope.rectTransform.rect.width, metrics.ScopeWidth - 4f, label + " scope slot");
                        Assert.Greater(hideButton.rect.width, 40f, label + " hide width");
                        Assert.IsTrue(hideImage.raycastTarget, label + " hide raycast");
                        AssertContained(built.Top, brand.rectTransform, label + " brand");
                        AssertContained(built.Top, title.rectTransform, label + " title");
                        AssertContained(built.Top, scope.rectTransform, label + " scope");
                        AssertContained(built.Top, hideButton, label + " hide");
                        Assert.LessOrEqual(
                            BoundsOf(brand.rectTransform).xMax,
                            BoundsOf(title.rectTransform).xMin + 1.5f,
                            label + " brand/title");

                        var estimate = status.text.Length * status.fontSize * 0.68f;
                        Assert.AreEqual(Vector2.zero, built.Status.anchorMin, label);
                        Assert.AreEqual(Vector2.zero, built.Status.anchorMax, label);
                        Assert.AreEqual(built.Status.sizeDelta.x, built.Status.rect.width, 1.5f, label + " chip rect");
                        Assert.GreaterOrEqual(built.Status.rect.width, estimate * 0.55f, label + " chip vs text");
                        Assert.LessOrEqual(built.Status.rect.width, estimate * 1.7f + 36f, label + " chip padding");
                        Assert.Less(built.Status.rect.width, built.Root.rect.width * 0.45f, label + " chip span");
                        Assert.GreaterOrEqual(status.fontSize * scale, 11f, label + " status screen px");
                    }

                    status.text = "Türkiye";
                    built.Shell.ApplyHudLayout(viewport.x, viewport.y, safe);
                    Canvas.ForceUpdateCanvases();
                    var scopeOnly = built.Status.rect.width;
                    status.text = "Türkiye  ·  Ankara";
                    built.Shell.ApplyHudLayout(viewport.x, viewport.y, safe);
                    Canvas.ForceUpdateCanvases();
                    Assert.Greater(built.Status.rect.width, scopeOnly + 8f, viewport + " city widens chip");
                    Assert.Less(built.Status.rect.width, built.Root.rect.width * 0.45f, viewport + " selected chip");

                    foreach (var label in new[] { "Dünya", "Welt", "Türkiye", "Türkei", "Sıfırla", "Zurück", "Reset" })
                        Assert.IsTrue(MapHudLayout.FitsOneLine(label, metrics.RailLabelSize, metrics.RailTextWidth), viewport + " " + label);
                }

                var zoomIn = built.Root.Find("CommandRail/ZoomIn");
                var zoomOut = built.Root.Find("CommandRail/ZoomOut");
                Assert.IsFalse(zoomIn.Find("Label").GetComponent<Text>().enabled);
                Assert.IsFalse(zoomOut.Find("Label").GetComponent<Text>().enabled);
                Assert.IsTrue(built.World.Find("Label").GetComponent<Text>().enabled);
                Assert.AreEqual(1, CountNamed(zoomIn, "Glyph"));
                Assert.AreEqual(1, CountNamed(zoomOut, "Glyph"));
                Assert.AreEqual(5, built.Root.Find("CommandRail").childCount);
                Assert.AreEqual(MapHudPalette.Button, built.World.GetComponent<Image>().color);
                Assert.AreEqual(MapHudPalette.ButtonActive, built.Root.Find("CommandRail/Turkey").GetComponent<Image>().color);
                Assert.IsTrue(zoomIn.GetComponent<Image>().raycastTarget);
                Assert.IsFalse(built.RailImage.raycastTarget);
                Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame);
                Assert.AreEqual(1, CountNamed(built.Top, "Brand"));
            }
            finally
            {
                Object.DestroyImmediate(built.Shell.gameObject);
                Object.DestroyImmediate(built.Canvas.gameObject);
            }
        }

        [Test]
        public void Shell_KeepsControlsInsideEveryLandscapeViewport()
        {
            var built = BuildShell();
            try
            {
                foreach (var viewport in Viewports)
                {
                    for (var open = 0; open < 2; open++)
                    {
                        var visible = open == 0;
                        built.Shell.SetInspectorVisible(visible);
                        var safe = new Rect(0f, 0f, viewport.x, viewport.y);
                        var metrics = MapHudLayout.Evaluate(viewport.x, viewport.y, safe, visible);
                        var scale = metrics.CanvasScale < 0.0001f ? 1f : metrics.CanvasScale;
                        built.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewport.x / scale);
                        built.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewport.y / scale);
                        built.Shell.ApplyHudLayout(viewport.x, viewport.y, safe);

                        Assert.AreEqual(metrics.RailButtonHeight, built.World.GetComponent<LayoutElement>().preferredHeight, 0.01f);
                        Assert.IsTrue(built.World.GetComponent<Image>().raycastTarget);
                        Assert.LessOrEqual(metrics.HorizontalChrome(visible), viewport.x / metrics.CanvasScale * 0.5f + 0.5f);
                        AssertContained(built.Root, built.World, viewport + " world");
                        AssertContained(built.Root, built.Top, viewport + " header");
                        AssertContained(built.Root, built.Status, viewport + " status");
                        if (visible)
                        {
                            AssertContained(built.Root, built.Inspector, viewport + " card");
                            AssertSeparated(built.Inspector, built.Status, viewport + " card/status");
                        }
                    }
                }

                Assert.AreEqual(1, CountNamed(built.Top, "Brand"));
                Assert.AreEqual(1, CountNamed(built.World, "Glyph"));
            }
            finally
            {
                Object.DestroyImmediate(built.Shell.gameObject);
                Object.DestroyImmediate(built.Canvas.gameObject);
            }
        }

        static void AssertContained(RectTransform root, RectTransform child, string message)
        {
            var area = BoundsOf(root);
            var item = BoundsOf(child);
            Assert.GreaterOrEqual(item.yMin, area.yMin - 1.5f, message);
            Assert.LessOrEqual(item.yMax, area.yMax + 1.5f, message);
            Assert.GreaterOrEqual(item.xMin, area.xMin - 1.5f, message);
            Assert.LessOrEqual(item.xMax, area.xMax + 1.5f, message);
        }

        static void AssertSeparated(RectTransform upper, RectTransform lower, string message)
        {
            var card = BoundsOf(upper);
            var status = BoundsOf(lower);
            Assert.Greater(card.yMin, status.yMax - 1.5f, message);
        }

        static int LineCount(Text text, string value)
        {
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.resizeTextForBestFit = false;
            var generator = new TextGenerator();
            generator.Populate(string.IsNullOrEmpty(value) ? " " : value, settings);
            return generator.lineCount;
        }

        static Rect BoundsOf(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static int CountNamed(Transform parent, string name)
        {
            var count = 0;
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    count++;
                count += CountNamed(child, name);
            }

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

        static ShellBuild BuildShell()
        {
            var canvasObject = new GameObject("hud-canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(MapHudLayout.ReferenceWidth, MapHudLayout.ReferenceHeight);
            scaler.matchWidthOrHeight = MapHudLayout.Match;

            var root = NewRect("SafeArea", canvasObject.transform);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1920f);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1080f);

            var top = NewRect("TopBar", root);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = new Vector2(1f, 1f);
            top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(0f, 56f);
            top.gameObject.AddComponent<Image>();
            var topLayout = top.gameObject.AddComponent<HorizontalLayoutGroup>();
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;

            var title = NewText("Title", top);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var scope = NewText("Scope", top);
            var scopeElement = scope.gameObject.AddComponent<LayoutElement>();
            scopeElement.preferredWidth = 220f;
            scopeElement.minHeight = 40f;
            var collapse = NewRect("Collapse", top);
            collapse.gameObject.AddComponent<Image>();
            collapse.gameObject.AddComponent<LayoutElement>();
            var collapseLabel = NewText("Label", collapse);

            var rail = NewRect("CommandRail", root);
            rail.anchorMin = new Vector2(0f, 0f);
            rail.anchorMax = new Vector2(0f, 1f);
            rail.pivot = new Vector2(0f, 0.5f);
            var railImage = rail.gameObject.AddComponent<Image>();
            railImage.raycastTarget = true;
            var railLayout = rail.gameObject.AddComponent<VerticalLayoutGroup>();
            railLayout.childAlignment = TextAnchor.UpperCenter;
            railLayout.childControlWidth = true;
            railLayout.childControlHeight = true;
            railLayout.childForceExpandWidth = true;
            railLayout.childForceExpandHeight = false;

            var names = new[] { "World", "Turkey", "ZoomIn", "ZoomOut", "Restore" };
            var buttons = new RectTransform[names.Length];
            var labels = new Text[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                buttons[i] = NewRect(names[i], rail);
                var image = buttons[i].gameObject.AddComponent<Image>();
                image.raycastTarget = true;
                var element = buttons[i].gameObject.AddComponent<LayoutElement>();
                element.minHeight = 56f;
                element.preferredHeight = 56f;
                labels[i] = NewText("Label", buttons[i]);
            }

            var inspector = NewRect("Inspector", root);
            inspector.anchorMin = new Vector2(1f, 0f);
            inspector.anchorMax = new Vector2(1f, 1f);
            inspector.pivot = new Vector2(1f, 0.5f);
            inspector.gameObject.AddComponent<Image>();
            var inspectorLayout = inspector.gameObject.AddComponent<VerticalLayoutGroup>();
            inspectorLayout.childAlignment = TextAnchor.UpperLeft;
            inspectorLayout.childControlWidth = true;
            inspectorLayout.childControlHeight = true;
            var inspectorTitle = NewText("InspectorTitle", inspector);
            var scopeLabel = NewText("ScopeLabel", inspector);
            var scopeValue = NewText("ScopeValue", inspector);
            var cityLabel = NewText("CityLabel", inspector);
            var cityValue = NewText("CityValue", inspector);
            var coordinates = NewRect("Coordinates", inspector);
            coordinates.gameObject.AddComponent<LayoutElement>();
            var coordinatesLabel = NewText("CoordinatesLabel", coordinates);
            var coordinatesValue = NewText("CoordinatesValue", coordinates);

            var status = NewRect("Status", root);
            status.anchorMin = new Vector2(0f, 0f);
            status.anchorMax = new Vector2(1f, 0f);
            status.pivot = new Vector2(0.5f, 0f);
            status.sizeDelta = new Vector2(0f, 36f);
            status.gameObject.AddComponent<Image>();
            var statusText = NewText("StatusLabel", status);
            var statusRect = statusText.rectTransform;
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            var shellObject = new GameObject("shell", typeof(MapShellView));
            var shell = shellObject.GetComponent<MapShellView>();
            var serialized = new UnityEditor.SerializedObject(shell);
            Assign(serialized, "titleText", title);
            Assign(serialized, "scopeText", scope);
            Assign(serialized, "collapseLabel", collapseLabel);
            Assign(serialized, "worldLabel", labels[0]);
            Assign(serialized, "turkeyLabel", labels[1]);
            Assign(serialized, "zoomInLabel", labels[2]);
            Assign(serialized, "zoomOutLabel", labels[3]);
            Assign(serialized, "restoreLabel", labels[4]);
            Assign(serialized, "inspectorTitle", inspectorTitle);
            Assign(serialized, "inspectorScopeLabel", scopeLabel);
            Assign(serialized, "inspectorScopeValue", scopeValue);
            Assign(serialized, "inspectorCityLabel", cityLabel);
            Assign(serialized, "inspectorCityValue", cityValue);
            Assign(serialized, "inspectorCoordinatesLabel", coordinatesLabel);
            Assign(serialized, "inspectorCoordinatesValue", coordinatesValue);
            Assign(serialized, "statusText", statusText);
            Assign(serialized, "inspectorPanel", inspector.gameObject);
            Assign(serialized, "coordinatesRow", coordinates.gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return new ShellBuild
            {
                Canvas = canvas,
                Root = root,
                Top = top,
                RailImage = railImage,
                World = buttons[0],
                Inspector = inspector,
                Status = status,
                Shell = shell
            };
        }

        static void Assign(UnityEditor.SerializedObject serialized, string property, Object value)
        {
            serialized.FindProperty(property).objectReferenceValue = value;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static Text NewText(string name, Transform parent)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            return text;
        }

        sealed class ShellBuild
        {
            public Canvas Canvas;
            public RectTransform Root;
            public RectTransform Top;
            public Image RailImage;
            public RectTransform World;
            public RectTransform Inspector;
            public RectTransform Status;
            public MapShellView Shell;
        }
    }
}
