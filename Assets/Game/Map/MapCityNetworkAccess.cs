using System;
using System.Collections.Generic;

namespace LogistiCore.Map
{
    /// <summary>
    /// Stable identity from a vetted city catalog. City keys must be unique
    /// within a country (for example a dataset id, not just "Springfield").
    /// Legacy Turkey marker ids may remain as their existing keys.
    /// </summary>
    public readonly struct MapBusinessLocation : IEquatable<MapBusinessLocation>
    {
        public readonly string CountryCode;
        public readonly string CityKey;

        public MapBusinessLocation(string countryCode, string cityKey)
        {
            CountryCode = (countryCode ?? "").Trim().ToUpperInvariant();
            CityKey = (cityKey ?? "").Trim().ToLowerInvariant();
        }

        public bool IsValid =>
            CountryCode.Length == 3 && CityKey.Length > 0 &&
            CountryCode[0] >= 'A' && CountryCode[0] <= 'Z' &&
            CountryCode[1] >= 'A' && CountryCode[1] <= 'Z' &&
            CountryCode[2] >= 'A' && CountryCode[2] <= 'Z';

        public bool Equals(MapBusinessLocation other) =>
            string.Equals(CountryCode, other.CountryCode, StringComparison.Ordinal) &&
            string.Equals(CityKey, other.CityKey, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is MapBusinessLocation other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((CountryCode != null ? StringComparer.Ordinal.GetHashCode(CountryCode) : 0) * 397)
                    ^ (CityKey != null ? StringComparer.Ordinal.GetHashCode(CityKey) : 0);
            }
        }
    }

    public enum MapCityNetworkStage : byte
    {
        Unverified = 0,
        Explore = 1,
        Delivery = 2,
        BranchOpportunity = 3,
        OwnedFacility = 4
    }

    /// <summary>
    /// Read-only trusted projection. This is NOT a local economy, an unlock
    /// command or authorization for contract acceptance. Server transactions
    /// must validate every delivery, international access and branch purchase.
    /// </summary>
    public sealed class MapCityNetworkSnapshot
    {
        public readonly bool Trusted;
        public readonly MapBusinessLocation Headquarters;
        public readonly MapBusinessLocation[] DeliveryDestinations;
        public readonly MapBusinessLocation[] EligibleBranchLocations;
        public readonly MapBusinessLocation[] OwnedFacilityLocations;
        public readonly string[] ApprovedCountryCodes;

        public MapCityNetworkSnapshot(
            bool trusted,
            MapBusinessLocation headquarters,
            MapBusinessLocation[] deliveryDestinations,
            MapBusinessLocation[] eligibleBranchLocations,
            MapBusinessLocation[] ownedFacilityLocations,
            string[] approvedCountryCodes)
        {
            Trusted = trusted;
            Headquarters = headquarters;
            DeliveryDestinations = Copy(deliveryDestinations);
            EligibleBranchLocations = Copy(eligibleBranchLocations);
            OwnedFacilityLocations = Copy(ownedFacilityLocations);
            ApprovedCountryCodes = Copy(approvedCountryCodes);
        }

        static T[] Copy<T>(T[] source)
        {
            if (source == null || source.Length == 0)
                return new T[0];
            var clone = new T[source.Length];
            Array.Copy(source, clone, source.Length);
            return clone;
        }
    }

    public readonly struct MapCityNetworkView
    {
        public readonly MapCityNetworkStage Stage;
        public readonly bool IsHeadquarters;
        public readonly bool HasVerifiedDeliveryAccess;
        public readonly bool CanRequestBranchExpansion;
        public readonly bool HasOwnedFacility;
        public readonly bool HasApprovedCountryAccess;

        public MapCityNetworkView(MapCityNetworkStage stage, bool isHeadquarters,
            bool hasVerifiedDeliveryAccess, bool canRequestBranchExpansion,
            bool hasOwnedFacility, bool hasApprovedCountryAccess)
        {
            Stage = stage;
            IsHeadquarters = isHeadquarters;
            HasVerifiedDeliveryAccess = hasVerifiedDeliveryAccess;
            CanRequestBranchExpansion = canRequestBranchExpansion;
            HasOwnedFacility = hasOwnedFacility;
            HasApprovedCountryAccess = hasApprovedCountryAccess;
        }
    }

    /// <summary>
    /// World map may show valid cities without owning them. Delivery, country
    /// access and facility ownership are independent, server-derived flags:
    /// reaching a city never silently creates a branch, and opening a branch
    /// never fabricates a route or a playable facility scene.
    /// </summary>
    public static class MapCityNetworkAccess
    {
        public static MapCityNetworkView Evaluate(
            MapBusinessLocation city, MapCityNetworkSnapshot snapshot)
        {
            if (!city.IsValid || snapshot == null || !snapshot.Trusted)
                return new MapCityNetworkView(MapCityNetworkStage.Unverified,
                    false, false, false, false, false);

            var owned = Contains(snapshot.OwnedFacilityLocations, city);
            var delivery = Contains(snapshot.DeliveryDestinations, city);
            var expansion = !owned && Contains(snapshot.EligibleBranchLocations, city);
            var home = snapshot.Headquarters.IsValid && snapshot.Headquarters.Equals(city);
            var countryApproved =
                (snapshot.Headquarters.IsValid &&
                 string.Equals(snapshot.Headquarters.CountryCode, city.CountryCode, StringComparison.Ordinal))
                || ContainsCountry(snapshot.ApprovedCountryCodes, city.CountryCode);

            var stage = owned
                ? MapCityNetworkStage.OwnedFacility
                : expansion
                    ? MapCityNetworkStage.BranchOpportunity
                    : delivery
                        ? MapCityNetworkStage.Delivery
                        : MapCityNetworkStage.Explore;

            return new MapCityNetworkView(stage, home, delivery, expansion, owned, countryApproved);
        }

        static bool Contains(MapBusinessLocation[] locations, MapBusinessLocation city)
        {
            if (locations == null)
                return false;
            for (var i = 0; i < locations.Length; i++)
            {
                if (locations[i].IsValid && locations[i].Equals(city))
                    return true;
            }
            return false;
        }

        static bool ContainsCountry(string[] countries, string countryCode)
        {
            if (countries == null)
                return false;
            for (var i = 0; i < countries.Length; i++)
            {
                if (string.Equals(countries[i]?.Trim(), countryCode,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
