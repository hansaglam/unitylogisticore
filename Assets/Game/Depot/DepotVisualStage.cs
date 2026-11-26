using LogistiCore.Backend;

namespace LogistiCore.Depot
{
    /// <summary>
    /// Visual state is mapped only from a trusted depot readiness response.
    /// Geography alone never makes an owned building appear. No storage
    /// occupancy, truck position or delivery animation is invented.
    /// </summary>
    public enum DepotVisualStage : byte
    {
        Unverified = 0,
        ReservedParcel = 1,
        ActivationReady = 2,
        StorageOperational = 3
    }

    public static class DepotVisualStagePolicy
    {
        public static DepotVisualStage Resolve(bool verified,
            ClientStarterDepotProgressState company)
        {
            if (!verified || !company.HasCompany || !company.HasHeadquarters ||
                !company.HasStarterSite || company.DispatchReady)
                return DepotVisualStage.Unverified;

            if (company.WarehouseReady &&
                company.Stage == "warehouse_operational_dispatch_pending_routes" &&
                company.CapacityUnits == 24 &&
                company.CapacityModelId == "pallet_slots_v1")
                return DepotVisualStage.StorageOperational;

            if (company.CanRequestActivation && !company.WarehouseReady)
                return DepotVisualStage.ActivationReady;

            return DepotVisualStage.ReservedParcel;
        }
    }
}
