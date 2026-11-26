using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogistiCore.EditorTools
{
    /// <summary>
    /// Imports externally-created, reviewed PNGs as real Unity Sprites.
    /// Editor-only review scene does not affect owned depots, save state or
    /// any shipping scene. Binary art must be added separately with matching
    /// filenames; missing assets fail closed rather than making placeholders.
    /// </summary>
    public static class DepotArt02Import
    {
        public const string Folder =
            "Assets/Game/Art/Depot/ProductionCandidates";
        public const string ReviewScene =
            "Assets/Game/Scenes/DepotArtReview.unity";
        static readonly string[] Names =
        {
            "warehouse_3bay", "security_booth",
            "fence_straight", "fence_corner",
            "gate_pillar_light", "gate_pillar_camera", "sliding_gate",
            "blank_facility_sign", "direction_arrow", "curb_straight",
            "curb_corner", "planter_shrubs", "planter_grass",
            "bollard_a", "bollard_b", "dock_bumper"
        };

        [MenuItem("LogistiCore/Depot/Art 02/Import 16 Sprite Assets")]
        public static void ImportSprites()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Close Play mode before importing art.");
                return;
            }
            var missing = MissingFiles();
            if (missing.Count > 0)
            {
                Debug.LogError("DEPOT-ART-02: PNG files missing:\n" +
                    string.Join("\n", missing));
                return;
            }
            foreach (var name in Names)
            {
                var path = Folder + "/" + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError("PNG importer missing: " + path);
                    return;
                }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.spritePixelsPerUnit = 100f;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0.04f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            Debug.Log("DEPOT-ART-02: 16 individual sprites imported. " +
                "Review alpha and scale before integrating into a player scene.");
        }

        [MenuItem("LogistiCore/Depot/Art 02/Create Art Review Scene")]
        public static void CreateReviewScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Close Play mode first.");
                return;
            }
            var missing = MissingFiles();
            if (missing.Count > 0)
            {
                Debug.LogError("Import the 16 PNGs first. Missing:\n" +
                    string.Join("\n", missing));
                return;
            }
            foreach (var name in Names)
            {
                if (AssetDatabase.LoadAssetAtPath<Sprite>(
                        Folder + "/" + name + ".png") == null)
                {
                    Debug.LogError("Sprite not imported: " + name +
                        ". Run Import 16 Sprite Assets first.");
                    return;
                }
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ReviewScene) != null &&
                !EditorUtility.DisplayDialog("Review scene exists",
                    "Overwrite the existing DEPOT-ART-02 review scene? " +
                    "Its manually edited layout will be replaced.",
                    "Overwrite", "Cancel"))
                return;

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("DEPOT_ART_02_REVIEW_ONLY");
            var cameraObject = new GameObject("ArtReviewCamera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.10f, 0.14f, 1f);
            camera.allowHDR = false;

            // Pure art-composition scene: these renderers are never added to
            // a verified player/company scene until scale and QA sign-off.
            Place(root.transform, "fence_straight", -3.8f, 2.2f, 0.95f, 0);
            Place(root.transform, "fence_corner", 4.1f, 2.1f, 0.85f, 0);
            Place(root.transform, "warehouse_3bay", -0.7f, 0.45f, 1.08f, 10);
            Place(root.transform, "security_booth", 5.25f, -1.20f, 0.75f, 12);
            Place(root.transform, "sliding_gate", 3.8f, -3.45f, 0.75f, 20);
            Place(root.transform, "gate_pillar_light", 1.2f, -3.0f, 0.47f, 21);
            Place(root.transform, "gate_pillar_camera", 6.7f, -2.85f, 0.47f, 21);
            Place(root.transform, "curb_straight", -3.4f, -3.8f, 0.85f, 17);
            Place(root.transform, "curb_corner", -0.85f, -3.9f, 0.70f, 17);
            Place(root.transform, "direction_arrow", -6.3f, -2.2f, 0.55f, 22);
            Place(root.transform, "blank_facility_sign", -5.0f, 0.3f, 0.67f, 22);
            Place(root.transform, "planter_shrubs", -5.7f, -3.65f, 0.56f, 23);
            Place(root.transform, "planter_grass", -3.2f, -4.2f, 0.48f, 23);
            Place(root.transform, "bollard_a", 0.0f, -3.2f, 0.43f, 24);
            Place(root.transform, "bollard_b", 0.55f, -3.35f, 0.43f, 24);
            Place(root.transform, "dock_bumper", -0.7f, -2.55f, 0.42f, 25);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ReviewScene))
                Debug.LogError("Unable to save art review scene: " + ReviewScene);
            else
                Debug.Log("DEPOT-ART-02: Art review scene created: " +
                    ReviewScene + ". NOT approved for shipping.");
        }

        static void Place(Transform root, string name, float x,
            float y, float scale, int order)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                Folder + "/" + name + ".png");
            if (sprite == null)
                throw new InvalidOperationException("Missing sprite: " + name);
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x, y, 0);
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
        }

        static List<string> MissingFiles()
        {
            var missing = new List<string>();
            foreach (var name in Names)
            {
                var path = Folder + "/" + name + ".png";
                if (!System.IO.File.Exists(path))
                    missing.Add(path);
            }
            return missing;
        }
    }
}
