using System;
using System.Collections.Generic;

namespace LogistiCore.Backend
{
    public readonly struct ClientCommandOutcome
    {
        public ClientCommandOutcome(
            ClientCallableFault fault,
            ClientEnvelope envelope,
            string detailsCode,
            int invokeCount)
        {
            Fault = fault;
            Envelope = envelope;
            DetailsCode = detailsCode ?? "";
            InvokeCount = invokeCount;
        }

        public ClientCallableFault Fault { get; }
        public ClientEnvelope Envelope { get; }
        public string DetailsCode { get; }
        public int InvokeCount { get; }
        public bool Ok => Fault == ClientCallableFault.None;
    }

    public sealed class ClientCommandException : InvalidOperationException
    {
        public ClientCommandException(string detailsCode)
            : base(detailsCode)
        {
            DetailsCode = detailsCode ?? "";
        }

        public string DetailsCode { get; }
    }

    public delegate object ClientCommandInvoker(string callable, IReadOnlyDictionary<string, object> payload);

    public delegate void ClientCommandAsyncInvoker(
        string callable,
        IReadOnlyDictionary<string, object> payload,
        Action<object> completed,
        Action<Exception> failed);

    /// <summary>
    /// Auth-gated command session. Separate from ClientReadSession. Default invoker is null.
    /// Only acceptContract is dispatched in this ticket. Commands never auto-retry.
    /// </summary>
    public sealed class ClientCommandSession
    {
        bool _inflight;
        ClientCommandInvoker _invoker;
        ClientCommandAsyncInvoker _asyncInvoker;
        int _invokes;

        public int InvokeCount => _invokes;
        public bool HasInvoker => _invoker != null || _asyncInvoker != null;
        public bool InFlight => _inflight;

        public void SetInvoker(ClientCommandInvoker invoker)
        {
            _invoker = invoker;
        }

        public void SetAsyncInvoker(ClientCommandAsyncInvoker invoker)
        {
            _asyncInvoker = invoker;
        }

        public void ClearInvoker()
        {
            _invoker = null;
            _asyncInvoker = null;
        }

        public ClientCommandOutcome Execute(
            string callable,
            ClientBackendStatus status,
            bool hasUid,
            IReadOnlyDictionary<string, object> request)
        {
            ClientCommandOutcome outcome = default;
            var pending = true;
            ExecuteAsync(callable, status, hasUid, request, result =>
            {
                outcome = result;
                pending = false;
            });
            if (pending)
                return Fail(ClientCallableFault.TransportDisabled, "");
            return outcome;
        }

        public void ExecuteAsync(
            string callable,
            ClientBackendStatus status,
            bool hasUid,
            IReadOnlyDictionary<string, object> request,
            Action<ClientCommandOutcome> completed)
        {
            if (completed == null)
                return;
            if (!ClientCallableCatalog.IsSupportedPlayerCommand(callable))
            {
                completed(Fail(ClientCallableFault.NonRetryableCommand, ""));
                return;
            }

            var gate = ClientConnectionGate.Inspect(status, hasUid, ClientCommandAuthorization.IsApproved(status));
            if (gate != ClientCallableFault.None)
            {
                completed(Fail(gate, ""));
                return;
            }

            if (!TryValidateRequest(request, out var fault))
            {
                completed(Fail(fault, fault == ClientCallableFault.ProtocolMismatch ? "UNSUPPORTED_PROTOCOL" : "INVALID_ARGUMENT"));
                return;
            }

            if (_invoker == null && _asyncInvoker == null)
            {
                completed(Fail(ClientCallableFault.TransportDisabled, ""));
                return;
            }

            if (_inflight)
            {
                completed(Fail(ClientCallableFault.DuplicateInFlight, ""));
                return;
            }

            _inflight = true;
            _invokes++;
            if (_asyncInvoker != null)
            {
                try
                {
                    _asyncInvoker(callable, request, raw =>
                    {
                        _inflight = false;
                        Deliver(raw, completed);
                    }, ex =>
                    {
                        _inflight = false;
                        completed(MapFailure(ex));
                    });
                }
                catch (Exception exception)
                {
                    _inflight = false;
                    completed(MapFailure(exception));
                }

                return;
            }

            object value;
            try
            {
                value = _invoker(callable, request);
            }
            catch (Exception exception)
            {
                _inflight = false;
                completed(MapFailure(exception));
                return;
            }

            _inflight = false;
            Deliver(value, completed);
        }

        static bool TryValidateRequest(IReadOnlyDictionary<string, object> request, out ClientCallableFault fault)
        {
            fault = ClientCallableFault.MalformedResponse;
            if (request == null || request.Count != 3)
            {
                fault = ClientCallableFault.NonRetryableCommand;
                return false;
            }

            if (!request.TryGetValue("protocolVersion", out var versionRaw)
                || !ClientValue.TryInt(versionRaw, out var version))
            {
                fault = ClientCallableFault.ProtocolMismatch;
                return false;
            }

            if (!ClientProtocol.IsSupportedVersion(version))
            {
                fault = ClientCallableFault.ProtocolMismatch;
                return false;
            }

            if (!request.TryGetValue("idempotencyKey", out var keyRaw)
                || !ClientValue.TryString(keyRaw, out var key)
                || !ClientValue.IsSafeIdempotencyKey(key))
            {
                fault = ClientCallableFault.NonRetryableCommand;
                return false;
            }

            if (!request.TryGetValue("payload", out var payloadRaw)
                || !ClientValue.TryMap(payloadRaw, out var payload)
                || payload.Count != 1)
            {
                fault = ClientCallableFault.NonRetryableCommand;
                return false;
            }

            if (!ClientValue.TryString(ClientValue.Get(payload, "contractId"), out var contractId)
                || !ClientValue.IsSafeContractId(contractId))
            {
                fault = ClientCallableFault.NonRetryableCommand;
                return false;
            }

            fault = ClientCallableFault.None;
            return true;
        }

        void Deliver(object raw, Action<ClientCommandOutcome> completed)
        {
            if (!ClientEnvelope.TryParse(raw, out var envelope, out var fault))
            {
                completed(Fail(fault, ""));
                return;
            }

            completed(new ClientCommandOutcome(ClientCallableFault.None, envelope, "", _invokes));
        }

        ClientCommandOutcome MapFailure(Exception exception)
        {
            if (exception is TimeoutException)
                return Fail(ClientCallableFault.Timeout, "");
            if (exception is ClientCommandException command)
            {
                if (command.DetailsCode == "LIVE_HOLD")
                    return Fail(ClientCallableFault.ConnectionHold, command.DetailsCode);
                return Fail(ClientCallableErrors.FromDetailsCode(command.DetailsCode), command.DetailsCode);
            }
            if (exception is InvalidOperationException)
                return Fail(ClientCallableFault.NetworkUnavailable, "");
            return Fail(ClientCallableFault.ServerUnavailable, "");
        }

        ClientCommandOutcome Fail(ClientCallableFault fault, string detailsCode)
        {
            return new ClientCommandOutcome(fault, default, detailsCode, 0);
        }
    }
}
