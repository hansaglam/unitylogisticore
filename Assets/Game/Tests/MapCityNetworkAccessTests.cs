using LogistiCore.Map;
using NUnit.Framework;

namespace LogistiCore.Tests
{
    public sealed class MapCityNetworkAccessTests
    {
        static readonly MapBusinessLocation Ankara = new MapBusinessLocation("tur", "Ankara");
        static readonly MapBusinessLocation Istanbul = new MapBusinessLocation("TUR", "istanbul");
        static readonly MapBusinessLocation Berlin = new MapBusinessLocation("DEU", "berlin");
        static readonly MapBusinessLocation BerlinOther = new MapBusinessLocation("USA", "berlin");

        [Test]
        public void CityIdentityIsStableAndCountryScoped()
        {
            Assert.IsTrue(Ankara.IsValid);
            Assert.AreEqual("TUR", Ankara.CountryCode);
            Assert.AreEqual("ankara", Ankara.CityKey);
            Assert.AreEqual(new MapBusinessLocation("tur", "ankara"), Ankara);
            Assert.AreNotEqual(Berlin, BerlinOther);
            Assert.IsFalse(new MapBusinessLocation("", "berlin").IsValid);
            Assert.IsFalse(new MapBusinessLocation("DEU", "").IsValid);
        }

        [Test]
        public void WithoutTrustedSnapshotNoLocationIsClaimedOpen()
        {
            var candidate = new MapCityNetworkSnapshot(false, Ankara,
                new[] { Berlin }, new[] { Berlin }, new[] { Berlin }, new[] { "DEU" });
            var view = MapCityNetworkAccess.Evaluate(Berlin, candidate);
            Assert.AreEqual(MapCityNetworkStage.Unverified, view.Stage);
            Assert.IsFalse(view.HasVerifiedDeliveryAccess);
            Assert.IsFalse(view.CanRequestBranchExpansion);
            Assert.IsFalse(view.HasOwnedFacility);
            Assert.IsFalse(view.HasApprovedCountryAccess);
            Assert.AreEqual(MapCityNetworkStage.Unverified,
                MapCityNetworkAccess.Evaluate(Berlin, null).Stage);
        }

        [Test]
        public void ADeliveryDoesNotGrantBranchOwnershipOrExpansion()
        {
            var snapshot = new MapCityNetworkSnapshot(true, Ankara,
                new[] { Istanbul }, null, new[] { Ankara }, null);
            var view = MapCityNetworkAccess.Evaluate(Istanbul, snapshot);
            Assert.AreEqual(MapCityNetworkStage.Delivery, view.Stage);
            Assert.IsTrue(view.HasVerifiedDeliveryAccess);
            Assert.IsFalse(view.CanRequestBranchExpansion);
            Assert.IsFalse(view.HasOwnedFacility);
            Assert.IsTrue(view.HasApprovedCountryAccess);
        }

        [Test]
        public void ExpansionOpportunityDoesNotFabricateDeliveryContract()
        {
            var snapshot = new MapCityNetworkSnapshot(true, Ankara,
                null, new[] { Berlin }, new[] { Ankara }, null);
            var view = MapCityNetworkAccess.Evaluate(Berlin, snapshot);
            Assert.AreEqual(MapCityNetworkStage.BranchOpportunity, view.Stage);
            Assert.IsFalse(view.HasVerifiedDeliveryAccess);
            Assert.IsTrue(view.CanRequestBranchExpansion);
            Assert.IsFalse(view.HasOwnedFacility);
            Assert.IsFalse(view.HasApprovedCountryAccess);
        }

        [Test]
        public void OwnedFacilityDoesNotFabricateInternationalDelivery()
        {
            var snapshot = new MapCityNetworkSnapshot(true, Ankara,
                null, new[] { Berlin }, new[] { Berlin }, new[] { "DEU" });
            var view = MapCityNetworkAccess.Evaluate(Berlin, snapshot);
            Assert.AreEqual(MapCityNetworkStage.OwnedFacility, view.Stage);
            Assert.IsTrue(view.HasOwnedFacility);
            Assert.IsFalse(view.HasVerifiedDeliveryAccess);
            Assert.IsFalse(view.CanRequestBranchExpansion);
            Assert.IsTrue(view.HasApprovedCountryAccess);
        }

        [Test]
        public void ForeignCountryAccessDoesNotAutomaticallyEnableRoutesOrBranches()
        {
            var snapshot = new MapCityNetworkSnapshot(true, Ankara,
                null, null, new[] { Ankara }, new[] { "DEU" });
            var view = MapCityNetworkAccess.Evaluate(Berlin, snapshot);
            Assert.AreEqual(MapCityNetworkStage.Explore, view.Stage);
            Assert.IsTrue(view.HasApprovedCountryAccess);
            Assert.IsFalse(view.HasVerifiedDeliveryAccess);
            Assert.IsFalse(view.CanRequestBranchExpansion);
            Assert.IsFalse(view.HasOwnedFacility);
        }

        [Test]
        public void TrustedHeadquartersDoesNotClaimFacilityWithoutWarehouse()
        {
            var snapshot = new MapCityNetworkSnapshot(true, Ankara,
                null, null, null, null);
            var view = MapCityNetworkAccess.Evaluate(Ankara, snapshot);
            Assert.IsTrue(view.IsHeadquarters);
            Assert.IsFalse(view.HasOwnedFacility);
            Assert.AreEqual(MapCityNetworkStage.Explore, view.Stage);
        }

        [Test]
        public void SnapshotCopiesSourceCollectionsToAvoidCallerMutation()
        {
            var destinations = new[] { Istanbul };
            var countries = new[] { "DEU" };
            var snapshot = new MapCityNetworkSnapshot(true, Ankara,
                destinations, null, null, countries);
            destinations[0] = Berlin;
            countries[0] = "USA";
            Assert.IsTrue(MapCityNetworkAccess.Evaluate(Istanbul, snapshot).HasVerifiedDeliveryAccess);
            Assert.IsFalse(MapCityNetworkAccess.Evaluate(Berlin, snapshot).HasVerifiedDeliveryAccess);
            Assert.IsTrue(MapCityNetworkAccess.Evaluate(Berlin, snapshot).HasApprovedCountryAccess);
            Assert.IsFalse(MapCityNetworkAccess.Evaluate(BerlinOther, snapshot).HasApprovedCountryAccess);
        }
    }
}
