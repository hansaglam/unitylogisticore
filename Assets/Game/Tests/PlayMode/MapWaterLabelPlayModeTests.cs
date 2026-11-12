using System.Collections;
using LogistiCore.Localization;
using LogistiCore.Map;
using LogistiCore.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LogistiCore.Tests
{
    public sealed class MapWaterLabelPlayModeTests
    {
        [UnityTest]
        public IEnumerator WaterLabels_ReuseAndDoNotBlockTouches()
        {
            yield return SceneManager.LoadSceneAsync(AppNavigation.MapSceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var ready = 0f;
            while (!LocaleService.IsReady && ready < 8f)
            {
                ready += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(LocaleService.IsReady);
            var named = 0f;
            while (string.IsNullOrEmpty(MapWaterLabels.Name(MapWaterCatalog.All[0], "tr")) && named < 8f)
            {
                named += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsFalse(string.IsNullOrEmpty(MapWaterLabels.Name(MapWaterCatalog.All[0], "tr")), "Map water strings did not resolve.");
            var scene = SceneManager.GetActiveScene();
            var layer = MapWaterLabelLayer.Attach(scene);
            Assert.IsNotNull(layer);
            Assert.AreSame(layer, MapWaterLabelLayer.Attach(scene));
            Assert.AreEqual(HideFlags.DontSave, layer.gameObject.hideFlags);

            var shell = MapCommandShell.Attach(scene);
            Assert.IsNotNull(shell);
            Assert.IsTrue(shell.SelectRegion(0));
            yield return null;

            var rig = FindInActiveScene<MapCameraRig>();
            Assert.IsNotNull(rig);
            var camera = rig.GetComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 1920f / 1080f;
            MapFrames.WorldPresentation(out var min, out var max);
            MapViewMath.Fit(min, max, camera.aspect, MapFrames.WorldFocusPadding, out var center, out var size);
            camera.orthographicSize = size;
            camera.transform.position = new Vector3(center.x, center.y, MapCameraRig.CameraZ);

            var count = layer.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "tr", false);
            var placed = new MapWaterLabelLayer.Placed[MapWaterLabels.Capacity];
            var copied = layer.CopyPlaced(placed);
            Assert.AreEqual(count, copied);
            Assert.Greater(copied, 0);
            for (var i = 0; i < copied; i++)
                Assert.IsFalse(placed[i].Raycast, placed[i].Id);

            var texts = layer.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
                Assert.IsFalse(texts[i].raycastTarget, texts[i].name);

            Assert.IsTrue(shell.SelectRegion(1));
            yield return null;
            layer.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "tr", false);
            copied = layer.CopyPlaced(placed);
            Assert.AreSame(layer, MapWaterLabelLayer.Attach(scene));
            Assert.LessOrEqual(copied, MapWaterLabels.Capacity);
        }

        static T FindInActiveScene<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
