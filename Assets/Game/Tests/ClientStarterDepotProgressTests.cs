using System.Collections.Generic;
using LogistiCore.Backend;
using LogistiCore.UI;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    /// <summary>
    /// Deferred EditMode tests. The front end never grants itself depot capacity
    /// or European road access; only authenticated backend stages are displayed.
    /// </summary>
    public sealed class ClientStarterDepotProgressTests
    {
        static Dictionary<string, object> Base(string stage, bool company,
            bool headquarters, bool site, bool economy, bool fleet, bool warehouse)
        {
            var data = new Dictionary<string, object>
            {
                { "hasCompany", company }, { "hasHeadquarters", headquarters },
                { "hasStarterSite", site }, { "economyReady", economy },
                { "starterFleetReady", fleet }, { "warehouseReady", warehouse },
                { "dispatchReady", false }, { "stage", stage }
            };
            if (headquarters)
                data["headquarters"] = new Dictionary<string, object>
                {
                    { "countryCode", "TUR" }, { "cityKey", "ankara" }
                };
            if (warehouse)
                data["warehouse"] = new Dictionary<string, object>
                {
                    { "warehouseId", new string('a', 64) },
                    { "capacityModelId", "pallet_slots_v1" },
                    { "capacityUnits", 24 }
                };
            return data;
        }

        [Test]
        public void SetupStagesAreStrictAndDoNotInventReadyCompany()
        {
            var stages = new[]
            {
                Base("no_company", false, false, false, false, false, false),
                Base("legacy_hq_missing", true, false, false, false, false, false),
                Base("starter_site_missing", true, true, false, false, false, false),
                Base("awaiting_economy", true, true, true, false, false, false),
                Base("unsupported_starter_region", true, true, true, true, false, false),
                Base("awaiting_starter_fleet", true, true, true, true, false, false),
                Base("awaiting_warehouse_catalog", true, true, true, true, true, false),
                Base("ready_to_activate", true, true, true, true, true, false),
                Base("warehouse_operational_dispatch_pending_routes",
                    true, true, true, true, true, true)
            };

            foreach (var row in stages)
            {
                Assert.IsTrue(ClientReadParser.TryStarterDepotProgress(row, out var state),
                    (string)row["stage"]);
                Assert.IsFalse(state.DispatchReady, (string)row["stage"]);
                Assert.AreEqual((bool)row["warehouseReady"], state.WarehouseReady);
                Assert.AreEqual((string)row["stage"] == "ready_to_activate",
                    state.CanRequestActivation);
            }
        }

        [Test]
        public void OperationalWarehouseIsStorageOnlyWithBoundedCapacity()
        {
            var data = Base("warehouse_operational_dispatch_pending_routes",
                true, true, true, true, true, true);
            Assert.IsTrue(ClientReadParser.TryStarterDepotProgress(data, out var parsed));
            Assert.AreEqual("TUR", parsed.CountryCode);
            Assert.AreEqual("ankara", parsed.CityKey);
            Assert.AreEqual("pallet_slots_v1", parsed.CapacityModelId);
            Assert.AreEqual(24, parsed.CapacityUnits);
            Assert.IsFalse(parsed.DispatchReady);

            data["dispatchReady"] = true;
            Assert.IsFalse(ClientReadParser.TryStarterDepotProgress(data, out _));
            data["dispatchReady"] = false;
            var warehouse = (Dictionary<string, object>)data["warehouse"];
            warehouse["capacityUnits"] = 2400;
            Assert.IsFalse(ClientReadParser.TryStarterDepotProgress(data, out _));
            warehouse["capacityUnits"] = 24;
            warehouse["warehouseId"] = "fake";
            Assert.IsFalse(ClientReadParser.TryStarterDepotProgress(data, out _));
        }

        [Test]
        public void InvalidCrossStatePermissionsAreRejected()
        {
            var ready = Base("ready_to_activate", true, true, true, true, true, false);
            ready["warehouseReady"] = true;
            Assert.IsFalse(ClientReadParser.TryStarterDepotProgress(ready, out _));
            ready["warehouseReady"] = false;
            ready["unsupportedMoneyGrant"] = 999999;
            Assert.IsFalse(ClientReadParser.TryStarterDepotProgress(ready, out _));
            ready.Remove("unsupportedMoneyGrant");
            var location = (Dictionary<string, object>)ready["headquarters"];
            location["cityKey"] = "../ankara";
            Assert.IsFalse(ClientReadParser.TryStarterDepotProgress(ready, out _));
            location["cityKey"] = "ankara";
            location["ownerUid"] = "other";
            Assert.IsFalse(ClientReadParser.TryStarterDepotProgress(ready, out _));
        }

        [Test]
        public void PublicGameplayDispatchStillCannotActivateTheDepot()
        {
            Assert.IsTrue(ClientCallableCatalog.IsRead(
                ClientCallableCatalog.GetMyStarterDepotProgress));
            Assert.IsFalse(ClientCallableCatalog.IsCommand(
                ClientCallableCatalog.GetMyStarterDepotProgress));
            Assert.IsTrue(ClientCallableCatalog.IsCommand(
                ClientCallableCatalog.ActivateHeadquartersStarterDepot));
            Assert.IsFalse(ClientCallableCatalog.IsSupportedPlayerCommand(
                ClientCallableCatalog.ActivateHeadquartersStarterDepot));
        }

        [Test]
        public void OfflineStatusPanelDoesNotFabricateWarehouseOrFleet()
        {
            var root = new GameObject("StarterDepotUI", typeof(RectTransform),
                typeof(Canvas));
            var site = new GameObject("CompanyStarterSiteStatus",
                typeof(RectTransform), typeof(CompanyStarterSiteStatus));
            site.transform.SetParent(root.transform, false);
            try
            {
                var panel = site.GetComponent<CompanyStarterSiteStatus>();
                panel.Build(ShellWidgets.Builtin());
                Assert.IsFalse(panel.IsVerified);
                Assert.IsFalse(panel.HasStarterSite);
                Assert.IsFalse(panel.WarehouseReady);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
