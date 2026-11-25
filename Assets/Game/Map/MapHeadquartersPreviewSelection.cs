using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// One-shot navigation intent for an HQ location preview. This is NEVER the
    /// company's persisted headquarters, does not create a company, and makes
    /// no server entitlement or economy claims. It only moves the world map
    /// camera when the user explicitly chooses "Preview on map".
    /// </summary>
    public static class MapHeadquartersPreviewSelection
    {
        static MapBusinessLocation _pending;

        public static bool HasPending => _pending.IsValid;

        public static bool TryQueue(MapBusinessLocation location)
        {
            if (!location.IsValid || !MapTradeRegionCatalog.Includes(location))
                return false;
            _pending = location;
            return true;
        }

        public static bool TryConsume(out MapBusinessLocation location)
        {
            location = _pending;
            _pending = default;
            return location.IsValid;
        }

        public static void Clear()
        {
            _pending = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Clear();
    }
}
