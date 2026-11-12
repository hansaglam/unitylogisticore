namespace LogistiCore.Map
{
    public readonly struct MapRoadDeliverySnapshot
    {
        static readonly MapRoadDeliveryItem[] None = new MapRoadDeliveryItem[0];

        public MapRoadDeliverySnapshot(
            MapRoadDeliveryStatus status,
            MapRoadDeliveryItem[] items,
            string selectedId,
            bool synthetic)
        {
            Status = status;
            Items = ActiveOnly(items);
            SelectedId = selectedId ?? "";
            Synthetic = synthetic;
            if (!string.IsNullOrEmpty(SelectedId) && !TryGet(SelectedId, out _))
                SelectedId = "";
        }

        public MapRoadDeliveryStatus Status { get; }
        public MapRoadDeliveryItem[] Items { get; }
        public string SelectedId { get; }
        public bool Synthetic { get; }

        public bool TryGet(string deliveryId, out MapRoadDeliveryItem item)
        {
            for (var i = 0; i < Items.Length; i++)
            {
                if (Items[i].DeliveryId == deliveryId)
                {
                    item = Items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        public MapRoadDeliverySnapshot WithSelected(string deliveryId)
        {
            return new MapRoadDeliverySnapshot(Status, Items, deliveryId, Synthetic);
        }

        public static MapRoadDeliverySnapshot Unconfigured()
        {
            return new MapRoadDeliverySnapshot(MapRoadDeliveryStatus.Unconfigured, None, "", false);
        }

        public static MapRoadDeliverySnapshot Loading()
        {
            return new MapRoadDeliverySnapshot(MapRoadDeliveryStatus.Loading, None, "", false);
        }

        public static MapRoadDeliverySnapshot Error()
        {
            return new MapRoadDeliverySnapshot(MapRoadDeliveryStatus.Error, None, "", false);
        }

        public static MapRoadDeliverySnapshot Fixture(MapRoadDeliveryStatus status, MapRoadDeliveryItem[] items, string selectedId = "")
        {
            var list = ActiveOnly(items);
            if (status == MapRoadDeliveryStatus.Ready && list.Length == 0)
                status = MapRoadDeliveryStatus.Empty;
            if (status == MapRoadDeliveryStatus.Empty)
                list = None;
            return new MapRoadDeliverySnapshot(status, list, selectedId, true);
        }

        static MapRoadDeliveryItem[] ActiveOnly(MapRoadDeliveryItem[] items)
        {
            if (items == null || items.Length == 0)
                return None;

            var unique = new MapRoadDeliveryItem[items.Length];
            var count = 0;
            for (var i = 0; i < items.Length; i++)
            {
                var row = items[i];
                if (row.Lifecycle != MapRoadLifecycle.Active || string.IsNullOrEmpty(row.DeliveryId))
                    continue;
                var seen = false;
                for (var j = 0; j < count; j++)
                {
                    if (unique[j].DeliveryId == row.DeliveryId)
                        seen = true;
                }

                if (seen)
                    continue;
                unique[count] = row;
                count++;
            }

            if (count == 0)
                return None;
            if (count == unique.Length)
                return unique;
            var trimmed = new MapRoadDeliveryItem[count];
            for (var i = 0; i < count; i++)
                trimmed[i] = unique[i];
            return trimmed;
        }
    }
}
