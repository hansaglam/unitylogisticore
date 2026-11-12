using System;
using System.Collections.Generic;

namespace LogistiCore.Backend
{
    public enum ClientCallableKind
    {
        Read = 0,
        Command = 1
    }

    public enum ClientCallableFault
    {
        None = 0,
        Unconfigured = 1,
        ConnectionHold = 2,
        MissingAuth = 3,
        ExpiredAuth = 4,
        NetworkUnavailable = 5,
        Timeout = 6,
        PermissionDenied = 7,
        ProtocolMismatch = 8,
        MalformedResponse = 9,
        ServerUnavailable = 10,
        RetryableRead = 11,
        NonRetryableCommand = 12,
        DuplicateInFlight = 13,
        TransportDisabled = 14,
        Refused = 15
    }

    /// <summary>
    /// CLIENT-API-01 does not authorize a live Functions call. Flip only in tests or a later
    /// user-approved connectivity milestone.
    /// </summary>
    public static class ClientConnectionAuthorization
    {
        public const string HoldDetail = "CLIENT-API-01 connection is not authorized.";

        static readonly object Sync = new object();
        static bool _approved;

        public static bool Approved
        {
            get
            {
                lock (Sync)
                    return _approved;
            }
        }

        internal static void ApproveForTests()
        {
            lock (Sync)
                _approved = true;
        }

        internal static void ResetForTests()
        {
            lock (Sync)
                _approved = false;
        }
    }

    /// <summary>
    /// Default-deny switch for Editor live tests that talk to the Auth/Functions emulator.
    /// NUnit categories are documentation; this flag is what stops Run All from minting users.
    /// </summary>
    public static class ClientEmulatorLiveAuthorization
    {
        public const string EnvironmentVariable = "LOGISTICORE_EMU_LIVE";
        public const string ReadToken = "read";
        public const string AuthToken = "auth";
        public const string IdentityToken = "identity";
        public const string CompanyToken = "company";
        public const string EconomyToken = "economy";
        public const string FleetToken = "fleet";
        public const string MutateToken = "mutate";
        public const string ReadHoldDetail =
            "Emulator live reads are not authorized. Set LOGISTICORE_EMU_LIVE=read after explicit approval.";
        public const string AuthHoldDetail =
            "Emulator email/password Auth is not authorized. Set LOGISTICORE_EMU_LIVE=auth after Phase B approval.";
        public const string IdentityHoldDetail =
            "Emulator identity bootstrap is not authorized. Set LOGISTICORE_EMU_LIVE=identity after 01D approval.";
        public const string CompanyHoldDetail =
            "Emulator company creation is not authorized. Set LOGISTICORE_EMU_LIVE=company after 01E approval.";
        public const string EconomyHoldDetail =
            "Emulator economy initialization is not authorized. Set LOGISTICORE_EMU_LIVE=economy after 02B approval.";
        public const string FleetHoldDetail =
            "Emulator starter fleet grant is not authorized. Set LOGISTICORE_EMU_LIVE=fleet after 03B approval.";
        public const string MutationHoldDetail =
            "Emulator live mutations are not authorized. Set LOGISTICORE_EMU_LIVE=mutate after explicit approval.";

        static readonly object Sync = new object();
        static bool _testRead;
        static bool _testAuth;
        static bool _testIdentity;
        static bool _testCompany;
        static bool _testEconomy;
        static bool _testFleet;
        static bool _testMutate;

        public static bool AllowsRead
        {
            get
            {
                if (AllowsMutation)
                    return true;
                lock (Sync)
                {
                    if (_testRead)
                        return true;
                }

                return TokenIs(ReadToken);
            }
        }

        public static bool AllowsAuth
        {
            get
            {
                if (AllowsMutation)
                    return true;
                lock (Sync)
                {
                    if (_testAuth)
                        return true;
                }

                return TokenIs(AuthToken);
            }
        }

        public static bool AllowsIdentity
        {
            get
            {
                if (AllowsMutation)
                    return true;
                lock (Sync)
                {
                    if (_testIdentity)
                        return true;
                }

                return TokenIs(IdentityToken);
            }
        }

        public static bool AllowsCompany
        {
            get
            {
                if (AllowsMutation)
                    return true;
                lock (Sync)
                {
                    if (_testCompany)
                        return true;
                }

                return TokenIs(CompanyToken);
            }
        }

        public static bool AllowsEconomy
        {
            get
            {
                if (AllowsMutation)
                    return true;
                lock (Sync)
                {
                    if (_testEconomy)
                        return true;
                }

                return TokenIs(EconomyToken);
            }
        }

        public static bool AllowsFleet
        {
            get
            {
                if (AllowsMutation)
                    return true;
                lock (Sync)
                {
                    if (_testFleet)
                        return true;
                }

                return TokenIs(FleetToken);
            }
        }

        public static bool AllowsMutation
        {
            get
            {
                lock (Sync)
                {
                    if (_testMutate)
                        return true;
                }

                return TokenIs(MutateToken);
            }
        }

        static bool TokenIs(string expected)
        {
            var value = Environment.GetEnvironmentVariable(EnvironmentVariable);
            return string.Equals(value, expected, StringComparison.Ordinal);
        }

        internal static void ApproveReadsForTests()
        {
            lock (Sync)
                _testRead = true;
        }

        internal static void ApproveAuthForTests()
        {
            lock (Sync)
                _testAuth = true;
        }

        internal static void ApproveIdentityForTests()
        {
            lock (Sync)
                _testIdentity = true;
        }

        internal static void ApproveCompanyForTests()
        {
            lock (Sync)
                _testCompany = true;
        }

        internal static void ApproveEconomyForTests()
        {
            lock (Sync)
                _testEconomy = true;
        }

        internal static void ApproveFleetForTests()
        {
            lock (Sync)
                _testFleet = true;
        }

        internal static void ApproveMutationsForTests()
        {
            lock (Sync)
                _testMutate = true;
        }

        internal static void ResetForTests()
        {
            lock (Sync)
            {
                _testRead = false;
                _testAuth = false;
                _testIdentity = false;
                _testCompany = false;
                _testEconomy = false;
                _testFleet = false;
                _testMutate = false;
            }
        }
    }

    /// <summary>
    /// Development Auth rules for the emulator. Does not create users, sign in, or store secrets.
    /// Anonymous create/sign-in is a mutation. Password sign-in of an existing emulator user is a live read.
    /// </summary>
    public static class ClientEmulatorAuthPolicy
    {
        public const string AnonymousHold =
            "Anonymous Auth is not part of the default test suite.";
        public const string CreateUserHold =
            "Creating an Auth user requires an explicit live-mutation approval.";
        public const string PasswordHold =
            "Emulator password sign-in requires an explicit live-read or Phase B auth approval.";

        public static bool MayCreateAnonymousUser => ClientEmulatorLiveAuthorization.AllowsMutation;
        public static bool MaySignInAnonymously => ClientEmulatorLiveAuthorization.AllowsMutation;
        public static bool MayCreatePasswordUser =>
            ClientEmulatorLiveAuthorization.AllowsAuth || ClientEmulatorLiveAuthorization.AllowsMutation;
        public static bool MaySignInExistingPassword =>
            ClientEmulatorLiveAuthorization.AllowsRead
            || ClientEmulatorLiveAuthorization.AllowsAuth
            || ClientEmulatorLiveAuthorization.AllowsIdentity
            || ClientEmulatorLiveAuthorization.AllowsCompany
            || ClientEmulatorLiveAuthorization.AllowsEconomy
            || ClientEmulatorLiveAuthorization.AllowsFleet;

        public static bool MayEnsurePlayerIdentity => ClientEmulatorLiveAuthorization.AllowsIdentity;

        public static bool MayCreateCompany => ClientEmulatorLiveAuthorization.AllowsCompany;

        public static bool MayInitializeEconomy => ClientEmulatorLiveAuthorization.AllowsEconomy;

        public static bool MayProvisionStarterFleet => ClientEmulatorLiveAuthorization.AllowsFleet;

        public static bool IsLoopbackEmulatorHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;
            var value = host.Trim();
            return string.Equals(value, "127.0.0.1", StringComparison.Ordinal)
                || string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSyntheticTestEmail(string email)
        {
            return string.Equals(email, ClientEmulatorAuthCredentials.Email, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Public email and owner-controlled secret locations. Password is never stored here.
    /// </summary>
    public static class ClientEmulatorAuthCredentials
    {
        public const string Email = "logisticore-editor-integration@example.test";
        public const string PasswordEnvironmentVariable = "LOGISTICORE_EMU_AUTH_PASSWORD";
        public const string FileEnvironmentVariable = "LOGISTICORE_EMU_AUTH_FILE";
        public const string MissingSecretDetail =
            "Phase B password is not configured. Set LOGISTICORE_EMU_AUTH_FILE to an owner file outside the repo.";
    }

    public static class ClientProtocol
    {
        public const int Version = 1;
        public const string DemoProjectId = "demo-logisticore-local";
        public const string FunctionsRegion = "us-central1";

        public static bool IsSupportedVersion(int version)
        {
            return version == Version;
        }
    }

    public static class ClientCallableCatalog
    {
        public const string LocalHealth = "localHealth";
        public const string EnsurePlayerIdentity = "ensurePlayerIdentity";
        public const string GetMyCompanyState = "getMyCompanyState";
        public const string GetMyEconomyState = "getMyEconomyState";
        public const string GetMyFleetState = "getMyFleetState";
        public const string GetMyWarehouseState = "getMyWarehouseState";
        public const string GetMyContracts = "getMyContracts";
        public const string GetMyDeliveries = "getMyDeliveries";
        public const string GetMyMarketState = "getMyMarketState";
        public const string GetMyAcquisitionOffers = "getMyAcquisitionOffers";
        public const string GetMyWorldEvents = "getMyWorldEvents";
        public const string GetMyBankState = "getMyBankState";
        public const string CreateCompany = "createCompany";
        public const string InitializeCompanyEconomy = "initializeCompanyEconomy";
        public const string ProvisionCompanyStarterFleet = "provisionCompanyStarterFleet";
        public const string AcceptContract = "acceptContract";
        public const string StartDelivery = "startDelivery";
        public const string ClaimDeliveryReward = "claimDeliveryReward";
        public const string BuyVehicle = "buyVehicle";
        public const string HireDriver = "hireDriver";
        public const string AcquireTrailer = "acquireTrailer";
        public const string OpenWarehouse = "openWarehouse";
        public const string UpgradeWarehouse = "upgradeWarehouse";
        public const string QuoteCompanyLoan = "quoteCompanyLoan";
        public const string CancelCompanyLoanQuote = "cancelCompanyLoanQuote";
        public const string TakeCompanyLoan = "takeCompanyLoan";
        public const string RepayCompanyLoan = "repayCompanyLoan";

        static readonly string[] Reads =
        {
            LocalHealth, GetMyCompanyState, GetMyEconomyState, GetMyFleetState, GetMyWarehouseState,
            GetMyContracts, GetMyDeliveries, GetMyMarketState, GetMyAcquisitionOffers, GetMyWorldEvents,
            GetMyBankState
        };

        static readonly string[] Commands =
        {
            EnsurePlayerIdentity, CreateCompany, InitializeCompanyEconomy, ProvisionCompanyStarterFleet, AcceptContract, StartDelivery, ClaimDeliveryReward,
            BuyVehicle, HireDriver, AcquireTrailer, OpenWarehouse, UpgradeWarehouse,
            QuoteCompanyLoan, CancelCompanyLoanQuote, TakeCompanyLoan, RepayCompanyLoan
        };

        public static IReadOnlyList<string> ReadNames => Reads;
        public static IReadOnlyList<string> CommandNames => Commands;

        public static bool TryKind(string name, out ClientCallableKind kind)
        {
            for (var i = 0; i < Reads.Length; i++)
            {
                if (Reads[i] == name)
                {
                    kind = ClientCallableKind.Read;
                    return true;
                }
            }

            for (var i = 0; i < Commands.Length; i++)
            {
                if (Commands[i] == name)
                {
                    kind = ClientCallableKind.Command;
                    return true;
                }
            }

            kind = default;
            return false;
        }

        public static bool IsRead(string name)
        {
            return TryKind(name, out var kind) && kind == ClientCallableKind.Read;
        }

        public static bool IsCommand(string name)
        {
            return TryKind(name, out var kind) && kind == ClientCallableKind.Command;
        }

        public static bool IsSupportedPlayerCommand(string name)
        {
            return name == AcceptContract;
        }
    }

    public static class ClientCallableErrors
    {
        public static ClientCallableFault FromDetailsCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return ClientCallableFault.MalformedResponse;
            switch (code)
            {
                case "UNAUTHENTICATED":
                    return ClientCallableFault.MissingAuth;
                case "PERMISSION_DENIED":
                    return ClientCallableFault.PermissionDenied;
                case "UNSUPPORTED_PROTOCOL":
                    return ClientCallableFault.ProtocolMismatch;
                case "RETRY_LATER":
                    return ClientCallableFault.RetryableRead;
                case "INTERNAL":
                    return ClientCallableFault.ServerUnavailable;
                default:
                    return ClientCallableFault.NonRetryableCommand;
            }
        }

        public static bool MayRetryRead(ClientCallableFault fault)
        {
            return fault == ClientCallableFault.RetryableRead
                || fault == ClientCallableFault.NetworkUnavailable
                || fault == ClientCallableFault.Timeout
                || fault == ClientCallableFault.ServerUnavailable;
        }

        public static bool MayRetryCommand(ClientCallableFault fault)
        {
            return false;
        }

        public static string MessageKey(string detailsCode)
        {
            if (string.IsNullOrEmpty(detailsCode))
                return "errors.internal";
            return "errors." + detailsCode.ToLowerInvariant();
        }
    }

    public static class ClientConnectionGate
    {
        public static ClientCallableFault Inspect(
            ClientBackendStatus status,
            bool hasUid,
            bool connectionApproved)
        {
            if (!connectionApproved)
                return ClientCallableFault.ConnectionHold;
            if (status.Decision == ClientConnectionDecision.StayOffline)
                return ClientCallableFault.Unconfigured;
            if (status.Decision == ClientConnectionDecision.Refused)
                return ClientCallableFault.Refused;
            if (status.Phase == ClientBackendPhase.Starting)
                return ClientCallableFault.Timeout;
            if (status.Phase == ClientBackendPhase.Failed)
                return ClientCallableFault.ServerUnavailable;
            if (status.Phase != ClientBackendPhase.Ready)
                return ClientCallableFault.Unconfigured;
            if (!hasUid)
                return ClientCallableFault.MissingAuth;
            return ClientCallableFault.None;
        }
    }
}
