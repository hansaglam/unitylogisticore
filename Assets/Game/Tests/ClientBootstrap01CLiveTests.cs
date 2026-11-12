using System;
using System.Collections;
using System.Globalization;
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
    [Category(ClientLiveTestGuard.ReadCategory)]
    public class ClientBootstrap01CLiveTests
    {
        const string ExpectedUid = "oVzAaTajRgPMQzwlFTFectt0Sypu";
        const string EvidenceDir = "/tmp/logisticore-unified-bootstrap-01c";
        const string EvidencePath = EvidenceDir + "/live-call-evidence.json";
        const float StepSeconds = 90f;

        [SetUp]
        public void GateLiveEmulator()
        {
            ClientLiveTestGuard.RequireReadOnly();
        }

        [UnityTest]
        public IEnumerator Editor_ExistingEmail_LocalHealthAndCompanyRead()
        {
            Directory.CreateDirectory(EvidenceDir);
            var evidence = new StringBuilder();
            evidence.Append("{\n");
            evidence.Append("  \"approvalAt\": \"2026-11-06 19:56:42 +03\",\n");
            evidence.Append("  \"measuredAt\": \"").Append(Escape(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))).Append("\",\n");
            evidence.Append("  \"projectId\": \"").Append(ClientProtocol.DemoProjectId).Append("\",\n");
            evidence.Append("  \"email\": \"").Append(ClientEmulatorAuthCredentials.Email).Append("\",\n");
            evidence.Append("  \"expectedUid\": \"").Append(ExpectedUid).Append("\",\n");
            evidence.Append("  \"env\": \"read\",\n");
            evidence.Append("  \"mutate\": false,\n");
            evidence.Append("  \"anonymous\": false,\n");
            evidence.Append("  \"ensurePlayerIdentity\": false,\n");
            evidence.Append("  \"createCompany\": false,\n");
            evidence.Append("  \"getMyEconomyState\": false,\n");

            var settings = ClientEnvironmentSettings.Load();
            string emulatorFault;
            Assert.IsTrue(ClientEmulatorPasswordAuth.TryValidateEmulator(settings, out emulatorFault), emulatorFault);
            Assert.IsFalse(ClientEmulatorAuthPolicy.MaySignInAnonymously);
            Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreatePasswordUser);
            Assert.IsTrue(ClientEmulatorAuthPolicy.MaySignInExistingPassword);

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
            var status = FirebaseClient.Current;
            Assert.AreEqual(ClientBackendPhase.Ready, status.Phase);

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
            var uid = signIn.Result;
            Assert.AreEqual(ExpectedUid, uid);
            Assert.IsNotNull(auth.CurrentUser);
            Assert.AreEqual(ExpectedUid, auth.CurrentUser.UserId);
            Assert.IsFalse(auth.CurrentUser.IsAnonymous);
            evidence.Append("  \"uid\": \"").Append(uid).Append("\",\n");

            var functions = FirebaseFunctions.GetInstance(app, settings.FunctionsRegion);
            yield return CallRead(functions, ClientCallableCatalog.LocalHealth, "localHealth", evidence);
            yield return CallRead(functions, ClientCallableCatalog.GetMyCompanyState, "getMyCompanyState", evidence);
            evidence.Append("  \"commandsAttempted\": 0\n}\n");
            File.WriteAllText(EvidencePath, evidence.ToString());

            auth.SignOut();
            ClientConnectionAuthorization.ResetForTests();
            FirebaseClient.ResetForTests();
        }

        static IEnumerator CallRead(FirebaseFunctions functions, string name, string key, StringBuilder evidence)
        {
            Assert.IsTrue(ClientCallableCatalog.IsRead(name), name);
            var task = functions.GetHttpsCallable(name).CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            evidence.Append("  \"").Append(key).Append("\": {");
            if (task == null || !task.IsCompleted)
            {
                evidence.Append("\"outcome\":\"timeout\"},\n");
                Assert.Fail(name + " timed out.");
                yield break;
            }

            if (task.IsFaulted)
            {
                var functionsError = FindFunctionsException(task.Exception);
                var code = functionsError != null ? Kebab(functionsError.ErrorCode.ToString()) : "fault";
                var details = DetailsCode(functionsError);
                evidence.Append("\"outcome\":\"").Append(Escape(code)).Append("\"");
                if (!string.IsNullOrEmpty(details))
                    evidence.Append(",\"detailsCode\":\"").Append(Escape(details)).Append("\"");
                evidence.Append("},\n");
                if (name == ClientCallableCatalog.LocalHealth)
                    Assert.Fail("localHealth failed: " + code);
                if (name == ClientCallableCatalog.GetMyCompanyState &&
                    code != "failed-precondition")
                    Assert.Fail("getMyCompanyState unexpected: " + code);
                yield break;
            }

            var raw = task.Result != null ? task.Result.Data : null;
            if (!ClientEnvelope.TryParse(raw, out var envelope, out var fault))
            {
                evidence.Append("\"outcome\":\"malformed\"},\n");
                Assert.Fail(name + " malformed: " + fault);
                yield break;
            }

            evidence.Append("\"outcome\":\"ok\",\"protocolVersion\":")
                .Append(envelope.ProtocolVersion)
                .Append(",\"serverTime\":\"")
                .Append(Escape(envelope.ServerTime))
                .Append("\"");
            if (name == ClientCallableCatalog.LocalHealth)
            {
                string status = "";
                if (ClientValue.TryMap(envelope.Result, out var map))
                    ClientValue.TryString(ClientValue.Get(map, "status"), out status);
                evidence.Append(",\"resultStatus\":\"").Append(Escape(status)).Append("\"},\n");
                Assert.AreEqual(1, envelope.ProtocolVersion);
                Assert.IsFalse(string.IsNullOrEmpty(envelope.ServerTime));
                Assert.AreEqual("ok", status);
                yield break;
            }

            if (!ClientReadParser.TryCompany(envelope.Result, out var company))
            {
                evidence.Append(",\"parse\":\"failed\"},\n");
                Assert.Fail("getMyCompanyState unparsed.");
                yield break;
            }

            evidence.Append(",\"hasCompany\":")
                .Append(company.HasCompany ? "true" : "false")
                .Append(",\"economyReady\":")
                .Append(company.EconomyReady ? "true" : "false")
                .Append("},\n");
        }

        static IEnumerator WaitTask(System.Threading.Tasks.Task task, float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (task != null && !task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        static FunctionsException FindFunctionsException(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is FunctionsException functions)
                    return functions;
                if (current is AggregateException aggregate)
                {
                    foreach (var inner in aggregate.InnerExceptions)
                    {
                        var found = FindFunctionsException(inner);
                        if (found != null)
                            return found;
                    }
                }
            }

            return null;
        }

        static string DetailsCode(FunctionsException error)
        {
            if (error == null)
                return "";
            var text = error.Message ?? "";
            const string marker = "FAILED_PRECONDITION";
            return text.IndexOf(marker, StringComparison.Ordinal) >= 0 ? marker : "";
        }

        static string Kebab(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            var text = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsUpper(c) && i > 0)
                    text.Append('-');
                text.Append(char.ToLowerInvariant(c));
            }

            return text.ToString();
        }

        static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
