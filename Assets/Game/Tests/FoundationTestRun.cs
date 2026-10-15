using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LogistiCore.Tests
{
    static class FoundationTestRun
    {
        [MenuItem("LogistiCore/Foundation/Run Edit Mode Tests")]
        public static void Run()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var settings = new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "LogistiCore.Tests" }
            })
            {
                runSynchronously = true
            };

            api.RegisterCallbacks(new Callbacks());
            api.Execute(settings);
        }

        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log("[LogistiCore] Edit mode tests: " + result.PassCount + " passed, " + result.FailCount + " failed, " + result.SkipCount + " skipped.");
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus != TestStatus.Failed || result.HasChildren)
                    return;

                Debug.LogError("[LogistiCore] " + result.Test.FullName + "\n" + result.Message + "\n" + result.StackTrace);
            }
        }
    }
}
