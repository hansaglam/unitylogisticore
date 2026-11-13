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
    public sealed class MapPremiumHeaderRailTests
    {
        GameObject _root;
        MapCommandShell _shell;

        [SetUp]
        public void SetUp()
        {
            MapCompanyEconomyRead.ResetForTests();
            MapSceneSetup.EnsureLocalization();
            if (LocaleService.IsReady)
                LocaleService.TrySelect(LocaleCodes.English);
            _root = new GameObject("HeaderRailTest", typeof(RectTransform), typeof(Canvas), typeof(MapCommandShell));
            _shell = _root.GetComponent<MapCommandShell>();
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
        }

        [TearDown]
        public void TearDown()
        {
            MapCompanyEconomyRead.ResetForTests();
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void BrandEmblem_IsCachedAndNonInteractive()
        {
            var header = Find(_shell.transform, "CommandHeader");
            var emblem = Find(header, "BrandEmblem").GetComponent<Image>();
            var top = Find(header, "HeaderTopAccent").GetComponent<Image>();
            Assert.AreSame(MapPremiumBrandIcon.Get(), emblem.sprite);
            Assert.AreSame(MapPremiumBrandIcon.Get(), MapPremiumBrandIcon.Get());
            Assert.AreEqual(64, emblem.sprite.texture.width);
            Assert.IsFalse(emblem.raycastTarget);
            Assert.IsFalse(top.raycastTarget);
        }

        [Test]
        public void RailButtons_KeepTouchesOnTheRoot()
        {
            var button = Find(_shell.transform, "Mode0").GetComponent<Button>();
            var face = Find(button.transform, "PremiumFace").GetComponent<Image>();
            var edge = Find(button.transform, "PremiumEdge").GetComponent<Image>();
            var plate = Find(button.transform, "PremiumIconPlate").GetComponent<Image>();
            Assert.IsTrue(button.GetComponent<Image>().raycastTarget);
            Assert.IsFalse(face.raycastTarget);
            Assert.IsFalse(edge.raycastTarget);
            Assert.IsFalse(plate.raycastTarget);
            Assert.AreEqual(Image.Type.Sliced, face.type);
            Assert.AreEqual(Image.Type.Sliced, plate.type);
        }

        [Test]
        public void SelectedMode_UpdatesAccentWithoutChangingBindings()
        {
            var item = Find(_shell.transform, "Mode0");
            var edge = Find(item, "PremiumEdge").GetComponent<Image>();
            var outline = Find(item, "PremiumFace").GetComponent<Outline>();
            _shell.ShowModule(0);
            Assert.AreEqual(MapPremiumTheme.Cyan, new Color(edge.color.r, edge.color.g, edge.color.b, 1f));
            Assert.Greater(edge.color.a, 0.95f);
            Assert.IsTrue(outline.enabled);
            Assert.IsTrue(_shell.ManagementOpen);
        }

        [Test]
        public void CompactHeader_PreservesBrandAndCountrySeparation()
        {
            _shell.Apply(844f, 390f, new Rect(0f, 0f, 844f, 390f));
            Canvas.ForceUpdateCanvases();
            var core = Find(_shell.transform, "BrandCore") as RectTransform;
            var region = Find(_shell.transform, "RegionChip") as RectTransform;
            Assert.IsFalse(MapTopHud.Overlaps(core, region));
            Assert.AreEqual(1, Count(_shell.transform, "BrandEmblem"));
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

        static int Count(Transform root, string name)
        {
            var result = root.name == name ? 1 : 0;
            for (var i = 0; i < root.childCount; i++)
                result += Count(root.GetChild(i), name);
            return result;
        }
    }
}
