using LogistiCore.Map;
using UnityEditor;
using UnityEngine;

namespace LogistiCore.EditorTools
{
    public static class MapRibbonDebug
    {
        [MenuItem("LogistiCore/Map/Premium Lines/Show Premium Lines")]
        static void ShowPremium()
        {
            SetPremium(true);
        }

        [MenuItem("LogistiCore/Map/Premium Lines/Show Original Lines")]
        static void ShowOriginal()
        {
            SetPremium(false);
        }

        static void SetPremium(bool premium)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("Enter Play Mode on the map scene, then choose this menu again. The choice is not saved into the scene.");
                return;
            }

            var presenters = Object.FindObjectsByType<MapRibbonPresenter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (presenters.Length == 0)
            {
                Debug.LogWarning("No premium line presenter is active.");
                return;
            }

            for (var i = 0; i < presenters.Length; i++)
            {
                if (premium)
                    presenters[i].ShowPremium();
                else
                    presenters[i].ShowOriginal();
            }
        }
    }
}
