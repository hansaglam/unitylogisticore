using System.IO;
using LogistiCore.Depot;
using LogistiCore.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace LogistiCore.EditorTools
{
    /// <summary>
    /// Creates a separate, non-shipping 2.5D depot blockout for scene review.
    /// This NEVER edits Home/Company/Map, build profiles or player data.
    /// Any existing depot scene is opened, not overwritten.
    /// </summary>
    public static class DepotPrototypeSceneSetup
    {
        public const string ScenePath = "Assets/Game/Scenes/DepotPrototype.unity";

        [MenuItem("LogistiCore/Depot/Create or Open Prototype")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[LogistiCore] Exit Play mode before opening the depot prototype.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            var cameraObject = new GameObject("DepotPrototypeCamera",
                typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, -1.4f, -12f);
            cameraObject.transform.rotation = Quaternion.identity;
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = MapPremiumTheme.Background;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            var yard = new GameObject("AuthoredDepotBlockout_EditorOnly",
                typeof(MeshFilter), typeof(MeshRenderer),
                typeof(DepotYardBlockoutView), typeof(DepotPrototypeController));
            yard.transform.position = Vector3.zero;

            // Standard Unity Input System UI event source, no custom pointer
            // capture that conflicts with existing game navigation.
            var events = new GameObject("DepotEventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));

            if (!Directory.Exists("Assets/Game/Scenes"))
                Directory.CreateDirectory("Assets/Game/Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Unable to save the depot prototype scene.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = yard;
            Debug.Log("[LogistiCore] Opened non-shipping depot prototype: " + ScenePath +
                ". Play Mode uses authenticated emulator data; no owned site is shown offline.");
        }
    }
}
