using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Active truck delivery read model from getMyDeliveries via MapRoadDeliveriesRead.
    /// </summary>
    public static class MapRoadDeliveryStore
    {
        static bool _hasOverride;
        static MapRoadDeliverySnapshot _override;
        static bool _hasTrusted;
        static MapRoadDeliverySnapshot _trusted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            ClearOverride();
            ClearTrusted();
        }

        public static bool HasOverride => _hasOverride;

        public static MapRoadDeliverySnapshot Current
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

        public static MapRoadDeliverySnapshot FromBackend(ClientBackendStatus status)
        {
            switch (status.Phase)
            {
                case ClientBackendPhase.Starting:
                    return MapRoadDeliverySnapshot.Loading();
                case ClientBackendPhase.Failed:
                    return MapRoadDeliverySnapshot.Error();
                default:
                    return MapRoadDeliverySnapshot.Unconfigured();
            }
        }

        public static void Override(MapRoadDeliverySnapshot snapshot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!snapshot.Synthetic)
                return;
            _override = snapshot;
            _hasOverride = true;
#endif
        }

        public static bool TryApplyTrusted(MapRoadDeliverySnapshot snapshot)
        {
            if (snapshot.Synthetic || snapshot.Status == MapRoadDeliveryStatus.Unconfigured)
                return false;
            var selected = _hasTrusted ? _trusted.SelectedId : snapshot.SelectedId;
            _trusted = snapshot.WithSelected(selected);
            _hasTrusted = true;
            return true;
        }

        public static void Select(string deliveryId)
        {
            if (_hasOverride)
            {
                _override = _override.WithSelected(deliveryId);
                return;
            }

            if (_hasTrusted)
                _trusted = _trusted.WithSelected(deliveryId);
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
