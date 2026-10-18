using System.Collections.Generic;
using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    [DisallowMultipleComponent]
    public sealed class MapShellView : MonoBehaviour
    {
        [SerializeField] MapCameraRig cameraRig;
        [SerializeField] MapMarkerLayer markers;
        [SerializeField] Text titleText;
        [SerializeField] Text scopeText;
        [SerializeField] Text worldLabel;
        [SerializeField] Text turkeyLabel;
        [SerializeField] Text zoomInLabel;
        [SerializeField] Text zoomOutLabel;
        [SerializeField] Text restoreLabel;
        [SerializeField] Text collapseLabel;
        [SerializeField] Text inspectorTitle;
        [SerializeField] Text inspectorScopeLabel;
        [SerializeField] Text inspectorScopeValue;
        [SerializeField] Text inspectorCityLabel;
        [SerializeField] Text inspectorCityValue;
        [SerializeField] Text inspectorCoordinatesLabel;
        [SerializeField] Text inspectorCoordinatesValue;
        [SerializeField] Text statusText;
        [SerializeField] GameObject inspectorPanel;
        [SerializeField] GameObject coordinatesRow;

        readonly List<Binding> _bindings = new List<Binding>(16);
        bool _subscribed;
        bool _inspectorVisible = true;
        string _title;
        string _viewWorld;
        string _viewTurkey;
        string _collapse;
        string _expand;
        string _selectCity;

        void Awake()
        {
            Track(titleText, MapContent.Title, value => _title = value);
            Track(worldLabel, MapContent.FocusWorld, null);
            Track(turkeyLabel, MapContent.FocusTurkey, null);
            Track(zoomInLabel, MapContent.ZoomIn, null);
            Track(zoomOutLabel, MapContent.ZoomOut, null);
            Track(restoreLabel, MapContent.Restore, null);
            Track(inspectorTitle, MapContent.InspectorTitle, null);
            Track(inspectorScopeLabel, MapContent.InspectorScope, null);
            Track(inspectorCityLabel, MapContent.InspectorCity, null);
            Track(inspectorCoordinatesLabel, MapContent.InspectorCoordinates, null);
            Track(null, MapContent.ViewWorld, value => _viewWorld = value);
            Track(null, MapContent.ViewTurkey, value => _viewTurkey = value);
            Track(null, MapContent.Collapse, value => _collapse = value);
            Track(null, MapContent.Expand, value => _expand = value);
            Track(null, MapContent.SelectCity, value => _selectCity = value);
        }

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            Subscribe();
            if (cameraRig != null)
                cameraRig.ScopeChanged += OnPresentationChanged;
            if (markers != null)
                markers.SelectionChanged += OnPresentationChanged;
            RefreshDynamic();
        }

        void OnDisable()
        {
            if (cameraRig != null)
                cameraRig.ScopeChanged -= OnPresentationChanged;
            if (markers != null)
                markers.SelectionChanged -= OnPresentationChanged;
            Unsubscribe();
        }

        public void FocusWorld()
        {
            if (cameraRig != null)
                cameraRig.FocusWorld();
        }

        public void FocusTurkey()
        {
            if (cameraRig != null)
                cameraRig.FocusTurkey();
        }

        public void ZoomIn()
        {
            if (cameraRig != null)
                cameraRig.ZoomIn();
        }

        public void ZoomOut()
        {
            if (cameraRig != null)
                cameraRig.ZoomOut();
        }

        public void Restore()
        {
            if (cameraRig != null)
                cameraRig.Restore();
        }

        public void ToggleInspector()
        {
            _inspectorVisible = !_inspectorVisible;
            if (inspectorPanel != null)
                inspectorPanel.SetActive(_inspectorVisible);
            RefreshDynamic();
        }

        void OnPresentationChanged()
        {
            RefreshDynamic();
        }

        void Track(Text target, string key, System.Action<string> extra)
        {
            var binding = new Binding
            {
                Source = new LocalizedString(MapContent.Table, key),
                Target = target,
                Extra = extra
            };
            binding.Handler = value =>
            {
                binding.Value = value ?? string.Empty;
                if (binding.Target != null)
                    binding.Target.text = binding.Value;
                binding.Extra?.Invoke(binding.Value);
                RefreshDynamic();
            };
            _bindings.Add(binding);
        }

        void Subscribe()
        {
            if (_subscribed)
                return;

            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Source.StringChanged += _bindings[i].Handler;

            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed)
                return;

            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Source.StringChanged -= _bindings[i].Handler;

            _subscribed = false;
        }

        void OnLocaleChanged(Locale locale)
        {
            RefreshDynamic();
        }

        void RefreshDynamic()
        {
            var turkey = cameraRig == null || cameraRig.Scope == MapScope.Turkey;
            var scope = turkey ? _viewTurkey : _viewWorld;
            if (scopeText != null)
                scopeText.text = scope ?? string.Empty;
            if (inspectorScopeValue != null)
                inspectorScopeValue.text = scope ?? string.Empty;

            var cityId = markers != null ? markers.SelectedId : null;
            string cityName = null;
            var hasCity = !string.IsNullOrEmpty(cityId) && markers.TryGetLocalizedName(cityId, out cityName);
            if (inspectorCityValue != null)
                inspectorCityValue.text = hasCity ? cityName : (_selectCity ?? string.Empty);

            if (coordinatesRow != null)
                coordinatesRow.SetActive(hasCity);

            if (hasCity && inspectorCoordinatesValue != null && MapCities.TryGet(cityId, out var city))
            {
                var culture = LocaleService.CurrentCulture;
                inspectorCoordinatesValue.text =
                    LocalizedFormatting.Number(city.Latitude, culture, 2) + ", " +
                    LocalizedFormatting.Number(city.Longitude, culture, 2);
            }

            if (statusText != null)
            {
                if (hasCity)
                    statusText.text = scope + "  ·  " + cityName;
                else
                    statusText.text = scope ?? string.Empty;
            }

            if (collapseLabel != null)
                collapseLabel.text = _inspectorVisible ? (_collapse ?? string.Empty) : (_expand ?? string.Empty);

            TintScopeButton(worldLabel, !turkey);
            TintScopeButton(turkeyLabel, turkey);

            if (titleText != null && string.IsNullOrEmpty(titleText.text))
                titleText.text = _title ?? string.Empty;
        }

        static void TintScopeButton(Text label, bool active)
        {
            if (label == null)
                return;

            label.color = active
                ? new Color(0.55f, 0.93f, 1f, 1f)
                : new Color(0.82f, 0.89f, 0.94f, 1f);
            var image = label.transform.parent != null ? label.transform.parent.GetComponent<Image>() : null;
            if (image != null)
            {
                image.color = active
                    ? new Color(0.07f, 0.24f, 0.34f, 1f)
                    : new Color(0.06f, 0.12f, 0.18f, 1f);
            }
        }

        sealed class Binding
        {
            public LocalizedString Source;
            public Text Target;
            public string Value = string.Empty;
            public LocalizedString.ChangeHandler Handler;
            public System.Action<string> Extra;
        }
    }
}
