#if UNITY_EDITOR
using LogistiCore.Backend;

namespace LogistiCore.EditorTools
{
    /// <summary>
    /// Fail-closed checks for the Editor Play Mode emulator Auth test tool.
    /// Does not sign in and does not call Firebase.
    /// </summary>
    public static class EmulatorAuthTestGate
    {
        public const string ExpectedUid = "oVzAaTajRgPMQzwlFTFectt0Sypu";
        public const string MenuPath = "LogistiCore/Dev/Emulator Auth Test";
        public const string PlayModeRequired = "Sign-in is only allowed in Play Mode.";
        public const string ExplicitActionRequired = "Sign-in requires an explicit action.";
        public const string EmailNotAllowed = "Only the approved emulator test email is allowed.";
        public const string PasswordRequired = "Password is required.";
        public const string SignInFailed = "Sign-in failed.";
        public const string UnexpectedAccount = "Unexpected account.";
        public const string NotReady = "Firebase is not ready on the local emulator.";

        public static bool TryAllow(
            bool isPlaying,
            ClientEnvironmentSettings settings,
            ClientBackendStatus status,
            out string fault)
        {
            if (!isPlaying)
            {
                fault = PlayModeRequired;
                return false;
            }

            if (settings != null && settings.Environment == ClientBackendEnvironment.Production
                || status.Environment == ClientBackendEnvironment.Production)
            {
                fault = ClientConnectionPolicy.ProductionDetail;
                return false;
            }

            if (status.Decision == ClientConnectionDecision.StayOffline)
            {
                fault = ClientConnectionPolicy.OfflineDetail;
                return false;
            }

            string validateFault;
            if (!ClientLocalEmulatorSecurity.TryValidate(settings, out validateFault))
            {
                fault = validateFault ?? ClientConnectionPolicy.MissingEmulatorDetail;
                return false;
            }

            if (!ClientLocalEmulatorSecurity.AllowsLiveReads(settings, status))
            {
                fault = NotReady;
                return false;
            }

            fault = null;
            return true;
        }

        public static bool TrySignInExisting(
            bool userClicked,
            bool isPlaying,
            ClientEnvironmentSettings settings,
            ClientBackendStatus status,
            string email,
            string password,
            EmulatorAuthExistingSignIn signIn,
            System.Action signOut,
            out string uid,
            out string fault)
        {
            uid = "";
            if (!userClicked)
            {
                fault = ExplicitActionRequired;
                return false;
            }

            if (!TryAllow(isPlaying, settings, status, out fault))
                return false;

            if (!ClientEmulatorAuthPolicy.IsSyntheticTestEmail(email))
            {
                fault = EmailNotAllowed;
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                fault = PasswordRequired;
                return false;
            }

            if (signIn == null)
            {
                fault = SignInFailed;
                return false;
            }

            string signedUid;
            try
            {
                signedUid = signIn(email, password);
            }
            catch (System.Exception)
            {
                fault = SignInFailed;
                return false;
            }

            if (!string.Equals(signedUid, ExpectedUid, System.StringComparison.Ordinal))
            {
                if (signOut != null)
                    signOut();
                fault = UnexpectedAccount;
                return false;
            }

            uid = signedUid;
            fault = null;
            return true;
        }
    }

    public delegate string EmulatorAuthExistingSignIn(string email, string password);
}
#endif
