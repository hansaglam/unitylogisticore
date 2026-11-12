namespace LogistiCore.Map
{
    public enum MapFleetStatus
    {
        Unconfigured = 0,
        Loading = 1,
        Empty = 2,
        Ready = 3,
        Error = 4
    }

    public enum MapFleetKind
    {
        Unknown = 0,
        Truck = 1,
        Driver = 2,
        Trailer = 3
    }

    public enum MapFleetAssetStatus
    {
        Unknown = 0,
        Operational = 1,
        OutOfService = 2
    }
}
