using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    /// <summary>Contracts for the worldwide city art fallback; run with the final Unity regression pass.</summary>
    public sealed class MapWorldCityAtmosphereTests
    {
        [Test]
        public void ExistingTurkeyCityKeepsCuratedIdentity()
        {
            foreach (var city in MapCityAtmosphereCatalog.All)
            {
                Assert.IsTrue(MapWorldCityAtmosphereResolver.TryResolve(city.CityId, "TUR", out var resolved));
                Assert.AreEqual(city.Landmark, resolved.Landmark, city.CityId);
                Assert.AreEqual(city.Horizon, resolved.Horizon, city.CityId);
                Assert.AreEqual(city.SkyLow, resolved.SkyLow, city.CityId);
            }
        }

        [TestCase("berlin", "DEU", MapCityLandmark.Tower)]
        [TestCase("london", "GBR", MapCityLandmark.ClockTower)]
        [TestCase("paris", "FRA", MapCityLandmark.LatticeTower)]
        [TestCase("tokyo", "JPN", MapCityLandmark.NeedleTower)]
        [TestCase("sydney", "AUS", MapCityLandmark.SailRoof)]
        [TestCase("new_york", "USA", MapCityLandmark.Skyscrapers)]
        [TestCase("dubai", "ARE", MapCityLandmark.NeedleTower)]
        [TestCase("cairo", "EGY", MapCityLandmark.Pyramids)]
        [TestCase("singapore", "SGP", MapCityLandmark.Skyscrapers)]
        public void MajorCitiesGetOnlyAnExplicitlyCuratedLandmark(
            string cityId, string country, MapCityLandmark landmark)
        {
            Assert.IsTrue(MapWorldCityAtmosphereResolver.TryResolve(cityId, country, out var resolved));
            Assert.AreEqual(landmark, resolved.Landmark);
        }

        [TestCase("helsinki", "FIN")]
        [TestCase("cordoba", "ARG")]
        [TestCase("jaipur", "IND")]
        [TestCase("random_city_563", "ZAF")]
        public void UncuratedCityHasGenericButDeterministicArtwork(string cityId, string country)
        {
            Assert.IsTrue(MapWorldCityAtmosphereResolver.TryResolve(cityId, country, out var first));
            Assert.IsTrue(MapWorldCityAtmosphereResolver.TryResolve(cityId, country, out var second));
            Assert.AreEqual(MapCityLandmark.None, first.Landmark);
            Assert.AreEqual(MapCityHorizon.Urban, first.Horizon);
            Assert.AreEqual(first.SkylineSeed, second.SkylineSeed);
            Assert.AreEqual(first.SkyLow, second.SkyLow);
        }

        [Test]
        public void CountryAndCityIdsChooseIndependentStableStyles()
        {
            Assert.IsTrue(MapWorldCityAtmosphereResolver.TryResolve("springfield", "USA", out var unitedStates));
            Assert.IsTrue(MapWorldCityAtmosphereResolver.TryResolve("springfield", "AUS", out var australia));
            Assert.AreNotEqual(unitedStates.SkylineSeed, australia.SkylineSeed);
            Assert.AreNotEqual(unitedStates.SkyLow, australia.SkyLow);
            Assert.AreEqual(MapWorldCityAtmosphereResolver.StableSeed("DEU|berlin"),
                MapWorldCityAtmosphereResolver.StableSeed("DEU|berlin"));
        }

        [Test]
        public void MissingCityIdDoesNotCreateFabricatedArtwork()
        {
            Assert.IsFalse(MapWorldCityAtmosphereResolver.TryResolve("", "USA", out _));
            Assert.IsFalse(MapWorldCityAtmosphereResolver.TryResolve(null, "USA", out _));
            Assert.AreSame(MapPremiumCityArt.Banner, MapPremiumCityArt.ForWorldCity(null, "USA"));
        }

        [Test]
        public void GlobalArtworkIsCachedAndDoesNotChangeOriginalCityBannerApi()
        {
            var berlin = MapPremiumCityArt.ForWorldCity("berlin", "DEU");
            Assert.IsNotNull(berlin);
            Assert.AreSame(berlin, MapPremiumCityArt.ForWorldCity("berlin", "DEU"));
            Assert.AreEqual(320, berlin.texture.width);
            Assert.AreEqual(128, berlin.texture.height);
            Assert.AreSame(MapPremiumCityArt.ForCity("ankara"),
                MapPremiumCityArt.ForWorldCity("ankara", "TUR"));
            Assert.IsNotNull(MapPremiumCityArt.Banner);
        }
    }
}
