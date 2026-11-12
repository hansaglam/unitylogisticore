namespace LogistiCore.Map
{
    public enum MapWarehouseStatus
    {
        Unconfigured = 0,
        Loading = 1,
        Empty = 2,
        Ready = 3,
        Error = 4
    }

    public enum MapWarehouseSiteStatus
    {
        Unknown = 0,
        Operational = 1,
        OutOfService = 2
    }
}
