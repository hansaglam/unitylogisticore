using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Local equipment pick for an accepted contract. Not a startDelivery command.
    /// </summary>
    public static class MapJobsEquipmentSelection
    {
        static string _contractId = "";
        static string _truckId = "";
        static string _driverId = "";
        static string _trailerId = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Clear();
        }

        public static string ContractId => _contractId;
        public static string TruckId => _truckId;
        public static string DriverId => _driverId;
        public static string TrailerId => _trailerId;

        public static void Clear()
        {
            _contractId = "";
            _truckId = "";
            _driverId = "";
            _trailerId = "";
        }

        public static bool CanShow(MapJobsItem item, MapJobsSnapshot snapshot)
        {
            if (snapshot.Status != MapJobsStatus.Ready)
                return false;
            if (item.ContractStatus != MapJobContractStatus.Accepted || item.Expired)
                return false;
            if (item.DeliveryStatus == MapJobDeliveryStatus.Active)
                return false;
            return item.HasStartCandidates;
        }

        public static void Sync(MapJobsItem item, MapJobsSnapshot jobs, MapFleetSnapshot fleet)
        {
            if (!CanShow(item, jobs))
            {
                _contractId = item.ContractId ?? "";
                _truckId = "";
                _driverId = "";
                _trailerId = "";
                return;
            }

            if (_contractId != item.ContractId)
            {
                Clear();
                _contractId = item.ContractId ?? "";
            }

            if (!MapJobsCandidateJoin.TryResolve(item, fleet, out var slots))
            {
                _truckId = "";
                _driverId = "";
                _trailerId = "";
                return;
            }

            KeepOrAuto(ref _driverId, Slot(slots, MapFleetKind.Driver));
            KeepOrAuto(ref _truckId, Slot(slots, MapFleetKind.Truck));
            if (item.RequiresTrailer)
                KeepOrAuto(ref _trailerId, Slot(slots, MapFleetKind.Trailer));
            else
                _trailerId = "";
        }

        public static bool TrySelect(MapJobsItem item, MapFleetSnapshot fleet, MapFleetKind kind, string assetId)
        {
            if (string.IsNullOrEmpty(assetId) || item.ContractStatus != MapJobContractStatus.Accepted)
                return false;
            if (!item.TryCandidate(kind, out var candidate) || candidate.AssetId != assetId)
                return false;
            if (!MapJobsCandidateJoin.TrySlot(item, fleet, kind, out var slot) || !slot.Selectable)
                return false;
            if (_contractId != item.ContractId)
            {
                Clear();
                _contractId = item.ContractId ?? "";
            }

            if (kind == MapFleetKind.Truck)
                _truckId = assetId;
            else if (kind == MapFleetKind.Driver)
                _driverId = assetId;
            else if (kind == MapFleetKind.Trailer)
                _trailerId = assetId;
            else
                return false;
            return true;
        }

        public static bool IsSelected(MapFleetKind kind, string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return false;
            if (kind == MapFleetKind.Truck)
                return _truckId == assetId;
            if (kind == MapFleetKind.Driver)
                return _driverId == assetId;
            if (kind == MapFleetKind.Trailer)
                return _trailerId == assetId;
            return false;
        }

        static void KeepOrAuto(ref string selected, MapJobsCandidateSlot slot)
        {
            if (selected == slot.AssetId && slot.Selectable)
                return;
            selected = slot.Selectable ? slot.AssetId : "";
        }

        static MapJobsCandidateSlot Slot(MapJobsCandidateSlot[] slots, MapFleetKind kind)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].Kind == kind)
                    return slots[i];
            }

            return default;
        }
    }
}
