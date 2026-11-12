namespace LogistiCore.Map
{
    /// <summary>
    /// Fleet read model. Runtime never fills vehicles. Test/capture fixtures must set
    /// <see cref="Synthetic"/>.
    /// </summary>
    public readonly struct MapFleetSnapshot
    {
        static readonly MapFleetAsset[] None = new MapFleetAsset[0];

        public MapFleetSnapshot(MapFleetStatus status, MapFleetAsset[] assets, string selectedId, bool synthetic)
        {
            Status = status;
            Assets = assets ?? None;
            SelectedId = selectedId ?? "";
            Synthetic = synthetic;
        }

        public MapFleetStatus Status { get; }
        public MapFleetAsset[] Assets { get; }
        public string SelectedId { get; }
        public bool Synthetic { get; }

        public int VehicleCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Assets.Length; i++)
                {
                    if (Assets[i].IsVehicle)
                        count++;
                }

                return count;
            }
        }

        public int AvailableVehicleCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Assets.Length; i++)
                {
                    if (Assets[i].IsVehicle && Assets[i].Available)
                        count++;
                }

                return count;
            }
        }

        public bool TryGet(string assetId, out MapFleetAsset asset)
        {
            for (var i = 0; i < Assets.Length; i++)
            {
                if (Assets[i].AssetId == assetId)
                {
                    asset = Assets[i];
                    return true;
                }
            }

            asset = default;
            return false;
        }

        public MapFleetSnapshot WithSelected(string assetId)
        {
            var id = assetId ?? "";
            if (!string.IsNullOrEmpty(id) && !TryGet(id, out _))
                id = "";
            return new MapFleetSnapshot(Status, Assets, id, Synthetic);
        }

        public static MapFleetSnapshot Unconfigured()
        {
            return new MapFleetSnapshot(MapFleetStatus.Unconfigured, None, "", false);
        }

        public static MapFleetSnapshot Loading()
        {
            return new MapFleetSnapshot(MapFleetStatus.Loading, None, "", false);
        }

        public static MapFleetSnapshot Error()
        {
            return new MapFleetSnapshot(MapFleetStatus.Error, None, "", false);
        }

        public static MapFleetSnapshot Fixture(MapFleetStatus status, MapFleetAsset[] assets, string selectedId = "")
        {
            var list = assets ?? None;
            if (status == MapFleetStatus.Ready && VehicleCountOf(list) == 0)
                status = MapFleetStatus.Empty;
            if (status == MapFleetStatus.Empty)
                list = None;
            var id = selectedId ?? "";
            if (!string.IsNullOrEmpty(id))
            {
                var found = false;
                for (var i = 0; i < list.Length; i++)
                {
                    if (list[i].AssetId == id)
                        found = true;
                }

                if (!found)
                    id = "";
            }

            return new MapFleetSnapshot(status, list, id, true);
        }

        static int VehicleCountOf(MapFleetAsset[] assets)
        {
            var count = 0;
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i].IsVehicle)
                    count++;
            }

            return count;
        }
    }
}
