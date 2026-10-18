using LogistiCore.UI;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// The map scene requests landscape. It does not change the project orientation
    /// and it does not force portrait on exit, because this phase has no primary-section navigation.
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
