using System;
using System.Collections;
using System.IO;
using System.Text;
using Firebase;
using Firebase.Auth;
using Firebase.Functions;
using LogistiCore.Backend;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LogistiCore.Tests
{
    [Explicit]
    [Category(ClientLiveTestGuard.IdentityCategory)]
    public class ClientBootstrap01DLiveTests
    {
        const string ExpectedUid = "oVzAaTajRgPMQzwlFTFectt0Sypu";
        const string EvidenceDir = "/tmp/logisticore-unified-bootstrap-01d";
        const string EvidencePath = EvidenceDir + "/live-call-evidence.json";
        const float StepSeconds = 90f;

        [SetUp]
        public void GateLiveEmulator()
        {
            ClientLiveTestGuard.RequireIdentity();
        }

        [UnityTest]
        public IEnumerator Editor_ExistingEmail_EnsuresOnePlayerIdentity()
        {
            Directory.CreateDirectory(EvidenceDir);
            var evidence = new StringBuilder();
            evidence.Append("{\n");
            evidence.Append("  \"approvalAt\": \"2026-11-06 20:12:15 +03\",\n");
            evidence.Append("  \"measuredAt\": \"").Append(Escape(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))).Append("\",\n");
            evidence.Append("  \"projectId\": \"").Append(ClientProtocol.DemoProjectId).Append("\",\n");
            evidence.Append("  \"email\": \"").Append(ClientEmulatorAuthCredentials.Email).Append("\",\n");
            evidence.Append("  \"expectedUid\": \"").Append(ExpectedUid).Append("\",\n");
            evidence.Append("  \"env\": \"identity\",\n");
            evidence.Append("  \"mutate\": false,\n");
            evidence.Append("  \"anonymous\": false,\n");
            evidence.Append("  \"createCompany\": false,\n");

            var settings = ClientEnvironmentSettings.Load();
            string emulatorFault;
            Assert.IsTrue(ClientEmulatorPasswordAuth.TryValidateEmulator(settings, out emulatorFault), emulatorFault);
            Assert.IsTrue(ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity);
            Assert.IsFalse(ClientEmulatorAuthPolicy.MaySignInAnonymously);
            Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreatePasswordUser);

            string password;
            string secretFault;
            Assert.IsTrue(ClientEmulatorPasswordAuth.TryLoadPassword(out password, out secretFault), secretFault);

            LogAssert.ignoreFailingMessages = true;
            ClientConnectionAuthorization.ResetForTests();
            ClientConnectionAuthorization.ApproveForTests();
            FirebaseClient.ResetForTests();
            FirebaseClient.Start(settings);
            var readyDeadline = Time.realtimeSinceStartup + StepSeconds;
            while (FirebaseClient.Current.Phase == ClientBackendPhase.Starting &&
                   Time.realtimeSinceStartup < readyDeadline)
                yield return null;
            Assert.AreEqual(ClientBackendPhase.Ready, FirebaseClient.Current.Phase);

            var app = FirebaseApp.DefaultInstance;
            Assert.IsNotNull(app);
            Assert.AreEqual(ClientProtocol.DemoProjectId, app.Options.ProjectId);
            var auth = FirebaseAuth.GetAuth(app);
            auth.SignOut();
            yield return null;

            var signIn = ClientEmulatorPasswordAuth.SignInExistingOnly(
                auth, ClientEmulatorAuthCredentials.Email, password);
            yield return WaitTask(signIn, StepSeconds);
            Assert.IsTrue(signIn.IsCompleted);
            if (signIn.IsFaulted)
                Assert.Fail("Existing email sign-in failed.");
            Assert.AreEqual(ExpectedUid, signIn.Result);
            Assert.IsNotNull(auth.CurrentUser);
            Assert.AreEqual(ExpectedUid, auth.CurrentUser.UserId);
            Assert.IsFalse(auth.CurrentUser.IsAnonymous);
            evidence.Append("  \"uid\": \"").Append(ExpectedUid).Append("\",\n");

            var functions = FirebaseFunctions.GetInstance(app, settings.FunctionsRegion);
            yield return CallIdentity(functions, evidence);
            yield return CallCompany(functions, evidence);
            yield return CallEconomy(functions, evidence);
            evidence.Append("  \"ensurePlayerIdentityCount\": 1,\n");
            evidence.Append("  \"commandsAttempted\": 1\n}\n");
            File.WriteAllText(EvidencePath, evidence.ToString());

            auth.SignOut();
            ClientConnectionAuthorization.ResetForTests();
            FirebaseClient.ResetForTests();
        }

        static IEnumerator CallIdentity(FirebaseFunctions functions, StringBuilder evidence)
        {
            Assert.IsFalse(ClientCallableCatalog.IsRead(ClientCallableCatalog.EnsurePlayerIdentity));
            var task = functions.GetHttpsCallable(ClientCallableCatalog.EnsurePlayerIdentity)
                .CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            Assert.IsTrue(task.IsCompleted);
            if (task.IsFaulted)
                Assert.Fail("ensurePlayerIdentity faulted.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.AreEqual(1, envelope.ProtocolVersion);
            Assert.IsTrue(ClientReadParser.TryIdentity(envelope.Result, out var identity));
            Assert.AreEqual(ExpectedUid, identity.PlayerId);
            Assert.AreEqual(1, identity.SchemaVersion);
            Assert.AreEqual("active", identity.IdentityStatus);
            evidence.Append("  \"ensurePlayerIdentity\": {\"outcome\":\"ok\",\"playerId\":\"")
                .Append(Escape(identity.PlayerId))
                .Append("\",\"schemaVersion\":1,\"identityStatus\":\"active\",\"serverTime\":\"")
                .Append(Escape(envelope.ServerTime))
                .Append("\"},\n");
        }

        static IEnumerator CallCompany(FirebaseFunctions functions, StringBuilder evidence)
        {
            var task = functions.GetHttpsCallable(ClientCallableCatalog.GetMyCompanyState)
                .CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            Assert.IsTrue(task.IsCompleted);
            if (task.IsFaulted)
                Assert.Fail("getMyCompanyState faulted after identity.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.IsTrue(ClientReadParser.TryCompany(envelope.Result, out var company));
            Assert.IsFalse(company.HasCompany);
            evidence.Append("  \"getMyCompanyState\": {\"outcome\":\"ok\",\"hasCompany\":false},\n");
        }

        static IEnumerator CallEconomy(FirebaseFunctions functions, StringBuilder evidence)
        {
            var task = functions.GetHttpsCallable(ClientCallableCatalog.GetMyEconomyState)
                .CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            Assert.IsTrue(task.IsCompleted);
            if (task.IsFaulted)
                Assert.Fail("getMyEconomyState faulted after identity.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.IsTrue(ClientReadParser.TryEconomy(envelope.Result, out var economy));
            Assert.IsFalse(economy.HasCompany);
            Assert.IsFalse(economy.EconomyReady);
            Assert.IsFalse(economy.HasBalance);
            evidence.Append("  \"getMyEconomyState\": {\"outcome\":\"ok\",\"hasCompany\":false,\"economyReady\":false},\n");
        }

        static IEnumerator WaitTask(System.Threading.Tasks.Task task, float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (task != null && !task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
