using System;
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
    public sealed class MapFleetLive02Tests
    {
        GameObject _root;
        MapFleetView _view;
        ClientEnvironmentSettings _settings;

        [SetUp]
        public void SetUp()
        {
            FirebaseClient.ResetForTests();
            ClientConnectionAuthorization.ResetForTests();
            ClientJobsRuntime.ResetForTests();
            MapFleetStore.ClearOverride();
            MapFleetStore.ClearTrusted();
            MapFleetStateRead.ResetForTests();
            MapSceneSetup.EnsureLocalization();
            _root = new GameObject("FleetLive02Root", typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)_root.transform;
            rect.sizeDelta = new Vector2(1280f, 720f);
            _view = MapFleetView.Attach(rect);
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null)
                Object.DestroyImmediate(_settings);
            MapFleetStore.ClearOverride();
            MapFleetStore.ClearTrusted();
            ClientJobsRuntime.ResetForTests();
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void ReadyTruckAndDriver_ListBothAfterLoadingPlace()
        {
            _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
            MapFleetStore.Override(MapFleetFixtures.Loading());
            _view.Refresh();
            Assert.AreEqual(MapFleetStatus.Loading, _view.Status);
            Assert.AreEqual(0, _view.ActiveRowCount);
            Assert.IsFalse(_view.ListVisible);

            MapFleetStore.ClearOverride();
            Assert.IsTrue(ClientReadParser.TryFleet(BackendFleet(true, Truck(), Driver()), out var parsed));
            Assert.IsTrue(MapFleetStore.TryApplyTrusted(ClientStoreBridge.Fleet(parsed)));
            _view.Refresh();
            Assert.AreEqual(MapFleetStatus.Ready, _view.Status);
            Assert.IsTrue(_view.ListVisible);
            Assert.AreEqual(2, _view.ActiveRowCount);
            Assert.AreEqual("starter_truck", _view.RowId(0));
            Assert.AreEqual("starter_driver", _view.RowId(1));
            Assert.AreNotEqual("lc_starter_truck", _view.RowId(0));
            Assert.IsTrue(IsKind(_view.RowTitle(0), "Truck", "Kamyon", "Lkw"));
            Assert.IsTrue(IsKind(_view.RowTitle(1), "Driver", "Sürücü", "Fahrer"));
            Assert.IsTrue(IsKind(_view.RowMetaText(0), "Available", "Müsait", "Verfügbar"));
            Assert.IsFalse(_view.StatusText.Contains("Filo özeti yüklendi"));
            Assert.IsFalse(_view.StatusText.Contains("Fleet snapshot loaded"));
            Assert.IsFalse(_view.RowTitle(0).Contains("lc_starter_truck"));
        }

        [Test]
        public void AssignedVersusAvailable_UsesBackendFlags()
        {
            Assert.IsTrue(ClientReadParser.TryFleet(
                BackendFleet(
                    true,
                    Asset("starter_truck", "truck", "lc_starter_truck", "operational", null, true),
                    Asset("starter_driver", "driver", "lc_starter_driver", "operational", "ab" + new string('c', 62), false)),
                out var parsed));
            var snapshot = ClientStoreBridge.Fleet(parsed);
            Assert.IsTrue(MapFleetStore.TryApplyTrusted(snapshot));
            _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
            Assert.AreEqual(2, _view.ActiveRowCount);
            Assert.IsTrue(MapFleetStore.Current.TryGet("starter_truck", out var truck));
            Assert.IsTrue(truck.Available);
            Assert.IsFalse(truck.HasAssignment);
            Assert.AreEqual("lc_starter_truck", truck.CatalogId);
            Assert.AreNotEqual(truck.AssetId, truck.CatalogId);
            Assert.IsTrue(MapFleetStore.Current.TryGet("starter_driver", out var driver));
            Assert.IsFalse(driver.Available);
            Assert.IsTrue(driver.HasAssignment);
            Assert.IsFalse(driver.HasCapacity);
        }

        [Test]
        public void NullAssignmentId_IsAvailableNotParseError()
        {
            Assert.IsTrue(ClientReadParser.TryFleet(BackendFleet(true, Truck()), out var parsed));
            Assert.AreEqual("", parsed.Items[0].AssignmentId);
            Assert.IsTrue(parsed.Items[0].Available);
            var snapshot = ClientStoreBridge.Fleet(parsed);
            Assert.AreEqual(MapFleetStatus.Ready, snapshot.Status);
            Assert.AreEqual(1, snapshot.Assets.Length);
        }

        [Test]
        public void EmptyCompanyFleet_IsEmptyNotReady()
        {
            Assert.IsTrue(ClientReadParser.TryFleet(BackendFleet(true), out var parsed));
            Assert.IsTrue(MapFleetStore.TryApplyTrusted(ClientStoreBridge.Fleet(parsed)));
            _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
            Assert.AreEqual(MapFleetStatus.Empty, _view.Status);
            Assert.AreEqual(0, _view.ActiveRowCount);
            Assert.IsFalse(_view.ListVisible);
            Assert.AreNotEqual(MapFleetStatus.Error, _view.Status);
            Assert.AreNotEqual(MapFleetStatus.Unconfigured, _view.Status);
        }

        [Test]
        public void InvalidDto_IsErrorNotReady()
        {
            Assert.IsFalse(ClientReadParser.TryFleet(
                Map("hasCompany", true, "economyReady", true, "starterFleetReady", true, "items", new object[]
                {
                    Map("kind", "truck", "catalogId", "lc_starter_truck")
                }, "nextCursor", null),
                out _));
            ClientConnectionAuthorization.ApproveForTests();
            var session = new ClientReadSession();
            session.SetInvoker((_, __) => Envelope(Map(
                "hasCompany", true,
                "economyReady", true,
                "starterFleetReady", true,
                "items",
                new object[] { Map("kind", "truck") },
                "nextCursor", null)));
            MapFleetStateRead.HandleAuth("uid-a", ReadyStatus(), session);
            Assert.AreEqual(MapFleetStatus.Error, MapFleetStore.Current.Status);
            Assert.AreNotEqual(MapFleetStatus.Empty, MapFleetStore.Current.Status);
        }

        [Test]
        public void SignOutAndUidSwitch_DropTrustedFleet()
        {
            _settings = ValidLocal();
            var session = new ClientReadSession();
            session.SetInvoker((_, __) => Envelope(BackendFleet(true, Truck(), Driver())));
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth("uid-a");
            Assert.AreEqual(MapFleetStatus.Ready, MapFleetStore.Current.Status);
            Assert.AreEqual(2, MapFleetStore.Current.Assets.Length);
            ClientJobsRuntime.NotifyAuth("");
            Assert.AreEqual(MapFleetStatus.Unconfigured, MapFleetStore.Current.Status);
            Assert.AreEqual(0, MapFleetStore.Current.Assets.Length);
        }

        [Test]
        public void StaleUidFleet_IsDropped()
        {
            ClientConnectionAuthorization.ApproveForTests();
            Action<object> pending = null;
            var session = new ClientReadSession();
            session.SetAsyncInvoker((name, payload, ok, fail) =>
            {
                if (name == ClientCallableCatalog.GetMyFleetState && pending == null)
                {
                    pending = ok;
                    return;
                }

                ok(Envelope(BackendFleet(true)));
            });
            MapFleetStateRead.HandleAuth("uid-a", ReadyStatus(), session);
            MapFleetStateRead.HandleAuth("uid-b", ReadyStatus(), session);
            Assert.IsNotNull(pending);
            pending(Envelope(BackendFleet(true, Truck(), Driver())));
            Assert.AreEqual(MapFleetStatus.Empty, MapFleetStore.Current.Status);
            Assert.IsFalse(MapFleetStore.Current.TryGet("starter_truck", out _));
        }

        [Test]
        public void LongLocaleMeta_DoesNotOverflowRow()
        {
            MapFleetStore.Override(MapFleetFixtures.ReadyThree());
            _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
            Assert.Greater(_view.ActiveRowCount, 0);
            var hit = _view.RowHitPixels(0);
            Assert.GreaterOrEqual(hit.height, 43.5f);
            Assert.IsFalse(string.IsNullOrEmpty(_view.RowTitle(0)));
        }

        [Test]
        public void LoadingErrorUnconfigured_StayDistinct()
        {
            Assert.AreEqual(MapFleetStatus.Unconfigured, MapFleetStore.FromBackend(
                new ClientBackendStatus(ClientBackendEnvironment.Unconfigured, ClientConnectionDecision.StayOffline, ClientBackendPhase.Idle, "")).Status);
            MapFleetStore.TryApplyTrusted(MapFleetSnapshot.Loading());
            Assert.AreEqual(MapFleetStatus.Loading, MapFleetStore.Current.Status);
            MapFleetStore.TryApplyTrusted(MapFleetSnapshot.Error());
            Assert.AreEqual(MapFleetStatus.Error, MapFleetStore.Current.Status);
        }

        static bool IsKind(string value, string en, string tr, string de)
        {
            return value == en || value == tr || value == de;
        }

        static Dictionary<string, object> Truck()
        {
            return Asset("starter_truck", "truck", "lc_starter_truck", "operational", null, true);
        }

        static Dictionary<string, object> Driver()
        {
            return Asset("starter_driver", "driver", "lc_starter_driver", "operational", null, true);
        }

        static Dictionary<string, object> Asset(
            string assetId,
            string kind,
            string catalogId,
            string status,
            string assignmentId,
            bool available)
        {
            return Map(
                "assetId", assetId,
                "kind", kind,
                "catalogId", catalogId,
                "catalogVersion", "lc_dev_fleet_v2",
                "status", status,
                "assignmentId", assignmentId,
                "available", available);
        }

        static Dictionary<string, object> BackendFleet(bool hasCompany, params Dictionary<string, object>[] items)
        {
            return Map(
                "hasCompany", hasCompany,
                "economyReady", true,
                "starterFleetReady", true,
                "items", items,
                "nextCursor", null);
        }

        static Dictionary<string, object> Envelope(Dictionary<string, object> result)
        {
            return Map("protocolVersion", 1, "serverTime", "2026-11-07T13:00:00.000Z", "result", result);
        }

        static ClientBackendStatus ReadyStatus()
        {
            return new ClientBackendStatus(
                ClientBackendEnvironment.LocalEmulator,
                ClientConnectionDecision.ConnectToEmulator,
                ClientBackendPhase.Ready,
                "");
        }

        static ClientEnvironmentSettings ValidLocal()
        {
            var settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
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

        static Dictionary<string, object> Map(params object[] pairs)
        {
            var map = new Dictionary<string, object>();
            for (var i = 0; i < pairs.Length; i += 2)
                map[(string)pairs[i]] = pairs[i + 1];
            return map;
        }
    }
}
