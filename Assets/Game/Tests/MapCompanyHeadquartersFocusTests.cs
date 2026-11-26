using LogistiCore.Backend;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    /// <summary>
    /// CITY-START-04 contracts, saved for the final Unity validation pass.
    /// These tests do not use an authenticated Firebase emulator.
    /// </summary>
    public sealed class MapCompanyHeadquartersFocusTests
    {
        static ClientCompanyHeadquartersState State(
            bool company, bool headquarters, string companyId = "",
            string country = "", string city = "",
            string catalog = "lc_tur_europe_2026_11_v1")
        {
            return new ClientCompanyHeadquartersState(
                company, headquarters, companyId, country, city, catalog);
        }

        [Test]
        public void NoCompanyAndLegacyCompanyDoNotCreateFakeHeadquarters()
        {
            var result = MapCompanyHeadquartersFocusPolicy.Resolve(
                State(false, false), out var missing, out var point);
            Assert.AreEqual(MapHeadquartersFocusState.NoCompany, result);
            Assert.IsFalse(missing.Location.IsValid);
            Assert.AreEqual(Vector2.zero, point);

            result = MapCompanyHeadquartersFocusPolicy.Resolve(
                State(true, false, "co_legacy"), out missing, out point);
            Assert.AreEqual(MapHeadquartersFocusState.LegacyUnassigned, result);
            Assert.IsFalse(missing.Location.IsValid);
            Assert.AreEqual(Vector2.zero, point);
        }

        [Test]
        public void VerifiedTurkishHeadquartersTargetsExactCatalogCoordinates()
        {
            var state = State(true, true, "co_synthetic", "TUR", "ankara");
            var result = MapCompanyHeadquartersFocusPolicy.Resolve(
                state, out var city, out var point);
            Assert.AreEqual(MapHeadquartersFocusState.Focused, result);
            Assert.AreEqual(new MapBusinessLocation("TUR", "ankara"), city.Location);
            Assert.IsTrue(MapProjection.TryProject(city.Latitude, city.Longitude, out var expected));
            Assert.AreEqual(expected.x, point.x, 0.00001f);
            Assert.AreEqual(expected.y, point.y, 0.00001f);
        }

        [Test]
        public void EuropeanVerifiedHeadquartersCanUseExistingEuropeCamera()
        {
            MapGlobalCity berlin = default;
            foreach (var city in MapGlobalCityCatalog.All)
            {
                if (city.Location.CountryCode == "DEU" && city.DisplayName == "Berlin")
                {
                    berlin = city;
                    break;
                }
            }
            Assert.IsTrue(berlin.Location.IsValid);
            var result = MapCompanyHeadquartersFocusPolicy.Resolve(
                State(true, true, "co_synthetic", berlin.Location.CountryCode,
                    berlin.Location.CityKey), out var found, out _);
            Assert.AreEqual(MapHeadquartersFocusState.Focused, result);
            Assert.AreEqual(berlin.Location, found.Location);
        }

        [Test]
        public void InvalidOrOutOfRegionServerLocationNeverMovesMap()
        {
            foreach (var invalid in new[]
            {
                State(true, true, "", "TUR", "ankara"),
                State(true, true, "co_syn", "TUR", "not-a-city"),
                State(true, true, "co_syn", "USA", "ne-q60"),
                State(true, true, "co_syn", "TUR", "ankara", "other_catalog"),
                State(true, true, "co_syn", "ZZZ", "invented_city")
            })
            {
                var result = MapCompanyHeadquartersFocusPolicy.Resolve(
                    invalid, out var city, out var point);
                Assert.AreEqual(MapHeadquartersFocusState.InvalidHeadquarters, result);
                Assert.IsFalse(city.Location.IsValid);
                Assert.AreEqual(Vector2.zero, point);
            }
        }

        [Test]
        public void ExplicitUserNavigationSuppressesUnverifiedAutoFocusWithoutNetwork()
        {
            var go = new GameObject("HqFocusTest",
                typeof(Camera), typeof(MapCameraRig), typeof(MapCompanyHeadquartersFocus));
            try
            {
                var focus = go.GetComponent<MapCompanyHeadquartersFocus>();
                Assert.IsFalse(focus.HasRequestedRead);
                Assert.AreEqual(MapHeadquartersFocusState.AwaitingVerifiedAccount, focus.State);
                focus.SuppressNavigation();
                Assert.AreEqual(MapHeadquartersFocusState.UserNavigated, focus.State);
                Assert.IsFalse(focus.HasRequestedRead);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void HeadquartersReadIsNotAPayableCommandOrClientMutationAuthorization()
        {
            Assert.IsTrue(ClientCallableCatalog.IsRead(ClientCallableCatalog.GetMyCompanyHeadquarters));
            Assert.IsFalse(ClientCallableCatalog.IsCommand(ClientCallableCatalog.GetMyCompanyHeadquarters));
            Assert.IsFalse(ClientCallableCatalog.IsSupportedPlayerCommand(
                ClientCallableCatalog.CreateCompanyWithHeadquarters));
        }
    }
}
