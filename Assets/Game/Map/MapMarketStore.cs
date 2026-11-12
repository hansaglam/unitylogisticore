using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Market read model. Unity does not call getMyMarketState in this phase.
    /// </summary>
    public static class MapMarketStore
    {
        static bool _hasOverride;
        static MapMarketSnapshot _override;
        static bool _hasTrusted;
        static MapMarketSnapshot _trusted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            ClearOverride();
            ClearTrusted();
        }

        public static bool HasOverride => _hasOverride;

        public static MapMarketSnapshot Current
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

        public static MapMarketSnapshot FromBackend(ClientBackendStatus status)
        {
            switch (status.Phase)
            {
                case ClientBackendPhase.Starting:
                    return MapMarketSnapshot.Loading();
                case ClientBackendPhase.Failed:
                    return MapMarketSnapshot.Error();
                default:
                    return MapMarketSnapshot.Unconfigured();
            }
        }

        public static void Override(MapMarketSnapshot snapshot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!snapshot.Synthetic)
                return;
            _override = snapshot;
            _hasOverride = true;
#endif
        }

        public static bool TryApplyTrusted(MapMarketSnapshot snapshot)
        {
            if (snapshot.Synthetic || snapshot.Status == MapMarketStatus.Unconfigured)
                return false;
            _trusted = snapshot;
            _hasTrusted = true;
            return true;
        }

        public static void Select(string conditionId)
        {
            if (_hasOverride)
            {
                _override = _override.WithSelected(conditionId);
                return;
            }

            if (_hasTrusted)
                _trusted = _trusted.WithSelected(conditionId);
        }

        public static void SetCargoFilter(string cargoId)
        {
            if (_hasOverride)
            {
                _override = _override.WithCargo(cargoId);
                return;
            }

            if (_hasTrusted)
                _trusted = _trusted.WithCargo(cargoId);
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
