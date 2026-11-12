using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Warehouse read model. Unity does not call getMyWarehouseState in this phase.
    /// </summary>
    public static class MapWarehouseStore
    {
        static bool _hasOverride;
        static MapWarehouseSnapshot _override;
        static bool _hasTrusted;
        static MapWarehouseSnapshot _trusted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            ClearOverride();
            ClearTrusted();
        }

        public static bool HasOverride => _hasOverride;

        public static MapWarehouseSnapshot Current
        {
            get
            {
                if (_hasOverride)
                    return _override;
                if (_hasTrusted)
                    return _trusted;
                return FromBackend(FirebaseClient.Current);
            }
        }

        public static MapWarehouseSnapshot FromBackend(ClientBackendStatus status)
        {
            switch (status.Phase)
            {
                case ClientBackendPhase.Starting:
                    return MapWarehouseSnapshot.Loading();
                case ClientBackendPhase.Failed:
                    return MapWarehouseSnapshot.Error();
                default:
                    return MapWarehouseSnapshot.Unconfigured();
            }
        }

        public static void Override(MapWarehouseSnapshot snapshot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!snapshot.Synthetic)
                return;
            _override = snapshot;
            _hasOverride = true;
#endif
        }

        public static bool TryApplyTrusted(MapWarehouseSnapshot snapshot)
        {
            if (snapshot.Synthetic || snapshot.Status == MapWarehouseStatus.Unconfigured)
                return false;
            _trusted = snapshot;
            _hasTrusted = true;
            return true;
        }

        public static void Select(string warehouseId)
        {
            if (_hasOverride)
            {
                _override = _override.WithSelected(warehouseId);
                return;
            }

            if (_hasTrusted)
                _trusted = _trusted.WithSelected(warehouseId);
        }

        public static void ClearOverride()
        {
            _hasOverride = false;
            _override = default;
        }

        public static void ClearTrusted()
        {
            _hasTrusted = false;
            _trusted = default;
        }
    }
}
