using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// One source of truth for the map footer. Live operations are not invented.
    /// Firebase Ready still means unconfigured operations until a read model exists.
    /// </summary>
    public static class MapOperationStrip
    {
        static bool _hasOverride;
        static MapOperationSnapshot _override;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            ClearOverride();
        }

        public static bool HasOverride => _hasOverride;

        public static MapOperationSnapshot Current
        {
            get
            {
                if (_hasOverride)
                    return _override;

                return FromBackend(FirebaseClient.Current);
            }
        }

        public static MapOperationSnapshot FromBackend(ClientBackendStatus status)
        {
            if (MapCompanyStore.HasTrusted)
                return MapOperationSnapshot.VerifiedRead(MapOperationStatus.Idle);

            switch (status.Phase)
            {
                case ClientBackendPhase.Starting:
                    return MapOperationSnapshot.Loading();
                case ClientBackendPhase.Failed:
                    return MapOperationSnapshot.Error();
                default:
                    return MapOperationSnapshot.Unconfigured();
            }
        }

        public static void Override(MapOperationSnapshot snapshot)
        {
            _override = snapshot;
            _hasOverride = true;
        }

        public static void ClearOverride()
        {
            _hasOverride = false;
            _override = default;
        }
    }
}
