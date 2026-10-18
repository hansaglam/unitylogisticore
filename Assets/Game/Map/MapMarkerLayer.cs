using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// Eight city markers. Labels stay a constant screen size and hide at world framing.
    /// Positions update while the camera moves. They do not rebuild geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapMarkerLayer : MonoBehaviour
    {
        [SerializeField] Camera mapCamera;
        [SerializeField] RectTransform labelRoot;
        [SerializeField] MapCityMarker[] markers;
        [SerializeField] Text[] labels;

        readonly List<Binding> _bindings = new List<Binding>(8);
        string _selectedId;
        Vector3 _lastCameraPosition;
        float _lastSize = -1f;
        int _lastWidth;
        int _lastHeight;
        bool _subscribed;
        bool _labelsVisible = true;

        public string SelectedId => _selectedId;
        public int MarkerCount => markers == null ? 0 : markers.Length;

        public event Action SelectionChanged;

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (mapCamera == null)
                mapCamera = Camera.main;

            Subscribe();
            ApplyScaleAndLabels(force: true);
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void LateUpdate()
        {
            ApplyScaleAndLabels(force: false);
        }

        public bool TrySelect(Vector2 screen, out string cityId)
        {
            cityId = null;
            if (markers == null || mapCamera == null)
                return false;

            var best = 48f * 48f;
            MapCityMarker chosen = null;
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null)
                    continue;

                var point = mapCamera.WorldToScreenPoint(marker.transform.position);
                var dx = point.x - screen.x;
                var dy = point.y - screen.y;
                var distance = dx * dx + dy * dy;
                if (distance < best)
                {
                    best = distance;
                    chosen = marker;
                }
            }

            if (chosen == null)
                return false;

            SetSelected(chosen.CityId);
            cityId = chosen.CityId;
            return true;
        }

        public void ClearSelection()
        {
            if (_selectedId == null)
                return;

            SetSelected(null);
        }

        public bool TryGetLocalizedName(string cityId, out string name)
        {
            name = null;
            for (var i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i].CityId == cityId)
                {
                    name = _bindings[i].Value;
                    return !string.IsNullOrEmpty(name);
                }
            }

            return false;
        }

        void SetSelected(string cityId)
        {
            _selectedId = cityId;
            SelectionChanged?.Invoke();
            ApplyScaleAndLabels(force: true);
        }

        void Subscribe()
        {
            if (_subscribed || markers == null)
                return;

            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                var label = labels != null && i < labels.Length ? labels[i] : null;
                if (marker == null)
                    continue;

                var binding = new Binding
                {
                    CityId = marker.CityId,
                    Source = new LocalizedString(MapContent.Table, MapContent.CityKey(marker.CityId)),
                    Target = label
                };
                binding.Handler = value =>
                {
                    binding.Value = value ?? string.Empty;
                    if (binding.Target != null)
                        binding.Target.text = binding.Value;
                };
                binding.Source.StringChanged += binding.Handler;
                _bindings.Add(binding);
            }

            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed)
                return;

            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Source.StringChanged -= _bindings[i].Handler;

            _bindings.Clear();
            _subscribed = false;
        }

        void ApplyScaleAndLabels(bool force)
        {
            if (mapCamera == null)
                return;

            var position = mapCamera.transform.position;
            var size = mapCamera.orthographicSize;
            var width = Screen.width;
            var height = Screen.height;
            if (!force &&
                position == _lastCameraPosition &&
                Mathf.Approximately(size, _lastSize) &&
                width == _lastWidth &&
                height == _lastHeight)
            {
                return;
            }

            _lastCameraPosition = position;
            _lastSize = size;
            _lastWidth = width;
            _lastHeight = height;

            var screenHeight = height < 1 ? 1f : height;
            var world = (34f / screenHeight) * (size * 2f);
            if (markers != null)
            {
                for (var i = 0; i < markers.Length; i++)
                {
                    var marker = markers[i];
                    if (marker == null)
                        continue;

                    var selected = marker.CityId == _selectedId;
                    var scale = world * (selected ? 1.55f : 1f);
                    marker.transform.localScale = new Vector3(scale, scale, 1f);
                    var renderers = marker.GetComponentsInChildren<SpriteRenderer>();
                    for (var r = 0; r < renderers.Length; r++)
                    {
                        var renderer = renderers[r];
                        if (renderer.gameObject.name == "Glow")
                        {
                            renderer.color = selected
                                ? new Color(0.25f, 0.95f, 1f, 0.62f)
                                : new Color(0.13f, 0.78f, 0.95f, 0.28f);
                        }
                        else
                        {
                            renderer.color = selected
                                ? new Color(0.88f, 0.98f, 1f, 1f)
                                : new Color(0.20f, 0.86f, 0.98f, 1f);
                        }
                    }
                }
            }

            var turkeySize = TurkeyLabelThreshold(mapCamera.aspect);
            var visible = size <= turkeySize;
            _labelsVisible = visible;
            if (labels == null || labelRoot == null)
                return;

            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null)
                    continue;

                if (!visible || markers == null || i >= markers.Length || markers[i] == null)
                {
                    SetLabelVisible(label, false);
                    continue;
                }

                SetLabelVisible(label, true);
                label.color = markers[i].CityId == _selectedId
                    ? new Color(0.88f, 0.98f, 1f, 1f)
                    : new Color(0.86f, 0.93f, 0.97f, 0.94f);
                var screen = mapCamera.WorldToScreenPoint(markers[i].transform.position);
                screen.y += 28f;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(labelRoot, screen, null, out var local))
                {
                    var plate = label.rectTransform.parent as RectTransform;
                    if (plate != null && plate != labelRoot)
                        plate.anchoredPosition = local;
                    else
                        label.rectTransform.anchoredPosition = local;
                }
            }
        }

        static void SetLabelVisible(Text label, bool visible)
        {
            var parent = label.transform.parent;
            if (parent != null && parent.name.StartsWith("Plate_", System.StringComparison.Ordinal))
                parent.gameObject.SetActive(visible);
            else
                label.gameObject.SetActive(visible);
        }

        static float TurkeyLabelThreshold(float aspect)
        {
            MapFrames.Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect < 0.05f ? 16f / 9f : aspect, MapFrames.TurkeyFocusPadding, out _, out var size);
            return size * 1.35f;
        }

        sealed class Binding
        {
            public string CityId;
            public LocalizedString Source;
            public Text Target;
            public string Value = string.Empty;
            public LocalizedString.ChangeHandler Handler;
        }
    }
}
