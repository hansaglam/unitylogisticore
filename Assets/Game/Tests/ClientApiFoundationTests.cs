using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LogistiCore.Backend;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LogistiCore.Tests
{
    public class ClientApiFoundationTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        [SetUp]
        public void SetUp()
        {
            FirebaseClient.ResetForTests();
            ClientConnectionAuthorization.ResetForTests();
            ClientEmulatorLiveAuthorization.ResetForTests();
            MapFleetStore.ClearOverride();
            MapFleetStore.ClearTrusted();
            MapJobsStore.ClearOverride();
            MapJobsStore.ClearTrusted();
            MapWarehouseStore.ClearOverride();
            MapWarehouseStore.ClearTrusted();
            MapMarketStore.ClearOverride();
            MapMarketStore.ClearTrusted();
            MapRoadDeliveryStore.ClearOverride();
        }

        [TearDown]
        public void TearDown()
        {
            SetUp();
        }

        [Test]
        public void SceneHash_IsUnchanged()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void DefaultLiveAuthorization_DeniesEmulatorAuthAndMutations()
        {
            var previous = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable, null);
                ClientEmulatorLiveAuthorization.ResetForTests();
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsRead);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsAuth);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsIdentity);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsCompany);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsEconomy);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsFleet);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsMutation);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreateAnonymousUser);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MaySignInAnonymously);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreatePasswordUser);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MaySignInExistingPassword);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreateCompany);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayInitializeEconomy);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayProvisionStarterFleet);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable, previous);
                ClientEmulatorLiveAuthorization.ResetForTests();
            }
        }

        [Test]
        public void ReadToken_AllowsPasswordSignInButNotAnonymousCreate()
        {
            var previous = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    ClientEmulatorLiveAuthorization.EnvironmentVariable,
                    ClientEmulatorLiveAuthorization.ReadToken);
                ClientEmulatorLiveAuthorization.ResetForTests();
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsRead);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsAuth);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsIdentity);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsMutation);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MaySignInExistingPassword);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MaySignInAnonymously);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreateAnonymousUser);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreatePasswordUser);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable, previous);
                ClientEmulatorLiveAuthorization.ResetForTests();
            }
        }

        [Test]
        public void MutateToken_AllowsAnonymousAndPasswordCreate()
        {
            var previous = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    ClientEmulatorLiveAuthorization.EnvironmentVariable,
                    ClientEmulatorLiveAuthorization.MutateToken);
                ClientEmulatorLiveAuthorization.ResetForTests();
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsRead);
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsAuth);
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsIdentity);
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsCompany);
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsEconomy);
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsFleet);
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsMutation);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MaySignInAnonymously);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MayCreateAnonymousUser);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MayCreatePasswordUser);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MaySignInExistingPassword);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable, previous);
                ClientEmulatorLiveAuthorization.ResetForTests();
            }
        }

        [Test]
        public void AuthToken_AllowsPasswordAccountButNotAnonymous()
        {
            var previous = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    ClientEmulatorLiveAuthorization.EnvironmentVariable,
                    ClientEmulatorLiveAuthorization.AuthToken);
                ClientEmulatorLiveAuthorization.ResetForTests();
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsRead);
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsAuth);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsIdentity);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsMutation);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MayCreatePasswordUser);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MaySignInExistingPassword);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MaySignInAnonymously);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreateAnonymousUser);
                Assert.IsTrue(ClientEmulatorAuthPolicy.IsLoopbackEmulatorHost("127.0.0.1"));
                Assert.IsFalse(ClientEmulatorAuthPolicy.IsLoopbackEmulatorHost("identitytoolkit.googleapis.com"));
                Assert.IsTrue(ClientEmulatorAuthPolicy.IsSyntheticTestEmail(ClientEmulatorAuthCredentials.Email));
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable, previous);
                ClientEmulatorLiveAuthorization.ResetForTests();
            }
        }

        [Test]
        public void IdentityToken_AllowsEnsurePlayerIdentityWithoutAuthCreate()
        {
            var previous = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    ClientEmulatorLiveAuthorization.EnvironmentVariable,
                    ClientEmulatorLiveAuthorization.IdentityToken);
                ClientEmulatorLiveAuthorization.ResetForTests();
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsRead);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsAuth);
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsIdentity);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsCompany);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsEconomy);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsFleet);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsMutation);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MaySignInExistingPassword);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreatePasswordUser);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MaySignInAnonymously);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreateCompany);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayInitializeEconomy);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayProvisionStarterFleet);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable, previous);
                ClientEmulatorLiveAuthorization.ResetForTests();
            }
        }

        [Test]
        public void CompanyToken_AllowsCreateCompanyWithoutIdentityOrAuthCreate()
        {
            var previous = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    ClientEmulatorLiveAuthorization.EnvironmentVariable,
                    ClientEmulatorLiveAuthorization.CompanyToken);
                ClientEmulatorLiveAuthorization.ResetForTests();
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsRead);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsAuth);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsIdentity);
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsCompany);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsEconomy);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsFleet);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsMutation);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MayCreateCompany);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MaySignInExistingPassword);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreatePasswordUser);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MaySignInAnonymously);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayInitializeEconomy);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayProvisionStarterFleet);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable, previous);
                ClientEmulatorLiveAuthorization.ResetForTests();
            }
        }

        [Test]
        public void EconomyToken_AllowsInitializeWithoutCompanyOrIdentityWrites()
        {
            var previous = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    ClientEmulatorLiveAuthorization.EnvironmentVariable,
                    ClientEmulatorLiveAuthorization.EconomyToken);
                ClientEmulatorLiveAuthorization.ResetForTests();
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsRead);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsAuth);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsIdentity);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsCompany);
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsEconomy);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsFleet);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsMutation);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MayInitializeEconomy);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MaySignInExistingPassword);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreateCompany);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreatePasswordUser);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MaySignInAnonymously);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayProvisionStarterFleet);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable, previous);
                ClientEmulatorLiveAuthorization.ResetForTests();
            }
        }

        [Test]
        public void FleetToken_AllowsStarterGrantWithoutEconomyOrCompanyWrites()
        {
            var previous = Environment.GetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    ClientEmulatorLiveAuthorization.EnvironmentVariable,
                    ClientEmulatorLiveAuthorization.FleetToken);
                ClientEmulatorLiveAuthorization.ResetForTests();
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsRead);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsAuth);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsIdentity);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsCompany);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsEconomy);
                Assert.IsTrue(ClientEmulatorLiveAuthorization.AllowsFleet);
                Assert.IsFalse(ClientEmulatorLiveAuthorization.AllowsMutation);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MayProvisionStarterFleet);
                Assert.IsTrue(ClientEmulatorAuthPolicy.MaySignInExistingPassword);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayEnsurePlayerIdentity);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreateCompany);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayInitializeEconomy);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MayCreatePasswordUser);
                Assert.IsFalse(ClientEmulatorAuthPolicy.MaySignInAnonymously);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ClientEmulatorLiveAuthorization.EnvironmentVariable, previous);
                ClientEmulatorLiveAuthorization.ResetForTests();
            }
        }

        [Test]
        public void UnconfiguredEnvironment_DoesNotOpenASession()
        {
            var session = new ClientReadSession();
            var invokes = 0;
            session.SetInvoker((_, __) =>
            {
                invokes++;
                return null;
            });
            var outcome = session.Read(
                ClientCallableCatalog.GetMyFleetState,
                FirebaseClient.Start(null),
                true,
                ClientValue.ReadRequest());
            Assert.AreEqual(ClientCallableFault.ConnectionHold, outcome.Fault);
            Assert.AreEqual(0, invokes);
        }

        [Test]
        public void ApprovedSessionWithoutInvoker_DoesNotTouchTheNetwork()
        {
            ClientConnectionAuthorization.ApproveForTests();
            FirebaseClient.Connect = (s, d, done) => done(FirebaseClient.ClientSdkResult.Ready);
            var settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            settings.Configure(ClientBackendEnvironment.Development, "demo-logisticore-local", "app", "key");
            try
            {
                var status = FirebaseClient.Start(settings);
                var session = new ClientReadSession();
                var outcome = session.Read(ClientCallableCatalog.GetMyFleetState, status, true, ClientValue.ReadRequest());
                Assert.AreEqual(ClientCallableFault.TransportDisabled, outcome.Fault);
                Assert.AreEqual(0, session.InvokeCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ProductionEndpoint_IsNotInTheReadCatalog()
        {
            Assert.IsFalse(ClientCallableCatalog.TryKind("https://us-central1-logisticore.cloudfunctions.net", out _));
            Assert.AreEqual(ClientProtocol.DemoProjectId, "demo-logisticore-local");
        }

        [Test]
        public void MissingAuth_IsMappedBeforeInvoke()
        {
            ClientConnectionAuthorization.ApproveForTests();
            FirebaseClient.Connect = (s, d, done) => done(FirebaseClient.ClientSdkResult.Ready);
            var settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            settings.Configure(ClientBackendEnvironment.LocalEmulator, "demo-logisticore-local", "app", "key", "127.0.0.1", 8080);
            try
            {
                var status = FirebaseClient.Start(settings);
                var session = new ClientReadSession();
                var invokes = 0;
                session.SetInvoker((_, __) =>
                {
                    invokes++;
                    return null;
                });
                var outcome = session.Read(ClientCallableCatalog.GetMyCompanyState, status, false, ClientValue.ReadRequest());
                Assert.AreEqual(ClientCallableFault.MissingAuth, outcome.Fault);
                Assert.AreEqual(0, invokes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ProtocolMismatch_IsRejected()
        {
            var raw = Map(
                "protocolVersion", 2,
                "serverTime", "2026-10-16T12:00:00.000Z",
                "result", Map("status", "ok"));
            Assert.IsFalse(ClientEnvelope.TryParse(raw, out _, out var fault));
            Assert.AreEqual(ClientCallableFault.ProtocolMismatch, fault);
        }

        [Test]
        public void MalformedResponse_IsRejected()
        {
            Assert.IsFalse(ClientEnvelope.TryParse("not-json", out _, out var fault));
            Assert.AreEqual(ClientCallableFault.MalformedResponse, fault);
        }

        [Test]
        public void ExtraResponseFields_AreTolerated()
        {
            var raw = Map(
                "protocolVersion", 1,
                "serverTime", "2026-10-16T12:00:00.000Z",
                "result", Map("status", "ok"),
                "unused", "forward");
            Assert.IsTrue(ClientEnvelope.TryParse(raw, out var envelope, out var fault));
            Assert.AreEqual(ClientCallableFault.None, fault);
            Assert.AreEqual(1, envelope.ProtocolVersion);
        }

        [Test]
        public void Commands_AreNotDispatched()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = new ClientReadSession();
            var invokes = 0;
            session.SetInvoker((_, __) =>
            {
                invokes++;
                return null;
            });
            var outcome = session.Read(
                ClientCallableCatalog.StartDelivery,
                new ClientBackendStatus(ClientBackendEnvironment.LocalEmulator, ClientConnectionDecision.ConnectToEmulator, ClientBackendPhase.Ready, ""),
                true,
                ClientValue.ReadRequest());
            Assert.AreEqual(ClientCallableFault.NonRetryableCommand, outcome.Fault);
            Assert.AreEqual(0, invokes);
            Assert.IsFalse(ClientCallableErrors.MayRetryCommand(ClientCallableFault.RetryableRead));
        }

        [Test]
        public void DuplicateInFlight_IsRejected()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = new ClientReadSession();
            ClientReadOutcome nested = default;
            session.SetInvoker((name, payload) =>
            {
                nested = session.Read(name, ReadyStatus(), true, payload);
                return Envelope(Map("hasCompany", true, "economyReady", false, "starterFleetReady", false, "items", new object[0], "nextCursor", null));
            });
            var first = session.Read(ClientCallableCatalog.GetMyFleetState, ReadyStatus(), true, ClientValue.ReadRequest());
            Assert.IsTrue(first.Ok);
            Assert.AreEqual(ClientCallableFault.DuplicateInFlight, nested.Fault);
        }

        [Test]
        public void Money_RejectsFloatAndKeepsMinorUnits()
        {
            Assert.IsFalse(ClientMoney.TryValidate("10.5", "TST", 2));
            Assert.IsTrue(ClientMoney.TryFormat("100", "TST", 2, out var display));
            Assert.AreEqual("1.00 TST", display);
        }

        [Test]
        public void CityCompatibility_DoesNotRewriteMapCities()
        {
            Assert.IsTrue(ClientStoreBridge.CityKnown("istanbul"));
            Assert.IsFalse(ClientStoreBridge.CityKnown("synthetic_city"));
        }

        [Test]
        public void Store_TrustedEmpty_IsNotUnconfigured()
        {
            Assert.AreEqual(MapFleetStatus.Unconfigured, MapFleetStore.Current.Status);
            Assert.IsTrue(ClientReadParser.TryFleet(
                Map("hasCompany", true, "economyReady", true, "starterFleetReady", true, "items", new object[0], "nextCursor", null),
                out var fleet));
            Assert.IsTrue(MapFleetStore.TryApplyTrusted(ClientStoreBridge.Fleet(fleet)));
            Assert.AreEqual(MapFleetStatus.Empty, MapFleetStore.Current.Status);
            Assert.IsFalse(MapFleetStore.Current.Synthetic);
        }

        [Test]
        public void Store_RejectsSyntheticAsTrusted()
        {
            var fixture = MapFleetSnapshot.Fixture(MapFleetStatus.Ready, new[]
            {
                new MapFleetAsset("starter_truck", MapFleetKind.Truck, "test_truck", MapFleetAssetStatus.Operational, true, "", "", "", false, 0, false, 0)
            });
            Assert.IsFalse(MapFleetStore.TryApplyTrusted(fixture));
            Assert.AreEqual(MapFleetStatus.Unconfigured, MapFleetStore.Current.Status);
        }

        [Test]
        public void RoadFilter_RefusesUnclassifiedDeliveries()
        {
            Assert.IsTrue(ClientReadParser.TryDeliveries(
                Map(
                    "items",
                    new object[]
                    {
                        Map(
                            "deliveryId", "d1",
                            "contractId", "c1",
                            "status", "active",
                            "originCityId", "synthetic_city",
                            "destinationCityId", "synthetic_destination",
                            "routeId", "synthetic_route",
                            "cargoId", "synthetic_cargo",
                            "assets", new object[] { Map("assetId", "starter_truck", "kind", "truck") },
                            "startedAt", "2026-10-20T12:00:00.000Z",
                            "expectedArrivalAt", "2026-10-20T12:00:01.000Z",
                            "arrivalReady", false,
                            "completedAt", null,
                            "reward", "6",
                            "currency", "TST",
                            "scale", 2)
                    },
                    "nextCursor", null),
                out var page));
            Assert.IsFalse(ClientRoadFilter.CanPresentAsRoad(page.Items[0]));
            Assert.IsFalse(MapRoadDeliveryStore.TryApplyTrusted(MapRoadDeliverySnapshot.Fixture(
                MapRoadDeliveryStatus.Ready,
                new[] { new MapRoadDeliveryItem("d1", "c1", MapRoadLifecycle.Active, "istanbul", "ankara", "starter_truck", "2026-10-20T12:00:01.000Z", false) })));
            Assert.AreEqual(MapRoadDeliveryStatus.Unconfigured, MapRoadDeliveryStore.Current.Status);
        }

        [Test]
        public void MarketParser_RejectsInventedPrice()
        {
            Assert.IsFalse(ClientReadParser.TryMarket(
                Map("synthetic", true, "conditions", new object[] { Map("cityId", "istanbul", "cargoId", "ore", "price", "1") }),
                out _));
        }

        [Test]
        public void ErrorMapping_UsesCanonicalCodes()
        {
            Assert.AreEqual(ClientCallableFault.MissingAuth, ClientCallableErrors.FromDetailsCode("UNAUTHENTICATED"));
            Assert.AreEqual("errors.retry_later", ClientCallableErrors.MessageKey("RETRY_LATER"));
            Assert.IsTrue(ClientCallableErrors.MayRetryRead(ClientCallableFault.RetryableRead));
        }

        [Test]
        public void ManagementScene_KeepsOneEventSystem()
        {
            var scene = SceneManager.GetActiveScene();
            var count = 0;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                count += roots[i].GetComponentsInChildren<EventSystem>(true).Length;
            Assert.LessOrEqual(count, 1);
        }

        static ClientBackendStatus ReadyStatus()
        {
            return new ClientBackendStatus(
                ClientBackendEnvironment.LocalEmulator,
                ClientConnectionDecision.ConnectToEmulator,
                ClientBackendPhase.Ready,
                "");
        }

        static Dictionary<string, object> Envelope(Dictionary<string, object> result)
        {
            return Map("protocolVersion", 1, "serverTime", "2026-10-16T12:00:00.000Z", "result", result);
        }

        static Dictionary<string, object> Map(params object[] pairs)
        {
            var map = new Dictionary<string, object>();
            for (var i = 0; i < pairs.Length; i += 2)
                map[(string)pairs[i]] = pairs[i + 1];
            return map;
        }

        static string Sha(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var text = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                    text.Append(hash[i].ToString("x2"));
                return text.ToString();
            }
        }
    }
}
