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
    [Category(ClientLiveTestGuard.StarterFleetCategory)]
    public class ClientBootstrap03BLiveTests
    {
        const string ExpectedUid = "oVzAaTajRgPMQzwlFTFectt0Sypu";
        const string ExpectedCompanyId = "co_17ce1ad2a587a7088af4e54b18b6a347e754ed7e04f60cf2b72e1e9f186e3153";
        const string CompanyName = "LogistiCore Test Logistics";
        const string CatalogVersion = "lc_dev_fleet_v1";
        const string PackageVersion = "lc_starter_pkg_v1";
        const string KeyVariable = "LOGISTICORE_EMU_FLEET_IDEMPOTENCY_KEY";
        const string EvidenceDir = "/tmp/logisticore-unified-bootstrap-03b";
        const string EvidencePath = EvidenceDir + "/live-call-evidence.json";
        const float StepSeconds = 90f;

        [SetUp]
        public void GateLiveEmulator()
        {
            ClientLiveTestGuard.RequireFleet();
        }

        [UnityTest]
        public IEnumerator Editor_ExistingCompany_ProvisionsStarterFleetOnce()
        {
            var key = Environment.GetEnvironmentVariable(KeyVariable);
            if (string.IsNullOrEmpty(key) || key.Length > 128)
                Assert.Fail("Pre-persisted idempotency key is required.");

            Directory.CreateDirectory(EvidenceDir);
            var evidence = new StringBuilder();
            evidence.Append("{\n");
            evidence.Append("  \"approvalAt\": \"2026-11-06 22:33:11 +03\",\n");
            evidence.Append("  \"measuredAt\": \"").Append(Escape(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))).Append("\",\n");
            evidence.Append("  \"projectId\": \"").Append(ClientProtocol.DemoProjectId).Append("\",\n");
            evidence.Append("  \"expectedUid\": \"").Append(ExpectedUid).Append("\",\n");
            evidence.Append("  \"env\": \"fleet\",\n");
            evidence.Append("  \"mutate\": false,\n");
            evidence.Append("  \"idempotencyKey\": \"").Append(Escape(key)).Append("\",\n");

            var settings = ClientEnvironmentSettings.Load();
            string emulatorFault;
            Assert.IsTrue(ClientEmulatorPasswordAuth.TryValidateEmulator(settings, out emulatorFault), emulatorFault);
            Assert.IsTrue(ClientEmulatorAuthPolicy.MayProvisionStarterFleet);
            Assert.IsFalse(ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity);
            Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreateCompany);
            Assert.IsFalse(ClientEmulatorAuthPolicy.MayInitializeEconomy);
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
            evidence.Append("  \"uid\": \"").Append(ExpectedUid).Append("\",\n");

            var functions = FirebaseFunctions.GetInstance(app, settings.FunctionsRegion);
            yield return CallCompany(functions, evidence);
            yield return CallEconomy(functions, evidence, true);
            yield return CallFleet(functions, evidence, true);
            yield return CallProvision(functions, key, evidence);
            yield return CallEconomy(functions, evidence, false);
            yield return CallFleet(functions, evidence, false);
            yield return CallOptional(functions, ClientCallableCatalog.GetMyWarehouseState, "getMyWarehouseState", evidence);
            yield return CallOptional(functions, ClientCallableCatalog.GetMyContracts, "getMyContracts", evidence);
            evidence.Append("  \"getMyMarketState\": false,\n");
            evidence.Append("  \"provisionCount\": 1\n}\n");
            File.WriteAllText(EvidencePath, evidence.ToString());

            auth.SignOut();
            ClientConnectionAuthorization.ResetForTests();
            FirebaseClient.ResetForTests();
        }

        static IEnumerator CallCompany(FirebaseFunctions functions, StringBuilder evidence)
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
            Assert.IsTrue(company.EconomyReady);
            evidence.Append("  \"getMyCompanyState\": {\"economyReady\":true},\n");
        }

        static IEnumerator CallEconomy(FirebaseFunctions functions, StringBuilder evidence, bool preflight)
        {
            var task = functions.GetHttpsCallable(ClientCallableCatalog.GetMyEconomyState)
                .CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            if (!task.IsCompleted || task.IsFaulted)
                Assert.Fail("getMyEconomyState failed.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.IsTrue(ClientReadParser.TryEconomy(envelope.Result, out var economy));
            Assert.IsTrue(economy.EconomyReady);
            Assert.AreEqual("1500000", economy.Balance);
            Assert.AreEqual("LGC", economy.Currency);
            Assert.AreEqual(0, economy.Xp);
            Assert.AreEqual(1, economy.Level);
            if (preflight)
                evidence.Append("  \"preflightEconomy\": {\"balance\":\"1500000\",\"xp\":0,\"level\":1},\n");
            else
                evidence.Append("  \"getMyEconomyState\": {\"balance\":\"1500000\",\"xp\":0,\"level\":1},\n");
        }

        static IEnumerator CallFleet(FirebaseFunctions functions, StringBuilder evidence, bool preflight)
        {
            var task = functions.GetHttpsCallable(ClientCallableCatalog.GetMyFleetState)
                .CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            if (!task.IsCompleted || task.IsFaulted)
                Assert.Fail("getMyFleetState failed.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.IsTrue(ClientReadParser.TryFleet(envelope.Result, out var fleet));
            if (preflight)
            {
                Assert.IsFalse(fleet.StarterFleetReady);
                Assert.AreEqual(0, fleet.Items.Length);
                evidence.Append("  \"preflightStarterFleetReady\": false,\n");
                yield break;
            }

            Assert.IsTrue(fleet.StarterFleetReady);
            Assert.AreEqual(2, fleet.Items.Length);
            ClientFleetAsset truck = default;
            ClientFleetAsset driver = default;
            for (var i = 0; i < fleet.Items.Length; i++)
            {
                if (fleet.Items[i].AssetId == "starter_truck")
                    truck = fleet.Items[i];
                if (fleet.Items[i].AssetId == "starter_driver")
                    driver = fleet.Items[i];
                Assert.AreNotEqual("trailer", fleet.Items[i].Kind);
            }

            Assert.AreEqual("truck", truck.Kind);
            Assert.AreEqual("lc_starter_truck", truck.CatalogId);
            Assert.AreEqual(CatalogVersion, truck.CatalogVersion);
            Assert.AreEqual("driver", driver.Kind);
            Assert.AreEqual("lc_starter_driver", driver.CatalogId);
            Assert.AreEqual(CatalogVersion, driver.CatalogVersion);
            evidence.Append("  \"getMyFleetState\": {\"starterFleetReady\":true,\"itemCount\":2},\n");
        }

        static IEnumerator CallProvision(FirebaseFunctions functions, string key, StringBuilder evidence)
        {
            var payload = new Dictionary<string, object>
            {
                { "protocolVersion", 1 },
                { "idempotencyKey", key },
                { "payload", new Dictionary<string, object>() }
            };
            var task = functions.GetHttpsCallable(ClientCallableCatalog.ProvisionCompanyStarterFleet).CallAsync(payload);
            yield return WaitTask(task, StepSeconds);
            if (!task.IsCompleted || task.IsFaulted)
                Assert.Fail("provisionCompanyStarterFleet failed.");
            var raw = task.Result != null ? task.Result.Data : null;
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault), fault.ToString());
            Assert.IsTrue(ClientValue.TryMap(envelope.Result, out var created));
            ClientValue.TryBool(ClientValue.Get(created, "starterFleetReady"), out var ready);
            ClientValue.TryString(ClientValue.Get(created, "truckId"), out var truckId);
            ClientValue.TryString(ClientValue.Get(created, "driverId"), out var driverId);
            ClientValue.TryString(ClientValue.Get(created, "packageVersion"), out var packageVersion);
            Assert.IsTrue(ready);
            Assert.AreEqual("starter_truck", truckId);
            Assert.AreEqual("starter_driver", driverId);
            Assert.AreEqual(PackageVersion, packageVersion);
            evidence.Append("  \"provisionCompanyStarterFleet\": {\"truckId\":\"starter_truck\",\"driverId\":\"starter_driver\",\"packageVersion\":\"")
                .Append(PackageVersion)
                .Append("\",\"serverTime\":\"")
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
