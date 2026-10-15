using System;
using System.Globalization;
using System.Threading;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using Firebase.Functions;
using UnityEngine;

namespace LogistiCore.Backend
{
    public readonly struct ClientBackendStatus
    {
        public ClientBackendStatus(
            ClientBackendEnvironment environment,
            ClientConnectionDecision decision,
            ClientBackendPhase phase,
            string detail)
        {
            Environment = environment;
            Decision = decision;
            Phase = phase;
            Detail = detail ?? "";
        }

        public ClientBackendEnvironment Environment { get; }
        public ClientConnectionDecision Decision { get; }
        public ClientBackendPhase Phase { get; }
        public string Detail { get; }
    }

    /// <summary>
    /// One-shot Firebase startup. Unconfigured, refused, and production states never call the SDK.
    /// Authentication providers are not started and no callable or Firestore request is made.
    /// </summary>
    public static class FirebaseClient
    {
        internal const string StartingDetail = "Firebase initialization started.";
        internal const string ReadyDetail = "Firebase app is ready. No user was signed in and no backend call was made.";
        internal const string DependenciesDetail = "Firebase dependencies are not available.";
        internal const string FailedDetail = "Firebase initialization failed.";
        internal const string OffThreadDetail = "Firebase initialization must run on the Unity main thread.";

        internal enum ClientSdkResult
        {
            Ready = 0,
            DependenciesUnavailable = 1,
            Failed = 2
        }

        internal delegate void ConnectHandler(
            ClientEnvironmentSettings settings,
            ClientConnectionDecision decision,
            Action<ClientSdkResult> completed);

        static readonly object Sync = new object();
        static int _mainThreadId;
        static int _connectAttempts;
        static ClientBackendPhase _phase = ClientBackendPhase.Idle;
        static ClientBackendStatus _status;

        internal static ConnectHandler Connect = SdkConnect;

        public static ClientBackendStatus Current
        {
            get
            {
                lock (Sync)
                    return _status;
            }
        }

        internal static int ConnectAttempts
        {
            get
            {
                lock (Sync)
                    return _connectAttempts;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void CaptureMainThread()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            Start(ClientEnvironmentSettings.Load());
        }

        public static ClientBackendStatus Start(ClientEnvironmentSettings settings)
        {
            ClientEnvironmentSettings captured = null;
            ClientConnectionDecision decision = default;
            ConnectHandler connect = null;
            var invoke = false;
            var logInfo = false;
            var logError = false;
            var logDetail = "";

            lock (Sync)
            {
                if (_mainThreadId == 0)
                    _mainThreadId = Thread.CurrentThread.ManagedThreadId;

                var request = settings == null
                    ? ClientConnectionRequest.Unconfigured
                    : settings.ToRequest();

                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                {
                    return new ClientBackendStatus(
                        request.Environment,
                        ClientConnectionPolicy.Decide(request),
                        ClientBackendPhase.Failed,
                        OffThreadDetail);
                }

                if (_phase != ClientBackendPhase.Idle)
                    return _status;

                decision = ClientConnectionPolicy.Decide(request);
                if (decision != ClientConnectionDecision.ConnectToDevelopment &&
                    decision != ClientConnectionDecision.ConnectToEmulator)
                {
                    var phase = decision == ClientConnectionDecision.StayOffline
                        ? ClientBackendPhase.Offline
                        : ClientBackendPhase.Refused;
                    var detail = ClientConnectionPolicy.Describe(request.Environment, decision);
                    _phase = phase;
                    _status = new ClientBackendStatus(request.Environment, decision, phase, detail);
                    logDetail = detail;
                    logInfo = phase == ClientBackendPhase.Offline;
                    logError = phase == ClientBackendPhase.Refused;
                }
                else
                {
                    _phase = ClientBackendPhase.Starting;
                    _status = new ClientBackendStatus(
                        request.Environment,
                        decision,
                        ClientBackendPhase.Starting,
                        StartingDetail);
                    _connectAttempts++;
                    captured = settings;
                    connect = Connect;
                    invoke = true;
                }
            }

            if (!invoke)
            {
                if (logInfo)
                    GameLog.Info("Firebase", logDetail);
                else if (logError)
                    GameLog.Error("Firebase", logDetail);
                lock (Sync)
                    return _status;
            }

            try
            {
                connect(captured, decision, Finish);
            }
            catch (Exception exception)
            {
                lock (Sync)
                {
                    if (_phase == ClientBackendPhase.Starting)
                    {
                        _phase = ClientBackendPhase.Failed;
                        _status = new ClientBackendStatus(
                            _status.Environment,
                            _status.Decision,
                            ClientBackendPhase.Failed,
                            FailedDetail);
                    }
                }

                GameLog.Error("Firebase", "Initialization failed (" + exception.GetType().Name + ").");
            }

            lock (Sync)
                return _status;
        }

        internal static void ResetForTests()
        {
            lock (Sync)
            {
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                _connectAttempts = 0;
                _phase = ClientBackendPhase.Idle;
                _status = default;
                Connect = SdkConnect;
            }
        }

        static void Finish(ClientSdkResult result)
        {
            var failed = result != ClientSdkResult.Ready;
            var detail = result == ClientSdkResult.Ready
                ? ReadyDetail
                : result == ClientSdkResult.DependenciesUnavailable
                    ? DependenciesDetail
                    : FailedDetail;

            lock (Sync)
            {
                if (_phase != ClientBackendPhase.Starting)
                    return;

                _phase = failed ? ClientBackendPhase.Failed : ClientBackendPhase.Ready;
                _status = new ClientBackendStatus(_status.Environment, _status.Decision, _phase, detail);
            }

            if (failed)
                GameLog.Error("Firebase", detail);
            else
                GameLog.Info("Firebase", detail);
        }

        static void SdkConnect(
            ClientEnvironmentSettings settings,
            ClientConnectionDecision decision,
            Action<ClientSdkResult> completed)
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                try
                {
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        var typeName = task.Exception == null ? "Exception" : task.Exception.GetType().Name;
                        GameLog.Error("Firebase", "Dependency check failed (" + typeName + ").");
                        completed(ClientSdkResult.DependenciesUnavailable);
                        return;
                    }

                    if (task.Result != DependencyStatus.Available)
                    {
                        completed(ClientSdkResult.DependenciesUnavailable);
                        return;
                    }

                    var options = new AppOptions
                    {
                        ProjectId = settings.ProjectId,
                        AppId = settings.AppId,
                        ApiKey = settings.ApiKey
                    };
                    var app = FirebaseApp.Create(options);
                    if (decision == ClientConnectionDecision.ConnectToEmulator)
                        ApplyEmulator(app, settings);
                    completed(ClientSdkResult.Ready);
                }
                catch (Exception exception)
                {
                    GameLog.Error("Firebase", "Initialization failed (" + exception.GetType().Name + ").");
                    completed(ClientSdkResult.Failed);
                }
            });
        }

        static void ApplyEmulator(FirebaseApp app, ClientEnvironmentSettings settings)
        {
            if (IsPort(settings.EmulatorFirestorePort))
            {
                var firestore = FirebaseFirestore.GetInstance(app);
                var firestoreSettings = firestore.Settings;
                firestoreSettings.Host = settings.EmulatorHost + ":" +
                    settings.EmulatorFirestorePort.ToString(CultureInfo.InvariantCulture);
                firestoreSettings.SslEnabled = false;
                firestoreSettings.PersistenceEnabled = false;
            }

            if (IsPort(settings.EmulatorAuthPort))
                FirebaseAuth.GetAuth(app).UseEmulator(settings.EmulatorHost, settings.EmulatorAuthPort);

            if (!string.IsNullOrWhiteSpace(settings.FunctionsEmulatorOrigin) &&
                !string.IsNullOrWhiteSpace(settings.FunctionsRegion))
            {
                FirebaseFunctions.GetInstance(app, settings.FunctionsRegion)
                    .UseFunctionsEmulator(settings.FunctionsEmulatorOrigin);
            }
        }

        static bool IsPort(int port)
        {
            return port >= 1 && port <= 65535;
        }
    }
}
