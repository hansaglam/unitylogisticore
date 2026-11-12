using System;
using System.Collections;
using System.Collections.Generic;
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
    [Category(ClientLiveTestGuard.CompanyCategory)]
    public class ClientBootstrap01ELiveTests
    {
        const string ExpectedUid = "oVzAaTajRgPMQzwlFTFectt0Sypu";
        const string ExpectedCompanyId = "co_17ce1ad2a587a7088af4e54b18b6a347e754ed7e04f60cf2b72e1e9f186e3153";
        const string CompanyName = "LogistiCore Test Logistics";
        const string KeyVariable = "LOGISTICORE_EMU_COMPANY_IDEMPOTENCY_KEY";
        const string EvidenceDir = "/tmp/logisticore-unified-bootstrap-01e";
        const string EvidencePath = EvidenceDir + "/live-call-evidence.json";
        const float StepSeconds = 90f;

        [SetUp]
        public void GateLiveEmulator()
        {
            ClientLiveTestGuard.RequireCompany();
        }

        [UnityTest]
        public IEnumerator Editor_ExistingEmail_CreatesOneCompany()
        {
            var key = Environment.GetEnvironmentVariable(KeyVariable);
            if (string.IsNullOrEmpty(key) || key.Length > 128)
                Assert.Fail("Pre-persisted idempotency key is required.");

            Directory.CreateDirectory(EvidenceDir);
            var evidence = new StringBuilder();
            evidence.Append("{\n");
            evidence.Append("  \"approvalAt\": \"2026-11-06 20:31:44 +03\",\n");
            evidence.Append("  \"measuredAt\": \"").Append(Escape(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))).Append("\",\n");
            evidence.Append("  \"projectId\": \"").Append(ClientProtocol.DemoProjectId).Append("\",\n");
            evidence.Append("  \"email\": \"").Append(ClientEmulatorAuthCredentials.Email).Append("\",\n");
            evidence.Append("  \"expectedUid\": \"").Append(ExpectedUid).Append("\",\n");
            evidence.Append("  \"env\": \"company\",\n");
            evidence.Append("  \"mutate\": false,\n");
            evidence.Append("  \"anonymous\": false,\n");
            evidence.Append("  \"ensurePlayerIdentity\": false,\n");
            evidence.Append("  \"idempotencyKey\": \"").Append(Escape(key)).Append("\",\n");

            var settings = ClientEnvironmentSettings.Load();
            string emulatorFault;
            Assert.IsTrue(ClientEmulatorPasswordAuth.TryValidateEmulator(settings, out emulatorFault), emulatorFault);
            Assert.IsTrue(ClientEmulatorAuthPolicy.MayCreateCompany);
            Assert.IsFalse(ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity);
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
            if (!signIn.IsCompleted || signIn.IsFaulted)
                Assert.Fail("Existing email sign-in failed.");
            Assert.AreEqual(ExpectedUid, signIn.Result);
            Assert.AreEqual(ExpectedUid, auth.CurrentUser.UserId);
            Assert.IsFalse(auth.CurrentUser.IsAnonymous);
            evidence.Append("  \"uid\": \"").Append(ExpectedUid).Append("\",\n");

            var functions = FirebaseFunctions.GetInstance(app, settings.FunctionsRegion);
            yield return CallCompanyRead(functions, evidence, true);
            yield return CallCreate(functions, key, evidence);
            yield return CallCompanyRead(functions, evidence, false);
            yield return CallEconomy(functions, evidence);
            yield return CallOptional(functions, ClientCallableCatalog.GetMyFleetState, "getMyFleetState", evidence);
            yield return CallOptional(functions, ClientCallableCatalog.GetMyWarehouseState, "getMyWarehouseState", evidence);
            yield return CallOptional(functions, ClientCallableCatalog.GetMyContracts, "getMyContracts", evidence);
            evidence.Append("  \"getMyMarketState\": false,\n");
            evidence.Append("  \"createCompanyCount\": 1\n}\n");
            File.WriteAllText(EvidencePath, evidence.ToString());

            auth.SignOut();
            ClientConnectionAuthorization.ResetForTests();
            FirebaseClient.ResetForTests();
        }

        static IEnumerator CallCompanyRead(FirebaseFunctions functions, StringBuilder evidence, bool preflight)
        {
            var task = functions.GetHttpsCallable(ClientCallableCatalog.GetMyCompanyState)
                .CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            if (!task.IsCompleted || task.IsFaulted)
                Assert.Fail("getMyCompanyState failed.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.IsTrue(ClientReadParser.TryCompany(envelope.Result, out var company));
            if (preflight)
            {
                evidence.Append("  \"preflightHasCompany\": ").Append(company.HasCompany ? "true" : "false").Append(",\n");
                if (company.HasCompany)
                    Assert.Fail("Company already exists; createCompany was not invoked.");
                yield break;
            }

            Assert.IsTrue(company.HasCompany);
            Assert.AreEqual(ExpectedCompanyId, company.CompanyId);
            Assert.AreEqual(CompanyName, company.Name);
            Assert.AreEqual("foundation_pending_economy", company.BootstrapStatus);
            Assert.IsFalse(company.EconomyReady);
            evidence.Append("  \"getMyCompanyState\": {\"hasCompany\":true,\"companyId\":\"")
                .Append(company.CompanyId)
                .Append("\",\"name\":\"")
                .Append(Escape(company.Name))
                .Append("\",\"bootstrapStatus\":\"")
                .Append(Escape(company.BootstrapStatus))
                .Append("\",\"economyReady\":false},\n");
        }

        static IEnumerator CallCreate(FirebaseFunctions functions, string key, StringBuilder evidence)
        {
            var payload = new Dictionary<string, object>
            {
                { "protocolVersion", 1 },
                { "idempotencyKey", key },
                { "payload", new Dictionary<string, object> { { "name", CompanyName } } }
            };
            var task = functions.GetHttpsCallable(ClientCallableCatalog.CreateCompany).CallAsync(payload);
            yield return WaitTask(task, StepSeconds);
            if (!task.IsCompleted || task.IsFaulted)
                Assert.Fail("createCompany failed.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.IsTrue(ClientValue.TryMap(envelope.Result, out var created));
            Assert.IsTrue(ClientValue.TryString(ClientValue.Get(created, "companyId"), out var companyId));
            Assert.IsTrue(ClientValue.TryString(ClientValue.Get(created, "name"), out var name));
            ClientValue.TryString(ClientValue.Get(created, "bootstrapStatus"), out var bootstrap);
            ClientValue.TryBool(ClientValue.Get(created, "economyReady"), out var ready);
            Assert.AreEqual(ExpectedCompanyId, companyId);
            Assert.AreEqual(CompanyName, name);
            Assert.AreEqual("foundation_pending_economy", bootstrap);
            Assert.IsFalse(ready);
            evidence.Append("  \"createCompany\": {\"companyId\":\"")
                .Append(companyId)
                .Append("\",\"name\":\"")
                .Append(Escape(name))
                .Append("\",\"bootstrapStatus\":\"")
                .Append(Escape(bootstrap))
                .Append("\",\"economyReady\":false,\"serverTime\":\"")
                .Append(Escape(envelope.ServerTime))
                .Append("\"},\n");
        }

        static IEnumerator CallEconomy(FirebaseFunctions functions, StringBuilder evidence)
        {
            var task = functions.GetHttpsCallable(ClientCallableCatalog.GetMyEconomyState)
                .CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            if (!task.IsCompleted || task.IsFaulted)
                Assert.Fail("getMyEconomyState failed.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.IsTrue(ClientReadParser.TryEconomy(envelope.Result, out var economy));
            Assert.IsTrue(economy.HasCompany);
            Assert.IsFalse(economy.EconomyReady);
            Assert.IsFalse(economy.HasBalance);
            evidence.Append("  \"getMyEconomyState\": {\"hasCompany\":true,\"economyReady\":false},\n");
        }

        static IEnumerator CallOptional(FirebaseFunctions functions, string name, string key, StringBuilder evidence)
        {
            var task = functions.GetHttpsCallable(name).CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            if (!task.IsCompleted || task.IsFaulted)
            {
                evidence.Append("  \"").Append(key).Append("\": {\"outcome\":\"fault\"},\n");
                yield break;
            }

            var raw = task.Result != null ? task.Result.Data : null;
            if (!ClientEnvelope.TryParse(raw, out var envelope, out _))
            {
                evidence.Append("  \"").Append(key).Append("\": {\"outcome\":\"malformed\"},\n");
                yield break;
            }

            if (name == ClientCallableCatalog.GetMyFleetState && ClientReadParser.TryFleet(envelope.Result, out var fleet))
            {
                evidence.Append("  \"").Append(key).Append("\": {\"outcome\":\"ok\",\"starterFleetReady\":")
                    .Append(fleet.StarterFleetReady ? "true" : "false")
                    .Append(",\"itemCount\":").Append(fleet.Items.Length).Append("},\n");
                yield break;
            }

            if (name == ClientCallableCatalog.GetMyWarehouseState && ClientReadParser.TryWarehouse(envelope.Result, out var warehouses))
            {
                evidence.Append("  \"").Append(key).Append("\": {\"outcome\":\"ok\",\"itemCount\":")
                    .Append(warehouses.Items.Length).Append("},\n");
                yield break;
            }

            if (name == ClientCallableCatalog.GetMyContracts && ClientReadParser.TryContracts(envelope.Result, out var contracts))
            {
                evidence.Append("  \"").Append(key).Append("\": {\"outcome\":\"ok\",\"itemCount\":")
                    .Append(contracts.Items.Length).Append("},\n");
                yield break;
            }

            evidence.Append("  \"").Append(key).Append("\": {\"outcome\":\"ok\"},\n");
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
