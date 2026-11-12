using System.Collections.Generic;
using LogistiCore.Backend;
using LogistiCore.Localization;
using LogistiCore.UI;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// Runtime command shell for the world map. One header, one module rail,
    /// one city context panel, and one operational status strip. The map scene keeps
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
        Scrollbar _railBar;
        bool _railLaidOut;
        bool _regionLaidOut;
        float _railNormalized = 1f;
        bool _railPinned;
        bool _railDragging;
        RectTransform _context;
        RectTransform _summary;
        RectTransform _future;
        RectTransform _leftBleed;
        RectTransform _rightBleed;
        Button _close;
        Button _regionChip;
        RectTransform _regionMenu;
        CanvasGroup _menuGroup;
        RectTransform _regionContent;
        ScrollRect _regionScroll;
        Scrollbar _regionBar;
        bool _menuOpen;
        bool _menuAnimating;
        float _menuFrom;
        float _menuTime;
        Vector2 _menuRest;
        const float MenuMotionSeconds = 0.18f;
        readonly Button[] _regionRows = new Button[8];
        Text _brandLead;
        Text _brandTail;
        Text _economyText;
        Text _progressText;
        MapTopHud _hud;
        float _headerLeftPad;
        float _headerRightPad;
        float _headerTopPad;
        Text _regionLabel;
        int _region = 1;
        readonly string[] _regionNames = new string[8];
        string _regionSoon = string.Empty;
        readonly Button[] _moduleButtons = new Button[8];
        Text _section;
        Text _prompt;
        Text _cityName;
        Text _coordinateLabel;
        Text _coordinateValue;
        Text _summaryText;
        Text _summaryAction;
        Button _summaryButton;
        Image _summaryPip;
        MapOperationStatus _stripStatus;
        string _stripUnconfigured = string.Empty;
        string _stripLoading = string.Empty;
        string _stripError = string.Empty;
        string _stripIdle = string.Empty;
        string _stripSingle = string.Empty;
        string _stripMultiple = string.Empty;
        string _stripJobs = string.Empty;
        string _presentedStrip = string.Empty;
        bool _presentedJobs;
        float _copyLeft;
        float _copyRight;
        float _copyBottom;
        Image _contextImage;
        MapMarkerLayer _markers;
        MapCameraRig _rig;
        MapManagementPanel _management;
        MapSoonPanel _soon;
        int _soonRestoreIndex = -1;
        MapModule _soonRestoreModule;
        bool _soonRestoreExpanded;
        bool _soonRestoreOpen;
        bool _built;
        bool _listening;
        bool _applying;
        bool _suspendReframe;
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

        public Button HomeButton => null;
        public Button RegionButton => _regionChip;
        public bool RegionUiIntact => RegionRowsIntact();
        public int AliveRegionRowCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _regionRows.Length; i++)
                {
                    if (_regionRows[i] != null)
                        count++;
                }

                return count;
            }
        }

        public void SimulateStaleRegionRows()
        {
            TearDownRegion();
            _built = true;
        }

        public int LocalizationBindingCount => _bindings.Count;

        public void PresentCurrentStripForTests()
        {
            PresentStrip(MapOperationStrip.Current);
        }
        public bool RegionMenuOpen => _menuOpen;
        public int RegionIndex => _region;
        public Button CloseButton => _close;
        public PanelState State => _state;
        public Rect Viewport => _metrics.Viewport;
        public Rect HeaderRect => _metrics.Header;
        public Rect RailRect => _metrics.Rail;
        public Rect ContextRect => _metrics.Context;
        public Rect SummaryRect => _metrics.Summary;
        public bool FutureModesVisible =>
            _moduleButtons.Length > 1 && _moduleButtons[1] != null && _moduleButtons[1].gameObject.activeSelf;
        public bool ContextOpen => _state != PanelState.Closed && _metrics.ContextOpen;
        public bool CoordinatesVisible => _state == PanelState.City && _coordinateValue != null && _coordinateValue.gameObject.activeSelf;
        public string SummaryText => _summaryText != null ? _summaryText.text : string.Empty;
        public string CoordinateText => _state == PanelState.City && _coordinateValue != null ? _coordinateValue.text : string.Empty;
        public string CityText => _state == PanelState.City && _cityName != null && _cityName.enabled ? _cityName.text : string.Empty;
        public string PromptText => _state != PanelState.Closed && _prompt != null && _prompt.enabled ? _prompt.text : string.Empty;
        public string ModuleBody => ModuleCopy();
        public int InteractiveButtonCount => GetComponentsInChildren<Button>(true).Length;
        public MapManagementPanel Management => _management;
        public MapSoonPanel Soon => _soon;
        public bool SoonOpen => _soon != null && _soon.IsOpen;
        public ScrollRect RailScroll => _railScroll;
        public ScrollRect RegionScroll => _regionScroll;
        public bool ManagementOpen => _management != null && _management.IsOpen;
        public bool ManagementExpanded => _management != null && _management.Expanded;
        public MapModule ActiveModule => _management != null && _management.IsOpen ? _management.Module : MapModule.None;
        public int ModuleIndex => _moduleIndex;
        public string ShownCity => _shownCity;
        public MapOperationStatus StripStatus => _stripStatus;
        public bool RegionChipOverlapsBrand()
        {
            if (_brandLead == null || _brandTail == null || _regionChip == null)
                return false;
            var brand = WorldAabb(_brandLead.rectTransform);
            brand.xMax = Mathf.Max(brand.xMax, WorldAabb(_brandTail.rectTransform).xMax);
            var chip = WorldAabb((RectTransform)_regionChip.transform);
            return chip.xMin < brand.xMax - 0.5f && chip.xMax > brand.xMin && chip.yMax > brand.yMin && chip.yMin < brand.yMax;
        }

        public bool StripActionEnabled => _summaryButton != null && _summaryButton.interactable;
        public string StripActionText =>
            _summaryAction != null && _summaryAction.enabled ? _summaryAction.text : string.Empty;

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
            BuildIfNeeded();
            if (!Application.isPlaying)
                return;

            BindScene();
            Subscribe();
            HookButtons();
            Apply(Screen.width, Screen.height, Screen.safeArea);
        }

        void OnDisable()
        {
            if (_markers != null)
                _markers.SelectionChanged -= OnSelectionChanged;

            if (_listening)
            {
                if (_close != null)
                    _close.onClick.RemoveListener(CloseContext);
                if (_summaryButton != null)
                    _summaryButton.onClick.RemoveListener(OpenJobsFromStrip);
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
            if (_railPinned)
            {
                Canvas.willRenderCanvases -= PinRailOnCanvas;
                _railPinned = false;
            }

            ReleaseBindings();
        }

        void Update()
        {
            if (!_menuAnimating || !Application.isPlaying || _regionMenu == null)
                return;

            _menuTime += Time.unscaledDeltaTime;
            ApplyMenuMotion(MenuAmount());
            if (_menuTime < MenuMotionSeconds)
                return;

            _menuAnimating = false;
            ApplyMenuMotion(_menuOpen ? 1f : 0f);
            if (!_menuOpen)
                _regionMenu.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            var width = Screen.width;
            var height = Screen.height;
            var safe = Screen.safeArea;
            if (width != _width || height != _height || safe != _safe)
            {
                Apply(width, height, safe);
                return;
            }

            PresentStrip(MapOperationStrip.Current);
        }

        public void OpenHome()
        {
            AppNavigation.ShowHome();
        }

        public void ShowMapContext()
        {
            HideManagement();
            _state = PanelState.Module;
            _moduleIndex = -1;
            Relayout();
        }

        public void ShowModule(int index)
        {
            if (index < 0 || index >= _moduleButtons.Length)
                return;

            if (MapModuleIds.TryFromRail(index, out var module))
            {
                HideSoon();
                _soonRestoreOpen = false;
                OpenManagement(module);
                return;
            }

            if (MapModuleIds.IsTransport(index))
                return;

            CloseSoon();
            HideManagement();
            _state = PanelState.Module;
            _moduleIndex = index;
            Relayout();
        }

        public void OpenManagement(MapModule module)
        {
            if (module == MapModule.None)
            {
                CloseContext();
                return;
            }

            EnsureManagement();
            _state = PanelState.Module;
            _moduleIndex = MapModuleIds.RailIndex(module);
            _management.Show(module);
            WithoutReframe(Relayout);
        }

        public void OpenJobsFromStrip()
        {
            if (!MapOperationStrip.Current.CanOpenJobs)
                return;

            OpenManagement(MapModule.Jobs);
        }

        public void RefreshStrip()
        {
            PresentStrip(MapOperationStrip.Current);
        }

        public bool TryFocusFleetLocation()
        {
            return _management != null && _management.Fleet != null && _management.Fleet.TryFocusSelected();
        }

        public bool TryFocusJobOrigin()
        {
            return _management != null && _management.Jobs != null && _management.Jobs.TryFocusOrigin();
        }

        public bool TryFocusJobDestination()
        {
            return _management != null && _management.Jobs != null && _management.Jobs.TryFocusDestination();
        }

        public bool TryFocusWarehouseCity()
        {
            return _management != null && _management.Warehouses != null && _management.Warehouses.TryFocusCity();
        }

        public bool TryFocusWarehouseGps()
        {
            return _management != null && _management.Warehouses != null && _management.Warehouses.TryFocusGps();
        }

        public bool TryFocusMarketCity()
        {
            return _management != null && _management.Market != null && _management.Market.TryFocusCity();
        }

        public bool TryFocusRoadOrigin()
        {
            return _management != null && _management.Road != null && _management.Road.TryFocusOrigin();
        }

        public bool TryFocusRoadDestination()
        {
            return _management != null && _management.Road != null && _management.Road.TryFocusDestination();
        }

        public void SetManagementExpanded(bool expanded)
        {
            if (!ManagementOpen)
                return;

            _management.SetExpanded(expanded);
            PlaceManagement();
            PaintRows();
        }

        public void CloseContext()
        {
            HideSoon();
            HideManagement();
            _state = PanelState.Closed;
            _moduleIndex = -1;
            _soonRestoreOpen = false;
            WithoutReframe(Relayout);
        }

        public void OpenSoon(int railIndex)
        {
            _ = railIndex;
        }

        public void CloseSoon()
        {
            if (!SoonOpen)
                return;

            HideSoon();
            if (_soonRestoreOpen && _soonRestoreModule != MapModule.None)
            {
                OpenManagement(_soonRestoreModule);
                SetManagementExpanded(_soonRestoreExpanded);
            }
            else
            {
                _moduleIndex = _soonRestoreIndex;
                PaintRows();
                Relayout();
            }

            _soonRestoreOpen = false;
        }

        public void Apply(float screenWidth, float screenHeight, Rect safeArea)
        {
            if (this == null || _applying)
                return;

            _applying = true;
            try
            {
                BuildIfNeeded();
                HookButtons();
                _metrics = MapCommandLayout.Evaluate(screenWidth, screenHeight, safeArea, WantsContextPanel());
                if (!_metrics.ContextOpen && !ManagementOpen && !SoonOpen && _state != PanelState.Closed)
                    _state = PanelState.Closed;

                _width = screenWidth < 1f ? 0 : Mathf.RoundToInt(screenWidth);
                _height = screenHeight < 1f ? 0 : Mathf.RoundToInt(screenHeight);
                _safe = safeArea;
                ShowRailRows(true);
                Place(_metrics, screenWidth, screenHeight, safeArea);
                transform.SetAsLastSibling();
                Present();
                PlaceManagement();
                PlaceSoon();
                PinRailScroll();
                if (Application.isPlaying && _rig != null && !_suspendReframe)
                    _rig.ReframeForChrome();
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
            if (ManagementOpen)
            {
                Present();
                return;
            }

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
            PresentStrip(MapOperationStrip.Current);
        }

        string ModuleName(int index)
        {
            if (index < 0 || index >= _moduleNames.Length || string.IsNullOrEmpty(_moduleNames[index]))
                return string.Empty;

            return _moduleNames[index];
        }

        void PresentStrip(MapOperationSnapshot snapshot)
        {
            if (this == null)
                return;

            _stripStatus = snapshot.Status;
            var jobs = snapshot.CanOpenJobs;
            var copy = StripCopy(snapshot);
            if (_summaryText == null)
                return;
            if (copy == _presentedStrip && jobs == _presentedJobs)
            {
                if (_summaryButton != null)
                    _summaryButton.interactable = jobs;
                return;
            }

            _presentedStrip = copy;
            _presentedJobs = jobs;
            if (_summaryText != null)
                _summaryText.text = copy;
            if (_summaryButton != null)
                _summaryButton.interactable = jobs;
            if (_summaryAction != null)
            {
                _summaryAction.enabled = jobs;
                if (jobs)
                    _summaryAction.text = string.IsNullOrEmpty(_stripJobs) ? string.Empty : _stripJobs;
            }

            if (_summaryPip != null)
                _summaryPip.color = StripPip(snapshot.Status);

            LayoutStripCopy();
        }

        void LayoutStripCopy()
        {
            var actionPad = _presentedJobs ? 88f : 16f;
            InsetText(_summaryText, _copyLeft + 28f, _copyBottom, 0f, _copyRight + actionPad);
            if (_summaryAction != null)
                InsetText(_summaryAction, 0f, _copyBottom, 0f, _copyRight + 12f);
        }

        string StripCopy(MapOperationSnapshot snapshot)
        {
            switch (snapshot.Status)
            {
                case MapOperationStatus.Loading:
                    return FirstNonEmpty(_stripLoading, OpsContent.Table, OpsContent.StripLoading);
                case MapOperationStatus.Error:
                    return FirstNonEmpty(_stripError, OpsContent.Table, OpsContent.StripError);
                case MapOperationStatus.Idle:
                    return FirstNonEmpty(_stripIdle, OpsContent.Table, OpsContent.StripIdle);
                case MapOperationStatus.SingleActive:
                    if (!string.IsNullOrEmpty(snapshot.Origin) && !string.IsNullOrEmpty(snapshot.Destination))
                    {
                        var route = snapshot.Origin + " → " + snapshot.Destination;
                        if (!string.IsNullOrEmpty(snapshot.ExpectedArrival))
                            return route + "  " + snapshot.ExpectedArrival;
                        return route;
                    }

                    return FirstNonEmpty(_stripSingle, OpsContent.Table, OpsContent.StripSingle);
                case MapOperationStatus.MultipleActive:
                    return FirstNonEmpty(_stripMultiple, OpsContent.Table, OpsContent.StripMultiple);
                default:
                    return FirstNonEmpty(_stripUnconfigured, MapContent.Table, MapContent.SummaryUnconnected);
            }
        }

        static string FirstNonEmpty(string preferred, string table, string key)
        {
            if (!string.IsNullOrEmpty(preferred))
                return preferred;

            return LoadedLine(table, key);
        }

        static string LoadedLine(string table, string key)
        {
            if (!LocaleService.IsReady)
                return string.Empty;

            var database = LocalizationSettings.StringDatabase;
            var locale = LocalizationSettings.SelectedLocale;
            if (database == null || locale == null)
                return string.Empty;

            var handle = database.GetTableAsync(table, locale);
            if (!handle.IsDone)
                return string.Empty;

            var collection = handle.Result as StringTable;
            var entry = collection != null ? collection.GetEntry(key) : null;
            if (entry == null)
                return string.Empty;

            var text = entry.GetLocalizedString();
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
        }

        static Color StripPip(MapOperationStatus status)
        {
            switch (status)
            {
                case MapOperationStatus.Loading:
                    return MapHudPalette.AccentQuiet;
                case MapOperationStatus.Error:
                    return new Color(0.72f, 0.46f, 0.42f, 0.95f);
                case MapOperationStatus.Idle:
                    return MapHudPalette.TextMuted;
                case MapOperationStatus.SingleActive:
                case MapOperationStatus.MultipleActive:
                    return MapHudPalette.Accent;
                default:
                    return MapHudPalette.AccentQuiet;
            }
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
            PlaceRect(_context, metrics.Context, safe, scale);
            PlaceRect(_summary, metrics.Summary, safe, scale);
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
                if (text == _brandLead || text == _brandTail)
                    SetFont(text, metrics.CanvasTitle);
                else if (_hud != null && _hud.Owns(text))
                    continue;
                else if (IsRailStatus(text))
                    SetFont(text, RailSoonCanvas(metrics, scale, text.rectTransform.rect.width));
                else if (IsRailLabel(text))
                    SetFont(text, RailLabelCanvas(metrics, scale));
                else if (IsRegionMenuText(text))
                    SetFont(text, MapCommandLayout.MenuCanvasSize(screenWidth, screenHeight, scale));
                else
                    SetFont(text, metrics.CanvasFont);
            }

            var brandEnd = PlaceBrand(leftPad, topPad, metrics.CanvasTitle);
            PlaceRegion(metrics, brandEnd, topPad, scale, safe);
            PlaceHud();
        }

        void BuildIfNeeded()
        {
            if (this == null)
                return;

            if (_built && ShellChromeIntact())
            {
                EnsureRegionUi();
                return;
            }

            if (_built && !ShellChromeIntact())
                _built = false;

            if (_built)
                return;

            _built = true;
            ReleaseBindings();
            _rect = (RectTransform)transform;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _leftBleed = EdgeFill("LeftBleed");
            _rightBleed = EdgeFill("RightBleed");
            _header = Panel("CommandHeader", out _);
            EnsureManagement();
            _rail = Panel("ModuleRail", out var railImage);
            railImage.raycastTarget = false;
            _context = Panel("ContextPanel", out _contextImage);
            _summary = Panel("SummaryBar", out _);
            _brandLead = Line(_header, "Brand", font, FontStyle.Bold, MapHudPalette.Text);
            _brandTail = Line(_header, "BrandCore", font, FontStyle.Bold, MapHudPalette.Accent);
            _brandLead.text = "Logisti";
            _brandTail.text = "Core";
            _brandLead.horizontalOverflow = HorizontalWrapMode.Overflow;
            _brandTail.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hud = MapTopHud.Build(_header, font);
            _economyText = _hud.BalanceText;
            _progressText = _hud.ProgressText;
            PresentEconomy();
            BuildRegion(font);
            _contextImage.color = Surface;
            Stroke(_header);
            Stroke(_rail);
            Stroke(_context);
            Stroke(_summary);
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
            _summaryAction = Line(_summary, "Action", font, FontStyle.Bold, MapHudPalette.Accent);
            _summaryAction.alignment = TextAnchor.MiddleRight;
            _summaryAction.enabled = false;
            InsetText(_summaryAction, 0f, 0f, 0f, 16f);
            Motif(_summary, "Pip", MapHudPalette.AccentQuiet, 6f);
            var pip = _summary.Find("Pip");
            _summaryPip = pip != null ? pip.GetComponent<Image>() : null;
            _summaryButton = _summary.GetComponent<Button>() ?? _summary.gameObject.AddComponent<Button>();
            _summaryButton.targetGraphic = _summary.GetComponent<Image>();
            _summaryButton.interactable = false;
            var stripColors = _summaryButton.colors;
            stripColors.normalColor = Color.white;
            stripColors.highlightedColor = Color.white;
            stripColors.pressedColor = Color.white;
            stripColors.selectedColor = Color.white;
            stripColors.disabledColor = Color.white;
            stripColors.fadeDuration = 0f;
            _summaryButton.colors = stripColors;
            BuildRailScroll();
            _future = FutureRows(_railContent, font);
            TopBand(_section, 16f, 28f, 0f);
            TopBand(_prompt, 52f, 64f, 0f);
            TopBand(_cityName, 52f, 40f, 0f);
            TopBand(_coordinateLabel, 100f, 24f, 0f);
            TopBand(_coordinateValue, 126f, 32f, 0f);
            Quiet(_close);
            Stroke((RectTransform)_close.transform);
            for (var i = 0; i < MapContent.RegionViews.Length; i++)
            {
                var slot = i;
                TrackQuiet(MapContent.Table, MapContent.RegionViews[i], value =>
                {
                    _regionNames[slot] = value;
                    PaintRegion();
                });
            }

            TrackQuiet(MapContent.Table, MapContent.ModeSoon, value =>
            {
                _regionSoon = value;
                PaintRegion();
                PaintRows();
            });
            Subscribe();
            Track(MapContent.Table, MapContent.Title, null, value =>
            {
                _mapTitle = value;
                RefreshCopy();
            });
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
            TrackQuiet(MapContent.Table, MapContent.SummaryUnconnected, value =>
            {
                _stripUnconfigured = value;
                PresentStrip(MapOperationStrip.Current);
            });
            TrackQuiet(OpsContent.Table, OpsContent.StripLoading, value =>
            {
                _stripLoading = value;
                PresentStrip(MapOperationStrip.Current);
            });
            TrackQuiet(OpsContent.Table, OpsContent.StripError, value =>
            {
                _stripError = value;
                PresentStrip(MapOperationStrip.Current);
            });
            TrackQuiet(OpsContent.Table, OpsContent.StripIdle, value =>
            {
                _stripIdle = value;
                PresentStrip(MapOperationStrip.Current);
            });
            TrackQuiet(OpsContent.Table, OpsContent.StripSingle, value =>
            {
                _stripSingle = value;
                PresentStrip(MapOperationStrip.Current);
            });
            TrackQuiet(OpsContent.Table, OpsContent.StripMultiple, value =>
            {
                _stripMultiple = value;
                PresentStrip(MapOperationStrip.Current);
            });
            TrackQuiet(OpsContent.Table, OpsContent.TitleJobs, value =>
            {
                _stripJobs = value;
                PresentStrip(MapOperationStrip.Current);
            });
            TrackQuiet(OpsContent.Table, OpsContent.HudLevel, _ => PresentEconomy());
            TrackQuiet(OpsContent.Table, OpsContent.HudXp, _ => PresentEconomy());
            TrackQuiet(OpsContent.Table, OpsContent.HudLoading, _ => PresentEconomy());
            TrackQuiet(OpsContent.Table, OpsContent.HudUnavailable, _ => PresentEconomy());
            TrackQuiet(OpsContent.Table, OpsContent.HudReputation, _ => PresentEconomy());
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
            if (_listening)
                return;

            if (_close != null)
                _close.onClick.AddListener(CloseContext);
            if (_summaryButton != null)
                _summaryButton.onClick.AddListener(OpenJobsFromStrip);
            for (var i = 0; i < _moduleButtons.Length; i++)
            {
                var slot = i;
                if (_moduleButtons[i] != null)
                    WireRailTap(_moduleButtons[i], slot);
            }

            _listening = true;
        }

        void Subscribe()
        {
            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Subscribe();

            LocalizationSettings.SelectedLocaleChanged -= OnLocale;
            LocalizationSettings.SelectedLocaleChanged += OnLocale;
            MapCompanyEconomyRead.Applied -= PresentEconomy;
            MapCompanyEconomyRead.Applied += PresentEconomy;
        }

        void Unsubscribe()
        {
            MapCompanyEconomyRead.Applied -= PresentEconomy;
            LocalizationSettings.SelectedLocaleChanged -= OnLocale;
            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Unsubscribe();
        }

        void PresentEconomy()
        {
            if (_hud == null)
                return;

            _hud.BindCopy(
                LoadedLine(OpsContent.Table, OpsContent.HudLevel),
                LoadedLine(OpsContent.Table, OpsContent.HudXp),
                LoadedLine(OpsContent.Table, OpsContent.HudLoading),
                LoadedLine(OpsContent.Table, OpsContent.HudUnavailable),
                LoadedLine(OpsContent.Table, OpsContent.HudReputation));
            _hud.Present();
            PresentStrip(MapOperationStrip.Current);
        }

        void PlaceHud()
        {
            if (_hud == null)
                return;

            var scale = MapHudLayout.CanvasScale(_width, _height);
            var canvas = _header != null ? _header.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            _hud.Layout(
                _header,
                _regionChip != null ? (RectTransform)_regionChip.transform : null,
                _headerLeftPad,
                _headerRightPad,
                _headerTopPad,
                _metrics,
                scale,
                MapTopHud.MeasureTrailing(_header, _metrics, scale, canvas));
        }

        void OnLocale(Locale locale)
        {
            if (this == null)
                return;

            PresentEconomy();
            PlaceHud();
            ShowCity(_shownCity);
        }

        void Track(string table, string key, Text target, System.Action<string> extra = null)
        {
            var binding = new Binding(this, table, key, target, value =>
            {
                extra?.Invoke(value);
                if (target == _prompt || extra != null)
                    ShowCity(_shownCity);
            });
            _bindings.Add(binding);
        }

        void TrackQuiet(string table, string key, System.Action<string> extra)
        {
            _bindings.Add(new Binding(this, table, key, null, extra));
        }

        void ReleaseBindings()
        {
            Unsubscribe();
            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Dispose();
            _bindings.Clear();
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
            image.color = new Color(1f, 1f, 1f, 0f);
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
            var status = Line(row.transform, "Status", font, FontStyle.Normal, MapHudPalette.TextMuted);
            status.alignment = TextAnchor.MiddleCenter;
            status.horizontalOverflow = HorizontalWrapMode.Overflow;
            status.verticalOverflow = VerticalWrapMode.Overflow;
            status.gameObject.SetActive(false);
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
            for (var i = 0; i < _moduleButtons.Length; i++)
            {
                var status = _moduleButtons[i] != null ? FindText(_moduleButtons[i].transform, "Status") : null;
                if (status != null && MapModuleIds.IsTransport(i) && !string.IsNullOrEmpty(_regionSoon))
                    status.text = _regionSoon;
                Paint(_moduleButtons[i], _state == PanelState.Module && _moduleIndex == i, i);
            }
        }

        bool WantsContextPanel()
        {
            if (ManagementOpen || SoonOpen)
                return false;

            if (_state == PanelState.City)
                return true;

            if (MapModuleIds.IsTransport(_moduleIndex))
                return false;

            return _state == PanelState.Module && !MapModuleIds.TryFromRail(_moduleIndex, out _);
        }

        void EnsureManagement()
        {
            if (_management != null)
                return;

            _management = MapManagementPanel.Attach(this);
        }

        void HideManagement()
        {
            if (_management != null)
                _management.Hide();
        }

        void EnsureSoon()
        {
        }

        void HideSoon()
        {
            if (_soon != null)
                _soon.Hide();
        }

        void PlaceSoon()
        {
            if (_soon == null)
                return;

            _soon.Place(_metrics, _width > 0 ? _width : Screen.width, _height > 0 ? _height : Screen.height, _safe);
            if (_soon.transform != null)
                _soon.transform.SetAsLastSibling();
        }

        void PlaceManagement()
        {
            EnsureManagement();
            if (_management == null)
                return;

            _management.Place(_metrics, _width > 0 ? _width : Screen.width, _height > 0 ? _height : Screen.height, _safe);
            if (_management.transform != null)
                _management.transform.SetAsLastSibling();
        }

        void WithoutReframe(System.Action action)
        {
            _suspendReframe = true;
            try
            {
                action();
            }
            finally
            {
                _suspendReframe = false;
            }
        }

        string ModuleCopy()
        {
            if (_state != PanelState.Module)
                return string.Empty;

            if (ManagementOpen)
                return _management.BodyText;

            return _moduleIndex < 0 ? _selectPrompt : _unavailable;
        }

        static void Paint(Button button, bool selected, int slot)
        {
            if (button == null)
                return;

            var soon = MapModuleIds.IsTransport(slot);
            button.interactable = !soon;
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected && !soon ? SurfaceRaised : new Color(1f, 1f, 1f, 0f);
            var label = FindText(button.transform, "Label");
            var status = FindText(button.transform, "Status");
            var icon = button.transform.Find("Icon");
            var iconImage = icon != null ? icon.GetComponent<Image>() : null;
            var muted = new Color(MapHudPalette.TextMuted.r, MapHudPalette.TextMuted.g, MapHudPalette.TextMuted.b, 0.96f);
            var soonInk = new Color(MapHudPalette.Text.r, MapHudPalette.Text.g, MapHudPalette.Text.b, 0.92f);
            if (iconImage != null)
                iconImage.color = soon ? muted : selected ? MapHudPalette.Accent : muted;
            if (status != null)
            {
                status.gameObject.SetActive(soon);
                status.color = soonInk;
                status.fontStyle = FontStyle.Bold;
            }

            if (label == null)
                return;

            label.color = soon ? muted : selected ? MapHudPalette.Accent : muted;
            label.fontStyle = FontStyle.Bold;
        }

        void PlaceCopy(MapCommandLayout.Metrics metrics, float leftPad, float rightPad, float topPad, float bottomPad, float scale)
        {
            _copyLeft = leftPad;
            _copyRight = rightPad;
            _copyBottom = bottomPad;
            _headerLeftPad = leftPad;
            _headerRightPad = rightPad;
            _headerTopPad = topPad;
            PlaceMark(_summary, "Pip", leftPad + 14f, 6f, bottomPad * 0.5f);
            LayoutStripCopy();

            PlaceRail(metrics, leftPad, scale);

            var closeSide = Mathf.Max(metrics.CanvasFont * 1.6f, MapCommandLayout.MinTouchPixels / Mathf.Max(0.01f, scale));
            TopBand(_section, 18f, Mathf.Max(24f, metrics.CanvasFont * 1.3f), rightPad + closeSide);
            TopBand(_prompt, 18f + metrics.CanvasFont * 1.6f, metrics.CanvasFont * 2.4f, rightPad + closeSide);
            TopBand(_cityName, 18f + metrics.CanvasFont * 1.6f, metrics.CanvasFont * 1.8f, rightPad + closeSide);
            var coordinateTop = 18f + metrics.CanvasFont * 3.8f;
            TopBand(_coordinateLabel, coordinateTop, metrics.CanvasFont * 1.3f, rightPad + closeSide);
            TopBand(_coordinateValue, coordinateTop + metrics.CanvasFont * 1.4f, metrics.CanvasFont * 1.5f, rightPad + closeSide);
        }

        float PlaceBrand(float leftPad, float topPad, int titleSize)
        {
            if (_brandLead == null || _brandTail == null)
            {
                return leftPad + 12f + Mathf.Max(48f, "Logisti".Length * titleSize * 0.56f)
                    + Mathf.Max(28f, "Core".Length * titleSize * 0.56f)
                    + MapCommandLayout.BrandGlyphPadCanvas(titleSize);
            }

            SetFont(_brandLead, titleSize);
            SetFont(_brandTail, titleSize);
            var leadWidth = Mathf.Max(1f, _brandLead.preferredWidth);
            var tailWidth = Mathf.Max(1f, _brandTail.preferredWidth);
            var pad = MapCommandLayout.BrandGlyphPadCanvas(titleSize);
            var x = leftPad + 12f;
            AnchorLeft(_brandLead, x, leadWidth, 0f, topPad);
            AnchorLeft(_brandTail, x + leadWidth, tailWidth + pad, 0f, topPad);
            return x + leadWidth + tailWidth + pad;
        }

        float BrandEndCanvas(float leftPad, int titleSize)
        {
            if (_brandLead == null || _brandTail == null)
            {
                return leftPad + 12f + Mathf.Max(48f, "Logisti".Length * titleSize * 0.56f)
                    + Mathf.Max(28f, "Core".Length * titleSize * 0.56f)
                    + MapCommandLayout.BrandGlyphPadCanvas(titleSize);
            }

            var leadWidth = Mathf.Max(1f, _brandLead.preferredWidth);
            var tailWidth = Mathf.Max(1f, _brandTail.preferredWidth);
            return leftPad + 12f + leadWidth + tailWidth + MapCommandLayout.BrandGlyphPadCanvas(titleSize);
        }

        float BrandChipGap(int titleSize)
        {
            return MapCommandLayout.BrandChipGapCanvas(titleSize);
        }

        void ClipRegionName()
        {
            if (_regionLabel == null)
                return;

            var budget = _regionLabel.rectTransform.rect.width;
            var needed = MapCommandLayout.TextWidth(_regionLabel.text, _regionLabel.fontSize);
            if (needed <= budget + 1f)
                return;
            _regionLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _regionLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _regionLabel.text = MapManagementLayout.ClipLine(_regionLabel.text, _regionLabel.fontSize, budget);
        }

        float MeasuredBrandEndCanvas()
        {
            if (_header == null || _brandTail == null)
                return 0f;

            Canvas.ForceUpdateCanvases();
            var aabb = WorldAabb(_brandTail.rectTransform);
            return _header.InverseTransformPoint(new Vector3(aabb.xMax, aabb.center.y, 0f)).x;
        }

        static Rect WorldAabb(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var minX = corners[0].x;
            var minY = corners[0].y;
            var maxX = corners[0].x;
            var maxY = corners[0].y;
            for (var i = 1; i < 4; i++)
            {
                if (corners[i].x < minX)
                    minX = corners[i].x;
                if (corners[i].y < minY)
                    minY = corners[i].y;
                if (corners[i].x > maxX)
                    maxX = corners[i].x;
                if (corners[i].y > maxY)
                    maxY = corners[i].y;
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
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

        static void AnchorRight(Text text, float width, float topInset, float rightInset)
        {
            if (text == null)
                return;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(Mathf.Max(48f, width), Mathf.Max(18f, text.fontSize + 4f));
            rect.anchoredPosition = new Vector2(-Mathf.Max(8f, rightInset), -Mathf.Max(2f, topInset));
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

        void BuildRegion(Font font)
        {
            var chip = new GameObject("RegionChip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            chip.hideFlags = HideFlags.DontSave;
            chip.transform.SetParent(_header, false);
            var chipImage = chip.GetComponent<Image>();
            chipImage.color = new Color(1f, 1f, 1f, 0f);
            chipImage.raycastTarget = true;
            _regionChip = chip.GetComponent<Button>();
            _regionChip.targetGraphic = chipImage;
            Quiet(_regionChip);
            _regionChip.onClick.AddListener(ToggleRegionMenu);
            chip.GetComponent<Outline>().enabled = false;
            var face = new GameObject("Face", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            face.hideFlags = HideFlags.DontSave;
            face.transform.SetParent(chip.transform, false);
            var faceImage = face.GetComponent<Image>();
            faceImage.sprite = MapRegionGlyphs.Plate;
            faceImage.type = Image.Type.Sliced;
            faceImage.color = MapHudPalette.Button;
            faceImage.raycastTarget = false;
            var faceOutline = face.GetComponent<Outline>();
            faceOutline.effectColor = MapHudPalette.Accent;
            faceOutline.effectDistance = new Vector2(1f, -1f);
            faceOutline.useGraphicAlpha = true;
            var faceRect = (RectTransform)face.transform;
            faceRect.anchorMin = new Vector2(0.5f, 0.5f);
            faceRect.anchorMax = new Vector2(0.5f, 0.5f);
            faceRect.pivot = new Vector2(0.5f, 0.5f);
            var glyph = Glyph(chip.transform, "Glyph", MapRegionGlyphs.Globe, 16f);
            glyph.anchorMin = new Vector2(0f, 0.5f);
            glyph.anchorMax = new Vector2(0f, 0.5f);
            glyph.pivot = new Vector2(0f, 0.5f);
            glyph.anchoredPosition = new Vector2(8f, 0f);
            _regionLabel = Line(chip.transform, "Name", font, FontStyle.Bold, MapHudPalette.Text);
            _regionLabel.alignment = TextAnchor.MiddleLeft;
            _regionLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            var chevron = Glyph(chip.transform, "Chevron", MapRegionGlyphs.Chevron, 12f);
            chevron.anchorMin = new Vector2(1f, 0.5f);
            chevron.anchorMax = new Vector2(1f, 0.5f);
            chevron.pivot = new Vector2(1f, 0.5f);
            chevron.anchoredPosition = new Vector2(-8f, 0f);

            var menu = new GameObject("RegionMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            menu.hideFlags = HideFlags.DontSave;
            menu.transform.SetParent(transform, false);
            var menuImage = menu.GetComponent<Image>();
            menuImage.sprite = MapRegionGlyphs.Plate;
            menuImage.type = Image.Type.Sliced;
            var panel = MapHudPalette.Panel;
            menuImage.color = new Color(panel.r, panel.g, panel.b, 1f);
            menuImage.raycastTarget = true;
            var menuOutline = menu.GetComponent<Outline>();
            menuOutline.effectColor = MapHudPalette.Accent;
            menuOutline.effectDistance = new Vector2(1f, -1f);
            menuOutline.useGraphicAlpha = true;
            _regionMenu = (RectTransform)menu.transform;
            _menuGroup = menu.AddComponent<CanvasGroup>();
            _menuGroup.alpha = 0f;
            _menuGroup.blocksRaycasts = false;
            _menuGroup.interactable = false;
            var viewport = new GameObject("RegionViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewport.hideFlags = HideFlags.DontSave;
            viewport.transform.SetParent(_regionMenu, false);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(4f, 4f);
            viewportRect.offsetMax = new Vector2(-4f, -4f);
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;
            var content = new GameObject("RegionContent", typeof(RectTransform));
            content.hideFlags = HideFlags.DontSave;
            content.transform.SetParent(viewportRect, false);
            _regionContent = (RectTransform)content.transform;
            _regionContent.anchorMin = new Vector2(0f, 1f);
            _regionContent.anchorMax = new Vector2(1f, 1f);
            _regionContent.pivot = new Vector2(0.5f, 1f);
            _regionScroll = menu.AddComponent<ScrollRect>();
            _regionScroll.viewport = viewportRect;
            _regionScroll.content = _regionContent;
            _regionScroll.horizontal = false;
            _regionScroll.vertical = false;
            _regionScroll.movementType = ScrollRect.MovementType.Clamped;
            _regionScroll.inertia = false;
            _regionScroll.scrollSensitivity = 12f;
            var bar = new GameObject("RegionScroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            bar.hideFlags = HideFlags.DontSave;
            bar.transform.SetParent(_regionMenu, false);
            var barImage = bar.GetComponent<Image>();
            barImage.color = new Color(MapHudPalette.Accent.r, MapHudPalette.Accent.g, MapHudPalette.Accent.b, 0.18f);
            barImage.raycastTarget = false;
            _regionBar = bar.GetComponent<Scrollbar>();
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.offsetMin = new Vector2(-8f, 6f);
            barRect.offsetMax = new Vector2(-3f, -6f);
            var thumb = new GameObject("Thumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            thumb.hideFlags = HideFlags.DontSave;
            thumb.transform.SetParent(bar.transform, false);
            var thumbImage = thumb.GetComponent<Image>();
            thumbImage.color = MapHudPalette.Accent;
            thumbImage.raycastTarget = false;
            var thumbRect = (RectTransform)thumb.transform;
            thumbRect.anchorMin = new Vector2(0f, 0f);
            thumbRect.anchorMax = new Vector2(1f, 1f);
            thumbRect.offsetMin = new Vector2(1f, 1f);
            thumbRect.offsetMax = new Vector2(-1f, -1f);
            _regionBar.handleRect = thumbRect;
            _regionBar.targetGraphic = thumbImage;
            _regionBar.direction = Scrollbar.Direction.BottomToTop;
            _regionScroll.verticalScrollbar = _regionBar;
            _regionScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            bar.SetActive(false);
            for (var i = 0; i < _regionRows.Length; i++)
            {
                var slot = i;
                var row = new GameObject("Region" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                row.hideFlags = HideFlags.DontSave;
                row.transform.SetParent(_regionContent, false);
                var rowImage = row.GetComponent<Image>();
                rowImage.color = new Color(1f, 1f, 1f, 0f);
                rowImage.raycastTarget = true;
                var button = row.GetComponent<Button>();
                button.targetGraphic = rowImage;
                Quiet(button);
                button.interactable = true;
                var trigger = row.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                entry.callback.AddListener(data =>
                {
                    var pointer = data as PointerEventData;
                    if (pointer == null || pointer.button != PointerEventData.InputButton.Left || pointer.dragging)
                        return;
                    SelectRegion(slot);
                });
                trigger.triggers.Add(entry);
                var name = Line(row.transform, "Name", font, FontStyle.Bold, MapHudPalette.Text);
                name.alignment = TextAnchor.MiddleLeft;
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                var nameRect = name.rectTransform;
                nameRect.anchorMin = Vector2.zero;
                nameRect.anchorMax = Vector2.one;
                nameRect.offsetMin = new Vector2(12f, 0f);
                nameRect.offsetMax = new Vector2(-12f, 0f);
                var soon = Line(row.transform, "Soon", font, FontStyle.Normal, MapHudPalette.TextMuted);
                soon.alignment = TextAnchor.MiddleRight;
                soon.horizontalOverflow = HorizontalWrapMode.Overflow;
                var soonRect = soon.rectTransform;
                soonRect.anchorMin = Vector2.zero;
                soonRect.anchorMax = Vector2.one;
                soonRect.offsetMin = new Vector2(12f, 0f);
                soonRect.offsetMax = new Vector2(-12f, 0f);
                soon.gameObject.SetActive(false);
                var mark = new GameObject("Active", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                mark.hideFlags = HideFlags.DontSave;
                mark.transform.SetParent(row.transform, false);
                var markRect = (RectTransform)mark.transform;
                markRect.anchorMin = new Vector2(0f, 0.18f);
                markRect.anchorMax = new Vector2(0f, 0.82f);
                markRect.pivot = new Vector2(0f, 0.5f);
                markRect.sizeDelta = new Vector2(3f, 0f);
                markRect.anchoredPosition = new Vector2(4f, 0f);
                var markImage = mark.GetComponent<Image>();
                markImage.color = MapHudPalette.Accent;
                markImage.raycastTarget = false;
                mark.SetActive(false);
                _regionRows[i] = button;
            }

            menu.SetActive(false);
        }

        static RectTransform Glyph(Transform parent, string name, Sprite sprite, float size)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            image.sprite = sprite;
            image.color = MapHudPalette.Accent;
            image.raycastTarget = false;
            image.preserveAspect = true;
            var rect = (RectTransform)item.transform;
            rect.sizeDelta = new Vector2(size, size);
            return rect;
        }

        void PlaceRegion(MapCommandLayout.Metrics metrics, float brandEnd, float topPad, float scale, Rect safe)
        {
            EnsureRegionUi();
            if (!RegionRowsIntact())
                return;

            EnsureRig();
            SyncRegion();
            var label = _region >= 0 && _region < _regionNames.Length ? _regionNames[_region] : string.Empty;
            if (_regionLabel != null)
                _regionLabel.text = label;
            brandEnd = Mathf.Max(brandEnd, MeasuredBrandEndCanvas());
            var typePixels = MapCommandLayout.MenuTypePixels(safe.width > 1f ? metrics.Header.xMax : 1f, metrics.Header.yMax);
            var textWidth = Mathf.Max(typePixels * 2.4f, MapCommandLayout.TextWidth(label, typePixels)) / Mathf.Max(0.01f, scale);
            var width = 8f + 16f + 6f + textWidth + 6f + 12f + 8f;
            var headerWidth = metrics.Header.width / Mathf.Max(0.01f, scale);
            var room = headerWidth - brandEnd - BrandChipGap(metrics.CanvasTitle) - 8f;
            if (width > room)
                width = Mathf.Max(72f, room);
            var safeBand = metrics.Header.height - Mathf.Max(0f, metrics.Header.yMax - safe.yMax);
            var visualScreen = Mathf.Min(safeBand, Mathf.Max(MapCommandLayout.MinTouchPixels, metrics.FontPixels * 1.15f));
            if (visualScreen < 28f)
                visualScreen = Mathf.Max(28f, safeBand);
            var touch = MapCommandLayout.TouchPixels(metrics.Header.xMax, metrics.Header.yMax);
            var hitScreen = Mathf.Min(safeBand, Mathf.Max(visualScreen, touch));
            var icon = Mathf.Clamp(visualScreen * 0.42f, 16f, 22f) / Mathf.Max(0.01f, scale);
            var chevronSize = Mathf.Clamp(visualScreen * 0.28f, 12f, 16f) / Mathf.Max(0.01f, scale);
            width = 10f + icon + 6f + textWidth + 6f + chevronSize + 10f;
            if (width * scale < touch)
                width = touch / Mathf.Max(0.01f, scale);
            if (width > room)
                width = Mathf.Max(72f, room);
            var chip = (RectTransform)_regionChip.transform;
            chip.anchorMin = new Vector2(0f, 0f);
            chip.anchorMax = new Vector2(0f, 0f);
            chip.pivot = new Vector2(0f, 0f);
            chip.sizeDelta = new Vector2(width, hitScreen / Mathf.Max(0.01f, scale));
            chip.anchoredPosition = new Vector2(
                brandEnd + BrandChipGap(metrics.CanvasTitle),
                (safeBand - hitScreen) * 0.5f / Mathf.Max(0.01f, scale));
            var face = chip.Find("Face") as RectTransform;
            if (face != null)
                face.sizeDelta = new Vector2(width, visualScreen / Mathf.Max(0.01f, scale));
            var glyph = chip.Find("Glyph") as RectTransform;
            if (glyph != null)
            {
                glyph.sizeDelta = new Vector2(icon, icon);
                glyph.anchoredPosition = new Vector2(10f, 0f);
            }

            var chevron = chip.Find("Chevron") as RectTransform;
            if (chevron != null)
            {
                chevron.sizeDelta = new Vector2(chevronSize, chevronSize);
                chevron.anchoredPosition = new Vector2(-10f, 0f);
            }

            var nameRect = _regionLabel.rectTransform;
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(10f + icon + 6f, 0f);
            nameRect.offsetMax = new Vector2(-(10f + chevronSize + 4f), 0f);
            Canvas.ForceUpdateCanvases();
            ClipRegionName();

            var row = touch;
            var menuWidth = MapCommandLayout.RegionMenuWidth(
                typePixels,
                MapCommandLayout.PointScale(metrics.Header.xMax, metrics.Header.yMax),
                _regionNames);
            if (menuWidth < width * scale)
                menuWidth = width * scale;
            var menuTop = metrics.Header.y - 4f;
            var menuFloor = Mathf.Max(safe.yMin + 8f, metrics.Summary.yMax + 8f);
            var available = Mathf.Max(row, menuTop - menuFloor);
            var maskScreen = 8f * Mathf.Max(0.01f, scale);
            var wanted = row * _regionRows.Length + maskScreen;
            if (wanted > available)
            {
                var tightFloor = Mathf.Max(safe.yMin + 2f, metrics.Summary.yMax + 2f);
                var tight = menuTop - tightFloor;
                if (tight + 0.5f >= wanted)
                    available = Mathf.Max(available, tight);
            }

            var menuHeight = Mathf.Min(wanted, available);
            var menuX = (brandEnd + BrandChipGap(metrics.CanvasTitle)) * scale;
            var maxMenu = Mathf.Max(96f * MapCommandLayout.PointScale(metrics.Header.xMax, metrics.Header.yMax), safe.width - 16f);
            if (menuWidth > maxMenu)
                menuWidth = maxMenu;
            if (menuX + menuWidth > safe.xMax - 8f)
                menuX = safe.xMax - 8f - menuWidth;
            if (menuX < safe.xMin + 8f)
                menuX = safe.xMin + 8f;
            PlaceRect(_regionMenu, new Rect(menuX, menuTop - menuHeight, menuWidth, menuHeight), safe, scale);
            var keepMenu = _regionLaidOut && _regionScroll != null ? _regionScroll.verticalNormalizedPosition : 1f;
            var contentHeight = row / Mathf.Max(0.01f, scale) * _regionRows.Length;
            _regionContent.sizeDelta = new Vector2(0f, contentHeight);
            var rowHeight = row / Mathf.Max(0.01f, scale);
            for (var i = 0; i < _regionRows.Length; i++)
            {
                if (_regionRows[i] == null)
                    continue;
                var rect = (RectTransform)_regionRows[i].transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, rowHeight);
                rect.anchoredPosition = new Vector2(0f, -rowHeight * i);
            }

            var viewportCanvas = menuHeight / Mathf.Max(0.01f, scale) - 8f;
            var sliver = 12f / Mathf.Max(0.01f, scale);
            var scrolling = contentHeight > viewportCanvas + sliver;
            _regionLaidOut = true;
            _regionScroll.vertical = scrolling;
            _regionScroll.verticalNormalizedPosition = scrolling ? Mathf.Clamp01(keepMenu) : 1f;
            var point = MapCommandLayout.PointScale(metrics.Header.xMax, metrics.Header.yMax);
            var barCanvas = Mathf.Max(6f, 4f * point) / Mathf.Max(0.01f, scale);
            if (_regionBar != null)
            {
                var barRect = (RectTransform)_regionBar.transform;
                barRect.offsetMin = new Vector2(-(4f + barCanvas), 8f);
                barRect.offsetMax = new Vector2(-4f, -8f);
                if (_regionBar.gameObject.activeSelf != scrolling)
                    _regionBar.gameObject.SetActive(scrolling);
                var track = _regionBar.GetComponent<Image>();
                track.color = new Color(MapHudPalette.Accent.r, MapHudPalette.Accent.g, MapHudPalette.Accent.b, 0.45f);
            }

            var textPad = scrolling ? -(12f + barCanvas) : -12f;
            for (var i = 0; i < _regionRows.Length; i++)
            {
                if (_regionRows[i] == null)
                    continue;
                var name = _regionRows[i].transform.Find("Name") as RectTransform;
                if (name != null)
                    name.offsetMax = new Vector2(textPad, 0f);
            }
            _menuRest = _regionMenu.anchoredPosition;
            PaintRegion();
            ApplyMenuMotion(MenuAmount());
        }

        bool ShellChromeIntact()
        {
            return _header != null && _rail != null && _context != null && _summary != null
                && _brandLead != null && _brandTail != null
                && _hud != null && _hud.Intact;
        }

        bool RegionRowsIntact()
        {
            if (_regionChip == null || _regionMenu == null || _regionContent == null || _regionLabel == null)
                return false;
            for (var i = 0; i < _regionRows.Length; i++)
            {
                if (_regionRows[i] == null)
                    return false;
            }

            return true;
        }

        void EnsureRegionUi()
        {
            if (RegionRowsIntact())
                return;
            if (_header == null)
                return;
            TearDownRegion();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildRegion(font);
        }

        void TearDownRegion()
        {
            if (_regionChip != null)
            {
                _regionChip.onClick.RemoveListener(ToggleRegionMenu);
                Object.DestroyImmediate(_regionChip.gameObject);
            }
            else if (_header != null)
            {
                var leftoverChip = _header.Find("RegionChip");
                if (leftoverChip != null)
                    Object.DestroyImmediate(leftoverChip.gameObject);
            }

            if (_regionMenu != null)
                Object.DestroyImmediate(_regionMenu.gameObject);
            else
            {
                var leftoverMenu = transform.Find("RegionMenu");
                if (leftoverMenu != null)
                    Object.DestroyImmediate(leftoverMenu.gameObject);
            }

            for (var i = 0; i < _regionRows.Length; i++)
                _regionRows[i] = null;
            _regionChip = null;
            _regionMenu = null;
            _regionContent = null;
            _regionScroll = null;
            _regionBar = null;
            _regionLabel = null;
            _menuGroup = null;
            _regionLaidOut = false;
        }

        void SyncRegion()
        {
            if (_rig == null || RegionMenuOpen)
                return;

            var next = (int)_rig.Scope;
            if (next < 0 || next >= _regionRows.Length)
                next = 1;
            if (next != _region)
                _region = next;
        }

        void EnsureRig()
        {
            if (_rig != null)
                return;

            var scene = gameObject.scene;
            if (!scene.IsValid())
                return;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                _rig = roots[i].GetComponentInChildren<MapCameraRig>(true);
                if (_rig != null)
                    return;
            }
        }

        public void ToggleRegionMenu()
        {
            if (_regionMenu == null)
                return;

            SetRegionMenu(!_menuOpen);
        }

        public void CloseRegionMenu()
        {
            if (_regionMenu == null || !_menuOpen)
                return;

            SetRegionMenu(false);
        }

        public void ScrollRail(float verticalNormalized)
        {
            _railNormalized = Mathf.Clamp01(verticalNormalized);
            PinRailScroll();
        }

        public void ScrollRegion(float verticalNormalized)
        {
            ApplyScroll(_regionScroll, verticalNormalized);
        }

        void SetRegionMenu(bool open)
        {
            _menuFrom = _menuGroup != null ? _menuGroup.alpha : 0f;
            _menuOpen = open;
            _menuTime = 0f;
            _menuAnimating = Application.isPlaying;
            if (open)
                _regionMenu.gameObject.SetActive(true);

            if (open)
            {
                if (_regionScroll != null)
                    _regionScroll.verticalNormalizedPosition = 1f;
                var scale = MapHudLayout.CanvasScale(_width, _height);
                if (scale < 0.0001f)
                    scale = 1f;
                var leftPad = _safe.width > 1f ? _safe.xMin / scale : 0f;
                var topPad = _height > _safe.yMax ? (_height - _safe.yMax) / scale : 0f;
                PlaceRegion(_metrics, BrandEndCanvas(leftPad, _metrics.CanvasTitle), topPad, scale, _safe);
            }
            else
            {
                ApplyMenuMotion(MenuAmount());
            }

            if (!Application.isPlaying)
            {
                _menuAnimating = false;
                ApplyMenuMotion(open ? 1f : 0f);
                if (!open && _regionMenu != null)
                    _regionMenu.gameObject.SetActive(false);
                return;
            }

            if (_menuGroup == null)
                return;

            _menuGroup.blocksRaycasts = open;
            _menuGroup.interactable = open;
        }

        float MenuAmount()
        {
            if (!Application.isPlaying || !_menuAnimating)
                return _menuOpen ? 1f : 0f;

            var duration = MenuMotionSeconds <= 0f ? 1f : MenuMotionSeconds;
            var t = Mathf.Clamp01(_menuTime / duration);
            var eased = t * t * (3f - 2f * t);
            return Mathf.Lerp(_menuFrom, _menuOpen ? 1f : 0f, eased);
        }

        void ApplyMenuMotion(float openAmount)
        {
            if (_regionMenu == null)
                return;

            openAmount = Mathf.Clamp01(openAmount);
            if (_menuGroup == null)
                _menuGroup = _regionMenu.GetComponent<CanvasGroup>();
            if (_menuGroup != null)
            {
                _menuGroup.alpha = openAmount;
                _menuGroup.blocksRaycasts = _menuOpen;
                _menuGroup.interactable = _menuOpen;
            }

            var scale = Mathf.Lerp(0.98f, 1f, openAmount);
            _regionMenu.localScale = new Vector3(scale, scale, 1f);
            _regionMenu.anchoredPosition = _menuRest + new Vector2(0f, (1f - openAmount) * 6f);
            var chevron = _regionChip != null ? _regionChip.transform.Find("Chevron") as RectTransform : null;
            if (chevron != null)
                chevron.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(0f, 180f, openAmount));
            var face = _regionChip != null ? _regionChip.transform.Find("Face")?.GetComponent<Image>() : null;
            if (face != null)
                face.color = Color.Lerp(MapHudPalette.Button, MapHudPalette.ButtonActive, openAmount);
        }

        public bool SelectRegion(int index)
        {
            if (index < 0 || index >= MapContent.RegionViews.Length)
                return false;

            EnsureRig();
            if (_rig == null)
                return false;

            if (index != _region && _state != PanelState.Closed && !ManagementOpen)
                CloseContext();

            if (index == 0)
                _rig.FocusWorld();
            else if (index == 1)
                _rig.FocusTurkey();
            else
                _rig.FocusRegion((MapScope)index);
            _region = index;
            CloseRegionMenu();
            PaintRegion();
            return true;
        }

        internal void PreviewRegionLabel(string label)
        {
            if (_regionLabel != null && _regionLabel.text != label)
                _regionLabel.text = label ?? string.Empty;
            FitChip(label ?? string.Empty);
            FitMenu();
        }

        void FitChip(string label)
        {
            if (_regionChip == null || _metrics.Header.width < 1f)
                return;

            var screenWidth = _metrics.Header.xMax;
            var screenHeight = _metrics.Header.yMax;
            var scale = MapHudLayout.CanvasScale(screenWidth, screenHeight);
            if (scale < 0.0001f)
                scale = 1f;
            var typePixels = MapCommandLayout.MenuTypePixels(screenWidth, screenHeight);
            var textWidth = Mathf.Max(typePixels * 2.4f, MapCommandLayout.TextWidth(label, typePixels)) / scale;
            var glyph = _regionChip.transform.Find("Glyph") as RectTransform;
            var chevron = _regionChip.transform.Find("Chevron") as RectTransform;
            var icon = glyph != null ? glyph.sizeDelta.x : 16f;
            var chevronSize = chevron != null ? chevron.sizeDelta.x : 12f;
            var width = 10f + icon + 6f + textWidth + 6f + chevronSize + 10f;
            var headerWidth = _metrics.Header.width / scale;
            var leftPad = _safe.xMin / scale;
            var brandEnd = Mathf.Max(BrandEndCanvas(leftPad, _metrics.CanvasTitle), MeasuredBrandEndCanvas());
            var gap = BrandChipGap(_metrics.CanvasTitle);
            var room = headerWidth - brandEnd - gap - 8f;
            if (width > room)
                width = Mathf.Max(48f, room);
            var touch = MapCommandLayout.TouchPixels(screenWidth, screenHeight);
            if (width * scale < touch)
                width = Mathf.Min(touch / Mathf.Max(0.01f, scale), Mathf.Max(48f, room));
            var chip = (RectTransform)_regionChip.transform;
            chip.anchoredPosition = new Vector2(brandEnd + gap, chip.anchoredPosition.y);
            if (Mathf.Abs(chip.sizeDelta.x - width) > 0.5f)
                chip.sizeDelta = new Vector2(width, chip.sizeDelta.y);
            if (_regionLabel != null)
            {
                _regionLabel.text = label;
                Canvas.ForceUpdateCanvases();
                ClipRegionName();
            }

            PlaceHud();
        }

        void PaintRegion()
        {
            var label = _region >= 0 && _region < _regionNames.Length ? _regionNames[_region] : string.Empty;
            if (_regionLabel != null && _regionLabel.text != label)
                _regionLabel.text = label;
            FitChip(label);
            FitMenu();

            for (var i = 0; i < _regionRows.Length; i++)
            {
                var button = _regionRows[i];
                if (button == null)
                    continue;

                var selected = i == _region;
                var image = button.GetComponent<Image>();
                if (image != null)
                    image.color = selected ? MapHudPalette.ButtonActive : new Color(1f, 1f, 1f, 0f);
                var active = button.transform.Find("Active");
                if (active != null && active.gameObject.activeSelf != selected)
                    active.gameObject.SetActive(selected);
                var name = FindText(button.transform, "Name");
                if (name != null)
                {
                    var value = i < _regionNames.Length ? _regionNames[i] : string.Empty;
                    if (name.text != value)
                        name.text = value;
                    name.color = MapHudPalette.Text;
                }

                var soon = FindText(button.transform, "Soon");
                if (soon != null && soon.gameObject.activeSelf)
                    soon.gameObject.SetActive(false);
            }
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
            _railScroll.vertical = true;
            _railScroll.movementType = ScrollRect.MovementType.Clamped;
            _railScroll.inertia = true;
            _railScroll.decelerationRate = 0.12f;
            _railScroll.scrollSensitivity = 18f;
            var bar = new GameObject("RailScroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            bar.hideFlags = HideFlags.DontSave;
            bar.transform.SetParent(_rail, false);
            var barImage = bar.GetComponent<Image>();
            barImage.color = new Color(MapHudPalette.Accent.r, MapHudPalette.Accent.g, MapHudPalette.Accent.b, 0.16f);
            barImage.raycastTarget = false;
            _railBar = bar.GetComponent<Scrollbar>();
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(4f, 0f);
            barRect.anchoredPosition = new Vector2(-2f, 0f);
            var thumb = new GameObject("Thumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            thumb.hideFlags = HideFlags.DontSave;
            thumb.transform.SetParent(bar.transform, false);
            var thumbImage = thumb.GetComponent<Image>();
            thumbImage.color = MapHudPalette.Accent;
            thumbImage.raycastTarget = false;
            var thumbRect = (RectTransform)thumb.transform;
            thumbRect.anchorMin = Vector2.zero;
            thumbRect.anchorMax = Vector2.one;
            thumbRect.offsetMin = new Vector2(1f, 1f);
            thumbRect.offsetMax = new Vector2(-1f, -1f);
            _railBar.handleRect = thumbRect;
            _railBar.targetGraphic = thumbImage;
            _railBar.direction = Scrollbar.Direction.BottomToTop;
            _railBar.interactable = false;
            _railScroll.verticalScrollbar = null;
            _railScroll.horizontalScrollbar = null;
            _railScroll.onValueChanged.AddListener(OnRailScrolled);
            bar.SetActive(false);
            WireRailDrag();
            if (!_railPinned)
            {
                Canvas.willRenderCanvases += PinRailOnCanvas;
                _railPinned = true;
            }
        }

        void ShowRailRows(bool futureModes)
        {
            if (_future != null)
                _future.gameObject.SetActive(true);

            for (var i = 0; i < _moduleButtons.Length; i++)
            {
                if (_moduleButtons[i] != null)
                    _moduleButtons[i].gameObject.SetActive(i == 0 || futureModes);
            }
        }

        void PlaceRail(MapCommandLayout.Metrics metrics, float leftPad, float scale)
        {
            if (_rail == null || _railViewport == null || _railContent == null)
                return;

            var icon = MapCommandLayout.RailIconPixels(metrics.FontPixels) / scale;
            var gapPixels = MapCommandLayout.RailRowGap(metrics.FontPixels);
            var gap = gapPixels / scale;
            var rowHeight = MapCommandLayout.RailRowPixels(metrics.FontPixels, _width, _height) / scale;
            var width = Mathf.Max(44f, metrics.Rail.width / scale - leftPad);
            var viewportHeight = Mathf.Max(rowHeight, _rail.sizeDelta.y);
            var count = MapContent.FutureModes.Length;
            var iconGap = MapCommandLayout.RailIconLabelGap(metrics.FontPixels) / scale;
            var labelBlock = MapCommandLayout.RailLabelBlock(metrics.FontPixels) / scale;
            var keep = _railLaidOut ? _railNormalized : 1f;
            var rowsHeight = MapCommandLayout.RailContentHeight(metrics.FontPixels, count, gapPixels, _width, _height) / scale;
            var overflow = rowsHeight > viewportHeight + 1f;
            var edgeInset = overflow
                ? 2f / Mathf.Max(0.01f, scale)
                : 8f / Mathf.Max(0.01f, scale);
            var topInset = overflow
                ? edgeInset
                : MapCommandLayout.RailContentTopInset(viewportHeight, rowsHeight, edgeInset);
            var bottomInset = overflow ? edgeInset : topInset;
            var contentHeight = rowsHeight + topInset + bottomInset;

            _railViewport.anchorMin = new Vector2(0f, 1f);
            _railViewport.anchorMax = new Vector2(0f, 1f);
            _railViewport.pivot = new Vector2(0f, 1f);
            _railViewport.sizeDelta = new Vector2(width, viewportHeight);
            _railViewport.anchoredPosition = new Vector2(leftPad, 0f);

            _railContent.sizeDelta = new Vector2(width, contentHeight);
            var scroll = contentHeight > viewportHeight + 1f;

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
                    -(topInset + i * (rowHeight + gap)),
                    width,
                    rowHeight,
                    icon,
                    labelBlock,
                    iconGap,
                    MapModuleIds.IsTransport(i));
            }

            if (_railScroll == null)
                return;

            _railScroll.horizontal = false;
            _railScroll.vertical = scroll;
            _railLaidOut = true;
            _railNormalized = scroll ? Mathf.Clamp01(keep) : 1f;
            if (_railBar != null)
            {
                if (_railBar.gameObject.activeSelf != scroll)
                    _railBar.gameObject.SetActive(scroll);
                if (scroll)
                    _railBar.size = Mathf.Clamp01(viewportHeight / Mathf.Max(0.01f, contentHeight));
            }

            PinRailScroll();
        }

        void PinRailOnCanvas()
        {
            if (!_railDragging && (_railScroll == null || _railScroll.velocity.sqrMagnitude < 1f))
                PinRailScroll();
        }

        void PinRailScroll()
        {
            if (_railContent == null || _railViewport == null)
                return;

            var hidden = Mathf.Max(0f, _railContent.rect.height - _railViewport.rect.height);
            if (_railScroll != null && !_railScroll.vertical)
                hidden = 0f;
            var y = (1f - Mathf.Clamp01(_railNormalized)) * hidden;
            _railContent.anchoredPosition = new Vector2(0f, y);
            if (_railScroll != null)
                _railScroll.StopMovement();
            if (_railBar != null && _railBar.gameObject.activeSelf)
                _railBar.SetValueWithoutNotify(Mathf.Clamp01(_railNormalized));
        }

        void WireRailDrag()
        {
            if (_railViewport == null)
                return;

            var trigger = _railViewport.GetComponent<EventTrigger>() ?? _railViewport.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            var begin = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            begin.callback.AddListener(_ => _railDragging = true);
            trigger.triggers.Add(begin);
            var end = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            end.callback.AddListener(_ => _railDragging = false);
            trigger.triggers.Add(end);
        }

        void OnRailScrolled(Vector2 value)
        {
            if (!_applying)
                _railNormalized = Mathf.Clamp01(value.y);
            if (_railBar != null && _railBar.gameObject.activeSelf)
                _railBar.SetValueWithoutNotify(Mathf.Clamp01(_railNormalized));
        }

        void WireRailTap(Button button, int slot)
        {
            button.onClick.RemoveAllListeners();
            if (!MapModuleIds.IsTransport(slot))
                button.onClick.AddListener(() => ShowModule(slot));
            var trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(data =>
            {
                var pointer = data as PointerEventData;
                if (pointer == null || pointer.button != PointerEventData.InputButton.Left || pointer.dragging)
                    return;
                if (!MapModuleIds.IsTransport(slot))
                    ShowModule(slot);
            });
            trigger.triggers.Add(entry);
        }

        static void ApplyScroll(ScrollRect scroll, float verticalNormalized)
        {
            if (scroll == null || !scroll.vertical)
                return;

            scroll.verticalNormalizedPosition = Mathf.Clamp01(verticalNormalized);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = Mathf.Clamp01(verticalNormalized);
            Canvas.ForceUpdateCanvases();
        }

        static void PlaceRailRow(
            RectTransform row,
            float x,
            float y,
            float width,
            float height,
            float icon,
            float labelBlock,
            float iconGap,
            bool comingSoon)
        {
            if (row == null)
                return;

            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(width, height);
            row.anchoredPosition = new Vector2(x, y);
            var frames = MapCommandLayout.RailFrames(width, height, icon, labelBlock, iconGap, comingSoon, 0.5f);
            PlaceRailItem(row.Find("Icon") as RectTransform, frames.Icon, width);
            var status = row.Find("Status") as RectTransform;
            if (status != null)
            {
                status.gameObject.SetActive(comingSoon);
                if (comingSoon)
                    PlaceRailItem(status, frames.Status, width);
            }

            PlaceRailItem(row.Find("Label") as RectTransform, frames.Label, width);
        }

        static void PlaceRailItem(RectTransform item, Rect frame, float rowWidth)
        {
            if (item == null)
                return;
            item.anchorMin = new Vector2(0.5f, 1f);
            item.anchorMax = new Vector2(0.5f, 1f);
            item.pivot = new Vector2(0.5f, 1f);
            item.sizeDelta = frame.size;
            item.anchoredPosition = new Vector2(frame.center.x - rowWidth * 0.5f, -frame.yMin);
            var text = item.GetComponent<Text>();
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        void FitMenu()
        {
            if (_regionMenu == null || _metrics.Header.width < 1f)
                return;

            var screenWidth = _metrics.Header.xMax;
            var screenHeight = _metrics.Header.yMax;
            var scale = MapHudLayout.CanvasScale(screenWidth, screenHeight);
            if (scale < 0.0001f)
                scale = 1f;
            var typePixels = MapCommandLayout.MenuTypePixels(screenWidth, screenHeight);
            var screen = MapCommandLayout.RegionMenuWidth(typePixels, MapCommandLayout.PointScale(screenWidth, screenHeight), _regionNames);
            var chip = _regionChip != null ? ((RectTransform)_regionChip.transform).sizeDelta.x * scale : 0f;
            if (screen < chip)
                screen = chip;
            var canvas = screen / scale;
            var maxScreen = _safe.width > 1f ? Mathf.Max(96f, _safe.width - 16f) : screen;
            if (screen > maxScreen)
            {
                screen = maxScreen;
                canvas = screen / scale;
            }

            if (Mathf.Abs(_regionMenu.sizeDelta.x - canvas) > 0.5f)
                _regionMenu.sizeDelta = new Vector2(canvas, _regionMenu.sizeDelta.y);
            var menuRight = _menuRest.x * scale + screen;
            var limit = _safe.xMax - 8f;
            if (_safe.width > 1f && menuRight > limit)
            {
                var shift = (menuRight - limit) / scale;
                _menuRest.x -= shift;
                if (!_menuAnimating)
                    _regionMenu.anchoredPosition = new Vector2(_regionMenu.anchoredPosition.x - shift, _regionMenu.anchoredPosition.y);
            }
        }

        static bool IsRegionMenuText(Text text)
        {
            if (text == null || text.name != "Name")
                return false;

            var parent = text.transform.parent;
            if (parent == null)
                return false;

            if (parent.name == "RegionChip")
                return true;

            return parent.name.Length == 7 && parent.name.StartsWith("Region");
        }

        static bool IsRailLabel(Text text)
        {
            var parent = text != null ? text.transform.parent : null;
            if (parent == null || text.name != "Label")
                return false;

            return parent.name.Length == 5 && parent.name.StartsWith("Mode");
        }

        static bool IsRailStatus(Text text)
        {
            var parent = text != null ? text.transform.parent : null;
            if (parent == null || text.name != "Status")
                return false;

            return parent.name.Length == 5 && parent.name.StartsWith("Mode");
        }

        static int RailLabelCanvas(MapCommandLayout.Metrics metrics, float scale)
        {
            return Mathf.Max(10, Mathf.RoundToInt(MapCommandLayout.RailLabelPixels(metrics.FontPixels) / Mathf.Max(0.01f, scale)));
        }

        static int RailSoonCanvas(MapCommandLayout.Metrics metrics, float scale, float budget)
        {
            var size = Mathf.Max(9, Mathf.RoundToInt(MapCommandLayout.RailStatusPixels(metrics.FontPixels) / Mathf.Max(0.01f, scale)));
            if (budget < 8f)
                budget = metrics.Rail.width / Mathf.Max(0.01f, scale) - 8f;
            while (size > 9 && MapCommandLayout.TextWidth(MapCommandLayout.LongestSoon, size) > budget)
                size--;
            return size;
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
            readonly MapCommandShell _owner;
            readonly LocalizedString _source;
            readonly Text _target;
            readonly System.Action<string> _extra;
            bool _subscribed;

            public Binding(MapCommandShell owner, string table, string key, Text target, System.Action<string> extra)
            {
                _owner = owner;
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
                if (_owner == null)
                    return;

                var text = value ?? string.Empty;
                if (_target != null)
                    _target.text = text;
                _extra?.Invoke(text);
            }
        }
    }
}
