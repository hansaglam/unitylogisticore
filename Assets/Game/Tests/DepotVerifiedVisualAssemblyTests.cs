using System.Reflection;
using LogistiCore.Depot;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class DepotVerifiedVisualAssemblyTests
    {
        static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(obj, value);
        }

        [Test]
        public void UnverifiedPlayersNeverRevealAnySprites()
        {
            var root = new GameObject("DepotAssemblyTest");
            try
            {
                var site = new GameObject("Site", typeof(SpriteRenderer))
                    .GetComponent<SpriteRenderer>();
                var warehouse = new GameObject("Warehouse",
                    typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                site.transform.SetParent(root.transform, false);
                warehouse.transform.SetParent(root.transform, false);
                var assembly = root.AddComponent<DepotVerifiedVisualAssembly>();
                SetField(assembly, "siteRenderers", new[] { site });
                SetField(assembly, "warehouseRenderers", new[] { warehouse });

                assembly.Apply(new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, false, false));
                Assert.IsTrue(site.enabled);
                Assert.IsTrue(warehouse.enabled);

                assembly.Apply(new DepotMotionEvidence(false,
                    DepotVisualStage.StorageOperational, true, true));
                Assert.IsFalse(site.enabled);
                Assert.IsFalse(warehouse.enabled);
                Assert.AreEqual(DepotVisualStage.Unverified,
                    assembly.VisibleStage);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReservedAndActivationStagesHaveNoWarehouse()
        {
            var root = new GameObject("DepotStageAssemblyTest");
            try
            {
                var site = new GameObject("Site", typeof(SpriteRenderer))
                    .GetComponent<SpriteRenderer>();
                var warehouse = new GameObject("Warehouse",
                    typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                site.transform.SetParent(root.transform, false);
                warehouse.transform.SetParent(root.transform, false);
                var assembly = root.AddComponent<DepotVerifiedVisualAssembly>();
                SetField(assembly, "siteRenderers", new[] { site });
                SetField(assembly, "warehouseRenderers", new[] { warehouse });

                foreach (var stage in new[]
                {
                    DepotVisualStage.ReservedParcel,
                    DepotVisualStage.ActivationReady
                })
                {
                    assembly.Apply(new DepotMotionEvidence(true,
                        stage, true, true));
                    Assert.IsTrue(site.enabled);
                    Assert.IsFalse(warehouse.enabled);
                    Assert.AreEqual(stage, assembly.VisibleStage);
                }
                assembly.ClearVisualState();
                Assert.IsFalse(site.enabled);
                Assert.IsFalse(warehouse.enabled);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DowngradeFromOperationalToReservedHidesWarehouse()
        {
            var root = new GameObject("DepotStageDowngrade");
            try
            {
                var site = new GameObject("Site", typeof(SpriteRenderer))
                    .GetComponent<SpriteRenderer>();
                var warehouse = new GameObject("Warehouse",
                    typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                site.transform.SetParent(root.transform, false);
                warehouse.transform.SetParent(root.transform, false);
                var assembly = root.AddComponent<DepotVerifiedVisualAssembly>();
                SetField(assembly, "siteRenderers", new[] { site });
                SetField(assembly, "warehouseRenderers", new[] { warehouse });

                assembly.Apply(new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, false, false));
                Assert.IsTrue(warehouse.enabled);
                assembly.Apply(new DepotMotionEvidence(true,
                    DepotVisualStage.ReservedParcel, false, false));
                Assert.IsTrue(site.enabled);
                Assert.IsFalse(warehouse.enabled);
                Assert.AreEqual(DepotVisualStage.ReservedParcel,
                    assembly.VisibleStage);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void UnexpectedStageFailsClosed()
        {
            var root = new GameObject("DepotUnexpectedStage");
            try
            {
                var sprite = new GameObject("Warehouse", typeof(SpriteRenderer))
                    .GetComponent<SpriteRenderer>();
                sprite.transform.SetParent(root.transform, false);
                var assembly = root.AddComponent<DepotVerifiedVisualAssembly>();
                SetField(assembly, "warehouseRenderers", new[] { sprite });
                assembly.Apply(new DepotMotionEvidence(true,
                    DepotVisualStage.StorageOperational, false, false));
                Assert.IsTrue(sprite.enabled);
                assembly.Apply(new DepotMotionEvidence(true,
                    (DepotVisualStage)255, true, true));
                Assert.IsFalse(sprite.enabled);
                Assert.AreEqual(DepotVisualStage.Unverified,
                    assembly.VisibleStage);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
