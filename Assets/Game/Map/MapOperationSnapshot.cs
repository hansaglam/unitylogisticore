namespace LogistiCore.Map
{
    /// <summary>
    /// Footer read model. Runtime never fills active operations. Local fixtures set
    /// <see cref="Synthetic"/>. A future verified server read may set
    /// <see cref="Verified"/>. Unconfigured live data enables neither.
    /// </summary>
    public readonly struct MapOperationSnapshot
    {
        public MapOperationSnapshot(
            MapOperationStatus status,
            int activeCount,
            string origin,
            string destination,
            string expectedArrival,
            bool synthetic,
            bool verified = false)
        {
            Status = status;
            ActiveCount = activeCount < 0 ? 0 : activeCount;
            Origin = origin ?? "";
            Destination = destination ?? "";
            ExpectedArrival = expectedArrival ?? "";
            Synthetic = synthetic;
            Verified = verified && !synthetic;
        }

        public MapOperationStatus Status { get; }
        public int ActiveCount { get; }
        public string Origin { get; }
        public string Destination { get; }
        public string ExpectedArrival { get; }
        public bool Synthetic { get; }
        public bool Verified { get; }

        /// <summary>
        /// Trusted local fixture or a future verified server read. Unconfigured and
        /// unverified live snapshots never enable the footer action.
        /// </summary>
        public bool TrustedRead => Synthetic || Verified;

        public bool CanOpenJobs =>
            TrustedRead
            && (Status == MapOperationStatus.SingleActive || Status == MapOperationStatus.MultipleActive);

        public static MapOperationSnapshot Unconfigured()
        {
            return new MapOperationSnapshot(MapOperationStatus.Unconfigured, 0, "", "", "", false);
        }

        public static MapOperationSnapshot Loading()
        {
            return new MapOperationSnapshot(MapOperationStatus.Loading, 0, "", "", "", false);
        }

        public static MapOperationSnapshot Error()
        {
            return new MapOperationSnapshot(MapOperationStatus.Error, 0, "", "", "", false);
        }

        public static MapOperationSnapshot Fixture(
            MapOperationStatus status,
            int activeCount = 0,
            string origin = "",
            string destination = "",
            string expectedArrival = "")
        {
            return new MapOperationSnapshot(status, activeCount, origin, destination, expectedArrival, true);
        }

        /// <summary>
        /// Contract placeholder for a future verified operations read. Unity has no
        /// producer this phase. Not used by FirebaseClient.
        /// </summary>
        public static MapOperationSnapshot VerifiedRead(
            MapOperationStatus status,
            int activeCount = 0,
            string origin = "",
            string destination = "",
            string expectedArrival = "")
        {
            return new MapOperationSnapshot(status, activeCount, origin, destination, expectedArrival, false, true);
        }
    }
}
