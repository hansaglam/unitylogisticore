using System;
using UnityEngine;

namespace LogistiCore.Depot
{
    /// <summary>
    /// The ONLY presentation gate for an approved 2D depot sprite hierarchy.
    /// Do not infer ownership from the presence of art in a Unity scene.
    /// The caller supplies a fresh authenticated depot state and separately
    /// verified operation events. There are no server writes or fake trucks.
    ///
    /// This component remains unbound to the live scene until the ART-02D
    /// source sprites, scale and movement paths pass visual/device QA.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DepotVerifiedVisualAssembly : MonoBehaviour
    {
        [SerializeField] SpriteRenderer[] siteRenderers = Array.Empty<SpriteRenderer>();
        // Foundation/pad artwork is visible ONLY while activation is ready.
        // Keep it in a distinct collection: a foundation must never be
        // mistaken for a fully commissioned warehouse.
        [SerializeField] SpriteRenderer[] activationRenderers = Array.Empty<SpriteRenderer>();
        [SerializeField] SpriteRenderer[] warehouseRenderers = Array.Empty<SpriteRenderer>();
        [SerializeField] SpriteRenderer[] operationalDetails = Array.Empty<SpriteRenderer>();
        [SerializeField] DepotMotionRig motionRig;

        public DepotVisualStage VisibleStage { get; private set; } =
            DepotVisualStage.Unverified;

        void Awake()
        {
            ClearVisualState();
        }

        void OnDisable()
        {
            ClearVisualState();
        }

        /// <summary>
        /// Apply a trusted, freshly verified visual stage. All authorization
        /// decisions are upstream. This method does not grant access or
        /// activate a warehouse; it only changes visual presentation.
        /// </summary>
        public void Apply(DepotMotionEvidence evidence)
        {
            if (!evidence.VerifiedRead)
            {
                ClearVisualState();
                return;
            }

            // Only known real stages are shown. Invalid enums fail closed.
            switch (evidence.FacilityStage)
            {
                case DepotVisualStage.ReservedParcel:
                case DepotVisualStage.ActivationReady:
                case DepotVisualStage.StorageOperational:
                    break;
                default:
                    ClearVisualState();
                    return;
            }

            VisibleStage = evidence.FacilityStage;
            SetVisible(siteRenderers, true);
            var activationReady =
                evidence.FacilityStage == DepotVisualStage.ActivationReady;
            var hasWarehouse =
                evidence.FacilityStage == DepotVisualStage.StorageOperational;
            SetVisible(activationRenderers, activationReady);
            SetVisible(warehouseRenderers, hasWarehouse);
            SetVisible(operationalDetails, hasWarehouse);

            // Even if a caller accidentally supplies true event flags for a
            // reserved parcel, DepotMotionPolicy will refuse to animate it.
            if (motionRig != null)
                motionRig.ApplyEvidence(evidence);
        }

        /// <summary>
        /// Mandatory on sign-out, account switches, expired responses and
        /// scene teardown. Also called on Awake/OnDisable.
        /// </summary>
        public void ClearVisualState()
        {
            VisibleStage = DepotVisualStage.Unverified;
            SetVisible(siteRenderers, false);
            SetVisible(activationRenderers, false);
            SetVisible(warehouseRenderers, false);
            SetVisible(operationalDetails, false);
            if (motionRig != null)
                motionRig.ResetForIdentityChange();
        }

        static void SetVisible(SpriteRenderer[] renderers, bool visible)
        {
            if (renderers == null)
                return;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
            }
        }
    }
}
