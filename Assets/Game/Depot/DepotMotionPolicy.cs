namespace LogistiCore.Depot
{
    /// <summary>
    /// Visual-only, fail-closed motion decision. A verified warehouse alone
    /// never implies active dispatch, a vehicle arrival or a loading bay.
    /// Callers must derive event flags from authenticated, server-confirmed
    /// operation transitions; scene previews cannot confer ownership.
    /// </summary>
    public readonly struct DepotMotionEvidence
    {
        public readonly bool VerifiedRead;
        public readonly DepotVisualStage FacilityStage;
        public readonly bool ConfirmedGateOpen;
        public readonly bool ConfirmedDockOpen;

        public DepotMotionEvidence(bool verifiedRead, DepotVisualStage facilityStage,
            bool confirmedGateOpen, bool confirmedDockOpen)
        {
            VerifiedRead = verifiedRead;
            FacilityStage = facilityStage;
            ConfirmedGateOpen = confirmedGateOpen;
            ConfirmedDockOpen = confirmedDockOpen;
        }
    }

    public readonly struct DepotMotionVisualState
    {
        public readonly bool ShowFacility;
        public readonly bool OpenGate;
        public readonly bool OpenDock;

        public DepotMotionVisualState(bool showFacility, bool openGate,
            bool openDock)
        {
            ShowFacility = showFacility;
            OpenGate = openGate;
            OpenDock = openDock;
        }
    }

    public static class DepotMotionPolicy
    {
        public static DepotMotionVisualState Resolve(DepotMotionEvidence evidence)
        {
            if (!evidence.VerifiedRead ||
                evidence.FacilityStage != DepotVisualStage.StorageOperational)
                return default;

            return new DepotMotionVisualState(true,
                evidence.ConfirmedGateOpen, evidence.ConfirmedDockOpen);
        }
    }
}
