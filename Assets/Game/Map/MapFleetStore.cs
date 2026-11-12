using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// One source of truth for Fleet. Unity does not call getMyFleetState in this phase.
    /// Ready/Empty never come from Firebase phase alone.
    /// </summary>
    public static class MapFleetStore
    {
        static bool _hasOverride;
        static MapFleetSnapshot _override;
        static bool _hasTrusted;
        static MapFleetSnapshot _trusted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            ClearOverride();
            ClearTrusted();
        }

        public static bool HasOverride => _hasOverride;

        public static MapFleetSnapshot Current
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

        public static MapFleetSnapshot FromBackend(ClientBackendStatus status)
        {
            switch (status.Phase)
            {
                case ClientBackendPhase.Starting:
                    return MapFleetSnapshot.Loading();
                case ClientBackendPhase.Failed:
                    return MapFleetSnapshot.Error();
                default:
                    return MapFleetSnapshot.Unconfigured();
            }
        }

        public static void Override(MapFleetSnapshot snapshot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!snapshot.Synthetic)
                return;

            _override = snapshot;
            _hasOverride = true;
#endif
        }

        public static bool TryApplyTrusted(MapFleetSnapshot snapshot)
        {
            if (snapshot.Synthetic || snapshot.Status == MapFleetStatus.Unconfigured)
                return false;
            _trusted = snapshot;
            _hasTrusted = true;
            return true;
        }

        public static void Select(string assetId)
        {
            if (_hasOverride)
            {
                _override = _override.WithSelected(assetId);
                return;
            }

            if (_hasTrusted)
                _trusted = _trusted.WithSelected(assetId);
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
