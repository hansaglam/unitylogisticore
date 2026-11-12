using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using Firebase.Functions;

namespace LogistiCore.Backend
{
    /// <summary>
    /// Read-only Functions adapter. Commands are not dispatched.
    /// Offline tests must not call Invoke; they inject a mock session instead.
    /// </summary>
    public static class ClientFunctionsReadInvoker
    {
        public static void Invoke(
            string callable,
            IReadOnlyDictionary<string, object> payload,
            Action<object> completed,
            Action<Exception> failed)
        {
            if (!ClientCallableCatalog.IsRead(callable))
            {
                failed(new InvalidOperationException("command"));
                return;
            }

            if (!ClientLocalEmulatorSecurity.AllowsLiveReads(ClientJobsRuntime.Settings, FirebaseClient.Current))
            {
                failed(new InvalidOperationException("hold"));
                return;
            }

#if UNITY_EDITOR
            var trace = ClientReadLifecycleTrace.BeginRead(callable);
#endif
            try
            {
                var app = FirebaseApp.DefaultInstance;
                if (app == null)
                {
#if UNITY_EDITOR
                    ClientReadLifecycleTrace.ReadEvent(trace, ClientReadLifecycleTrace.Event.ReadFailed);
#endif
                    failed(new InvalidOperationException("app"));
                    return;
                }

                var functions = FirebaseFunctions.GetInstance(app, ClientProtocol.FunctionsRegion);
                var data = payload ?? ClientValue.ReadRequest();
#if UNITY_EDITOR
                ClientReadLifecycleTrace.ReadEvent(trace, ClientReadLifecycleTrace.Event.CallAsyncEntered);
#endif
                functions.GetHttpsCallable(callable)
                    .CallAsync(data)
                    .ContinueWithOnMainThread(task =>
                    {
                        if (task.IsCanceled)
                        {
#if UNITY_EDITOR
                            ClientReadLifecycleTrace.ReadEvent(trace, ClientReadLifecycleTrace.Event.ReadCanceled);
#endif
                            failed(new TimeoutException());
                            return;
                        }

                        if (task.IsFaulted)
                        {
#if UNITY_EDITOR
                            ClientReadLifecycleTrace.ReadEvent(trace, ClientReadLifecycleTrace.Event.ReadFailed);
#endif
                            failed(task.Exception != null ? task.Exception.GetBaseException() : new InvalidOperationException("fault"));
                            return;
                        }

#if UNITY_EDITOR
                        ClientReadLifecycleTrace.ReadEvent(trace, ClientReadLifecycleTrace.Event.ReadCompleted);
#endif
                        completed(task.Result != null ? task.Result.Data : null);
                    });
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR
                ClientReadLifecycleTrace.ReadEvent(trace, ClientReadLifecycleTrace.Event.ReadFailed);
#endif
                failed(exception);
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>Bounded Editor-only lifecycle evidence; never accepts tokens, identifiers or exception text.</summary>
    public static class ClientReadLifecycleTrace
    {
        public enum Event { SessionStarted, AuthSubscribed, AuthStateChanged, UserObserved, ReadEntered, CallAsyncEntered, ReadCompleted, ReadFailed, ReadCanceled }
        public enum ReadKind { Other, Contracts, Fleet, Deliveries, Economy, Company, Warehouse, Market, AcquisitionOffers, WorldEvents, Bank }
        public readonly struct Ticket
        {
            internal Ticket(int generation, int call, ReadKind kind) { Generation = generation; Call = call; Kind = kind; }
            public int Generation { get; }
            public int Call { get; }
            public ReadKind Kind { get; }
        }
        const int Capacity = 24;
        static readonly object Sync = new object();
        static readonly Queue<string> Entries = new Queue<string>(Capacity);
        static int _generation, _sequence, _calls;
        static bool? _userPresent;
        static long _started = System.Diagnostics.Stopwatch.GetTimestamp();

        public static void Reset()
        {
            lock (Sync)
            {
                Entries.Clear(); _generation = 0; _sequence = 0; _calls = 0; _userPresent = null;
                _started = System.Diagnostics.Stopwatch.GetTimestamp();
            }
        }
        public static void BeginSession()
        {
            lock (Sync) { _generation++; _userPresent = null; Add(Event.SessionStarted, default, false); }
        }
        public static void AuthEvent(Event kind)
        {
            if (kind != Event.AuthSubscribed && kind != Event.AuthStateChanged) return;
            lock (Sync) Add(kind, default, false);
        }
        public static void ObserveUser(bool present)
        {
            lock (Sync) { _userPresent = present; Add(Event.UserObserved, default, false); }
        }
        public static Ticket BeginRead(string callable)
        {
            lock (Sync)
            {
                var ticket = new Ticket(_generation, ++_calls, Classify(callable));
                Add(Event.ReadEntered, ticket, true);
                return ticket;
            }
        }
        public static void ReadEvent(Ticket ticket, Event kind)
        {
            if (kind != Event.CallAsyncEntered && kind != Event.ReadCompleted && kind != Event.ReadFailed && kind != Event.ReadCanceled) return;
            lock (Sync) Add(kind, ticket, true);
        }
        public static string Snapshot()
        {
            lock (Sync)
                return "Lifecycle snapshot: generation=" + _generation + "; retained=" + Entries.Count
                    + "/24; last observed user=" + Presence(_userPresent) + "\n" + string.Join("\n", Entries.ToArray());
        }
        static string Presence(bool? present) => present.HasValue ? (present.Value ? "yes" : "no") : "unknown";
        static void Add(Event kind, Ticket ticket, bool read)
        {
            var elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - _started) * 1000L / System.Diagnostics.Stopwatch.Frequency;
            var generation = read ? ticket.Generation : _generation;
            var line = "seq=" + (++_sequence) + "; generation=" + generation + "; elapsedMs=" + elapsed
                + "; event=" + kind + "; lastObservedUser=" + Presence(generation == _generation ? _userPresent : null);
            if (read) line += "; call=" + ticket.Call + "; read=" + ticket.Kind;
            if (Entries.Count == Capacity) Entries.Dequeue();
            Entries.Enqueue(line);
        }
        static ReadKind Classify(string callable)
        {
            switch (callable)
            {
                case ClientCallableCatalog.GetMyContracts: return ReadKind.Contracts;
                case ClientCallableCatalog.GetMyFleetState: return ReadKind.Fleet;
                case ClientCallableCatalog.GetMyDeliveries: return ReadKind.Deliveries;
                case ClientCallableCatalog.GetMyEconomyState: return ReadKind.Economy;
                case ClientCallableCatalog.GetMyCompanyState: return ReadKind.Company;
                case ClientCallableCatalog.GetMyWarehouseState: return ReadKind.Warehouse;
                case ClientCallableCatalog.GetMyMarketState: return ReadKind.Market;
                case ClientCallableCatalog.GetMyAcquisitionOffers: return ReadKind.AcquisitionOffers;
                case ClientCallableCatalog.GetMyWorldEvents: return ReadKind.WorldEvents;
                case ClientCallableCatalog.GetMyBankState: return ReadKind.Bank;
                default: return ReadKind.Other;
            }
        }
    }
#endif

}
