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
    public sealed class MapManagementPlayModeTests
    {
        [UnityTest]
        public IEnumerator MapModules_TwentyCyclesKeepOneEventSystemAndTheMapRoot()
        {
            yield return SceneManager.LoadSceneAsync(AppNavigation.MapSceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            var scene = SceneManager.GetActiveScene();
            var shell = MapCommandShell.Attach(scene);
            Assert.IsNotNull(shell);
            shell.Apply(Screen.width, Screen.height, Screen.safeArea);
            var map = GameObject.Find("Map");
            Assert.IsNotNull(map);
            var mapId = map.GetInstanceID();
            var rig = Object.FindFirstObjectByType<MapCameraRig>();
            Assert.IsNotNull(rig);
            var camera = rig.GetComponent<Camera>();
            yield return null;
            var pos = camera.transform.position;
            var size = camera.orthographicSize;

            for (var i = 0; i < 20; i++)
            {
                shell.OpenManagement(MapModule.Fleet);
                yield return null;
                Assert.IsTrue(shell.ManagementOpen);
                shell.OpenManagement(MapModule.Jobs);
                yield return null;
                shell.SetManagementExpanded(true);
                yield return null;
                shell.SetManagementExpanded(false);
                shell.OpenManagement(MapModule.Warehouses);
                shell.OpenManagement(MapModule.Market);
                shell.CloseContext();
                yield return null;
                Assert.IsFalse(shell.ManagementOpen);
                Assert.AreEqual(1, Count<EventSystem>(scene));
                Assert.AreEqual(1, Count<MapManagementPanel>(scene));
                Assert.AreEqual(mapId, GameObject.Find("Map").GetInstanceID());
            }

            Assert.AreEqual(pos.x, camera.transform.position.x, 0.05f);
            Assert.AreEqual(size, camera.orthographicSize, 0.05f);
            Assert.AreEqual(1, Count<EventSystem>(scene));
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
