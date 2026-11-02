using System.Collections.Generic;
using LogistiCore.Localization;
using LogistiCore.UI;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// Runtime command shell for the world map. One header, one module rail,
    /// one city context panel, and one unconnected summary. The map scene keeps
    /// its geography. Home goes through <see cref="AppNavigation.ShowHome"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapCommandShell : MonoBehaviour
    {
        static readonly Color Surface = new Color(0.016f, 0.036f, 0.062f, 1f);
        static readonly Color SurfaceRaised = new Color(0.055f, 0.102f, 0.138f, 1f);
        static readonly Color StrokeColor = new Color(0.47f, 0.76f, 0.84f, 0.38f);
        RectTransform _rect;
        RectTransform _header;
        RectTransform _rail;
        RectTransform _railViewport;
        RectTransform _railContent;
        ScrollRect _railScroll;
        RectTransform _context;
        RectTransform _summary;
        RectTransform _future;
        RectTransform _leftBleed;
        RectTransform _rightBleed;
        Button _home;
        Button _close;
        Button _mapRow;
        readonly Button[] _moduleButtons = new Button[8];
        Text _section;
        Text _prompt;
        Text _cityName;
        Text _coordinateLabel;
        Text _coordinateValue;
        Text _summaryText;
        Image _contextImage;
        MapMarkerLayer _markers;
        MapCameraRig _rig;
        bool _built;
        bool _listening;
        bool _applying;
        PanelState _state;
        int _moduleIndex = -1;
        int _width = -1;
        int _height = -1;
        Rect _safe;
        MapCommandLayout.Metrics _metrics;
        string _mapTitle = string.Empty;
        string _cityHeading = string.Empty;
        string _selectPrompt = string.Empty;
        string _unavailable = string.Empty;
        readonly string[] _moduleNames = new string[8];

        readonly List<Binding> _bindings = new List<Binding>(16);

        public enum PanelState
        {
            Closed = 0,
            City = 1,
            Module = 2
        }

        public Button HomeButton => _home;
        public Button CloseButton => _close;
        public PanelState State => _state;
        public Rect Viewport => _metrics.Viewport;
        public Rect HeaderRect => _metrics.Header;
        public Rect RailRect => _metrics.Rail;
        public Rect ContextRect => _metrics.Context;
        public Rect SummaryRect => _metrics.Summary;
        public bool FutureModesVisible => _future != null && _future.gameObject.activeSelf;
        public bool ContextOpen => _state != PanelState.Closed && _metrics.ContextOpen;
        public bool CoordinatesVisible => _state == PanelState.City && _coordinateValue != null && _coordinateValue.gameObject.activeSelf;
        public string SummaryText => _summaryText != null ? _summaryText.text : string.Empty;
        public string CoordinateText => _state == PanelState.City && _coordinateValue != null ? _coordinateValue.text : string.Empty;
        public string CityText => _state == PanelState.City && _cityName != null && _cityName.enabled ? _cityName.text : string.Empty;
        public string PromptText => _state != PanelState.Closed && _prompt != null && _prompt.enabled ? _prompt.text : string.Empty;
        public string ModuleBody => _state == PanelState.Module ? (_moduleIndex < 0 ? _selectPrompt : _unavailable) : string.Empty;
        public int InteractiveButtonCount => GetComponentsInChildren<Button>(true).Length;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            var active = SceneManager.GetActiveScene();
            if (active.IsValid())
                Attach(active);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Attach(scene);
        }

        public static MapCommandShell Attach(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            if (scene.path != AppNavigation.MapScenePath && scene.name != AppNavigation.MapSceneName)
                return null;

            var roots = scene.GetRootGameObjects();
            Canvas canvas = null;
            for (var i = 0; i < roots.Length; i++)
            {
                var existing = roots[i].GetComponentInChildren<MapCommandShell>(true);
                if (existing != null)
                    return existing;

                if (canvas == null)
                    canvas = roots[i].GetComponentInChildren<Canvas>(true);
            }

            if (canvas == null)
                return null;

            var safe = canvas.GetComponentInChildren<SafeAreaRect>(true);
            var parent = safe != null ? safe.transform as RectTransform : canvas.transform as RectTransform;
            if (parent == null)
                return null;

            var item = new GameObject("CommandShell", typeof(RectTransform));
            item.hideFlags = HideFlags.DontSave;
            item.SetActive(false);
            var view = item.AddComponent<MapCommandShell>();
            if (item.scene != scene)
                SceneManager.MoveGameObjectToScene(item, scene);
            item.transform.SetParent(parent, false);
            item.SetActive(true);
            view.Apply(Screen.width, Screen.height, Screen.safeArea);
            return view;
        }

        void Awake()
        {
            BuildIfNeeded();
        }

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            BuildIfNeeded();
            BindScene();
            Subscribe();
            HookButtons();
            Apply(Screen.width, Screen.height, Screen.safeArea);
        }

        void OnDisable()
        {
            if (_markers != null)
                _markers.SelectionChanged -= OnSelectionChanged;

            if (_home != null && _listening)
            {
                _home.onClick.RemoveListener(OpenHome);
                if (_close != null)
                    _close.onClick.RemoveListener(CloseContext);
                if (_mapRow != null)
                    _mapRow.onClick.RemoveListener(ShowMapContext);
                for (var i = 0; i < _moduleButtons.Length; i++)
                {
                    if (_moduleButtons[i] != null)
                        _moduleButtons[i].onClick.RemoveAllListeners();
                }

                _listening = false;
            }

            Unsubscribe();
        }

        void OnDestroy()
        {
            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Dispose();
            _bindings.Clear();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            var width = Screen.width;
            var height = Screen.height;
            var safe = Screen.safeArea;
            if (width == _width && height == _height && safe == _safe)
                return;

            Apply(width, height, safe);
        }

        public void OpenHome()
        {
            AppNavigation.ShowHome();
        }

        public void ShowMapContext()
        {
            _state = PanelState.Module;
            _moduleIndex = -1;
            Relayout();
        }

        public void ShowModule(int index)
        {
            if (index < 0 || index >= _moduleButtons.Length)
                return;

            _state = PanelState.Module;
            _moduleIndex = index;
            Relayout();
        }

        public void CloseContext()
        {
            _state = PanelState.Closed;
            _moduleIndex = -1;
            Relayout();
        }

        public void Apply(float screenWidth, float screenHeight, Rect safeArea)
        {
            if (_applying)
                return;

            _applying = true;
            try
            {
                BuildIfNeeded();
                HookButtons();
                _metrics = MapCommandLayout.Evaluate(screenWidth, screenHeight, safeArea, _state != PanelState.Closed);
                if (!_metrics.ContextOpen)
                    _state = PanelState.Closed;

                Place(_metrics, screenWidth, screenHeight, safeArea);
                if (_future != null)
                    _future.gameObject.SetActive(_metrics.FutureModes);

                Present();
                if (Application.isPlaying && _rig != null)
                    _rig.ReframeForChrome();

                _width = screenWidth < 1f ? 0 : Mathf.RoundToInt(screenWidth);
                _height = screenHeight < 1f ? 0 : Mathf.RoundToInt(screenHeight);
                _safe = safeArea;
            }
            finally
            {
                _applying = false;
            }
        }

        string _shownCity;

        void Relayout()
        {
            if (_width > 0)
                Apply(_width, _height, _safe);
            else
                Present();
        }

        internal void ShowCity(string cityId)
        {
            _shownCity = cityId;
            var known = !string.IsNullOrEmpty(cityId) && MapCities.TryGet(cityId, out _);
            if (known)
            {
                _state = PanelState.City;
                _moduleIndex = -1;
            }
            else if (_state == PanelState.City)
            {
                _state = PanelState.Closed;
            }

            Relayout();
        }

        void Present()
        {
            var open = _state != PanelState.Closed && _metrics.ContextOpen;
            if (_context != null)
                _context.gameObject.SetActive(open);

            var city = open && _state == PanelState.City;
            var known = city && !string.IsNullOrEmpty(_shownCity) && MapCities.TryGet(_shownCity, out _);
            string localized = null;
            var named = known && _markers != null && _markers.TryGetLocalizedName(_shownCity, out localized) && !string.IsNullOrEmpty(localized);

            if (_section != null)
            {
                _section.enabled = open;
                if (open)
                    _section.text = city ? _cityHeading : (_moduleIndex < 0 ? _mapTitle : ModuleName(_moduleIndex));
            }

            if (_prompt != null)
            {
                _prompt.enabled = open && !city;
                if (_prompt.enabled)
                    _prompt.text = _moduleIndex < 0 ? _selectPrompt : _unavailable;
            }

            if (_cityName != null)
            {
                _cityName.enabled = city && named;
                _cityName.text = city && named ? localized : string.Empty;
            }

            if (_coordinateLabel != null)
                _coordinateLabel.gameObject.SetActive(known);
            if (_coordinateValue != null)
            {
                _coordinateValue.gameObject.SetActive(known);
                if (known && MapCities.TryGet(_shownCity, out var located))
                {
                    var culture = LocaleService.CurrentCulture;
                    _coordinateValue.text =
                        LocalizedFormatting.Number(located.Latitude, culture, 2) + ", " +
                        LocalizedFormatting.Number(located.Longitude, culture, 2);
                }
                else
                {
                    _coordinateValue.text = string.Empty;
                }
            }

            PaintRows();
        }

        string ModuleName(int index)
        {
            if (index < 0 || index >= _moduleNames.Length || string.IsNullOrEmpty(_moduleNames[index]))
                return string.Empty;

            return _moduleNames[index];
        }

        void Place(MapCommandLayout.Metrics metrics, float screenWidth, float screenHeight, Rect safeArea)
        {
            var safe = safeArea.width < 1f || safeArea.height < 1f
                ? new Rect(0f, 0f, screenWidth, screenHeight)
                : safeArea;
            var scale = MapHudLayout.CanvasScale(screenWidth, screenHeight);
            if (scale < 0.0001f)
                scale = 1f;

            StretchRoot();
            var leftBleed = Mathf.Max(0f, safe.xMin);
            var rightBleed = Mathf.Max(0f, screenWidth - safe.xMax);
            var bandBottom = metrics.Summary.yMax;
            var bandHeight = Mathf.Max(0f, metrics.Header.yMin - bandBottom);
            PlaceBleed(_leftBleed, new Rect(0f, bandBottom, leftBleed, bandHeight), safe, scale);
            PlaceBleed(_rightBleed, new Rect(screenWidth - rightBleed, bandBottom, rightBleed, bandHeight), safe, scale);
            PlaceRect(_header, metrics.Header, safe, scale);
            PlaceRect(_rail, metrics.Rail, safe, scale);
            if (!metrics.FutureModes)
                ShortenRail(metrics, safe, scale);
            PlaceRect(_context, metrics.Context, safe, scale);
            PlaceRect(_summary, metrics.Summary, safe, scale);
            PlaceRect(_home != null ? _home.transform as RectTransform : null, metrics.Home, metrics.Header, scale);
            var leftPad = Mathf.Max(0f, safe.xMin) / scale;
            var rightPad = Mathf.Max(0f, screenWidth - safe.xMax) / scale;
            PlaceClose(metrics, rightPad, scale);
            var topPad = Mathf.Max(0f, screenHeight - safe.yMax) / scale;
            var bottomPad = Mathf.Max(0f, safe.yMin) / scale;
            PlaceCopy(metrics, leftPad, rightPad, topPad, bottomPad, scale);

            SetFont(_summaryText, metrics.CanvasFont);
            SetFont(_prompt, metrics.CanvasFont);
            SetFont(_cityName, metrics.CanvasTitle);
            SetFont(_coordinateLabel, metrics.CanvasFont);
            SetFont(_coordinateValue, metrics.CanvasFont);
            var texts = GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == _cityName || text == _summaryText || text == _prompt || text == _coordinateLabel || text == _coordinateValue)
                    continue;
                if (text.transform.parent != null && text.transform.parent.name == "Home")
                    SetFont(text, metrics.CanvasTitle);
                else if (IsRailLabel(text))
                    SetFont(text, RailLabelCanvas(metrics, scale));
                else
                    SetFont(text, metrics.CanvasFont);
            }
        }

        void BuildIfNeeded()
        {
            if (_built)
                return;

            _built = true;
            _rect = (RectTransform)transform;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _leftBleed = EdgeFill("LeftBleed");
            _rightBleed = EdgeFill("RightBleed");
            _header = Panel("CommandHeader", out _);
            _rail = Panel("ModuleRail", out var railImage);
            railImage.raycastTarget = false;
            _context = Panel("ContextPanel", out _contextImage);
            _summary = Panel("SummaryBar", out _);
            var brand = Line(_header, "Brand", font, FontStyle.Bold, MapHudPalette.Accent);
            AnchorLeft(brand, 16f, 220f, 0f, 0f);
            var page = Line(_header, "Page", font, FontStyle.Bold, MapHudPalette.Text);
            AnchorLeft(page, 210f, 240f, 0f, 0f);
            _home = ActionButton(_header, "Home", font, out _);
            HideHomeLabel(FindText(_home.transform, "Label"));
            Edge(_home.transform, "Mark", font);
            _contextImage.color = Surface;
            Stroke(_header);
            Stroke(_rail);
            Stroke(_context);
            Stroke(_summary);
            Motif(_header, "BrandMark", MapHudPalette.Accent, 7f);
            var section = Line(_context, "Section", font, FontStyle.Bold, MapHudPalette.Accent);
            _prompt = Line(_context, "Prompt", font, FontStyle.Normal, MapHudPalette.Text);
            _cityName = Line(_context, "CityName", font, FontStyle.Bold, MapHudPalette.Text);
            _coordinateLabel = Line(_context, "CoordinateLabel", font, FontStyle.Normal, MapHudPalette.TextMuted);
            _coordinateValue = Line(_context, "CoordinateValue", font, FontStyle.Normal, MapHudPalette.Accent);
            _section = section;
            _close = ActionButton(_context, "Close", font, out var closeLabel);
            HideHomeLabel(closeLabel);
            var closeMark = Line(_close.transform, "Mark", font, FontStyle.Bold, MapHudPalette.Accent);
            closeMark.text = "\u00d7";
            closeMark.alignment = TextAnchor.MiddleCenter;
            var closeMarkRect = closeMark.rectTransform;
            closeMarkRect.anchorMin = Vector2.zero;
            closeMarkRect.anchorMax = Vector2.one;
            closeMarkRect.offsetMin = Vector2.zero;
            closeMarkRect.offsetMax = Vector2.zero;
            _summaryText = Line(_summary, "Label", font, FontStyle.Normal, MapHudPalette.TextMuted);
            InsetText(_summaryText, 28f, 0f, 0f, 16f);
            Motif(_summary, "Pip", MapHudPalette.AccentQuiet, 6f);
            BuildRailScroll();
            _mapRow = CurrentRow(_railContent, font);
            _future = FutureRows(_railContent, font);
            TopBand(_section, 16f, 28f, 0f);
            TopBand(_prompt, 52f, 64f, 0f);
            TopBand(_cityName, 52f, 40f, 0f);
            TopBand(_coordinateLabel, 100f, 24f, 0f);
            TopBand(_coordinateValue, 126f, 32f, 0f);
            Quiet(_home);
            Quiet(_close);
            Stroke(_home.transform as RectTransform);
            Stroke((RectTransform)_close.transform);
            Track(ShellContent.Table, ShellContent.Brand, FindText(_header, "Brand"));
            Track(MapContent.Table, MapContent.Title, FindText(_header, "Page"));
            Track(ShellContent.Table, ShellContent.Home, FindText(_home.transform, "Label"));
            Track(MapContent.Table, MapContent.Title, null, value =>
            {
                _mapTitle = value;
                RefreshCopy();
            });
            Track(MapContent.Table, MapContent.RailMap, FindText(_mapRow.transform, "Label"));
            Track(MapContent.Table, MapContent.InspectorCity, null, value =>
            {
                _cityHeading = value;
                RefreshCopy();
            });
            Track(MapContent.Table, MapContent.SelectCity, null, value =>
            {
                _selectPrompt = value;
                RefreshCopy();
            });
            Track(MapContent.Table, MapContent.ModuleUnavailable, null, value =>
            {
                _unavailable = value;
                RefreshCopy();
            });
            Track(MapContent.Table, MapContent.Close, closeLabel);
            Track(MapContent.Table, MapContent.InspectorCoordinates, _coordinateLabel);
            Track(MapContent.Table, MapContent.SummaryUnconnected, _summaryText);
            for (var i = 0; i < MapContent.FutureModes.Length; i++)
            {
                var slot = i;
                Track(MapContent.Table, MapContent.FutureModes[i], null, value =>
                {
                    _moduleNames[slot] = value;
                    RefreshCopy();
                });
                Track(MapContent.Table, MapContent.RailLabels[i + 1], FindText(_moduleButtons[i].transform, "Label"));
            }
        }

        void BindScene()
        {
            if (_markers != null)
                _markers.SelectionChanged -= OnSelectionChanged;

            _markers = null;
            _rig = null;
            var scene = gameObject.scene;
            if (!scene.IsValid())
                return;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (_markers == null)
                    _markers = roots[i].GetComponentInChildren<MapMarkerLayer>(true);
                if (_rig == null)
                    _rig = roots[i].GetComponentInChildren<MapCameraRig>(true);
            }

            if (_markers != null)
                _markers.SelectionChanged += OnSelectionChanged;
        }

        void OnSelectionChanged()
        {
            ShowCity(_markers != null ? _markers.SelectedId : null);
        }

        void HookButtons()
        {
            if (_listening || _home == null)
                return;

            _home.onClick.AddListener(OpenHome);
            if (_close != null)
                _close.onClick.AddListener(CloseContext);
            if (_mapRow != null)
                _mapRow.onClick.AddListener(ShowMapContext);
            for (var i = 0; i < _moduleButtons.Length; i++)
            {
                var slot = i;
                if (_moduleButtons[i] != null)
                    _moduleButtons[i].onClick.AddListener(() => ShowModule(slot));
            }

            _listening = true;
        }

        void Subscribe()
        {
            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Subscribe();

            LocalizationSettings.SelectedLocaleChanged -= OnLocale;
            LocalizationSettings.SelectedLocaleChanged += OnLocale;
        }

        void Unsubscribe()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocale;
            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Unsubscribe();
        }

        void OnLocale(Locale locale)
        {
            ShowCity(_shownCity);
        }

        void Track(string table, string key, Text target, System.Action<string> extra = null)
        {
            var binding = new Binding(table, key, target, value =>
            {
                extra?.Invoke(value);
                if (target == _prompt || extra != null)
                    ShowCity(_shownCity);
            });
            _bindings.Add(binding);
        }

        RectTransform Panel(string name, out Image image)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(transform, false);
            image = item.GetComponent<Image>();
            image.color = Surface;
            image.raycastTarget = true;
            return (RectTransform)item.transform;
        }

        Button ActionButton(RectTransform parent, string name, Font font, out Text label)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            image.color = SurfaceRaised;
            image.raycastTarget = true;
            var button = item.GetComponent<Button>();
            button.targetGraphic = image;
            label = Line(item.transform, "Label", font, FontStyle.Bold, MapHudPalette.Text);
            InsetText(label, 12f, 0f, 0f, 8f);
            return button;
        }

        Button CurrentRow(RectTransform parent, Font font)
        {
            return RailButton(parent, "CurrentMode", font, MapRailIcons.Map);
        }

        RectTransform FutureRows(RectTransform parent, Font font)
        {
            var root = new GameObject("FutureModes", typeof(RectTransform));
            root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            for (var i = 0; i < MapContent.FutureModes.Length; i++)
                _moduleButtons[i] = RailButton(rect, "Mode" + i, font, MapRailIcons.Truck + i);

            return rect;
        }

        Button RailButton(RectTransform parent, string name, Font font, int iconKind)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            row.hideFlags = HideFlags.DontSave;
            row.transform.SetParent(parent, false);
            var rect = (RectTransform)row.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(72f, 64f);
            var image = row.GetComponent<Image>();
            image.color = name == "CurrentMode" ? SurfaceRaised : new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            var button = row.GetComponent<Button>();
            button.targetGraphic = image;
            Quiet(button);
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            icon.hideFlags = HideFlags.DontSave;
            icon.transform.SetParent(row.transform, false);
            var iconImage = icon.GetComponent<Image>();
            iconImage.sprite = MapRailIcons.Get(iconKind);
            iconImage.color = MapHudPalette.Accent;
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(28f, 28f);
            iconRect.anchoredPosition = new Vector2(0f, -4f);
            var label = Line(row.transform, "Label", font, FontStyle.Bold, MapHudPalette.Accent);
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(-4f, 16f);
            labelRect.anchoredPosition = new Vector2(0f, 2f);
            return button;
        }

        static Text Line(Transform parent, string name, Font font, FontStyle style, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>();
            text.font = font;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = string.Empty;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        static void HideHomeLabel(Text label)
        {
            if (label == null)
                return;

            label.raycastTarget = false;
            var color = label.color;
            color.a = 0f;
            label.color = color;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        static void Edge(Transform parent, string name, Font font)
        {
            var text = Line(parent, name, font, FontStyle.Bold, MapHudPalette.Accent);
            text.text = "\u2190";
            text.alignment = TextAnchor.MiddleCenter;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        static void InsetText(Text text, float left, float bottom, float top, float right)
        {
            if (text == null)
                return;

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        static Text FindText(Transform root, string name)
        {
            if (root == null)
                return null;

            var child = root.Find(name);
            if (child == null)
            {
                for (var i = 0; i < root.childCount; i++)
                {
                    var found = FindText(root.GetChild(i), name);
                    if (found != null)
                        return found;
                }

                return null;
            }

            var text = child.GetComponent<Text>();
            if (text != null)
                return text;

            return child.GetComponentInChildren<Text>(true);
        }

        static void SetFont(Text text, int size)
        {
            if (text == null || size < 1)
                return;

            text.resizeTextForBestFit = false;
            text.fontSize = size;
        }

        static void Quiet(Button button)
        {
            if (button == null)
                return;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = MapHudPalette.Highlighted;
            colors.pressedColor = MapHudPalette.Pressed;
            colors.selectedColor = MapHudPalette.ButtonActive;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        void StretchRoot()
        {
            if (_rect == null)
                return;

            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.one;
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }

        void RefreshCopy()
        {
            if (_applying || !_built)
                return;

            Present();
        }

        void PaintRows()
        {
            var mapSelected = _state != PanelState.Module || _moduleIndex < 0;
            Paint(_mapRow, mapSelected);
            for (var i = 0; i < _moduleButtons.Length; i++)
                Paint(_moduleButtons[i], _state == PanelState.Module && _moduleIndex == i);
        }

        static void Paint(Button button, bool selected)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? SurfaceRaised : new Color(1f, 1f, 1f, 0f);
            var label = FindText(button.transform, "Label");
            var icon = button.transform.Find("Icon");
            var iconImage = icon != null ? icon.GetComponent<Image>() : null;
            var muted = new Color(MapHudPalette.TextMuted.r, MapHudPalette.TextMuted.g, MapHudPalette.TextMuted.b, 0.82f);
            if (iconImage != null)
                iconImage.color = selected ? MapHudPalette.Accent : muted;
            if (label == null)
                return;

            label.color = selected ? MapHudPalette.Accent : muted;
        }

        void PlaceCopy(MapCommandLayout.Metrics metrics, float leftPad, float rightPad, float topPad, float bottomPad, float scale)
        {
            var brand = FindText(_header, "Brand");
            var page = FindText(_header, "Page");
            if (brand != null)
            {
                brand.horizontalOverflow = HorizontalWrapMode.Overflow;
                brand.alignment = TextAnchor.MiddleLeft;
                var brandWidth = Mathf.Max(80f, "LOGISTICORE".Length * metrics.CanvasTitle * 0.62f + 8f);
                AnchorLeft(brand, leftPad + 32f, brandWidth, 0f, topPad);
                PlaceMark(_header, "BrandMark", leftPad + 16f, 7f, -topPad * 0.5f);
                if (page != null)
                {
                    page.horizontalOverflow = HorizontalWrapMode.Overflow;
                    page.alignment = TextAnchor.MiddleLeft;
                    AnchorLeft(page, leftPad + 32f + brandWidth + 16f, metrics.CanvasTitle * 8f, 0f, topPad);
                }
            }

            PlaceMark(_summary, "Pip", leftPad + 14f, 6f, bottomPad * 0.5f);
            InsetText(_summaryText, leftPad + 28f, bottomPad, 0f, rightPad + 16f);

            PlaceRail(metrics, leftPad, scale);

            var closeSide = Mathf.Max(metrics.CanvasFont * 1.6f, MapCommandLayout.MinTouchPixels / Mathf.Max(0.01f, scale));
            TopBand(_section, 18f, Mathf.Max(24f, metrics.CanvasFont * 1.3f), rightPad + closeSide);
            TopBand(_prompt, 18f + metrics.CanvasFont * 1.6f, metrics.CanvasFont * 2.4f, rightPad + closeSide);
            TopBand(_cityName, 18f + metrics.CanvasFont * 1.6f, metrics.CanvasFont * 1.8f, rightPad + closeSide);
            var coordinateTop = 18f + metrics.CanvasFont * 3.8f;
            TopBand(_coordinateLabel, coordinateTop, metrics.CanvasFont * 1.3f, rightPad + closeSide);
            TopBand(_coordinateValue, coordinateTop + metrics.CanvasFont * 1.4f, metrics.CanvasFont * 1.5f, rightPad + closeSide);
        }

        static void TopBand(Text text, float fromTop, float height, float rightPad)
        {
            if (text == null)
                return;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            var right = 16f + rightPad;
            rect.sizeDelta = new Vector2(-(16f + right), Mathf.Max(18f, height));
            rect.anchoredPosition = new Vector2((16f - right) * 0.5f, -fromTop);
        }

        void PlaceClose(MapCommandLayout.Metrics metrics, float rightPad, float scale)
        {
            if (_close == null)
                return;

            var side = Mathf.Max(metrics.CanvasFont * 1.6f, MapCommandLayout.MinTouchPixels / Mathf.Max(0.01f, scale));
            var rect = (RectTransform)_close.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(side, side);
            rect.anchoredPosition = new Vector2(-(10f + rightPad), -10f);
        }

        static void AnchorLeft(Text text, float x, float width, float bottomInset, float topInset)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, -(bottomInset + topInset));
            rect.anchoredPosition = new Vector2(x, (bottomInset - topInset) * 0.5f);
        }

        static void Stroke(RectTransform parent)
        {
            if (parent == null)
                return;

            Hair(parent, "StrokeTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f));
            Hair(parent, "StrokeBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f));
            Hair(parent, "StrokeLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0f));
            Hair(parent, "StrokeRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(1f, 0f));
        }

        static void Hair(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            var hair = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hair.hideFlags = HideFlags.DontSave;
            hair.transform.SetParent(parent, false);
            var rect = (RectTransform)hair.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            var image = hair.GetComponent<Image>();
            image.color = StrokeColor;
            image.raycastTarget = false;
        }

        static void Motif(Transform parent, string name, Color color, float size)
        {
            var mark = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            mark.hideFlags = HideFlags.DontSave;
            mark.transform.SetParent(parent, false);
            var rect = (RectTransform)mark.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(12f, 0f);
            var image = mark.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static void PlaceMark(RectTransform parent, string name, float x, float size, float y)
        {
            if (parent == null)
                return;

            var mark = parent.Find(name) as RectTransform;
            if (mark == null)
                return;

            mark.anchorMin = new Vector2(0f, 0.5f);
            mark.anchorMax = new Vector2(0f, 0.5f);
            mark.pivot = new Vector2(0f, 0.5f);
            mark.sizeDelta = new Vector2(size, size);
            mark.anchoredPosition = new Vector2(x, y);
        }

        void BuildRailScroll()
        {
            var viewport = new GameObject("RailViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewport.hideFlags = HideFlags.DontSave;
            viewport.transform.SetParent(_rail, false);
            _railViewport = (RectTransform)viewport.transform;
            var hit = viewport.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var content = new GameObject("RailContent", typeof(RectTransform));
            content.hideFlags = HideFlags.DontSave;
            content.transform.SetParent(_railViewport, false);
            _railContent = (RectTransform)content.transform;
            _railContent.anchorMin = new Vector2(0f, 1f);
            _railContent.anchorMax = new Vector2(0f, 1f);
            _railContent.pivot = new Vector2(0f, 1f);

            _railScroll = _rail.gameObject.AddComponent<ScrollRect>();
            _railScroll.viewport = _railViewport;
            _railScroll.content = _railContent;
            _railScroll.horizontal = false;
            _railScroll.vertical = false;
            _railScroll.movementType = ScrollRect.MovementType.Clamped;
            _railScroll.inertia = true;
            _railScroll.scrollSensitivity = 12f;
            _railScroll.verticalScrollbar = null;
            _railScroll.horizontalScrollbar = null;
        }

        void PlaceRail(MapCommandLayout.Metrics metrics, float leftPad, float scale)
        {
            if (_rail == null || _railViewport == null || _railContent == null)
                return;

            var icon = MapCommandLayout.RailIconPixels(metrics.FontPixels) / scale;
            var label = MapCommandLayout.RailLabelPixels(metrics.FontPixels) / scale;
            var rowHeight = MapCommandLayout.RailRowPixels(metrics.FontPixels) / scale;
            var gap = 2f / scale;
            var width = Mathf.Max(44f, metrics.Rail.width / scale - leftPad);
            var viewportHeight = Mathf.Max(rowHeight, _rail.sizeDelta.y);
            var count = metrics.FutureModes ? 1 + MapContent.FutureModes.Length : 1;
            var contentHeight = count * rowHeight + Mathf.Max(0, count - 1) * gap;

            _railViewport.anchorMin = new Vector2(0f, 1f);
            _railViewport.anchorMax = new Vector2(0f, 1f);
            _railViewport.pivot = new Vector2(0f, 1f);
            _railViewport.sizeDelta = new Vector2(width, viewportHeight);
            _railViewport.anchoredPosition = new Vector2(leftPad, 0f);

            _railContent.sizeDelta = new Vector2(width, contentHeight);
            _railContent.anchoredPosition = new Vector2(0f, _railContent.anchoredPosition.y);

            PlaceRailRow(_railContent.Find("CurrentMode") as RectTransform, 0f, 0f, width, rowHeight, icon, label);
            if (_future != null)
            {
                _future.anchorMin = new Vector2(0f, 1f);
                _future.anchorMax = new Vector2(0f, 1f);
                _future.pivot = new Vector2(0f, 1f);
                _future.sizeDelta = new Vector2(width, contentHeight);
                _future.anchoredPosition = Vector2.zero;
            }

            for (var i = 0; i < MapContent.FutureModes.Length; i++)
            {
                PlaceRailRow(
                    _future.Find("Mode" + i) as RectTransform,
                    0f,
                    -((i + 1) * (rowHeight + gap)),
                    width,
                    rowHeight,
                    icon,
                    label);
            }

            if (_railScroll == null)
                return;

            var scroll = contentHeight > viewportHeight + 1f;
            _railScroll.horizontal = false;
            _railScroll.vertical = scroll;
            if (!scroll)
                _railContent.anchoredPosition = Vector2.zero;
        }

        static void PlaceRailRow(RectTransform row, float x, float y, float width, float height, float icon, float label)
        {
            if (row == null)
                return;

            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(width, height);
            row.anchoredPosition = new Vector2(x, y);
            var iconRect = row.Find("Icon") as RectTransform;
            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0.5f, 1f);
                iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 1f);
                iconRect.sizeDelta = new Vector2(icon, icon);
                iconRect.anchoredPosition = new Vector2(0f, -4f);
            }

            var text = row.Find("Label") as RectTransform;
            if (text == null)
                return;

            text.anchorMin = new Vector2(0f, 0f);
            text.anchorMax = new Vector2(1f, 0f);
            text.pivot = new Vector2(0.5f, 0f);
            text.sizeDelta = new Vector2(-4f, label * 1.35f);
            text.anchoredPosition = new Vector2(0f, 1f);
        }

        static bool IsRailLabel(Text text)
        {
            var parent = text != null ? text.transform.parent : null;
            if (parent == null)
                return false;

            return parent.name == "CurrentMode" || (parent.name.Length == 5 && parent.name.StartsWith("Mode"));
        }

        static int RailLabelCanvas(MapCommandLayout.Metrics metrics, float scale)
        {
            return Mathf.Max(10, Mathf.RoundToInt(MapCommandLayout.RailLabelPixels(metrics.FontPixels) / Mathf.Max(0.01f, scale)));
        }

        void ShortenRail(MapCommandLayout.Metrics metrics, Rect safe, float scale)
        {
            var screenHeight = MapCommandLayout.RailRowPixels(metrics.FontPixels) + 8f;
            if (screenHeight >= metrics.Rail.height - 1f)
                return;

            var top = metrics.Rail.yMax;
            PlaceRect(
                _rail,
                new Rect(metrics.Rail.x, top - screenHeight, metrics.Rail.width, screenHeight),
                safe,
                scale);
        }

        RectTransform EdgeFill(string name)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(transform, false);
            var image = item.GetComponent<Image>();
            image.color = Surface;
            image.raycastTarget = false;
            return (RectTransform)item.transform;
        }

        static void PlaceBleed(RectTransform rect, Rect screen, Rect origin, float scale)
        {
            if (rect == null)
                return;

            rect.gameObject.SetActive(screen.width > 0.5f && screen.height > 0.5f);
            PlaceRect(rect, screen, origin, scale);
        }

        static void PlaceRect(RectTransform rect, Rect screen, Rect origin, float scale)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2((screen.x - origin.xMin) / scale, (screen.y - origin.yMin) / scale);
            rect.sizeDelta = new Vector2(screen.width / scale, screen.height / scale);
        }

        sealed class Binding
        {
            readonly LocalizedString _source;
            readonly Text _target;
            readonly System.Action<string> _extra;
            bool _subscribed;

            public Binding(string table, string key, Text target, System.Action<string> extra)
            {
                _source = new LocalizedString(table, key);
                _target = target;
                _extra = extra;
            }

            public void Subscribe()
            {
                if (_subscribed)
                    return;

                _source.StringChanged += OnChanged;
                _subscribed = true;
            }

            public void Unsubscribe()
            {
                if (!_subscribed)
                    return;

                _source.StringChanged -= OnChanged;
                _subscribed = false;
            }

            public void Dispose()
            {
                Unsubscribe();
                ((System.IDisposable)_source).Dispose();
            }

            void OnChanged(string value)
            {
                var text = value ?? string.Empty;
                if (_target != null)
                    _target.text = text;
                _extra?.Invoke(text);
            }
        }
    }
}
