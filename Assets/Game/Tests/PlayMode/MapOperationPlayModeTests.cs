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
    public sealed class MapOperationPlayModeTests
    {
        [UnityTest]
        public IEnumerator Footer_StaysUnconfiguredWithoutFakeOperations()
        {
            yield return SceneManager.LoadSceneAsync(AppNavigation.MapSceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            MapOperationStrip.ClearOverride();
            var scene = SceneManager.GetActiveScene();
            var shell = MapCommandShell.Attach(scene);
            Assert.IsNotNull(shell);
            shell.Apply(Screen.width, Screen.height, Screen.safeArea);
            yield return null;
            Assert.IsFalse(MapOperationStrip.HasOverride);
            Assert.AreNotEqual(MapOperationStatus.Idle, shell.StripStatus);
            Assert.AreNotEqual(MapOperationStatus.SingleActive, shell.StripStatus);
            Assert.AreNotEqual(MapOperationStatus.MultipleActive, shell.StripStatus);
            Assert.IsFalse(shell.StripActionEnabled);
            Assert.IsFalse(MapOperationStrip.Current.Verified);
            Assert.IsFalse(MapOperationStrip.Current.TrustedRead);
            Assert.IsFalse(string.IsNullOrEmpty(shell.SummaryText));
            for (var i = 0; i < shell.SummaryText.Length; i++)
                Assert.IsFalse(char.IsDigit(shell.SummaryText[i]), shell.SummaryText);
            Assert.AreEqual("Logisti", Find(shell.transform, "Brand").GetComponent<UnityEngine.UI.Text>().text);
            Assert.AreEqual(1, Count<EventSystem>(scene));
            Assert.AreEqual(1, Count<MapCommandShell>(scene));
            shell.OpenJobsFromStrip();
            Assert.IsFalse(shell.ManagementOpen);
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
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
