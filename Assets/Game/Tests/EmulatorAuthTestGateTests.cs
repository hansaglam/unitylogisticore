using System;
using System.Collections.Generic;
using System.IO;
using LogistiCore.Backend;
using LogistiCore.EditorTools;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class EmulatorAuthTestGateTests
    {
        const string Secret = "01e-never-persist-secret-xyz";
        ClientEnvironmentSettings _settings;

        [SetUp]
        public void SetUp()
        {
            FirebaseClient.ResetForTests();
            ClientConnectionAuthorization.ResetForTests();
            ClientJobsRuntime.ResetForTests();
            MapJobsStore.ClearOverride();
            MapJobsContractsRead.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null)
                UnityEngine.Object.DestroyImmediate(_settings);
            SetUp();
        }

        [Test]
        public void GateLivesInEditorAssemblyOnly()
        {
            Assert.AreEqual("LogistiCore.Editor", typeof(EmulatorAuthTestGate).Assembly.GetName().Name);
            Assert.AreEqual("LogistiCore/Dev/Emulator Auth Test", EmulatorAuthTestGate.MenuPath);
        }

        [Test]
        public void PlayModeRequired_OutsidePlayMode()
        {
            _settings = ValidLocal();
            var signed = new[] { 0 };
            Assert.IsFalse(EmulatorAuthTestGate.TrySignInExisting(
                true, false, _settings, ReadyStatus(), Email(), Secret, Track(signed), null, out _, out var fault));
            Assert.AreEqual(EmulatorAuthTestGate.PlayModeRequired, fault);
            Assert.AreEqual(0, signed[0]);
        }

        [Test]
        public void ExplicitClickRequired()
        {
            _settings = ValidLocal();
            var signed = new[] { 0 };
            Assert.IsFalse(EmulatorAuthTestGate.TrySignInExisting(
                false, true, _settings, ReadyStatus(), Email(), Secret, Track(signed), null, out _, out var fault));
            Assert.AreEqual(EmulatorAuthTestGate.ExplicitActionRequired, fault);
            Assert.AreEqual(0, signed[0]);
        }

        [Test]
        public void WrongProjectId_IsRejected()
        {
            _settings = Local("other-project", "127.0.0.1");
            Assert.IsFalse(EmulatorAuthTestGate.TryAllow(true, _settings, ReadyStatus(), out var fault));
            Assert.AreEqual(ClientLocalEmulatorSecurity.InvalidProjectDetail, fault);
        }

        [Test]
        public void NonLoopback_IsRejected()
        {
            _settings = Local(ClientProtocol.DemoProjectId, "8.8.8.8");
            Assert.IsFalse(EmulatorAuthTestGate.TryAllow(true, _settings, ReadyStatus(), out var fault));
            Assert.AreEqual(ClientLocalEmulatorSecurity.NonLoopbackDetail, fault);
        }

        [Test]
        public void ProductionSettings_AreRejected()
        {
            _settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            _settings.Configure(
                ClientBackendEnvironment.Production,
                ClientProtocol.DemoProjectId,
                "app",
                "key",
                "127.0.0.1",
                ClientLocalEmulatorSecurity.FirestorePort,
                ClientLocalEmulatorSecurity.AuthPort,
                ClientLocalEmulatorSecurity.FunctionsOrigin,
                ClientProtocol.FunctionsRegion);
            Assert.IsFalse(EmulatorAuthTestGate.TryAllow(true, _settings, ReadyStatus(), out var fault));
            Assert.AreEqual(ClientConnectionPolicy.ProductionDetail, fault);
        }

        [Test]
        public void ProductionStatus_IsRejected()
        {
            _settings = ValidLocal();
            var status = new ClientBackendStatus(
                ClientBackendEnvironment.Production,
                ClientConnectionDecision.Refused,
                ClientBackendPhase.Refused,
                ClientConnectionPolicy.ProductionDetail);
            Assert.IsFalse(EmulatorAuthTestGate.TryAllow(true, _settings, status, out var fault));
            Assert.AreEqual(ClientConnectionPolicy.ProductionDetail, fault);
        }

        [Test]
        public void StayOffline_IsRejected()
        {
            _settings = ValidLocal();
            var status = new ClientBackendStatus(
                ClientBackendEnvironment.Unconfigured,
                ClientConnectionDecision.StayOffline,
                ClientBackendPhase.Offline,
                ClientConnectionPolicy.OfflineDetail);
            Assert.IsFalse(EmulatorAuthTestGate.TryAllow(true, _settings, status, out var fault));
            Assert.AreEqual(ClientConnectionPolicy.OfflineDetail, fault);
        }

        [Test]
        public void MissingLocalEnvironment_IsRejected()
        {
            Assert.IsFalse(EmulatorAuthTestGate.TryAllow(true, null, ReadyStatus(), out var fault));
            Assert.AreEqual(ClientLocalEmulatorSecurity.MissingEndpointDetail, fault);
        }

        [Test]
        public void UnconfiguredSharedEnvironment_IsRejected()
        {
            _settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            _settings.Configure(ClientBackendEnvironment.Unconfigured);
            Assert.IsFalse(EmulatorAuthTestGate.TryAllow(true, _settings, ReadyStatus(), out var fault));
            Assert.AreEqual(ClientConnectionPolicy.MissingEmulatorDetail, fault);
        }

        [Test]
        public void NonSyntheticEmail_IsRejected()
        {
            _settings = ValidLocal();
            var signed = new[] { 0 };
            Assert.IsFalse(EmulatorAuthTestGate.TrySignInExisting(
                true, true, _settings, ReadyStatus(), "other@example.test", Secret, Track(signed), null, out _, out var fault));
            Assert.AreEqual(EmulatorAuthTestGate.EmailNotAllowed, fault);
            Assert.AreEqual(0, signed[0]);
        }

        [Test]
        public void UnexpectedUid_SignsOut()
        {
            _settings = ValidLocal();
            var signedOut = 0;
            Assert.IsFalse(EmulatorAuthTestGate.TrySignInExisting(
                true,
                true,
                _settings,
                ReadyStatus(),
                Email(),
                Secret,
                (email, password) => "other-uid",
                () => signedOut++,
                out _,
                out var fault));
            Assert.AreEqual(EmulatorAuthTestGate.UnexpectedAccount, fault);
            Assert.AreEqual(1, signedOut);
        }

        [Test]
        public void SignInException_DoesNotLeakSecret()
        {
            _settings = ValidLocal();
            Assert.IsFalse(EmulatorAuthTestGate.TrySignInExisting(
                true,
                true,
                _settings,
                ReadyStatus(),
                Email(),
                Secret,
                (email, password) => throw new InvalidOperationException(password),
                null,
                out _,
                out var fault));
            Assert.AreEqual(EmulatorAuthTestGate.SignInFailed, fault);
            Assert.IsFalse(fault.Contains(Secret));
        }

        [Test]
        public void CredentialsAreNotPersisted()
        {
            _settings = ValidLocal();
            string uid;
            Assert.IsTrue(EmulatorAuthTestGate.TrySignInExisting(
                true,
                true,
                _settings,
                ReadyStatus(),
                Email(),
                Secret,
                (email, password) => EmulatorAuthTestGate.ExpectedUid,
                null,
                out uid,
                out var fault));
            Assert.IsNull(fault);
            Assert.AreEqual(EmulatorAuthTestGate.ExpectedUid, uid);
            Assert.AreNotEqual(Secret, EditorPrefs.GetString("EmulatorAuthPassword"));
            Assert.AreNotEqual(Secret, PlayerPrefs.GetString("EmulatorAuthPassword"));
            AssertNoSecretOnDisk();
        }

        [Test]
        public void SignInDoesNotBypassAuthLifecycle()
        {
            _settings = ValidLocal();
            var invokes = 0;
            var session = new ClientReadSession();
            session.SetInvoker((_, __) =>
            {
                invokes++;
                return Envelope(Page(Completed()));
            });
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            Assert.IsTrue(EmulatorAuthTestGate.TrySignInExisting(
                true,
                true,
                _settings,
                ReadyStatus(),
                Email(),
                Secret,
                (email, password) => EmulatorAuthTestGate.ExpectedUid,
                null,
                out _,
                out _));
            Assert.AreEqual(0, invokes);
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);

            ClientJobsRuntime.NotifyAuth(EmulatorAuthTestGate.ExpectedUid);
            Assert.AreEqual(4, invokes);
            Assert.AreEqual(MapJobsStatus.Ready, MapJobsStore.Current.Status);
            Assert.IsTrue(MapJobsStore.Current.TryGet(CompletedId, out var job));
            Assert.IsTrue(job.InCategory(MapJobCategory.History));

            ClientJobsRuntime.NotifyAuth("");
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
            Assert.IsFalse(MapJobsStore.Current.TryGet(CompletedId, out _));
        }

        [Test]
        public void StaleUidResponse_IsRejected()
        {
            _settings = ValidLocal();
            Action<object> pending = null;
            var n = 0;
            var session = new ClientReadSession();
            session.SetAsyncInvoker((name, payload, ok, fail) =>
            {
                n++;
                if (n == 1)
                {
                    pending = ok;
                    return;
                }

                ok(Envelope(Page()));
            });
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth("uid-a");
            ClientJobsRuntime.NotifyAuth(EmulatorAuthTestGate.ExpectedUid);
            pending(Envelope(Page(Completed())));
            Assert.AreEqual(EmulatorAuthTestGate.ExpectedUid, MapJobsContractsRead.Uid);
            Assert.AreEqual(MapJobsStatus.Empty, MapJobsStore.Current.Status);
            Assert.IsFalse(MapJobsStore.Current.TryGet(CompletedId, out _));
        }

        [Test]
        public void ReadSessionStillRefusesCommands()
        {
            _settings = ValidLocal();
            var names = new List<string>();
            var session = new ClientReadSession();
            session.SetInvoker((name, payload) =>
            {
                names.Add(name);
                return Envelope(Page());
            });
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth(EmulatorAuthTestGate.ExpectedUid);
            var command = session.Read(
                ClientCallableCatalog.AcceptContract,
                ReadyStatus(),
                true,
                ClientValue.ReadRequest());
            Assert.AreEqual(ClientCallableFault.NonRetryableCommand, command.Fault);
            Assert.AreEqual(4, names.Count);
            CollectionAssert.Contains(names, ClientCallableCatalog.GetMyContracts);
        }

        [Test]
        public void ApprovedFlagIsNotSet()
        {
            _settings = ValidLocal();
            Assert.IsFalse(ClientConnectionAuthorization.Approved);
            EmulatorAuthTestGate.TryAllow(true, _settings, ReadyStatus(), out _);
            Assert.IsFalse(ClientConnectionAuthorization.Approved);
        }

        static EmulatorAuthExistingSignIn Track(int[] signed)
        {
            return (email, password) =>
            {
                signed[0]++;
                return EmulatorAuthTestGate.ExpectedUid;
            };
        }

        void AssertNoSecretOnDisk()
        {
            var local = Path.Combine(Application.dataPath, "Game/Backend/Resources/LocalClientEnvironment.asset");
            if (File.Exists(local))
                Assert.IsFalse(File.ReadAllText(local).Contains(Secret));
            var editor = Path.Combine(Application.dataPath, "Game/Editor/EmulatorAuthTestWindow.cs");
            Assert.IsFalse(File.ReadAllText(editor).Contains(Secret));
            Assert.IsFalse(File.ReadAllText(editor).Contains(Secret.ToLowerInvariant()));
        }

        static string Email()
        {
            return ClientEmulatorAuthCredentials.Email;
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

        const string CompletedId = "9298bef2f37fca8a0ee1de263803936763390db67afb18b63835ecb1e0e69c54";

        static Dictionary<string, object> Envelope(Dictionary<string, object> result)
        {
            return Map("protocolVersion", 1, "serverTime", "2026-11-07T00:33:00.000Z", "result", result);
        }

        static Dictionary<string, object> Page(params Dictionary<string, object>[] items)
        {
            return Map("items", items, "nextCursor", null);
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

        static Dictionary<string, object> Map(params object[] pairs)
        {
            var map = new Dictionary<string, object>();
            for (var i = 0; i < pairs.Length; i += 2)
                map[(string)pairs[i]] = pairs[i + 1];
            return map;
        }
    }
}
