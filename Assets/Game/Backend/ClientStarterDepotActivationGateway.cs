using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using Firebase.Functions;

namespace LogistiCore.Backend
{
    /// <summary>
    /// Narrow, explicit LOCAL-EMULATOR command transport for a one-time free
    /// starter storage depot activation. Does not open general gameplay writes.
    /// Requires the same opt-in company test window as HQ creation.
    /// </summary>
    public static class ClientStarterDepotActivationGateway
    {
        public static bool IsAuthorized(ClientEnvironmentSettings settings,
            ClientBackendStatus status, string uid)
        {
            return !string.IsNullOrEmpty(uid) &&
                FirebaseClient.SdkAppReady &&
                status.Environment == ClientBackendEnvironment.LocalEmulator &&
                ClientLocalEmulatorSecurity.AllowsLiveReads(settings, status) &&
                ClientEmulatorLiveAuthorization.AllowsCompany;
        }

        public static void Submit(string idempotencyKey, string expectedUid,
            ClientEnvironmentSettings settings, ClientBackendStatus status,
            Action completed, Action<string> failed)
        {
            if (completed == null || failed == null)
                return;
            if (!ClientValue.IsSafeIdempotencyKey(idempotencyKey) ||
                !IsAuthorized(settings, status, expectedUid))
            {
                failed("HOLD");
                return;
            }

            try
            {
                var app = FirebaseApp.DefaultInstance;
                var auth = Firebase.Auth.FirebaseAuth.GetAuth(app);
                if (auth == null || auth.CurrentUser == null ||
                    auth.CurrentUser.UserId != expectedUid)
                {
                    failed("AUTH_CHANGED");
                    return;
                }
                if (!IsAuthorized(settings, FirebaseClient.Current, expectedUid))
                {
                    failed("HOLD");
                    return;
                }

                // Empty payload: location, warehouse type and capacity come
                // only from server-verified headquarters + pinned catalog.
                var request = ClientValue.CommandRequest(idempotencyKey,
                    new Dictionary<string, object>());
                FirebaseFunctions.GetInstance(app, ClientProtocol.FunctionsRegion)
                    .GetHttpsCallable(ClientCallableCatalog.ActivateHeadquartersStarterDepot)
                    .CallAsync(request)
                    .ContinueWithOnMainThread(task =>
                    {
                        if (task.IsCanceled || task.IsFaulted)
                        {
                            failed("UNCERTAIN");
                            return;
                        }

                        // Never claim activation on the command response alone.
                        // The UI makes a fresh authenticated read to confirm.
                        if (task.Result == null ||
                            !ClientEnvelope.TryParse(task.Result.Data, out var envelope, out _) ||
                            !ClientValue.TryMap(envelope.Result, out var values) ||
                            !ClientValue.TryString(ClientValue.Get(values, "stage"), out var stage) ||
                            !ClientValue.TryBool(ClientValue.Get(values, "dispatchReady"), out var dispatchReady) ||
                            stage != "warehouse_operational_dispatch_pending_routes" ||
                            dispatchReady)
                        {
                            failed("UNCERTAIN");
                            return;
                        }
                        completed();
                    });
            }
            catch
            {
                failed("UNCERTAIN");
            }
        }
    }
}
