using System;
using System.Collections.Generic;
using LogistiCore.Backend;
using LogistiCore.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class EmulatorContractsDtoProbeTests
    {
        ClientEnvironmentSettings _settings;

        [SetUp]
        public void SetUp()
        {
            FirebaseClient.ResetForTests();
            ClientConnectionAuthorization.ResetForTests();
            ClientConnectionAuthorization.ApproveForTests();
            ClientJobsRuntime.ResetForTests();
            ClientCommandAuthorization.AllowsLiveFunctions(null, default);
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null)
                UnityEngine.Object.DestroyImmediate(_settings);
            SetUp();
        }

        [Test]
        public void MissingAuth_DisablesVerify()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => Envelope(Page()));
            Assert.IsFalse(EmulatorContractsDtoProbe.TryCanVerify(
                true, _settings, ReadyStatus(), "", session, false, out var hold));
            Assert.AreEqual(EmulatorContractsDtoProbe.AuthHold, hold);
            Assert.AreEqual(0, session.InvokeCount);
        }

        [Test]
        public void WrongUid_DisablesVerify()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => Envelope(Page()));
            Assert.IsFalse(EmulatorContractsDtoProbe.TryCanVerify(
                true, _settings, ReadyStatus(), "other-uid", session, false, out var hold));
            Assert.AreEqual(EmulatorContractsDtoProbe.AuthHold, hold);
            Assert.AreEqual(0, session.InvokeCount);
        }

        [Test]
        public void WrongProject_DisablesVerify()
        {
            _settings = Local("other-project", "127.0.0.1");
            var session = Session((_, __) => Envelope(Page()));
            Assert.IsFalse(EmulatorContractsDtoProbe.TryCanVerify(
                true, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid, session, false, out _));
            Assert.AreEqual(0, session.InvokeCount);
        }

        [Test]
        public void Offline_DisablesVerify()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => Envelope(Page()));
            var offline = new ClientBackendStatus(
                ClientBackendEnvironment.Unconfigured,
                ClientConnectionDecision.StayOffline,
                ClientBackendPhase.Offline,
                ClientConnectionPolicy.OfflineDetail);
            Assert.IsFalse(EmulatorContractsDtoProbe.TryCanVerify(
                true, _settings, offline, EmulatorAuthTestGate.ExpectedUid, session, false, out _));
            Assert.AreEqual(0, session.InvokeCount);
        }

        [Test]
        public void SingleClick_OneRead_NoRetry()
        {
            _settings = ValidLocal();
            var names = new List<string>();
            var session = Session((name, payload) =>
            {
                names.Add(name);
                Assert.AreEqual(1, payload["protocolVersion"]);
                return Envelope(Page(CompletedWithCandidates()));
            });
            string verdict = null;
            Assert.IsTrue(EmulatorContractsDtoProbe.TryBeginVerify(
                true, true, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid, session, false, 1,
                (v, e, g) => verdict = v, out _));
            Assert.AreEqual(1, session.InvokeCount);
            Assert.AreEqual(1, names.Count);
            Assert.AreEqual(ClientCallableCatalog.GetMyContracts, names[0]);
            Assert.AreEqual(EmulatorContractsDtoProbe.DtoVerified, verdict);
            Assert.IsFalse(ClientCommandAuthorization.AllowsLiveFunctions(_settings, ReadyStatus()));
        }

        [Test]
        public void Busy_BlocksSecondCall()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => Envelope(Page(CompletedWithCandidates())));
            Assert.IsFalse(EmulatorContractsDtoProbe.TryBeginVerify(
                true, true, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid, session, true, 1,
                (v, e, g) => { }, out var hold));
            Assert.AreEqual(EmulatorContractsDtoProbe.DuplicateBlocked, hold);
            Assert.AreEqual(0, session.InvokeCount);
        }

        [Test]
        public void MissingCandidateAssets_FieldMissing()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => Envelope(Page(CompletedBare())));
            string verdict = null;
            Assert.IsTrue(EmulatorContractsDtoProbe.TryBeginVerify(
                true, true, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid, session, false, 1,
                (v, e, g) => verdict = v, out _));
            Assert.AreEqual(EmulatorContractsDtoProbe.DtoFieldMissing, verdict);
        }

        [Test]
        public void ValidCandidates_VerifiedAndParsed()
        {
            _settings = ValidLocal();
            string evidence = null;
            string verdict = null;
            var session = Session((_, __) => Envelope(Page(CompletedWithCandidates("starter_driver", "starter_truck"))));
            Assert.IsTrue(EmulatorContractsDtoProbe.TryBeginVerify(
                true, true, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid, session, false, 1,
                (v, e, g) =>
                {
                    verdict = v;
                    evidence = e;
                },
                out _));
            Assert.AreEqual(EmulatorContractsDtoProbe.DtoVerified, verdict);
            StringAssert.Contains("driver=starter_driver", evidence);
            StringAssert.Contains("truck=starter_truck", evidence);
            Assert.AreEqual(1, session.InvokeCount);
        }

        [Test]
        public void AuthSwitch_DoesNotApplyStaleResult()
        {
            _settings = ValidLocal();
            Action<object> pending = null;
            var session = new ClientReadSession();
            session.SetAsyncInvoker((name, payload, ok, fail) => { pending = ok; });
            var applied = "";
            var gen = 1;
            Assert.IsTrue(EmulatorContractsDtoProbe.TryBeginVerify(
                true, true, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid, session, false, gen,
                (v, e, g) =>
                {
                    if (g != gen)
                        return;
                    applied = v;
                },
                out _));
            gen = 2;
            pending(Envelope(Page(CompletedWithCandidates())));
            Assert.AreEqual("", applied);
            Assert.AreEqual(1, session.InvokeCount);
        }

        [Test]
        public void CommandsAreNotInvoked()
        {
            _settings = ValidLocal();
            var names = new List<string>();
            var session = Session((name, payload) =>
            {
                names.Add(name);
                Assert.AreNotEqual(ClientCallableCatalog.AcceptContract, name);
                Assert.AreNotEqual(ClientCallableCatalog.StartDelivery, name);
                Assert.AreNotEqual(ClientCallableCatalog.ClaimDeliveryReward, name);
                return Envelope(Page(CompletedWithCandidates()));
            });
            EmulatorContractsDtoProbe.TryBeginVerify(
                true, true, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid, session, false, 1,
                (v, e, g) => { }, out _);
            var command = session.Read(
                ClientCallableCatalog.AcceptContract,
                ReadyStatus(),
                true,
                ClientValue.ReadRequest());
            Assert.AreEqual(ClientCallableFault.NonRetryableCommand, command.Fault);
            Assert.AreEqual(1, names.Count);
            Assert.AreEqual(ClientCallableCatalog.GetMyContracts, names[0]);
        }

        [Test]
        public void Snapshot_ShowsNoActiveUserOutsidePlay()
        {
            _settings = ValidLocal();
            var snap = EmulatorContractsDtoProbe.Snapshot(false, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid);
            Assert.AreEqual(EmulatorContractsDtoProbe.NoActiveUser, snap.AuthDisplay);
            Assert.IsFalse(snap.SignedIn);
        }

        [Test]
        public void CorruptCandidates_ParserHold()
        {
            _settings = ValidLocal();
            var bad = CompletedBare();
            bad["candidateAssets"] = new List<Dictionary<string, object>>
            {
                Map("assetId", "starter_truck", "kind", "truck", "catalogId", "x"),
                Map("assetId", "starter_driver", "kind", "driver")
            };
            var session = Session((_, __) => Envelope(Page(bad)));
            string verdict = null;
            EmulatorContractsDtoProbe.TryBeginVerify(
                true, true, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid, session, false, 1,
                (v, e, g) => verdict = v, out _);
            Assert.AreEqual(EmulatorContractsDtoProbe.ParserHold, verdict);
        }

        [Test]
        public void FailedRead_FunctionsReadError()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => { throw new InvalidOperationException("transport"); });
            string verdict = null;
            Assert.IsTrue(EmulatorContractsDtoProbe.TryBeginVerify(
                true, true, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid, session, false, 1,
                (v, e, g) => verdict = v, out _));
            Assert.AreEqual(EmulatorContractsDtoProbe.FunctionsReadError, verdict);
        }

        [Test]
        public void FailureEvidence_ExcludesRawExceptionAndKeepsStage()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => { throw new InvalidOperationException("SECRET_TOKEN_PRIVATE_BODY"); });
            var outcome = session.Read(ClientCallableCatalog.GetMyContracts, ReadyStatus(), true, ClientValue.ReadRequest());
            Assert.AreEqual(EmulatorContractsDtoProbe.FunctionsReadError, EmulatorContractsDtoProbe.Evaluate(outcome, out var evidence));
            Assert.AreEqual("stage=Transport; fault=NetworkUnavailable; exception=InvalidOperationException; functions=None", evidence);
            Assert.AreEqual(1, session.InvokeCount);
            StringAssert.DoesNotContain("SECRET", evidence);
        }

        [Test]
        public void FailureEvidence_FunctionsCodeAndEnvelopeAreDistinct()
        {
            var failed = new ClientReadOutcome(ClientCallableFault.ServerUnavailable, default, 0,
                ClientReadFailureStage.Transport, ClientReadExceptionKind.FunctionsException,
                Firebase.Functions.FunctionsErrorCode.Unauthenticated);
            EmulatorContractsDtoProbe.Evaluate(failed, out var evidence);
            StringAssert.Contains("functions=Unauthenticated", evidence);
            var session = Session((_, __) => "PRIVATE_RESPONSE_BODY");
            var parsed = session.Read(ClientCallableCatalog.GetMyContracts, ReadyStatus(), true, ClientValue.ReadRequest());
            Assert.AreEqual(ClientReadFailureStage.Envelope, parsed.FailureStage);
            EmulatorContractsDtoProbe.Evaluate(parsed, out evidence);
            StringAssert.DoesNotContain("PRIVATE", evidence);
            var unknown = new ClientReadOutcome(ClientCallableFault.ServerUnavailable, default, 0,
                ClientReadFailureStage.Transport, ClientReadExceptionKind.FunctionsException,
                (Firebase.Functions.FunctionsErrorCode)999);
            Assert.IsNull(unknown.FunctionsCode);
        }

        [Test]
        public void NotPlaying_DisablesVerify()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => Envelope(Page()));
            Assert.IsFalse(EmulatorContractsDtoProbe.TryCanVerify(
                false, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid, session, false, out _));
            Assert.AreEqual(0, session.InvokeCount);
        }

        [Test]
        public void ClickRequired()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => Envelope(Page()));
            Assert.IsFalse(EmulatorContractsDtoProbe.TryBeginVerify(
                false, true, _settings, ReadyStatus(), EmulatorAuthTestGate.ExpectedUid, session, false, 1,
                (v, e, g) => { }, out var hold));
            Assert.AreEqual(EmulatorAuthTestGate.ExplicitActionRequired, hold);
            Assert.AreEqual(0, session.InvokeCount);
        }

        static ClientReadSession Session(ClientReadInvoker invoker)
        {
            var session = new ClientReadSession();
            session.SetInvoker(invoker);
            return session;
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
            return Map("protocolVersion", 1, "serverTime", "2026-11-07T20:55:00.000Z", "result", result);
        }

        static Dictionary<string, object> Page(params Dictionary<string, object>[] items)
        {
            return Map("items", items, "nextCursor", null);
        }

        static Dictionary<string, object> CompletedBare()
        {
            return Map(
                "contractId", EmulatorContractsDtoProbe.CompletedContractId,
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

        static Dictionary<string, object> CompletedWithCandidates(string driver = "starter_driver", string truck = "starter_truck")
        {
            var row = CompletedBare();
            row["candidateAssets"] = new List<Dictionary<string, object>>
            {
                Map("assetId", driver, "kind", "driver"),
                Map("assetId", truck, "kind", "truck")
            };
            return row;
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
