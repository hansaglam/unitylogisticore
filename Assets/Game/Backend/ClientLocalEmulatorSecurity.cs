using System;

namespace LogistiCore.Backend
{
    /// <summary>
    /// Fail-closed local emulator read gate. Does not approve Development or Production.
    /// Does not flip ClientConnectionAuthorization.
    /// </summary>
    public static class ClientLocalEmulatorSecurity
    {
        public const int AuthPort = 9099;
        public const int FirestorePort = 8080;
        public const int FunctionsPort = 5001;
        public const string FunctionsOrigin = "http://127.0.0.1:5001";
        public const string InvalidProjectDetail = "Local emulator project id is not demo-logisticore-local.";
        public const string NonLoopbackDetail = "Local emulator host is not loopback.";
        public const string MissingEndpointDetail = "Local emulator Auth, Firestore, or Functions endpoint is missing.";
        public const string InvalidEndpointDetail = "Local emulator endpoints are not the approved loopback ports.";

        public static bool TryValidate(ClientEnvironmentSettings settings, out string fault)
        {
            fault = MissingEndpointDetail;
            if (settings == null)
                return false;
            if (settings.Environment != ClientBackendEnvironment.LocalEmulator)
            {
                fault = ClientConnectionPolicy.MissingEmulatorDetail;
                return false;
            }

            if (!string.Equals(settings.ProjectId, ClientProtocol.DemoProjectId, StringComparison.Ordinal))
            {
                fault = InvalidProjectDetail;
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.AppId) || string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                fault = ClientConnectionPolicy.MissingEmulatorDetail;
                return false;
            }

            if (!ClientEmulatorAuthPolicy.IsLoopbackEmulatorHost(settings.EmulatorHost))
            {
                fault = NonLoopbackDetail;
                return false;
            }

            if (settings.EmulatorAuthPort != AuthPort
                || settings.EmulatorFirestorePort != FirestorePort)
            {
                fault = InvalidEndpointDetail;
                return false;
            }

            if (!TryValidateFunctionsOrigin(settings.FunctionsEmulatorOrigin)
                || !string.Equals(settings.FunctionsRegion, ClientProtocol.FunctionsRegion, StringComparison.Ordinal))
            {
                fault = InvalidEndpointDetail;
                return false;
            }

            fault = null;
            return true;
        }

        public static bool AllowsLiveReads(ClientEnvironmentSettings settings, ClientBackendStatus status)
        {
            if (status.Environment == ClientBackendEnvironment.Production
                || status.Decision == ClientConnectionDecision.Refused
                || status.Decision == ClientConnectionDecision.StayOffline)
                return false;
            if (status.Decision != ClientConnectionDecision.ConnectToEmulator)
                return false;
            if (status.Phase != ClientBackendPhase.Ready)
                return false;
            return TryValidate(settings, out _);
        }

        public static bool TryValidateFunctionsOrigin(string origin)
        {
            if (string.IsNullOrWhiteSpace(origin))
                return false;
            Uri uri;
            if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out uri))
                return false;
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!ClientEmulatorAuthPolicy.IsLoopbackEmulatorHost(uri.Host))
                return false;
            if (uri.Port != FunctionsPort)
                return false;
            if (!string.IsNullOrEmpty(uri.UserInfo))
                return false;
            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                return false;
            var path = uri.AbsolutePath;
            return path == "/" || string.IsNullOrEmpty(path);
        }
    }

    public static class ClientReadAuthorization
    {
        public static bool IsApproved(ClientBackendStatus status)
        {
            if (ClientConnectionAuthorization.Approved)
                return true;
            return ClientLocalEmulatorSecurity.AllowsLiveReads(ClientJobsRuntime.Settings, status);
        }
    }

    /// <summary>
    /// Command gates reuse the read environment bar and never open production.
    /// Live Functions command dispatch stays HOLD unless a later owner window flips it.
    /// </summary>
    public static class ClientCommandAuthorization
    {
        public const string LiveHoldDetail = "Live gameplay commands are not authorized.";

        public static bool IsApproved(ClientBackendStatus status)
        {
            if (status.Environment == ClientBackendEnvironment.Production)
                return false;
            return ClientLocalEmulatorSecurity.AllowsLiveReads(ClientJobsRuntime.Settings, status);
        }

        public static bool AllowsLiveFunctions(ClientEnvironmentSettings settings, ClientBackendStatus status)
        {
            return false;
        }
    }
}
