using System.Reflection;
using LogistiCore.EditorTools;
using LogistiCore.Localization;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LogistiCore.Tests
{
    public sealed class MapJobsViewLifecycleTests
    {
        MapJobsView _view;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            MapJobsStore.ClearOverride();
            _root = new GameObject("JobsLifecycleRoot", typeof(RectTransform), typeof(Canvas));
            var root = (RectTransform)_root.transform;
            root.sizeDelta = new Vector2(1280f, 720f);
            _view = MapJobsView.Attach(root);
        }

        [TearDown]
        public void TearDown()
        {
            MapJobsStore.ClearOverride();
            MapJobsStore.ClearTrusted();
            MapJobsAccept.ResetForTests();
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void StaleBuiltFlag_RebuildsMissingCategoryLabels()
        {
            MapJobsStore.Override(MapJobsFixtures.Mixed());
            _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
            Assert.IsTrue(_view.RuntimeUiIntact);
            Assert.AreEqual(3, _view.RuntimeCategoryHostCount);

            _view.SimulateStaleRuntimeUi();
            Assert.IsFalse(_view.RuntimeUiIntact);
            Assert.AreEqual(0, _view.RuntimeCategoryHostCount);

            Assert.DoesNotThrow(() => _view.Refresh());
            Assert.IsTrue(_view.RuntimeUiIntact);
            Assert.AreEqual(3, _view.RuntimeCategoryHostCount);
            Assert.AreEqual(1, CountNamed(_view.transform, "ManagementJobsCat0"));
            Assert.AreEqual(6, _view.ActiveRowCount);
        }

        [Test]
        public void Rebuild_DoesNotDuplicateCategoryHosts()
        {
            MapJobsStore.Override(MapJobsFixtures.Mixed());
            _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
            _view.SimulateStaleRuntimeUi();
            _view.Refresh();
            _view.Refresh();
            _view.SimulateStaleRuntimeUi();
            _view.Refresh();
            Assert.AreEqual(1, CountNamed(_view.transform, "ManagementJobsCat0"));
            Assert.AreEqual(1, CountNamed(_view.transform, "ManagementJobsCat1"));
            Assert.AreEqual(1, CountNamed(_view.transform, "ManagementJobsCat2"));
            Assert.AreEqual(3, _view.RuntimeCategoryHostCount);
        }

        [Test]
        public void OnEnable_RecoversStaleCategoryLabels()
        {
            MapJobsStore.Override(MapJobsFixtures.Mixed());
            _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
            _view.SimulateStaleRuntimeUi();
            typeof(MapJobsView)
                .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_view, null);
            Assert.IsTrue(_view.RuntimeUiIntact);
            Assert.AreEqual(3, _view.RuntimeCategoryHostCount);
        }

        [Test]
        public void LocalizationNotReady_PresentDoesNotThrow()
        {
            MapJobsStore.Override(MapJobsSnapshot.Unconfigured());
            Assert.DoesNotThrow(() => _view.Refresh());
            Assert.DoesNotThrow(() => _view.Place(8f, 40f, 14f, 16f, 44f, 1f, false));
            StringAssert.DoesNotContain("ops.", _view.StatusText);
        }

        [Test]
        public void CategorySwitch_ClearsStaleDetailWithoutDuplicatingHosts()
        {
            MapSceneSetup.EnsureLocalization();
            var item = MapJobsFixtures.Contract(
                "done_izmir",
                MapJobContractStatus.Completed,
                "izmir",
                "istanbul",
                "lc_dev_general_freight_v1",
                false,
                "1000",
                "kg",
                "1000",
                "LGC",
                2);
            MapJobsStore.Override(MapJobsSnapshot.Fixture(
                MapJobsStatus.Ready,
                new[] { item },
                "done_izmir",
                MapJobCategory.History));
            _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
            _view.Select("done_izmir");
            Assert.AreEqual(1, _view.ActiveRowCount);
            Assert.IsTrue(_view.DetailVisible);
            Assert.IsFalse(_view.RowTitleOverlapsMeta(0));
            Assert.IsFalse(_view.DetailBlocksOverlap());
            StringAssert.DoesNotContain("ops.jobs", _view.DetailBody);
            StringAssert.DoesNotContain("lc_dev_general_freight_v1", _view.DetailBody);
            Assert.IsTrue(_view.DetailBody.Contains("10,00 LGC") || _view.DetailBody.Contains("10.00 LGC"), _view.DetailBody);
            Assert.IsTrue(
                _view.DetailBody.Contains("General Freight")
                || _view.DetailBody.Contains("Genel Yük")
                || _view.DetailBody.Contains("Stückgut"),
                _view.DetailBody);

            _view.SetCategory(MapJobCategory.Available);
            Assert.AreEqual("", _view.SelectedId);
            Assert.AreEqual(0, _view.ActiveRowCount);
            Assert.IsFalse(_view.DetailVisible);
            Assert.AreEqual("", _view.DetailBody);
            StringAssert.DoesNotContain("→", _view.StatusText);

            _view.SetCategory(MapJobCategory.Active);
            Assert.AreEqual("", _view.SelectedId);
            Assert.AreEqual(0, _view.ActiveRowCount);
            Assert.IsFalse(_view.DetailVisible);

            _view.SetCategory(MapJobCategory.History);
            Assert.AreEqual("done_izmir", _view.SelectedId);
            Assert.AreEqual(1, _view.ActiveRowCount);
            _view.Select("done_izmir");
            Assert.IsTrue(_view.DetailVisible);
            Assert.IsTrue(_view.DetailCloseVisible);
            _view.CloseDetail();
            Assert.IsFalse(_view.DetailVisible);
            Assert.IsFalse(_view.DetailCloseVisible);
            Assert.AreEqual("", _view.DetailBody);
            Assert.AreEqual(1, _view.ActiveRowCount);
            Assert.IsFalse(_view.OriginActive);
            _view.Refresh();
            Assert.IsFalse(_view.DetailVisible);
            _view.Select("done_izmir");
            Assert.IsTrue(_view.DetailVisible);
            _view.SetCategory(MapJobCategory.History);
            Assert.AreEqual(1, CountNamed(_view.transform, "ManagementJobsCat0"));
            Assert.AreEqual(1, CountNamed(_view.transform, "ManagementJobsDetail"));
            Assert.AreEqual(1, _view.ActiveRowCount);
        }

        [Test]
        public void CargoLookup_TrEnDeAndUnknownFallback()
        {
            MapSceneSetup.EnsureLocalization();
            var previous = LocaleService.CurrentCode;
            try
            {
                var known = MapJobsFixtures.Contract(
                    "done_izmir",
                    MapJobContractStatus.Completed,
                    "izmir",
                    "istanbul",
                    "lc_dev_general_freight_v1",
                    false,
                    "1000",
                    "kg",
                    "1000",
                    "LGC",
                    2);
                MapJobsStore.Override(MapJobsSnapshot.Fixture(
                    MapJobsStatus.Ready,
                    new[] { known },
                    "done_izmir",
                    MapJobCategory.History));
                _view.Place(8f, 48f, 16f, 22f, 44f, 1f, true);
                _view.Select("done_izmir");

                Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.Turkish));
                _view.Refresh();
                StringAssert.Contains("Genel Yük", _view.DetailBody);
                StringAssert.Contains("1.000 kg", _view.DetailBody);
                StringAssert.Contains("10,00 LGC", _view.DetailBody);
                StringAssert.DoesNotContain("ops.jobs", _view.DetailBody);
                StringAssert.DoesNotContain("lc_dev_general_freight_v1", _view.RowMetaText(0));

                Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.English));
                _view.Refresh();
                StringAssert.Contains("General Freight", _view.DetailBody);
                StringAssert.Contains("1,000 kg", _view.DetailBody);
                StringAssert.Contains("10.00 LGC", _view.DetailBody);

                Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.German));
                _view.Refresh();
                StringAssert.Contains("Stückgut", _view.DetailBody);
                Assert.IsFalse(_view.DetailBlocksOverlap());
                Assert.IsFalse(_view.RowTitleOverlapsMeta(0));

                var unknown = MapJobsFixtures.Contract(
                    "done_mystery",
                    MapJobContractStatus.Completed,
                    "izmir",
                    "istanbul",
                    "lc_unmapped_cargo_x",
                    false,
                    "2",
                    "t",
                    "1000",
                    "LGC",
                    2);
                MapJobsStore.Override(MapJobsSnapshot.Fixture(
                    MapJobsStatus.Ready,
                    new[] { unknown },
                    "done_mystery",
                    MapJobCategory.History));
                _view.Select("done_mystery");
                StringAssert.Contains("Unbekannte Fracht", _view.DetailBody);
                StringAssert.DoesNotContain("lc_unmapped_cargo_x", _view.DetailBody);
                StringAssert.DoesNotContain("ops.jobs", _view.DetailBody);
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous))
                    LocaleService.TrySelect(previous);
            }
        }

        [Test]
        public void TrustedOffered_ShowsAccept_CompletedAndExpiredDoNot()
        {
            MapSceneSetup.EnsureLocalization();
            var previous = LocaleService.CurrentCode;
            try
            {
                var offered = MapJobsFixtures.Contract(
                    "offer_ankara_bursa",
                    MapJobContractStatus.Offered,
                    "ankara",
                    "bursa",
                    "lc_dev_general_freight_v1",
                    false,
                    "1000",
                    "kg",
                    "1000",
                    "LGC",
                    2,
                    "",
                    MapJobDeliveryStatus.None,
                    "",
                    "",
                    "2099-01-01T00:00:00.000Z");
                Assert.IsTrue(MapJobsStore.TryApplyTrusted(new MapJobsSnapshot(
                    MapJobsStatus.Ready,
                    new[] { offered },
                    "offer_ankara_bursa",
                    MapJobCategory.Available,
                    false)));
                _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
                _view.Select("offer_ankara_bursa");
                Assert.IsTrue(_view.AcceptVisible);
                Assert.IsFalse(_view.AcceptInteractable);

                Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.Turkish));
                _view.Refresh();
                Assert.AreEqual("Kabul Et", _view.AcceptText);
                Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.English));
                _view.Refresh();
                Assert.AreEqual("Accept", _view.AcceptText);
                Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.German));
                _view.Refresh();
                Assert.AreEqual("Annehmen", _view.AcceptText);

                var completed = MapJobsFixtures.Contract(
                    "9298bef2f37fca8a0ee1de263803936763390db67afb18b63835ecb1e0e69c54",
                    MapJobContractStatus.Completed,
                    "izmir",
                    "istanbul",
                    "lc_dev_general_freight_v1",
                    false,
                    "1000",
                    "kg",
                    "1000",
                    "LGC",
                    2);
                MapJobsStore.ClearTrusted();
                Assert.IsTrue(MapJobsStore.TryApplyTrusted(new MapJobsSnapshot(
                    MapJobsStatus.Ready,
                    new[] { completed },
                    completed.ContractId,
                    MapJobCategory.History,
                    false)));
                _view.SetCategory(MapJobCategory.History);
                _view.Select(completed.ContractId);
                Assert.IsTrue(_view.DetailVisible);
                Assert.IsFalse(_view.AcceptVisible);

                var expired = MapJobsFixtures.Contract(
                    "offer_ankara_bursa",
                    MapJobContractStatus.Offered,
                    "ankara",
                    "bursa",
                    "lc_dev_general_freight_v1",
                    true,
                    "1000",
                    "kg",
                    "1000",
                    "LGC",
                    2,
                    "",
                    MapJobDeliveryStatus.None,
                    "",
                    "",
                    "2020-01-01T00:00:00.000Z");
                MapJobsStore.ClearTrusted();
                Assert.IsTrue(MapJobsStore.TryApplyTrusted(new MapJobsSnapshot(
                    MapJobsStatus.Ready,
                    new[] { expired },
                    "offer_ankara_bursa",
                    MapJobCategory.History,
                    false)));
                _view.Select("offer_ankara_bursa");
                Assert.IsFalse(_view.AcceptVisible);
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous))
                    LocaleService.TrySelect(previous);
            }
        }

        [Test]
        public void LocalizationReady_WritesOpsCopy()
        {
            MapSceneSetup.EnsureLocalization();
            Assert.IsTrue(LocaleService.IsReady);
            MapJobsStore.Override(MapJobsFixtures.Mixed());
            _view.SetCategory(MapJobCategory.History);
            _view.Place(8f, 40f, 14f, 16f, 44f, 1f, true);
            Assert.AreEqual(MapJobCategory.History, _view.Category);
            Assert.AreEqual(2, _view.ActiveRowCount);
            Assert.IsFalse(string.IsNullOrWhiteSpace(_view.StatusText));
            Assert.AreNotEqual(OpsContent.BodyJobs, _view.StatusText);
        }

        static int CountNamed(Transform root, string name)
        {
            var count = 0;
            var items = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].name == name)
                    count++;
            }

            return count;
        }
    }
}
