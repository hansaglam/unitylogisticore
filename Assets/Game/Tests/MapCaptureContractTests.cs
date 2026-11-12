using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogistiCore.Tests
{
    public sealed class MapCaptureContractTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        [Test]
        public void RibbonPass_StaysInTheWorldShader()
        {
            var shader = File.ReadAllText("Assets/Game/Map/MapRibbonLine.shader");
            StringAssert.Contains("ZTest Always", shader);
            StringAssert.Contains("\"Queue\" = \"Transparent\"", shader);
        }

        [Test]
        public void ProductCanvas_IsOverlayBeforeCapture()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var canvas = Find<Canvas>(scene);
                var cameras = 0;
                var roots = scene.GetRootGameObjects();
                for (var i = 0; i < roots.Length; i++)
                    cameras += roots[i].GetComponentsInChildren<Camera>(true).Length;
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                Assert.IsNull(canvas.worldCamera);
                Assert.AreEqual(1, cameras);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        static T Find<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var item = roots[i].GetComponentInChildren<T>(true);
                if (item != null)
                    return item;
            }

            return null;
        }

        static string Sha(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = System.Security.Cryptography.SHA256.Create())
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
