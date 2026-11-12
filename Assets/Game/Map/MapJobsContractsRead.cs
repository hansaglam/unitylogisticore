using System;
using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Auth-gated getMyContracts → MapJobsStore. Does not call deliveries or commands.
    /// </summary>
    public static class MapJobsContractsRead
    {
        static ClientReadSession _session;
        static ClientBackendStatus _status;
        static string _uid = "";
        static int _generation;
        static bool _inFlight;
        static bool _settled;
        static bool _queued;

        public static event Action Applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            ResetForTests();
        }

        public static string Uid => _uid;
        public static bool InFlight => _inFlight;
        public static int InvokeCount => _session != null ? _session.InvokeCount : 0;

        public static void ResetForTests()
        {
            Applied = null;
            _session = null;
            _status = default;
            _uid = "";
            _generation++;
            _inFlight = false;
            _settled = false;
            _queued = false;
            MapJobsStore.ClearTrusted();
        }

        public static void BeginPlaySession()
        {
            _generation++;
            _inFlight = false;
            _queued = false;
            _settled = false;
            MapJobsStore.ClearTrusted();
            Notify();
        }

        public static void HandleAuth(string uid, ClientBackendStatus status, ClientReadSession session)
        {
            uid = uid ?? "";
            _status = status;
            _session = session;
            if (ClientLocalEmulatorSecurity.AllowsLiveReads(ClientJobsRuntime.Settings, status))
                MapJobsStore.ClearOverride();
            if (string.IsNullOrEmpty(uid))
            {
                SignOut();
                return;
            }

            if (_uid != uid)
            {
                _generation++;
                _uid = uid;
                _settled = false;
                MapJobsStore.ClearTrusted();
            }

            if (_inFlight)
                _queued = true;
            TryLoad(_settled && MapJobsStore.Current.Status == MapJobsStatus.Error);
        }

        public static void SignOut()
        {
            _generation++;
            _uid = "";
            _settled = false;
            _queued = false;
            _session = null;
            MapJobsStore.ClearTrusted();
            Notify();
        }

        public static void TryRefresh()
        {
            TryLoad(MapJobsStore.Current.Status == MapJobsStatus.Error);
        }

        public static void RequestTrustedRefresh()
        {
            if (string.IsNullOrEmpty(_uid) || _session == null)
                return;
            _settled = false;
            if (_inFlight)
            {
                _queued = true;
                return;
            }

            TryLoad(true);
        }

        static void TryLoad(bool retryError)
        {
            if (string.IsNullOrEmpty(_uid) || _session == null)
                return;
            if (_inFlight)
                return;
            if (_settled && !retryError)
                return;
            if (!_session.HasInvoker)
                return;

            var gate = ClientConnectionGate.Inspect(
                _status,
                true,
                ClientReadAuthorization.IsApproved(_status));
            if (gate == ClientCallableFault.ConnectionHold
                || gate == ClientCallableFault.Unconfigured
                || gate == ClientCallableFault.Refused)
                return;
            if (gate == ClientCallableFault.MissingAuth)
            {
                SignOut();
                return;
            }

            if (_status.Phase == ClientBackendPhase.Starting)
                return;
            if (gate != ClientCallableFault.None && gate != ClientCallableFault.ServerUnavailable)
                return;

            var gen = _generation;
            _inFlight = true;
            if (gate == ClientCallableFault.None)
                MapJobsStore.TryApplyTrusted(MapJobsSnapshot.Loading());

            _session.ReadAsync(
                ClientCallableCatalog.GetMyContracts,
                _status,
                true,
                ClientValue.ReadRequest(),
                outcome => Complete(gen, outcome));
        }

        static void Complete(int gen, ClientReadOutcome outcome)
        {
            _inFlight = false;
            if (gen != _generation)
            {
                if (_queued)
                {
                    _queued = false;
                    TryLoad(true);
                }

                Notify();
                return;
            }

            if (_queued)
            {
                _queued = false;
                TryLoad(true);
                Notify();
                return;
            }

            if (!outcome.Ok)
            {
                ApplyFault(outcome.Fault);
                Notify();
                return;
            }

            if (!ClientReadParser.TryContracts(outcome.Envelope.Result, out var page))
            {
                MapJobsStore.TryApplyTrusted(MapJobsSnapshot.Error());
                _settled = true;
                Notify();
                return;
            }

            if (!MapJobsStore.TryApplyTrusted(ClientStoreBridge.Jobs(page)))
            {
                MapJobsStore.TryApplyTrusted(MapJobsSnapshot.Error());
                _settled = true;
                Notify();
                return;
            }

            _settled = true;
            Notify();
        }

        static void ApplyFault(ClientCallableFault fault)
        {
            if (fault == ClientCallableFault.DuplicateInFlight)
                return;
            if (fault == ClientCallableFault.ConnectionHold
                || fault == ClientCallableFault.TransportDisabled
                || fault == ClientCallableFault.Unconfigured
                || fault == ClientCallableFault.Refused)
            {
                if (!_settled)
                    MapJobsStore.ClearTrusted();
                return;
            }

            if (fault == ClientCallableFault.MissingAuth)
            {
                SignOut();
                return;
            }

            MapJobsStore.TryApplyTrusted(MapJobsSnapshot.Error());
            _settled = true;
        }

        static void Notify()
        {
            var handler = Applied;
            if (handler != null)
                handler();
        }
    }
}
