using LogistiCore.Map;
using LogistiCore.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace LogistiCore.Tests
{
    /// <summary>
    /// CITY-START-01 preview regression contracts, saved for the final Unity
    /// EditMode/PlayMode test milestone. No live backend company writes.
    /// </summary>
    public sealed class HeadquartersPreviewTests
    {
        [TearDown]
        public void TearDown()
        {
            MapHeadquartersPreviewSelection.Clear();
        }

        [Test]
        public void ValidTurkeyAndEuropeanCitiesCanBePreviewedOnce()
        {
            var ankara = new MapBusinessLocation("TUR", "ankara");
            Assert.IsTrue(MapHeadquartersPreviewSelection.TryQueue(ankara));
            Assert.IsTrue(MapHeadquartersPreviewSelection.HasPending);
            Assert.IsTrue(MapHeadquartersPreviewSelection.TryConsume(out var consumed));
            Assert.AreEqual(ankara, consumed);
            Assert.IsFalse(MapHeadquartersPreviewSelection.HasPending);
            Assert.IsFalse(MapHeadquartersPreviewSelection.TryConsume(out _));

            MapBusinessLocation berlin = default;
            foreach (var city in MapGlobalCityCatalog.All)
            {
                if (city.Location.CountryCode == "DEU" && city.DisplayName == "Berlin")
                {
                    berlin = city.Location;
                    break;
                }
            }
            Assert.IsTrue(berlin.IsValid);
            Assert.IsTrue(MapHeadquartersPreviewSelection.TryQueue(berlin));
            Assert.IsTrue(MapHeadquartersPreviewSelection.TryConsume(out consumed));
            Assert.AreEqual(berlin, consumed);
        }

        [Test]
        public void RejectsUnlistedOrOutsideLaunchRegionWithoutReplacingValidPending()
        {
            var ankara = new MapBusinessLocation("TUR", "ankara");
            Assert.IsTrue(MapHeadquartersPreviewSelection.TryQueue(ankara));
            Assert.IsFalse(MapHeadquartersPreviewSelection.TryQueue(default));
            Assert.IsFalse(MapHeadquartersPreviewSelection.TryQueue(
                new MapBusinessLocation("TUR", "not_a_province")));
            Assert.IsFalse(MapHeadquartersPreviewSelection.TryQueue(
                new MapBusinessLocation("USA", "ne-q60")));
            Assert.IsTrue(MapHeadquartersPreviewSelection.TryConsume(out var picked));
            Assert.AreEqual(ankara, picked);
        }

        [Test]
        public void PreviewComponentCreatesLocaleReadyDropdownsWithoutCreatingCompany()
        {
            var canvas = new GameObject("CompanyPreviewHarness",
                typeof(RectTransform), typeof(Canvas));
            var panel = new GameObject("Preview", typeof(RectTransform),
                typeof(CompanyHeadquartersPreview));
            panel.transform.SetParent(canvas.transform, false);
            try
            {
                var preview = panel.GetComponent<CompanyHeadquartersPreview>();
                preview.Build(ShellWidgets.Builtin());
                preview.Build(ShellWidgets.Builtin());
                Assert.IsTrue(preview.Built);
                Assert.Greater(preview.CountryCount, 35);
                Assert.Greater(preview.CityCount, 0);
                Assert.IsTrue(preview.SelectedLocation.IsValid);
                var dropdowns = panel.GetComponentsInChildren<Dropdown>(true);
                Assert.AreEqual(2, dropdowns.Length);
                Assert.IsFalse(MapHeadquartersPreviewSelection.HasPending);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
