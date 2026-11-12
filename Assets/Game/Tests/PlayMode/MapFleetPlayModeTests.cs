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
    public sealed class MapFleetPlayModeTests
    {
        [UnityTest]
        public IEnumerator Fleet_StaysUnconfiguredWithoutFakeVehicles()
        {
            yield return SceneManager.LoadSceneAsync(AppNavigation.MapSceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            MapFleetStore.ClearOverride();
            var scene = SceneManager.GetActiveScene();
            var shell = MapCommandShell.Attach(scene);
            Assert.IsNotNull(shell);
            shell.Apply(Screen.width, Screen.height, Screen.safeArea);
            shell.OpenManagement(MapModule.Fleet);
            yield return null;
            Assert.IsFalse(MapFleetStore.HasOverride);
            Assert.AreNotEqual(MapFleetStatus.Ready, shell.Management.Fleet.Status);
            Assert.AreNotEqual(MapFleetStatus.Empty, shell.Management.Fleet.Status);
            Assert.AreEqual(0, shell.Management.Fleet.ActiveRowCount);
            Assert.IsFalse(shell.Management.Fleet.FocusActive);
            Assert.AreEqual(1, Count<EventSystem>(scene));
            Assert.AreEqual(1, Count<MapManagementPanel>(scene));
            Assert.IsFalse(shell.TryFocusFleetLocation());
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
