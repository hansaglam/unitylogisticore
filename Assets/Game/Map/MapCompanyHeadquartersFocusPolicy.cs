using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Map
{
    public enum MapHeadquartersFocusState : byte
    {
        AwaitingVerifiedAccount = 0,
        Fetching = 1,
        Focused = 2,
        NoCompany = 3,
        LegacyUnassigned = 4,
        InvalidHeadquarters = 5,
        Unavailable = 6,
        UserNavigated = 7,
        ManualPreview = 8
    }

    /// <summary>
    /// Pure mapping of a trusted company HQ read result into the existing
    /// Türkiye-Europe launch map. No fallback to GPS, locale, example city,
    /// device storage, legacy warehouse location or user-entered preview.
    /// </summary>
    public static class MapCompanyHeadquartersFocusPolicy
    {
        public static MapHeadquartersFocusState Resolve(
            ClientCompanyHeadquartersState account, out MapGlobalCity city, out Vector2 world)
        {
            city = default;
            world = default;

            if (!account.HasCompany)
                return MapHeadquartersFocusState.NoCompany;
            if (!account.HasHeadquarters)
                return MapHeadquartersFocusState.LegacyUnassigned;
            if (string.IsNullOrEmpty(account.CompanyId) ||
                account.CatalogVersion != "lc_tur_europe_2026_11_v1")
                return MapHeadquartersFocusState.InvalidHeadquarters;

            var location = new MapBusinessLocation(account.CountryCode, account.CityKey);
            if (!location.IsValid ||
                !MapGlobalCityCatalog.TryGet(location, out city) ||
                !MapTradeRegionCatalog.Includes(city) ||
                !MapProjection.TryProject(city.Latitude, city.Longitude, out world))
            {
                city = default;
                world = default;
                return MapHeadquartersFocusState.InvalidHeadquarters;
            }

            return MapHeadquartersFocusState.Focused;
        }
    }
}
