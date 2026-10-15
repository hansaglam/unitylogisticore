namespace LogistiCore.Backend
{
    public enum ClientBackendEnvironment
    {
        Unconfigured = 0,
        LocalEmulator = 1,
        Development = 2,
        Production = 3
    }

    public enum ClientConnectionDecision
    {
        StayOffline = 0,
        Refused = 1,
        ConnectToEmulator = 2,
        ConnectToDevelopment = 3
    }

    public enum ClientBackendPhase
    {
        Idle = 0,
        Offline = 1,
        Refused = 2,
        Starting = 3,
        Ready = 4,
        Failed = 5
    }

    /// <summary>
    /// Facts the connection policy is allowed to see. Project ids and keys stay on the settings asset.
    /// </summary>
    public readonly struct ClientConnectionRequest
    {
        public ClientConnectionRequest(
            ClientBackendEnvironment environment,
            bool hasAppConfiguration,
            bool hasEmulatorEndpoint)
        {
            Environment = environment;
            HasAppConfiguration = hasAppConfiguration;
            HasEmulatorEndpoint = hasEmulatorEndpoint;
        }

        public static ClientConnectionRequest Unconfigured =>
            new ClientConnectionRequest(ClientBackendEnvironment.Unconfigured, false, false);

        public ClientBackendEnvironment Environment { get; }
        public bool HasAppConfiguration { get; }
        public bool HasEmulatorEndpoint { get; }
    }

    /// <summary>
    /// Decides whether this process may open a Firebase connection.
    /// Production is always refused. Missing development or emulator configuration never selects another environment.
    /// </summary>
    public static class ClientConnectionPolicy
    {
        public const string OfflineDetail = "Unconfigured. No Firebase connection was opened.";
        public const string MissingDevelopmentDetail = "Development configuration is missing. Production was not used.";
        public const string MissingEmulatorDetail = "Emulator configuration is missing. Production was not used.";
        public const string ProductionDetail = "Production is disabled. No Firebase connection was opened.";
        public const string UnrecognizedDetail = "The backend environment is not recognized. No Firebase connection was opened.";

        public static ClientConnectionDecision Decide(ClientConnectionRequest request)
        {
            switch (request.Environment)
            {
                case ClientBackendEnvironment.Unconfigured:
                    return ClientConnectionDecision.StayOffline;
                case ClientBackendEnvironment.Development:
                    return request.HasAppConfiguration
                        ? ClientConnectionDecision.ConnectToDevelopment
                        : ClientConnectionDecision.Refused;
                case ClientBackendEnvironment.LocalEmulator:
                    return request.HasAppConfiguration && request.HasEmulatorEndpoint
                        ? ClientConnectionDecision.ConnectToEmulator
                        : ClientConnectionDecision.Refused;
                default:
                    return ClientConnectionDecision.Refused;
            }
        }

        public static string Describe(ClientBackendEnvironment environment, ClientConnectionDecision decision)
        {
            if (decision == ClientConnectionDecision.StayOffline)
                return OfflineDetail;

            if (decision != ClientConnectionDecision.Refused)
                return UnrecognizedDetail;

            switch (environment)
            {
                case ClientBackendEnvironment.Development:
                    return MissingDevelopmentDetail;
                case ClientBackendEnvironment.LocalEmulator:
                    return MissingEmulatorDetail;
                case ClientBackendEnvironment.Production:
                    return ProductionDetail;
                default:
                    return UnrecognizedDetail;
            }
        }
    }
}
