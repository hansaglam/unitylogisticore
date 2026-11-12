using System.Reflection;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LogistiCore.Tests
{
    public sealed class MapCommandShellLifecycleTests
    {
        MapCommandShell _shell;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CommandLifecycleRoot", typeof(RectTransform), typeof(Canvas), typeof(MapCommandShell));
            _shell = _root.GetComponent<MapCommandShell>();
            _shell.Apply(1280f, 720f, new Rect(0f, 0f, 1280f, 720f));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void StaleRegionRows_RebuildOnApply()
        {
            Assert.IsTrue(_shell.RegionUiIntact);
            Assert.AreEqual(8, _shell.AliveRegionRowCount);
            Assert.IsNotNull(_shell.RegionButton);

            _shell.SimulateStaleRegionRows();
            Assert.IsFalse(_shell.RegionUiIntact);
            Assert.AreEqual(0, _shell.AliveRegionRowCount);

            Assert.DoesNotThrow(() => _shell.Apply(1280f, 720f, new Rect(0f, 0f, 1280f, 720f)));
            Assert.IsTrue(_shell.RegionUiIntact);
            Assert.AreEqual(8, _shell.AliveRegionRowCount);
            Assert.IsNotNull(_shell.RegionButton);
            Assert.AreEqual(1, CountNamed(_shell.transform, "RegionChip"));
            Assert.AreEqual(1, CountNamed(_shell.transform, "RegionMenu"));
        }

        [Test]
        public void RebuildRegion_DoesNotDuplicateRows()
        {
            _shell.SimulateStaleRegionRows();
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            _shell.SimulateStaleRegionRows();
            _shell.Apply(844f, 390f, new Rect(0f, 0f, 844f, 390f));
            Assert.AreEqual(1, CountNamed(_shell.transform, "RegionChip"));
            Assert.AreEqual(1, CountNamed(_shell.transform, "RegionMenu"));
            Assert.AreEqual(1, CountNamed(_shell.transform, "Region0"));
            Assert.AreEqual(8, _shell.AliveRegionRowCount);
        }

        [Test]
        public void OnEnableAndLateUpdate_DoNotThrowAfterStaleRows()
        {
            _shell.SimulateStaleRegionRows();
            Assert.DoesNotThrow(() => Invoke(_shell, "OnEnable"));
            Assert.IsTrue(_shell.RegionUiIntact);
            _shell.SimulateStaleRegionRows();
            Assert.DoesNotThrow(() => Invoke(_shell, "LateUpdate"));
            Assert.DoesNotThrow(() => _shell.Apply(1024f, 768f, new Rect(0f, 0f, 1024f, 768f)));
            Assert.IsTrue(_shell.RegionUiIntact);
        }

        static void Invoke(object target, string method)
        {
            var found = target.GetType().GetMethod(
                method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(found, method);
            found.Invoke(target, null);
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
