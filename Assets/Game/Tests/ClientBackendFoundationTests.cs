using System;
using System.Threading;
using LogistiCore.Backend;
using LogistiCore.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace LogistiCore.Tests
{
    public class ClientConnectionPolicyTests
    {
        [Test]
        public void Unconfigured_StaysOfflineEvenWhenValuesArePresent()
        {
            var decision = ClientConnectionPolicy.Decide(
                new ClientConnectionRequest(ClientBackendEnvironment.Unconfigured, true, true));

            Assert.AreEqual(ClientConnectionDecision.StayOffline, decision);
            Assert.AreEqual(
                ClientConnectionPolicy.OfflineDetail,
                ClientConnectionPolicy.Describe(ClientBackendEnvironment.Unconfigured, decision));
        }

        [Test]
        public void DevelopmentWithoutConfiguration_IsRefusedAndDoesNotBecomeProduction()
        {
            var request = new ClientConnectionRequest(ClientBackendEnvironment.Development, false, true);
            var decision = ClientConnectionPolicy.Decide(request);

            Assert.AreEqual(ClientBackendEnvironment.Development, request.Environment);
            Assert.AreEqual(ClientConnectionDecision.Refused, decision);
            Assert.AreEqual(ClientConnectionPolicy.MissingDevelopmentDetail, ClientConnectionPolicy.Describe(request.Environment, decision));
        }

        [Test]
        public void DevelopmentWithConfiguration_ConnectsOnlyToDevelopment()
        {
            var decision = ClientConnectionPolicy.Decide(
                new ClientConnectionRequest(ClientBackendEnvironment.Development, true, true));

            Assert.AreEqual(ClientConnectionDecision.ConnectToDevelopment, decision);
        }

        [Test]
        public void EmulatorWithoutEndpointOrAppConfiguration_IsRefused()
        {
            Assert.AreEqual(
                ClientConnectionDecision.Refused,
                ClientConnectionPolicy.Decide(new ClientConnectionRequest(ClientBackendEnvironment.LocalEmulator, true, false)));
            Assert.AreEqual(
                ClientConnectionDecision.Refused,
                ClientConnectionPolicy.Decide(new ClientConnectionRequest(ClientBackendEnvironment.LocalEmulator, false, true)));
            Assert.AreEqual(
                ClientConnectionPolicy.MissingEmulatorDetail,
                ClientConnectionPolicy.Describe(ClientBackendEnvironment.LocalEmulator, ClientConnectionDecision.Refused));
        }

        [Test]
        public void EmulatorWithConfiguration_ConnectsToTheEmulator()
        {
            var decision = ClientConnectionPolicy.Decide(
                new ClientConnectionRequest(ClientBackendEnvironment.LocalEmulator, true, true));

            Assert.AreEqual(ClientConnectionDecision.ConnectToEmulator, decision);
        }

        [Test]
        public void Production_IsAlwaysRefused()
        {
            var decision = ClientConnectionPolicy.Decide(
                new ClientConnectionRequest(ClientBackendEnvironment.Production, true, true));

            Assert.AreEqual(ClientConnectionDecision.Refused, decision);
            Assert.AreEqual(
                ClientConnectionPolicy.ProductionDetail,
                ClientConnectionPolicy.Describe(ClientBackendEnvironment.Production, decision));
        }
    }

    public class ClientEnvironmentSettingsTests
    {
        [Test]
        public void EmptyOrWhitespaceConfiguration_IsNotUsable()
        {
            var settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            try
            {
                settings.Configure(ClientBackendEnvironment.Development, "  ", "app", "key");
                var request = settings.ToRequest();
                Assert.IsFalse(request.HasAppConfiguration);
                Assert.AreEqual(ClientBackendEnvironment.Development, request.Environment);
                Assert.AreEqual(ClientConnectionDecision.Refused, ClientConnectionPolicy.Decide(request));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void EmulatorEndpoint_RequiresAHostAndAValidPortOrAnExplicitFunctionsOrigin()
        {
            var settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            try
            {
                settings.Configure(
                    ClientBackendEnvironment.LocalEmulator,
                    "local-project",
                    "local-app",
                    "local-key",
                    " ",
                    1);
                Assert.IsFalse(settings.ToRequest().HasEmulatorEndpoint);

                settings.Configure(
                    ClientBackendEnvironment.LocalEmulator,
                    "local-project",
                    "local-app",
                    "local-key",
                    "emulator.invalid",
                    65536);
                Assert.IsFalse(settings.ToRequest().HasEmulatorEndpoint);

                settings.Configure(
                    ClientBackendEnvironment.LocalEmulator,
                    "local-project",
                    "local-app",
                    "local-key",
                    nextFunctionsEmulatorOrigin: "http://emulator.invalid",
                    nextFunctionsRegion: " ");
                Assert.IsFalse(settings.ToRequest().HasEmulatorEndpoint);

                settings.Configure(
                    ClientBackendEnvironment.LocalEmulator,
                    "local-project",
                    "local-app",
                    "local-key",
                    "emulator.invalid",
                    1);
                Assert.IsTrue(settings.ToRequest().HasEmulatorEndpoint);
                Assert.AreEqual(ClientConnectionDecision.ConnectToEmulator, ClientConnectionPolicy.Decide(settings.ToRequest()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void LocalOverride_WinsAndCanKeepTheClientOffline()
        {
            var local = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            var shared = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            try
            {
                local.Configure(ClientBackendEnvironment.Unconfigured);
                shared.Configure(ClientBackendEnvironment.Development, "local-project", "local-app", "local-key");

                var selected = ClientEnvironmentSettings.Select(local, shared);
                Assert.AreSame(local, selected);
                Assert.AreEqual(ClientConnectionDecision.StayOffline, ClientConnectionPolicy.Decide(selected.ToRequest()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(local);
                UnityEngine.Object.DestroyImmediate(shared);
            }
        }

        [Test]
        public void MissingLocalOverride_UsesTheSharedAsset()
        {
            var shared = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            try
            {
                shared.Configure(ClientBackendEnvironment.Unconfigured);
                Assert.AreSame(shared, ClientEnvironmentSettings.Select(null, shared));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shared);
            }
        }

        [Test]
        public void DefaultAsset_IsUnconfiguredAndEmpty()
        {
            ClientEnvironmentAssetSetup.EnsureDefaultAsset();
            var settings = ClientEnvironmentSettings.Load();
            var request = settings.ToRequest();

            Assert.AreEqual(ClientBackendEnvironment.Unconfigured, settings.Environment);
            Assert.IsFalse(request.HasAppConfiguration);
            Assert.IsFalse(request.HasEmulatorEndpoint);
            Assert.AreEqual(ClientConnectionDecision.StayOffline, ClientConnectionPolicy.Decide(request));
            Assert.IsFalse(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(settings)));
        }
    }

    public class FirebaseClientTests
    {
        [SetUp]
        public void SetUp()
        {
            FirebaseClient.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            FirebaseClient.ResetForTests();
        }

        [Test]
        public void UnconfiguredStartup_DoesNotEnterTheSdk()
        {
            var settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            try
            {
                var status = FirebaseClient.Start(settings);

                Assert.AreEqual(0, FirebaseClient.ConnectAttempts);
                Assert.AreEqual(ClientBackendPhase.Offline, status.Phase);
                Assert.AreEqual(ClientConnectionDecision.StayOffline, status.Decision);
                Assert.AreEqual(ClientConnectionPolicy.OfflineDetail, status.Detail);
                Assert.IsFalse(status.Detail.Contains("super-secret-key"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void NullSettings_StayOffline()
        {
            var status = FirebaseClient.Start(null);

            Assert.AreEqual(0, FirebaseClient.ConnectAttempts);
            Assert.AreEqual(ClientBackendPhase.Offline, status.Phase);
        }

        [Test]
        public void FilledUnconfiguredSettings_DoNotConnect()
        {
            var settings = Configured(ClientBackendEnvironment.Unconfigured);
            try
            {
                var status = FirebaseClient.Start(settings);

                Assert.AreEqual(0, FirebaseClient.ConnectAttempts);
                Assert.AreEqual(ClientConnectionDecision.StayOffline, status.Decision);
                Assert.IsFalse(status.Detail.Contains("super-secret-key"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ProductionAndMissingDevelopment_DoNotEnterTheSdk()
        {
            var production = Configured(ClientBackendEnvironment.Production);
            var missing = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            missing.Configure(ClientBackendEnvironment.Development);
            try
            {
                LogAssert.Expect(LogType.Error, FirebaseLog(ClientConnectionPolicy.ProductionDetail));
                var productionStatus = FirebaseClient.Start(production);
                Assert.AreEqual(ClientBackendPhase.Refused, productionStatus.Phase);
                Assert.AreEqual(ClientConnectionPolicy.ProductionDetail, productionStatus.Detail);

                FirebaseClient.ResetForTests();
                LogAssert.Expect(LogType.Error, FirebaseLog(ClientConnectionPolicy.MissingDevelopmentDetail));
                var missingStatus = FirebaseClient.Start(missing);
                Assert.AreEqual(ClientBackendPhase.Refused, missingStatus.Phase);
                Assert.AreEqual(ClientConnectionPolicy.MissingDevelopmentDetail, missingStatus.Detail);
                Assert.AreEqual(0, FirebaseClient.ConnectAttempts);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(production);
                UnityEngine.Object.DestroyImmediate(missing);
            }
        }

        [Test]
        public void SecondStart_KeepsTheFirstDecision()
        {
            var first = FirebaseClient.Start(null);
            var configured = Configured(ClientBackendEnvironment.Development);
            try
            {
                var second = FirebaseClient.Start(configured);

                Assert.AreEqual(ClientBackendPhase.Offline, first.Phase);
                Assert.AreEqual(ClientBackendPhase.Offline, second.Phase);
                Assert.AreEqual(0, FirebaseClient.ConnectAttempts);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(configured);
            }
        }

        [Test]
        public void ApprovedDevelopmentConfiguration_EntersTheSdkOnceAndDoesNotSignIn()
        {
            var calls = 0;
            ClientConnectionDecision seen = default;
            FirebaseClient.Connect = (settings, decision, completed) =>
            {
                calls++;
                seen = decision;
                completed(FirebaseClient.ClientSdkResult.Ready);
            };

            var settings = Configured(ClientBackendEnvironment.Development);
            try
            {
                var status = FirebaseClient.Start(settings);
                var again = FirebaseClient.Start(settings);

                Assert.AreEqual(1, calls);
                Assert.AreEqual(1, FirebaseClient.ConnectAttempts);
                Assert.AreEqual(ClientConnectionDecision.ConnectToDevelopment, seen);
                Assert.AreEqual(ClientBackendPhase.Ready, status.Phase);
                Assert.AreEqual(ClientBackendPhase.Ready, again.Phase);
                Assert.AreEqual(FirebaseClient.ReadyDetail, status.Detail);
                Assert.IsFalse(status.Detail.Contains("super-secret-key"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void EmulatorConfiguration_SelectsTheEmulatorWithoutCallingFirebase()
        {
            ClientConnectionDecision seen = default;
            FirebaseClient.Connect = (settings, decision, completed) =>
            {
                seen = decision;
                completed(FirebaseClient.ClientSdkResult.Ready);
            };

            var settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            settings.Configure(
                ClientBackendEnvironment.LocalEmulator,
                "local-project",
                "local-app",
                "super-secret-key",
                "emulator.invalid",
                1);
            try
            {
                var status = FirebaseClient.Start(settings);
                Assert.AreEqual(ClientConnectionDecision.ConnectToEmulator, seen);
                Assert.AreEqual(ClientBackendPhase.Ready, status.Phase);
                Assert.AreEqual(1, FirebaseClient.ConnectAttempts);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void DependencyFailure_IsTerminalAndDoesNotLeakConfiguration()
        {
            FirebaseClient.Connect = (settings, decision, completed) =>
                completed(FirebaseClient.ClientSdkResult.DependenciesUnavailable);

            var settings = Configured(ClientBackendEnvironment.Development);
            try
            {
                LogAssert.Expect(LogType.Error, FirebaseLog(FirebaseClient.DependenciesDetail));
                var status = FirebaseClient.Start(settings);
                var again = FirebaseClient.Start(settings);

                Assert.AreEqual(ClientBackendPhase.Failed, status.Phase);
                Assert.AreEqual(FirebaseClient.DependenciesDetail, status.Detail);
                Assert.AreEqual(ClientBackendPhase.Failed, again.Phase);
                Assert.AreEqual(1, FirebaseClient.ConnectAttempts);
                Assert.IsFalse(status.Detail.Contains("super-secret-key"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ConnectorException_FailsWithoutRetryOrSecret()
        {
            FirebaseClient.Connect = (settings, decision, completed) =>
                throw new InvalidOperationException("super-secret-key");

            var settings = Configured(ClientBackendEnvironment.Development);
            try
            {
                LogAssert.Expect(LogType.Error, FirebaseLog("Initialization failed (InvalidOperationException)."));
                var status = FirebaseClient.Start(settings);

                Assert.AreEqual(ClientBackendPhase.Failed, status.Phase);
                Assert.AreEqual(FirebaseClient.FailedDetail, status.Detail);
                Assert.IsFalse(status.Detail.Contains("super-secret-key"));
                Assert.AreEqual(1, FirebaseClient.ConnectAttempts);
                FirebaseClient.Start(settings);
                Assert.AreEqual(1, FirebaseClient.ConnectAttempts);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void InFlightStart_DoesNotEnterTwice()
        {
            Action<FirebaseClient.ClientSdkResult> pending = null;
            FirebaseClient.Connect = (settings, decision, completed) => pending = completed;

            var settings = Configured(ClientBackendEnvironment.Development);
            try
            {
                var first = FirebaseClient.Start(settings);
                var second = FirebaseClient.Start(settings);
                Assert.AreEqual(ClientBackendPhase.Starting, first.Phase);
                Assert.AreEqual(ClientBackendPhase.Starting, second.Phase);
                Assert.AreEqual(1, FirebaseClient.ConnectAttempts);

                LogAssert.Expect(LogType.Error, FirebaseLog(FirebaseClient.FailedDetail));
                pending(FirebaseClient.ClientSdkResult.Failed);
                Assert.AreEqual(ClientBackendPhase.Failed, FirebaseClient.Current.Phase);
                Assert.AreEqual(1, FirebaseClient.ConnectAttempts);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void OffMainThread_DoesNotEnterTheSdkOrReplaceTheCurrentDecision()
        {
            var settings = Configured(ClientBackendEnvironment.Development);
            ClientBackendStatus status = default;
            try
            {
                var thread = new Thread(() => status = FirebaseClient.Start(settings));
                thread.Start();
                thread.Join();

                Assert.AreEqual(ClientBackendPhase.Failed, status.Phase);
                Assert.AreEqual(FirebaseClient.OffThreadDetail, status.Detail);
                Assert.AreEqual(0, FirebaseClient.ConnectAttempts);
                Assert.AreEqual(ClientBackendPhase.Idle, FirebaseClient.Current.Phase);

                FirebaseClient.Connect = (value, decision, completed) => completed(FirebaseClient.ClientSdkResult.Ready);
                var mainThreadStatus = FirebaseClient.Start(settings);
                Assert.AreEqual(ClientBackendPhase.Ready, mainThreadStatus.Phase);
                Assert.AreEqual(1, FirebaseClient.ConnectAttempts);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        static string FirebaseLog(string message)
        {
            return "[LogistiCore] [Firebase] " + message;
        }

        static ClientEnvironmentSettings Configured(ClientBackendEnvironment environment)
        {
            var settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            settings.Configure(environment, "local-project", "local-app", "super-secret-key", "emulator.invalid", 1);
            return settings;
        }
    }
}
