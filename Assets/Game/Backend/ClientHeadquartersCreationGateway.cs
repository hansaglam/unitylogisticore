using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using Firebase.Functions;
using LogistiCore.Map;
using UnityEngine;

namespace LogistiCore.Backend
{
    /// <summary>
    /// ONLY the explicit first-company HQ creation operation. Never change
    /// ClientCommandAuthorization's existing global command hold. The narrow
    /// authorization requires an opted-in company test window, a verified
    /// loopback Firebase emulator, signed-in identity and a direct user click.
    /// There is deliberately no release/production transport here.
    /// </summary>
    public static class ClientHeadquartersCreationGateway
    {
        public static bool IsAuthorized(ClientEnvironmentSettings settings,
            ClientBackendStatus status, bool hasSignedInUser)
        {
            return hasSignedInUser && FirebaseClient.SdkAppReady &&
                ClientLocalEmulatorSecurity.AllowsLiveReads(settings, status) &&
                ClientEmulatorLiveAuthorization.AllowsCompany;
        }

        public static void Submit(string name, MapBusinessLocation city, string idempotencyKey,
            ClientEnvironmentSettings settings, ClientBackendStatus status, string expectedUid,
            Action<object> completed, Action<string> failed)
        {
            if (failed == null || completed == null)
                return;

            if (!ClientValue.IsSafeIdempotencyKey(idempotencyKey) ||
                string.IsNullOrWhiteSpace(name) || name.Length > 128 ||
                !city.IsValid || !MapTradeRegionCatalog.Includes(city) ||
                !IsAuthorized(settings, status, !string.IsNullOrEmpty(expectedUid)) ||
                status.Environment != ClientBackendEnvironment.LocalEmulator)
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

                var request = ClientValue.CommandRequest(idempotencyKey,
                    new Dictionary<string, object>
                    {
                        { "name", name },
                        { "headquarters", new Dictionary<string, object>
                            {
                                { "countryCode", city.CountryCode },
                                { "cityKey", city.CityKey }
                            }
                        }
                    });
                // Perform all restrictions again immediately before dispatch.
                if (!IsAuthorized(settings, FirebaseClient.Current, true))
                {
                    failed("HOLD");
                    return;
                }

                FirebaseFunctions.GetInstance(app, ClientProtocol.FunctionsRegion)
                    .GetHttpsCallable(ClientCallableCatalog.CreateCompanyWithHeadquarters)
                    .CallAsync(request)
                    .ContinueWithOnMainThread(task =>
                    {
                        if (task.IsCanceled || task.IsFaulted)
                        {
                            // The command may have committed even if the response
                            // was lost. The UI retains the same idempotency key.
                            failed(task.IsCanceled ? "UNCERTAIN" : "UNKNOWN");
                            return;
                        }
                        completed(task.Result != null ? task.Result.Data : null);
                    });
            }
            catch
            {
                failed("UNKNOWN");
            }
        }
    }
}
