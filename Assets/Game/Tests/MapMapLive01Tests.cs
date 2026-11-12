using System;
using System.Collections.Generic;
using LogistiCore.Backend;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class MapMapLive01Tests
    {
        ClientEnvironmentSettings _settings;

        [SetUp]
        public void SetUp()
        {
            FirebaseClient.ResetForTests();
            ClientConnectionAuthorization.ResetForTests();
            ClientJobsRuntime.ResetForTests();
            MapJobsStore.ClearOverride();
            MapFleetStore.ClearOverride();
            MapRoadDeliveryStore.ClearOverride();
            MapJobsContractsRead.ResetForTests();
            MapFleetStateRead.ResetForTests();
            MapRoadDeliveriesRead.ResetForTests();
            MapCompanyEconomyRead.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null)
                UnityEngine.Object.DestroyImmediate(_settings);
            SetUp();
        }

        [Test]
        public void RoadFilter_AllowsKnownActiveCitiesAndRejectsCompleted()
        {
            Assert.IsTrue(ClientReadParser.TryDeliveries(
                Map(
                    "items",
                    new object[]
                    {
                        ActiveIzmirIstanbul(),
                        CompletedIzmirIstanbulDelivery(),
                        Map(
                            "deliveryId", "d-synth",
                            "contractId", "c-synth",
                            "status", "active",
                            "originCityId", "synthetic_city",
                            "destinationCityId", "synthetic_destination",
                            "routeId", "synthetic_route",
                            "cargoId", "x",
                            "assets", new object[0],
                            "startedAt", "2026-11-07T00:00:00.000Z",
                            "expectedArrivalAt", "",
                            "arrivalReady", false,
                            "completedAt", null,
                            "reward", "6",
                            "currency", "TST",
                            "scale", 2)
                    },
                    "nextCursor", null),
                out var page));
            Assert.IsTrue(ClientRoadFilter.CanPresentAsRoad(page.Items[0]));
            Assert.IsFalse(ClientRoadFilter.CanPresentAsRoad(page.Items[1]));
            Assert.IsFalse(ClientRoadFilter.CanPresentAsRoad(page.Items[2]));
            var road = ClientStoreBridge.Road(page);
            Assert.AreEqual(MapRoadDeliveryStatus.Ready, road.Status);
            Assert.AreEqual(1, road.Items.Length);
            Assert.AreEqual("d-active", road.Items[0].DeliveryId);
            Assert.AreEqual("starter_truck", road.Items[0].TruckId);
            Assert.IsFalse(road.Synthetic);
            Assert.IsTrue(MapCities.TryGet("izmir", out _));
            Assert.IsTrue(MapCities.TryGet("istanbul", out _));
            Assert.IsTrue(MapCities.TryGet("ankara", out _));
            Assert.IsTrue(MapCities.TryGet("bursa", out _));
            Assert.IsTrue(MapCities.TryGet("antalya", out _));
            Assert.IsTrue(MapCities.TryGet("adana", out _));
            Assert.IsTrue(MapCities.TryGet("diyarbakir", out _));
            Assert.IsTrue(MapCities.TryGet("trabzon", out _));
        }

        [Test]
        public void CompletedOnly_IsEmptyRoadNotError()
        {
            Assert.IsTrue(ClientReadParser.TryDeliveries(
                Map("items", new object[] { CompletedIzmirIstanbulDelivery() }, "nextCursor", null),
                out var page));
            var road = ClientStoreBridge.Road(page);
            Assert.AreEqual(MapRoadDeliveryStatus.Empty, road.Status);
            Assert.AreEqual(0, road.Items.Length);
            Assert.IsTrue(MapRoadDeliveryStore.TryApplyTrusted(road));
            Assert.AreEqual(MapRoadDeliveryStatus.Empty, MapRoadDeliveryStore.Current.Status);
            Assert.AreNotEqual(MapRoadDeliveryStatus.Error, MapRoadDeliveryStore.Current.Status);
            Assert.AreNotEqual(MapRoadDeliveryStatus.Unconfigured, MapRoadDeliveryStore.Current.Status);
        }

        [Test]
        public void RoadReadError_IsErrorNotEmpty()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) => { throw new System.InvalidOperationException("offline"); });
            MapRoadDeliveriesRead.HandleAuth("uid-a", ReadyStatus(), session);
            Assert.AreEqual(MapRoadDeliveryStatus.Error, MapRoadDeliveryStore.Current.Status);
            Assert.AreNotEqual(MapRoadDeliveryStatus.Empty, MapRoadDeliveryStore.Current.Status);
        }

        [Test]
        public void FleetDocumentId_StaysSeparateFromCatalogId()
        {
            Assert.IsTrue(ClientReadParser.TryFleet(
                Map(
                    "hasCompany", true,
                    "economyReady", true,
                    "starterFleetReady", true,
                    "items",
                    new object[]
                    {
                        Map(
                            "assetId", "asset_truck_doc",
                            "kind", "truck",
                            "catalogId", "starter_truck",
                            "catalogVersion", "1",
                            "status", "operational",
                            "assignmentId", "",
                            "available", true),
                        Map(
                            "assetId", "asset_driver_doc",
                            "kind", "driver",
                            "catalogId", "starter_driver",
                            "catalogVersion", "1",
                            "status", "operational",
                            "assignmentId", "",
                            "available", true)
                    },
                    "nextCursor", null),
                out var fleet));
            var snapshot = ClientStoreBridge.Fleet(fleet);
            Assert.IsTrue(MapFleetStore.TryApplyTrusted(snapshot));
            Assert.AreEqual(MapFleetStatus.Ready, MapFleetStore.Current.Status);
            Assert.IsTrue(MapFleetStore.Current.TryGet("asset_truck_doc", out var truck));
            Assert.AreEqual("starter_truck", truck.CatalogId);
            Assert.AreNotEqual(truck.AssetId, truck.CatalogId);
            Assert.IsTrue(MapFleetStore.Current.TryGet("asset_driver_doc", out var driver));
            Assert.AreEqual(MapFleetKind.Driver, driver.Kind);
            Assert.IsFalse(driver.HasCapacity);
            Assert.IsFalse(driver.HasGps);
        }

        [Test]
        public void JobsJoinDeliveries_ByContractId()
        {
            var contracts = new MapJobsSnapshot(
                MapJobsStatus.Ready,
                new[]
                {
                    new MapJobsItem(
                        "c1",
                        MapJobContractStatus.InProgress,
                        false,
                        "izmir",
                        "istanbul",
                        "lc_dev_general_freight_v1",
                        "1000",
                        "kg",
                        "1000",
                        "LGC",
                        2,
                        "",
                        MapJobDeliveryStatus.None,
                        "",
                        "")
                },
                "c1",
                MapJobCategory.Active,
                false);
            Assert.IsTrue(MapJobsStore.TryApplyTrusted(contracts));
            Assert.IsTrue(ClientReadParser.TryDeliveries(
                Map("items", new object[] { ActiveIzmirIstanbul("c1") }, "nextCursor", null),
                out var page));
            Assert.IsTrue(MapJobsStore.TryJoinDeliveries(page));
            Assert.IsTrue(MapJobsStore.Current.TryGet("c1", out var item));
            Assert.AreEqual("d-active", item.DeliveryId);
            Assert.AreEqual(MapJobDeliveryStatus.Active, item.DeliveryStatus);
            Assert.AreEqual("starter_truck", item.AssignedVehicleId);
        }

        [Test]
        public void SignOut_ClearsJobsFleetRoadCompany()
        {
            var session = Session((name, payload) => Envelope(Reply(name)));
            _settings = ValidLocal();
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth("uid-a");
            Assert.AreEqual(MapJobsStatus.Ready, MapJobsStore.Current.Status);
            Assert.AreEqual(MapFleetStatus.Ready, MapFleetStore.Current.Status);
            Assert.AreEqual(MapRoadDeliveryStatus.Empty, MapRoadDeliveryStore.Current.Status);
            Assert.IsTrue(MapCompanyStore.HasTrusted);
            ClientJobsRuntime.NotifyAuth("");
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
            Assert.AreEqual(MapFleetStatus.Unconfigured, MapFleetStore.Current.Status);
            Assert.AreEqual(MapRoadDeliveryStatus.Unconfigured, MapRoadDeliveryStore.Current.Status);
            Assert.IsFalse(MapCompanyStore.HasTrusted);
        }

        [Test]
        public void WrongUidStaleDelivery_IsDropped()
        {
            ClientConnectionAuthorization.ApproveForTests();
            Action<object> pending = null;
            var session = new ClientReadSession();
            session.SetAsyncInvoker((name, payload, ok, fail) =>
            {
                if (name == ClientCallableCatalog.GetMyDeliveries && pending == null)
                {
                    pending = ok;
                    return;
                }

                ok(Envelope(Reply(name)));
            });
            MapRoadDeliveriesRead.HandleAuth("uid-a", ReadyStatus(), session);
            MapRoadDeliveriesRead.HandleAuth("uid-b", ReadyStatus(), session);
            Assert.IsNotNull(pending);
            pending(Envelope(Map("items", new object[] { ActiveIzmirIstanbul() }, "nextCursor", null)));
            Assert.AreEqual(MapRoadDeliveryStatus.Empty, MapRoadDeliveryStore.Current.Status);
        }

        static ClientEnvironmentSettings ValidLocal()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            settings.Configure(
                ClientBackendEnvironment.LocalEmulator,
                ClientProtocol.DemoProjectId,
                "local-app",
                "fake-api-key",
                "127.0.0.1",
                ClientLocalEmulatorSecurity.FirestorePort,
                ClientLocalEmulatorSecurity.AuthPort,
                ClientLocalEmulatorSecurity.FunctionsOrigin,
                ClientProtocol.FunctionsRegion);
            return settings;
        }

        static ClientReadSession Session(ClientReadInvoker invoker)
        {
            var session = new ClientReadSession();
            session.SetInvoker(invoker);
            return session;
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
            return Map("protocolVersion", 1, "serverTime", "2026-11-07T12:00:00.000Z", "result", result);
        }

        static Dictionary<string, object> Reply(string name)
        {
            if (name == ClientCallableCatalog.GetMyContracts)
            {
                return Map(
                    "items",
                    new object[]
                    {
                        Map(
                            "contractId", "c1",
                            "status", "completed",
                            "expired", false,
                            "originCityId", "izmir",
                            "destinationCityId", "istanbul",
                            "routeId", "r1",
                            "cargoId", "lc_dev_general_freight_v1",
                            "quantity", "1000",
                            "unit", "kg",
                            "reward", "1000",
                            "currency", "LGC",
                            "scale", 2,
                            "expiresAt", "",
                            "acceptedAt", "")
                    },
                    "nextCursor", null);
            }

            if (name == ClientCallableCatalog.GetMyFleetState)
            {
                return Map(
                    "hasCompany", true,
                    "economyReady", true,
                    "starterFleetReady", true,
                    "items",
                    new object[]
                    {
                        Map(
                            "assetId", "asset_truck_doc",
                            "kind", "truck",
                            "catalogId", "starter_truck",
                            "catalogVersion", "1",
                            "status", "operational",
                            "assignmentId", "",
                            "available", true)
                    },
                    "nextCursor", null);
            }

            if (name == ClientCallableCatalog.GetMyDeliveries)
                return Map("items", new object[0], "nextCursor", null);

            return Map(
                "hasCompany", true,
                "economyReady", true,
                "companyId", "co_test",
                "balance", "1501000",
                "currency", "LGC",
                "scale", 2,
                "xp", 1,
                "level", 1,
                "configVersion", "1",
                "rulesVersion", "1");
        }

        static Dictionary<string, object> ActiveIzmirIstanbul(string contractId = "c-active")
        {
            return Map(
                "deliveryId", "d-active",
                "contractId", contractId,
                "status", "active",
                "originCityId", "izmir",
                "destinationCityId", "istanbul",
                "routeId", "route_izmir_istanbul",
                "cargoId", "lc_dev_general_freight_v1",
                "assets", new object[] { Map("assetId", "starter_truck", "kind", "truck") },
                "startedAt", "2026-11-07T00:00:00.000Z",
                "expectedArrivalAt", "2026-11-07T12:00:00.000Z",
                "arrivalReady", false,
                "completedAt", null,
                "reward", "1000",
                "currency", "LGC",
                "scale", 2);
        }

        static Dictionary<string, object> CompletedIzmirIstanbulDelivery()
        {
            return Map(
                "deliveryId", "d-done",
                "contractId", "c-done",
                "status", "completed",
                "originCityId", "izmir",
                "destinationCityId", "istanbul",
                "routeId", "route_izmir_istanbul",
                "cargoId", "lc_dev_general_freight_v1",
                "assets", new object[] { Map("assetId", "starter_truck", "kind", "truck") },
                "startedAt", "2026-11-07T00:00:00.000Z",
                "expectedArrivalAt", "2026-11-07T01:00:00.000Z",
                "arrivalReady", true,
                "completedAt", "2026-11-07T01:00:00.000Z",
                "reward", "1000",
                "currency", "LGC",
                "scale", 2);
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
