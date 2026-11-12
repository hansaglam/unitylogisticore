using LogistiCore.EditorTools;
using LogistiCore.Localization;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LogistiCore.Tests
{
    public sealed class MapCommandShellLocalizationTests
    {
        MapCommandShell _shell;
        GameObject _root;
        string _previousLocale;

        [SetUp]
        public void SetUp()
        {
            MapOperationStrip.ClearOverride();
            MapSceneSetup.EnsureLocalization();
            _previousLocale = LocaleService.CurrentCode;
            _root = new GameObject("CommandLocalizationRoot", typeof(RectTransform), typeof(Canvas), typeof(MapCommandShell));
            _shell = _root.GetComponent<MapCommandShell>();
            _shell.Apply(1280f, 720f, new Rect(0f, 0f, 1280f, 720f));
        }

        [TearDown]
        public void TearDown()
        {
            MapOperationStrip.ClearOverride();
            if (_root != null)
                Object.DestroyImmediate(_root);
            _root = null;
            if (!string.IsNullOrEmpty(_previousLocale) && LocaleService.IsReady)
                LocaleService.TrySelect(_previousLocale);
        }

        [Test]
        public void StripPresent_DuringAsyncCompletion_DoesNotWaitOnGetTable()
        {
            MapOperationStrip.Override(MapOperationSnapshot.Loading());
            Assert.DoesNotThrow(() => _shell.PresentCurrentStripForTests());
            Assert.DoesNotThrow(() => _shell.Apply(1280f, 720f, new Rect(0f, 0f, 1280f, 720f)));
            Assert.DoesNotThrow(() => _shell.PresentCurrentStripForTests());
        }

        [Test]
        public void RapidLocaleChanges_TrEnDe_DoNotThrow()
        {
            Assert.IsTrue(LocaleService.IsReady);
            MapOperationStrip.Override(MapOperationSnapshot.Loading());
            Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.Turkish));
            Assert.DoesNotThrow(() => _shell.PresentCurrentStripForTests());
            Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.English));
            Assert.DoesNotThrow(() => _shell.PresentCurrentStripForTests());
            Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.German));
            Assert.DoesNotThrow(() => _shell.PresentCurrentStripForTests());
            Assert.IsTrue(LocaleService.TrySelect(LocaleCodes.Turkish));
            Assert.AreEqual(LocaleCodes.Turkish, LocaleService.CurrentCode);
        }

        [Test]
        public void MissingStripCache_FallsBackEmptyWithoutTableWait()
        {
            MapOperationStrip.Override(MapOperationSnapshot.Error());
            Assert.DoesNotThrow(() => _shell.PresentCurrentStripForTests());
            Assert.IsNotNull(_shell.SummaryText);
        }

        [Test]
        public void ApplyAgain_DoesNotDuplicateBindings()
        {
            var count = _shell.LocalizationBindingCount;
            Assert.Greater(count, 0);
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            _shell.Apply(844f, 390f, new Rect(0f, 0f, 844f, 390f));
            Assert.AreEqual(count, _shell.LocalizationBindingCount);
        }

        [Test]
        public void StaleRegionRebuild_KeepsStripSafe()
        {
            MapOperationStrip.Override(MapOperationSnapshot.Loading());
            _shell.SimulateStaleRegionRows();
            Assert.DoesNotThrow(() => _shell.Apply(1280f, 720f, new Rect(0f, 0f, 1280f, 720f)));
            Assert.DoesNotThrow(() => _shell.PresentCurrentStripForTests());
            Assert.IsTrue(_shell.RegionUiIntact);
        }

        [Test]
        public void DestroyedShell_IgnoresLaterLocaleCallbacks()
        {
            MapOperationStrip.Override(MapOperationSnapshot.Loading());
            Object.DestroyImmediate(_root);
            _root = null;
            Assert.IsTrue(_shell == null);
            Assert.DoesNotThrow(() => LocaleService.TrySelect(LocaleCodes.English));
            Assert.DoesNotThrow(() => LocaleService.TrySelect(LocaleCodes.Turkish));
        }
    }
}
