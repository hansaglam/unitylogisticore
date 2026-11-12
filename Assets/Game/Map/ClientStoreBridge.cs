using LogistiCore.Backend;

namespace LogistiCore.Map
{
    public static class ClientStoreBridge
    {
        public static MapFleetSnapshot Fleet(ClientFleetState state)
        {
            var assets = new MapFleetAsset[state.Items.Length];
            for (var i = 0; i < state.Items.Length; i++)
            {
                var row = state.Items[i];
                assets[i] = new MapFleetAsset(
                    row.AssetId,
                    MapFleetAsset.ParseKind(row.Kind),
                    row.CatalogId,
                    MapFleetAsset.ParseStatus(row.Status),
                    row.Available,
                    row.AssignmentId,
                    "",
                    "",
                    false,
                    0d,
                    false,
                    0d);
            }

            var status = MapFleetStatus.Ready;
            if (assets.Length == 0 || !state.HasCompany)
                status = MapFleetStatus.Empty;
            return new MapFleetSnapshot(status, status == MapFleetStatus.Empty ? new MapFleetAsset[0] : assets, "", false);
        }

        public static MapWarehouseSnapshot Warehouse(ClientWarehouseState state)
        {
            var items = new MapWarehouseItem[state.Items.Length];
            for (var i = 0; i < state.Items.Length; i++)
            {
                var row = state.Items[i];
                items[i] = new MapWarehouseItem(
                    row.WarehouseId,
                    row.CatalogId,
                    row.Type,
                    row.CityId,
                    row.Status == "out_of_service"
                        ? MapWarehouseSiteStatus.OutOfService
                        : MapWarehouseSiteStatus.Operational,
                    row.Level,
                    row.CapacityModelId,
                    row.CapacityUnits,
                    row.UsedCapacity,
                    true);
            }

            var status = items.Length == 0 ? MapWarehouseStatus.Empty : MapWarehouseStatus.Ready;
            if (!state.HasCompany)
                status = MapWarehouseStatus.Empty;
            return new MapWarehouseSnapshot(status, status == MapWarehouseStatus.Empty ? new MapWarehouseItem[0] : items, "", false);
        }

        public static MapJobsSnapshot Jobs(ClientContractPage page)
        {
            var items = new MapJobsItem[page.Items.Length];
            for (var i = 0; i < page.Items.Length; i++)
            {
                var row = page.Items[i];
                items[i] = new MapJobsItem(
                    row.ContractId,
                    MapJobsItem.ParseContract(row.Status),
                    row.Expired,
                    row.OriginCityId,
                    row.DestinationCityId,
                    row.CargoId,
                    row.Quantity,
                    row.Unit,
                    row.Reward,
                    row.Currency,
                    row.Scale,
                    "",
                    MapJobDeliveryStatus.None,
                    "",
                    "",
                    row.ExpiresAt,
                    "",
                    "",
                    "",
                    MapCandidates(row.CandidateAssets),
                    row.CandidateAssetsPresent);
            }

            var status = items.Length == 0 ? MapJobsStatus.Empty : MapJobsStatus.Ready;
            return new MapJobsSnapshot(status, items, "", MapJobCategory.Available, false);
        }

        public static MapMarketSnapshot Market(ClientMarketState state)
        {
            var items = new MapMarketItem[state.Conditions.Length];
            for (var i = 0; i < state.Conditions.Length; i++)
            {
                var row = state.Conditions[i];
                items[i] = new MapMarketItem(
                    row.CityId,
                    row.CargoId,
                    true,
                    row.DemandIndex,
                    true,
                    row.SupplyIndex,
                    true,
                    row.AdjustmentBps,
                    row.TrendBps);
            }

            var status = items.Length == 0 ? MapMarketStatus.Empty : MapMarketStatus.Ready;
            return new MapMarketSnapshot(status, items, "", "", state.MarketVersion, state.PeriodEndsAt, false);
        }

        public static MapRoadDeliverySnapshot Road(ClientDeliveryPage page)
        {
            var items = new MapRoadDeliveryItem[page.Items.Length];
            var count = 0;
            for (var i = 0; i < page.Items.Length; i++)
            {
                var row = page.Items[i];
                if (!ClientRoadFilter.CanPresentAsRoad(row))
                    continue;
                var kinds = new string[row.Assets.Length];
                var ids = new string[row.Assets.Length];
                for (var a = 0; a < row.Assets.Length; a++)
                {
                    kinds[a] = row.Assets[a].Kind;
                    ids[a] = row.Assets[a].AssetId;
                }

                items[count] = new MapRoadDeliveryItem(
                    row.DeliveryId,
                    row.ContractId,
                    MapRoadDeliveryItem.Parse(row.Status),
                    row.OriginCityId,
                    row.DestinationCityId,
                    MapRoadDeliveryItem.TruckFromAssets(kinds, ids),
                    row.ExpectedArrivalAt,
                    row.ArrivalReady);
                count++;
            }

            if (count != items.Length)
            {
                var trimmed = new MapRoadDeliveryItem[count];
                for (var i = 0; i < count; i++)
                    trimmed[i] = items[i];
                items = trimmed;
            }

            var status = items.Length == 0 ? MapRoadDeliveryStatus.Empty : MapRoadDeliveryStatus.Ready;
            return new MapRoadDeliverySnapshot(status, items, "", false);
        }

        public static bool CityKnown(string cityId)
        {
            return MapCities.TryGet(cityId, out _);
        }

        static MapJobCandidate[] MapCandidates(ClientDeliveryAsset[] assets)
        {
            if (assets == null || assets.Length == 0)
                return new MapJobCandidate[0];
            var mapped = new MapJobCandidate[assets.Length];
            for (var i = 0; i < assets.Length; i++)
            {
                mapped[i] = new MapJobCandidate(
                    assets[i].AssetId,
                    MapFleetAsset.ParseKind(assets[i].Kind));
            }

            return mapped;
        }
    }
}
