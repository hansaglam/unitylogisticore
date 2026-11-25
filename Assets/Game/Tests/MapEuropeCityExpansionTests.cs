using System;
using System.Collections.Generic;
using LogistiCore.Map;
using NUnit.Framework;

namespace LogistiCore.Tests
{
    /// <summary>
    /// MAP-06 regression contracts. Intentionally saved for the final Unity validation pass.
    /// </summary>
    public sealed class MapEuropeCityExpansionTests
    {
        [Test]
        public void TurkeyEuropeLaunchHasBroaderEuropeanCoverage()
        {
            var regional = 0;
            var turkey = 0;
            var european = 0;
            var byCountry = new Dictionary<string, int>(StringComparer.Ordinal);
            var allLocations = new HashSet<MapBusinessLocation>();
            foreach (var city in MapGlobalCityCatalog.All)
            {
                Assert.IsTrue(city.Location.IsValid, city.DisplayName);
                Assert.IsTrue(allLocations.Add(city.Location), city.DisplayName);
                if (!MapTradeRegionCatalog.Includes(city))
                    continue;
                regional++;
                if (city.Location.CountryCode == "TUR")
                    turkey++;
                else
                {
                    european++;
                    if (!byCountry.ContainsKey(city.Location.CountryCode))
                        byCountry[city.Location.CountryCode] = 0;
                    byCountry[city.Location.CountryCode]++;
                }
            }

            Assert.AreEqual(81, turkey);
            Assert.GreaterOrEqual(european, 160);
            Assert.GreaterOrEqual(regional, 240);
            Assert.GreaterOrEqual(byCountry.Count, 40);
            Assert.GreaterOrEqual(byCountry["DEU"], 4);
            Assert.GreaterOrEqual(byCountry["FRA"], 20);
            Assert.GreaterOrEqual(byCountry["ITA"], 20);
        }

        [Test]
        public void OriginalTurkeyMarkersStayOnTheirExactLegacyCoordinates()
        {
            foreach (var previous in MapCities.All)
            {
                Assert.IsTrue(MapGlobalCityCatalog.TryGet(
                    new MapBusinessLocation("TUR", previous.Id), out var retained));
                Assert.AreEqual(previous.Latitude, retained.Latitude, 0.0000001d);
                Assert.AreEqual(previous.Longitude, retained.Longitude, 0.0000001d);
            }
        }

        [Test]
        public void EuropeanCitiesDoNotAutomaticallyGainDeliveryOrBranchRights()
        {
            var berlin = default(MapBusinessLocation);
            foreach (var city in MapGlobalCityCatalog.All)
            {
                if (city.Location.CountryCode == "DEU" &&
                    string.Equals(city.DisplayName, "Berlin", StringComparison.Ordinal))
                {
                    berlin = city.Location;
                    break;
                }
            }

            Assert.IsTrue(berlin.IsValid);
            Assert.IsTrue(MapTradeRegionCatalog.Includes(berlin));
            var withoutBackendAccess = MapCityNetworkAccess.Evaluate(berlin, null);
            Assert.AreEqual(MapCityNetworkStage.Unverified, withoutBackendAccess.Stage);
            Assert.IsFalse(withoutBackendAccess.HasVerifiedDeliveryAccess);
            Assert.IsFalse(withoutBackendAccess.HasOwnedFacility);
        }
    }
}
