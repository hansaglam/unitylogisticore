namespace LogistiCore.Map
{
    public readonly struct MapJobsSnapshot
    {
        static readonly MapJobsItem[] None = new MapJobsItem[0];

        public MapJobsSnapshot(
            MapJobsStatus status,
            MapJobsItem[] items,
            string selectedId,
            MapJobCategory category,
            bool synthetic)
        {
            Status = status;
            Items = Dedup(items);
            SelectedId = selectedId ?? "";
            Category = category;
            Synthetic = synthetic;
            if (!string.IsNullOrEmpty(SelectedId)
                && (!TryGet(SelectedId, out var selected) || !selected.InCategory(Category)))
                SelectedId = "";
        }

        public MapJobsStatus Status { get; }
        public MapJobsItem[] Items { get; }
        public string SelectedId { get; }
        public MapJobCategory Category { get; }
        public bool Synthetic { get; }

        public int CountIn(MapJobCategory category)
        {
            var count = 0;
            for (var i = 0; i < Items.Length; i++)
            {
                if (Items[i].InCategory(category))
                    count++;
            }

            return count;
        }

        public bool TryGet(string contractId, out MapJobsItem item)
        {
            for (var i = 0; i < Items.Length; i++)
            {
                if (Items[i].ContractId == contractId)
                {
                    item = Items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        public MapJobsSnapshot WithSelected(string contractId)
        {
            return new MapJobsSnapshot(Status, Items, contractId, Category, Synthetic);
        }

        public MapJobsSnapshot WithCategory(MapJobCategory category)
        {
            var selected = SelectedId;
            if (TryGet(selected, out var item) && item.InCategory(category))
                return new MapJobsSnapshot(Status, Items, selected, category, Synthetic);
            return new MapJobsSnapshot(Status, Items, FirstIdIn(category), category, Synthetic);
        }

        public MapJobsSnapshot Reconcile(MapJobCategory category, string selectedId)
        {
            var selected = selectedId ?? "";
            if (TryGet(selected, out var item) && item.InCategory(category))
                return new MapJobsSnapshot(Status, Items, selected, category, Synthetic);
            return new MapJobsSnapshot(Status, Items, "", category, Synthetic);
        }

        string FirstIdIn(MapJobCategory category)
        {
            for (var i = 0; i < Items.Length; i++)
            {
                if (Items[i].InCategory(category))
                    return Items[i].ContractId;
            }

            return "";
        }

        public static MapJobsSnapshot Unconfigured()
        {
            return new MapJobsSnapshot(MapJobsStatus.Unconfigured, None, "", MapJobCategory.Available, false);
        }

        public static MapJobsSnapshot Loading()
        {
            return new MapJobsSnapshot(MapJobsStatus.Loading, None, "", MapJobCategory.Available, false);
        }

        public static MapJobsSnapshot Error()
        {
            return new MapJobsSnapshot(MapJobsStatus.Error, None, "", MapJobCategory.Available, false);
        }

        public static MapJobsSnapshot Fixture(
            MapJobsStatus status,
            MapJobsItem[] items,
            string selectedId = "",
            MapJobCategory category = MapJobCategory.Available)
        {
            var list = Dedup(items);
            if (status == MapJobsStatus.Ready && list.Length == 0)
                status = MapJobsStatus.Empty;
            if (status == MapJobsStatus.Empty)
                list = None;
            return new MapJobsSnapshot(status, list, selectedId, category, true);
        }

        static MapJobsItem[] Dedup(MapJobsItem[] items)
        {
            if (items == null || items.Length == 0)
                return None;

            var unique = new MapJobsItem[items.Length];
            var count = 0;
            for (var i = 0; i < items.Length; i++)
            {
                var id = items[i].ContractId;
                if (string.IsNullOrEmpty(id))
                    continue;
                var seen = false;
                for (var j = 0; j < count; j++)
                {
                    if (unique[j].ContractId == id)
                        seen = true;
                }

                if (seen)
                    continue;
                unique[count] = items[i];
                count++;
            }

            if (count == items.Length)
                return unique;
            var trimmed = new MapJobsItem[count];
            for (var i = 0; i < count; i++)
                trimmed[i] = unique[i];
            return trimmed;
        }
    }
}
