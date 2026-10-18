using System;
using UnityEngine;

namespace LogistiCore.Map
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class MapCameraRig : MonoBehaviour
    {
        public const float CameraZ = -10f;

        Camera _camera;
        Vector2 _worldMin;
        Vector2 _worldMax;
        Vector2 _turkeyMin;
        Vector2 _turkeyMax;
        Vector2 _targetCenter;
        float _targetSize;
        MapScope _scope = MapScope.Turkey;
        bool _moving;
        bool _userAdjusted;
        bool _framed;
        float _appliedAspect;

        public MapScope Scope => _scope;
        public bool UserAdjusted => _userAdjusted;
        public float TargetOrthographicSize => _targetSize;
        public Vector2 TargetCenter => _targetCenter;

        public event Action ScopeChanged;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            CacheBounds();
        }

        void OnEnable()
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();

            CacheBounds();
            if (!_framed && Application.isPlaying)
                ApplyImmediate(MapScope.Turkey);
        }

        void LateUpdate()
        {
            if (_camera == null)
                return;

            var aspect = Aspect();
            if (!_userAdjusted && Mathf.Abs(aspect - _appliedAspect) > 0.01f)
                ApplyImmediate(_scope);

            if (!_moving)
                return;

            var t = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            var current = new Vector2(transform.position.x, transform.position.y);
            var next = Vector2.Lerp(current, _targetCenter, t);
            var nextSize = Mathf.Lerp(_camera.orthographicSize, _targetSize, t);
            if ((next - _targetCenter).sqrMagnitude < 0.000001f && Mathf.Abs(nextSize - _targetSize) < 0.0001f)
            {
                next = _targetCenter;
                nextSize = _targetSize;
                _moving = false;
            }

            transform.position = new Vector3(next.x, next.y, CameraZ);
            _camera.orthographicSize = nextSize;
        }

        public void FocusWorld()
        {
            _userAdjusted = false;
            ApplyFocus(MapScope.World, smooth: true);
        }

        public void FocusTurkey()
        {
            _userAdjusted = false;
            ApplyFocus(MapScope.Turkey, smooth: true);
        }

        public void Restore()
        {
            _userAdjusted = false;
            ApplyFocus(_scope, smooth: true);
        }

        public void ZoomIn()
        {
            ZoomAtScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 1.25f);
        }

        public void ZoomOut()
        {
            ZoomAtScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 0.8f);
        }

        public void ZoomAtScreen(Vector2 screen, float zoomInFactor)
        {
            if (_camera == null)
                return;

            var aspect = Aspect();
            var center = _moving ? _targetCenter : new Vector2(transform.position.x, transform.position.y);
            var size = _moving ? _targetSize : _camera.orthographicSize;
            MapViewMath.ZoomAbout(
                center,
                size,
                screen,
                zoomInFactor,
                aspect,
                Screen.width,
                Screen.height,
                MapFrames.MinimumOrthographicSize(aspect),
                MapFrames.MaximumOrthographicSize(aspect),
                _worldMin,
                _worldMax,
                out var newCenter,
                out var newSize);

            _userAdjusted = true;
            _targetCenter = newCenter;
            _targetSize = newSize;
            _moving = true;
        }

        public void PanScreenDelta(Vector2 screenDelta)
        {
            if (_camera == null)
                return;

            var height = Screen.height < 1 ? 1f : Screen.height;
            var unitsPerPixel = (_camera.orthographicSize * 2f) / height;
            var center = new Vector2(transform.position.x, transform.position.y);
            center.x -= screenDelta.x * unitsPerPixel;
            center.y -= screenDelta.y * unitsPerPixel;
            center = MapViewMath.ClampCenter(center, _camera.orthographicSize, Aspect(), _worldMin, _worldMax);
            transform.position = new Vector3(center.x, center.y, CameraZ);
            _targetCenter = center;
            _targetSize = _camera.orthographicSize;
            _moving = false;
            _userAdjusted = true;
        }

        public void ApplyImmediate(MapScope scope)
        {
            ApplyFocus(scope, smooth: false);
        }

        void ApplyFocus(MapScope scope, bool smooth)
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();

            CacheBounds();
            var aspect = Aspect();
            var boundsMin = scope == MapScope.World ? _worldMin : _turkeyMin;
            var boundsMax = scope == MapScope.World ? _worldMax : _turkeyMax;
            var padding = scope == MapScope.World ? MapFrames.WorldFocusPadding : MapFrames.TurkeyFocusPadding;
            MapViewMath.Fit(boundsMin, boundsMax, aspect, padding, out var center, out var size);
            size = MapViewMath.ClampSize(size, MapFrames.MinimumOrthographicSize(aspect), MapFrames.MaximumOrthographicSize(aspect));
            center = MapViewMath.ClampCenter(center, size, aspect, _worldMin, _worldMax);

            var changed = _scope != scope;
            _scope = scope;
            _targetCenter = center;
            _targetSize = size;
            _appliedAspect = aspect;
            _framed = true;

            if (!smooth)
            {
                transform.position = new Vector3(center.x, center.y, CameraZ);
                _camera.orthographicSize = size;
                _moving = false;
            }
            else
            {
                _moving = true;
            }

            if (changed)
                ScopeChanged?.Invoke();
        }

        void CacheBounds()
        {
            MapFrames.World(out _worldMin, out _worldMax);
            MapFrames.Turkey(out _turkeyMin, out _turkeyMax);
        }

        float Aspect()
        {
            var aspect = _camera != null ? _camera.aspect : 0f;
            if (aspect < 0.05f)
                aspect = 16f / 9f;
            return aspect;
        }
    }
}
