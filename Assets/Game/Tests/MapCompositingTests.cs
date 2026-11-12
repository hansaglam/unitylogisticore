using System.IO;
using System.Security.Cryptography;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.Tests
{
    public sealed class MapCompositingTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        [Test]
        public void OverlayCanvas_KeepsCoastlinesBehindTheHud()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var canvas = FindInScene<Canvas>(scene);
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                Assert.IsNull(canvas.worldCamera);

                var coast = GameObject.Find("Coastlines").GetComponent<Renderer>();
                var turkey = GameObject.Find("TurkeyCoast").GetComponent<Renderer>();
                Assert.Greater(coast.sortingOrder, canvas.sortingOrder);
                Assert.Greater(turkey.sortingOrder, canvas.sortingOrder);

                var shell = MapCommandShell.Attach(scene);
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                Assert.AreEqual(MapCommandShell.PanelState.Closed, shell.State);
                Assert.IsFalse(shell.ContextOpen);
                Assert.IsFalse(shell.CloseButton.gameObject.activeInHierarchy);
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                Assert.IsNull(canvas.worldCamera);
                Assert.AreEqual(1, Count<Camera>(scene));
                Assert.AreEqual(1, Count<EventSystem>(scene));
                Assert.IsNull(shell.HomeButton);
                Assert.IsNotNull(shell.RegionButton);

                shell.Apply(2868f, 1320f, new Rect(186f, 60f, 2496f, 1200f));
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                Assert.IsNull(canvas.worldCamera);
                shell.ShowCity("ankara");
                Assert.IsTrue(shell.ContextOpen);
                Assert.AreEqual(MapCommandShell.PanelState.City, shell.State);
                Assert.IsTrue(shell.CoordinatesVisible);

                var openWidth = shell.Viewport.width;
                shell.CloseContext();
                Assert.AreEqual(MapCommandShell.PanelState.Closed, shell.State);
                Assert.Greater(shell.Viewport.width, openWidth);
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void Labels_StayAboveTheirMarkers_AcrossPanAndZoom()
        {
            var width = Screen.width;
            var height = Screen.height;
            Assert.Greater(width, 200);
            Assert.Greater(height, 200);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var camera = FindInScene<Camera>(scene);
                camera.orthographic = true;
                camera.aspect = width / (float)height;
                MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
                MapViewMath.Fit(turkeyMin, turkeyMax, camera.aspect, MapFrames.TurkeyFocusPadding, out var center, out var size);
                size = MapViewMath.ClampSize(size, MapFrames.MinimumOrthographicSize(camera.aspect), MapFrames.MaximumOrthographicSize(camera.aspect));
                camera.orthographicSize = size;
                camera.transform.position = new Vector3(center.x, center.y, MapCameraRig.CameraZ);

                var layer = FindInScene<MapMarkerLayer>(scene);
                var canvas = FindInScene<Canvas>(scene);
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);

                layer.ApplyViewport(width, height, true);
                var visible = AssertLabelsSitOnMarkers(camera, canvas, width, height);
                Assert.GreaterOrEqual(visible, 4, "visible labels");

                camera.transform.position += new Vector3(size * 0.08f, size * 0.04f, 0f);
                layer.ApplyViewport(width, height, true);
                Assert.GreaterOrEqual(AssertLabelsSitOnMarkers(camera, canvas, width, height), 4);

                var zoomed = Mathf.Max(MapFrames.MinimumOrthographicSize(camera.aspect), camera.orthographicSize * 0.92f);
                camera.orthographicSize = zoomed;
                layer.ApplyViewport(width, height, true);
                Assert.GreaterOrEqual(AssertLabelsSitOnMarkers(camera, canvas, width, height), 4);
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                Assert.AreEqual(8, FindAllInScene<MapCityMarker>(scene).Length);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        static int AssertLabelsSitOnMarkers(Camera camera, Canvas canvas, int width, int height)
        {
            var center = new Vector2(camera.transform.position.x, camera.transform.position.y);
            var units = MapMarkerLod.UnitsPerPixel(camera.orthographicSize, height);
            var scale = MapHudLayout.CanvasScale(width, height);
            var root = canvas.transform.Find("CityLabels");
            Assert.IsNotNull(root);
            var markers = root.GetComponentsInChildren<MapCityMarker>(true);
            if (markers.Length == 0)
                markers = FindAllInScene<MapCityMarker>(canvas.gameObject.scene);
            Assert.AreEqual(8, markers.Length);
            var visible = 0;
            var report = new System.Text.StringBuilder();
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                Assert.IsTrue(MapCities.TryGet(marker.CityId, out _), marker.CityId);
                var plate = root.Find("Plate_" + marker.CityId) as RectTransform;
                Assert.IsNotNull(plate, marker.CityId);
                if (!plate.gameObject.activeInHierarchy)
                    continue;

                visible++;
                var world = marker.transform.position;
                var screen = MapMarkerLod.ToScreen(new Vector2(world.x, world.y), center, units, width, height);
                var text = plate.GetComponentInChildren<Text>(true);
                var half = new Vector2(
                    plate.sizeDelta.x * scale * 0.5f,
                    (text.fontSize + 10f) * scale * 0.5f);
                var visual = RectTransformUtility.WorldToScreenPoint(null, plate.position);
                var matched = false;
                var dx = 0f;
                var dy = 0f;
                for (var side = 0; side < 4; side++)
                {
                    var expected = MapMarkerLayer.PlateAnchor(screen, half, side);
                    dx = visual.x - expected.x;
                    dy = visual.y - expected.y;
                    if (Mathf.Abs(dx) <= 4f && Mathf.Abs(dy) <= 4f)
                    {
                        matched = true;
                        break;
                    }
                }

                report.Append(marker.CityId)
                    .Append(" dx=").Append(dx.ToString("0.0"))
                    .Append(" dy=").Append(dy.ToString("0.0"))
                    .Append(" font=").Append(text.fontSize)
                    .Append('\n');
                if (!matched || Vector2.Distance(visual, screen) > 220f)
                    report.Append("MISS\n");
            }

            if (report.ToString().Contains("MISS"))
                Assert.Fail("scale=" + scale.ToString("0.000") + " canvas=" + canvas.scaleFactor.ToString("0.000") + "\n" + report);
            return visible;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            var found = FindAllInScene<T>(scene);
            Assert.AreEqual(1, found.Length, typeof(T).Name);
            return found[0];
        }

        static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            var list = new System.Collections.Generic.List<T>();
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                list.AddRange(roots[i].GetComponentsInChildren<T>(true));
            return list.ToArray();
        }

        static int Count<T>(Scene scene) where T : Component
        {
            var count = 0;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                count += roots[i].GetComponentsInChildren<T>(true).Length;
            return count;
        }

        static string Sha(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var text = new System.Text.StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                    text.Append(hash[i].ToString("x2"));
                return text.ToString();
            }
        }
    }
}
