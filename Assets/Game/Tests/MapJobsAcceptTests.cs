using System;
using System.Collections.Generic;
using LogistiCore.Backend;
using LogistiCore.Localization;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LogistiCore.Tests
{
    public sealed class MapJobsAcceptTests
    {
        const string UidA = "uid-a";
        const string UidB = "uid-b";
        const string OfferId = "offer_ankara_bursa";
        const string CompletedId = "9298bef2f37fca8a0ee1de263803936763390db67afb18b63835ecb1e0e69c54";
        ClientEnvironmentSettings _settings;

        [SetUp]
        public void SetUp()
        {
            FirebaseClient.ResetForTests();
            ClientConnectionAuthorization.ResetForTests();
            ClientEmulatorLiveAuthorization.ResetForTests();
            ClientJobsRuntime.ResetForTests();
            MapJobsStore.ClearOverride();
            MapJobsContractsRead.ResetForTests();
            MapJobsAccept.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null)
                Object.DestroyImmediate(_settings);
            SetUp();
        }

        [Test]
        public void OfferedContract_SendsAcceptThenRefreshesContracts()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            var contractsReads = 0;
            var commands = 0;
            IReadOnlyDictionary<string, object> sent = null;
            var read = ReadSession((name, payload) =>
            {
                if (name != ClientCallableCatalog.GetMyContracts)
                    return Envelope(Page());
                contractsReads++;
                return Envelope(Page(contractsReads == 1 ? Offered() : AcceptedRow()));
            });
            ClientJobsRuntime.BindForTests(ReadyStatus(), read, _settings);
            ClientJobsRuntime.SetCommandInvokerForTests((name, payload) =>
            {
                commands++;
                sent = payload;
                Assert.AreEqual(ClientCallableCatalog.AcceptContract, name);
                return CommandEnvelope(AcceptedResult(OfferId));
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.AreEqual(MapJobContractStatus.Offered, Current(OfferId).ContractStatus);
            Assert.IsTrue(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(1, commands);
            Assert.AreEqual(2, contractsReads);
            var payload = (Dictionary<string, object>)sent["payload"];
            Assert.AreEqual(OfferId, payload["contractId"]);
            Assert.AreEqual(1, payload.Count);
            Assert.AreEqual(1, sent["protocolVersion"]);
            Assert.IsTrue(ClientValue.IsSafeIdempotencyKey((string)sent["idempotencyKey"]));
            Assert.AreEqual(MapJobContractStatus.Accepted, Current(OfferId).ContractStatus);
            Assert.AreEqual(MapJobsAcceptPhase.Idle, MapJobsAccept.Phase);
        }

        [Test]
        public void CompletedContract_DoesNotCommand()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            var commands = 0;
            BindOfferedRead(Page(Completed()));
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) =>
            {
                commands++;
                return CommandEnvelope(AcceptedResult(CompletedId));
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsFalse(MapJobsAccept.TryBegin(CompletedId));
            Assert.AreEqual(0, commands);
            Assert.AreEqual(MapJobContractStatus.Completed, Current(CompletedId).ContractStatus);
            Assert.AreEqual("CONTRACT_NOT_OFFERED", MapJobsAccept.DetailsCode);
        }

        [Test]
        public void ExpiredOffer_IsBlockedLocally()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            var commands = 0;
            BindOfferedRead(Page(Offered("2020-01-01T00:00:00.000Z", false)));
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) =>
            {
                commands++;
                return CommandEnvelope(AcceptedResult(OfferId));
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsFalse(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(0, commands);
            Assert.AreEqual("OFFER_EXPIRED", MapJobsAccept.DetailsCode);
            Assert.AreEqual(MapJobContractStatus.Offered, Current(OfferId).ContractStatus);
        }

        [Test]
        public void MissingAuth_IsBlocked()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            var commands = 0;
            BindOfferedRead(Page(Offered()));
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) =>
            {
                commands++;
                return null;
            });
            ClientJobsRuntime.NotifyAuth("");
            Assert.IsFalse(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(0, commands);
        }

        [Test]
        public void WrongProject_IsBlocked()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = Local("other-project", "127.0.0.1");
            var commands = 0;
            var read = ReadSession((_, __) => Envelope(Page(Offered())));
            ClientJobsRuntime.BindForTests(ReadyStatus(), read, _settings);
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) =>
            {
                commands++;
                return CommandEnvelope(AcceptedResult(OfferId));
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsFalse(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(0, commands);
        }

        [Test]
        public void StayOffline_IsBlocked()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            var commands = 0;
            var read = ReadSession((_, __) => Envelope(Page(Offered())));
            var offline = new ClientBackendStatus(
                ClientBackendEnvironment.LocalEmulator,
                ClientConnectionDecision.StayOffline,
                ClientBackendPhase.Ready,
                "");
            ClientJobsRuntime.BindForTests(offline, read, _settings);
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) =>
            {
                commands++;
                return CommandEnvelope(AcceptedResult(OfferId));
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsFalse(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(0, commands);
        }

        [Test]
        public void DoubleTap_SendsOneRequest()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            var commands = 0;
            Action<object> pending = null;
            BindOfferedRead(Page(Offered()));
            ClientJobsRuntime.SetCommandAsyncInvokerForTests((name, payload, ok, fail) =>
            {
                commands++;
                pending = ok;
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsTrue(MapJobsAccept.TryBegin(OfferId));
            var key = MapJobsAccept.ActiveIdempotencyKey;
            Assert.IsFalse(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(1, commands);
            Assert.AreEqual(key, MapJobsAccept.ActiveIdempotencyKey);
            Assert.AreEqual(MapJobsAcceptPhase.Busy, MapJobsAccept.Phase);
            pending(CommandEnvelope(AcceptedResult(OfferId)));
        }

        [Test]
        public void Timeout_IsUncertainAndDoesNotAutoRetry()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            var commands = 0;
            BindOfferedRead(Page(Offered()));
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) =>
            {
                commands++;
                throw new TimeoutException();
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsTrue(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(1, commands);
            Assert.AreEqual(MapJobContractStatus.Offered, Current(OfferId).ContractStatus);
            Assert.AreNotEqual(MapJobsAcceptPhase.Busy, MapJobsAccept.Phase);
            Assert.AreEqual(OpsContent.JobsAcceptUncertain, MapJobsAccept.MessageKey);
            Assert.IsFalse(ClientCallableErrors.MayRetryCommand(ClientCallableFault.Timeout));
        }

        [Test]
        public void StaleResponse_IsNotAppliedToNewUser()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            Action<object> pending = null;
            var read = ReadSession((_, __) =>
            {
                if (MapJobsContractsRead.Uid == UidB)
                    return Envelope(Page());
                return Envelope(Page(Offered()));
            });
            ClientJobsRuntime.BindForTests(ReadyStatus(), read, _settings);
            ClientJobsRuntime.SetCommandAsyncInvokerForTests((name, payload, ok, fail) => { pending = ok; });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsTrue(MapJobsAccept.TryBegin(OfferId));
            ClientJobsRuntime.NotifyAuth(UidB);
            pending(CommandEnvelope(AcceptedResult(OfferId)));
            Assert.AreEqual(UidB, MapJobsAccept.Uid);
            Assert.IsFalse(MapJobsStore.Current.TryGet(OfferId, out _));
            Assert.AreEqual(MapJobsAcceptPhase.Idle, MapJobsAccept.Phase);
        }

        [Test]
        public void ServerReject_DoesNotChangeTrustedState()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            BindOfferedRead(Page(Offered()));
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) =>
            {
                throw new ClientCommandException("CONTRACT_QUOTA_REACHED");
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsTrue(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(MapJobContractStatus.Offered, Current(OfferId).ContractStatus);
            Assert.AreEqual(MapJobsAcceptPhase.Failed, MapJobsAccept.Phase);
            Assert.AreEqual("CONTRACT_QUOTA_REACHED", MapJobsAccept.DetailsCode);
            Assert.AreEqual(OpsContent.JobsAcceptQuota, MapJobsAccept.MessageKey);
        }

        [Test]
        public void PlayStop_DropsInFlightResult()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            Action<object> pending = null;
            BindOfferedRead(Page(Offered()));
            ClientJobsRuntime.SetCommandAsyncInvokerForTests((name, payload, ok, fail) => { pending = ok; });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsTrue(MapJobsAccept.TryBegin(OfferId));
            MapJobsAccept.BeginPlaySession();
            pending(CommandEnvelope(AcceptedResult(OfferId)));
            Assert.AreEqual(MapJobsAcceptPhase.Idle, MapJobsAccept.Phase);
            Assert.AreEqual("", MapJobsAccept.ActiveContractId);
        }

        [Test]
        public void BindForTests_DoesNotAttachLiveCommandInvoker()
        {
            _settings = ValidLocal();
            var read = ReadSession((_, __) => Envelope(Page()));
            ClientJobsRuntime.BindForTests(ReadyStatus(), read, _settings);
            Assert.IsFalse(ClientJobsRuntime.Commands.HasInvoker);
            Assert.IsFalse(ClientCommandAuthorization.AllowsLiveFunctions(_settings, ReadyStatus()));
            Assert.AreEqual(0, ClientFunctionsCommandInvoker.CallAsyncAttempts);
        }

        [Test]
        public void NonLoopback_IsBlocked()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = Local(ClientProtocol.DemoProjectId, "8.8.8.8");
            var commands = 0;
            var read = ReadSession((_, __) => Envelope(Page(Offered())));
            ClientJobsRuntime.BindForTests(ReadyStatus(), read, _settings);
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) =>
            {
                commands++;
                return CommandEnvelope(AcceptedResult(OfferId));
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsFalse(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(0, commands);
        }

        [Test]
        public void BackendOfferExpired_DoesNotChangeState()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            BindOfferedRead(Page(Offered()));
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) =>
            {
                throw new ClientCommandException("OFFER_EXPIRED");
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsTrue(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(MapJobContractStatus.Offered, Current(OfferId).ContractStatus);
            Assert.AreEqual("OFFER_EXPIRED", MapJobsAccept.DetailsCode);
            Assert.AreEqual(OpsContent.JobsAcceptExpired, MapJobsAccept.MessageKey);
        }

        [Test]
        public void BackendContractExpiredAlias_MapsExpiredCopy()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            BindOfferedRead(Page(Offered()));
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) =>
            {
                throw new ClientCommandException("CONTRACT_EXPIRED");
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsTrue(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(MapJobContractStatus.Offered, Current(OfferId).ContractStatus);
            Assert.AreEqual(OpsContent.JobsAcceptExpired, MapJobsAccept.MessageKey);
        }

        [Test]
        public void BackendIneligible_DoesNotChangeState()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            BindOfferedRead(Page(Offered()));
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) =>
            {
                throw new ClientCommandException("CONTRACT_INELIGIBLE");
            });
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsTrue(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(MapJobContractStatus.Offered, Current(OfferId).ContractStatus);
            Assert.AreEqual("CONTRACT_INELIGIBLE", MapJobsAccept.DetailsCode);
            Assert.AreEqual(OpsContent.JobsAcceptIneligible, MapJobsAccept.MessageKey);
        }

        [Test]
        public void SuccessfulCommand_FailedRefresh_StaysUnconfirmed()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            var contractsReads = 0;
            var read = ReadSession((name, payload) =>
            {
                if (name != ClientCallableCatalog.GetMyContracts)
                    return Envelope(Page());
                contractsReads++;
                if (contractsReads == 1)
                    return Envelope(Page(Offered()));
                throw new InvalidOperationException("offline");
            });
            ClientJobsRuntime.BindForTests(ReadyStatus(), read, _settings);
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) => CommandEnvelope(AcceptedResult(OfferId)));
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.AreEqual(MapJobContractStatus.Offered, Current(OfferId).ContractStatus);
            Assert.IsTrue(MapJobsAccept.TryBegin(OfferId));
            Assert.AreEqual(2, contractsReads);
            Assert.AreNotEqual(MapJobContractStatus.Accepted, Current(OfferId).ContractStatus);
            Assert.IsTrue(MapJobsAccept.Unconfirmed);
            Assert.AreEqual(OpsContent.JobsAcceptUncertain, MapJobsAccept.MessageKey);
        }

        [Test]
        public void AppRelaunch_DoesNotReuseIdempotencyKey()
        {
            ClientConnectionAuthorization.ApproveForTests();
            _settings = ValidLocal();
            BindOfferedRead(Page(Offered()));
            ClientJobsRuntime.SetCommandInvokerForTests((_, __) => CommandEnvelope(AcceptedResult(OfferId)));
            ClientJobsRuntime.NotifyAuth(UidA);
            Assert.IsTrue(MapJobsAccept.TryBegin(OfferId));
            MapJobsAccept.BeginPlaySession();
            Assert.AreEqual("", MapJobsAccept.ActiveIdempotencyKey);
            Assert.IsFalse(ClientCallableErrors.MayRetryCommand(ClientCallableFault.Timeout));
        }

        void BindOfferedRead(Dictionary<string, object> page)
        {
            var read = ReadSession((_, __) => Envelope(page));
            ClientJobsRuntime.BindForTests(ReadyStatus(), read, _settings);
        }

        static MapJobsItem Current(string id)
        {
            MapJobsStore.Current.TryGet(id, out var item);
            return item;
        }

        static ClientReadSession ReadSession(ClientReadInvoker invoker)
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

        static ClientEnvironmentSettings ValidLocal()
        {
            return Local(ClientProtocol.DemoProjectId, "127.0.0.1");
        }

        static ClientEnvironmentSettings Local(string projectId, string host)
        {
            var settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            settings.Configure(
                ClientBackendEnvironment.LocalEmulator,
                projectId,
                "local-app",
                "fake-api-key",
                host,
                ClientLocalEmulatorSecurity.FirestorePort,
                ClientLocalEmulatorSecurity.AuthPort,
                ClientLocalEmulatorSecurity.FunctionsOrigin,
                ClientProtocol.FunctionsRegion);
            return settings;
        }

        static Dictionary<string, object> Envelope(Dictionary<string, object> result)
        {
            return Map("protocolVersion", 1, "serverTime", "2026-11-07T13:00:00.000Z", "result", result);
        }

        static Dictionary<string, object> CommandEnvelope(Dictionary<string, object> result)
        {
            return Envelope(result);
        }

        static Dictionary<string, object> Page(params Dictionary<string, object>[] items)
        {
            return Map("items", items, "nextCursor", null);
        }

        static Dictionary<string, object> Offered(string expires = "2099-01-01T00:00:00.000Z", bool expired = false)
        {
            return Map(
                "contractId", OfferId,
                "status", "offered",
                "expired", expired,
                "originCityId", "ankara",
                "destinationCityId", "bursa",
                "routeId", "ankara-bursa",
                "cargoId", "lc_dev_general_freight_v1",
                "quantity", "1000",
                "unit", "kg",
                "reward", "1000",
                "currency", "LGC",
                "scale", 2,
                "expiresAt", expires,
                "acceptedAt", null);
        }

        static Dictionary<string, object> AcceptedRow()
        {
            return Map(
                "contractId", OfferId,
                "status", "accepted",
                "expired", false,
                "originCityId", "ankara",
                "destinationCityId", "bursa",
                "routeId", "ankara-bursa",
                "cargoId", "lc_dev_general_freight_v1",
                "quantity", "1000",
                "unit", "kg",
                "reward", "1000",
                "currency", "LGC",
                "scale", 2,
                "expiresAt", "2099-01-01T00:00:00.000Z",
                "acceptedAt", "2026-11-07T13:00:00.000Z");
        }

        static Dictionary<string, object> Completed()
        {
            return Map(
                "contractId", CompletedId,
                "status", "completed",
                "expired", false,
                "originCityId", "izmir",
                "destinationCityId", "istanbul",
                "routeId", "izmir-istanbul",
                "cargoId", "lc_dev_general_freight_v1",
                "quantity", "1000",
                "unit", "kg",
                "reward", "1000",
                "currency", "LGC",
                "scale", 2,
                "expiresAt", "2026-11-07T00:00:00.000Z",
                "acceptedAt", "2026-11-06T23:27:08.252Z");
        }

        static Dictionary<string, object> AcceptedResult(string id)
        {
            return Map(
                "contractId", id,
                "status", "accepted",
                "acceptedAt", "2026-11-07T13:00:00.000Z",
                "reward", "6",
                "currency", "LGC",
                "scale", 2,
                "equipmentReserved", false);
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
