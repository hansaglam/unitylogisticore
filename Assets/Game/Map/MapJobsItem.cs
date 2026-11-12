namespace LogistiCore.Map
{
    public readonly struct MapJobCandidate
    {
        public MapJobCandidate(string assetId, MapFleetKind kind)
        {
            AssetId = assetId ?? "";
            Kind = kind;
        }

        public string AssetId { get; }
        public MapFleetKind Kind { get; }
    }

    /// <summary>
    /// One getMyContracts row, with optional getMyDeliveries fields kept separate.
    /// Reward stays a decimal string. No float conversion.
    /// </summary>
    public readonly struct MapJobsItem
    {
        public MapJobsItem(
            string contractId,
            MapJobContractStatus contractStatus,
            bool expired,
            string originCityId,
            string destinationCityId,
            string cargoId,
            string quantity,
            string unit,
            string reward,
            string currency,
            int scale,
            string deliveryId,
            MapJobDeliveryStatus deliveryStatus,
            string expectedArrivalAt,
            string assignedVehicleId,
            string expiresAt = "",
            string requirementTruck = "",
            string requirementDriver = "",
            string requirementTrailer = "",
            MapJobCandidate[] candidates = null,
            bool candidatesPresent = false)
        {
            ContractId = contractId ?? "";
            ContractStatus = contractStatus;
            Expired = expired;
            OriginCityId = originCityId ?? "";
            DestinationCityId = destinationCityId ?? "";
            CargoId = cargoId ?? "";
            Quantity = quantity ?? "";
            Unit = unit ?? "";
            Reward = reward ?? "";
            Currency = currency ?? "";
            Scale = scale;
            DeliveryId = deliveryId ?? "";
            DeliveryStatus = deliveryStatus;
            ExpectedArrivalAt = expectedArrivalAt ?? "";
            AssignedVehicleId = assignedVehicleId ?? "";
            ExpiresAt = expiresAt ?? "";
            RequirementTruck = requirementTruck ?? "";
            RequirementDriver = requirementDriver ?? "";
            RequirementTrailer = requirementTrailer ?? "";
            Candidates = candidates ?? new MapJobCandidate[0];
            CandidatesPresent = candidatesPresent;
        }

        public string ContractId { get; }
        public MapJobContractStatus ContractStatus { get; }
        public bool Expired { get; }
        public string OriginCityId { get; }
        public string DestinationCityId { get; }
        public string CargoId { get; }
        public string Quantity { get; }
        public string Unit { get; }
        public string Reward { get; }
        public string Currency { get; }
        public int Scale { get; }
        public string DeliveryId { get; }
        public MapJobDeliveryStatus DeliveryStatus { get; }
        public string ExpectedArrivalAt { get; }
        public string AssignedVehicleId { get; }
        public string ExpiresAt { get; }
        public string RequirementTruck { get; }
        public string RequirementDriver { get; }
        public string RequirementTrailer { get; }
        public MapJobCandidate[] Candidates { get; }
        public bool CandidatesPresent { get; }
        public bool RequiresTrailer
        {
            get
            {
                for (var i = 0; i < Candidates.Length; i++)
                {
                    if (Candidates[i].Kind == MapFleetKind.Trailer)
                        return true;
                }

                return false;
            }
        }
        public bool HasStartCandidates
        {
            get
            {
                return CandidatesPresent
                    && TryCandidate(MapFleetKind.Driver, out _)
                    && TryCandidate(MapFleetKind.Truck, out _)
                    && (!RequiresTrailer || TryCandidate(MapFleetKind.Trailer, out _));
            }
        }

        public bool HasReward => !string.IsNullOrEmpty(Reward);
        public bool HasSafeReward => MapJobsMoney.TryFormat(Reward, Currency, Scale, ".", out _);
        public bool HasCargo => !string.IsNullOrEmpty(CargoId);
        public bool HasQuantity => !string.IsNullOrEmpty(Quantity);
        public bool HasArrival => !string.IsNullOrEmpty(ExpectedArrivalAt);
        public bool HasOfferExpiry => ContractStatus == MapJobContractStatus.Offered && !string.IsNullOrEmpty(ExpiresAt);
        public bool HasVehicle => !string.IsNullOrEmpty(AssignedVehicleId);
        public bool HasRequirements =>
            !string.IsNullOrEmpty(RequirementTruck)
            || !string.IsNullOrEmpty(RequirementDriver)
            || !string.IsNullOrEmpty(RequirementTrailer);
        public bool HasOriginCity => MapCities.TryGet(OriginCityId, out _);
        public bool HasDestinationCity => MapCities.TryGet(DestinationCityId, out _);

        public string RewardDisplay
        {
            get
            {
                return MapJobsMoney.TryFormat(Reward, Currency, Scale, ".", out var text) ? text : "";
            }
        }

        public string RouteLabel
        {
            get
            {
                var origin = string.IsNullOrEmpty(OriginCityId) ? "" : OriginCityId;
                var dest = string.IsNullOrEmpty(DestinationCityId) ? "" : DestinationCityId;
                if (origin.Length == 0 && dest.Length == 0)
                    return ContractId;
                if (origin.Length == 0)
                    return dest;
                if (dest.Length == 0)
                    return origin;
                return origin + " → " + dest;
            }
        }

        public bool InCategory(MapJobCategory category)
        {
            switch (category)
            {
                case MapJobCategory.Available:
                    return ContractStatus == MapJobContractStatus.Offered && !Expired;
                case MapJobCategory.Active:
                    return ContractStatus == MapJobContractStatus.Accepted
                        || ContractStatus == MapJobContractStatus.InProgress
                        || DeliveryStatus == MapJobDeliveryStatus.Active
                        || ContractStatus == MapJobContractStatus.Unknown;
                default:
                    return ContractStatus == MapJobContractStatus.Completed
                        || ContractStatus == MapJobContractStatus.Failed
                        || (ContractStatus == MapJobContractStatus.Offered && Expired);
            }
        }

        public MapJobsItem WithDelivery(
            string deliveryId,
            MapJobDeliveryStatus deliveryStatus,
            string expectedArrivalAt,
            string assignedVehicleId)
        {
            return new MapJobsItem(
                ContractId,
                ContractStatus,
                Expired,
                OriginCityId,
                DestinationCityId,
                CargoId,
                Quantity,
                Unit,
                Reward,
                Currency,
                Scale,
                deliveryId,
                deliveryStatus,
                expectedArrivalAt,
                assignedVehicleId,
                ExpiresAt,
                RequirementTruck,
                RequirementDriver,
                RequirementTrailer,
                Candidates,
                CandidatesPresent);
        }

        public bool TryCandidate(MapFleetKind kind, out MapJobCandidate candidate)
        {
            for (var i = 0; i < Candidates.Length; i++)
            {
                if (Candidates[i].Kind == kind)
                {
                    candidate = Candidates[i];
                    return !string.IsNullOrEmpty(Candidates[i].AssetId);
                }
            }

            candidate = default;
            return false;
        }

        public static MapJobContractStatus ParseContract(string status)
        {
            if (status == "offered")
                return MapJobContractStatus.Offered;
            if (status == "accepted")
                return MapJobContractStatus.Accepted;
            if (status == "in_progress")
                return MapJobContractStatus.InProgress;
            if (status == "completed")
                return MapJobContractStatus.Completed;
            if (status == "failed")
                return MapJobContractStatus.Failed;
            return MapJobContractStatus.Unknown;
        }

        public static MapJobDeliveryStatus ParseDelivery(string status)
        {
            if (status == "active")
                return MapJobDeliveryStatus.Active;
            if (status == "completed")
                return MapJobDeliveryStatus.Completed;
            return MapJobDeliveryStatus.None;
        }
    }
}
