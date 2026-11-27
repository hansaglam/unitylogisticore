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
        public void AccountSwitchClosesPendingGateAndDockVisuals()
        {
            var go = new GameObject("DepotSessionResetTest");
            var gate = new GameObject("MovableGate").transform;
            var door = new GameObject("MovableDoor").transform;
            try
            {
                gate.SetParent(go.transform, false);
                door.SetParent(go.transform, false);
                gate.localPosition = new Vector3(2f, -1f, 0f);
                door.localPosition = new Vector3(-2f, 1f, 0f);
                var rig = go.AddComponent<DepotMotionRig>();
                var flags = System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance;
                // The rig is isolated from a live backend/scene; bind only
                // local dummy sprite transforms and explicitly reset rest.
                rig.ResetForIdentityChange();
                typeof(DepotMotionRig).GetField("slidingGateLeaf", flags)
                    .SetValue(rig, gate);
                typeof(DepotMotionRig).GetField("loadingDoorLeaf", flags)
                    .SetValue(rig, door);
                typeof(DepotMotionRig).GetField("_initialized", flags)
                    .SetValue(rig, false);
                rig.CaptureRestPose();

                rig.ApplyEvidence(new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, true, true));
                Assert.IsTrue(rig.GateTargetOpen);
                Assert.IsTrue(rig.DockTargetOpen);
                gate.localPosition = new Vector3(3f, -1f, 0f);
                door.localPosition = new Vector3(-2f, 1.8f, 0f);

                rig.ResetForIdentityChange();
                Assert.IsFalse(rig.ShowsFacility);
                Assert.IsFalse(rig.GateTargetOpen);
                Assert.IsFalse(rig.DockTargetOpen);
                Assert.AreEqual(new Vector3(2f, -1f, 0f),
                    gate.localPosition);
                Assert.AreEqual(new Vector3(-2f, 1f, 0f),
                    door.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GateAndDockAdvanceDeterministicallyAndResetOnLossOfEvidence()
        {
            var root = new GameObject("MotionStepRoot");
            var gate = new GameObject("GateLeaf").transform;
            var dock = new GameObject("DockLeaf").transform;
            try
            {
                gate.SetParent(root.transform, false);
                dock.SetParent(root.transform, false);
                gate.localPosition = new Vector3(-1f, -0.2f, 0f);
                dock.localPosition = new Vector3(0.8f, 0.2f, 0f);
                var rig = root.AddComponent<DepotMotionRig>();
                var binding = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic;
                typeof(DepotMotionRig).GetField("slidingGateLeaf", binding)
                    .SetValue(rig, gate);
                typeof(DepotMotionRig).GetField("loadingDoorLeaf", binding)
                    .SetValue(rig, dock);
                typeof(DepotMotionRig).GetField("_initialized", binding)
                    .SetValue(rig, false);
                rig.CaptureRestPose();

                rig.Advance(1f);
                Assert.AreEqual(new Vector3(-1f, -0.2f, 0f), gate.localPosition);
                Assert.AreEqual(new Vector3(0.8f, 0.2f, 0f), dock.localPosition);

                rig.ApplyEvidence(new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, true, true));
                rig.Advance(0.5f);
                Assert.Less(Vector3.Distance(gate.localPosition,
                    new Vector3(0f, -0.2f, 0f)), 0.11f);
                Assert.Less(Vector3.Distance(dock.localPosition,
                    new Vector3(0.8f, 0.8f, 0f)), 0.01f);
                rig.Advance(5f);
                Assert.AreEqual(new Vector3(0f, -0.2f, 0f), gate.localPosition);
                Assert.AreEqual(new Vector3(0.8f, 1f, 0f), dock.localPosition);

                rig.ApplyEvidence(new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, false, false));
                rig.Advance(5f);
                Assert.AreEqual(new Vector3(-1f, -0.2f, 0f), gate.localPosition);
                Assert.AreEqual(new Vector3(0.8f, 0.2f, 0f), dock.localPosition);

                rig.ApplyEvidence(new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, true, true));
                rig.Advance(0.15f);
                rig.ApplyEvidence(default);
                Assert.AreEqual(new Vector3(-1f, -0.2f, 0f), gate.localPosition);
                Assert.AreEqual(new Vector3(0.8f, 0.2f, 0f), dock.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidDeltaNeverMovesSpriteLeaves()
        {
            var root = new GameObject("MotionInvalidDelta");
            var leaf = new GameObject("Leaf").transform;
            try
            {
                leaf.SetParent(root.transform, false);
                var rig = root.AddComponent<DepotMotionRig>();
                var binding = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic;
                typeof(DepotMotionRig).GetField("slidingGateLeaf", binding)
                    .SetValue(rig, leaf);
                typeof(DepotMotionRig).GetField("_initialized", binding)
                    .SetValue(rig, false);
                rig.CaptureRestPose();
                rig.ApplyEvidence(new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, true, false));
                rig.Advance(float.NaN);
                rig.Advance(float.PositiveInfinity);
                rig.Advance(-1f);
                rig.Advance(0f);
                Assert.AreEqual(Vector3.zero, leaf.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
