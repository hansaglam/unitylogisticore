using System;
using System.IO;
using System.Threading.Tasks;
using Firebase.Auth;
using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Tests
{
    /// <summary>
    /// Editor-only Auth emulator helper. Does not run unless Phase B live authorization is on.
    /// Never logs passwords or tokens.
    /// </summary>
    public static class ClientEmulatorPasswordAuth
    {
        const string RepoMarker = "/LogistiCore/";

        public static bool TryLoadPassword(out string password, out string fault)
        {
            password = Environment.GetEnvironmentVariable(
                ClientEmulatorAuthCredentials.PasswordEnvironmentVariable);
            if (!string.IsNullOrEmpty(password))
            {
                fault = null;
                return true;
            }

            var path = Environment.GetEnvironmentVariable(ClientEmulatorAuthCredentials.FileEnvironmentVariable);
            if (string.IsNullOrEmpty(path))
            {
                fault = ClientEmulatorAuthCredentials.MissingSecretDetail;
                return false;
            }

            if (!Path.IsPathRooted(path) || path.IndexOf(RepoMarker, StringComparison.Ordinal) >= 0)
            {
                fault = "Auth secret file must be an absolute path outside the repository.";
                password = null;
                return false;
            }

            try
            {
                if (!File.Exists(path))
                {
                    fault = ClientEmulatorAuthCredentials.MissingSecretDetail;
                    password = null;
                    return false;
                }

                password = File.ReadAllText(path).Trim();
                if (password.Length < 12)
                {
                    password = null;
                    fault = ClientEmulatorAuthCredentials.MissingSecretDetail;
                    return false;
                }

                fault = null;
                return true;
            }
            catch (Exception)
            {
                password = null;
                fault = ClientEmulatorAuthCredentials.MissingSecretDetail;
                return false;
            }
        }

        public static bool TryValidateEmulator(ClientEnvironmentSettings settings, out string fault)
        {
            if (settings == null || settings.Environment != ClientBackendEnvironment.LocalEmulator)
            {
                fault = "Local emulator environment is required.";
                return false;
            }

            if (!ClientEmulatorAuthPolicy.IsLoopbackEmulatorHost(settings.EmulatorHost))
            {
                fault = "Auth emulator host is not loopback.";
                return false;
            }

            if (settings.EmulatorAuthPort != 9099)
            {
                fault = "Auth emulator port must be 9099.";
                return false;
            }

            if (settings.ProjectId != ClientProtocol.DemoProjectId)
            {
                fault = "Unexpected Firebase project.";
                return false;
            }

            fault = null;
            return true;
        }

        public static async Task<string> SignInExistingOnly(FirebaseAuth auth, string email, string password)
        {
            if (auth == null)
                throw new InvalidOperationException("Auth is missing.");
            if (!ClientEmulatorAuthPolicy.IsSyntheticTestEmail(email))
                throw new InvalidOperationException("Unexpected test email.");
            if (!ClientEmulatorAuthPolicy.MaySignInExistingPassword)
                throw new InvalidOperationException(ClientEmulatorAuthPolicy.PasswordHold);
            if (ClientEmulatorAuthPolicy.MayCreatePasswordUser)
                throw new InvalidOperationException(ClientEmulatorAuthPolicy.CreateUserHold);

            var existing = await auth.SignInWithEmailAndPasswordAsync(email, password);
            if (existing == null || existing.User == null)
                throw new InvalidOperationException("Password sign-in returned no user.");
            return existing.User.UserId;
        }

        public static async Task<string> SignInOrCreateOnce(
            FirebaseAuth auth, string email, string password, bool allowCreate)
        {
            if (auth == null)
                throw new InvalidOperationException("Auth is missing.");
            if (!ClientEmulatorAuthPolicy.IsSyntheticTestEmail(email))
                throw new InvalidOperationException("Unexpected test email.");
            if (!ClientEmulatorAuthPolicy.MaySignInExistingPassword)
                throw new InvalidOperationException(ClientEmulatorAuthPolicy.PasswordHold);

            try
            {
                var existing = await auth.SignInWithEmailAndPasswordAsync(email, password);
                return existing.User.UserId;
            }
            catch (Exception)
            {
                if (!allowCreate)
                    throw new InvalidOperationException("Password sign-in failed without creating a user.");
            }

            if (!ClientEmulatorAuthPolicy.MayCreatePasswordUser)
                throw new InvalidOperationException(ClientEmulatorAuthPolicy.CreateUserHold);

            try
            {
                var created = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
                return created.User.UserId;
            }
            catch (Exception)
            {
                throw new InvalidOperationException("Auth create failed; existing account was not overridden.");
            }
        }
    }
}
