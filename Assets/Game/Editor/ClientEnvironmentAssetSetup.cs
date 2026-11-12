using LogistiCore.Backend;
using UnityEditor;
using UnityEngine;

namespace LogistiCore.EditorTools
{
    /// <summary>
    /// Creates the committed Unconfigured environment asset once. It never overwrites an existing asset.
    /// </summary>
    [InitializeOnLoad]
    public static class ClientEnvironmentAssetSetup
    {
        public const string AssetPath = "Assets/Game/Backend/Resources/ClientEnvironment.asset";
        public const string LocalAssetPath = "Assets/Game/Backend/Resources/LocalClientEnvironment.asset";

        static ClientEnvironmentAssetSetup()
        {
            EditorApplication.delayCall += EnsureWhenIdle;
        }

        [MenuItem("LogistiCore/Backend/Ensure Environment Asset")]
        public static void EnsureDefaultAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ClientEnvironmentSettings>(AssetPath);
            if (existing != null)
            {
                var request = existing.ToRequest();
                if (existing.Environment != ClientBackendEnvironment.Unconfigured || request.HasAppConfiguration)
                {
                    Debug.LogWarning(
                        "[LogistiCore] Shared ClientEnvironment is not the empty Unconfigured default. " +
                        "Put non-production values in the gitignored LocalClientEnvironment asset.");
                }

                return;
            }

            EnsureFolder("Assets/Game", "Backend");
            EnsureFolder("Assets/Game/Backend", "Resources");
            var settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LogistiCore] Created Unconfigured ClientEnvironment asset.");
        }

        [MenuItem("LogistiCore/Backend/Ensure Local Emulator Environment")]
        public static void EnsureLocalEmulatorAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ClientEnvironmentSettings>(LocalAssetPath);
            if (existing != null)
            {
                Debug.Log("[LogistiCore] LocalClientEnvironment already exists (gitignored).");
                return;
            }

            EnsureFolder("Assets/Game", "Backend");
            EnsureFolder("Assets/Game/Backend", "Resources");
            var settings = ScriptableObject.CreateInstance<ClientEnvironmentSettings>();
            settings.Configure(
                ClientBackendEnvironment.LocalEmulator,
                ClientProtocol.DemoProjectId,
                "local-app",
                "fake-api-key",
                "127.0.0.1",
                ClientLocalEmulatorSecurity.FirestorePort,
                ClientLocalEmulatorSecurity.AuthPort,
                ClientLocalEmulatorSecurity.FunctionsOrigin,
                ClientProtocol.FunctionsRegion);
            AssetDatabase.CreateAsset(settings, LocalAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LogistiCore] Created gitignored LocalClientEnvironment for demo-logisticore-local loopback.");
        }

        static void EnsureWhenIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            EnsureDefaultAsset();
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
