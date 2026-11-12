using System;
using System.Collections.Generic;
using LogistiCore.Backend;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class ClientJobsRuntimeTests
    {
        const string Uid = "uid-runtime";
        const string CompletedId = "9298bef2f37fca8a0ee1de263803936763390db67afb18b63835ecb1e0e69c54";
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
        public void MissingLocalEnvironment_StaysOffline()
        {
            var shared = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            try
            {
                shared.Configure(ClientBackendEnvironment.Unconfigured);
                var selected = ClientEnvironmentSettings.Select(null, shared);
                Assert.AreEqual(ClientConnectionDecision.StayOffline, ClientConnectionPolicy.Decide(selected.ToRequest()));
                Assert.IsFalse(ClientLocalEmulatorSecurity.TryValidate(selected, out _));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shared);
            }
        }

        [Test]
        public void InvalidProjectId_FailsClosed()
        {
            _settings = Local("other-project", "127.0.0.1");
            Assert.IsFalse(ClientLocalEmulatorSecurity.TryValidate(_settings, out var fault));
            Assert.AreEqual(ClientLocalEmulatorSecurity.InvalidProjectDetail, fault);
            AssertNoRemoteCall();
        }

        [Test]
        public void NonLoopbackEndpoint_FailsClosed()
        {
            _settings = Local(ClientProtocol.DemoProjectId, "8.8.8.8");
            Assert.IsFalse(ClientLocalEmulatorSecurity.TryValidate(_settings, out var fault));
            Assert.AreEqual(ClientLocalEmulatorSecurity.NonLoopbackDetail, fault);
            AssertNoRemoteCall();
        }

        [Test]
        public void MissingConnectionAuthorization_DoesNotCallRemote()
        {
            _settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            _settings.Configure(ClientBackendEnvironment.Unconfigured);
            AssertNoRemoteCall();
        }

        [Test]
        public void AuthNotReady_DoesNotRead()
        {
            _settings = ValidLocal();
            var invokes = 0;
            var session = Session((_, __) =>
            {
                invokes++;
                return Envelope(Page());
            });
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth("");
            Assert.AreEqual(0, invokes);
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
        }

        [Test]
        public void ValidAuthLifecycle_ReadsContracts()
        {
            _settings = ValidLocal();
            var session = Session((name, payload) =>
            {
                Assert.IsTrue(ClientCallableCatalog.IsRead(name), name);
                Assert.AreNotEqual(ClientCallableCatalog.AcceptContract, name);
                return Envelope(Page(CompletedIzmirIstanbul()));
            });
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth(Uid);
            Assert.AreEqual(4, session.InvokeCount);
            Assert.AreEqual(MapJobsStatus.Ready, MapJobsStore.Current.Status);
            Assert.IsTrue(MapJobsStore.Current.TryGet(CompletedId, out var item));
            Assert.IsTrue(item.InCategory(MapJobCategory.History));
        }

        [Test]
        public void OpenJobsView_RefreshesAfterAuth()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => Envelope(Page(CompletedIzmirIstanbul())));
            var root = new GameObject("JobsRuntimePanel", typeof(RectTransform));
            try
            {
                var view = MapJobsView.Attach((RectTransform)root.transform);
                Assert.AreEqual(MapJobsStatus.Unconfigured, view.Status);
                ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
                ClientJobsRuntime.NotifyAuth(Uid);
                Assert.AreEqual(MapJobsStatus.Ready, view.Status);
                Assert.IsFalse(view.FixtureVisible);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CompletedContract_IsHistoryNotAvailable()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => Envelope(Page(CompletedIzmirIstanbul())));
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth(Uid);
            MapJobsStore.Current.TryGet(CompletedId, out var item);
            Assert.IsTrue(item.InCategory(MapJobCategory.History));
            Assert.IsFalse(item.InCategory(MapJobCategory.Available));
        }

        [Test]
        public void LiveMode_IgnoresFixtureOverride()
        {
            _settings = ValidLocal();
            MapJobsStore.Override(MapJobsSnapshot.Fixture(
                MapJobsStatus.Ready,
                new[] { new MapJobsItem("fixture_1", MapJobContractStatus.Offered, false, "izmir", "istanbul", "cargo", "1", "kg", "1", "LGC", 2, "", MapJobDeliveryStatus.None, "", "") }));
            Assert.IsTrue(MapJobsStore.Current.Synthetic);
            var session = Session((_, __) => Envelope(Page(CompletedIzmirIstanbul())));
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth(Uid);
            Assert.IsFalse(MapJobsStore.Current.Synthetic);
            Assert.IsTrue(MapJobsStore.Current.TryGet(CompletedId, out _));
            Assert.IsFalse(MapJobsStore.Current.TryGet("fixture_1", out _));
        }

        [Test]
        public void FunctionsInvokerError_IsErrorNotEmpty()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => throw new InvalidOperationException("offline"));
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth(Uid);
            Assert.AreEqual(MapJobsStatus.Error, MapJobsStore.Current.Status);
            Assert.AreNotEqual(MapJobsStatus.Empty, MapJobsStore.Current.Status);
        }

        [Test]
        public void SignOut_ClearsTrustedJobs()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => Envelope(Page(CompletedIzmirIstanbul())));
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth(Uid);
            ClientJobsRuntime.NotifyAuth("");
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
        }

        [Test]
        public void StaleAsyncUid_IsIgnored()
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
            Assert.IsTrue(MapJobsContractsRead.InFlight);
            ClientJobsRuntime.NotifyAuth("uid-b");
            Assert.IsNotNull(pending);
            pending(Envelope(Page(CompletedIzmirIstanbul())));
            Assert.AreEqual("uid-b", MapJobsContractsRead.Uid);
            Assert.AreEqual(MapJobsStatus.Empty, MapJobsStore.Current.Status);
            Assert.IsFalse(MapJobsStore.Current.TryGet(CompletedId, out _));
        }

        [Test]
        public void DuplicateRefresh_IsCoalesced()
        {
            _settings = ValidLocal();
            var session = Session((_, __) => Envelope(Page(CompletedIzmirIstanbul())));
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth(Uid);
            MapJobsContractsRead.TryRefresh();
            MapJobsContractsRead.TryRefresh();
            Assert.AreEqual(4, session.InvokeCount);
        }

        [Test]
        public void FirstPlay_ReadyEvent_AttachesOnce()
        {
            _settings = ValidLocal();
            var names = new List<string>();
            PrepareReplay(names);
            ClientJobsRuntime.InstallForTests();
            Assert.IsFalse(ClientJobsRuntime.LiveAttached);
            MockReadyStart();
            Assert.AreEqual(1, FirebaseClient.ConnectAttempts);
            Assert.IsTrue(ClientJobsRuntime.LiveAttached);
            Assert.IsNotNull(ClientJobsRuntime.Session);
            ClientJobsRuntime.NotifyAuth(Uid);
            Assert.AreEqual(4, names.Count);
            Assert.AreEqual(1, CountName(names, ClientCallableCatalog.GetMyContracts));
        }

        [Test]
        public void DomainReloadDisabled_SecondAndThirdPlay_ReplayReadyWithoutSecondConnect()
        {
            _settings = ValidLocal();
            var names = new List<string>();
            PrepareReplay(names);
            ClientJobsRuntime.InstallForTests();
            MockReadyStart();
            Assert.AreEqual(1, FirebaseClient.ConnectAttempts);
            ClientJobsRuntime.NotifyAuth(Uid);
            Assert.AreEqual(4, names.Count);
            for (var play = 2; play <= 3; play++)
            {
                ClientJobsRuntime.OverrideSettingsForTests(_settings);
                ClientJobsRuntime.SetAsyncInvokerForTests(Async(names));
                ClientJobsRuntime.EnterPlaySessionForTests();
                FirebaseClient.Start(_settings);
                Assert.AreEqual(1, FirebaseClient.ConnectAttempts, "play " + play);
                Assert.IsTrue(ClientJobsRuntime.LiveAttached, "play " + play);
                ClientJobsRuntime.NotifyAuth(Uid);
                Assert.AreEqual(play * 4, names.Count, "play " + play);
                Assert.AreEqual(play, CountName(names, ClientCallableCatalog.GetMyContracts), "play " + play);
            }
        }

        [Test]
        public void AuthHooked_ReplaysExistingUidWithoutDuplicateSubscription()
        {
            _settings = ValidLocal();
            var names = new List<string>();
            PrepareReplay(names);
            ClientJobsRuntime.InstallForTests();
            MockReadyStart();
            ClientJobsRuntime.NotifyAuth(Uid);
            var hooks = ClientJobsRuntime.AuthSubscriptions;
            ClientJobsRuntime.EnterPlaySessionForTests();
            Assert.AreEqual(hooks, ClientJobsRuntime.AuthSubscriptions);
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
            ClientJobsRuntime.NotifyAuth(Uid);
            Assert.AreEqual(hooks, ClientJobsRuntime.AuthSubscriptions);
            Assert.AreEqual(8, names.Count);
            Assert.AreEqual(MapJobsStatus.Ready, MapJobsStore.Current.Status);
        }

        [Test]
        public void EnterPlaySession_DoesNotAutoLogin()
        {
            _settings = ValidLocal();
            var names = new List<string>();
            PrepareReplay(names);
            ClientJobsRuntime.InstallForTests();
            MockReadyStart();
            ClientJobsRuntime.EnterPlaySessionForTests();
            Assert.AreEqual(0, names.Count);
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
        }

        [Test]
        public void EnterPlaySession_DropsStaleReadFromPreviousPlay()
        {
            _settings = ValidLocal();
            Action<object> pending = null;
            ClientJobsRuntime.OverrideSettingsForTests(_settings);
            ClientJobsRuntime.SetAsyncInvokerForTests((name, payload, ok, fail) =>
            {
                pending = ok;
            });
            ClientJobsRuntime.InstallForTests();
            MockReadyStart();
            ClientJobsRuntime.NotifyAuth(Uid);
            Assert.IsNotNull(pending);
            var stale = pending;
            pending = null;
            ClientJobsRuntime.SetAsyncInvokerForTests((name, payload, ok, fail) =>
            {
                ok(Envelope(Page()));
            });
            ClientJobsRuntime.EnterPlaySessionForTests();
            ClientJobsRuntime.NotifyAuth(Uid);
            stale(Envelope(Page(CompletedIzmirIstanbul())));
            Assert.AreEqual(MapJobsStatus.Empty, MapJobsStore.Current.Status);
            Assert.IsFalse(MapJobsStore.Current.TryGet(CompletedId, out _));
        }

        [Test]
        public void Replay_DoesNotAttachWhenStayOffline()
        {
            _settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            _settings.Configure(ClientBackendEnvironment.Unconfigured);
            var names = new List<string>();
            PrepareReplay(names);
            ClientJobsRuntime.InstallForTests();
            FirebaseClient.Start(_settings);
            ClientJobsRuntime.InstallForTests();
            Assert.IsFalse(ClientJobsRuntime.LiveAttached);
            Assert.IsNull(ClientJobsRuntime.Session);
            Assert.AreEqual(0, names.Count);
            Assert.AreEqual(0, FirebaseClient.ConnectAttempts);
        }

        [Test]
        public void Replay_WrongProject_DoesNotAttach()
        {
            _settings = Local("other-project", "127.0.0.1");
            var names = new List<string>();
            PrepareReplay(names);
            ClientJobsRuntime.InstallForTests();
            MockReadyStart();
            ClientJobsRuntime.OverrideSettingsForTests(_settings);
            ClientJobsRuntime.SetAsyncInvokerForTests(Async(names));
            ClientJobsRuntime.InstallForTests();
            Assert.IsFalse(ClientJobsRuntime.LiveAttached);
            Assert.AreEqual(0, names.Count);
        }

        [Test]
        public void Replay_AlreadySignedIn_StartsJobsRead()
        {
            _settings = ValidLocal();
            var names = new List<string>();
            PrepareReplay(names);
            MockReadyStart();
            ClientJobsRuntime.InstallForTests();
            Assert.IsTrue(ClientJobsRuntime.LiveAttached);
            ClientJobsRuntime.NotifyAuth(Uid);
            Assert.AreEqual(4, names.Count);
            Assert.AreEqual(MapJobsStatus.Ready, MapJobsStore.Current.Status);
        }

        [Test]
        public void Replay_LateSignIn_UsesExistingSession()
        {
            _settings = ValidLocal();
            var names = new List<string>();
            PrepareReplay(names);
            MockReadyStart();
            ClientJobsRuntime.InstallForTests();
            var session = ClientJobsRuntime.Session;
            ClientJobsRuntime.NotifyAuth(Uid);
            Assert.AreSame(session, ClientJobsRuntime.Session);
            Assert.AreEqual(4, names.Count);
        }

        [Test]
        public void Replay_ReadyTwice_DoesNotDuplicateReadOrAuthSubscribe()
        {
            _settings = ValidLocal();
            var names = new List<string>();
            PrepareReplay(names);
            MockReadyStart();
            ClientJobsRuntime.InstallForTests();
            var hooks = ClientJobsRuntime.AuthSubscriptions;
            ClientJobsRuntime.NotifyAuth(Uid);
            ClientJobsRuntime.OnBackendStatus(FirebaseClient.Current);
            ClientJobsRuntime.OnBackendStatus(FirebaseClient.Current);
            Assert.AreEqual(hooks, ClientJobsRuntime.AuthSubscriptions);
            Assert.AreEqual(4, names.Count);
            Assert.AreSame(ClientJobsRuntime.Session, ClientJobsRuntime.Session);
        }

        [Test]
        public void CommandsAreNotInvoked()
        {
            _settings = ValidLocal();
            var names = new List<string>();
            var session = Session((name, payload) =>
            {
                names.Add(name);
                Assert.IsTrue(ClientCallableCatalog.IsRead(name));
                return Envelope(Page());
            });
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth(Uid);
            var command = session.Read(
                ClientCallableCatalog.AcceptContract,
                ReadyStatus(),
                true,
                ClientValue.ReadRequest());
            Assert.AreEqual(ClientCallableFault.NonRetryableCommand, command.Fault);
            Assert.AreEqual(4, names.Count);
            Assert.AreEqual(1, CountName(names, ClientCallableCatalog.GetMyContracts));
            Assert.AreEqual(1, CountName(names, ClientCallableCatalog.GetMyFleetState));
            Assert.AreEqual(1, CountName(names, ClientCallableCatalog.GetMyDeliveries));
            Assert.AreEqual(1, CountName(names, ClientCallableCatalog.GetMyEconomyState));
        }

        void AssertNoRemoteCall()
        {
            var session = Session((_, __) =>
            {
                Assert.Fail("remote call");
                return null;
            });
            ClientJobsRuntime.BindForTests(ReadyStatus(), session, _settings);
            ClientJobsRuntime.NotifyAuth(Uid);
            Assert.AreEqual(0, session.InvokeCount);
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
        }

        static int CountName(List<string> names, string callable)
        {
            var count = 0;
            for (var i = 0; i < names.Count; i++)
            {
                if (names[i] == callable)
                    count++;
            }

            return count;
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

        void PrepareReplay(List<string> names)
        {
            ClientJobsRuntime.OverrideSettingsForTests(_settings);
            ClientJobsRuntime.SetAsyncInvokerForTests(Async(names));
        }

        void MockReadyStart()
        {
            FirebaseClient.Connect = (settings, decision, completed) =>
                completed(FirebaseClient.ClientSdkResult.Ready);
            FirebaseClient.Start(_settings);
        }

        static ClientReadAsyncInvoker Async(List<string> names)
        {
            return (name, payload, ok, fail) =>
            {
                names.Add(name);
                ok(Envelope(Page(CompletedIzmirIstanbul())));
            };
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
            return Map("protocolVersion", 1, "serverTime", "2026-11-07T00:33:00.000Z", "result", result);
        }

        static Dictionary<string, object> Page(params Dictionary<string, object>[] items)
        {
            return Map("items", items, "nextCursor", null);
        }

        static Dictionary<string, object> CompletedIzmirIstanbul()
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
