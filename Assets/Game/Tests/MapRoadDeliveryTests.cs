using LogistiCore.Backend;
using LogistiCore.EditorTools;
using LogistiCore.Localization;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor.Localization;

namespace LogistiCore.Tests
{
    public sealed class MapRoadDeliveryTests
    {
        [TearDown]
        public void Clear()
        {
            MapRoadDeliveryStore.ClearOverride();
        }

        [Test]
        public void FromBackend_StaysUnconfiguredWithoutFakeDeliveries()
        {
            Assert.AreEqual(MapRoadDeliveryStatus.Unconfigured, MapRoadDeliveryStore.FromBackend(Phase(ClientBackendPhase.Idle)).Status);
            Assert.AreEqual(MapRoadDeliveryStatus.Unconfigured, MapRoadDeliveryStore.FromBackend(Phase(ClientBackendPhase.Ready)).Status);
            Assert.AreEqual(MapRoadDeliveryStatus.Loading, MapRoadDeliveryStore.FromBackend(Phase(ClientBackendPhase.Starting)).Status);
            Assert.AreEqual(MapRoadDeliveryStatus.Error, MapRoadDeliveryStore.FromBackend(Phase(ClientBackendPhase.Failed)).Status);
            Assert.AreEqual(0, MapRoadDeliveryStore.FromBackend(Phase(ClientBackendPhase.Ready)).Items.Length);
            MapRoadDeliveryStore.Override(MapRoadDeliverySnapshot.Unconfigured());
            Assert.IsFalse(MapRoadDeliveryStore.HasOverride);
            Assert.IsFalse(MapRoadDeliveryStore.Current.Synthetic);
            Assert.AreNotEqual(MapRoadDeliveryStatus.Ready, MapRoadDeliveryStore.Current.Status);
            Assert.AreNotEqual(MapRoadDeliveryStatus.Empty, MapRoadDeliveryStore.Current.Status);
        }

        [Test]
        public void Snapshot_KeepsOnlyActiveUniqueDeliveries()
        {
            var many = MapRoadFixtures.Many();
            Assert.AreEqual(MapRoadDeliveryStatus.Ready, many.Status);
            Assert.AreEqual(3, many.Items.Length);
            Assert.IsTrue(many.TryGet("d1", out _));
            Assert.IsTrue(many.TryGet("d2", out _));
            Assert.IsTrue(many.TryGet("d4", out var missing));
            Assert.IsFalse(many.TryGet("d3", out _));
            Assert.IsFalse(many.TryGet("d5", out _));
            Assert.IsFalse(missing.HasTruck);
            Assert.IsFalse(missing.HasArrival);
            Assert.IsFalse(missing.HasDestinationCity);
            Assert.AreEqual(MapRoadDeliveryStatus.Empty, MapRoadFixtures.Empty().Status);
            Assert.AreEqual(0, MapRoadFixtures.Empty().Items.Length);
            Assert.AreEqual(MapRoadLifecycle.Unknown, MapRoadDeliveryItem.Parse("paused"));
            Assert.AreEqual("starter_truck", MapRoadDeliveryItem.TruckFromAssets(new[] { "driver", "truck" }, new[] { "starter_driver", "starter_truck" }));
            Assert.AreEqual("", MapRoadDeliveryItem.TruckFromAssets(new[] { "driver" }, new[] { "starter_driver" }));
        }

        [Test]
        public void Empty_IsNotUnconfigured()
        {
            Assert.AreNotEqual(MapRoadFixtures.Empty().Status, MapRoadDeliverySnapshot.Unconfigured().Status);
            Assert.IsTrue(MapRoadFixtures.Empty().Synthetic);
            Assert.IsFalse(MapRoadDeliverySnapshot.Unconfigured().Synthetic);
        }

        [Test]
        public void OpsRoad_HasTrEnDeWithoutInventedCounts()
        {
            MapSceneSetup.EnsureLocalization();
            var collection = LocalizationEditorSettings.GetStringTableCollection(OpsContent.Table);
            Assert.AreEqual("Canlı teslimat bağlantısı yok.", Value(collection, "tr", OpsContent.BodyRoad));
            Assert.AreEqual("No live delivery connection.", Value(collection, "en", OpsContent.BodyRoad));
            Assert.AreEqual("Keine Live-Lieferverbindung.", Value(collection, "de", OpsContent.BodyRoad));
            Assert.AreEqual("Karayolu", Value(collection, "tr", OpsContent.TitleRoad));
            Assert.AreEqual("Aktif karayolu teslimatı yok.", Value(collection, "tr", OpsContent.RoadEmpty));
            foreach (var table in collection.StringTables)
            {
                var empty = table.GetEntry(OpsContent.RoadEmpty).Value;
                Assert.IsFalse(string.IsNullOrWhiteSpace(empty));
                Assert.IsFalse(char.IsDigit(empty[0]));
            }
        }

        static ClientBackendStatus Phase(ClientBackendPhase phase)
        {
            return new ClientBackendStatus(ClientBackendEnvironment.Unconfigured, ClientConnectionDecision.StayOffline, phase, "");
        }

        static string Value(UnityEditor.Localization.StringTableCollection collection, string code, string key)
        {
            foreach (var table in collection.StringTables)
            {
                if (table.LocaleIdentifier.Code == code)
                    return table.GetEntry(key).Value;
            }

            return "";
        }
    }
}
