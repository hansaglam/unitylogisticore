namespace LogistiCore.Map
{
    public readonly struct MapMarketSnapshot
    {
        static readonly MapMarketItem[] None = new MapMarketItem[0];

        public MapMarketSnapshot(
            MapMarketStatus status,
            MapMarketItem[] items,
            string selectedId,
            string cargoFilter,
            string marketVersion,
            string periodEndsAt,
            bool synthetic)
        {
            Status = status;
            Items = Dedup(items);
            SelectedId = selectedId ?? "";
            CargoFilter = cargoFilter ?? "";
            MarketVersion = marketVersion ?? "";
            PeriodEndsAt = periodEndsAt ?? "";
            Synthetic = synthetic;
            if (!string.IsNullOrEmpty(SelectedId) && !TryGet(SelectedId, out _))
                SelectedId = "";
        }

        public MapMarketStatus Status { get; }
        public MapMarketItem[] Items { get; }
        public string SelectedId { get; }
        public string CargoFilter { get; }
        public string MarketVersion { get; }
        public string PeriodEndsAt { get; }
        public bool Synthetic { get; }
        public bool HasPriceFeed => false;
        public bool HasPeriodEnd => !string.IsNullOrEmpty(PeriodEndsAt);

        public int CountIn(string cargoFilter)
        {
            var count = 0;
            for (var i = 0; i < Items.Length; i++)
            {
                if (Items[i].InCargo(cargoFilter))
                    count++;
            }

            return count;
        }

        public bool TryGet(string conditionId, out MapMarketItem item)
        {
            for (var i = 0; i < Items.Length; i++)
            {
                if (Items[i].ConditionId == conditionId)
                {
                    item = Items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        public MapMarketSnapshot WithSelected(string conditionId)
        {
            return new MapMarketSnapshot(Status, Items, conditionId, CargoFilter, MarketVersion, PeriodEndsAt, Synthetic);
        }

        public MapMarketSnapshot WithCargo(string cargoFilter)
        {
            return new MapMarketSnapshot(Status, Items, SelectedId, cargoFilter, MarketVersion, PeriodEndsAt, Synthetic);
        }

        public static MapMarketSnapshot Unconfigured()
        {
            return new MapMarketSnapshot(MapMarketStatus.Unconfigured, None, "", "", "", "", false);
        }

        public static MapMarketSnapshot Loading()
        {
            return new MapMarketSnapshot(MapMarketStatus.Loading, None, "", "", "", "", false);
        }

        public static MapMarketSnapshot Error()
        {
            return new MapMarketSnapshot(MapMarketStatus.Error, None, "", "", "", "", false);
        }

        public static MapMarketSnapshot Fixture(
            MapMarketStatus status,
            MapMarketItem[] items,
            string selectedId = "",
            string cargoFilter = "",
            string marketVersion = "synthetic_fixture",
            string periodEndsAt = "")
        {
            var list = Dedup(items);
            if (status == MapMarketStatus.Ready && list.Length == 0)
                status = MapMarketStatus.Empty;
            if (status == MapMarketStatus.Empty)
                list = None;
            return new MapMarketSnapshot(status, list, selectedId, cargoFilter, marketVersion, periodEndsAt, true);
        }

        static MapMarketItem[] Dedup(MapMarketItem[] items)
        {
            if (items == null || items.Length == 0)
                return None;

            var unique = new MapMarketItem[items.Length];
            var count = 0;
            for (var i = 0; i < items.Length; i++)
            {
                var id = items[i].ConditionId;
                if (string.IsNullOrEmpty(items[i].CityId) && string.IsNullOrEmpty(items[i].CargoId))
                    continue;
                var seen = false;
                for (var j = 0; j < count; j++)
                {
                    if (unique[j].ConditionId == id)
                        seen = true;
                }

                if (seen)
                    continue;
                unique[count] = items[i];
                count++;
            }

            if (count == items.Length)
                return unique;
            var trimmed = new MapMarketItem[count];
            for (var i = 0; i < count; i++)
                trimmed[i] = unique[i];
            return trimmed;
        }
    }
}
