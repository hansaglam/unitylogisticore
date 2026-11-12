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
    public sealed class MapJobsPlayModeTests
    {
        [UnityTest]
        public IEnumerator Jobs_StaysUnconfiguredWithoutFakeContracts()
        {
            yield return SceneManager.LoadSceneAsync(AppNavigation.MapSceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            MapJobsStore.ClearOverride();
            var scene = SceneManager.GetActiveScene();
            var shell = MapCommandShell.Attach(scene);
            Assert.IsNotNull(shell);
            shell.Apply(Screen.width, Screen.height, Screen.safeArea);
            shell.OpenManagement(MapModule.Jobs);
            yield return null;
            Assert.IsFalse(MapJobsStore.HasOverride);
            Assert.AreNotEqual(MapJobsStatus.Ready, shell.Management.Jobs.Status);
            Assert.AreNotEqual(MapJobsStatus.Empty, shell.Management.Jobs.Status);
            Assert.AreEqual(0, shell.Management.Jobs.ActiveRowCount);
            Assert.IsFalse(shell.Management.Jobs.FixtureVisible);
            Assert.AreEqual(1, Count<EventSystem>(scene));
            Assert.AreEqual(1, Count<MapManagementPanel>(scene));
            Assert.IsFalse(shell.TryFocusJobOrigin());
            Assert.IsFalse(shell.TryFocusJobDestination());
            shell.CloseContext();
            shell.OpenJobsFromStrip();
            Assert.IsFalse(shell.ManagementOpen);
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
