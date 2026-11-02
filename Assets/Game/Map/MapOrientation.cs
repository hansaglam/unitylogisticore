using LogistiCore.UI;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// The map scene requests landscape while it is active.
    /// Returning Home is portrait, applied by AppNavigation before the next scene loads.
    /// This does not change PlayerSettings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapOrientation : MonoBehaviour
    {
        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            ScreenLayouts.Apply(ScreenLayoutMode.Landscape);
        }
    }
}
