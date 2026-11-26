namespace LogistiCore.Depot
{
    /// <summary>
    /// Stable names for editor-exported UI illustrations. These are rendered
    /// from authored geometry, not runtime-owned warehouse state.
    /// </summary>
    public static class DepotIllustrationCatalog
    {
        public const string GeneratedFolder = "Assets/Game/Art/Depot/Generated";
        public const int Width = 1024;
        public const int Height = 768;

        public static readonly DepotVisualStage[] Stages =
        {
            DepotVisualStage.Unverified,
            DepotVisualStage.ReservedParcel,
            DepotVisualStage.ActivationReady,
            DepotVisualStage.StorageOperational
        };

        public static string FileName(DepotVisualStage stage)
        {
            switch (stage)
            {
                case DepotVisualStage.Unverified: return "depot_neutral.png";
                case DepotVisualStage.ReservedParcel: return "depot_reserved.png";
                case DepotVisualStage.ActivationReady: return "depot_prepared.png";
                case DepotVisualStage.StorageOperational: return "depot_operational.png";
                default: return null;
            }
        }
    }
}
