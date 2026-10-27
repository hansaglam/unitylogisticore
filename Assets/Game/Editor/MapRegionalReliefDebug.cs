using LogistiCore.Map;
using UnityEditor;
using UnityEngine;

namespace LogistiCore.EditorTools
{
    public static class MapRegionalReliefDebug
    {
        [MenuItem("LogistiCore/Map/Regional Relief/Show Regional Detail")]
        static void ShowRegional()
        {
            SetGlobalOnly(false);
        }

        [MenuItem("LogistiCore/Map/Regional Relief/Show Global Relief Only")]
        static void ShowGlobal()
        {
            SetGlobalOnly(true);
        }

        static void SetGlobalOnly(bool globalOnly)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("Enter Play Mode on the map scene, then choose this menu again. The choice is not saved into the scene.");
                return;
            }

            var presenters = Object.FindObjectsByType<MapRegionalReliefPresenter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (presenters.Length == 0)
            {
                Debug.LogWarning("No regional relief presenter is active.");
                return;
            }

            for (var i = 0; i < presenters.Length; i++)
                presenters[i].GlobalOnly = globalOnly;
        }
    }
}
