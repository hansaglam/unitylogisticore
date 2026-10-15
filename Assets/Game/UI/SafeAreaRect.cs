using UnityEngine;

namespace LogistiCore.UI
{
    /// <summary>
    /// Insets a full-screen rect to Screen.safeArea.
    /// Unity does not provide a safe-area event. The check runs only while this component is enabled
    /// and exits before doing work when the screen, orientation, and safe rect are unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaRect : MonoBehaviour
    {
        RectTransform _rect;
        Rect _safeArea;
        int _width;
        int _height;
        ScreenOrientation _orientation;
        bool _applied;

        void OnEnable()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (_applied &&
                Screen.width == _width &&
                Screen.height == _height &&
                Screen.orientation == _orientation &&
                Screen.safeArea == _safeArea)
            {
                return;
            }

            Apply();
        }

        void Apply()
        {
            _width = Screen.width;
            _height = Screen.height;
            _orientation = Screen.orientation;
            _safeArea = Screen.safeArea;
            _applied = true;

            SafeAreaLayout.CalculateAnchors(_safeArea, _width, _height, out var anchorMin, out var anchorMax);
            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
