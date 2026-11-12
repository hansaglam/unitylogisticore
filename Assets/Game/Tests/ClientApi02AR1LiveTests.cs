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
    public class ClientApi02AR1LiveTests
    {
        const string EvidenceDir = "/tmp/logisticore-client-api-02a-r1";
        const string EvidencePath = EvidenceDir + "/live-call-evidence.json";
        const float StepSeconds = 90f;

        [SetUp]
        public void GateLiveEmulator()
        {
            // Anonymous sign-in creates an Auth user even when callables are reads.
            ClientLiveTestGuard.RequireMutation();
        }

        [UnityTest]
        public IEnumerator Editor_ConnectsToLocalEmulator_ReadOnlyCallables()
        {
            Directory.CreateDirectory(EvidenceDir);
            var evidence = new Evidence();
            evidence.MeasuredAt = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
            evidence.ProjectId = ClientProtocol.DemoProjectId;

            FirebaseClient.ResetForTests();
            ClientConnectionAuthorization.ResetForTests();
            ClientConnectionAuthorization.ApproveForTests();

            var settings = ClientEnvironmentSettings.Load();
            Assert.IsNotNull(settings);
            Assert.AreEqual(ClientBackendEnvironment.LocalEmulator, settings.Environment);
            var request = settings.ToRequest();
            Assert.AreEqual(ClientConnectionDecision.ConnectToEmulator, ClientConnectionPolicy.Decide(request));
            evidence.Environment = settings.Environment.ToString();
            evidence.FunctionsOrigin = settings.FunctionsEmulatorOrigin;
            evidence.FunctionsRegion = settings.FunctionsRegion;
            evidence.AuthHost = settings.EmulatorHost + ":" +
                settings.EmulatorAuthPort.ToString(CultureInfo.InvariantCulture);

            var status = FirebaseClient.Start(settings);
            Assert.AreEqual(ClientConnectionDecision.ConnectToEmulator, status.Decision);
            var readyDeadline = Time.realtimeSinceStartup + StepSeconds;
            while (FirebaseClient.Current.Phase == ClientBackendPhase.Starting &&
                   Time.realtimeSinceStartup < readyDeadline)
                yield return null;

            status = FirebaseClient.Current;
            evidence.FirebasePhase = status.Phase.ToString();
            evidence.FirebaseDetail = status.Detail;
            if (status.Phase != ClientBackendPhase.Ready)
            {
                WriteEvidence(evidence);
                Assert.Fail("Firebase SDK did not become Ready: " + status.Phase + " " + status.Detail);
            }

            FirebaseApp app = FirebaseApp.DefaultInstance;
            Assert.AreEqual(ClientProtocol.DemoProjectId, app.Options.ProjectId);
            evidence.AppProjectId = app.Options.ProjectId;

            var auth = FirebaseAuth.GetAuth(app);
            Assert.IsTrue(ClientEmulatorAuthPolicy.MaySignInAnonymously, ClientEmulatorAuthPolicy.AnonymousHold);
            var signIn = auth.SignInAnonymouslyAsync();
            yield return WaitTask(signIn, StepSeconds);
            if (!signIn.IsCompleted || signIn.IsFaulted || signIn.IsCanceled ||
                signIn.Result == null || signIn.Result.User == null)
            {
                evidence.AuthFault = FaultText(signIn);
                WriteEvidence(evidence);
                Assert.Fail("Anonymous emulator Auth failed.");
            }

            var user = signIn.Result.User;
            evidence.AuthAnonymous = user.IsAnonymous;
            evidence.AuthUid = user.UserId;
            Assert.IsTrue(user.IsAnonymous);
            Assert.IsFalse(string.IsNullOrEmpty(user.UserId));

            Assert.AreEqual(ClientCallableFault.None,
                ClientConnectionGate.Inspect(status, true, ClientConnectionAuthorization.Approved));
            Assert.IsTrue(ClientCallableCatalog.IsRead(ClientCallableCatalog.LocalHealth));
            Assert.IsTrue(ClientCallableCatalog.IsRead(ClientCallableCatalog.GetMyCompanyState));
            Assert.IsTrue(ClientCallableCatalog.IsRead(ClientCallableCatalog.GetMyEconomyState));
            Assert.IsFalse(ClientCallableCatalog.IsRead(ClientCallableCatalog.EnsurePlayerIdentity));
            Assert.IsFalse(ClientCallableCatalog.IsRead(ClientCallableCatalog.CreateCompany));

            var functions = FirebaseFunctions.GetInstance(app, settings.FunctionsRegion);
            yield return CallRead(functions, ClientCallableCatalog.LocalHealth, evidence.LocalHealth);
            if (evidence.LocalHealth.Outcome != "ok" || evidence.LocalHealth.ResultStatus != "ok")
            {
                WriteEvidence(evidence);
                Assert.Fail("localHealth did not return protocol ok.");
            }

            yield return CallRead(functions, ClientCallableCatalog.GetMyCompanyState, evidence.Company);
            if (evidence.Company.Outcome != "failed-precondition" && evidence.Company.Outcome != "ok")
            {
                WriteEvidence(evidence);
                Assert.Fail("getMyCompanyState returned unexpected " + evidence.Company.Outcome);
            }

            var economyAllowed = evidence.Company.Outcome == "ok" &&
                                 evidence.Company.HasCompany &&
                                 evidence.Company.EconomyReady;
            if (economyAllowed)
                yield return CallRead(functions, ClientCallableCatalog.GetMyEconomyState, evidence.Economy);
            else
            {
                evidence.Economy.Name = ClientCallableCatalog.GetMyEconomyState;
                evidence.Economy.Outcome = "skipped";
                evidence.Economy.Detail =
                    "Company/economy preconditions were not met. ensurePlayerIdentity and createCompany were not called.";
            }

            evidence.Invoked = evidence.Economy.Outcome == "skipped" ? 2 : 3;
            evidence.CommandsAttempted = 0;
            evidence.EnsurePlayerIdentity = false;
            evidence.CreateCompany = false;
            WriteEvidence(evidence);

            var delete = user.DeleteAsync();
            yield return WaitTask(delete, 30f);
            auth.SignOut();
            ClientConnectionAuthorization.ResetForTests();
            FirebaseClient.ResetForTests();
        }

        static IEnumerator CallRead(FirebaseFunctions functions, string name, CallableEvidence target)
        {
            target.Name = name;
            Assert.IsTrue(ClientCallableCatalog.IsRead(name), name);
            var task = functions.GetHttpsCallable(name).CallAsync(ClientValue.ReadRequest());
            yield return WaitTask(task, StepSeconds);
            if (task == null || !task.IsCompleted)
            {
                target.Outcome = "timeout";
                yield break;
            }

            if (task.IsCanceled)
            {
                target.Outcome = "canceled";
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
            if (name == ClientCallableCatalog.LocalHealth)
            {
                if (ClientValue.TryMap(envelope.Result, out var map) &&
                    ClientValue.TryString(ClientValue.Get(map, "status"), out var health))
                    target.ResultStatus = health;
                target.Outcome = target.ResultStatus == "ok" ? "ok" : "unexpected-result";
                yield break;
            }

            if (name == ClientCallableCatalog.GetMyCompanyState)
            {
                if (!ClientReadParser.TryCompany(envelope.Result, out var company))
                {
                    target.Outcome = "unparsed-company";
                    yield break;
                }

                target.HasCompany = company.HasCompany;
                target.EconomyReady = company.EconomyReady;
                target.Outcome = "ok";
                yield break;
            }

            if (name == ClientCallableCatalog.GetMyEconomyState)
            {
                if (!ClientReadParser.TryEconomy(envelope.Result, out var economy))
                {
                    target.Outcome = "unparsed-economy";
                    yield break;
                }

                target.HasCompany = economy.HasCompany;
                target.EconomyReady = economy.EconomyReady;
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
                return "canceled-or-empty";
            var inner = task.Exception.GetBaseException();
            return inner.GetType().Name + ": " + inner.Message;
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
            return string.Equals(key, "idToken", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "refreshToken", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "accessToken", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "token", StringComparison.OrdinalIgnoreCase);
        }

        static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        sealed class Evidence
        {
            public string MeasuredAt;
            public string ProjectId;
            public string Environment;
            public string FunctionsOrigin;
            public string FunctionsRegion;
            public string AuthHost;
            public string FirebasePhase;
            public string FirebaseDetail;
            public string AppProjectId;
            public bool AuthAnonymous;
            public string AuthUid;
            public string AuthFault;
            public int Invoked;
            public int CommandsAttempted;
            public bool EnsurePlayerIdentity;
            public bool CreateCompany;
            public readonly CallableEvidence LocalHealth = new CallableEvidence();
            public readonly CallableEvidence Company = new CallableEvidence();
            public readonly CallableEvidence Economy = new CallableEvidence();

            public string ToJson()
            {
                var text = new StringBuilder();
                text.Append("{\n");
                Field(text, "measuredAt", MeasuredAt);
                Field(text, "projectId", ProjectId);
                Field(text, "environment", Environment);
                Field(text, "functionsOrigin", FunctionsOrigin);
                Field(text, "functionsRegion", FunctionsRegion);
                Field(text, "authHost", AuthHost);
                Field(text, "firebasePhase", FirebasePhase);
                Field(text, "firebaseDetail", FirebaseDetail);
                Field(text, "appProjectId", AppProjectId);
                text.Append("  \"authAnonymous\": ").Append(AuthAnonymous ? "true" : "false").Append(",\n");
                Field(text, "authUid", AuthUid);
                Field(text, "authFault", AuthFault);
                text.Append("  \"invoked\": ").Append(Invoked).Append(",\n");
                text.Append("  \"commandsAttempted\": ").Append(CommandsAttempted).Append(",\n");
                text.Append("  \"ensurePlayerIdentity\": ").Append(EnsurePlayerIdentity ? "true" : "false").Append(",\n");
                text.Append("  \"createCompany\": ").Append(CreateCompany ? "true" : "false").Append(",\n");
                text.Append("  \"forbiddenInvoked\": [],\n");
                text.Append("  \"localHealth\": ").Append(LocalHealth.ToJson()).Append(",\n");
                text.Append("  \"getMyCompanyState\": ").Append(Company.ToJson()).Append(",\n");
                text.Append("  \"getMyEconomyState\": ").Append(Economy.ToJson()).Append('\n');
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
            public string Name;
            public string Outcome;
            public string Detail;
            public string Raw;
            public int ProtocolVersion;
            public string ServerTime;
            public string ResultStatus;
            public bool HasCompany;
            public bool EconomyReady;

            public string ToJson()
            {
                var text = new StringBuilder();
                text.Append("{\n");
                text.Append("    \"name\": ").Append(Quote(Name)).Append(",\n");
                text.Append("    \"outcome\": ").Append(Quote(Outcome)).Append(",\n");
                text.Append("    \"detail\": ").Append(Quote(Detail)).Append(",\n");
                text.Append("    \"protocolVersion\": ").Append(ProtocolVersion).Append(",\n");
                text.Append("    \"serverTime\": ").Append(Quote(ServerTime)).Append(",\n");
                text.Append("    \"resultStatus\": ").Append(Quote(ResultStatus)).Append(",\n");
                text.Append("    \"hasCompany\": ").Append(HasCompany ? "true" : "false").Append(",\n");
                text.Append("    \"economyReady\": ").Append(EconomyReady ? "true" : "false").Append(",\n");
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
