using System;
using System.Collections.Generic;
using Firebase.Functions;

namespace LogistiCore.Backend
{
    public enum ClientReadFailureStage { None, Gate, Transport, Envelope }
    public enum ClientReadExceptionKind { None, FunctionsException, TimeoutException, InvalidOperationException, Other }

    public readonly struct ClientReadOutcome
    {
        public ClientReadOutcome(ClientCallableFault fault, ClientEnvelope envelope, int invokeCount,
            ClientReadFailureStage failureStage = ClientReadFailureStage.None,
            ClientReadExceptionKind exceptionKind = ClientReadExceptionKind.None,
            FunctionsErrorCode? functionsCode = null)
        {
            FailureStage = failureStage;
            ExceptionKind = exceptionKind;
            FunctionsCode = functionsCode.HasValue && Enum.IsDefined(typeof(FunctionsErrorCode), functionsCode.Value)
                ? functionsCode : null;
            Fault = fault;
            Envelope = envelope;
            InvokeCount = invokeCount;
        }

        public ClientReadFailureStage FailureStage { get; }
        public ClientReadExceptionKind ExceptionKind { get; }
        public FunctionsErrorCode? FunctionsCode { get; }
        public string FunctionsCodeName => FunctionsCode.HasValue ? FunctionsCode.Value.ToString() : "None";
        public ClientCallableFault Fault { get; }
        public ClientEnvelope Envelope { get; }
        public int InvokeCount { get; }
        public bool Ok => Fault == ClientCallableFault.None;
    }

    public delegate object ClientReadInvoker(string callable, IReadOnlyDictionary<string, object> payload);

    public delegate void ClientReadAsyncInvoker(
        string callable,
        IReadOnlyDictionary<string, object> payload,
        Action<object> completed,
        Action<Exception> failed);

    /// <summary>
    /// Read-only callable session. The default invoker is null: no network, no Firebase Functions.
    /// Commands are refused. Duplicate in-flight reads are refused.
    /// </summary>
    public sealed class ClientReadSession
    {
        readonly HashSet<string> _inflight = new HashSet<string>();
        ClientReadInvoker _invoker;
        ClientReadAsyncInvoker _asyncInvoker;
        int _invokes;

        public int InvokeCount => _invokes;
        public bool HasInvoker => _invoker != null || _asyncInvoker != null;
        public bool HasAsyncInvoker => _asyncInvoker != null;

        public void SetInvoker(ClientReadInvoker invoker)
        {
            _invoker = invoker;
        }

        public void SetAsyncInvoker(ClientReadAsyncInvoker invoker)
        {
            _asyncInvoker = invoker;
        }

        public void ClearInvoker()
        {
            _invoker = null;
            _asyncInvoker = null;
        }

        public ClientReadOutcome Read(
            string callable,
            ClientBackendStatus status,
            bool hasUid,
            IReadOnlyDictionary<string, object> payload)
        {
            ClientReadOutcome outcome = default;
            var pending = true;
            ReadAsync(callable, status, hasUid, payload, result =>
            {
                outcome = result;
                pending = false;
            });
            if (pending)
                return Fail(ClientCallableFault.TransportDisabled);
            return outcome;
        }

        public void ReadAsync(
            string callable,
            ClientBackendStatus status,
            bool hasUid,
            IReadOnlyDictionary<string, object> payload,
            Action<ClientReadOutcome> completed)
        {
            if (completed == null)
                return;
            if (!ClientCallableCatalog.IsRead(callable))
            {
                completed(Fail(ClientCallableFault.NonRetryableCommand));
                return;
            }

            var gate = ClientConnectionGate.Inspect(status, hasUid, ClientReadAuthorization.IsApproved(status));
            if (gate != ClientCallableFault.None)
            {
                completed(Fail(gate));
                return;
            }

            if (_invoker == null && _asyncInvoker == null)
            {
                completed(Fail(ClientCallableFault.TransportDisabled));
                return;
            }

            if (!_inflight.Add(callable))
            {
                completed(Fail(ClientCallableFault.DuplicateInFlight));
                return;
            }

            _invokes++;
            var request = payload ?? ClientValue.ReadRequest();
            if (_asyncInvoker != null)
            {
                try
                {
                    _asyncInvoker(callable, request, raw =>
                    {
                        _inflight.Remove(callable);
                        Deliver(raw, completed);
                    }, ex =>
                    {
                        _inflight.Remove(callable);
                        completed(ExceptionFailure(ex));
                    });
                }
                catch (Exception exception)
                {
                    _inflight.Remove(callable);
                    completed(ExceptionFailure(exception));
                }

                return;
            }

            object raw;
            try
            {
                raw = _invoker(callable, request);
            }
            catch (Exception exception)
            {
                _inflight.Remove(callable);
                completed(ExceptionFailure(exception));
                return;
            }

            _inflight.Remove(callable);
            Deliver(raw, completed);
        }

        void Deliver(object raw, Action<ClientReadOutcome> completed)
        {
            if (!ClientEnvelope.TryParse(raw, out var envelope, out var fault))
            {
                completed(new ClientReadOutcome(fault, default, 0, ClientReadFailureStage.Envelope));
                return;
            }

            completed(new ClientReadOutcome(ClientCallableFault.None, envelope, _invokes));
        }

        static ClientReadOutcome ExceptionFailure(Exception exception)
        {
            // Never retain or expose messages, details, headers, identifiers or response bodies.
            if (exception is AggregateException aggregate)
                exception = aggregate.GetBaseException();
            var kind = exception is FunctionsException ? ClientReadExceptionKind.FunctionsException
                : exception is TimeoutException ? ClientReadExceptionKind.TimeoutException
                : exception is InvalidOperationException ? ClientReadExceptionKind.InvalidOperationException
                : ClientReadExceptionKind.Other;
            FunctionsErrorCode? code = exception is FunctionsException functions ? functions.ErrorCode : (FunctionsErrorCode?)null;
            return new ClientReadOutcome(MapException(exception), default, 0,
                ClientReadFailureStage.Transport, kind, code);
        }

        static ClientCallableFault MapException(Exception exception)
        {
            if (exception is TimeoutException)
                return ClientCallableFault.Timeout;
            if (exception is InvalidOperationException)
                return ClientCallableFault.NetworkUnavailable;
            return ClientCallableFault.ServerUnavailable;
        }

        static ClientReadOutcome Fail(ClientCallableFault fault)
        {
            return new ClientReadOutcome(fault, default, 0, ClientReadFailureStage.Gate);
        }
    }
}
