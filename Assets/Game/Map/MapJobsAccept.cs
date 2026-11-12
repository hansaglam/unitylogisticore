using System;
using LogistiCore.Backend;
using LogistiCore.Localization;
using UnityEngine;

namespace LogistiCore.Map
{
    public enum MapJobsAcceptPhase
    {
        Idle = 0,
        Busy = 1,
        Uncertain = 2,
        Failed = 3
    }

    /// <summary>
    /// Explicit acceptContract only. Does not start, claim, or generate offers.
    /// Does not mutate Jobs state from the command result; getMyContracts is the trusted view.
    /// </summary>
    public static class MapJobsAccept
    {
        static ClientCommandSession _session;
        static ClientBackendStatus _status;
        static string _uid = "";
        static int _generation;
        static bool _inFlight;
        static string _activeKey = "";
        static string _activeContractId = "";
        static MapJobsAcceptPhase _phase;
        static string _messageKey = "";
        static string _detailsCode = "";
        static bool _awaitingRefresh;

        public static event Action Changed;

        public static string Uid => _uid;
        public static bool InFlight => _inFlight;
        public static MapJobsAcceptPhase Phase => _phase;
        public static string MessageKey => _messageKey;
        public static string DetailsCode => _detailsCode;
        public static string ActiveIdempotencyKey => _activeKey;
        public static string ActiveContractId => _activeContractId;
        public static int InvokeCount => _session != null ? _session.InvokeCount : 0;
        public static bool Unconfirmed => _phase == MapJobsAcceptPhase.Uncertain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            ResetForTests();
        }

        public static void ResetForTests()
        {
            MapJobsContractsRead.Applied -= OnContractsApplied;
            MapJobsContractsRead.Applied += OnContractsApplied;
            Changed = null;
            _session = null;
            _status = default;
            _uid = "";
            _generation++;
            ClearAction();
        }

        public static void BeginPlaySession()
        {
            _generation++;
            ClearAction();
            Notify();
        }

        public static void HandleAuth(string uid, ClientBackendStatus status, ClientCommandSession session)
        {
            uid = uid ?? "";
            _status = status;
            _session = session;
            if (string.IsNullOrEmpty(uid))
            {
                SignOut();
                return;
            }

            if (_uid != uid)
            {
                _generation++;
                _uid = uid;
                ClearAction();
            }

            Notify();
        }

        public static void SignOut()
        {
            _generation++;
            _uid = "";
            ClearAction();
            Notify();
        }

        public static bool CanShow(MapJobsItem item, MapJobsSnapshot snapshot)
        {
            if (snapshot.Synthetic || snapshot.Status != MapJobsStatus.Ready)
                return false;
            return IsOpenOffer(item);
        }

        public static bool CanInteract(MapJobsItem item, MapJobsSnapshot snapshot)
        {
            if (!CanShow(item, snapshot))
                return false;
            if (_inFlight || _phase == MapJobsAcceptPhase.Busy || _phase == MapJobsAcceptPhase.Uncertain)
                return false;
            if (string.IsNullOrEmpty(_uid) || _session == null || !_session.HasInvoker)
                return false;
            if (!ClientLocalEmulatorSecurity.TryValidate(ClientJobsRuntime.Settings, out _))
                return false;
            var gate = ClientConnectionGate.Inspect(
                _status,
                true,
                ClientCommandAuthorization.IsApproved(_status));
            return gate == ClientCallableFault.None;
        }

        public static bool TryBegin(string contractId)
        {
            if (_inFlight)
                return false;
            if (string.IsNullOrEmpty(_uid) || _session == null)
            {
                Fail("UNAUTHENTICATED", OpsContent.JobsAcceptUnauthenticated);
                return false;
            }

            if (!ClientLocalEmulatorSecurity.TryValidate(ClientJobsRuntime.Settings, out _))
            {
                Fail("", OpsContent.JobsAcceptUnavailable);
                return false;
            }

            var snapshot = MapJobsStore.Current;
            if (snapshot.Synthetic || snapshot.Status != MapJobsStatus.Ready
                || !snapshot.TryGet(contractId, out var item))
            {
                Fail("CONTRACT_NOT_OFFERED", OpsContent.JobsAcceptNotOffered);
                return false;
            }

            if (item.ContractStatus != MapJobContractStatus.Offered)
            {
                Fail("CONTRACT_NOT_OFFERED", OpsContent.JobsAcceptNotOffered);
                return false;
            }

            if (item.Expired || !IsOpenOffer(item))
            {
                Fail("OFFER_EXPIRED", OpsContent.JobsAcceptExpired);
                return false;
            }

            if (!CanInteract(item, snapshot))
            {
                if (!_session.HasInvoker)
                    Fail("", OpsContent.JobsAcceptUnavailable);
                else
                    Fail("", OpsContent.JobsAcceptUnavailable);
                return false;
            }

            if (!ClientValue.IsSafeContractId(contractId))
            {
                Fail("INVALID_ARGUMENT", OpsContent.JobsAcceptInvalid);
                return false;
            }

            var key = ClientValue.NewIdempotencyKey();
            var request = ClientValue.CommandRequest(key, ClientValue.AcceptContractPayload(contractId));
            var gen = _generation;
            _inFlight = true;
            _phase = MapJobsAcceptPhase.Busy;
            _activeKey = key;
            _activeContractId = contractId;
            _messageKey = OpsContent.JobsAcceptBusy;
            _detailsCode = "";
            Notify();
            _session.ExecuteAsync(
                ClientCallableCatalog.AcceptContract,
                _status,
                true,
                request,
                outcome => Complete(gen, contractId, key, outcome));
            return true;
        }

        static void Complete(int gen, string contractId, string key, ClientCommandOutcome outcome)
        {
            if (gen != _generation)
                return;
            _inFlight = false;
            if (!outcome.Ok)
            {
                if (outcome.Fault == ClientCallableFault.DuplicateInFlight)
                {
                    _inFlight = true;
                    _phase = MapJobsAcceptPhase.Busy;
                    Notify();
                    return;
                }

                if (outcome.Fault == ClientCallableFault.Timeout
                    || outcome.Fault == ClientCallableFault.NetworkUnavailable
                    || outcome.Fault == ClientCallableFault.ServerUnavailable)
                {
                    _phase = MapJobsAcceptPhase.Uncertain;
                    _messageKey = OpsContent.JobsAcceptUncertain;
                    _detailsCode = outcome.DetailsCode;
                    Notify();
                    MapJobsContractsRead.RequestTrustedRefresh();
                    return;
                }

                Fail(outcome.DetailsCode, OpsContent.CommandErrorKey(outcome.DetailsCode));
                return;
            }

            if (!ClientReadParser.TryAcceptContract(outcome.Envelope.Result, out var accepted)
                || accepted.ContractId != contractId
                || !accepted.IsAccepted)
            {
                Fail("", OpsContent.JobsAcceptUnavailable);
                return;
            }

            _awaitingRefresh = true;
            _phase = MapJobsAcceptPhase.Busy;
            _messageKey = OpsContent.JobsAcceptBusy;
            Notify();
            MapJobsContractsRead.RequestTrustedRefresh();
        }

        static void OnContractsApplied()
        {
            if (_inFlight)
                return;
            if (string.IsNullOrEmpty(_activeContractId))
                return;
            var snapshot = MapJobsStore.Current;
            if (snapshot.TryGet(_activeContractId, out var item)
                && item.ContractStatus == MapJobContractStatus.Accepted)
            {
                _awaitingRefresh = false;
                _phase = MapJobsAcceptPhase.Idle;
                _messageKey = "";
                _detailsCode = "";
                _activeKey = "";
                Notify();
                return;
            }

            if (_awaitingRefresh)
            {
                _awaitingRefresh = false;
                _phase = MapJobsAcceptPhase.Uncertain;
                _messageKey = OpsContent.JobsAcceptUncertain;
                Notify();
                return;
            }

            if (_phase == MapJobsAcceptPhase.Uncertain
                && snapshot.TryGet(_activeContractId, out item)
                && IsOpenOffer(item))
            {
                _phase = MapJobsAcceptPhase.Idle;
                Notify();
            }
        }

        static bool IsOpenOffer(MapJobsItem item)
        {
            if (item.ContractStatus != MapJobContractStatus.Offered || item.Expired)
                return false;
            if (!string.IsNullOrEmpty(item.ExpiresAt)
                && ClientIsoTime.TryParse(item.ExpiresAt, out var utc)
                && DateTime.UtcNow >= utc)
                return false;
            return true;
        }

        static void Fail(string detailsCode, string messageKey)
        {
            _phase = MapJobsAcceptPhase.Failed;
            _detailsCode = detailsCode ?? "";
            _messageKey = messageKey ?? OpsContent.JobsAcceptUnavailable;
            Notify();
        }

        static void ClearAction()
        {
            _inFlight = false;
            _awaitingRefresh = false;
            _activeKey = "";
            _activeContractId = "";
            _phase = MapJobsAcceptPhase.Idle;
            _messageKey = "";
            _detailsCode = "";
        }

        static void Notify()
        {
            var handler = Changed;
            if (handler != null)
                handler();
        }
    }
}
