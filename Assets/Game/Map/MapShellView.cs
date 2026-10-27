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

        bool _chromeReady;
        RectTransform _topBar;
        RectTransform _rail;
        RectTransform _statusBar;
        RectTransform _inspectorRect;
        HorizontalLayoutGroup _topLayout;
        VerticalLayoutGroup _railLayout;
        VerticalLayoutGroup _inspectorLayout;
        LayoutElement _titleElement;
        LayoutElement _scopeElement;
        LayoutElement _collapseElement;
        readonly LayoutElement[] _railButtons = new LayoutElement[MapHudLayout.RailButtonCount];
        readonly Text[] _railLabels = new Text[MapHudLayout.RailButtonCount];
        CanvasScaler _scaler;
        int _layoutWidth = -1;
        int _layoutHeight = -1;
        Rect _layoutSafe;
        bool _layoutInspector;
        bool _layoutApplied;
        bool _coordinatesShown = true;
        string _fittedTitle = string.Empty;
        string _fittedScope = string.Empty;
        string _fittedStatus = string.Empty;
        Text _brand;
        LayoutElement _brandElement;
        Image _topRule;
        Image _inspectorRule;
        Image _railImage;
        Image _topImage;
        Image _inspectorImage;
        Image _statusImage;
        Image _collapseImage;
        readonly Image[] _glyphs = new Image[MapHudLayout.RailButtonCount];
        readonly Image[] _accents = new Image[MapHudLayout.RailButtonCount];

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
            ApplyCurrentScreen();
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

        internal bool InspectorVisible => _inspectorVisible;

        public void ToggleInspector()
        {
            SetInspectorVisible(!_inspectorVisible);
            RefreshDynamic();
            _layoutApplied = false;
            ApplyCurrentScreen();
        }

        internal void SetInspectorVisible(bool visible)
        {
            _inspectorVisible = visible;
            if (inspectorPanel != null)
                inspectorPanel.SetActive(visible);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            ApplyCurrentScreen();
        }

        internal void ApplyHudLayout(float screenWidth, float screenHeight, Rect safeArea)
        {
            EnsureChrome();
            var reference = _scaler != null ? _scaler.referenceResolution : new Vector2(MapHudLayout.ReferenceWidth, MapHudLayout.ReferenceHeight);
            var match = _scaler != null ? _scaler.matchWidthOrHeight : MapHudLayout.Match;
            var metrics = MapHudLayout.Evaluate(screenWidth, screenHeight, safeArea, _inspectorVisible, reference, match);
            var safeWidth = safeArea.width >= 1f ? safeArea.width : screenWidth;
            var safeHeight = safeArea.height >= 1f ? safeArea.height : screenHeight;
            ApplyMetrics(metrics, safeWidth, safeHeight);
            _layoutWidth = screenWidth < 1f ? 0 : Mathf.RoundToInt(screenWidth);
            _layoutHeight = screenHeight < 1f ? 0 : Mathf.RoundToInt(screenHeight);
            _layoutSafe = safeArea;
            _layoutInspector = _inspectorVisible;
            _layoutApplied = true;
        }

        void ApplyCurrentScreen()
        {
            var width = Screen.width;
            var height = Screen.height;
            var safe = Screen.safeArea;
            if (_layoutApplied &&
                width == _layoutWidth &&
                height == _layoutHeight &&
                safe == _layoutSafe &&
                _inspectorVisible == _layoutInspector)
            {
                return;
            }

            ApplyHudLayout(width, height, safe);
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
            {
                coordinatesRow.SetActive(hasCity);
                if (_coordinatesShown != hasCity)
                {
                    _coordinatesShown = hasCity;
                    _layoutApplied = false;
                }
            }

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

            var title = _title ?? string.Empty;
            var headerScope = scopeText != null ? scopeText.text ?? string.Empty : string.Empty;
            var status = statusText != null ? statusText.text ?? string.Empty : string.Empty;
            if (title != _fittedTitle || headerScope != _fittedScope || status != _fittedStatus)
            {
                _fittedTitle = title;
                _fittedScope = headerScope;
                _fittedStatus = status;
                _layoutApplied = false;
            }
        }

        void EnsureChrome()
        {
            if (_chromeReady)
                return;

            _chromeReady = true;
            _topBar = titleText != null ? titleText.transform.parent as RectTransform : null;
            _statusBar = statusText != null ? statusText.transform.parent as RectTransform : null;
            _inspectorRect = inspectorPanel != null ? inspectorPanel.transform as RectTransform : null;
            _rail = ParentRail(worldLabel);
            _topLayout = _topBar != null ? _topBar.GetComponent<HorizontalLayoutGroup>() : null;
            _railLayout = _rail != null ? _rail.GetComponent<VerticalLayoutGroup>() : null;
            _inspectorLayout = _inspectorRect != null ? _inspectorRect.GetComponent<VerticalLayoutGroup>() : null;
            _titleElement = titleText != null ? titleText.GetComponent<LayoutElement>() : null;
            _scopeElement = scopeText != null ? scopeText.GetComponent<LayoutElement>() : null;
            _collapseElement = collapseLabel != null ? collapseLabel.GetComponentInParent<LayoutElement>() : null;
            CacheRailButton(0, worldLabel);
            CacheRailButton(1, turkeyLabel);
            CacheRailButton(2, zoomInLabel);
            CacheRailButton(3, zoomOutLabel);
            CacheRailButton(4, restoreLabel);
            var canvas = titleText != null ? titleText.GetComponentInParent<Canvas>() : null;
            if (canvas == null && worldLabel != null)
                canvas = worldLabel.GetComponentInParent<Canvas>();
            _scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            _railImage = _rail != null ? _rail.GetComponent<Image>() : null;
            _topImage = _topBar != null ? _topBar.GetComponent<Image>() : null;
            _inspectorImage = _inspectorRect != null ? _inspectorRect.GetComponent<Image>() : null;
            _statusImage = _statusBar != null ? _statusBar.GetComponent<Image>() : null;
            _collapseImage = collapseLabel != null && collapseLabel.transform.parent != null
                ? collapseLabel.transform.parent.GetComponent<Image>()
                : null;
            EnsureAdornments();
        }

        void CacheRailButton(int index, Text label)
        {
            _railLabels[index] = label;
            if (label == null || label.transform.parent == null)
                return;

            _railButtons[index] = label.transform.parent.GetComponent<LayoutElement>();
        }

        static RectTransform ParentRail(Text label)
        {
            if (label == null || label.transform.parent == null || label.transform.parent.parent == null)
                return null;

            return label.transform.parent.parent as RectTransform;
        }

        void ApplyMetrics(MapHudLayout.Metrics metrics, float safeWidth, float safeHeight)
        {
            if (_topBar != null)
            {
                var size = _topBar.sizeDelta;
                size.y = metrics.TopBarHeight;
                _topBar.sizeDelta = size;
            }

            if (_topLayout != null)
            {
                _topLayout.padding = new RectOffset(
                    Mathf.RoundToInt(metrics.TopPaddingLeft),
                    Mathf.RoundToInt(metrics.TopPaddingRight),
                    Mathf.RoundToInt(metrics.TopPaddingVertical),
                    Mathf.RoundToInt(metrics.TopPaddingVertical));
                _topLayout.spacing = metrics.TopSpacing;
            }

            if (_statusBar != null)
            {
                _statusBar.anchorMin = Vector2.zero;
                _statusBar.anchorMax = Vector2.zero;
                _statusBar.pivot = Vector2.zero;
                _statusBar.anchoredPosition = new Vector2(metrics.RailWidth + metrics.Gap, metrics.Gap + 6f);
            }

            if (_rail != null)
            {
                _rail.offsetMin = new Vector2(_rail.offsetMin.x, metrics.Gap);
                _rail.offsetMax = new Vector2(metrics.RailWidth, -metrics.TopBarHeight);
            }

            if (_railLayout != null)
            {
                var pad = Mathf.RoundToInt(metrics.RailPadding);
                _railLayout.padding = new RectOffset(pad, pad, pad, pad);
                _railLayout.spacing = metrics.RailSpacing;
            }

            if (_inspectorRect != null)
            {
                var scale = metrics.CanvasScale < 0.0001f ? 1f : metrics.CanvasScale;
                var localHeight = safeHeight / scale;
                var height = metrics.CardHeight(_coordinatesShown);
                var limit = localHeight - metrics.TopBarHeight - metrics.StatusHeight - metrics.Gap * 2f;
                if (limit > 40f && height > limit)
                    height = limit;
                _inspectorRect.anchorMin = new Vector2(1f, 1f);
                _inspectorRect.anchorMax = new Vector2(1f, 1f);
                _inspectorRect.pivot = new Vector2(1f, 1f);
                _inspectorRect.sizeDelta = new Vector2(metrics.InspectorWidth, height);
                _inspectorRect.anchoredPosition = new Vector2(-metrics.Gap, -(metrics.TopBarHeight + metrics.Gap));
            }

            if (_inspectorLayout != null)
            {
                var pad = Mathf.RoundToInt(metrics.InspectorPadding);
                _inspectorLayout.padding = new RectOffset(pad, pad, pad, pad);
                _inspectorLayout.spacing = metrics.InspectorSpacing;
            }

            SetHeight(_titleElement, metrics.TopBarHeight * 0.625f);
            SetWidth(_scopeElement, metrics.ScopeWidth);
            if (_collapseElement != null)
            {
                _collapseElement.minHeight = metrics.CollapseHeight;
                _collapseElement.preferredHeight = metrics.CollapseHeight;
                _collapseElement.minWidth = metrics.CollapseMinWidth;
                _collapseElement.preferredWidth = metrics.CollapseWidth;
            }

            for (var i = 0; i < _railButtons.Length; i++)
            {
                var element = _railButtons[i];
                if (element == null)
                    continue;

                element.minHeight = metrics.RailButtonHeight;
                element.preferredHeight = metrics.RailButtonHeight;
                element.flexibleHeight = 0f;
                element.flexibleWidth = 0f;
            }

            SetFont(_railLabels[0], metrics.RailLabelSize);
            SetFont(_railLabels[1], metrics.RailLabelSize);
            SetFont(_railLabels[2], metrics.ZoomLabelSize);
            SetFont(_railLabels[3], metrics.ZoomLabelSize);
            SetFont(_railLabels[4], metrics.RailLabelSize);
            FitRailLabel(_railLabels[0]);
            FitRailLabel(_railLabels[1]);
            FitRailLabel(_railLabels[2]);
            FitRailLabel(_railLabels[3]);
            FitRailLabel(_railLabels[4]);
            if (_rail != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rail);
            SetFont(titleText, metrics.TitleSize);
            SetFont(scopeText, metrics.ScopeSize);
            SetFont(collapseLabel, metrics.CollapseSize);
            SetFont(inspectorTitle, metrics.InspectorTitleSize);
            SetFont(inspectorScopeLabel, metrics.InspectorLabelSize);
            SetFont(inspectorScopeValue, metrics.InspectorValueSize);
            SetFont(inspectorCityLabel, metrics.InspectorLabelSize);
            SetFont(inspectorCityValue, metrics.InspectorValueSize);
            SetFont(inspectorCoordinatesLabel, metrics.InspectorLabelSize);
            SetFont(inspectorCoordinatesValue, metrics.InspectorValueSize);
            SetFont(statusText, metrics.StatusSize);
            FitHeader(metrics, safeWidth);
            FitStatusChip(metrics, safeWidth);
            PlaceInspectorRule(metrics);
            SetOwnedHeight(inspectorTitle, metrics.InspectorTitleRow);
            SetOwnedHeight(inspectorScopeLabel, metrics.InspectorLabelRow);
            SetOwnedHeight(inspectorScopeValue, metrics.InspectorValueRow);
            SetOwnedHeight(inspectorCityLabel, metrics.InspectorLabelRow);
            SetOwnedHeight(inspectorCityValue, metrics.InspectorValueRow);
            SetOwnedHeight(inspectorCoordinatesLabel, metrics.InspectorLabelRow);
            SetOwnedHeight(inspectorCoordinatesValue, metrics.InspectorValueRow);
            if (coordinatesRow != null)
            {
                var element = coordinatesRow.GetComponent<LayoutElement>();
                if (element != null)
                {
                    element.minHeight = metrics.CoordinatesRow;
                    element.preferredHeight = metrics.CoordinatesRow;
                }
            }

            PlaceGlyphs(metrics);
            StyleChrome();
        }

        static void SetHeight(LayoutElement element, float height)
        {
            if (element == null)
                return;

            element.minHeight = height;
            element.preferredHeight = height;
        }

        static void SetWidth(LayoutElement element, float width)
        {
            if (element == null)
                return;

            element.preferredWidth = width;
        }

        static void SetFont(Text text, int size)
        {
            if (text == null || size < 1)
                return;

            text.resizeTextForBestFit = false;
            text.fontSize = size;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        static void FitRailLabel(Text text)
        {
            if (text == null)
                return;

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(6f, 4f);
            rect.offsetMax = new Vector2(-6f, -4f);
            text.alignment = TextAnchor.MiddleLeft;
        }

        static void SetOwnedHeight(Text text, float height)
        {
            if (text == null)
                return;

            var element = text.GetComponent<LayoutElement>();
            if (element == null)
                return;

            element.minHeight = height;
            element.preferredHeight = height;
        }

        void TintScopeButton(Text label, bool active)
        {
            var index = -1;
            if (label == worldLabel)
                index = 0;
            else if (label == turkeyLabel)
                index = 1;
            StyleCommand(index, label, active);
        }

        void EnsureAdornments()
        {
            _brand = EnsureBrand();
            _topRule = EnsureRule(_topBar, "TopRule");
            _inspectorRule = EnsureRule(_inspectorRect, "InspectorRule");
            for (var i = 0; i < _railLabels.Length; i++)
                EnsureCommandChrome(i, _railLabels[i]);
            QuietButton(_collapseImage != null ? _collapseImage.GetComponent<UnityEngine.UI.Button>() : null);
            for (var i = 2; i < _railButtons.Length; i++)
                QuietButton(_railButtons[i] != null ? _railButtons[i].GetComponent<UnityEngine.UI.Button>() : null);
            QuietButton(worldLabel != null ? worldLabel.GetComponentInParent<UnityEngine.UI.Button>() : null);
            QuietButton(turkeyLabel != null ? turkeyLabel.GetComponentInParent<UnityEngine.UI.Button>() : null);
        }

        Text EnsureBrand()
        {
            if (_topBar == null || titleText == null)
                return _brand;

            if (_brand != null)
                return _brand;

            var existing = _topBar.Find("Brand");
            if (existing != null)
            {
                _brand = existing.GetComponent<Text>();
                _brandElement = existing.GetComponent<LayoutElement>();
                return _brand;
            }

            var font = titleText.font != null
                ? titleText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                return null;

            var brandObject = new GameObject("Brand", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            brandObject.hideFlags = HideFlags.DontSave;
            brandObject.transform.SetParent(_topBar, false);
            brandObject.transform.SetSiblingIndex(0);
            _brand = brandObject.GetComponent<Text>();
            _brand.font = font;
            _brand.text = MapHudPalette.Brand;
            _brand.alignment = TextAnchor.MiddleLeft;
            _brand.color = MapHudPalette.Accent;
            _brand.raycastTarget = false;
            _brand.horizontalOverflow = HorizontalWrapMode.Overflow;
            _brand.verticalOverflow = VerticalWrapMode.Truncate;
            _brand.fontStyle = FontStyle.Bold;
            _brandElement = brandObject.GetComponent<LayoutElement>();
            _brandElement.flexibleWidth = 0f;
            return _brand;
        }

        static Image EnsureRule(RectTransform parent, string name)
        {
            if (parent == null)
                return null;

            var existing = parent.Find(name);
            Image image;
            if (existing != null)
            {
                image = existing.GetComponent<Image>();
            }
            else
            {
                var ruleObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                ruleObject.hideFlags = HideFlags.DontSave;
                ruleObject.transform.SetParent(parent, false);
                image = ruleObject.GetComponent<Image>();
                var element = ruleObject.GetComponent<LayoutElement>();
                element.ignoreLayout = true;
            }

            if (image == null)
                return null;

            image.raycastTarget = false;
            image.color = MapHudPalette.Rule;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 2f);
            rect.anchoredPosition = Vector2.zero;
            return image;
        }

        void EnsureCommandChrome(int index, Text label)
        {
            if (label == null || label.transform.parent == null)
                return;

            var parent = label.transform.parent;
            _glyphs[index] = EnsureIcon(parent, "Glyph", MapHudGlyphs.Get(index));
            _accents[index] = EnsureIcon(parent, "Accent", MapHudGlyphs.Rounded());
            var buttonImage = parent.GetComponent<Image>();
            if (buttonImage != null && buttonImage.sprite == null)
            {
                buttonImage.sprite = MapHudGlyphs.Rounded();
                buttonImage.type = Image.Type.Sliced;
            }
        }

        static Image EnsureIcon(Transform parent, string name, Sprite sprite)
        {
            var existing = parent.Find(name);
            Image image;
            if (existing != null)
            {
                image = existing.GetComponent<Image>();
            }
            else
            {
                var iconObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconObject.hideFlags = HideFlags.DontSave;
                iconObject.transform.SetParent(parent, false);
                image = iconObject.GetComponent<Image>();
                iconObject.GetComponent<LayoutElement>().ignoreLayout = true;
            }

            if (image == null)
                return null;

            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        static void QuietButton(UnityEngine.UI.Button button)
        {
            if (button == null)
                return;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = MapHudPalette.Highlighted;
            colors.pressedColor = MapHudPalette.Pressed;
            colors.selectedColor = MapHudPalette.Highlighted;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        void PlaceGlyphs(MapHudLayout.Metrics metrics)
        {
            var icon = metrics.IconSize;
            var inset = 8f;
            for (var i = 0; i < _glyphs.Length; i++)
            {
                var symbolOnly = i == 2 || i == 3;
                var glyph = _glyphs[i];
                if (glyph != null)
                {
                    var rect = glyph.rectTransform;
                    var glyphSize = symbolOnly ? Mathf.Min(metrics.RailButtonHeight * 0.42f, icon + 4f) : icon;
                    if (symbolOnly)
                    {
                        rect.anchorMin = new Vector2(0.5f, 0.5f);
                        rect.anchorMax = new Vector2(0.5f, 0.5f);
                        rect.pivot = new Vector2(0.5f, 0.5f);
                        rect.anchoredPosition = Vector2.zero;
                    }
                    else
                    {
                        rect.anchorMin = new Vector2(0f, 0.5f);
                        rect.anchorMax = new Vector2(0f, 0.5f);
                        rect.pivot = new Vector2(0f, 0.5f);
                        rect.anchoredPosition = new Vector2(inset, 0f);
                    }

                    rect.sizeDelta = new Vector2(glyphSize, glyphSize);
                }

                var accent = _accents[i];
                if (accent != null)
                {
                    accent.type = Image.Type.Sliced;
                    var rect = accent.rectTransform;
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    rect.offsetMin = new Vector2(0f, 8f);
                    rect.offsetMax = new Vector2(3f, -8f);
                }

                var label = _railLabels[i];
                if (label == null)
                    continue;

                label.enabled = !symbolOnly;
                if (symbolOnly)
                    continue;

                var labelRect = label.rectTransform;
                labelRect.offsetMin = new Vector2(inset + icon + 6f, 4f);
                labelRect.offsetMax = new Vector2(-8f, -4f);
            }
        }

        void FitHeader(MapHudLayout.Metrics metrics, float safeWidth)
        {
            if (_brand == null || _brandElement == null)
                return;

            var scale = metrics.CanvasScale < 0.0001f ? 1f : metrics.CanvasScale;
            var readable = Mathf.Max(14, Mathf.RoundToInt(15f / scale));
            var brandSize = Mathf.Max(readable, Mathf.RoundToInt(metrics.TitleSize * 0.56f));
            var belowTitle = Mathf.Max(readable, metrics.TitleSize - 4);
            if (brandSize > belowTitle)
                brandSize = belowTitle;
            if (brandSize >= metrics.TitleSize && metrics.TitleSize > 2)
                brandSize = metrics.TitleSize - 2;
            PrepareLine(_brand, brandSize, FontStyle.Bold);
            _brand.alignment = TextAnchor.MiddleLeft;
            KeepOneLine(titleText);
            KeepOneLine(scopeText);
            KeepOneLine(collapseLabel);
            if (titleText != null)
                titleText.alignment = TextAnchor.MiddleLeft;

            var inner = HeaderInnerWidth(metrics, safeWidth);
            var collapseWidth = _collapseElement != null ? _collapseElement.preferredWidth : metrics.CollapseWidth;
            if (collapseWidth < metrics.CollapseMinWidth)
                collapseWidth = metrics.CollapseMinWidth;
            var titleWidth = titleText != null ? MeasuredWidth(titleText) + 8f : 0f;
            var scopeWidth = scopeText != null ? MeasuredWidth(scopeText) + 12f : 0f;
            var brandWidth = MeasuredWidth(_brand) + 10f;
            var gaps = metrics.TopSpacing * 3f;
            var chrome = metrics.TopPaddingLeft + metrics.TopPaddingRight + gaps;
            var showScope = scopeWidth > 1f && chrome + collapseWidth + titleWidth + scopeWidth + brandWidth <= inner;
            if (!showScope)
                scopeWidth = 0f;

            var reserved = chrome + collapseWidth + Mathf.Max(48f, titleWidth);
            while (brandSize > readable && reserved + brandWidth > inner)
            {
                brandSize--;
                PrepareLine(_brand, brandSize, FontStyle.Bold);
                brandWidth = MeasuredWidth(_brand) + 10f;
            }

            SetSlot(_brandElement, brandWidth, false);
            if (_titleElement != null)
            {
                var titleSlot = Mathf.Max(48f, titleWidth);
                var room = inner - chrome - collapseWidth - brandWidth - scopeWidth;
                if (room > 48f && titleSlot > room)
                    titleSlot = room;
                _titleElement.minWidth = titleSlot;
                _titleElement.flexibleWidth = 1f;
            }

            if (scopeText != null)
                scopeText.enabled = showScope;
            if (_scopeElement != null)
            {
                _scopeElement.ignoreLayout = !showScope;
                if (showScope)
                    SetSlot(_scopeElement, scopeWidth, false);
            }
        }

        void FitStatusChip(MapHudLayout.Metrics metrics, float safeWidth)
        {
            if (_statusBar == null)
                return;

            var scale = metrics.CanvasScale < 0.0001f ? 1f : metrics.CanvasScale;
            var room = safeWidth / scale - metrics.RailWidth - metrics.Gap * 3f;
            if (_statusBar.parent is RectTransform parent && parent.rect.width > 1f)
            {
                var parentRoom = parent.rect.width - metrics.RailWidth - metrics.Gap * 3f;
                if (parentRoom > 1f && (room < 1f || parentRoom < room))
                    room = parentRoom;
            }

            var chip = 96f;
            if (statusText != null && !string.IsNullOrEmpty(statusText.text))
            {
                statusText.alignment = TextAnchor.MiddleCenter;
                KeepOneLine(statusText);
                var measured = MeasuredWidth(statusText) + 28f;
                if (measured > chip)
                    chip = measured;
            }

            var limit = room > 1f ? room * 0.48f : metrics.ChipWidth;
            if (chip > limit && limit > 96f)
                chip = limit;

            _statusBar.anchorMin = Vector2.zero;
            _statusBar.anchorMax = Vector2.zero;
            _statusBar.pivot = Vector2.zero;
            _statusBar.sizeDelta = new Vector2(chip, metrics.StatusHeight);
            var statusElement = _statusBar.GetComponent<LayoutElement>();
            if (statusElement != null)
            {
                statusElement.minWidth = chip;
                statusElement.preferredWidth = chip;
                statusElement.flexibleWidth = 0f;
            }

            if (statusText != null)
            {
                statusText.rectTransform.offsetMin = new Vector2(14f, 0f);
                statusText.rectTransform.offsetMax = new Vector2(-14f, 0f);
            }
        }

        float HeaderInnerWidth(MapHudLayout.Metrics metrics, float safeWidth)
        {
            var scale = metrics.CanvasScale < 0.0001f ? 1f : metrics.CanvasScale;
            var estimated = safeWidth / scale;
            if (_topBar == null || _topBar.rect.width <= 1f)
                return estimated;

            var rectWidth = _topBar.rect.width;
            if (rectWidth < estimated)
                return rectWidth;
            return estimated;
        }

        static void KeepOneLine(Text text)
        {
            if (text == null)
                return;

            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        static void PrepareLine(Text text, int size, FontStyle style)
        {
            if (text == null)
                return;

            KeepOneLine(text);
            text.fontStyle = style;
            if (size > 0)
                text.fontSize = size;
        }

        static void SetSlot(LayoutElement element, float width, bool flexible)
        {
            if (element == null)
                return;

            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = flexible ? 1f : 0f;
            element.ignoreLayout = false;
        }

        static float MeasuredWidth(Text text)
        {
            if (text == null || text.font == null || string.IsNullOrEmpty(text.text))
                return 0f;

            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            var width = text.preferredWidth;
            var fallback = text.text.Length * text.fontSize * 0.68f;
            var cap = text.fontSize * text.text.Length * 1.2f;
            var floor = text.fontSize * text.text.Length * 0.42f;
            if (width < floor || width > cap)
                width = fallback;
            return width;
        }

        void PlaceInspectorRule(MapHudLayout.Metrics metrics)
        {
            if (_inspectorRule == null)
                return;

            var rect = _inspectorRule.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(metrics.InspectorPadding + metrics.InspectorTitleRow));
            rect.sizeDelta = new Vector2(-(metrics.InspectorPadding * 2f), 1f);
            _inspectorRule.color = MapHudPalette.Rule;
        }

        void StyleChrome()
        {
            Paint(_topImage, MapHudPalette.Panel, false);
            Paint(_inspectorImage, MapHudPalette.Card, true);
            Paint(_statusImage, MapHudPalette.Chip, true);
            if (_railImage != null)
            {
                _railImage.color = new Color(MapHudPalette.Panel.r, MapHudPalette.Panel.g, MapHudPalette.Panel.b, 0f);
                _railImage.raycastTarget = false;
            }

            if (_collapseImage != null)
            {
                if (_collapseImage.sprite == null)
                {
                    _collapseImage.sprite = MapHudGlyphs.Rounded();
                    _collapseImage.type = Image.Type.Sliced;
                }

                _collapseImage.color = MapHudPalette.Button;
            }

            if (titleText != null)
            {
                titleText.color = MapHudPalette.Text;
                titleText.fontStyle = FontStyle.Bold;
            }

            if (scopeText != null)
                scopeText.color = MapHudPalette.Accent;
            if (statusText != null)
                statusText.color = MapHudPalette.TextMuted;

            if (inspectorTitle != null)
                inspectorTitle.color = MapHudPalette.Text;
            PaintLabel(inspectorScopeLabel);
            PaintLabel(inspectorCityLabel);
            PaintLabel(inspectorCoordinatesLabel);
            PaintValue(inspectorScopeValue);
            PaintValue(inspectorCityValue);
            PaintValue(inspectorCoordinatesValue);
            if (collapseLabel != null)
                collapseLabel.color = MapHudPalette.Text;

            var turkey = cameraRig == null || cameraRig.Scope == MapScope.Turkey;
            StyleCommand(0, worldLabel, !turkey);
            StyleCommand(1, turkeyLabel, turkey);
            StyleCommand(2, zoomInLabel, false);
            StyleCommand(3, zoomOutLabel, false);
            StyleCommand(4, restoreLabel, false);
            if (_topRule != null)
                _topRule.color = MapHudPalette.Rule;
            if (_inspectorRule != null)
                _inspectorRule.color = MapHudPalette.Rule;
        }

        void StyleCommand(int index, Text label, bool active)
        {
            if (label != null)
                label.color = active ? MapHudPalette.Accent : MapHudPalette.Text;

            var parent = label != null ? label.transform.parent : null;
            var image = parent != null ? parent.GetComponent<Image>() : null;
            if (image != null)
                image.color = active ? MapHudPalette.ButtonActive : MapHudPalette.Button;

            if (index >= 0 && index < _glyphs.Length && _glyphs[index] != null)
                _glyphs[index].color = active ? MapHudPalette.Accent : MapHudPalette.TextMuted;
            if (index >= 0 && index < _accents.Length && _accents[index] != null)
                _accents[index].color = active ? MapHudPalette.Accent : new Color(1f, 1f, 1f, 0f);
        }

        static void Paint(Image image, Color color, bool rounded)
        {
            if (image == null)
                return;

            if (rounded && image.sprite == null)
            {
                image.sprite = MapHudGlyphs.Rounded();
                image.type = Image.Type.Sliced;
            }

            image.color = color;
        }

        static void PaintLabel(Text text)
        {
            if (text != null)
                text.color = MapHudPalette.TextMuted;
        }

        static void PaintValue(Text text)
        {
            if (text != null)
                text.color = MapHudPalette.Text;
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
