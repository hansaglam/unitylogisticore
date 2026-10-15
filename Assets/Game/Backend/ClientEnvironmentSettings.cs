using UnityEngine;

namespace LogistiCore.Backend
{
    /// <summary>
    /// Client environment selection. The committed asset stays Unconfigured and empty.
    /// Non-production values belong in the gitignored LocalClientEnvironment resource, which wins when present.
    /// </summary>
    public sealed class ClientEnvironmentSettings : ScriptableObject
    {
        public const string SharedResourceName = "ClientEnvironment";
        public const string LocalResourceName = "LocalClientEnvironment";

        [SerializeField]
        [Header("Unconfigured is the only safe default. Do not paste production configuration.")]
        ClientBackendEnvironment environment = ClientBackendEnvironment.Unconfigured;

        [SerializeField] string projectId = "";
        [SerializeField] string appId = "";
        [SerializeField] string apiKey = "";
        [SerializeField] string emulatorHost = "";
        [SerializeField] int emulatorFirestorePort;
        [SerializeField] int emulatorAuthPort;
        [SerializeField] string functionsEmulatorOrigin = "";
        [SerializeField] string functionsRegion = "";

        public ClientBackendEnvironment Environment => environment;

        internal string ProjectId => projectId ?? "";
        internal string AppId => appId ?? "";
        internal string ApiKey => apiKey ?? "";
        internal string EmulatorHost => emulatorHost ?? "";
        internal int EmulatorFirestorePort => emulatorFirestorePort;
        internal int EmulatorAuthPort => emulatorAuthPort;
        internal string FunctionsEmulatorOrigin => functionsEmulatorOrigin ?? "";
        internal string FunctionsRegion => functionsRegion ?? "";

        public static ClientEnvironmentSettings Load()
        {
            var local = Resources.Load<ClientEnvironmentSettings>(LocalResourceName);
            var shared = Resources.Load<ClientEnvironmentSettings>(SharedResourceName);
            return Select(local, shared);
        }

        internal static ClientEnvironmentSettings Select(
            ClientEnvironmentSettings local,
            ClientEnvironmentSettings shared)
        {
            if (local != null)
                return local;
            if (shared != null)
                return shared;
            return CreateInstance<ClientEnvironmentSettings>();
        }

        public void Configure(
            ClientBackendEnvironment backendEnvironment,
            string nextProjectId = "",
            string nextAppId = "",
            string nextApiKey = "",
            string nextEmulatorHost = "",
            int nextEmulatorFirestorePort = 0,
            int nextEmulatorAuthPort = 0,
            string nextFunctionsEmulatorOrigin = "",
            string nextFunctionsRegion = "")
        {
            environment = backendEnvironment;
            projectId = nextProjectId ?? "";
            appId = nextAppId ?? "";
            apiKey = nextApiKey ?? "";
            emulatorHost = nextEmulatorHost ?? "";
            emulatorFirestorePort = nextEmulatorFirestorePort;
            emulatorAuthPort = nextEmulatorAuthPort;
            functionsEmulatorOrigin = nextFunctionsEmulatorOrigin ?? "";
            functionsRegion = nextFunctionsRegion ?? "";
        }

        public ClientConnectionRequest ToRequest()
        {
            return new ClientConnectionRequest(
                environment,
                HasText(projectId) && HasText(appId) && HasText(apiKey),
                HasEmulatorEndpoint());
        }

        bool HasEmulatorEndpoint()
        {
            var hostReady = HasText(emulatorHost);
            if (hostReady && IsPort(emulatorFirestorePort))
                return true;
            if (hostReady && IsPort(emulatorAuthPort))
                return true;
            return HasText(functionsEmulatorOrigin) && HasText(functionsRegion);
        }

        static bool HasText(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        static bool IsPort(int port)
        {
            return port >= 1 && port <= 65535;
        }
    }
}
