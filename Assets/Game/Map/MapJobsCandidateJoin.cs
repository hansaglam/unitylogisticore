namespace LogistiCore.Map
{
    public readonly struct MapJobsCandidateSlot
    {
        public MapJobsCandidateSlot(
            MapFleetKind kind,
            string assetId,
            bool inFleet,
            bool kindMatches,
            bool available,
            bool assigned)
        {
            Kind = kind;
            AssetId = assetId ?? "";
            InFleet = inFleet;
            KindMatches = kindMatches;
            Available = available;
            Assigned = assigned;
        }

        public MapFleetKind Kind { get; }
        public string AssetId { get; }
        public bool InFleet { get; }
        public bool KindMatches { get; }
        public bool Available { get; }
        public bool Assigned { get; }
        public bool Selectable =>
            !string.IsNullOrEmpty(AssetId) && InFleet && KindMatches && Available && !Assigned;
        public bool MissingFromFleet => !string.IsNullOrEmpty(AssetId) && !InFleet;
    }

    /// <summary>
    /// Joins contract candidate document IDs to the trusted fleet snapshot.
    /// Does not invent GPS, capacity, or catalog display names.
    /// </summary>
    public static class MapJobsCandidateJoin
    {
        public static bool TryResolve(MapJobsItem item, MapFleetSnapshot fleet, out MapJobsCandidateSlot[] slots)
        {
            slots = new MapJobsCandidateSlot[0];
            if (!item.HasStartCandidates)
                return false;
            var count = item.RequiresTrailer ? 3 : 2;
            var resolved = new MapJobsCandidateSlot[count];
            if (!TrySlot(item, fleet, MapFleetKind.Driver, out resolved[0]))
                return false;
            if (!TrySlot(item, fleet, MapFleetKind.Truck, out resolved[1]))
                return false;
            if (item.RequiresTrailer && !TrySlot(item, fleet, MapFleetKind.Trailer, out resolved[2]))
                return false;
            slots = resolved;
            return true;
        }

        public static bool TrySlot(MapJobsItem item, MapFleetSnapshot fleet, MapFleetKind kind, out MapJobsCandidateSlot slot)
        {
            slot = default;
            if (!item.TryCandidate(kind, out var candidate))
                return false;
            if (fleet.Assets == null || !fleet.TryGet(candidate.AssetId, out var asset))
            {
                slot = new MapJobsCandidateSlot(kind, candidate.AssetId, false, false, false, false);
                return true;
            }

            var kindMatches = asset.Kind == kind;
            slot = new MapJobsCandidateSlot(
                kind,
                candidate.AssetId,
                true,
                kindMatches,
                kindMatches && asset.Available,
                asset.HasAssignment);
            return true;
        }

        public static bool IsListedCandidate(MapJobsItem item, string assetId)
        {
            if (string.IsNullOrEmpty(assetId) || item.Candidates == null)
                return false;
            for (var i = 0; i < item.Candidates.Length; i++)
            {
                if (item.Candidates[i].AssetId == assetId)
                    return true;
            }

            return false;
        }
    }
}
