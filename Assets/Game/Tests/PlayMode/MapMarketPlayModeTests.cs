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
    public sealed class MapMarketPlayModeTests
    {
        [UnityTest]
        public IEnumerator Market_StayUnconfiguredWithoutFakePrices()
        {
            yield return SceneManager.LoadSceneAsync(AppNavigation.MapSceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            MapMarketStore.ClearOverride();
            var scene = SceneManager.GetActiveScene();
            var shell = MapCommandShell.Attach(scene);
            Assert.IsNotNull(shell);
            shell.Apply(Screen.width, Screen.height, Screen.safeArea);
            shell.OpenManagement(MapModule.Market);
            yield return null;
            Assert.IsFalse(MapMarketStore.HasOverride);
            Assert.AreNotEqual(MapMarketStatus.Ready, shell.Management.Market.Status);
            Assert.AreNotEqual(MapMarketStatus.Empty, shell.Management.Market.Status);
            Assert.AreEqual(0, shell.Management.Market.ActiveRowCount);
            Assert.IsFalse(shell.Management.Market.FixtureVisible);
            Assert.IsFalse(shell.Management.Market.PriceVisible);
            Assert.AreEqual(1, Count<EventSystem>(scene));
            Assert.AreEqual(1, Count<MapManagementPanel>(scene));
            Assert.IsFalse(shell.TryFocusMarketCity());
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
