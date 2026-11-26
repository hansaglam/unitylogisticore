using System.Collections.Generic;
using LogistiCore.Backend;
using LogistiCore.UI;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    /// <summary>
    /// Deferred START-DEPOT-01 client read contracts. Never contact live backend.
    /// </summary>
    public sealed class ClientStarterDepotSiteTests
    {
        static Dictionary<string, object> ReservedSite()
        {
            return new Dictionary<string, object>
            {
                { "hasCompany", true }, { "hasHeadquarters", true },
                { "hasStarterSite", true }, { "companyId", "co_synthetic" },
                { "starterSite", new Dictionary<string, object>
                    {
                        { "countryCode", "TUR" }, { "cityKey", "ankara" },
                        { "catalogVersion", "lc_tur_europe_2026_11_v1" },
                        { "status", "site_reserved" },
                        { "dispatchStatus", "unavailable_pending_verified_routes" }
                    }
                }
            };
        }

        [Test]
        public void StarterSiteIsReadOnlyAndNeverDispatchReady()
        {
            Assert.IsTrue(ClientCallableCatalog.IsRead(ClientCallableCatalog.GetMyStarterDepotSite));
            Assert.IsFalse(ClientCallableCatalog.IsCommand(ClientCallableCatalog.GetMyStarterDepotSite));
            Assert.IsTrue(ClientReadParser.TryStarterDepotSite(ReservedSite(), out var state));
            Assert.IsTrue(state.HasCompany);
            Assert.IsTrue(state.HasHeadquarters);
            Assert.IsTrue(state.HasStarterSite);
            Assert.AreEqual("TUR", state.CountryCode);
            Assert.AreEqual("ankara", state.CityKey);
            Assert.AreEqual("co_synthetic", state.CompanyId);
            Assert.IsFalse(state.DispatchReady);
        }

        [Test]
        public void MissingOrLegacyRecordsAreNotInvented()
        {
            foreach (var values in new []
            {
                new { company = false, hq = false },
                new { company = true, hq = false },
                new { company = true, hq = true },
            })
            {
                var data = new Dictionary<string, object>
                {
                    { "hasCompany", values.company },
                    { "hasHeadquarters", values.hq },
                    { "hasStarterSite", false },
                };
                Assert.IsTrue(ClientReadParser.TryStarterDepotSite(data, out var state));
                Assert.IsFalse(state.HasStarterSite);
                Assert.IsFalse(state.DispatchReady);
                Assert.AreEqual(string.Empty, state.CityKey);
            }
        }

        [Test]
        public void ForgedPermissionsInvalidFieldsAndUnsupportedCatalogAreRejected()
        {
            var data = ReservedSite();
            var site = (Dictionary<string, object>)data["starterSite"];
            foreach (var pair in new[]
            {
                new KeyValuePair<string, object>("status", "operational"),
                new KeyValuePair<string, object>("dispatchStatus", "ready"),
                new KeyValuePair<string, object>("catalogVersion", "unsupported"),
                new KeyValuePair<string, object>("cityKey", "../ankara"),
                new KeyValuePair<string, object>("countryCode", "us"),
            })
            {
                var old = site[pair.Key];
                site[pair.Key] = pair.Value;
                Assert.IsFalse(ClientReadParser.TryStarterDepotSite(data, out _), pair.Key);
                site[pair.Key] = old;
            }
            site["balance"] = 999999;
            Assert.IsFalse(ClientReadParser.TryStarterDepotSite(data, out _));
            Assert.IsFalse(ClientReadParser.TryStarterDepotSite(
                new Dictionary<string, object> { { "hasCompany", false },
                    { "hasHeadquarters", false }, { "hasStarterSite", true } }, out _));
        }

        [Test]
        public void CompanyPreviewIncludesSiteStatusWithoutGrantingItOffline()
        {
            var canvas = new GameObject("StarterDepotTest", typeof(RectTransform), typeof(Canvas));
            var previewRoot = new GameObject("HeadquartersPreview",
                typeof(RectTransform), typeof(CompanyHeadquartersPreview));
            previewRoot.transform.SetParent(canvas.transform, false);
            try
            {
                previewRoot.GetComponent<CompanyHeadquartersPreview>().Build(ShellWidgets.Builtin());
                var site = previewRoot.GetComponentInChildren<CompanyStarterSiteStatus>(true);
                Assert.IsNotNull(site);
                Assert.IsFalse(site.IsVerified);
                Assert.IsFalse(site.HasStarterSite);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
