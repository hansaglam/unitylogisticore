using UnityEngine;

namespace LogistiCore.Depot
{
    /// <summary>
    /// Separate authored sprite layers for future gate/door animation.
    /// This rig NEVER invokes backend commands or creates vehicle events.
    /// Evidence must come from authenticated, server-confirmed transitions.
    /// Dormant by default; safe to include in a review-only scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DepotMotionRig : MonoBehaviour
    {
        [SerializeField] Transform slidingGateLeaf;
        [SerializeField] Transform loadingDoorLeaf;
        [SerializeField] Vector3 gateTravel = new Vector3(1f, 0f, 0f);
        [SerializeField] Vector3 dockTravel = new Vector3(0f, 0.8f, 0f);
        [SerializeField, Min(0.01f)] float gateSpeed = 1.8f;
        [SerializeField, Min(0.01f)] float dockSpeed = 1.2f;

        Vector3 _gateRest;
        Vector3 _dockRest;
        bool _initialized;
        bool _gateTargetOpen;
        bool _dockTargetOpen;
        bool _showFacility;

        public bool GateTargetOpen => _gateTargetOpen;
        public bool DockTargetOpen => _dockTargetOpen;
        public bool ShowsFacility => _showFacility;

        void Awake()
        {
            CaptureRestPose();
            ApplyEvidence(default);
        }

        void OnDisable()
        {
            // No visual motion survives leaving the scene, including a
            // paused animation part-way through a transition.
            ResetForIdentityChange();
        }

        /// <summary>
        /// Must be invoked by the owning session adapter on account switch,
        /// sign-out or failed/expired authenticated data refresh. Does not
        /// confer or remove facility ownership; only clears visual movement.
        /// </summary>
        public void ResetForIdentityChange()
        {
            CaptureRestPose();
            _gateTargetOpen = false;
            _dockTargetOpen = false;
            _showFacility = false;
            RestoreRestPose();
        }

        public void CaptureRestPose()
        {
            if (_initialized)
                return;
            if (slidingGateLeaf != null)
                _gateRest = slidingGateLeaf.localPosition;
            if (loadingDoorLeaf != null)
                _dockRest = loadingDoorLeaf.localPosition;
            _initialized = true;
        }

        public void ApplyEvidence(DepotMotionEvidence evidence)
        {
            CaptureRestPose();
            var state = DepotMotionPolicy.Resolve(evidence);
            _showFacility = state.ShowFacility;
            _gateTargetOpen = state.OpenGate;
            _dockTargetOpen = state.OpenDock;
            if (!_showFacility)
                RestoreRestPose();
            // Visibility is owned by a separate verified site layer. Never
            // automatically enable SpriteRenderers as proof of ownership.
        }

        void Update()
        {
            if (!_initialized || !_showFacility)
                return;
            if (slidingGateLeaf != null)
            {
                var target = _gateRest +
                    (_gateTargetOpen ? gateTravel : Vector3.zero);
                slidingGateLeaf.localPosition = Vector3.MoveTowards(
                    slidingGateLeaf.localPosition, target,
                    Mathf.Max(0.01f, gateSpeed) * Time.deltaTime);
            }
            if (loadingDoorLeaf != null)
            {
                var target = _dockRest +
                    (_dockTargetOpen ? dockTravel : Vector3.zero);
                loadingDoorLeaf.localPosition = Vector3.MoveTowards(
                    loadingDoorLeaf.localPosition, target,
                    Mathf.Max(0.01f, dockSpeed) * Time.deltaTime);
            }
        }

        void RestoreRestPose()
        {
            if (slidingGateLeaf != null)
                slidingGateLeaf.localPosition = _gateRest;
            if (loadingDoorLeaf != null)
                loadingDoorLeaf.localPosition = _dockRest;
        }
    }
}
