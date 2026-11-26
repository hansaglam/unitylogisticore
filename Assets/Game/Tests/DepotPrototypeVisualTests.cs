using System.Reflection;
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
        public void AccountChangeClearsFailedReadLatchAndPreviouslyVerifiedDepot()
        {
            var obj = new GameObject("DepotAccountScopeRegression",
                typeof(MeshFilter), typeof(MeshRenderer),
                typeof(DepotYardBlockoutView), typeof(DepotPrototypeController));
            try
            {
                var controller = obj.GetComponent<DepotPrototypeController>();
                var type = typeof(DepotPrototypeController);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                type.GetField("_uid", flags).SetValue(controller, "old-player");
                type.GetField("_error", flags).SetValue(controller, true);
                type.GetField("_verified", flags).SetValue(controller, true);
                type.GetField("_requested", flags).SetValue(controller, true);
                type.GetField("_stage", flags).SetValue(controller,
                    DepotVisualStage.StorageOperational);
                type.GetField("_generation", flags).SetValue(controller, 5);

                type.GetMethod("ResetReadState", flags).Invoke(controller, null);

                Assert.IsFalse(controller.HasVerifiedData);
                Assert.AreEqual(DepotVisualStage.Unverified, controller.CurrentStage);
                Assert.IsFalse((bool)type.GetField("_error", flags).GetValue(controller));
                Assert.IsFalse((bool)type.GetField("_requested", flags).GetValue(controller));
                Assert.AreEqual("", type.GetField("_uid", flags).GetValue(controller));
                Assert.AreEqual(6, type.GetField("_generation", flags).GetValue(controller));
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void AuthoredKitOnlyShowsClosedDocksForVerifiedOperationalStage()
        {
            var obj = new GameObject("AuthoredDepotKitTest",
                typeof(MeshFilter), typeof(MeshRenderer), typeof(DepotYardBlockoutView));
            try
            {
                var yard = obj.GetComponent<DepotYardBlockoutView>();
                yard.SetStage(DepotVisualStage.Unverified);
                var neutralTriangles = yard.AuthoredTriangleCount;
                Assert.IsFalse(yard.ShowsLoadingDockArt);
                Assert.IsFalse(yard.ShowsWarehouse);

                yard.SetStage(DepotVisualStage.ReservedParcel);
                var reservedTriangles = yard.AuthoredTriangleCount;
                Assert.Greater(reservedTriangles, neutralTriangles);
                Assert.IsFalse(yard.ShowsLoadingDockArt);

                yard.SetStage(DepotVisualStage.ActivationReady);
                Assert.Greater(yard.AuthoredTriangleCount, reservedTriangles);
                Assert.IsFalse(yard.ShowsLoadingDockArt);

                yard.SetStage(DepotVisualStage.StorageOperational);
                Assert.IsTrue(yard.ShowsWarehouse);
                Assert.IsTrue(yard.ShowsLoadingDockArt);
                Assert.Greater(yard.AuthoredTriangleCount, reservedTriangles);
                Assert.IsFalse(yard.ShowsVehicle);
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void OperationalBuildingHasTwoDistinctVisibleWallFaces()
        {
            var obj = new GameObject("DepotWallContrastRegression",
                typeof(MeshFilter), typeof(MeshRenderer),
                typeof(DepotYardBlockoutView));
            try
            {
                var yard = obj.GetComponent<DepotYardBlockoutView>();
                var filter = obj.GetComponent<MeshFilter>();
                yard.SetStage(DepotVisualStage.ReservedParcel);
                Assert.IsFalse(ContainsColor(filter.sharedMesh.colors32,
                    new Color32(151, 179, 184, 255)));

                yard.SetStage(DepotVisualStage.StorageOperational);
                var colors = filter.sharedMesh.colors32;
                Assert.IsTrue(ContainsColor(colors,
                    new Color32(151, 179, 184, 255)),
                    "Bright loading facade must be rendered.");
                Assert.IsTrue(ContainsColor(colors,
                    new Color32(84, 117, 132, 255)),
                    "Side wall must create a distinct building silhouette.");
                Assert.IsFalse(yard.ShowsVehicle);
            }
            finally { Object.DestroyImmediate(obj); }
        }

        static bool ContainsColor(Color32[] colors, Color32 expected)
        {
            for (var i = 0; i < colors.Length; i++)
                if (colors[i].Equals(expected))
                    return true;
            return false;
        }

        [Test]
        public void SiteInfrastructureAndVisualStateStayVerifiedOnly()
        {
            var obj = new GameObject("DepotSiteInfrastructureRegression",
                typeof(MeshFilter), typeof(MeshRenderer),
                typeof(DepotYardBlockoutView));
            try
            {
                var view = obj.GetComponent<DepotYardBlockoutView>();
                view.SetStage(DepotVisualStage.Unverified);
                Assert.IsFalse(view.ShowsSiteInfrastructure);
                var neutralTriangles = view.AuthoredTriangleCount;

                view.SetStage(DepotVisualStage.ReservedParcel);
                Assert.IsTrue(view.ShowsSiteInfrastructure);
                Assert.Greater(view.AuthoredTriangleCount, neutralTriangles);
                Assert.IsFalse(view.ShowsWarehouse);

                view.SetStage(DepotVisualStage.ActivationReady);
                Assert.IsTrue(view.ShowsSiteInfrastructure);
                Assert.IsFalse(view.ShowsLoadingDockArt);

                view.SetStage(DepotVisualStage.StorageOperational);
                Assert.IsTrue(view.ShowsSiteInfrastructure);
                Assert.IsTrue(view.ShowsLoadingDockArt);
                Assert.IsFalse(view.ShowsVehicle);
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void YardTapOnlySelectsVerifiedSiteAndNeverInventsDispatch()
        {
            var warehouseFace = new Vector2(2.1f, -1.0f);
            var site = new Vector2(-2.9f, -0.3f);
            var road = new Vector2(6.0f, -5.5f);
            Assert.AreEqual(DepotYardInspectTarget.None,
                DepotYardInspectionPolicy.Resolve(
                    DepotVisualStage.Unverified, warehouseFace));
            Assert.AreEqual(DepotYardInspectTarget.Parcel,
                DepotYardInspectionPolicy.Resolve(
                    DepotVisualStage.ReservedParcel, site));
            Assert.AreEqual(DepotYardInspectTarget.Storage,
                DepotYardInspectionPolicy.Resolve(
                    DepotVisualStage.StorageOperational, warehouseFace));
            Assert.AreNotEqual(DepotYardInspectTarget.Storage,
                DepotYardInspectionPolicy.Resolve(
                    DepotVisualStage.ActivationReady, warehouseFace));
            Assert.AreEqual(DepotYardInspectTarget.None,
                DepotYardInspectionPolicy.Resolve(
                    DepotVisualStage.StorageOperational, road));
            Assert.AreEqual(DepotYardInspectTarget.None,
                DepotYardInspectionPolicy.Resolve(
                    DepotVisualStage.StorageOperational,
                    new Vector2(float.NaN, 0f)));
        }

        [Test]
        public void DepotIllustrationStageNamesAreUniqueAndNeverImplyDispatch()
        {
            var names = new System.Collections.Generic.HashSet<string>();
            Assert.AreEqual(4, DepotIllustrationCatalog.Stages.Length);
            foreach (var stage in DepotIllustrationCatalog.Stages)
            {
                var fileName = DepotIllustrationCatalog.FileName(stage);
                Assert.IsFalse(string.IsNullOrEmpty(fileName));
                Assert.IsTrue(fileName.EndsWith(".png"));
                Assert.IsTrue(names.Add(fileName));
            }
            Assert.IsNull(DepotIllustrationCatalog.FileName((DepotVisualStage)255));
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
