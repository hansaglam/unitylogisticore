using LogistiCore.Backend;
using LogistiCore.Depot;
using LogistiCore.Map;
using NUnit.Framework;

namespace LogistiCore.Tests
{
    public sealed class DepotPrototypeNavigationTests
    {
        static readonly MapBusinessLocation Ankara =
            new MapBusinessLocation("TUR", "ankara");
        static readonly MapBusinessLocation Istanbul =
            new MapBusinessLocation("TUR", "istanbul");

        static ClientStarterDepotProgressState Progress(bool company = true,
            bool headquarters = true, bool site = true)
        {
            return new ClientStarterDepotProgressState(
                company, headquarters, site, false, false, false,
                "awaiting_economy", headquarters ? "TUR" : "",
                headquarters ? "ankara" : "", "", "", 0);
        }

        [Test]
        public void OnlyFreshLocalVerifiedHeadquartersSiteCanOpenPrototype()
        {
            Assert.IsTrue(DepotPrototypeNavigation.CanInspect(
                true, true, Progress(), Ankara, true));
            Assert.IsFalse(DepotPrototypeNavigation.CanInspect(
                false, true, Progress(), Ankara, true));
            Assert.IsFalse(DepotPrototypeNavigation.CanInspect(
                true, false, Progress(), Ankara, true));
            Assert.IsFalse(DepotPrototypeNavigation.CanInspect(
                true, true, Progress(), Ankara, false));
            Assert.IsFalse(DepotPrototypeNavigation.CanInspect(
                true, true, Progress(), Istanbul, true));
        }

        [Test]
        public void MissingCompanyHeadquartersOrSiteAlwaysFailsClosed()
        {
            Assert.IsFalse(DepotPrototypeNavigation.CanInspect(
                true, true, Progress(company:false), Ankara, true));
            Assert.IsFalse(DepotPrototypeNavigation.CanInspect(
                true, true, Progress(headquarters:false), Ankara, true));
            Assert.IsFalse(DepotPrototypeNavigation.CanInspect(
                true, true, Progress(site:false), Ankara, true));
            Assert.IsFalse(DepotPrototypeNavigation.CanInspect(
                true, true, Progress(), new MapBusinessLocation("TUR", "unknown"), true));
        }
    }
}
