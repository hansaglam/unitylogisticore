using System.Collections.Generic;
using LogistiCore.Backend;
using LogistiCore.EditorTools;
using LogistiCore.Localization;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LogistiCore.Tests
{
    public sealed class MapJobsCandidateTests
    {
        const string AcceptedId = "accepted_izmir_istanbul";
        const string TruckId = "starter_truck";
        const string DriverId = "starter_driver";
        const string TrailerId = "starter_trailer";
        MapJobsView _view;
        GameObject _root;
        string _previousLocale;

        [SetUp]
        public void SetUp()
        {
            FirebaseClient.ResetForTests();
            ClientConnectionAuthorization.ResetForTests();
            ClientJobsRuntime.ResetForTests();
            MapJobsStore.ClearOverride();
            MapJobsStore.ClearTrusted();
            MapFleetStore.ClearOverride();
            MapFleetStore.ClearTrusted();
            MapJobsContractsRead.ResetForTests();
            MapJobsAccept.ResetForTests();
            MapJobsEquipmentSelection.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_view != null && _root != null)
                Object.DestroyImmediate(_root);
            _view = null;
            _root = null;
            if (!string.IsNullOrEmpty(_previousLocale) && LocaleService.IsReady)
                LocaleService.TrySelect(_previousLocale);
            SetUp();
        }

        [Test]
        public void TryContracts_ParsesDriverTruckRegardlessOfOrder()
        {
            Assert.IsTrue(ClientReadParser.TryContracts(Page(ContractRow(Assets(
                Asset(TruckId, "truck"),
                Asset(DriverId, "driver")))), out var page));
            Assert.IsTrue(page.Items[0].CandidateAssetsPresent);
            Assert.AreEqual(2, page.Items[0].CandidateAssets.Length);
            Assert.AreEqual(TruckId, page.Items[0].CandidateAssets[0].AssetId);
            Assert.AreEqual("truck", page.Items[0].CandidateAssets[0].Kind);
            Assert.AreEqual(DriverId, page.Items[0].CandidateAssets[1].AssetId);
            var jobs = ClientStoreBridge.Jobs(page);
            Assert.IsTrue(jobs.Items[0].TryCandidate(MapFleetKind.Driver, out var driver));
            Assert.IsTrue(jobs.Items[0].TryCandidate(MapFleetKind.Truck, out var truck));
            Assert.AreEqual(DriverId, driver.AssetId);
            Assert.AreEqual(TruckId, truck.AssetId);
            Assert.IsFalse(jobs.Items[0].RequiresTrailer);
        }

        [Test]
        public void TryContracts_ParsesDriverTrailerTruck()
        {
            Assert.IsTrue(ClientReadParser.TryContracts(Page(ContractRow(Assets(
                Asset(TrailerId, "trailer"),
                Asset(TruckId, "truck"),
                Asset(DriverId, "driver")))), out var page));
            var item = ClientStoreBridge.Jobs(page).Items[0];
            Assert.IsTrue(item.RequiresTrailer);
            Assert.IsTrue(item.TryCandidate(MapFleetKind.Trailer, out var trailer));
            Assert.AreEqual(TrailerId, trailer.AssetId);
            Assert.IsTrue(item.HasStartCandidates);
        }

        [Test]
        public void TryContracts_MissingCandidates_StayReadableWithoutSelection()
        {
            Assert.IsTrue(ClientReadParser.TryContracts(Page(ContractRow(null)), out var page));
            Assert.IsFalse(page.Items[0].CandidateAssetsPresent);
            Assert.AreEqual(0, page.Items[0].CandidateAssets.Length);
            var item = ClientStoreBridge.Jobs(page).Items[0];
            Assert.IsFalse(item.HasStartCandidates);
            Assert.IsFalse(MapJobsEquipmentSelection.CanShow(item, ReadyJobs(item, MapJobCategory.Active)));
        }

        [Test]
        public void TryContracts_CorruptCandidates_FailClosed()
        {
            Assert.IsFalse(ClientReadParser.TryContracts(Page(ContractRow(Assets())), out _));
            Assert.IsFalse(ClientReadParser.TryContracts(Page(ContractRow(Assets(Asset("", "truck"), Asset(DriverId, "driver")))), out _));
            Assert.IsFalse(ClientReadParser.TryContracts(Page(ContractRow(Assets(Asset(TruckId, "truck"), Asset(TruckId, "driver")))), out _));
            Assert.IsFalse(ClientReadParser.TryContracts(Page(ContractRow(Assets(Asset(TruckId, "boat"), Asset(DriverId, "driver")))), out _));
            Assert.IsFalse(ClientReadParser.TryContracts(Page(ContractRow(Assets(Asset(TruckId, "truck"), Asset(DriverId, "driver"), Asset(TrailerId, "truck")))), out _));
            var extra = Asset(TruckId, "truck");
            extra["catalogId"] = "lc_dev_truck";
            Assert.IsFalse(ClientReadParser.TryContracts(Page(ContractRow(Assets(extra, Asset(DriverId, "driver")))), out _));
        }

        [Test]
        public void Join_UsesAssetDocumentId_NotCatalogId()
        {
            var item = MapJobsFixtures.AcceptedDriverTruck();
            var fleet = MapFleetSnapshot.Fixture(
                MapFleetStatus.Ready,
                new[]
                {
                    MapFleetFixtures.Truck("other_truck", TruckId, true, false, MapFleetAssetStatus.Operational),
                    MapFleetFixtures.Driver(DriverId, "lc_dev_driver", true, false)
                });
            Assert.IsTrue(MapJobsCandidateJoin.TrySlot(item, fleet, MapFleetKind.Truck, out var truck));
            Assert.IsTrue(truck.MissingFromFleet);
            Assert.IsFalse(truck.Selectable);
            Assert.IsFalse(MapJobsEquipmentSelection.TrySelect(item, fleet, MapFleetKind.Truck, "other_truck"));
        }

        [Test]
        public void Join_RejectsUnlistedAvailableTruck_AndAssignedOrMissing()
        {
            var item = MapJobsFixtures.AcceptedDriverTruck();
            var fleet = MapFleetSnapshot.Fixture(
                MapFleetStatus.Ready,
                new[]
                {
                    MapFleetFixtures.Truck(TruckId, "catalog_truck", false, true, MapFleetAssetStatus.Operational),
                    MapFleetFixtures.Driver("other_driver", "catalog_driver", true, false),
                    MapFleetFixtures.Truck("spare_truck", "catalog_spare", true, false, MapFleetAssetStatus.Operational)
                });
            Assert.IsTrue(MapJobsCandidateJoin.TrySlot(item, fleet, MapFleetKind.Truck, out var truck));
            Assert.IsTrue(truck.Assigned);
            Assert.IsFalse(truck.Selectable);
            Assert.IsTrue(MapJobsCandidateJoin.TrySlot(item, fleet, MapFleetKind.Driver, out var driver));
            Assert.IsTrue(driver.MissingFromFleet);
            Assert.IsFalse(MapJobsCandidateJoin.IsListedCandidate(item, "spare_truck"));
            Assert.IsFalse(MapJobsEquipmentSelection.TrySelect(item, fleet, MapFleetKind.Truck, "spare_truck"));
            Assert.IsFalse(MapJobsEquipmentSelection.TrySelect(item, fleet, MapFleetKind.Truck, TruckId));
        }

        [Test]
        public void Join_KindMismatch_IsUnavailable()
        {
            var item = MapJobsFixtures.AcceptedDriverTruck();
            var fleet = MapFleetSnapshot.Fixture(
                MapFleetStatus.Ready,
                new[]
                {
                    MapFleetFixtures.Truck("unrelated_truck", "catalog", true, false, MapFleetAssetStatus.Operational),
                    MapFleetFixtures.Driver(TruckId, "wrong", true, false),
                    MapFleetFixtures.Driver(DriverId, "driver", true, false)
                });
            Assert.IsTrue(MapJobsCandidateJoin.TrySlot(item, fleet, MapFleetKind.Truck, out var truck));
            Assert.IsTrue(truck.InFleet);
            Assert.IsFalse(truck.KindMatches);
            Assert.IsFalse(truck.Selectable);
        }

        [Test]
        public void EquipmentUi_AcceptedOnly_HidesOfferedAndCompleted()
        {
            MapSceneSetup.EnsureLocalization();
            AttachView();
            MapFleetStore.Override(AvailableFleet());
            MapJobsStore.Override(AcceptedSnapshot());
            _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
            _view.SetCategory(MapJobCategory.Active);
            _view.Select(AcceptedId);
            Assert.IsTrue(_view.EquipmentVisible);
            Assert.AreEqual(2, _view.EquipmentRowCount);
            Assert.IsTrue(_view.EquipmentSelectInteractable(0));
            Assert.IsTrue(_view.EquipmentSelectInteractable(1));
            Assert.IsFalse(_view.StartDeliveryHoldVisible);
            Assert.IsFalse(_view.DetailBlocksOverlap());
            Assert.IsTrue(_view.DetailCloseVisible);

            MapJobsStore.Override(MapJobsSnapshot.Fixture(
                MapJobsStatus.Ready,
                new[]
                {
                    MapJobsFixtures.Contract(
                        "offer_ankara_bursa",
                        MapJobContractStatus.Offered,
                        "ankara",
                        "bursa",
                        "steel",
                        false,
                        "1",
                        "t",
                        "6",
                        "TST",
                        2,
                        "",
                        MapJobDeliveryStatus.None,
                        "",
                        "",
                        "2099-01-01T00:00:00.000Z",
                        "",
                        "",
                        "",
                        new[]
                        {
                            new MapJobCandidate(DriverId, MapFleetKind.Driver),
                            new MapJobCandidate(TruckId, MapFleetKind.Truck)
                        },
                        true)
                },
                "offer_ankara_bursa",
                MapJobCategory.Available));
            _view.SetCategory(MapJobCategory.Available);
            _view.Select("offer_ankara_bursa");
            Assert.IsFalse(_view.EquipmentVisible);

            MapJobsStore.Override(MapJobsSnapshot.Fixture(
                MapJobsStatus.Ready,
                new[]
                {
                    MapJobsFixtures.Contract(
                        "done_izmir",
                        MapJobContractStatus.Completed,
                        "izmir",
                        "istanbul",
                        "steel",
                        false,
                        "1",
                        "t",
                        "6",
                        "TST",
                        2,
                        "",
                        MapJobDeliveryStatus.Completed,
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        new[]
                        {
                            new MapJobCandidate(DriverId, MapFleetKind.Driver),
                            new MapJobCandidate(TruckId, MapFleetKind.Truck)
                        },
                        true)
                },
                "done_izmir",
                MapJobCategory.History));
            _view.SetCategory(MapJobCategory.History);
            _view.Select("done_izmir");
            Assert.IsFalse(_view.EquipmentVisible);
        }

        [Test]
        public void ContractAndAuthSwitch_ClearSelection()
        {
            var first = MapJobsFixtures.AcceptedDriverTruck();
            var second = MapJobsFixtures.AcceptedDriverTruck("accepted_ankara_bursa", "other_truck", "other_driver");
            var fleet = MapFleetSnapshot.Fixture(
                MapFleetStatus.Ready,
                new[]
                {
                    MapFleetFixtures.Truck(TruckId, "c1", true, false, MapFleetAssetStatus.Operational),
                    MapFleetFixtures.Driver(DriverId, "d1", true, false),
                    MapFleetFixtures.Truck("other_truck", "c2", true, false, MapFleetAssetStatus.Operational),
                    MapFleetFixtures.Driver("other_driver", "d2", true, false)
                });
            var jobs = new MapJobsSnapshot(
                MapJobsStatus.Ready,
                new[] { first, second },
                first.ContractId,
                MapJobCategory.Active,
                false);
            Assert.IsTrue(MapJobsStore.TryApplyTrusted(jobs));
            MapJobsEquipmentSelection.Sync(first, MapJobsStore.Current, fleet);
            Assert.AreEqual(TruckId, MapJobsEquipmentSelection.TruckId);
            Assert.AreEqual(DriverId, MapJobsEquipmentSelection.DriverId);
            MapJobsStore.Select(second.ContractId);
            Assert.AreEqual("", MapJobsEquipmentSelection.TruckId);
            MapJobsEquipmentSelection.Sync(second, MapJobsStore.Current, fleet);
            Assert.AreEqual("other_truck", MapJobsEquipmentSelection.TruckId);
            MapJobsStore.SetCategory(MapJobCategory.History);
            Assert.AreEqual("", MapJobsEquipmentSelection.TruckId);
            MapJobsEquipmentSelection.Sync(first, ReadyJobs(first, MapJobCategory.Active), fleet);
            MapJobsEquipmentSelection.Sync(first, ReadyJobs(first, MapJobCategory.Active), fleet);
            MapJobsContractsRead.BeginPlaySession();
            Assert.AreEqual("", MapJobsEquipmentSelection.ContractId);
            Assert.AreEqual("", MapJobsEquipmentSelection.TruckId);
            MapJobsEquipmentSelection.Sync(first, ReadyJobs(first, MapJobCategory.Active), fleet);
            MapJobsContractsRead.SignOut();
            Assert.AreEqual("", MapJobsEquipmentSelection.ContractId);
            Assert.AreEqual("", MapJobsEquipmentSelection.TruckId);
        }

        [Test]
        public void Localization_EquipmentLabels_TrEnDe()
        {
            MapSceneSetup.EnsureLocalization();
            _previousLocale = LocaleService.CurrentCode;
            AttachView();
            MapFleetStore.Override(AvailableFleet());
            MapJobsStore.Override(AcceptedSnapshot());
            _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
            _view.SetCategory(MapJobCategory.Active);
            _view.Select(AcceptedId);
            Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.Turkish));
            _view.Refresh();
            Assert.AreEqual("Kamyon", OneOf(_view.EquipmentKindText(0), _view.EquipmentKindText(1), "Kamyon"));
            Assert.IsTrue(Contains(_view.EquipmentKindText(0), _view.EquipmentKindText(1), "Sürücü"));
            Assert.IsTrue(_view.EquipmentMetaText(0).Length > 0);
            Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.English));
            _view.Refresh();
            Assert.IsTrue(Contains(_view.EquipmentKindText(0), _view.EquipmentKindText(1), "Truck"));
            Assert.IsTrue(Contains(_view.EquipmentKindText(0), _view.EquipmentKindText(1), "Driver"));
            Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.German));
            _view.Refresh();
            Assert.IsTrue(Contains(_view.EquipmentKindText(0), _view.EquipmentKindText(1), "Lkw"));
            Assert.IsTrue(Contains(_view.EquipmentKindText(0), _view.EquipmentKindText(1), "Fahrer"));
            Assert.IsFalse(_view.EquipmentKindText(0).Contains("starter_"));
            Assert.IsFalse(_view.EquipmentKindText(1).Contains("lc_dev"));
        }

        [Test]
        public void NoLiveCommandInvocation()
        {
            var names = new List<string>();
            var read = new ClientReadSession();
            read.SetInvoker((name, payload) =>
            {
                names.Add(name);
                return Envelope(Page(ContractRow(Assets(Asset(DriverId, "driver"), Asset(TruckId, "truck")))));
            });
            var command = new ClientCommandSession();
            command.SetInvoker((name, payload) =>
            {
                names.Add(name);
                return Envelope(new Dictionary<string, object>());
            });
            ClientConnectionAuthorization.ApproveForTests();
            MapJobsContractsRead.HandleAuth("uid-a", ReadyStatus(), read);
            MapJobsAccept.HandleAuth("uid-a", ReadyStatus(), command);
            var item = MapJobsFixtures.AcceptedDriverTruck();
            Assert.IsTrue(MapJobsEquipmentSelection.CanShow(item, ReadyJobs(item, MapJobCategory.Active)));
            Assert.AreEqual(1, names.Count);
            Assert.AreEqual(ClientCallableCatalog.GetMyContracts, names[0]);
            Assert.AreEqual(0, command.InvokeCount);
            Assert.IsFalse(ClientCommandAuthorization.AllowsLiveFunctions(ClientJobsRuntime.Settings, ReadyStatus()));
        }

        [Test]
        public void JobsRead_CorruptCandidates_BecomeError_WithoutCommands()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var names = new List<string>();
            var session = new ClientReadSession();
            session.SetInvoker((name, payload) =>
            {
                names.Add(name);
                return Envelope(Page(ContractRow(Assets(Asset(TruckId, "truck"), Asset(TruckId, "driver")))));
            });
            MapJobsContractsRead.HandleAuth("uid-a", ReadyStatus(), session);
            Assert.AreEqual(MapJobsStatus.Error, MapJobsStore.Current.Status);
            Assert.AreEqual(1, names.Count);
            Assert.AreEqual(ClientCallableCatalog.GetMyContracts, names[0]);
        }

        void AttachView()
        {
            _root = new GameObject("JobsCandidateRoot", typeof(RectTransform), typeof(Canvas));
            var root = (RectTransform)_root.transform;
            root.sizeDelta = new Vector2(1280f, 720f);
            _view = MapJobsView.Attach(root);
        }

        static MapJobsSnapshot AcceptedSnapshot()
        {
            var item = MapJobsFixtures.AcceptedDriverTruck();
            return MapJobsSnapshot.Fixture(MapJobsStatus.Ready, new[] { item }, item.ContractId, MapJobCategory.Active);
        }

        static MapFleetSnapshot AvailableFleet()
        {
            return MapFleetSnapshot.Fixture(
                MapFleetStatus.Ready,
                new[]
                {
                    MapFleetFixtures.Truck(TruckId, "do-not-show", true, false, MapFleetAssetStatus.Operational),
                    MapFleetFixtures.Driver(DriverId, "do-not-show", true, false)
                });
        }

        static MapJobsSnapshot ReadyJobs(MapJobsItem item, MapJobCategory category)
        {
            return new MapJobsSnapshot(MapJobsStatus.Ready, new[] { item }, item.ContractId, category, false);
        }

        static bool Contains(string a, string b, string token)
        {
            return (a != null && a.IndexOf(token) >= 0) || (b != null && b.IndexOf(token) >= 0);
        }

        static string OneOf(string a, string b, string token)
        {
            if (a == token)
                return a;
            if (b == token)
                return b;
            return a + "|" + b;
        }

        static ClientBackendStatus ReadyStatus()
        {
            return new ClientBackendStatus(
                ClientBackendEnvironment.LocalEmulator,
                ClientConnectionDecision.ConnectToEmulator,
                ClientBackendPhase.Ready,
                "");
        }

        static Dictionary<string, object> Envelope(Dictionary<string, object> result)
        {
            return Map("protocolVersion", 1, "serverTime", "2026-11-07T17:40:00.000Z", "result", result);
        }

        static Dictionary<string, object> Page(params Dictionary<string, object>[] items)
        {
            return Map("items", items, "nextCursor", null);
        }

        static Dictionary<string, object> ContractRow(object candidates)
        {
            var row = Map(
                "contractId", AcceptedId,
                "status", "accepted",
                "expired", false,
                "originCityId", "izmir",
                "destinationCityId", "istanbul",
                "routeId", "route_izmir_istanbul",
                "cargoId", "lc_dev_general_freight",
                "quantity", "1",
                "unit", "load",
                "reward", "100000",
                "currency", "LGC",
                "scale", 2,
                "expiresAt", "2026-11-07T00:00:00.000Z",
                "acceptedAt", "2026-11-07T00:10:00.000Z");
            if (candidates != null)
                row["candidateAssets"] = candidates;
            return row;
        }

        static List<Dictionary<string, object>> Assets(params Dictionary<string, object>[] items)
        {
            return new List<Dictionary<string, object>>(items);
        }

        static Dictionary<string, object> Asset(string id, string kind)
        {
            return Map("assetId", id, "kind", kind);
        }

        static Dictionary<string, object> Map(params object[] pairs)
        {
            var map = new Dictionary<string, object>();
            for (var i = 0; i < pairs.Length; i += 2)
                map[(string)pairs[i]] = pairs[i + 1];
            return map;
        }
    }
}
