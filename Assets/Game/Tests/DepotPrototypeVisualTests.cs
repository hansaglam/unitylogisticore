using LogistiCore.Backend;
using LogistiCore.Depot;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    /// <summary>
    /// Deferred visual-blockout tests. No mocked company owns a real warehouse.
    /// </summary>
    public sealed class DepotPrototypeVisualTests
    {
        static ClientStarterDepotProgressState State(
            bool company, bool hq, bool site, bool warehouse, string stage,
            int units = 0, string model = "")
        {
            return new ClientStarterDepotProgressState(
                company, hq, site, warehouse, warehouse, warehouse,
                stage, hq ? "TUR" : "", hq ? "ankara" : "",
                warehouse ? new string('a',64) : "", model, units);
        }

        [Test]
        public void UnverifiedAndLegacyPlayersNeverReceiveAnOwnedVisualBuilding()
        {
            Assert.AreEqual(DepotVisualStage.Unverified,
                DepotVisualStagePolicy.Resolve(false,
                    State(true, true, true, true,
                        "warehouse_operational_dispatch_pending_routes",24,"pallet_slots_v1")));
            Assert.AreEqual(DepotVisualStage.Unverified,
                DepotVisualStagePolicy.Resolve(true, State(false,false,false,false,"no_company")));
            Assert.AreEqual(DepotVisualStage.Unverified,
                DepotVisualStagePolicy.Resolve(true,
                    State(true,false,false,false,"legacy_hq_missing")));
            Assert.AreEqual(DepotVisualStage.Unverified,
                DepotVisualStagePolicy.Resolve(true,
                    State(true,true,false,false,"starter_site_missing")));
        }

        [Test]
        public void VisualProgressionTracksServerStateWithoutInventedDispatch()
        {
            Assert.AreEqual(DepotVisualStage.ReservedParcel,
                DepotVisualStagePolicy.Resolve(true,
                    State(true,true,true,false,"awaiting_economy")));
            Assert.AreEqual(DepotVisualStage.ActivationReady,
                DepotVisualStagePolicy.Resolve(true,
                    State(true,true,true,false,"ready_to_activate")));
            Assert.AreEqual(DepotVisualStage.StorageOperational,
                DepotVisualStagePolicy.Resolve(true,
                    State(true,true,true,true,
                        "warehouse_operational_dispatch_pending_routes",
                        24, "pallet_slots_v1")));
            Assert.AreNotEqual(DepotVisualStage.StorageOperational,
                DepotVisualStagePolicy.Resolve(true,
                    State(true,true,true,true,
                        "warehouse_operational_dispatch_pending_routes",
                        2400, "pallet_slots_v1")));
        }

        [Test]
        public void StaticBlockoutNeverCreatesFakeVehiclesOrInventedMotion()
        {
            var obj = new GameObject("StaticDepotTest",
                typeof(MeshFilter),typeof(MeshRenderer),typeof(DepotYardBlockoutView));
            try
            {
                var view = obj.GetComponent<DepotYardBlockoutView>();
                view.SetStage(DepotVisualStage.Unverified);
                Assert.IsFalse(view.ShowsWarehouse);
                Assert.IsFalse(view.ShowsVehicle);
                view.SetStage(DepotVisualStage.ReservedParcel);
                Assert.IsFalse(view.ShowsWarehouse);
                view.SetStage(DepotVisualStage.StorageOperational);
                Assert.IsTrue(view.ShowsWarehouse);
                Assert.IsFalse(view.ShowsVehicle);
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void PrototypeControllerDoesNotClaimVerifiedCompanyOffline()
        {
            var obj = new GameObject("OfflineDepot",
                typeof(MeshFilter),typeof(MeshRenderer),
                typeof(DepotYardBlockoutView),typeof(DepotPrototypeController));
            try
            {
                var controller = obj.GetComponent<DepotPrototypeController>();
                Assert.IsFalse(controller.HasVerifiedData);
                Assert.AreEqual(DepotVisualStage.Unverified, controller.CurrentStage);
            }
            finally { Object.DestroyImmediate(obj); }
        }
    }
}
