using System;
using System.Collections.Generic;
using LogistiCore.Backend;
using LogistiCore.Map;
using NUnit.Framework;

namespace LogistiCore.Tests
{
    public sealed class MapJobsContractsReadTests
    {
        const string UidA = "uid-a";
        const string UidB = "uid-b";
        const string CompletedId = "9298bef2f37fca8a0ee1de263803936763390db67afb18b63835ecb1e0e69c54";

        [SetUp]
        public void SetUp()
        {
            FirebaseClient.ResetForTests();
            ClientConnectionAuthorization.ResetForTests();
            ClientJobsRuntime.ResetForTests();
            MapJobsStore.ClearOverride();
            MapJobsContractsRead.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            SetUp();
        }

        [Test]
        public void MissingAuth_DoesNotLoadContracts()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) =>
            {
                Assert.Fail("getMyContracts must not run without a uid.");
                return null;
            });
            MapJobsContractsRead.HandleAuth("", ReadyStatus(), session);
            Assert.AreEqual("", MapJobsContractsRead.Uid);
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
            Assert.AreEqual(0, session.InvokeCount);
        }

        [Test]
        public void CompletedContract_MapsToHistory()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((name, payload) =>
            {
                Assert.AreEqual(ClientCallableCatalog.GetMyContracts, name);
                Assert.AreEqual(1, payload["protocolVersion"]);
                Assert.IsFalse(payload.ContainsKey("status"));
                return Envelope(Page(CompletedIzmirIstanbul()));
            });
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            var snapshot = MapJobsStore.Current;
            Assert.AreEqual(MapJobsStatus.Ready, snapshot.Status);
            Assert.IsFalse(snapshot.Synthetic);
            Assert.AreEqual(1, snapshot.Items.Length);
            Assert.IsTrue(snapshot.TryGet(CompletedId, out var item));
            Assert.AreEqual(MapJobContractStatus.Completed, item.ContractStatus);
            Assert.IsTrue(item.InCategory(MapJobCategory.History));
            Assert.IsFalse(item.InCategory(MapJobCategory.Available));
            Assert.IsFalse(item.InCategory(MapJobCategory.Active));
            Assert.AreEqual(1, snapshot.CountIn(MapJobCategory.History));
            Assert.AreEqual(0, snapshot.CountIn(MapJobCategory.Available));
            Assert.AreEqual("izmir", item.OriginCityId);
            Assert.AreEqual("istanbul", item.DestinationCityId);
            Assert.AreEqual("100000", item.Reward);
            Assert.AreEqual("LGC", item.Currency);
            Assert.AreEqual(2, item.Scale);
            Assert.AreEqual("", item.DeliveryId);
            Assert.AreEqual(1, session.InvokeCount);
        }

        [Test]
        public void OfferedContract_MapsToAvailable()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var expires = "2026-11-07T01:00:00.000Z";
            var session = Session((_, __) => Envelope(Page(OfferedAnkaraBursa(expires))));
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            var snapshot = MapJobsStore.Current;
            Assert.IsTrue(snapshot.TryGet("offer_ankara_bursa", out var item));
            Assert.AreEqual(MapJobContractStatus.Offered, item.ContractStatus);
            Assert.IsTrue(item.InCategory(MapJobCategory.Available));
            Assert.AreEqual(expires, item.ExpiresAt);
            Assert.AreEqual(1, snapshot.CountIn(MapJobCategory.Available));
        }

        [Test]
        public void EmptyPage_IsEmptyNotError()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) => Envelope(Page()));
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            Assert.AreEqual(MapJobsStatus.Empty, MapJobsStore.Current.Status);
            Assert.AreEqual(0, MapJobsStore.Current.Items.Length);
            Assert.IsFalse(MapJobsStore.Current.Synthetic);
        }

        [Test]
        public void ReadFailure_IsErrorNotEmpty()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) => throw new InvalidOperationException("offline"));
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            Assert.AreEqual(MapJobsStatus.Error, MapJobsStore.Current.Status);
            Assert.AreNotEqual(MapJobsStatus.Empty, MapJobsStore.Current.Status);
            Assert.AreEqual(0, MapJobsStore.Current.Items.Length);
        }

        [Test]
        public void InvalidDto_IsNotAppliedAsTrustedJobs()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) => Envelope(Map(
                "items", new object[] { Map("status", "completed", "originCityId", "izmir") },
                "nextCursor", null)));
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            Assert.AreEqual(MapJobsStatus.Error, MapJobsStore.Current.Status);
            Assert.IsFalse(MapJobsStore.Current.TryGet("", out _));
            Assert.AreEqual(0, MapJobsStore.Current.Items.Length);
        }

        [Test]
        public void BeginPlaySession_DropsInFlightResult()
        {
            ClientConnectionAuthorization.ApproveForTests();
            Action<object> pending = null;
            var session = new ClientReadSession();
            session.SetAsyncInvoker((name, payload, ok, fail) => { pending = ok; });
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            Assert.AreEqual(1, session.InvokeCount);
            MapJobsContractsRead.BeginPlaySession();
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
            pending(Envelope(Page(CompletedIzmirIstanbul())));
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
            Assert.IsFalse(MapJobsStore.Current.TryGet(CompletedId, out _));
            session.SetAsyncInvoker((name, payload, ok, fail) => ok(Envelope(Page())));
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            Assert.AreEqual(2, session.InvokeCount);
            Assert.AreEqual(MapJobsStatus.Empty, MapJobsStore.Current.Status);
        }

        [Test]
        public void StaleResponse_AfterUserSwitch_IsDropped()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = new ClientReadSession();
            session.SetInvoker((name, payload) =>
            {
                if (session.InvokeCount == 1)
                {
                    MapJobsContractsRead.HandleAuth(UidB, ReadyStatus(), session);
                    return Envelope(Page(CompletedIzmirIstanbul()));
                }

                return Envelope(Page());
            });
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            Assert.AreEqual(UidB, MapJobsContractsRead.Uid);
            Assert.AreEqual(MapJobsStatus.Empty, MapJobsStore.Current.Status);
            Assert.IsFalse(MapJobsStore.Current.TryGet(CompletedId, out _));
            Assert.AreEqual(2, session.InvokeCount);
        }

        [Test]
        public void SignOut_ClearsTrustedJobs()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) => Envelope(Page(CompletedIzmirIstanbul())));
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            Assert.AreEqual(MapJobsStatus.Ready, MapJobsStore.Current.Status);
            MapJobsContractsRead.HandleAuth("", ReadyStatus(), session);
            Assert.AreEqual("", MapJobsContractsRead.Uid);
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
            Assert.AreEqual(1, session.InvokeCount);
        }

        [Test]
        public void RepeatRefresh_DoesNotIssueParallelReads()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var session = Session((_, __) => Envelope(Page(CompletedIzmirIstanbul())));
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            MapJobsContractsRead.TryRefresh();
            MapJobsContractsRead.TryRefresh();
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            Assert.AreEqual(1, session.InvokeCount);
            Assert.IsFalse(MapJobsContractsRead.InFlight);
        }

        [Test]
        public void CommandsAreNotInvoked()
        {
            ClientConnectionAuthorization.ApproveForTests();
            var names = new List<string>();
            var session = Session((name, payload) =>
            {
                names.Add(name);
                Assert.AreNotEqual(ClientCallableCatalog.AcceptContract, name);
                Assert.AreNotEqual(ClientCallableCatalog.StartDelivery, name);
                Assert.AreNotEqual(ClientCallableCatalog.ClaimDeliveryReward, name);
                Assert.IsFalse(name.IndexOf("generate", StringComparison.OrdinalIgnoreCase) >= 0);
                return Envelope(Page());
            });
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            var command = session.Read(
                ClientCallableCatalog.AcceptContract,
                ReadyStatus(),
                true,
                ClientValue.ReadRequest());
            Assert.AreEqual(ClientCallableFault.NonRetryableCommand, command.Fault);
            Assert.AreEqual(1, names.Count);
            Assert.AreEqual(ClientCallableCatalog.GetMyContracts, names[0]);
        }

        [Test]
        public void UnapprovedConnection_DoesNotCallFunctions()
        {
            var session = Session((_, __) =>
            {
                Assert.Fail("connection hold must not invoke");
                return null;
            });
            MapJobsContractsRead.HandleAuth(UidA, ReadyStatus(), session);
            Assert.AreEqual(MapJobsStatus.Unconfigured, MapJobsStore.Current.Status);
            Assert.AreEqual(0, session.InvokeCount);
        }

        static ClientReadSession Session(ClientReadInvoker invoker)
        {
            var session = new ClientReadSession();
            session.SetInvoker(invoker);
            return session;
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
            return Map("protocolVersion", 1, "serverTime", "2026-11-07T00:19:00.000Z", "result", result);
        }

        static Dictionary<string, object> Page(params Dictionary<string, object>[] items)
        {
            return Map("items", items, "nextCursor", null);
        }

        static Dictionary<string, object> CompletedIzmirIstanbul()
        {
            return Map(
                "contractId", CompletedId,
                "status", "completed",
                "expired", false,
                "originCityId", "izmir",
                "destinationCityId", "istanbul",
                "routeId", "route_izmir_istanbul",
                "cargoId", "lc_dev_general_freight",
                "quantity", "1",
                "unit", "load",
                "reward", "100000",
                "currency", "LGC",
                "scale", 2,
                "expiresAt", "2026-11-07T00:00:00.000Z",
                "acceptedAt", "2026-11-07T00:10:00.000Z");
        }

        static Dictionary<string, object> OfferedAnkaraBursa(string expiresAt)
        {
            return Map(
                "contractId", "offer_ankara_bursa",
                "status", "offered",
                "expired", false,
                "originCityId", "ankara",
                "destinationCityId", "bursa",
                "routeId", "route_ankara_bursa",
                "cargoId", "lc_dev_general_freight",
                "quantity", "1",
                "unit", "load",
                "reward", "50000",
                "currency", "LGC",
                "scale", 2,
                "expiresAt", expiresAt,
                "acceptedAt", "");
        }

        static Dictionary<string, object> Map(params object[] pairs)
        {
            var map = new Dictionary<string, object>();
            for (var i = 0; i < pairs.Length; i += 2)
                map[(string)pairs[i]] = pairs[i + 1];
            return map;
        }
    }
}
