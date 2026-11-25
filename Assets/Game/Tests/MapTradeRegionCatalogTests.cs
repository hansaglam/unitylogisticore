using System.Collections.Generic;
using LogistiCore.Map;
using NUnit.Framework;

namespace LogistiCore.Tests
{
    /// <summary>Deferred contracts for the final Unity validation milestone.</summary>
    public sealed class MapTradeRegionCatalogTests
    {
        [Test]
        public void ProvinceCatalogContainsExactly81TurkeyCentersWithoutDuplicateIdentities()
        {
            var turkey = new HashSet<MapBusinessLocation>();
            foreach (var city in MapGlobalCityCatalog.All)
                if (city.Location.CountryCode == "TUR")
                    Assert.IsTrue(turkey.Add(city.Location), city.DisplayName);
            Assert.AreEqual(81, turkey.Count);
            foreach (var original in MapCities.All)
                Assert.IsTrue(turkey.Contains(new MapBusinessLocation("TUR", original.Id)), original.Id);
        }

        [Test]
        public void InitialOperatingMapIncludesEuropeAndTurkeyOnly()
        {
            Assert.Greater(MapTradeRegionCatalog.Count, 110);
            Assert.IsTrue(MapTradeRegionCatalog.Includes(new MapBusinessLocation("TUR", "ankara")));
            Assert.IsTrue(MapTradeRegionCatalog.Includes(new MapBusinessLocation("TUR", "edirne")));
            Assert.IsFalse(MapTradeRegionCatalog.Includes(new MapBusinessLocation("IND", "ne-q5838")));
            var germany = false;
            var france = false;
            var turkey = 0;
            foreach (var city in MapGlobalCityCatalog.All)
            {
                if (!MapTradeRegionCatalog.Includes(city))
                    continue;
                if (city.Location.CountryCode == "TUR") turkey++;
                if (city.Location.CountryCode == "DEU") germany = true;
                if (city.Location.CountryCode == "FRA") france = true;
            }

            Assert.AreEqual(81, turkey);
            Assert.IsTrue(germany);
            Assert.IsTrue(france);
        }

        [Test]
        public void DisplaySlugIsNotUsedAsCountryScopedPrimaryKey()
        {
            foreach (var city in MapGlobalCityCatalog.All)
            {
                if (!MapTradeRegionCatalog.Includes(city))
                    continue;
                var artKey = MapTradeRegionCatalog.ArtworkKey(city);
                Assert.IsFalse(string.IsNullOrEmpty(artKey), city.DisplayName);
                Assert.IsTrue(city.Location.IsValid);
            }
            Assert.AreEqual("new_york", MapTradeRegionCatalog.ArtworkKey(
                new MapGlobalCity("USA", "test-code", "New York", 40.0d, -74.0d, 1)));
        }

        [Test]
        public void VisibleLocationNeverInventsBusinessPermission()
        {
            var city = new MapBusinessLocation("DEU", "ne-q64");
            // The map catalog being visible and a nullable trusted game state
            // are separate concepts. Without server evidence no city is unlocked.
            var access = MapCityNetworkAccess.Evaluate(city, null);
            Assert.AreEqual(MapCityNetworkStage.Unverified, access.Stage);
            Assert.IsFalse(access.HasVerifiedDeliveryAccess);
            Assert.IsFalse(access.HasOwnedFacility);
        }
    }
}
