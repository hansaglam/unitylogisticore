using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace LogistiCore.Map
{
    /// <summary>
    /// Mouse drag and wheel in the editor. One-finger pan and two-finger pinch on a touchscreen.
    /// Gesture state resets when the touch count changes so a new finger does not jump the camera.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MapCameraRig))]
    public sealed class MapGestures : MonoBehaviour
    {
        internal const float TapSlopPixels = 10f;

        internal static bool IsTap(float travel)
        {
            return travel <= TapSlopPixels;
        }

        MapCameraRig _rig;
        PointerEventData _pointer;
        readonly List<RaycastResult> _hits = new List<RaycastResult>(8);

        int _lastTouchCount;
        bool _touchBlocked;
        Vector2 _touchStart;
        Vector2 _lastTouch;
        float _touchTravel;
        Vector2 _pinchMid;
        float _pinchDistance;

        bool _mouseDown;
        bool _mouseBlocked;
        Vector2 _mouseStart;
        Vector2 _lastMouse;
        float _mouseTravel;

        void Awake()
        {
            _rig = GetComponent<MapCameraRig>();
        }

        bool _inputEnabled;

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnhancedTouchSupport.Enable();
            _inputEnabled = true;
            if (_pointer == null && EventSystem.current != null)
                _pointer = new PointerEventData(EventSystem.current);
        }

        void OnDisable()
        {
            if (!_inputEnabled)
                return;

            _inputEnabled = false;
            if (EnhancedTouchSupport.enabled)
                EnhancedTouchSupport.Disable();

            _lastTouchCount = 0;
            _mouseDown = false;
        }

        void Update()
        {
            var touches = Touch.activeTouches;
            var count = touches.Count;
            if (count > 0)
            {
                HandleTouches(touches, count);
                _mouseDown = false;
                return;
            }

            if (_lastTouchCount > 0)
            {
                if (_lastTouchCount == 1 && !_touchBlocked && IsTap(_touchTravel))
                    SelectAt(_lastTouch);

                _lastTouchCount = 0;
                _touchBlocked = false;
            }

            HandleMouse();
        }

        void HandleTouches(UnityEngine.InputSystem.Utilities.ReadOnlyArray<Touch> touches, int count)
        {
            if (count != _lastTouchCount)
            {
                _touchBlocked = false;
                _touchTravel = 0f;
                if (count == 1)
                {
                    var touch = touches[0];
                    _touchStart = touch.screenPosition;
                    _lastTouch = _touchStart;
                    _touchBlocked = OverUi(_touchStart, touch.touchId);
                }
                else if (count >= 2)
                {
                    var a = touches[0];
                    var b = touches[1];
                    _pinchMid = (a.screenPosition + b.screenPosition) * 0.5f;
                    _pinchDistance = Vector2.Distance(a.screenPosition, b.screenPosition);
                    _touchBlocked = OverUi(a.screenPosition, a.touchId) || OverUi(b.screenPosition, b.touchId);
                }

                _lastTouchCount = count;
                return;
            }

            if (_touchBlocked || _rig == null)
                return;

            if (count == 1)
            {
                var touch = touches[0];
                var delta = touch.screenPosition - _lastTouch;
                _lastTouch = touch.screenPosition;
                _touchTravel += delta.magnitude;
                if (delta.sqrMagnitude > 0f)
                    _rig.PanScreenDelta(delta);
                return;
            }

            if (count < 2)
                return;

            var first = touches[0].screenPosition;
            var second = touches[1].screenPosition;
            var mid = (first + second) * 0.5f;
            var distance = Vector2.Distance(first, second);
            if (_pinchDistance > 1f && distance > 1f)
            {
                var factor = distance / _pinchDistance;
                if (factor > 1.002f || factor < 0.998f)
                    _rig.ZoomAtScreen(_pinchMid, factor);
            }

            _pinchDistance = distance;
            _pinchMid = mid;
        }

        void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || _rig == null)
                return;

            var position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _mouseDown = true;
                _mouseBlocked = OverUi(position, -1);
                _mouseStart = position;
                _lastMouse = position;
                _mouseTravel = 0f;
            }

            if (_mouseDown && mouse.leftButton.isPressed && !_mouseBlocked)
            {
                var delta = position - _lastMouse;
                _lastMouse = position;
                _mouseTravel += delta.magnitude;
                if (delta.sqrMagnitude > 0f)
                    _rig.PanScreenDelta(delta);
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (_mouseDown && !_mouseBlocked && IsTap(_mouseTravel))
                    SelectAt(_mouseStart);

                _mouseDown = false;
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f && !OverUi(position, -1))
            {
                var factor = scroll > 0f ? 1.12f : 1f / 1.12f;
                _rig.ZoomAtScreen(position, factor);
            }
        }

        void SelectAt(Vector2 screen)
        {
            var layer = GetComponent<MapMarkerLayer>();
            if (layer == null)
                layer = FindAnyObjectByType<MapMarkerLayer>();

            if (layer == null)
                return;

            if (!layer.TrySelect(screen, out _))
                layer.ClearSelection();
        }

        bool OverUi(Vector2 screen, int pointerId)
        {
            var system = EventSystem.current;
            if (system == null)
                return false;

            if (_pointer == null)
                _pointer = new PointerEventData(system);

            _pointer.Reset();
            _pointer.position = screen;
            _pointer.pointerId = pointerId;
            _hits.Clear();
            system.RaycastAll(_pointer, _hits);
            return _hits.Count > 0;
        }
    }
}
