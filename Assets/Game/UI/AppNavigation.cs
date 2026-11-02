using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LogistiCore.UI
{
    /// <summary>
    /// Single-scene route between portrait Home, portrait Company, and the landscape map.
    /// There is no persistent navigation object.
    /// </summary>
    public static class AppNavigation
    {
        public const string HomeSceneName = "Home";
        public const string MapSceneName = "WorldMapVerticalSlice";
        public const string CompanySceneName = "Company";
        public const string HomeScenePath = "Assets/Game/Scenes/Home.unity";
        public const string MapScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        public const string CompanyScenePath = "Assets/Game/Scenes/Company.unity";
        public const LoadSceneMode NavigationMode = LoadSceneMode.Single;

        public readonly struct Destination
        {
            public readonly string SceneName;
            public readonly ScreenLayoutMode Layout;

            public Destination(string sceneName, ScreenLayoutMode layout)
            {
                SceneName = sceneName;
                Layout = layout;
            }
        }

        public static Destination HomeDestination =>
            new Destination(HomeSceneName, ScreenLayoutMode.Portrait);

        public static Destination MapDestination =>
            new Destination(MapSceneName, ScreenLayoutMode.Landscape);

        public static Destination CompanyDestination =>
            new Destination(CompanySceneName, ScreenLayoutMode.Portrait);

        public static Destination LastDestination { get; private set; }

        /// <summary>
        /// Edit Mode tests set this so a route can be checked without replacing the open scene.
        /// </summary>
        internal static bool BlockSceneLoad { get; set; }

        static bool _loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _loading = false;
            BlockSceneLoad = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static void ShowHome()
        {
            Go(HomeDestination);
        }

        public static void ShowMap()
        {
            Go(MapDestination);
        }

        public static void ShowCompany()
        {
            Go(CompanyDestination);
        }

        public static void Go(Destination destination)
        {
            LastDestination = destination;
            ScreenLayouts.Apply(destination.Layout);

            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.name == destination.SceneName)
                return;

            if (BlockSceneLoad || _loading)
                return;

            _loading = true;
            try
            {
                // Single-mode load enables the incoming scene before the outgoing scene
                // unloads. EventSystem.current stays the first enabled system, so the
                // outgoing one would keep input and the editor would warn until unload.
                // Deactivate it first. The incoming scene brings its own system.
                DeactivateEventSystems();
                SceneManager.LoadScene(destination.SceneName, NavigationMode);
            }
            finally
            {
                _loading = false;
            }
        }

        static void DeactivateEventSystems()
        {
            var count = SceneManager.sceneCount;
            for (var s = 0; s < count; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                var roots = scene.GetRootGameObjects();
                for (var i = 0; i < roots.Length; i++)
                {
                    var systems = roots[i].GetComponentsInChildren<EventSystem>(true);
                    for (var n = 0; n < systems.Length; n++)
                    {
                        var system = systems[n];
                        if (system != null && system.gameObject.activeSelf)
                            system.gameObject.SetActive(false);
                    }
                }
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _loading = false;
            if (scene.name == HomeSceneName || scene.name == CompanySceneName)
                ScreenLayouts.Apply(ScreenLayoutMode.Portrait);
            else if (scene.name == MapSceneName)
                ScreenLayouts.Apply(ScreenLayoutMode.Landscape);
        }
    }
}
