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
    [Category(ClientLiveTestGuard.EconomyCategory)]
    public class ClientBootstrap02BLiveTests
    {
        const string ExpectedUid = "oVzAaTajRgPMQzwlFTFectt0Sypu";
        const string ExpectedCompanyId = "co_17ce1ad2a587a7088af4e54b18b6a347e754ed7e04f60cf2b72e1e9f186e3153";
        const string CompanyName = "LogistiCore Test Logistics";
        const string ConfigVersion = "lc_dev_economy_v1";
        const string RulesVersion = "synthetic_v1";
        const string KeyVariable = "LOGISTICORE_EMU_ECONOMY_IDEMPOTENCY_KEY";
        const string EvidenceDir = "/tmp/logisticore-unified-bootstrap-02b";
        const string EvidencePath = EvidenceDir + "/live-call-evidence.json";
        const float StepSeconds = 90f;

        [SetUp]
        public void GateLiveEmulator()
        {
            ClientLiveTestGuard.RequireEconomy();
        }

        [UnityTest]
        public IEnumerator Editor_ExistingCompany_InitializesEconomyOnce()
        {
            var key = Environment.GetEnvironmentVariable(KeyVariable);
            if (string.IsNullOrEmpty(key) || key.Length > 128)
                Assert.Fail("Pre-persisted idempotency key is required.");

            Directory.CreateDirectory(EvidenceDir);
            var evidence = new StringBuilder();
            evidence.Append("{\n");
            evidence.Append("  \"approvalAt\": \"2026-11-06 21:09:48 +03\",\n");
            evidence.Append("  \"measuredAt\": \"").Append(Escape(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))).Append("\",\n");
            evidence.Append("  \"projectId\": \"").Append(ClientProtocol.DemoProjectId).Append("\",\n");
            evidence.Append("  \"expectedUid\": \"").Append(ExpectedUid).Append("\",\n");
            evidence.Append("  \"env\": \"economy\",\n");
            evidence.Append("  \"mutate\": false,\n");
            evidence.Append("  \"ensurePlayerIdentity\": false,\n");
            evidence.Append("  \"createCompany\": false,\n");
            evidence.Append("  \"idempotencyKey\": \"").Append(Escape(key)).Append("\",\n");

            var settings = ClientEnvironmentSettings.Load();
            string emulatorFault;
            Assert.IsTrue(ClientEmulatorPasswordAuth.TryValidateEmulator(settings, out emulatorFault), emulatorFault);
            Assert.IsTrue(ClientEmulatorAuthPolicy.MayInitializeEconomy);
            Assert.IsFalse(ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity);
            Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreateCompany);
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
            evidence.Append("  \"uid\": \"").Append(ExpectedUid).Append("\",\n");

            var functions = FirebaseFunctions.GetInstance(app, settings.FunctionsRegion);
            yield return CallCompany(functions, evidence, true);
            yield return CallEconomyRead(functions, evidence, true);
            yield return CallInitialize(functions, key, evidence);
            yield return CallCompany(functions, evidence, false);
            yield return CallEconomyRead(functions, evidence, false);
            yield return CallOptional(functions, ClientCallableCatalog.GetMyFleetState, "getMyFleetState", evidence);
            yield return CallOptional(functions, ClientCallableCatalog.GetMyWarehouseState, "getMyWarehouseState", evidence);
            yield return CallOptional(functions, ClientCallableCatalog.GetMyContracts, "getMyContracts", evidence);
            evidence.Append("  \"getMyMarketState\": false,\n");
            evidence.Append("  \"initializeCompanyEconomyCount\": 1\n}\n");
            File.WriteAllText(EvidencePath, evidence.ToString());

            auth.SignOut();
            ClientConnectionAuthorization.ResetForTests();
            FirebaseClient.ResetForTests();
        }

        static IEnumerator CallCompany(FirebaseFunctions functions, StringBuilder evidence, bool preflight)
        {
            var task = functions.GetHttpsCallable(ClientCallableCatalog.GetMyCompanyState)
                .CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            if (!task.IsCompleted || task.IsFaulted)
                Assert.Fail("getMyCompanyState failed.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.IsTrue(ClientReadParser.TryCompany(envelope.Result, out var company));
            Assert.IsTrue(company.HasCompany);
            Assert.AreEqual(ExpectedCompanyId, company.CompanyId);
            Assert.AreEqual(CompanyName, company.Name);
            if (preflight)
            {
                evidence.Append("  \"preflightEconomyReady\": ").Append(company.EconomyReady ? "true" : "false").Append(",\n");
                if (company.EconomyReady)
                    Assert.Fail("Economy already ready; initializeCompanyEconomy was not invoked.");
                Assert.AreEqual("foundation_pending_economy", company.BootstrapStatus);
                yield break;
            }

            Assert.IsTrue(company.EconomyReady);
            Assert.AreEqual("economy_ready", company.BootstrapStatus);
            evidence.Append("  \"getMyCompanyState\": {\"economyReady\":true,\"bootstrapStatus\":\"economy_ready\"},\n");
        }

        static IEnumerator CallEconomyRead(FirebaseFunctions functions, StringBuilder evidence, bool preflight)
        {
            var task = functions.GetHttpsCallable(ClientCallableCatalog.GetMyEconomyState)
                .CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            if (!task.IsCompleted || task.IsFaulted)
                Assert.Fail("getMyEconomyState failed.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.IsTrue(ClientReadParser.TryEconomy(envelope.Result, out var economy));
            if (preflight)
            {
                Assert.IsTrue(economy.HasCompany);
                Assert.IsFalse(economy.EconomyReady);
                Assert.IsFalse(economy.HasBalance);
                yield break;
            }

            Assert.IsTrue(economy.EconomyReady);
            Assert.IsTrue(economy.HasBalance);
            Assert.AreEqual(ExpectedCompanyId, economy.CompanyId);
            Assert.AreEqual("1500000", economy.Balance);
            Assert.AreEqual("LGC", economy.Currency);
            Assert.AreEqual(2, economy.Scale);
            Assert.AreEqual(0, economy.Xp);
            Assert.AreEqual(1, economy.Level);
            Assert.AreEqual(ConfigVersion, economy.ConfigVersion);
            Assert.AreEqual(RulesVersion, economy.RulesVersion);
            Assert.IsTrue(ClientMoney.TryFormat(economy.Balance, economy.Currency, economy.Scale, out var display));
            Assert.AreEqual("15000.00 LGC", display);
            evidence.Append("  \"getMyEconomyState\": {\"balance\":\"1500000\",\"currency\":\"LGC\",\"scale\":2,\"xp\":0,\"level\":1,\"configVersion\":\"")
                .Append(ConfigVersion).Append("\"},\n");
        }

        static IEnumerator CallInitialize(FirebaseFunctions functions, string key, StringBuilder evidence)
        {
            var payload = new Dictionary<string, object>
            {
                { "protocolVersion", 1 },
                { "idempotencyKey", key },
                { "payload", new Dictionary<string, object>() }
            };
            var task = functions.GetHttpsCallable(ClientCallableCatalog.InitializeCompanyEconomy).CallAsync(payload);
            yield return WaitTask(task, StepSeconds);
            if (!task.IsCompleted || task.IsFaulted)
                Assert.Fail("initializeCompanyEconomy failed.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.IsTrue(ClientValue.TryMap(envelope.Result, out var created));
            Assert.IsTrue(ClientValue.TryString(ClientValue.Get(created, "companyId"), out var companyId));
            ClientValue.TryString(ClientValue.Get(created, "balance"), out var balance);
            ClientValue.TryBool(ClientValue.Get(created, "economyReady"), out var ready);
            ClientValue.TryInt(ClientValue.Get(created, "xp"), out var xp);
            ClientValue.TryInt(ClientValue.Get(created, "level"), out var level);
            Assert.AreEqual(ExpectedCompanyId, companyId);
            Assert.IsTrue(ready);
            Assert.AreEqual("1500000", balance);
            Assert.AreEqual(0, xp);
            Assert.AreEqual(1, level);
            evidence.Append("  \"initializeCompanyEconomy\": {\"companyId\":\"")
                .Append(companyId)
                .Append("\",\"balance\":\"1500000\",\"economyReady\":true,\"serverTime\":\"")
                .Append(Escape(envelope.ServerTime))
                .Append("\"},\n");
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
                evidence.Append("  \"").Append(key).Append("\": {\"starterFleetReady\":")
                    .Append(fleet.StarterFleetReady ? "true" : "false")
                    .Append(",\"itemCount\":").Append(fleet.Items.Length).Append("},\n");
                yield break;
            }

            if (name == ClientCallableCatalog.GetMyWarehouseState && ClientReadParser.TryWarehouse(envelope.Result, out var warehouses))
            {
                evidence.Append("  \"").Append(key).Append("\": {\"itemCount\":")
                    .Append(warehouses.Items.Length).Append("},\n");
                yield break;
            }

            if (name == ClientCallableCatalog.GetMyContracts && ClientReadParser.TryContracts(envelope.Result, out var contracts))
            {
                evidence.Append("  \"").Append(key).Append("\": {\"itemCount\":")
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
