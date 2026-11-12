using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LogistiCore.EditorTools
{
    public static class MapDeviceBuild
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string Output = "Builds/iOS/MapDeviceTest";
        const string RebuildCOutput = "Builds/iOS/MapDeviceTest-RebuildC";
        const string UnityIos04Output = "Builds/iOS/MapDeviceTest-UnityIos04";
        const string UnityIos04BOutput = "Builds/iOS/MapDeviceTest-UnityIos04B";
        const string UnityIos04DOutput = "Builds/iOS/MapDeviceTest-UnityIos04D";

        public static void Build()
        {
            BuildTo(Output);
        }

        public static void BuildRebuildC()
        {
            BuildTo(RebuildCOutput);
        }

        public static void BuildUnityIos04()
        {
            BuildTo(UnityIos04Output);
        }

        public static void BuildUnityIos04B()
        {
            BuildTo(UnityIos04BOutput);
        }

        public static void BuildUnityIos04D()
        {
            BuildTo(UnityIos04DOutput);
        }

        static void BuildTo(string output)
        {
            var sha = Sha(ScenePath);
            if (sha != SceneSha)
            {
                Debug.LogError("Map device build refused. Scene hash is " + sha);
                EditorApplication.Exit(2);
                return;
            }

            if (Directory.Exists(output) && Directory.GetFileSystemEntries(output).Length > 0)
            {
                Debug.LogError("Map device build refused. Output already exists: " + output);
                EditorApplication.Exit(3);
                return;
            }

            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.iOS,
                options = BuildOptions.Development
            });
            var succeeded = report.summary.result == BuildResult.Succeeded;
            if (succeeded)
                PatchPodfileForCurrentXcode(output);
            Debug.Log(
                "Map device build " + report.summary.result
                + " output=" + output
                + " errors=" + report.summary.totalErrors
                + " warnings=" + report.summary.totalWarnings
                + " size=" + report.summary.totalSize);
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        static void PatchPodfileForCurrentXcode(string output)
        {
            var path = Path.Combine(output, "Podfile");
            if (!File.Exists(path))
                return;

            var text = File.ReadAllText(path);
            if (text.Contains("post_install do |installer|"))
                return;

            File.AppendAllText(
                path,
                "\npost_install do |installer|\n" +
                "  installer.pods_project.targets.each do |target|\n" +
                "    target.build_configurations.each do |config|\n" +
                "      if config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'].to_f < 15.0\n" +
                "        config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'\n" +
                "      end\n" +
                "    end\n" +
                "  end\n" +
                "end\n");
            Debug.Log("Map device build patched Podfile for Xcode deployment target 15.0.");
        }

        static string Sha(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var text = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                    text.Append(hash[i].ToString("x2"));
                return text.ToString();
            }
        }
    }
}
