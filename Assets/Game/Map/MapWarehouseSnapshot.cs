namespace LogistiCore.Map
{
    public readonly struct MapWarehouseSnapshot
    {
        static readonly MapWarehouseItem[] None = new MapWarehouseItem[0];

        public MapWarehouseSnapshot(
            MapWarehouseStatus status,
            MapWarehouseItem[] items,
            string selectedId,
            bool synthetic)
        {
            Status = status;
            Items = Dedup(items);
            SelectedId = selectedId ?? "";
            Synthetic = synthetic;
            if (!string.IsNullOrEmpty(SelectedId) && !TryGetOwned(SelectedId, out _))
                SelectedId = "";
        }

        public MapWarehouseStatus Status { get; }
        public MapWarehouseItem[] Items { get; }
        public string SelectedId { get; }
        public bool Synthetic { get; }
        public bool OffersSupported => false;

        public int OwnedCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Items.Length; i++)
                {
                    if (Items[i].Owned)
                        count++;
                }

                return count;
            }
        }

        public bool TryGet(string warehouseId, out MapWarehouseItem item)
        {
            for (var i = 0; i < Items.Length; i++)
            {
                if (Items[i].WarehouseId == warehouseId)
                {
                    item = Items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        public bool TryGetOwned(string warehouseId, out MapWarehouseItem item)
        {
            if (!TryGet(warehouseId, out item))
                return false;
            if (!item.Owned)
            {
                item = default;
                return false;
            }

            return true;
        }

        public MapWarehouseSnapshot WithSelected(string warehouseId)
        {
            return new MapWarehouseSnapshot(Status, Items, warehouseId, Synthetic);
        }

        public static MapWarehouseSnapshot Unconfigured()
        {
            return new MapWarehouseSnapshot(MapWarehouseStatus.Unconfigured, None, "", false);
        }

        public static MapWarehouseSnapshot Loading()
        {
            return new MapWarehouseSnapshot(MapWarehouseStatus.Loading, None, "", false);
        }

        public static MapWarehouseSnapshot Error()
        {
            return new MapWarehouseSnapshot(MapWarehouseStatus.Error, None, "", false);
        }

        public static MapWarehouseSnapshot Fixture(
            MapWarehouseStatus status,
            MapWarehouseItem[] items,
            string selectedId = "")
        {
            var list = Dedup(items);
            if (status == MapWarehouseStatus.Ready && CountOwned(list) == 0 && AllOwnedOrEmpty(list))
                status = MapWarehouseStatus.Empty;
            if (status == MapWarehouseStatus.Empty)
                list = None;
            return new MapWarehouseSnapshot(status, list, selectedId, true);
        }

        static int CountOwned(MapWarehouseItem[] items)
        {
            var count = 0;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].Owned)
                    count++;
            }

            return count;
        }

        static bool AllOwnedOrEmpty(MapWarehouseItem[] items)
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (!items[i].Owned)
                    return false;
            }

            return true;
        }

        static MapWarehouseItem[] Dedup(MapWarehouseItem[] items)
        {
            if (items == null || items.Length == 0)
                return None;

            var unique = new MapWarehouseItem[items.Length];
            var count = 0;
            for (var i = 0; i < items.Length; i++)
            {
                var id = items[i].WarehouseId;
                if (string.IsNullOrEmpty(id))
                    continue;
                var seen = false;
                for (var j = 0; j < count; j++)
                {
                    if (unique[j].WarehouseId == id)
                        seen = true;
                }

                if (seen)
                    continue;
                unique[count] = items[i];
                count++;
            }

            if (count == items.Length)
                return unique;
            var trimmed = new MapWarehouseItem[count];
            for (var i = 0; i < count; i++)
                trimmed[i] = unique[i];
            return trimmed;
        }
    }
}
