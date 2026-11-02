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
        int _appliedWidth;
        int _appliedHeight;
        Rect _appliedSafe;

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
            var safe = Screen.safeArea;
            var turkeyScreenChanged = _scope == MapScope.Turkey
                && (Screen.width != _appliedWidth || Screen.height != _appliedHeight || safe != _appliedSafe);
            if (!_userAdjusted && (Mathf.Abs(aspect - _appliedAspect) > 0.01f || turkeyScreenChanged))
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

        /// <summary>
        /// Reapplies the current frame after the command shell changes the open area.
        /// A pan or pinch sets <see cref="UserAdjusted"/> and this call leaves that view alone.
        /// </summary>
        public void ReframeForChrome()
        {
            if (_userAdjusted)
                return;

            ApplyImmediate(_scope);
        }

        /// <summary>
        /// Keeps the Türkiye zoom floor and moves the opening frame only enough for the
        /// eight city hit targets to land in the command shell's open map area.
        /// Geographic coordinates are not changed. The size never goes below the
        /// existing Türkiye fit, and it never goes past the world frame.
        /// </summary>
        public static void FitCitiesInside(
            Vector2 fittedCenter,
            float fittedSize,
            float aspect,
            float screenWidth,
            float screenHeight,
            Rect safeArea,
            Vector2 worldMin,
            Vector2 worldMax,
            out Vector2 center,
            out float size)
        {
            center = fittedCenter;
            size = fittedSize;
            if (screenWidth < 1f || screenHeight < 1f || fittedSize < 0.0001f)
                return;

            var reserved = MapCommandLayout.Evaluate(screenWidth, screenHeight, safeArea, true);
            var metrics = reserved.ContextOpen
                ? reserved
                : MapCommandLayout.Evaluate(screenWidth, screenHeight, safeArea, false);
            var hole = Inset(metrics.Viewport, MapMarkerLod.MarkerHitPixels + CityClearancePixels);
            if (hole.width < 24f || hole.height < 24f)
                return;

            var maxSize = MapFrames.MaximumOrthographicSize(aspect);
            for (var step = 0; step < 32; step++)
            {
                var fit = ShiftInto(ref center, size, hole, screenWidth, screenHeight);
                if (fit == CityFit.Inside)
                    break;
                if (fit == CityFit.Shifted)
                    continue;
                if (size >= maxSize - 0.001f)
                    break;

                size = Mathf.Min(maxSize, size * 1.05f);
            }

            center = MapViewMath.ClampCenter(center, size, aspect, worldMin, worldMax);
        }

        enum CityFit
        {
            Inside = 0,
            Shifted = 1,
            Overflow = 2
        }

        static CityFit ShiftInto(ref Vector2 center, float size, Rect hole, float screenWidth, float screenHeight)
        {
            var units = MapMarkerLod.UnitsPerPixel(size, screenHeight);
            var west = 0f;
            var east = 0f;
            var south = 0f;
            var north = 0f;
            for (var i = 0; i < MapCities.All.Length; i++)
            {
                var city = MapCities.All[i];
                var world = MapProjection.Project(city.Latitude, city.Longitude);
                var screen = MapMarkerLod.ToScreen(world, center, units, screenWidth, screenHeight);
                if (screen.x < hole.xMin)
                    west = Mathf.Max(west, hole.xMin - screen.x);
                else if (screen.x > hole.xMax)
                    east = Mathf.Max(east, screen.x - hole.xMax);

                if (screen.y < hole.yMin)
                    south = Mathf.Max(south, hole.yMin - screen.y);
                else if (screen.y > hole.yMax)
                    north = Mathf.Max(north, screen.y - hole.yMax);
            }

            var overflow = (west > 0.5f && east > 0.5f) || (south > 0.5f && north > 0.5f);
            var shifted = false;
            if (west > 0.5f && east <= 0.5f)
            {
                center.x -= west * units;
                shifted = true;
            }
            else if (east > 0.5f && west <= 0.5f)
            {
                center.x += east * units;
                shifted = true;
            }

            if (south > 0.5f && north <= 0.5f)
            {
                center.y -= south * units;
                shifted = true;
            }
            else if (north > 0.5f && south <= 0.5f)
            {
                center.y += north * units;
                shifted = true;
            }

            if (overflow)
                return CityFit.Overflow;
            if (!shifted && west <= 0.5f && east <= 0.5f && south <= 0.5f && north <= 0.5f)
                return CityFit.Inside;
            return shifted ? CityFit.Shifted : CityFit.Overflow;
        }

        static Rect Inset(Rect rect, float padding)
        {
            rect.xMin += padding;
            rect.yMin += padding;
            rect.xMax -= padding;
            rect.yMax -= padding;
            return rect;
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
            if (scope == MapScope.Turkey)
            {
                FitCitiesInside(
                    center,
                    size,
                    aspect,
                    Screen.width,
                    Screen.height,
                    Screen.safeArea,
                    _worldMin,
                    _worldMax,
                    out center,
                    out size);
            }

            var changed = _scope != scope;
            _scope = scope;
            _targetCenter = center;
            _targetSize = size;
            _appliedAspect = aspect;
            _appliedWidth = Screen.width;
            _appliedHeight = Screen.height;
            _appliedSafe = Screen.safeArea;
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
            // Travel limits match the World presentation frame. World() stays the full
            // geographic span used by the meshes, so Antarctica remains in the data
            // and outside the camera.
            MapFrames.WorldPresentation(out _worldMin, out _worldMax);
            MapFrames.Turkey(out _turkeyMin, out _turkeyMax);
        }

        /// <summary>
        /// Keeps the Türkiye orthographic size and moves the camera west only as far as
        /// needed so a city marker is not drawn on a rail button. World framing is unchanged.
        /// The rail panel itself does not receive touches; the buttons do.
        /// </summary>
        internal const float CityClearancePixels = 18f;

        internal static Vector2 CenterClearOfRail(
            Vector2 fittedCenter,
            float orthographicSize,
            float screenWidth,
            float screenHeight,
            Rect safeArea)
        {
            if (screenWidth < 1f || screenHeight < 1f || orthographicSize < 0.0001f)
                return fittedCenter;

            var safe = safeArea.width < 1f || safeArea.height < 1f
                ? new Rect(0f, 0f, screenWidth, screenHeight)
                : safeArea;
            var buttons = RailButtons(screenWidth, screenHeight, safe);
            var units = MapMarkerLod.UnitsPerPixel(orthographicSize, screenHeight);
            var shift = 0f;
            for (var c = 0; c < MapCities.All.Length; c++)
            {
                var city = MapCities.All[c];
                var world = MapProjection.Project(city.Latitude, city.Longitude);
                var screen = MapMarkerLod.ToScreen(world, fittedCenter, units, screenWidth, screenHeight);
                for (var b = 0; b < buttons.Length; b++)
                {
                    var button = buttons[b];
                    if (screen.y < button.yMin - CityClearancePixels || screen.y > button.yMax + CityClearancePixels)
                        continue;

                    var deficit = button.xMax + CityClearancePixels - screen.x;
                    if (deficit > shift)
                        shift = deficit;
                }
            }

            if (shift <= 0f)
                return fittedCenter;

            return new Vector2(fittedCenter.x - shift * units, fittedCenter.y);
        }

        internal static Rect[] RailButtons(float screenWidth, float screenHeight, Rect safeArea)
        {
            var safe = safeArea.width < 1f || safeArea.height < 1f
                ? new Rect(0f, 0f, screenWidth, screenHeight)
                : safeArea;
            var metrics = MapHudLayout.Evaluate(screenWidth, screenHeight, safe, true);
            var scale = metrics.CanvasScale < 0.0001f ? 1f : metrics.CanvasScale;
            var railX = safe.xMin;
            var railY = safe.yMin + metrics.Gap * scale;
            var railWidth = metrics.RailWidth * scale;
            var railHeight = safe.height - (metrics.TopBarHeight + metrics.Gap) * scale;
            var pad = metrics.RailPadding * scale;
            var height = metrics.RailButtonHeight * scale;
            var spacing = metrics.RailSpacing * scale;
            var rects = new Rect[MapHudLayout.RailButtonCount];
            var top = railY + railHeight - pad;
            var x = railX + pad;
            var width = railWidth - pad * 2f;
            for (var i = 0; i < rects.Length; i++)
            {
                top -= height;
                rects[i] = new Rect(x, top, width, height);
                top -= spacing;
            }

            return rects;
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
