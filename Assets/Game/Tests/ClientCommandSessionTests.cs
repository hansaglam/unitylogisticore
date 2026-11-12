using System;
using System.Collections.Generic;
using LogistiCore.Backend;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class ClientCommandSessionTests
    {
        ClientEnvironmentSettings _settings;

        [SetUp]
        public void SetUp()
        {
            FirebaseClient.ResetForTests();
            ClientConnectionAuthorization.ResetForTests();
            ClientEmulatorLiveAuthorization.ResetForTests();
            ClientJobsRuntime.ResetForTests();
            _settings = ValidLocal();
            ClientJobsRuntime.BindForTests(ReadyStatus(), new ClientReadSession(), _settings);
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null)
                UnityEngine.Object.DestroyImmediate(_settings);
            SetUp();
        }

        [Test]
        public void OfferedContract_BuildsAcceptEnvelope()
        {
            ClientConnectionAuthorization.ApproveForTests();
            IReadOnlyDictionary<string, object> sent = null;
            var session = Session((name, payload) =>
            {
                sent = payload;
                Assert.AreEqual(ClientCallableCatalog.AcceptContract, name);
                return Envelope(Accepted("offer_ankara_bursa"));
            });
            var request = ClientValue.CommandRequest(
                "accept_offered_contract_key",
                ClientValue.AcceptContractPayload("offer_ankara_bursa"));
            var outcome = session.Execute(ClientCallableCatalog.AcceptContract, ReadyStatus(), true, request);
            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(1, session.InvokeCount);
            Assert.AreEqual(1, request["protocolVersion"]);
            Assert.AreEqual("accept_offered_contract_key", request["idempotencyKey"]);
            var inner = (Dictionary<string, object>)request["payload"];
            Assert.AreEqual(1, inner.Count);
            Assert.AreEqual("offer_ankara_bursa", inner["contractId"]);
            Assert.IsFalse(inner.ContainsKey("reward"));
            Assert.IsFalse(inner.ContainsKey("status"));
            Assert.AreEqual(3, sent.Count);
            Assert.IsTrue(ClientReadParser.TryAcceptContract(outcome.Envelope.Result, out var parsed));
            Assert.IsTrue(parsed.IsAccepted);
        }

        [Test]
        public void ExtraPayloadFields_AreRejected()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) =>
            {
                Assert.Fail("must not invoke");
                return null;
            });
            var payload = new Dictionary<string, object>
            {
                { "contractId", "offer_ankara_bursa" },
                { "reward", "1000" }
            };
            var outcome = session.Execute(
                ClientCallableCatalog.AcceptContract,
                ReadyStatus(),
                true,
                ClientValue.CommandRequest("accept_extra_payload_key", payload));
            Assert.AreEqual(ClientCallableFault.NonRetryableCommand, outcome.Fault);
            Assert.AreEqual(0, session.InvokeCount);
        }

        [Test]
        public void StartAndClaimAndGenerate_AreRefused()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) =>
            {
                Assert.Fail("unsupported command");
                return null;
            });
            Assert.AreEqual(
                ClientCallableFault.NonRetryableCommand,
                session.Execute(ClientCallableCatalog.StartDelivery, ReadyStatus(), true, AcceptRequest()).Fault);
            Assert.AreEqual(
                ClientCallableFault.NonRetryableCommand,
                session.Execute(ClientCallableCatalog.ClaimDeliveryReward, ReadyStatus(), true, AcceptRequest()).Fault);
            Assert.AreEqual(
                ClientCallableFault.NonRetryableCommand,
                session.Execute("generateCompanyContractOffers", ReadyStatus(), true, AcceptRequest()).Fault);
            Assert.IsFalse(ClientCallableCatalog.TryKind("generateCompanyContractOffers", out _));
            Assert.AreEqual(0, session.InvokeCount);
            Assert.IsFalse(ClientCallableErrors.MayRetryCommand(ClientCallableFault.Timeout));
        }

        [Test]
        public void MissingAuth_IsBlocked()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) =>
            {
                Assert.Fail("auth");
                return null;
            });
            var outcome = session.Execute(ClientCallableCatalog.AcceptContract, ReadyStatus(), false, AcceptRequest());
            Assert.AreEqual(ClientCallableFault.MissingAuth, outcome.Fault);
            Assert.AreEqual(0, session.InvokeCount);
        }

        [Test]
        public void StayOffline_IsBlocked()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) =>
            {
                Assert.Fail("offline");
                return null;
            });
            var status = new ClientBackendStatus(
                ClientBackendEnvironment.LocalEmulator,
                ClientConnectionDecision.StayOffline,
                ClientBackendPhase.Ready,
                "");
            var outcome = session.Execute(ClientCallableCatalog.AcceptContract, status, true, AcceptRequest());
            Assert.IsTrue(
                outcome.Fault == ClientCallableFault.Unconfigured
                || outcome.Fault == ClientCallableFault.ConnectionHold,
                outcome.Fault.ToString());
            Assert.AreEqual(0, session.InvokeCount);
        }

        [Test]
        public void ProductionEnvironment_IsBlocked()
        {
            var session = Session((_, __) =>
            {
                Assert.Fail("production");
                return null;
            });
            var status = new ClientBackendStatus(
                ClientBackendEnvironment.Production,
                ClientConnectionDecision.ConnectToEmulator,
                ClientBackendPhase.Ready,
                "");
            var outcome = session.Execute(ClientCallableCatalog.AcceptContract, status, true, AcceptRequest());
            Assert.AreEqual(ClientCallableFault.ConnectionHold, outcome.Fault);
            Assert.AreEqual(0, session.InvokeCount);
        }

        [Test]
        public void DuplicateInFlight_DoesNotStartSecondCall()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = new ClientCommandSession();
            ClientCommandOutcome nested = default;
            session.SetInvoker((name, payload) =>
            {
                nested = session.Execute(name, ReadyStatus(), true, payload);
                return Envelope(Accepted("offer_ankara_bursa"));
            });
            var first = session.Execute(ClientCallableCatalog.AcceptContract, ReadyStatus(), true, AcceptRequest());
            Assert.IsTrue(first.Ok);
            Assert.AreEqual(ClientCallableFault.DuplicateInFlight, nested.Fault);
            Assert.AreEqual(1, session.InvokeCount);
        }

        [Test]
        public void Timeout_DoesNotRetry()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) => throw new TimeoutException());
            var outcome = session.Execute(ClientCallableCatalog.AcceptContract, ReadyStatus(), true, AcceptRequest());
            Assert.AreEqual(ClientCallableFault.Timeout, outcome.Fault);
            Assert.AreEqual(1, session.InvokeCount);
            Assert.IsFalse(ClientCallableErrors.MayRetryCommand(outcome.Fault));
        }

        [Test]
        public void SameIdempotencyKey_IsClientStableForOneAction()
        {
            Assert.IsTrue(ClientValue.IsSafeIdempotencyKey("accept_offered_contract_key"));
            Assert.IsTrue(ClientValue.IsSafeContractId("offer_ankara_bursa"));
            Assert.IsFalse(ClientValue.IsSafeIdempotencyKey("__reserved__"));
            Assert.IsFalse(ClientValue.IsSafeContractId(""));
            var key = ClientValue.NewIdempotencyKey();
            Assert.IsTrue(ClientValue.IsSafeIdempotencyKey(key));
            Assert.AreEqual(32, key.Length);
        }

        [Test]
        public void LiveFunctionsInvoker_StaysHold()
        {
            Assert.IsFalse(ClientCommandAuthorization.AllowsLiveFunctions(_settings, ReadyStatus()));
            var failed = 0;
            ClientFunctionsCommandInvoker.ResetForTests();
            ClientFunctionsCommandInvoker.Invoke(
                ClientCallableCatalog.AcceptContract,
                AcceptRequest(),
                _ => Assert.Fail("live command"),
                _ => failed++);
            Assert.AreEqual(1, failed);
            Assert.AreEqual(1, ClientFunctionsCommandInvoker.DispatchAttempts);
            Assert.AreEqual(0, ClientFunctionsCommandInvoker.CallAsyncAttempts);
        }

        [Test]
        public void GatedTransport_DoesNotCallFirebase()
        {
            var session = new ClientCommandSession();
            session.SetAsyncInvoker(ClientFunctionsCommandInvoker.Invoke);
            ClientFunctionsCommandInvoker.ResetForTests();
            var outcome = session.Execute(ClientCallableCatalog.AcceptContract, ReadyStatus(), true, AcceptRequest());
            Assert.AreEqual(ClientCallableFault.ConnectionHold, outcome.Fault);
            Assert.AreEqual(1, session.InvokeCount);
            Assert.AreEqual(0, ClientFunctionsCommandInvoker.CallAsyncAttempts);
        }

        [Test]
        public void ReadSessionStillRefusesCommands()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var reads = new ClientReadSession();
            reads.SetInvoker((_, __) =>
            {
                Assert.Fail("command on read");
                return null;
            });
            var outcome = reads.Read(ClientCallableCatalog.AcceptContract, ReadyStatus(), true, ClientValue.ReadRequest());
            Assert.AreEqual(ClientCallableFault.NonRetryableCommand, outcome.Fault);
            Assert.AreEqual(0, reads.InvokeCount);
        }

        static ClientCommandSession Session(ClientCommandInvoker invoker)
        {
            var session = new ClientCommandSession();
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

        static Dictionary<string, object> AcceptRequest()
        {
            return ClientValue.CommandRequest("accept_offered_contract_key", ClientValue.AcceptContractPayload("offer_ankara_bursa"));
        }

        static Dictionary<string, object> Envelope(Dictionary<string, object> result)
        {
            return new Dictionary<string, object>
            {
                { "protocolVersion", 1 },
                { "serverTime", "2026-11-07T13:00:00.000Z" },
                { "result", result }
            };
        }

        static Dictionary<string, object> Accepted(string id)
        {
            return new Dictionary<string, object>
            {
                { "contractId", id },
                { "status", "accepted" },
                { "acceptedAt", "2026-11-07T13:00:00.000Z" },
                { "reward", "6" },
                { "currency", "LGC" },
                { "scale", 2 },
                { "equipmentReserved", false }
            };
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
    }
}
