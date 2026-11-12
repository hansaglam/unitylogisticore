using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using Firebase.Functions;

namespace LogistiCore.Backend
{
    /// <summary>
    /// acceptContract Functions adapter. CallAsync is behind AllowsLiveFunctions, which stays false.
    /// </summary>
    public static class ClientFunctionsCommandInvoker
    {
        static int _dispatchAttempts;
        static int _callAsyncAttempts;

        public static int DispatchAttempts => _dispatchAttempts;
        public static int CallAsyncAttempts => _callAsyncAttempts;

        internal static void ResetForTests()
        {
            _dispatchAttempts = 0;
            _callAsyncAttempts = 0;
        }

        public static void Invoke(
            string callable,
            IReadOnlyDictionary<string, object> payload,
            Action<object> completed,
            Action<Exception> failed)
        {
            if (failed == null)
                return;
            _dispatchAttempts++;
            if (!ClientCallableCatalog.IsSupportedPlayerCommand(callable))
            {
                failed(new ClientCommandException("INVALID_ARGUMENT"));
                return;
            }

            if (!ClientValue.TryValidateAcceptContractRequest(payload))
            {
                failed(new ClientCommandException("INVALID_ARGUMENT"));
                return;
            }

            if (!ClientCommandAuthorization.AllowsLiveFunctions(ClientJobsRuntime.Settings, FirebaseClient.Current)
                || !ClientLocalEmulatorSecurity.AllowsLiveReads(ClientJobsRuntime.Settings, FirebaseClient.Current)
                || !FirebaseClient.SdkAppReady)
            {
                failed(new ClientCommandException("LIVE_HOLD"));
                return;
            }

            try
            {
                var app = FirebaseApp.DefaultInstance;
                if (app == null)
                {
                    failed(new InvalidOperationException("app"));
                    return;
                }

                _callAsyncAttempts++;
                var functions = FirebaseFunctions.GetInstance(app, ClientProtocol.FunctionsRegion);
                functions.GetHttpsCallable(callable)
                    .CallAsync(payload)
                    .ContinueWithOnMainThread(task =>
                    {
                        if (task.IsCanceled)
                        {
                            failed(new TimeoutException());
                            return;
                        }

                        if (task.IsFaulted)
                        {
                            failed(task.Exception != null ? task.Exception.GetBaseException() : new InvalidOperationException("fault"));
                            return;
                        }

                        if (completed != null)
                            completed(task.Result != null ? task.Result.Data : null);
                    });
            }
            catch (Exception exception)
            {
                failed(exception);
            }
        }
    }
}
