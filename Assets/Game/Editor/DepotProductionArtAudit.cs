using System.Collections.Generic;
using LogistiCore.Depot;
using UnityEditor;
using UnityEngine;

namespace LogistiCore.EditorTools
{
    /// <summary>
    /// Read-only audit of the local imported depot sprites. Checks mechanical
    /// requirements, not subjective commercial art approval. No scene edits,
    /// importer mutation, reimports, generated PNGs or file deletions.
    /// </summary>
    public static class DepotProductionArtAudit
    {
        const string Folder =
            "Assets/Game/Art/Depot/ProductionCandidates";

        static readonly string[] Required =
        {
            "warehouse_3bay", "security_booth",
            "fence_straight", "fence_corner",
            "gate_pillar_light", "gate_pillar_camera",
            "sliding_gate", "blank_facility_sign", "direction_arrow",
            "curb_straight", "curb_corner", "planter_shrubs",
            "planter_grass", "bollard_a", "bollard_b", "dock_bumper"
        };

        [MenuItem("LogistiCore/Depot/Art 02/Audit Sprite Production Readiness")]
        public static void Audit()
        {
            var failed = new List<string>();
            foreach (var name in Required)
            {
                var path = Folder + "/" + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (importer == null || sprite == null)
                {
                    failed.Add(name + ": missing PNG or Sprite import");
                    continue;
                }

                if (importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single)
                    failed.Add(name + ": must import as a single sprite");
                if (!importer.alphaIsTransparency)
                    failed.Add(name + ": alpha transparency not enabled");
                if (importer.mipmapEnabled)
                    failed.Add(name + ": mipmaps must be off for review");
                if (importer.spritePixelsPerUnit != 100f)
                    failed.Add(name + ": unexpected pixels-per-unit");
                if (sprite.rect.width <= 0f || sprite.rect.height <= 0f)
                    failed.Add(name + ": invalid sprite dimensions");
            }

            // The original gate/fence images were explicitly rejected for
            // final assembly in ART-02C. Passing PNG metadata alone must
            // NEVER grant shipping readiness; separate authored components
            // and a calibrated Unity capture are still required.
            var separateGateLeaf =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    Folder + "/sliding_gate_leaf_v2.png") != null;
            var calibratedFence =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    Folder + "/fence_straight_v2.png") != null &&
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    Folder + "/fence_corner_v2.png") != null;
            if (!separateGateLeaf)
                failed.Add("ART-02D: independently movable sliding gate leaf v2 missing");
            if (!calibratedFence)
                failed.Add("ART-02D: calibrated straight/corner fence v2 missing");

            if (failed.Count == 0)
                Debug.Log("[DepotArtAudit] Mechanical import checks passed. " +
                    "Visual and device art approval is STILL REQUIRED.");
            else
                Debug.LogWarning("[DepotArtAudit] NOT PRODUCTION-READY. " +
                    failed.Count + " blocking item(s):\n" +
                    string.Join("\n", failed));
        }
    }
}
