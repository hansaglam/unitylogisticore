using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
    [Category(ClientLiveTestGuard.MutationCategory)]
    public class ClientApi02B2LiveTests
    {
        const string EvidenceDir = "/tmp/logisticore-client-api-02b2";
        const string EvidencePath = EvidenceDir + "/live-call-evidence.json";
        const string RetainedPath = EvidenceDir + "/retained-session.json";
        const string CompanyName = "LogistiCore Test Logistics";
        const float StepSeconds = 90f;

        [SetUp]
        public void GateLiveEmulator()
        {
            ClientLiveTestGuard.RequireMutation();
        }

        [UnityTest]
        public IEnumerator Editor_OptionB_CreatesOneIdentityAndCompany()
        {
            Directory.CreateDirectory(EvidenceDir);
            LogAssert.ignoreFailingMessages = true;
            try
            {
            if (File.Exists(RetainedPath) && File.Exists(EvidencePath))
            {
                var existing = File.ReadAllText(EvidencePath);
                Assert.IsTrue(existing.Contains("\"createCompany\""), "Prior 02B-2 evidence must remain.");
                yield break;
            }

            var evidence = new Evidence();
            evidence.MeasuredAt = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
            evidence.ApprovalAt = "2026-11-06 03:22:46 +0300";
            evidence.ProjectId = ClientProtocol.DemoProjectId;
            evidence.CompanyName = CompanyName;

            ClientConnectionAuthorization.ResetForTests();
            ClientConnectionAuthorization.ApproveForTests();

            var settings = ClientEnvironmentSettings.Load();
            Assert.AreEqual(ClientBackendEnvironment.LocalEmulator, settings.Environment);
            evidence.FunctionsOrigin = settings.FunctionsEmulatorOrigin;
            evidence.AuthHost = settings.EmulatorHost + ":" +
                settings.EmulatorAuthPort.ToString(CultureInfo.InvariantCulture);

            FirebaseClient.ResetForTests();
            var status = FirebaseClient.Start(settings);
            var readyDeadline = Time.realtimeSinceStartup + StepSeconds;
            while (FirebaseClient.Current.Phase == ClientBackendPhase.Starting &&
                   Time.realtimeSinceStartup < readyDeadline)
                yield return null;
            status = FirebaseClient.Current;
            var app = TryDefaultApp();
            if (status.Phase != ClientBackendPhase.Ready || app == null)
            {
                WriteEvidence(evidence);
                Assert.Fail("Firebase SDK was not Ready.");
            }

            Assert.AreEqual(ClientProtocol.DemoProjectId, app.Options.ProjectId);
            var auth = FirebaseAuth.GetAuth(app);
            Assert.IsTrue(ClientEmulatorAuthPolicy.MaySignInAnonymously, ClientEmulatorAuthPolicy.AnonymousHold);
            auth.SignOut();
            yield return null;
            var signIn = auth.SignInAnonymouslyAsync();
            yield return WaitTask(signIn, StepSeconds);
            if (!signIn.IsCompleted || signIn.IsFaulted || signIn.Result == null || signIn.Result.User == null)
            {
                WriteEvidence(evidence);
                Assert.Fail("Anonymous emulator Auth failed.");
            }

            yield return null;
            var user = auth.CurrentUser;
            if (user == null || user.UserId != signIn.Result.User.UserId)
            {
                WriteEvidence(evidence);
                Assert.Fail("Auth CurrentUser did not match the SignIn result.");
            }

            evidence.AuthUid = user.UserId;
            evidence.AuthAnonymous = user.IsAnonymous;
            Assert.IsTrue(user.IsAnonymous);

            var functions = FirebaseFunctions.GetInstance(app, settings.FunctionsRegion);

            evidence.FirstMutationAt = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
            yield return Call(functions, ClientCallableCatalog.EnsurePlayerIdentity, ClientValue.ReadRequest(), evidence.Identity);
            if (evidence.Identity.Outcome != "ok")
            {
                WriteEvidence(evidence);
                Assert.Fail("ensurePlayerIdentity failed: " + evidence.Identity.Outcome);
            }

            Assert.AreEqual(user.UserId, evidence.Identity.PlayerId);
            Assert.AreEqual(1, evidence.Identity.SchemaVersion);
            Assert.AreEqual("active", evidence.Identity.IdentityStatus);

            evidence.IdempotencyKey = "lc02b2-" + Guid.NewGuid().ToString("N");
            var createPayload = new Dictionary<string, object>
            {
                { "protocolVersion", 1 },
                { "idempotencyKey", evidence.IdempotencyKey },
                { "payload", new Dictionary<string, object> { { "name", CompanyName } } }
            };
            yield return Call(functions, ClientCallableCatalog.CreateCompany, createPayload, evidence.CompanyCreate);
            if (evidence.CompanyCreate.Outcome != "ok")
            {
                WriteEvidence(evidence);
                Assert.Fail("createCompany failed: " + evidence.CompanyCreate.Outcome + " " + evidence.CompanyCreate.Detail);
            }

            Assert.AreEqual(CompanyName, evidence.CompanyCreate.CompanyName);
            Assert.AreEqual(1, evidence.CompanyCreate.SchemaVersion);
            Assert.AreEqual("foundation_pending_economy", evidence.CompanyCreate.BootstrapStatus);
            Assert.IsFalse(evidence.CompanyCreate.EconomyReady);
            Assert.IsTrue(evidence.CompanyCreate.CompanyId.StartsWith("co_"));
            Assert.AreEqual(67, evidence.CompanyCreate.CompanyId.Length);

            yield return Call(functions, ClientCallableCatalog.GetMyCompanyState, ClientValue.ReadRequest(), evidence.CompanyRead);
            Assert.AreEqual("ok", evidence.CompanyRead.Outcome);
            Assert.IsTrue(evidence.CompanyRead.HasCompany);
            Assert.AreEqual(CompanyName, evidence.CompanyRead.CompanyName);
            Assert.IsFalse(evidence.CompanyRead.EconomyReady);
            Assert.AreEqual(evidence.CompanyCreate.CompanyId, evidence.CompanyRead.CompanyId);

            yield return Call(functions, ClientCallableCatalog.GetMyEconomyState, ClientValue.ReadRequest(), evidence.Economy);
            Assert.AreEqual("ok", evidence.Economy.Outcome);
            Assert.IsTrue(evidence.Economy.HasCompany);
            Assert.IsFalse(evidence.Economy.EconomyReady);
            Assert.IsFalse(evidence.Economy.HasBalanceField);

            yield return Call(functions, ClientCallableCatalog.GetMyFleetState, ClientValue.ReadRequest(), evidence.Fleet);
            yield return Call(functions, ClientCallableCatalog.GetMyWarehouseState, ClientValue.ReadRequest(), evidence.Warehouse);
            yield return Call(functions, ClientCallableCatalog.GetMyContracts, ClientValue.ReadRequest(), evidence.Contracts);

            evidence.MarketCalled = false;
            evidence.UserDeleted = false;
            WriteEvidence(evidence);
            File.WriteAllText(RetainedPath,
                "{\n" +
                "  \"projectId\": \"demo-logisticore-local\",\n" +
                "  \"uid\": \"" + Escape(user.UserId) + "\",\n" +
                "  \"companyId\": \"" + Escape(evidence.CompanyCreate.CompanyId) + "\",\n" +
                "  \"companyName\": \"" + Escape(CompanyName) + "\",\n" +
                "  \"idempotencyKey\": \"" + Escape(evidence.IdempotencyKey) + "\",\n" +
                "  \"userDeleted\": false,\n" +
                "  \"note\": \"No token stored. Emulator restart discards Auth/Firestore memory.\"\n" +
                "}\n");

            ClientConnectionAuthorization.ResetForTests();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        static FirebaseApp TryDefaultApp()
        {
            try
            {
                var app = FirebaseApp.DefaultInstance;
                if (app != null && app.Options != null &&
                    app.Options.ProjectId == ClientProtocol.DemoProjectId)
                    return app;
            }
            catch (Exception)
            {
            }

            return null;
        }

        static IEnumerator Call(
            FirebaseFunctions functions,
            string name,
            IDictionary<string, object> payload,
            CallableEvidence target)
        {
            target.Callable = name;
            var task = functions.GetHttpsCallable(name).CallAsync(payload);
            yield return WaitTask(task, StepSeconds);
            if (task == null || !task.IsCompleted)
            {
                target.Outcome = "timeout";
                yield break;
            }

            if (task.IsFaulted)
            {
                var functionsError = FindFunctionsException(task.Exception);
                target.Detail = functionsError != null ? functionsError.Message : FaultText(task);
                target.Outcome = functionsError != null
                    ? Kebab(functionsError.ErrorCode.ToString())
                    : "fault";
                yield break;
            }

            var raw = task.Result != null ? task.Result.Data : null;
            target.Raw = JsonDump(raw);
            if (!ClientEnvelope.TryParse(raw, out var envelope, out var fault))
            {
                target.Outcome = "malformed";
                target.Detail = fault.ToString();
                yield break;
            }

            target.ProtocolVersion = envelope.ProtocolVersion;
            target.ServerTime = envelope.ServerTime;
            if (name == ClientCallableCatalog.EnsurePlayerIdentity)
            {
                if (!ClientReadParser.TryIdentity(envelope.Result, out var identity))
                {
                    target.Outcome = "unparsed";
                    yield break;
                }

                target.PlayerId = identity.PlayerId;
                target.SchemaVersion = identity.SchemaVersion;
                target.IdentityStatus = identity.IdentityStatus;
                target.Outcome = "ok";
                yield break;
            }

            if (name == ClientCallableCatalog.CreateCompany || name == ClientCallableCatalog.GetMyCompanyState)
            {
                if (name == ClientCallableCatalog.CreateCompany)
                {
                    if (!ClientValue.TryMap(envelope.Result, out var created) ||
                        !ClientValue.TryString(ClientValue.Get(created, "companyId"), out var companyId) ||
                        !ClientValue.TryString(ClientValue.Get(created, "name"), out var nameValue))
                    {
                        target.Outcome = "unparsed";
                        yield break;
                    }

                    ClientValue.TryInt(ClientValue.Get(created, "schemaVersion"), out var schema);
                    ClientValue.TryString(ClientValue.Get(created, "bootstrapStatus"), out var bootstrap);
                    ClientValue.TryBool(ClientValue.Get(created, "economyReady"), out var ready);
                    target.CompanyId = companyId;
                    target.CompanyName = nameValue;
                    target.SchemaVersion = schema;
                    target.BootstrapStatus = bootstrap;
                    target.EconomyReady = ready;
                    target.HasCompany = true;
                    target.Outcome = "ok";
                    yield break;
                }

                if (!ClientReadParser.TryCompany(envelope.Result, out var company))
                {
                    target.Outcome = "unparsed";
                    yield break;
                }

                target.HasCompany = company.HasCompany;
                target.CompanyId = company.CompanyId;
                target.CompanyName = company.Name;
                target.BootstrapStatus = company.BootstrapStatus;
                target.EconomyReady = company.EconomyReady;
                target.Outcome = "ok";
                yield break;
            }

            if (name == ClientCallableCatalog.GetMyEconomyState)
            {
                if (!ClientReadParser.TryEconomy(envelope.Result, out var economy))
                {
                    target.Outcome = "unparsed";
                    yield break;
                }

                target.HasCompany = economy.HasCompany;
                target.EconomyReady = economy.EconomyReady;
                target.HasBalanceField = !string.IsNullOrEmpty(economy.Balance);
                target.Outcome = "ok";
                yield break;
            }

            if (name == ClientCallableCatalog.GetMyFleetState)
            {
                if (!ClientReadParser.TryFleet(envelope.Result, out var fleet))
                {
                    target.Outcome = "unparsed";
                    yield break;
                }

                target.HasCompany = fleet.HasCompany;
                target.EconomyReady = fleet.EconomyReady;
                target.StarterFleetReady = fleet.StarterFleetReady;
                target.ItemCount = fleet.Items != null ? fleet.Items.Length : -1;
                target.Outcome = "ok";
                yield break;
            }

            if (name == ClientCallableCatalog.GetMyWarehouseState)
            {
                if (!ClientReadParser.TryWarehouse(envelope.Result, out var warehouse))
                {
                    target.Outcome = "unparsed";
                    yield break;
                }

                target.HasCompany = warehouse.HasCompany;
                target.EconomyReady = warehouse.EconomyReady;
                target.ItemCount = warehouse.Items != null ? warehouse.Items.Length : -1;
                target.Outcome = "ok";
                yield break;
            }

            if (name == ClientCallableCatalog.GetMyContracts)
            {
                if (!ClientReadParser.TryContracts(envelope.Result, out var page))
                {
                    target.Outcome = "unparsed";
                    yield break;
                }

                target.ItemCount = page.Items != null ? page.Items.Length : -1;
                target.Outcome = "ok";
            }
        }

        static IEnumerator WaitTask(Task task, float seconds)
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

        static string FaultText(Task task)
        {
            if (task == null || task.Exception == null)
                return "empty";
            var inner = task.Exception.GetBaseException();
            return inner.GetType().Name + ": " + inner.Message;
        }

        static string Kebab(string value)
        {
            var text = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                if (char.IsUpper(value[i]) && i > 0)
                    text.Append('-');
                text.Append(char.ToLowerInvariant(value[i]));
            }

            return text.ToString();
        }

        static void WriteEvidence(Evidence evidence)
        {
            Directory.CreateDirectory(EvidenceDir);
            File.WriteAllText(EvidencePath, evidence.ToJson());
        }

        static string JsonDump(object value)
        {
            var text = new StringBuilder();
            WriteJson(text, value, 0);
            return text.ToString();
        }

        static void WriteJson(StringBuilder text, object value, int depth)
        {
            if (depth > 8 || value == null)
            {
                text.Append("null");
                return;
            }

            if (value is string s)
            {
                text.Append('"').Append(Escape(s)).Append('"');
                return;
            }

            if (value is bool flag)
            {
                text.Append(flag ? "true" : "false");
                return;
            }

            if (value is int || value is long || value is float || value is double)
            {
                text.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary map)
            {
                text.Append('{');
                var first = true;
                foreach (DictionaryEntry entry in map)
                {
                    var key = entry.Key as string ?? "";
                    if (IsSecret(key))
                        continue;
                    if (!first)
                        text.Append(',');
                    first = false;
                    text.Append('"').Append(Escape(key)).Append("\":");
                    WriteJson(text, entry.Value, depth + 1);
                }

                text.Append('}');
                return;
            }

            if (value is IList list)
            {
                text.Append('[');
                for (var i = 0; i < list.Count; i++)
                {
                    if (i > 0)
                        text.Append(',');
                    WriteJson(text, list[i], depth + 1);
                }

                text.Append(']');
                return;
            }

            text.Append('"').Append(Escape(Convert.ToString(value, CultureInfo.InvariantCulture))).Append('"');
        }

        static bool IsSecret(string key)
        {
            return key.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        sealed class Evidence
        {
            public string MeasuredAt;
            public string ApprovalAt;
            public string FirstMutationAt;
            public string ProjectId;
            public string FunctionsOrigin;
            public string AuthHost;
            public string AuthUid;
            public bool AuthAnonymous;
            public string CompanyName;
            public string IdempotencyKey;
            public bool MarketCalled;
            public bool UserDeleted;
            public readonly CallableEvidence Identity = new CallableEvidence();
            public readonly CallableEvidence CompanyCreate = new CallableEvidence();
            public readonly CallableEvidence CompanyRead = new CallableEvidence();
            public readonly CallableEvidence Economy = new CallableEvidence();
            public readonly CallableEvidence Fleet = new CallableEvidence();
            public readonly CallableEvidence Warehouse = new CallableEvidence();
            public readonly CallableEvidence Contracts = new CallableEvidence();

            public string ToJson()
            {
                var text = new StringBuilder();
                text.Append("{\n");
                Field(text, "measuredAt", MeasuredAt);
                Field(text, "approvalAt", ApprovalAt);
                Field(text, "firstMutationAt", FirstMutationAt);
                Field(text, "projectId", ProjectId);
                Field(text, "functionsOrigin", FunctionsOrigin);
                Field(text, "authHost", AuthHost);
                Field(text, "authUid", AuthUid);
                text.Append("  \"authAnonymous\": ").Append(AuthAnonymous ? "true" : "false").Append(",\n");
                Field(text, "companyName", CompanyName);
                Field(text, "idempotencyKey", IdempotencyKey);
                text.Append("  \"marketCalled\": ").Append(MarketCalled ? "true" : "false").Append(",\n");
                text.Append("  \"userDeleted\": ").Append(UserDeleted ? "true" : "false").Append(",\n");
                text.Append("  \"ensurePlayerIdentity\": ").Append(Identity.ToJson()).Append(",\n");
                text.Append("  \"createCompany\": ").Append(CompanyCreate.ToJson()).Append(",\n");
                text.Append("  \"getMyCompanyState\": ").Append(CompanyRead.ToJson()).Append(",\n");
                text.Append("  \"getMyEconomyState\": ").Append(Economy.ToJson()).Append(",\n");
                text.Append("  \"getMyFleetState\": ").Append(Fleet.ToJson()).Append(",\n");
                text.Append("  \"getMyWarehouseState\": ").Append(Warehouse.ToJson()).Append(",\n");
                text.Append("  \"getMyContracts\": ").Append(Contracts.ToJson()).Append('\n');
                text.Append("}\n");
                return text.ToString();
            }

            static void Field(StringBuilder text, string key, string value)
            {
                text.Append("  \"").Append(key).Append("\": ");
                if (value == null)
                    text.Append("null");
                else
                    text.Append('"').Append(Escape(value)).Append('"');
                text.Append(",\n");
            }
        }

        sealed class CallableEvidence
        {
            public string Callable;
            public string Outcome;
            public string Detail;
            public string Raw;
            public int ProtocolVersion;
            public string ServerTime;
            public string PlayerId;
            public int SchemaVersion;
            public string IdentityStatus;
            public string CompanyId;
            public string CompanyName;
            public string BootstrapStatus;
            public bool HasCompany;
            public bool EconomyReady;
            public bool HasBalanceField;
            public bool StarterFleetReady;
            public int ItemCount;

            public string ToJson()
            {
                var text = new StringBuilder();
                text.Append("{\n");
                text.Append("    \"name\": ").Append(Quote(Callable)).Append(",\n");
                text.Append("    \"outcome\": ").Append(Quote(Outcome)).Append(",\n");
                text.Append("    \"detail\": ").Append(Quote(Detail)).Append(",\n");
                text.Append("    \"protocolVersion\": ").Append(ProtocolVersion).Append(",\n");
                text.Append("    \"serverTime\": ").Append(Quote(ServerTime)).Append(",\n");
                text.Append("    \"playerId\": ").Append(Quote(PlayerId)).Append(",\n");
                text.Append("    \"schemaVersion\": ").Append(SchemaVersion).Append(",\n");
                text.Append("    \"identityStatus\": ").Append(Quote(IdentityStatus)).Append(",\n");
                text.Append("    \"companyId\": ").Append(Quote(CompanyId)).Append(",\n");
                text.Append("    \"companyName\": ").Append(Quote(CompanyName)).Append(",\n");
                text.Append("    \"bootstrapStatus\": ").Append(Quote(BootstrapStatus)).Append(",\n");
                text.Append("    \"hasCompany\": ").Append(HasCompany ? "true" : "false").Append(",\n");
                text.Append("    \"economyReady\": ").Append(EconomyReady ? "true" : "false").Append(",\n");
                text.Append("    \"hasBalanceField\": ").Append(HasBalanceField ? "true" : "false").Append(",\n");
                text.Append("    \"starterFleetReady\": ").Append(StarterFleetReady ? "true" : "false").Append(",\n");
                text.Append("    \"itemCount\": ").Append(ItemCount).Append(",\n");
                text.Append("    \"raw\": ").Append(string.IsNullOrEmpty(Raw) ? "null" : Raw).Append('\n');
                text.Append("  }");
                return text.ToString();
            }

            static string Quote(string value)
            {
                return value == null ? "null" : "\"" + Escape(value) + "\"";
            }
        }
    }
}
