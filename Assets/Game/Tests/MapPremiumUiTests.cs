using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LogistiCore.Tests
{
    public sealed class MapPremiumUiTests
    {
        GameObject _root;
        RectTransform _rect;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("PremiumTest", typeof(RectTransform), typeof(Canvas));
            _rect = (RectTransform)_root.transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void TextTokens_PassReferenceContrast()
        {
            Assert.GreaterOrEqual(MapPremiumTheme.ContrastRatio(
                MapPremiumTheme.TextPrimary, MapPremiumTheme.Panel), 4.5f);
            Assert.GreaterOrEqual(MapPremiumTheme.ContrastRatio(
                MapPremiumTheme.TextSecondary, MapPremiumTheme.Card), 4.5f);
            Assert.GreaterOrEqual(MapPremiumTheme.ContrastRatio(
                MapPremiumTheme.Cyan, MapPremiumTheme.Card), 4.5f);
            Assert.GreaterOrEqual(MapPremiumTheme.MinimumTouchPoints, 44f);
            Assert.AreEqual(MapPremiumTheme.Cyan, MapHudPalette.Accent);
        }

        [Test]
        public void Surfaces_ShareCachedNineSliceAndDoNotBlockMap()
        {
            var panel = MapPremiumUi.CreateSurface(_rect, "Panel", MapPremiumSurface.Panel);
            var card = MapPremiumUi.CreateSurface(_rect, "Card", MapPremiumSurface.Card);
            Assert.AreSame(panel.sprite, card.sprite);
            Assert.AreEqual(Image.Type.Sliced, panel.type);
            Assert.AreEqual(MapPremiumTheme.Panel, panel.color);
            Assert.AreEqual(MapPremiumTheme.Card, card.color);
            Assert.IsFalse(panel.raycastTarget);
            Assert.IsFalse(card.raycastTarget);
            Assert.Greater(panel.sprite.border.x, 0f);
        }

        [Test]
        public void Buttons_HaveResponsiveTouchContractAndRealHitSurface()
        {
            var button = MapPremiumUi.CreateButton(_rect, "Action", true);
            var image = button.GetComponent<Image>();
            MapPremiumUi.EnsureTouchTarget(button, 2868f, 1320f, 1.35f);
            var layout = button.GetComponent<LayoutElement>();
            Assert.IsTrue(image.raycastTarget);
            Assert.AreSame(image, button.targetGraphic);
            Assert.AreEqual(Selectable.Transition.ColorTint, button.transition);
            Assert.GreaterOrEqual(layout.minWidth * 1.35f, 131.5f);
            Assert.GreaterOrEqual(layout.minHeight * 1.35f, 131.5f);
        }

        [Test]
        public void StatusChip_UsesProvidedLocalizationWithoutSyntheticData()
        {
            var chip = MapPremiumUi.CreateStatusChip(
                _rect, "Connection", null, "Bağlantı yok", MapPremiumStatus.Warning);
            var image = chip.GetComponent<Image>();
            var label = chip.Find("StatusText").GetComponent<Text>();
            Assert.AreEqual("Bağlantı yok", label.text);
            Assert.AreEqual(MapPremiumTheme.Warning, label.color);
            Assert.IsFalse(label.raycastTarget);

            MapPremiumUi.SetStatusChip(image, label, "Offline", MapPremiumStatus.Error);
            Assert.AreEqual("Offline", label.text);
            Assert.AreEqual(MapPremiumTheme.Error, label.color);
        }

        [Test]
        public void NoDuplicateSprites_OnMultipleSurfaces()
        {
            var sprite = MapPremiumSprites.Rounded;
            MapPremiumUi.CreateSurface(_rect, "First", MapPremiumSurface.Chip);
            MapPremiumUi.CreateSurface(_rect, "Second", MapPremiumSurface.Selected);
            Assert.AreSame(sprite, MapPremiumSprites.Rounded);
            Assert.AreEqual(2, _root.GetComponentsInChildren<Image>(true).Length);
        }
    }
}
