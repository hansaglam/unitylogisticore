using System;
using Firebase.Auth;
using UnityEngine;

namespace LogistiCore.Backend
{
    /// <summary>
    /// Binds Firebase Auth + a read-only session to MapJobsContractsRead.
    /// Commands use a separate session and are never auto-dispatched.
    /// </summary>
    public static class ClientJobsRuntime
    {
        static ClientEnvironmentSettings _settings;
        static ClientEnvironmentSettings _settingsOverride;
        static ClientReadAsyncInvoker _testAsyncInvoker;
        static ClientCommandAsyncInvoker _testCommandAsync;
        static ClientBackendStatus _status;
        static ClientReadSession _session;
        static ClientCommandSession _commands;
        static bool _liveAttached;
        static bool _authHooked;
        static FirebaseAuth _auth;
        static int _statusEvents;
        static int _authSubscriptions;

        public static ClientEnvironmentSettings Settings => _settings;
        public static bool LiveAttached => _liveAttached;
        internal static bool AuthHooked => _authHooked;
        internal static int StatusEvents => _statusEvents;
        internal static int AuthSubscriptions => _authSubscriptions;
        internal static ClientReadSession Session => _session;
        internal static ClientCommandSession Commands => _commands;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Install()
        {
            ResetForTests();
            ListenAndReplay();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnterPlaySession()
        {
#if UNITY_EDITOR
            ClientReadLifecycleTrace.BeginSession();
#endif

            Map.MapJobsContractsRead.BeginPlaySession();
            Map.MapFleetStateRead.BeginPlaySession();
            Map.MapRoadDeliveriesRead.BeginPlaySession();
            Map.MapCompanyEconomyRead.BeginPlaySession();
            Map.MapJobsAccept.BeginPlaySession();
            _session = null;
            _commands = null;
            ListenAndReplay();
        }

        static void ListenAndReplay()
        {
            FirebaseClient.StatusChanged -= OnBackendStatus;
            FirebaseClient.StatusChanged += OnBackendStatus;
            OnBackendStatus(FirebaseClient.Current);
        }

        public static void ResetForTests()
        {
#if UNITY_EDITOR
            ClientReadLifecycleTrace.Reset();
#endif

            UnhookAuth();
            _settings = null;
            _settingsOverride = null;
            _testAsyncInvoker = null;
            _testCommandAsync = null;
            ClientFunctionsCommandInvoker.ResetForTests();
            _status = default;
            _session = null;
            _commands = null;
            _liveAttached = false;
            _statusEvents = 0;
            _authSubscriptions = 0;
            Map.MapJobsContractsRead.ResetForTests();
            Map.MapFleetStateRead.ResetForTests();
            Map.MapRoadDeliveriesRead.ResetForTests();
            Map.MapCompanyEconomyRead.ResetForTests();
            Map.MapJobsAccept.ResetForTests();
        }

        internal static void OverrideSettingsForTests(ClientEnvironmentSettings settings)
        {
            _settingsOverride = settings;
        }

        internal static void SetAsyncInvokerForTests(ClientReadAsyncInvoker invoker)
        {
            _testAsyncInvoker = invoker;
        }

        internal static void SetCommandInvokerForTests(ClientCommandInvoker invoker)
        {
            if (_commands == null)
                _commands = new ClientCommandSession();
            _commands.SetInvoker(invoker);
        }

        internal static void SetCommandAsyncInvokerForTests(ClientCommandAsyncInvoker invoker)
        {
            _testCommandAsync = invoker;
            if (_commands == null)
                _commands = new ClientCommandSession();
            _commands.SetAsyncInvoker(invoker);
        }

        internal static void InstallForTests()
        {
            UnhookAuth();
            _settings = null;
            _status = default;
            _session = null;
            _commands = null;
            _liveAttached = false;
            _statusEvents = 0;
            _authSubscriptions = 0;
            ListenAndReplay();
        }

        internal static void EnterPlaySessionForTests()
        {
            EnterPlaySession();
        }

        public static void BindForTests(
            ClientBackendStatus status,
            ClientReadSession session,
            ClientEnvironmentSettings settings)
        {
            UnhookAuth();
            _status = status;
            _session = session;
            _commands = new ClientCommandSession();
            _settings = settings;
            _liveAttached = ClientLocalEmulatorSecurity.AllowsLiveReads(settings, status);
        }

        public static void NotifyAuth(string uid)
        {
            if (_session == null)
            {
                Map.MapJobsContractsRead.SignOut();
                Map.MapFleetStateRead.SignOut();
                Map.MapRoadDeliveriesRead.SignOut();
                Map.MapCompanyEconomyRead.SignOut();
                Map.MapJobsAccept.SignOut();
                return;
            }

            if (_commands == null)
                _commands = new ClientCommandSession();
            Map.MapJobsContractsRead.HandleAuth(uid ?? "", _status, _session);
            Map.MapFleetStateRead.HandleAuth(uid ?? "", _status, _session);
            Map.MapRoadDeliveriesRead.HandleAuth(uid ?? "", _status, _session);
            Map.MapCompanyEconomyRead.HandleAuth(uid ?? "", _status, _session);
            Map.MapJobsAccept.HandleAuth(uid ?? "", _status, _commands);
        }

        internal static void OnBackendStatus(ClientBackendStatus status)
        {
            _statusEvents++;
            _status = status;
            _settings = _settingsOverride != null
                ? _settingsOverride
                : ClientEnvironmentSettings.Load();
            if (!ClientLocalEmulatorSecurity.AllowsLiveReads(_settings, status))
            {
                if (_liveAttached)
                {
                    _liveAttached = false;
                    UnhookAuth();
                    _session = null;
                    _commands = null;
                    Map.MapJobsContractsRead.SignOut();
                    Map.MapFleetStateRead.SignOut();
                    Map.MapRoadDeliveriesRead.SignOut();
                    Map.MapCompanyEconomyRead.SignOut();
                    Map.MapJobsAccept.SignOut();
                }

                return;
            }

            if (_session == null)
            {
                _session = new ClientReadSession();
                _session.SetAsyncInvoker(_testAsyncInvoker ?? ClientFunctionsReadInvoker.Invoke);
            }

            if (_commands == null)
                _commands = new ClientCommandSession();
            if (_testCommandAsync != null)
                _commands.SetAsyncInvoker(_testCommandAsync);
            else if (!_commands.HasInvoker)
                _commands.SetAsyncInvoker(ClientFunctionsCommandInvoker.Invoke);

            _liveAttached = true;
            HookAuth();
        }

        static void HookAuth()
        {
            if (!FirebaseClient.SdkAppReady)
                return;

            FirebaseAuth auth;
            try
            {
                auth = FirebaseAuth.DefaultInstance;
            }
            catch (Exception)
            {
                auth = null;
            }

            if (auth == null)
                return;

            if (!_authHooked || _auth != auth)
            {
                if (_auth != null && _authHooked)
                    _auth.StateChanged -= OnAuthChanged;
                _auth = auth;
                _auth.StateChanged += OnAuthChanged;
                _authHooked = true;
                _authSubscriptions++;
#if UNITY_EDITOR
                ClientReadLifecycleTrace.AuthEvent(ClientReadLifecycleTrace.Event.AuthSubscribed);
#endif

            }

            PushUser(_auth.CurrentUser);
        }

        static void UnhookAuth()
        {
            if (_auth != null && _authHooked)
                _auth.StateChanged -= OnAuthChanged;
            _auth = null;
            _authHooked = false;
        }

        static void OnAuthChanged(object sender, EventArgs args)
        {
#if UNITY_EDITOR
            ClientReadLifecycleTrace.AuthEvent(ClientReadLifecycleTrace.Event.AuthStateChanged);
#endif

            var auth = sender as FirebaseAuth;
            PushUser(auth != null ? auth.CurrentUser : null);
        }

        static void PushUser(FirebaseUser user)
        {
#if UNITY_EDITOR
            ClientReadLifecycleTrace.ObserveUser(user != null);
#endif

            NotifyAuth(user != null ? user.UserId : "");
        }
    }
}
