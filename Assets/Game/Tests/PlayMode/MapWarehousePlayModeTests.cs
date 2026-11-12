using System.Collections;
using LogistiCore.Map;
using LogistiCore.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LogistiCore.Tests
{
    public sealed class MapWarehousePlayModeTests
    {
        [UnityTest]
        public IEnumerator Warehouses_StayUnconfiguredWithoutFakeOwnership()
        {
            yield return SceneManager.LoadSceneAsync(AppNavigation.MapSceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            MapWarehouseStore.ClearOverride();
            var scene = SceneManager.GetActiveScene();
            var shell = MapCommandShell.Attach(scene);
            Assert.IsNotNull(shell);
            shell.Apply(Screen.width, Screen.height, Screen.safeArea);
            shell.OpenManagement(MapModule.Warehouses);
            yield return null;
            Assert.IsFalse(MapWarehouseStore.HasOverride);
            Assert.AreNotEqual(MapWarehouseStatus.Ready, shell.Management.Warehouses.Status);
            Assert.AreNotEqual(MapWarehouseStatus.Empty, shell.Management.Warehouses.Status);
            Assert.AreEqual(0, shell.Management.Warehouses.ActiveRowCount);
            Assert.IsFalse(shell.Management.Warehouses.FixtureVisible);
            Assert.IsFalse(shell.Management.Warehouses.OffersVisible);
            Assert.AreEqual(1, Count<EventSystem>(scene));
            Assert.AreEqual(1, Count<MapManagementPanel>(scene));
            Assert.IsFalse(shell.TryFocusWarehouseCity());
            Assert.IsFalse(shell.TryFocusWarehouseGps());
        }

        static int Count<T>(Scene scene) where T : Component
        {
            var count = 0;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                count += roots[i].GetComponentsInChildren<T>(true).Length;
            return count;
        }
    }
}
