using LogistiCore.Backend;
using LogistiCore.Map;
using LogistiCore.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace LogistiCore.Tests
{
    /// <summary>
    /// Deferred EditMode regression contract: never connects to Firebase.
    /// </summary>
    public sealed class CompanyHeadquartersSetupFlowTests
    {
        [Test]
        public void FormNeedsExplicitReviewAndDoesNotConfirmOffline()
        {
            var root = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            var panel = new GameObject("HeadquartersPreview",
                typeof(RectTransform), typeof(CompanyHeadquartersPreview));
            panel.transform.SetParent(root.transform, false);

            try
            {
                var preview = panel.GetComponent<CompanyHeadquartersPreview>();
                preview.Build(ShellWidgets.Builtin());
                var form = panel.GetComponent<CompanyHeadquartersSetupFlow>();
                Assert.IsNotNull(form);
                Assert.AreEqual(CompanyHeadquartersSetupFlow.Stage.Edit, form.CurrentStage);
                Assert.IsFalse(form.CanConfirm);
                Assert.AreEqual(4, panel.GetComponentsInChildren<Button>(true).Length);

                var input = panel.GetComponentInChildren<InputField>(true);
                Assert.IsNotNull(input);
                input.text = "New Logistics";
                form.ReviewSelection();

                Assert.IsTrue(form.HasReviewedSelection);
                Assert.AreEqual("New Logistics", form.CompanyName);
                Assert.AreEqual(CompanyHeadquartersSetupFlow.Stage.RequiresConnection,
                    form.CurrentStage);
                Assert.IsFalse(form.CanConfirm);
                form.ConfirmCompany();
                Assert.AreEqual(CompanyHeadquartersSetupFlow.Stage.RequiresConnection,
                    form.CurrentStage);

                input.text = "Different company";
                Assert.AreEqual(CompanyHeadquartersSetupFlow.Stage.Edit, form.CurrentStage);
                Assert.IsFalse(form.CanConfirm);
                Assert.IsFalse(form.HasReviewedSelection);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidNameDoesNotBecomeAConfirmedCompanyProposal()
        {
            var root = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            var panel = new GameObject("HeadquartersPreview",
                typeof(RectTransform), typeof(CompanyHeadquartersPreview));
            panel.transform.SetParent(root.transform, false);

            try
            {
                var preview = panel.GetComponent<CompanyHeadquartersPreview>();
                preview.Build(ShellWidgets.Builtin());
                var flow = panel.GetComponent<CompanyHeadquartersSetupFlow>();
                var input = panel.GetComponentInChildren<InputField>(true);

                foreach (var name in new[] { "", "a", "   ", "x" + new string('a', 65), "A\nB" })
                {
                    input.text = name;
                    flow.ReviewSelection();
                    Assert.AreEqual(CompanyHeadquartersSetupFlow.Stage.Edit, flow.CurrentStage);
                    Assert.IsFalse(flow.CanConfirm);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NarrowCompanyGatewayIsAlwaysClosedWithoutApprovedSignedInEmulator()
        {
            Assert.IsFalse(ClientHeadquartersCreationGateway.IsAuthorized(
                null, default, false));
            Assert.IsFalse(ClientCallableCatalog.IsSupportedPlayerCommand(
                ClientCallableCatalog.CreateCompanyWithHeadquarters));
            Assert.IsTrue(ClientCallableCatalog.IsCommand(
                ClientCallableCatalog.CreateCompanyWithHeadquarters));
            Assert.IsTrue(ClientCallableCatalog.IsRead(
                ClientCallableCatalog.GetMyCompanyHeadquarters));
        }
    }
}
