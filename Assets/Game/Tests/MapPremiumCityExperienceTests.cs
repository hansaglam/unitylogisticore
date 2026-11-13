using LogistiCore.Backend;
using LogistiCore.EditorTools;
using LogistiCore.Localization;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LogistiCore.Tests
{
    public sealed class MapPremiumCityExperienceTests
    {
        [Test]
        public void CityArt_IsReusableAndDoesNotRequireExternalAsset()
        {
            var first = MapPremiumCityArt.Banner;
            Assert.IsNotNull(first);
            Assert.AreSame(first, MapPremiumCityArt.Banner);
            Assert.AreEqual(320, first.texture.width);
            Assert.AreEqual(128, first.texture.height);
        }

        [Test]
        public void SchematicNeighborCounts_AreDerivedFromRealGraphOnly()
        {
            var degreeTotal = 0;
            foreach (var city in MapCities.All)
            {
                var count = MapSchematicLinks.NeighborCount(city.Id);
                Assert.Greater(count, 0, city.Id);
                degreeTotal += count;
            }
            Assert.AreEqual(MapSchematicLinks.Count * 2, degreeTotal);
            Assert.AreEqual(0, MapSchematicLinks.NeighborCount("fictional_city"));
            Assert.AreEqual(0, MapSchematicLinks.NeighborCount(null));
        }

        [Test]
        public void SelectionEasing_IsClampedMonotonicAndEndsOnTarget()
        {
            Assert.AreEqual(0f, MapPremiumNetworkOverlay.SelectionEase(-10f), 0.0001f);
            Assert.AreEqual(0f, MapPremiumNetworkOverlay.SelectionEase(0f), 0.0001f);
            Assert.AreEqual(0.5f, MapPremiumNetworkOverlay.SelectionEase(0.5f), 0.0001f);
            Assert.AreEqual(1f, MapPremiumNetworkOverlay.SelectionEase(1f), 0.0001f);
            Assert.AreEqual(1f, MapPremiumNetworkOverlay.SelectionEase(10f), 0.0001f);
            var previous = 0f;
            for (var i = 1; i <= 100; i++)
            {
                var current = MapPremiumNetworkOverlay.SelectionEase(i / 100f);
                Assert.GreaterOrEqual(current, previous);
                previous = current;
            }
        }

        [Test]
        public void CityPanel_UsesDataBoundInfoAndNonInteractiveArtwork()
        {
            MapCompanyEconomyRead.ResetForTests();
            MapSceneSetup.EnsureLocalization();
            var root = new GameObject("CityArtHarness", typeof(RectTransform),
                typeof(Canvas), typeof(MapCommandShell));
            try
            {
                var shell = root.GetComponent<MapCommandShell>();
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                shell.ShowCity("ankara");
                Assert.IsTrue(shell.ContextOpen);
                var panel = Find(root.transform, "ContextPanel");
                var art = Find(panel, "CityHeroArtwork").GetComponent<Image>();
                var coordinates = Find(panel, "CityCoordinatesCard").GetComponent<Image>();
                var schematic = Find(panel, "CityNetworkCard").GetComponent<Image>();
                var count = Find(panel, "SchematicCount").GetComponent<Text>();
                Assert.IsTrue(art.gameObject.activeSelf);
                Assert.IsFalse(art.raycastTarget);
                Assert.IsFalse(coordinates.raycastTarget);
                Assert.IsFalse(schematic.raycastTarget);
                Assert.AreEqual(Image.Type.Sliced, schematic.type);
                Assert.AreEqual(
                    MapSchematicLinks.NeighborCount("ankara").ToString(), count.text);

                shell.CloseContext();
                Assert.IsFalse(shell.ContextOpen);
            }
            finally
            {
                Object.DestroyImmediate(root);
                MapCompanyEconomyRead.ResetForTests();
            }
        }

        [Test]
        public void ManagementArt_IsCachedAndNotAHitTarget()
        {
            var first = MapPremiumPanelArt.Header;
            Assert.AreSame(first, MapPremiumPanelArt.Header);
            Assert.AreEqual(256, first.texture.width);
            Assert.AreEqual(88, first.texture.height);

            var root = new GameObject("PanelArtHarness", typeof(RectTransform),
                typeof(Canvas), typeof(MapCommandShell));
            try
            {
                var shell = root.GetComponent<MapCommandShell>();
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                var panel = shell.Management;
                Assert.IsNotNull(panel);
                var art = Find(panel.transform, "ManagementHeaderArt").GetComponent<Image>();
                var accent = Find(panel.transform, "ManagementHeaderAccent").GetComponent<Image>();
                var close = Find(panel.transform, "ManagementClose").GetComponent<Button>();
                Assert.AreSame(first, art.sprite);
                Assert.IsFalse(art.raycastTarget);
                Assert.IsFalse(accent.raycastTarget);
                Assert.IsTrue(close.GetComponent<Image>().raycastTarget);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AllMapCardCopy_IsProvidedInEnglishTurkishGerman()
        {
            var title = false;
            var note = false;
            foreach (var line in OpsContent.Lines)
            {
                if (line.Key != OpsContent.MapSchematicTitle &&
                    line.Key != OpsContent.MapSchematicNote)
                    continue;
                Assert.IsFalse(string.IsNullOrWhiteSpace(line.English));
                Assert.IsFalse(string.IsNullOrWhiteSpace(line.Turkish));
                Assert.IsFalse(string.IsNullOrWhiteSpace(line.German));
                title |= line.Key == OpsContent.MapSchematicTitle;
                note |= line.Key == OpsContent.MapSchematicNote;
            }
            Assert.IsTrue(title);
            Assert.IsTrue(note);
        }

        static Transform Find(Transform parent, string name)
        {
            if (parent == null)
                return null;
            if (parent.name == name)
                return parent;
            for (var i = 0; i < parent.childCount; i++)
            {
                var found = Find(parent.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
