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
    /// <summary>
    /// UNITY-IOS-04A: rail rows stay at 44 logical iOS points. Eight rows do not
    /// fit the 2868×1320 band, so the rail scrolls instead of packing hits.
    /// Editor geometry is not a physical-touch PASS.
    /// </summary>
    public sealed class MapRailTouchTargetTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        [Test]
        public void PhoneRail_HitTargets_StayFortyFourPointsAndScroll()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var settingsBefore = Sha(ProjectSettingsPath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                var phone = new Rect(186f, 60f, 2496f, 1200f);
                const float width = 2868f;
                const float height = 1320f;
                shell.Apply(width, height, phone);
                Canvas.ForceUpdateCanvases();

                var metrics = MapCommandLayout.Evaluate(width, height, phone, true);
                Assert.IsTrue(metrics.FutureModes);
                Assert.AreEqual(8, MapContent.FutureModes.Length);

                var canvasScale = MapHudLayout.CanvasScale(width, height);
                var pointScale = MapCommandLayout.PointScale(width, height);
                var pointTouchScreen = MapCommandLayout.TouchPixels(width, height);
                var rowScreen = MapCommandLayout.RailRowPixels(metrics.FontPixels, width, height);

                Assert.Greater(canvasScale, 1.3f);
                Assert.Less(canvasScale, 1.4f);
                Assert.AreEqual(3f, pointScale, 0.001f);
                Assert.AreEqual(132f, pointTouchScreen, 0.05f);
                Assert.GreaterOrEqual(rowScreen, pointTouchScreen - 0.05f);

                var rowCanvas = rowScreen / canvasScale;
                var rowPoints = rowScreen / pointScale;
                Assert.GreaterOrEqual(rowPoints, MapCommandLayout.MinTouchPixels - 0.01f);

                var rail = Find(shell.transform, "ModuleRail") as RectTransform;
                var viewport = Find(rail, "RailViewport") as RectTransform;
                var content = Find(rail, "RailContent") as RectTransform;
                var scroll = rail.GetComponent<ScrollRect>();
                Assert.IsTrue(scroll.vertical);
                Assert.Greater(content.sizeDelta.y, viewport.sizeDelta.y + 1f);

                var bandScreen = metrics.Rail.height;
                Assert.AreEqual(1012.8f, bandScreen, 1.5f);
                Assert.Less(bandScreen, 8f * pointTouchScreen - 0.01f);

                Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
                Assert.IsNotNull(viewport.GetComponent<RectMask2D>());

                var rows = new RectTransform[8];
                for (var i = 0; i < 8; i++)
                    rows[i] = Find(shell.transform, "Mode" + i) as RectTransform;

                RectTransform previous = null;
                for (var i = 0; i < 8; i++)
                {
                    var row = rows[i];
                    Assert.IsTrue(row.gameObject.activeInHierarchy, "Mode" + i);
                    Assert.IsTrue(row.GetComponent<Button>().targetGraphic.raycastTarget, "Mode" + i);

                    var hitScreen = row.sizeDelta.y * canvasScale;
                    var hitPoints = hitScreen / pointScale;
                    Assert.GreaterOrEqual(hitScreen, pointTouchScreen - 1f, "Mode" + i + " screen");
                    Assert.GreaterOrEqual(hitPoints, MapCommandLayout.MinTouchPixels - 0.35f, "Mode" + i + " points");

                    if (i == 0)
                    {
                        AssertInsideViewport(viewport, row, new Vector2(0.5f, 0.98f), "Mode0 top");
                        AssertInsideViewport(viewport, row, new Vector2(0.5f, 0.5f), "Mode0 center");
                        AssertInsideViewport(viewport, row, new Vector2(0.5f, 0.02f), "Mode0 bottom");
                    }

                    AssertExclusiveSample(rows, i, new Vector2(0.5f, 0.98f), "Mode" + i + " top");
                    AssertExclusiveSample(rows, i, new Vector2(0.5f, 0.5f), "Mode" + i + " center");
                    AssertExclusiveSample(rows, i, new Vector2(0.5f, 0.02f), "Mode" + i + " bottom");

                    if (previous != null)
                    {
                        var previousBottom = rail.InverseTransformPoint(previous.TransformPoint(new Vector3(0f, previous.rect.yMin, 0f))).y;
                        var nextTop = rail.InverseTransformPoint(row.TransformPoint(new Vector3(0f, row.rect.yMax, 0f))).y;
                        Assert.LessOrEqual(nextTop, previousBottom + 0.4f, "Mode" + i + " adjacent");
                    }

                    previous = row;
                }

                TestContext.WriteLine(
                    "rail-hit phone 2868x1320 rowScreen=" + rowScreen.ToString("0.0")
                    + " rowPoints=" + rowPoints.ToString("0.0")
                    + " band=" + bandScreen.ToString("0.0")
                    + " contentCanvas=" + content.sizeDelta.y.ToString("0.0")
                    + " viewportCanvas=" + viewport.sizeDelta.y.ToString("0.0")
                    + " Editor geometry is not a physical-touch PASS.");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
            Assert.AreEqual(settingsBefore, Sha(ProjectSettingsPath));
        }

        static void AssertInsideViewport(RectTransform viewport, RectTransform row, Vector2 normalized, string label)
        {
            var local = new Vector3(
                Mathf.Lerp(row.rect.xMin, row.rect.xMax, normalized.x),
                Mathf.Lerp(row.rect.yMin, row.rect.yMax, normalized.y),
                0f);
            var inViewport = viewport.InverseTransformPoint(row.TransformPoint(local));
            Assert.IsTrue(viewport.rect.Contains(new Vector2(inViewport.x, inViewport.y)), label);
        }

        static void AssertExclusiveSample(RectTransform[] rows, int owner, Vector2 normalized, string label)
        {
            var row = rows[owner];
            var world = row.TransformPoint(new Vector3(
                Mathf.Lerp(row.rect.xMin, row.rect.xMax, normalized.x),
                Mathf.Lerp(row.rect.yMin, row.rect.yMax, normalized.y),
                0f));
            for (var i = 0; i < rows.Length; i++)
            {
                var local = rows[i].InverseTransformPoint(world);
                var inside = rows[i].rect.Contains(new Vector2(local.x, local.y));
                if (i == owner)
                    Assert.IsTrue(inside, label);
                else
                    Assert.IsFalse(inside, label + " leaked into Mode" + i);
            }
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var hit = Find(root.GetChild(i), name);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        static string Sha(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var text = new char[hash.Length * 2];
                for (var i = 0; i < hash.Length; i++)
                {
                    var value = hash[i];
                    text[i * 2] = Hex(value >> 4);
                    text[i * 2 + 1] = Hex(value & 15);
                }

                return new string(text);
            }
        }

        static char Hex(int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + (value - 10));
        }
    }
}
