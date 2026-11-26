using System.Collections.Generic;
using LogistiCore.Backend;
using NUnit.Framework;

namespace LogistiCore.Tests
{
    /// <summary>Saved for the agreed final Unity regression milestone.</summary>
    public sealed class ClientCompanyHeadquartersTests
    {
        [Test]
        public void ReadCatalogRecognizesHqWithoutEnablingUnreviewedClientMutation()
        {
            Assert.IsTrue(ClientCallableCatalog.IsRead(ClientCallableCatalog.GetMyCompanyHeadquarters));
            Assert.IsTrue(ClientCallableCatalog.IsCommand(ClientCallableCatalog.CreateCompanyWithHeadquarters));
            // Client runtime still needs explicit company-auth gates and confirmation UI.
            Assert.IsFalse(ClientCallableCatalog.IsSupportedPlayerCommand(
                ClientCallableCatalog.CreateCompanyWithHeadquarters));
        }

        [Test]
        public void LegacyCompanyWithNoHeadquartersRemainsRepresentable()
        {
            var result = new Dictionary<string, object>
            {
                { "hasCompany", true },
                { "hasHeadquarters", false }
            };
            Assert.IsTrue(ClientReadParser.TryCompanyHeadquarters(result, out var parsed));
            Assert.IsTrue(parsed.HasCompany);
            Assert.IsFalse(parsed.HasHeadquarters);
            Assert.AreEqual(string.Empty, parsed.CityKey);
            Assert.IsTrue(ClientReadParser.TryCompanyHeadquarters(
                new Dictionary<string, object> { { "hasCompany", false }, { "hasHeadquarters", false } },
                out parsed));
            Assert.IsFalse(parsed.HasCompany);
            Assert.IsFalse(parsed.HasHeadquarters);
        }

        [Test]
        public void ValidatedHeadquartersCanBeDisplayedOnlyWithCountryScopedId()
        {
            var headquarters = new Dictionary<string, object>
            {
                { "countryCode", "TUR" }, { "cityKey", "ankara" },
                { "catalogVersion", "lc_tur_europe_2026_11_v1" }
            };
            var payload = new Dictionary<string, object>
            {
                { "hasCompany", true }, { "hasHeadquarters", true },
                { "companyId", "co_synthetic" }, { "headquarters", headquarters }
            };
            Assert.IsTrue(ClientReadParser.TryCompanyHeadquarters(payload, out var parsed));
            Assert.IsTrue(parsed.HasHeadquarters);
            Assert.AreEqual("TUR", parsed.CountryCode);
            Assert.AreEqual("ankara", parsed.CityKey);
            Assert.AreEqual("co_synthetic", parsed.CompanyId);

            headquarters["cityKey"] = "../ankara";
            Assert.IsFalse(ClientReadParser.TryCompanyHeadquarters(payload, out _));
            headquarters["cityKey"] = "ankara";
            headquarters["catalogVersion"] = "unverified";
            Assert.IsFalse(ClientReadParser.TryCompanyHeadquarters(payload, out _));
            headquarters["catalogVersion"] = "lc_tur_europe_2026_11_v1";
            headquarters["ownerUid"] = "forged";
            Assert.IsFalse(ClientReadParser.TryCompanyHeadquarters(payload, out _));
            Assert.IsFalse(ClientReadParser.TryCompanyHeadquarters(
                new Dictionary<string, object> { { "hasCompany", false }, { "hasHeadquarters", true } },
                out _));
        }
    }
}
