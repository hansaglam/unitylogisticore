using System.IO;
using LogistiCore.Depot;
using UnityEditor;
using UnityEngine;

namespace LogistiCore.EditorTools
{
    /// <summary>
    /// Opt-in export of the identical deterministic material study used in
    /// the yard mesh. Never overwrites existing files without confirmation.
    /// </summary>
    public static class DepotSurfaceAtlasExporter
    {
        public const string Path = "Assets/Game/Art/Depot/Generated/DepotMaterialStudy01.png";

        [MenuItem("LogistiCore/Depot/Export Surface Material Study")]
        public static void Export()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[LogistiCore] Stop Play mode before exporting art.");
                return;
            }
            var exists = File.Exists(Path);
            if (!EditorUtility.DisplayDialog("Depot material study",
                (exists ? "Replace the existing material study PNG?" :
                    "Export the depot material study PNG?") +
                "\nOutput: " + Path +
                "\nThis is prototype art; review before use in a release.",
                "Export", "Cancel"))
                return;

            var atlas = DepotSurfacePatternAtlas.Create();
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.WriteAllBytes(Path, atlas.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(atlas);
            }
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(Path) as TextureImporter;
            if (importer == null)
                throw new IOException("Failed to import depot material study: " + Path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Debug.Log("[LogistiCore] Material study exported for art review: " + Path);
        }
    }
}
