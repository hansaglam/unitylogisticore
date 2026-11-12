using System;
using LogistiCore.Backend;
using NUnit.Framework;

namespace LogistiCore.Tests
{
    public static class ClientLiveTestGuard
    {
        public const string ReadCategory = "EmulatorRead";
        public const string AuthCategory = "EmulatorAuth";
        public const string IdentityCategory = "EmulatorIdentity";
        public const string CompanyCategory = "EmulatorCompany";
        public const string EconomyCategory = "EmulatorEconomy";
        public const string StarterFleetCategory = "EmulatorStarterFleet";
        public const string MutationCategory = "EmulatorMutation";

        public static void RequireRead()
        {
            if (!ClientEmulatorLiveAuthorization.AllowsRead)
                Assert.Ignore(ClientEmulatorLiveAuthorization.ReadHoldDetail);
        }

        public static void RequireReadOnly()
        {
            var token = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            if (!string.Equals(token, ClientEmulatorLiveAuthorization.ReadToken, StringComparison.Ordinal))
                Assert.Ignore(ClientEmulatorLiveAuthorization.ReadHoldDetail);
            if (ClientEmulatorLiveAuthorization.AllowsMutation)
                Assert.Ignore("Read-only live tests must not run under LOGISTICORE_EMU_LIVE=mutate.");
            if (!ClientEmulatorAuthPolicy.MaySignInExistingPassword)
                Assert.Ignore(ClientEmulatorAuthPolicy.PasswordHold);
            if (ClientEmulatorAuthPolicy.MayCreatePasswordUser || ClientEmulatorAuthPolicy.MaySignInAnonymously)
                Assert.Ignore("Read-only live tests refuse Auth account creation.");
        }

        public static void RequireIdentity()
        {
            var token = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            if (!string.Equals(token, ClientEmulatorLiveAuthorization.IdentityToken, StringComparison.Ordinal))
                Assert.Ignore(ClientEmulatorLiveAuthorization.IdentityHoldDetail);
            if (ClientEmulatorLiveAuthorization.AllowsMutation)
                Assert.Ignore("Identity live tests must not run under LOGISTICORE_EMU_LIVE=mutate.");
            if (!ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity)
                Assert.Ignore(ClientEmulatorLiveAuthorization.IdentityHoldDetail);
            if (!ClientEmulatorAuthPolicy.MaySignInExistingPassword)
                Assert.Ignore(ClientEmulatorAuthPolicy.PasswordHold);
            if (ClientEmulatorAuthPolicy.MayCreatePasswordUser || ClientEmulatorAuthPolicy.MaySignInAnonymously)
                Assert.Ignore("Identity live tests refuse Auth account creation.");
        }

        public static void RequireCompany()
        {
            var token = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            if (!string.Equals(token, ClientEmulatorLiveAuthorization.CompanyToken, StringComparison.Ordinal))
                Assert.Ignore(ClientEmulatorLiveAuthorization.CompanyHoldDetail);
            if (ClientEmulatorLiveAuthorization.AllowsMutation)
                Assert.Ignore("Company live tests must not run under LOGISTICORE_EMU_LIVE=mutate.");
            if (!ClientEmulatorAuthPolicy.MayCreateCompany)
                Assert.Ignore(ClientEmulatorLiveAuthorization.CompanyHoldDetail);
            if (ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity)
                Assert.Ignore("Company live tests must not call ensurePlayerIdentity.");
            if (!ClientEmulatorAuthPolicy.MaySignInExistingPassword)
                Assert.Ignore(ClientEmulatorAuthPolicy.PasswordHold);
            if (ClientEmulatorAuthPolicy.MayCreatePasswordUser || ClientEmulatorAuthPolicy.MaySignInAnonymously)
                Assert.Ignore("Company live tests refuse Auth account creation.");
        }

        public static void RequireEconomy()
        {
            var token = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            if (!string.Equals(token, ClientEmulatorLiveAuthorization.EconomyToken, StringComparison.Ordinal))
                Assert.Ignore(ClientEmulatorLiveAuthorization.EconomyHoldDetail);
            if (ClientEmulatorLiveAuthorization.AllowsMutation)
                Assert.Ignore("Economy live tests must not run under LOGISTICORE_EMU_LIVE=mutate.");
            if (!ClientEmulatorAuthPolicy.MayInitializeEconomy)
                Assert.Ignore(ClientEmulatorLiveAuthorization.EconomyHoldDetail);
            if (ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity)
                Assert.Ignore("Economy live tests must not call ensurePlayerIdentity.");
            if (ClientEmulatorAuthPolicy.MayCreateCompany)
                Assert.Ignore("Economy live tests must not create a company.");
            if (!ClientEmulatorAuthPolicy.MaySignInExistingPassword)
                Assert.Ignore(ClientEmulatorAuthPolicy.PasswordHold);
            if (ClientEmulatorAuthPolicy.MayCreatePasswordUser || ClientEmulatorAuthPolicy.MaySignInAnonymously)
                Assert.Ignore("Economy live tests refuse Auth account creation.");
        }

        public static void RequireFleet()
        {
            var token = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            if (!string.Equals(token, ClientEmulatorLiveAuthorization.FleetToken, StringComparison.Ordinal))
                Assert.Ignore(ClientEmulatorLiveAuthorization.FleetHoldDetail);
            if (ClientEmulatorLiveAuthorization.AllowsMutation)
                Assert.Ignore("Fleet live tests must not run under LOGISTICORE_EMU_LIVE=mutate.");
            if (!ClientEmulatorAuthPolicy.MayProvisionStarterFleet)
                Assert.Ignore(ClientEmulatorLiveAuthorization.FleetHoldDetail);
            if (ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity)
                Assert.Ignore("Fleet live tests must not call ensurePlayerIdentity.");
            if (ClientEmulatorAuthPolicy.MayCreateCompany)
                Assert.Ignore("Fleet live tests must not create a company.");
            if (ClientEmulatorAuthPolicy.MayInitializeEconomy)
                Assert.Ignore("Fleet live tests must not initialize economy.");
            if (!ClientEmulatorAuthPolicy.MaySignInExistingPassword)
                Assert.Ignore(ClientEmulatorAuthPolicy.PasswordHold);
            if (ClientEmulatorAuthPolicy.MayCreatePasswordUser || ClientEmulatorAuthPolicy.MaySignInAnonymously)
                Assert.Ignore("Fleet live tests refuse Auth account creation.");
        }

        public static void RequireAuth()
        {
            if (!ClientEmulatorLiveAuthorization.AllowsAuth)
                Assert.Ignore(ClientEmulatorLiveAuthorization.AuthHoldDetail);
            if (ClientEmulatorLiveAuthorization.AllowsMutation &&
                !string.Equals(
                    Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable),
                    ClientEmulatorLiveAuthorization.AuthToken,
                    StringComparison.Ordinal))
            {
                Assert.Ignore(
                    "Phase B must not run under LOGISTICORE_EMU_LIVE=mutate. Use auth only.");
            }
        }

        public static void RequireMutation()
        {
            if (!ClientEmulatorLiveAuthorization.AllowsMutation)
                Assert.Ignore(ClientEmulatorLiveAuthorization.MutationHoldDetail);
            if (!ClientEmulatorAuthPolicy.MaySignInAnonymously)
                Assert.Ignore(ClientEmulatorAuthPolicy.AnonymousHold);
        }
    }
}
