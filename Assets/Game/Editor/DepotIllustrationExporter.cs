using System;
using System.IO;
using LogistiCore.Depot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogistiCore.EditorTools
{
    /// <summary>
    /// Explicit editor-only sprite export from the authored depot geometry.
    /// Exported PNGs are visual references/UI illustrations, never evidence of
    /// a player-owned depot. This never touches Home/Company/Map or Build Profiles.
    /// </summary>
    public static class DepotIllustrationExporter
    {
        [MenuItem("LogistiCore/Depot/Export Authored Illustration Sprites")]
        public static void Export()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[LogistiCore] Exit Play mode before exporting depot art.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Depot illustration export",
                "Render four transparent stage illustrations from the authored depot mesh into " +
                DepotIllustrationCatalog.GeneratedFolder +
                "? Files in that folder with matching names will be replaced.",
                "Export", "Cancel"))
                return;

            var folder = DepotIllustrationCatalog.GeneratedFolder;
            Directory.CreateDirectory(folder);

            // A separate preview scene avoids capturing the user's working
            // scene, serialized GameObjects or unrelated overlays.
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var cameraObject = new GameObject("DepotArtExportCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 6.6f;
                camera.transform.position = new Vector3(0f, -1.4f, -12f);
                camera.transform.rotation = Quaternion.identity;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;

                var yardObject = new GameObject("DepotAuthoredExport",
                    typeof(MeshFilter), typeof(MeshRenderer),
                    typeof(DepotYardBlockoutView));
                SceneManager.MoveGameObjectToScene(yardObject, preview);
                var yard = yardObject.GetComponent<DepotYardBlockoutView>();

                foreach (var stage in DepotIllustrationCatalog.Stages)
                {
                    var name = DepotIllustrationCatalog.FileName(stage);
                    if (string.IsNullOrEmpty(name))
                        throw new InvalidOperationException("Invalid depot illustration stage.");

                    yard.SetStage(stage);
                    var bytes = RenderPng(camera);
                    File.WriteAllBytes(Path.Combine(folder, name), bytes);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var stage in DepotIllustrationCatalog.Stages)
            {
                var path = folder + "/" + DepotIllustrationCatalog.FileName(stage);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new IOException("PNG was written but could not be imported: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }

            Debug.Log("[LogistiCore] Exported four authored depot illustration PNGs to " +
                folder + ". These are prototype UI art, not final shipping sprites.");
        }

        static byte[] RenderPng(Camera camera)
        {
            var previous = RenderTexture.active;
            var target = new RenderTexture(DepotIllustrationCatalog.Width,
                DepotIllustrationCatalog.Height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(DepotIllustrationCatalog.Width,
                DepotIllustrationCatalog.Height, TextureFormat.RGBA32, false, false);
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply(false, false);
                return image.EncodeToPNG();
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
