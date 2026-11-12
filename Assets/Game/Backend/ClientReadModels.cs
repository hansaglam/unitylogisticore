using System.Collections;
using System.Collections.Generic;

namespace LogistiCore.Backend
{
    public readonly struct ClientIdentityResult
    {
        public ClientIdentityResult(string playerId, int schemaVersion, string identityStatus)
        {
            PlayerId = playerId ?? "";
            SchemaVersion = schemaVersion;
            IdentityStatus = identityStatus ?? "";
        }

        public string PlayerId { get; }
        public int SchemaVersion { get; }
        public string IdentityStatus { get; }
    }

    public readonly struct ClientCompanyState
    {
        public ClientCompanyState(
            bool hasCompany,
            string companyId,
            string name,
            string bootstrapStatus,
            bool economyReady)
        {
            HasCompany = hasCompany;
            CompanyId = companyId ?? "";
            Name = name ?? "";
            BootstrapStatus = bootstrapStatus ?? "";
            EconomyReady = economyReady;
        }

        public bool HasCompany { get; }
        public string CompanyId { get; }
        public string Name { get; }
        public string BootstrapStatus { get; }
        public bool EconomyReady { get; }
    }

    public readonly struct ClientEconomyState
    {
        public ClientEconomyState(
            bool hasCompany,
            bool economyReady,
            string companyId,
            string balance,
            string currency,
            int scale,
            int xp,
            int level,
            string configVersion,
            string rulesVersion)
        {
            HasCompany = hasCompany;
            EconomyReady = economyReady;
            CompanyId = companyId ?? "";
            Balance = balance ?? "";
            Currency = currency ?? "";
            Scale = scale;
            Xp = xp;
            Level = level;
            ConfigVersion = configVersion ?? "";
            RulesVersion = rulesVersion ?? "";
        }

        public bool HasCompany { get; }
        public bool EconomyReady { get; }
        public string CompanyId { get; }
        public string Balance { get; }
        public string Currency { get; }
        public int Scale { get; }
        public int Xp { get; }
        public int Level { get; }
        public string ConfigVersion { get; }
        public string RulesVersion { get; }
        public bool HasBalance => EconomyReady && ClientMoney.TryValidate(Balance, Currency, Scale);
        public bool HasReputation => false;
    }

    public readonly struct ClientFleetAsset
    {
        public ClientFleetAsset(
            string assetId,
            string kind,
            string catalogId,
            string catalogVersion,
            string status,
            string assignmentId,
            bool available)
        {
            AssetId = assetId ?? "";
            Kind = kind ?? "";
            CatalogId = catalogId ?? "";
            CatalogVersion = catalogVersion ?? "";
            Status = status ?? "";
            AssignmentId = assignmentId ?? "";
            Available = available;
        }

        public string AssetId { get; }
        public string Kind { get; }
        public string CatalogId { get; }
        public string CatalogVersion { get; }
        public string Status { get; }
        public string AssignmentId { get; }
        public bool Available { get; }
    }

    public readonly struct ClientFleetState
    {
        public ClientFleetState(
            bool hasCompany,
            bool economyReady,
            bool starterFleetReady,
            ClientFleetAsset[] items,
            string nextCursor)
        {
            HasCompany = hasCompany;
            EconomyReady = economyReady;
            StarterFleetReady = starterFleetReady;
            Items = items ?? new ClientFleetAsset[0];
            NextCursor = nextCursor ?? "";
        }

        public bool HasCompany { get; }
        public bool EconomyReady { get; }
        public bool StarterFleetReady { get; }
        public ClientFleetAsset[] Items { get; }
        public string NextCursor { get; }
    }

    public readonly struct ClientWarehouseRow
    {
        public ClientWarehouseRow(
            string warehouseId,
            string catalogId,
            string catalogVersion,
            string cityId,
            string type,
            int level,
            string status,
            string capacityModelId,
            string capacityUnits,
            string usedCapacity)
        {
            WarehouseId = warehouseId ?? "";
            CatalogId = catalogId ?? "";
            CatalogVersion = catalogVersion ?? "";
            CityId = cityId ?? "";
            Type = type ?? "";
            Level = level;
            Status = status ?? "";
            CapacityModelId = capacityModelId ?? "";
            CapacityUnits = capacityUnits ?? "";
            UsedCapacity = usedCapacity ?? "";
        }

        public string WarehouseId { get; }
        public string CatalogId { get; }
        public string CatalogVersion { get; }
        public string CityId { get; }
        public string Type { get; }
        public int Level { get; }
        public string Status { get; }
        public string CapacityModelId { get; }
        public string CapacityUnits { get; }
        public string UsedCapacity { get; }
    }

    public readonly struct ClientWarehouseState
    {
        public ClientWarehouseState(bool hasCompany, bool economyReady, ClientWarehouseRow[] items, string nextCursor)
        {
            HasCompany = hasCompany;
            EconomyReady = economyReady;
            Items = items ?? new ClientWarehouseRow[0];
            NextCursor = nextCursor ?? "";
        }

        public bool HasCompany { get; }
        public bool EconomyReady { get; }
        public ClientWarehouseRow[] Items { get; }
        public string NextCursor { get; }
    }

    public readonly struct ClientContractRow
    {
        public ClientContractRow(
            string contractId,
            string status,
            bool expired,
            string originCityId,
            string destinationCityId,
            string routeId,
            string cargoId,
            string quantity,
            string unit,
            string reward,
            string currency,
            int scale,
            string expiresAt,
            string acceptedAt,
            ClientDeliveryAsset[] candidateAssets = null,
            bool candidateAssetsPresent = false)
        {
            ContractId = contractId ?? "";
            Status = status ?? "";
            Expired = expired;
            OriginCityId = originCityId ?? "";
            DestinationCityId = destinationCityId ?? "";
            RouteId = routeId ?? "";
            CargoId = cargoId ?? "";
            Quantity = quantity ?? "";
            Unit = unit ?? "";
            Reward = reward ?? "";
            Currency = currency ?? "";
            Scale = scale;
            ExpiresAt = expiresAt ?? "";
            AcceptedAt = acceptedAt ?? "";
            CandidateAssets = candidateAssets ?? new ClientDeliveryAsset[0];
            CandidateAssetsPresent = candidateAssetsPresent;
        }

        public string ContractId { get; }
        public string Status { get; }
        public bool Expired { get; }
        public string OriginCityId { get; }
        public string DestinationCityId { get; }
        public string RouteId { get; }
        public string CargoId { get; }
        public string Quantity { get; }
        public string Unit { get; }
        public string Reward { get; }
        public string Currency { get; }
        public int Scale { get; }
        public string ExpiresAt { get; }
        public string AcceptedAt { get; }
        public ClientDeliveryAsset[] CandidateAssets { get; }
        public bool CandidateAssetsPresent { get; }
    }

    public readonly struct ClientContractPage
    {
        public ClientContractPage(ClientContractRow[] items, string nextCursor)
        {
            Items = items ?? new ClientContractRow[0];
            NextCursor = nextCursor ?? "";
        }

        public ClientContractRow[] Items { get; }
        public string NextCursor { get; }
    }

    public readonly struct ClientDeliveryAsset
    {
        public ClientDeliveryAsset(string assetId, string kind)
        {
            AssetId = assetId ?? "";
            Kind = kind ?? "";
        }

        public string AssetId { get; }
        public string Kind { get; }
    }

    public readonly struct ClientDeliveryRow
    {
        public ClientDeliveryRow(
            string deliveryId,
            string contractId,
            string status,
            string originCityId,
            string destinationCityId,
            string routeId,
            string cargoId,
            ClientDeliveryAsset[] assets,
            string startedAt,
            string expectedArrivalAt,
            bool arrivalReady,
            string completedAt,
            string reward,
            string currency,
            int scale)
        {
            DeliveryId = deliveryId ?? "";
            ContractId = contractId ?? "";
            Status = status ?? "";
            OriginCityId = originCityId ?? "";
            DestinationCityId = destinationCityId ?? "";
            RouteId = routeId ?? "";
            CargoId = cargoId ?? "";
            Assets = assets ?? new ClientDeliveryAsset[0];
            StartedAt = startedAt ?? "";
            ExpectedArrivalAt = expectedArrivalAt ?? "";
            ArrivalReady = arrivalReady;
            CompletedAt = completedAt ?? "";
            Reward = reward ?? "";
            Currency = currency ?? "";
            Scale = scale;
        }

        public string DeliveryId { get; }
        public string ContractId { get; }
        public string Status { get; }
        public string OriginCityId { get; }
        public string DestinationCityId { get; }
        public string RouteId { get; }
        public string CargoId { get; }
        public ClientDeliveryAsset[] Assets { get; }
        public string StartedAt { get; }
        public string ExpectedArrivalAt { get; }
        public bool ArrivalReady { get; }
        public string CompletedAt { get; }
        public string Reward { get; }
        public string Currency { get; }
        public int Scale { get; }
        public bool HasTransportMode => false;
    }

    public readonly struct ClientDeliveryPage
    {
        public ClientDeliveryPage(ClientDeliveryRow[] items, string nextCursor)
        {
            Items = items ?? new ClientDeliveryRow[0];
            NextCursor = nextCursor ?? "";
        }

        public ClientDeliveryRow[] Items { get; }
        public string NextCursor { get; }
    }

    public readonly struct ClientMarketCondition
    {
        public ClientMarketCondition(
            string cityId,
            string cargoId,
            int demandIndex,
            int supplyIndex,
            int adjustmentBps,
            int trendBps)
        {
            CityId = cityId ?? "";
            CargoId = cargoId ?? "";
            DemandIndex = demandIndex;
            SupplyIndex = supplyIndex;
            AdjustmentBps = adjustmentBps;
            TrendBps = trendBps;
        }

        public string CityId { get; }
        public string CargoId { get; }
        public int DemandIndex { get; }
        public int SupplyIndex { get; }
        public int AdjustmentBps { get; }
        public int TrendBps { get; }
        public bool HasPrice => false;
        public bool HasCurrency => false;
    }

    public readonly struct ClientMarketState
    {
        public ClientMarketState(
            bool synthetic,
            string marketVersion,
            string policy,
            int policyVersion,
            string epoch,
            string periodEndsAt,
            ClientMarketCondition[] conditions)
        {
            Synthetic = synthetic;
            MarketVersion = marketVersion ?? "";
            Policy = policy ?? "";
            PolicyVersion = policyVersion;
            Epoch = epoch ?? "";
            PeriodEndsAt = periodEndsAt ?? "";
            Conditions = conditions ?? new ClientMarketCondition[0];
        }

        public bool Synthetic { get; }
        public string MarketVersion { get; }
        public string Policy { get; }
        public int PolicyVersion { get; }
        public string Epoch { get; }
        public string PeriodEndsAt { get; }
        public ClientMarketCondition[] Conditions { get; }
    }

    public readonly struct ClientAcceptContractResult
    {
        public ClientAcceptContractResult(
            string contractId,
            string status,
            string acceptedAt,
            string reward,
            string currency,
            int scale,
            bool equipmentReserved)
        {
            ContractId = contractId ?? "";
            Status = status ?? "";
            AcceptedAt = acceptedAt ?? "";
            Reward = reward ?? "";
            Currency = currency ?? "";
            Scale = scale;
            EquipmentReserved = equipmentReserved;
        }

        public string ContractId { get; }
        public string Status { get; }
        public string AcceptedAt { get; }
        public string Reward { get; }
        public string Currency { get; }
        public int Scale { get; }
        public bool EquipmentReserved { get; }
        public bool IsAccepted => Status == "accepted";
    }

    public static class ClientReadParser
    {
        public static bool TryAcceptContract(object result, out ClientAcceptContractResult parsed)
        {
            parsed = default;
            if (!ClientValue.TryMap(result, out var map))
                return false;
            if (!ClientValue.TryString(ClientValue.Get(map, "contractId"), out var contractId)
                || !ClientValue.IsSafeContractId(contractId))
                return false;
            if (!ClientValue.TryString(ClientValue.Get(map, "status"), out var status) || status != "accepted")
                return false;
            var acceptedAt = ClientValue.Get(map, "acceptedAt") as string ?? "";
            var reward = StringOrNumber(ClientValue.Get(map, "reward"));
            ClientValue.TryString(ClientValue.Get(map, "currency"), out var currency);
            ClientValue.TryInt(ClientValue.Get(map, "scale"), out var scale);
            ClientValue.TryBool(ClientValue.Get(map, "equipmentReserved"), out var reserved);
            parsed = new ClientAcceptContractResult(contractId, status, acceptedAt, reward, currency, scale, reserved);
            return true;
        }

        public static bool TryIdentity(object result, out ClientIdentityResult parsed)
        {
            parsed = default;
            if (!ClientValue.TryMap(result, out var map))
                return false;
            if (!ClientValue.TryString(ClientValue.Get(map, "playerId"), out var playerId) || playerId.Length == 0)
                return false;
            if (!ClientValue.TryInt(ClientValue.Get(map, "schemaVersion"), out var schema))
                return false;
            if (!ClientValue.TryString(ClientValue.Get(map, "identityStatus"), out var status))
                return false;
            parsed = new ClientIdentityResult(playerId, schema, status);
            return true;
        }

        public static bool TryCompany(object result, out ClientCompanyState parsed)
        {
            parsed = default;
            if (!ClientValue.TryMap(result, out var map))
                return false;
            if (!ClientValue.TryBool(ClientValue.Get(map, "hasCompany"), out var hasCompany))
                return false;
            if (!hasCompany)
            {
                parsed = new ClientCompanyState(false, "", "", "", false);
                return true;
            }

            if (!ClientValue.TryString(ClientValue.Get(map, "companyId"), out var companyId))
                return false;
            if (!ClientValue.TryString(ClientValue.Get(map, "name"), out var name))
                return false;
            ClientValue.TryString(ClientValue.Get(map, "bootstrapStatus"), out var bootstrap);
            ClientValue.TryBool(ClientValue.Get(map, "economyReady"), out var ready);
            parsed = new ClientCompanyState(true, companyId, name, bootstrap, ready);
            return true;
        }

        public static bool TryEconomy(object result, out ClientEconomyState parsed)
        {
            parsed = default;
            if (!ClientValue.TryMap(result, out var map))
                return false;
            ClientValue.TryBool(ClientValue.Get(map, "hasCompany"), out var hasCompany);
            ClientValue.TryBool(ClientValue.Get(map, "economyReady"), out var ready);
            if (!ready)
            {
                parsed = new ClientEconomyState(hasCompany, false, "", "", "", 0, 0, 0, "", "");
                return true;
            }

            if (!ClientValue.TryString(ClientValue.Get(map, "balance"), out var balance))
                return false;
            if (!ClientValue.TryString(ClientValue.Get(map, "currency"), out var currency))
                return false;
            if (!ClientValue.TryInt(ClientValue.Get(map, "scale"), out var scale))
                return false;
            if (!ClientMoney.TryValidate(balance, currency, scale))
                return false;
            ClientValue.TryString(ClientValue.Get(map, "companyId"), out var companyId);
            ClientValue.TryInt(ClientValue.Get(map, "xp"), out var xp);
            ClientValue.TryInt(ClientValue.Get(map, "level"), out var level);
            ClientValue.TryString(ClientValue.Get(map, "configVersion"), out var config);
            ClientValue.TryString(ClientValue.Get(map, "rulesVersion"), out var rules);
            parsed = new ClientEconomyState(hasCompany, true, companyId, balance, currency, scale, xp, level, config, rules);
            return true;
        }

        public static bool TryFleet(object result, out ClientFleetState parsed)
        {
            parsed = default;
            if (!ClientValue.TryMap(result, out var map) || !TryItems(map, out var rows))
                return false;
            var items = new ClientFleetAsset[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                if (!ClientValue.TryMap(rows[i], out var row))
                    return false;
                if (!ClientValue.TryString(ClientValue.Get(row, "assetId"), out var assetId))
                    return false;
                ClientValue.TryString(ClientValue.Get(row, "kind"), out var kind);
                ClientValue.TryString(ClientValue.Get(row, "catalogId"), out var catalogId);
                ClientValue.TryString(ClientValue.Get(row, "catalogVersion"), out var catalogVersion);
                ClientValue.TryString(ClientValue.Get(row, "status"), out var status);
                var assignment = ClientValue.Get(row, "assignmentId");
                string assignmentId;
                if (assignment == null)
                    assignmentId = "";
                else if (!ClientValue.TryString(assignment, out assignmentId))
                    return false;
                bool available;
                if (!ClientValue.TryBool(ClientValue.Get(row, "available"), out available))
                    available = status == "operational" && string.IsNullOrEmpty(assignmentId);
                items[i] = new ClientFleetAsset(assetId, kind, catalogId, catalogVersion, status, assignmentId, available);
            }

            ClientValue.TryBool(ClientValue.Get(map, "hasCompany"), out var hasCompany);
            ClientValue.TryBool(ClientValue.Get(map, "economyReady"), out var economyReady);
            ClientValue.TryBool(ClientValue.Get(map, "starterFleetReady"), out var starter);
            var cursor = ClientValue.Get(map, "nextCursor") as string ?? "";
            parsed = new ClientFleetState(hasCompany, economyReady, starter, items, cursor);
            return true;
        }

        public static bool TryWarehouse(object result, out ClientWarehouseState parsed)
        {
            parsed = default;
            if (!ClientValue.TryMap(result, out var map) || !TryItems(map, out var rows))
                return false;
            var items = new ClientWarehouseRow[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                if (!ClientValue.TryMap(rows[i], out var row))
                    return false;
                if (!ClientValue.TryString(ClientValue.Get(row, "warehouseId"), out var id))
                    return false;
                ClientValue.TryString(ClientValue.Get(row, "catalogId"), out var catalogId);
                ClientValue.TryString(ClientValue.Get(row, "catalogVersion"), out var catalogVersion);
                ClientValue.TryString(ClientValue.Get(row, "cityId"), out var cityId);
                ClientValue.TryString(ClientValue.Get(row, "type"), out var type);
                ClientValue.TryInt(ClientValue.Get(row, "level"), out var level);
                ClientValue.TryString(ClientValue.Get(row, "status"), out var status);
                ClientValue.TryString(ClientValue.Get(row, "capacityModelId"), out var model);
                var units = StringOrNumber(ClientValue.Get(row, "capacityUnits"));
                var used = StringOrNumber(ClientValue.Get(row, "usedCapacity"));
                items[i] = new ClientWarehouseRow(id, catalogId, catalogVersion, cityId, type, level, status, model, units, used);
            }

            ClientValue.TryBool(ClientValue.Get(map, "hasCompany"), out var hasCompany);
            ClientValue.TryBool(ClientValue.Get(map, "economyReady"), out var economyReady);
            parsed = new ClientWarehouseState(hasCompany, economyReady, items, ClientValue.Get(map, "nextCursor") as string ?? "");
            return true;
        }

        public static bool TryContracts(object result, out ClientContractPage parsed)
        {
            parsed = default;
            if (!ClientValue.TryMap(result, out var map) || !TryItems(map, out var rows))
                return false;
            var items = new ClientContractRow[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                if (!ClientValue.TryMap(rows[i], out var row))
                    return false;
                if (!ClientValue.TryString(ClientValue.Get(row, "contractId"), out var id))
                    return false;
                ClientValue.TryString(ClientValue.Get(row, "status"), out var status);
                ClientValue.TryBool(ClientValue.Get(row, "expired"), out var expired);
                ClientValue.TryString(ClientValue.Get(row, "originCityId"), out var origin);
                ClientValue.TryString(ClientValue.Get(row, "destinationCityId"), out var dest);
                ClientValue.TryString(ClientValue.Get(row, "routeId"), out var route);
                ClientValue.TryString(ClientValue.Get(row, "cargoId"), out var cargo);
                var quantity = StringOrNumber(ClientValue.Get(row, "quantity"));
                ClientValue.TryString(ClientValue.Get(row, "unit"), out var unit);
                var reward = StringOrNumber(ClientValue.Get(row, "reward"));
                ClientValue.TryString(ClientValue.Get(row, "currency"), out var currency);
                ClientValue.TryInt(ClientValue.Get(row, "scale"), out var scale);
                ClientValue.TryString(ClientValue.Get(row, "expiresAt"), out var expires);
                var accepted = ClientValue.Get(row, "acceptedAt") as string ?? "";
                if (!TryCandidateAssets(row, out var candidates, out var candidatesPresent))
                    return false;
                items[i] = new ClientContractRow(
                    id, status, expired, origin, dest, route, cargo, quantity, unit, reward, currency, scale, expires, accepted,
                    candidates, candidatesPresent);
            }

            parsed = new ClientContractPage(items, ClientValue.Get(map, "nextCursor") as string ?? "");
            return true;
        }

        public static bool TryDeliveries(object result, out ClientDeliveryPage parsed)
        {
            parsed = default;
            if (!ClientValue.TryMap(result, out var map) || !TryItems(map, out var rows))
                return false;
            var items = new ClientDeliveryRow[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                if (!ClientValue.TryMap(rows[i], out var row))
                    return false;
                if (!ClientValue.TryString(ClientValue.Get(row, "deliveryId"), out var id))
                    return false;
                ClientValue.TryString(ClientValue.Get(row, "contractId"), out var contractId);
                ClientValue.TryString(ClientValue.Get(row, "status"), out var status);
                ClientValue.TryString(ClientValue.Get(row, "originCityId"), out var origin);
                ClientValue.TryString(ClientValue.Get(row, "destinationCityId"), out var dest);
                ClientValue.TryString(ClientValue.Get(row, "routeId"), out var route);
                ClientValue.TryString(ClientValue.Get(row, "cargoId"), out var cargo);
                var assets = ParseAssets(ClientValue.Get(row, "assets"));
                ClientValue.TryString(ClientValue.Get(row, "startedAt"), out var started);
                ClientValue.TryString(ClientValue.Get(row, "expectedArrivalAt"), out var arrival);
                ClientValue.TryBool(ClientValue.Get(row, "arrivalReady"), out var ready);
                var completed = ClientValue.Get(row, "completedAt") as string ?? "";
                var reward = StringOrNumber(ClientValue.Get(row, "reward"));
                ClientValue.TryString(ClientValue.Get(row, "currency"), out var currency);
                ClientValue.TryInt(ClientValue.Get(row, "scale"), out var scale);
                if (row.Contains("transportMode"))
                    return false;
                items[i] = new ClientDeliveryRow(
                    id, contractId, status, origin, dest, route, cargo, assets, started, arrival, ready, completed,
                    reward, currency, scale);
            }

            parsed = new ClientDeliveryPage(items, ClientValue.Get(map, "nextCursor") as string ?? "");
            return true;
        }

        public static bool TryMarket(object result, out ClientMarketState parsed)
        {
            parsed = default;
            if (!ClientValue.TryMap(result, out var map))
                return false;
            ClientValue.TryBool(ClientValue.Get(map, "synthetic"), out var synthetic);
            ClientValue.TryString(ClientValue.Get(map, "marketVersion"), out var version);
            ClientValue.TryString(ClientValue.Get(map, "policy"), out var policy);
            ClientValue.TryInt(ClientValue.Get(map, "policyVersion"), out var policyVersion);
            var epoch = StringOrNumber(ClientValue.Get(map, "epoch"));
            ClientValue.TryString(ClientValue.Get(map, "periodEndsAt"), out var period);
            if (!ClientValue.TryList(ClientValue.Get(map, "conditions"), out var rows))
                rows = new object[0];
            var items = new ClientMarketCondition[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                if (!ClientValue.TryMap(rows[i], out var row))
                    return false;
                if (row.Contains("price") || row.Contains("currency"))
                    return false;
                ClientValue.TryString(ClientValue.Get(row, "cityId"), out var cityId);
                ClientValue.TryString(ClientValue.Get(row, "cargoId"), out var cargoId);
                ClientValue.TryInt(ClientValue.Get(row, "demandIndex"), out var demand);
                ClientValue.TryInt(ClientValue.Get(row, "supplyIndex"), out var supply);
                ClientValue.TryInt(ClientValue.Get(row, "adjustmentBps"), out var adj);
                ClientValue.TryInt(ClientValue.Get(row, "trendBps"), out var trend);
                items[i] = new ClientMarketCondition(cityId, cargoId, demand, supply, adj, trend);
            }

            parsed = new ClientMarketState(synthetic, version, policy, policyVersion, epoch, period, items);
            return true;
        }

        static bool TryItems(IDictionary map, out IList rows)
        {
            return ClientValue.TryList(ClientValue.Get(map, "items"), out rows);
        }

        public static bool TryCandidateAssets(IDictionary row, out ClientDeliveryAsset[] assets, out bool present)
        {
            assets = new ClientDeliveryAsset[0];
            present = false;
            if (row == null || !row.Contains("candidateAssets"))
                return true;
            present = true;
            if (!ClientValue.TryList(ClientValue.Get(row, "candidateAssets"), out var list))
                return false;
            if (list.Count != 2 && list.Count != 3)
                return false;
            var parsed = new ClientDeliveryAsset[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                if (!ClientValue.TryMap(list[i], out var entry) || entry.Count != 2)
                    return false;
                if (!ClientValue.TryString(ClientValue.Get(entry, "assetId"), out var assetId)
                    || !ClientValue.IsSafeContractId(assetId))
                    return false;
                if (!ClientValue.TryString(ClientValue.Get(entry, "kind"), out var kind)
                    || !IsCandidateKind(kind))
                    return false;
                parsed[i] = new ClientDeliveryAsset(assetId, kind);
            }

            if (!SelectionRolesValid(parsed))
                return false;
            assets = parsed;
            return true;
        }

        static bool IsCandidateKind(string kind)
        {
            return kind == "truck" || kind == "driver" || kind == "trailer";
        }

        static bool SelectionRolesValid(ClientDeliveryAsset[] assets)
        {
            var truck = 0;
            var driver = 0;
            var trailer = 0;
            for (var i = 0; i < assets.Length; i++)
            {
                var id = assets[i].AssetId;
                for (var j = 0; j < i; j++)
                {
                    if (assets[j].AssetId == id)
                        return false;
                }

                if (assets[i].Kind == "truck")
                    truck++;
                else if (assets[i].Kind == "driver")
                    driver++;
                else if (assets[i].Kind == "trailer")
                    trailer++;
                else
                    return false;
            }

            if (assets.Length == 2)
                return driver == 1 && truck == 1 && trailer == 0;
            return driver == 1 && truck == 1 && trailer == 1;
        }

        static ClientDeliveryAsset[] ParseAssets(object raw)
        {
            if (!ClientValue.TryList(raw, out var list))
                return new ClientDeliveryAsset[0];
            var assets = new ClientDeliveryAsset[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                if (!ClientValue.TryMap(list[i], out var row))
                    return new ClientDeliveryAsset[0];
                ClientValue.TryString(ClientValue.Get(row, "assetId"), out var id);
                ClientValue.TryString(ClientValue.Get(row, "kind"), out var kind);
                assets[i] = new ClientDeliveryAsset(id, kind);
            }

            return assets;
        }

        static string StringOrNumber(object value)
        {
            if (value == null)
                return "";
            if (value is string text)
                return text;
            if (value is int i)
                return i.ToString();
            if (value is long l)
                return l.ToString();
            return "";
        }
    }

    public static class ClientRoadFilter
    {
        public static bool CanPresentAsRoad(ClientDeliveryRow row)
        {
            if (string.IsNullOrEmpty(row.DeliveryId) || string.IsNullOrEmpty(row.ContractId))
                return false;
            if (row.Status != "active" && row.Status != "in_progress")
                return false;
            return Map.ClientStoreBridge.CityKnown(row.OriginCityId)
                && Map.ClientStoreBridge.CityKnown(row.DestinationCityId);
        }
    }
}
