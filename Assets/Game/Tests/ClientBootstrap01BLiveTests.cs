using System;
using System.Collections;
using System.IO;
using Firebase;
using Firebase.Auth;
using LogistiCore.Backend;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LogistiCore.Tests
{
    [Explicit]
    [Category(ClientLiveTestGuard.AuthCategory)]
    public class ClientBootstrap01BLiveTests
    {
        const string EvidenceDir = "/tmp/logisticore-unified-bootstrap-01b";
        const string UidPath = EvidenceDir + "/uid-continuity.json";
        const string ProcessVariable = "LOGISTICORE_EMU_AUTH_PROCESS";
        const float StepSeconds = 90f;

        [SetUp]
        public void GateLiveEmulator()
        {
            ClientLiveTestGuard.RequireAuth();
        }

        [UnityTest]
        public IEnumerator Editor_EmailPassword_ReusesOneUidAcrossEditorProcesses()
        {
            var process = Environment.GetEnvironmentVariable(ProcessVariable);
            if (process != "1" && process != "2")
            {
                Assert.Ignore(
                    "Set LOGISTICORE_EMU_AUTH_PROCESS=1 then a second Editor run with =2 after Phase B approval.");
                yield break;
            }

            var settings = ClientEnvironmentSettings.Load();
            string emulatorFault;
            Assert.IsTrue(ClientEmulatorPasswordAuth.TryValidateEmulator(settings, out emulatorFault), emulatorFault);
            Assert.IsTrue(ClientEmulatorAuthPolicy.IsSyntheticTestEmail(ClientEmulatorAuthCredentials.Email));

            string password;
            string secretFault;
            Assert.IsTrue(ClientEmulatorPasswordAuth.TryLoadPassword(out password, out secretFault), secretFault);

            LogAssert.ignoreFailingMessages = true;
            ClientConnectionAuthorization.ResetForTests();
            ClientConnectionAuthorization.ApproveForTests();
            FirebaseClient.ResetForTests();
            var status = FirebaseClient.Start(settings);
            var readyDeadline = Time.realtimeSinceStartup + StepSeconds;
            while (FirebaseClient.Current.Phase == ClientBackendPhase.Starting &&
                   Time.realtimeSinceStartup < readyDeadline)
                yield return null;
            status = FirebaseClient.Current;
            Assert.AreEqual(ClientBackendPhase.Ready, status.Phase);

            var app = FirebaseApp.DefaultInstance;
            Assert.IsNotNull(app);
            Assert.AreEqual(ClientProtocol.DemoProjectId, app.Options.ProjectId);
            var auth = FirebaseAuth.GetAuth(app);
            Assert.IsFalse(ClientEmulatorAuthPolicy.MaySignInAnonymously);

            var signIn = ClientEmulatorPasswordAuth.SignInOrCreateOnce(
                auth, ClientEmulatorAuthCredentials.Email, password, process == "1");
            yield return WaitTask(signIn, StepSeconds);
            Assert.IsTrue(signIn.IsCompleted);
            if (signIn.IsFaulted)
                Assert.Fail(FaultName(signIn.Exception));
            var uid = signIn.Result;
            Assert.IsFalse(string.IsNullOrEmpty(uid));
            Assert.AreEqual(uid, auth.CurrentUser.UserId);

            if (process == "1")
            {
                Directory.CreateDirectory(EvidenceDir);
                File.WriteAllText(UidPath, "{\"uid\":\"" + uid + "\"}\n");
                auth.SignOut();
                yield break;
            }

            Assert.IsTrue(File.Exists(UidPath), "Process 1 UID evidence is required.");
            var recorded = File.ReadAllText(UidPath);
            Assert.IsTrue(recorded.IndexOf(uid, StringComparison.Ordinal) >= 0, "UID mismatch across Editor processes.");
            auth.SignOut();
        }

        static IEnumerator WaitTask(System.Threading.Tasks.Task task, float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (task != null && !task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        static string FaultName(Exception exception)
        {
            if (exception == null)
                return "faulted";
            if (exception.InnerException != null)
                return exception.InnerException.GetType().Name;
            return exception.GetType().Name;
        }
    }
}
