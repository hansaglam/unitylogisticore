using LogistiCore.EditorTools;
using NUnit.Framework;

namespace LogistiCore.Tests
{
    public class FoundationSceneSafetyTests
    {
        [Test]
        public void FoundationSetup_StartupDoesNotCreateOrReplaceTheScene()
        {
            Assert.AreEqual(
                FoundationSetup.FoundationSceneFileAction.None,
                FoundationSetup.FileActionFor(explicitRequest: false, sceneExists: true, replaceExisting: false));
            Assert.AreEqual(
                FoundationSetup.FoundationSceneFileAction.None,
                FoundationSetup.FileActionFor(explicitRequest: false, sceneExists: true, replaceExisting: true));
            Assert.AreEqual(
                FoundationSetup.FoundationSceneFileAction.None,
                FoundationSetup.FileActionFor(explicitRequest: false, sceneExists: false, replaceExisting: true));
            Assert.AreEqual(
                FoundationSetup.FoundationSceneFileAction.None,
                FoundationSetup.FileActionFor(explicitRequest: true, sceneExists: true, replaceExisting: false));
            Assert.AreEqual(
                FoundationSetup.FoundationSceneFileAction.CreateMissing,
                FoundationSetup.FileActionFor(explicitRequest: true, sceneExists: false, replaceExisting: false));
            Assert.AreEqual(
                FoundationSetup.FoundationSceneFileAction.CreateMissing,
                FoundationSetup.FileActionFor(explicitRequest: true, sceneExists: false, replaceExisting: true));
            Assert.AreEqual(
                FoundationSetup.FoundationSceneFileAction.ReplaceWithBackup,
                FoundationSetup.FileActionFor(explicitRequest: true, sceneExists: true, replaceExisting: true));
        }

        [Test]
        public void FoundationSetup_PendingPathCannotBeTheVerificationScene()
        {
            Assert.AreNotEqual(FoundationSetup.ScenePath, FoundationSetup.PendingScenePath);
            StringAssert.EndsWith("FoundationVerification.pending.unity", FoundationSetup.PendingScenePath);
        }
    }
}
