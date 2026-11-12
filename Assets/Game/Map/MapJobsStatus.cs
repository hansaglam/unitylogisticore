namespace LogistiCore.Map
{
    public enum MapJobsStatus
    {
        Unconfigured = 0,
        Loading = 1,
        Empty = 2,
        Ready = 3,
        Error = 4
    }

    public enum MapJobContractStatus
    {
        Unknown = 0,
        Offered = 1,
        Accepted = 2,
        InProgress = 3,
        Completed = 4,
        Failed = 5
    }

    public enum MapJobDeliveryStatus
    {
        None = 0,
        Active = 1,
        Completed = 2
    }

    public enum MapJobCategory
    {
        Available = 0,
        Active = 1,
        History = 2
    }
}
