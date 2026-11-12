using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Trusted getMyEconomyState snapshot. No HUD is attached in this phase unless a
    /// management surface already exists; values stay backend-authoritative.
    /// </summary>
    public static class MapCompanyStore
    {
        static bool _hasTrusted;
        static ClientEconomyState _economy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            ClearTrusted();
        }

        public static bool HasTrusted => _hasTrusted;
        public static ClientEconomyState Economy => _hasTrusted ? _economy : default;

        public static bool TryApplyTrusted(ClientEconomyState economy)
        {
            if (!economy.HasCompany && !economy.EconomyReady)
            {
                _economy = economy;
                _hasTrusted = true;
                return true;
            }

            _economy = economy;
            _hasTrusted = true;
            return true;
        }

        public static void ClearTrusted()
        {
            _hasTrusted = false;
            _economy = default;
        }
    }
}
