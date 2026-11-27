using LogistiCore.Depot;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class DepotMotionPolicyTests
    {
        [Test]
        public void NoMotionWithoutVerifiedOperationalWarehouse()
        {
            var stages = new[]
            {
                DepotVisualStage.Unverified,
                DepotVisualStage.ReservedParcel,
                DepotVisualStage.ActivationReady
            };
            foreach (var stage in stages)
            {
                var result = DepotMotionPolicy.Resolve(
                    new DepotMotionEvidence(true, stage, true, true));
                Assert.IsFalse(result.ShowFacility);
                Assert.IsFalse(result.OpenGate);
                Assert.IsFalse(result.OpenDock);
            }
            var unverified = DepotMotionPolicy.Resolve(
                new DepotMotionEvidence(false,
                    DepotVisualStage.StorageOperational, true, true));
            Assert.IsFalse(unverified.ShowFacility);
            Assert.IsFalse(unverified.OpenGate);
            Assert.IsFalse(unverified.OpenDock);
        }

        [Test]
        public void OperationalWarehouseAloneDoesNotAnimateAnything()
        {
            var result = DepotMotionPolicy.Resolve(
                new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, false, false));
            Assert.IsTrue(result.ShowFacility);
            Assert.IsFalse(result.OpenGate);
            Assert.IsFalse(result.OpenDock);
        }

        [Test]
        public void ConfirmedActionsAreIndependent()
        {
            var gate = DepotMotionPolicy.Resolve(
                new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, true, false));
            Assert.IsTrue(gate.OpenGate);
            Assert.IsFalse(gate.OpenDock);
            var dock = DepotMotionPolicy.Resolve(
                new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, false, true));
            Assert.IsFalse(dock.OpenGate);
            Assert.IsTrue(dock.OpenDock);
        }

        [Test]
        public void FreshRigNeverInventsOpenEvents()
        {
            var go = new GameObject("DepotMotionRigSafety",
                typeof(DepotMotionRig));
            try
            {
                var rig = go.GetComponent<DepotMotionRig>();
                rig.ApplyEvidence(new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, false, false));
                Assert.IsTrue(rig.ShowsFacility);
                Assert.IsFalse(rig.GateTargetOpen);
                Assert.IsFalse(rig.DockTargetOpen);
                rig.ApplyEvidence(default);
                Assert.IsFalse(rig.ShowsFacility);
                Assert.IsFalse(rig.GateTargetOpen);
                Assert.IsFalse(rig.DockTargetOpen);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
