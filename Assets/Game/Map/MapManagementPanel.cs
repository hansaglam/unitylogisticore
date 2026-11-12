using System.Collections.Generic;
using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// One reusable landscape management overlay. No gameplay data, no fake assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapManagementPanel : MonoBehaviour
    {
        static readonly Color Surface = new Color(0.027f, 0.055f, 0.094f, 0.96f);
        static readonly Color Raised = new Color(0.055f, 0.102f, 0.138f, 1f);

        RectTransform _blocker;
        RectTransform _panel;
        Button _close;
        Button _expand;
        Button _chip;
        RectTransform _menu;
        readonly Button[] _choices = new Button[5];
        Text _title;
        Text _body;
        Text _expandLabel;
        Text _chipLabel;
        Image _expandIcon;
        Image _closeIcon;
        bool _built;
        bool _listening;
        bool _menuOpen;
        bool _expanded;
        float _placedWidth;
        float _placedHeight;
        float _placedScale = 1f;
        MapModule _module = MapModule.Fleet;
        MapFleetView _fleet;
        MapJobsView _jobs;
        MapWarehouseView _warehouses;
        MapMarketView _market;
        MapRoadDeliveryView _road;
        readonly List<CopyBind> _binds = new List<CopyBind>(16);
        readonly string[] _titles = new string[5];
        readonly string[] _bodies = new string[5];
        string _expandCopy = string.Empty;
        string _collapseCopy = string.Empty;

        public MapModule Module => _module;
        public bool Expanded => _expanded;
        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;
        public bool MenuOpen => _menuOpen;
        public Button CloseButton => _close;
        public Button ExpandButton => _expand;
        public Button SwitchButton => _chip;
        public string TitleText => _title != null ? _title.text : string.Empty;
        public string BodyText => _body != null ? _body.text : string.Empty;
        public string ExpandText => string.Empty;
        public string ExpandAccessName => _expanded ? _collapseCopy : _expandCopy;
        public Sprite ExpandSprite => _expandIcon != null ? _expandIcon.sprite : null;
        public Sprite CloseSprite => _closeIcon != null ? _closeIcon.sprite : null;
        public float CloseIconAlpha => _closeIcon != null ? _closeIcon.color.a : 0f;
        public Button[] ModuleChoices => _choices;
        public RectTransform PanelRect => _panel;
        public RectTransform BlockerRect => _blocker;
        public MapFleetView Fleet => _fleet;
        public MapJobsView Jobs => _jobs;
        public MapWarehouseView Warehouses => _warehouses;
        public MapMarketView Market => _market;
        public MapRoadDeliveryView Road => _road;

        public static MapManagementPanel Attach(MapCommandShell shell)
        {
            if (shell == null)
                return null;

            var existing = shell.GetComponentInChildren<MapManagementPanel>(true);
            if (existing != null)
                return existing;

            var item = new GameObject("ManagementRoot", typeof(RectTransform));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(shell.transform, false);
            var view = item.AddComponent<MapManagementPanel>();
            view.BuildIfNeeded();
            return view;
        }

        void Awake()
        {
            BuildIfNeeded();
        }

        void OnEnable()
        {
            BuildIfNeeded();
            Hook();
            Subscribe();
        }

        void OnDisable()
        {
            Unhook();
            Unsubscribe();
        }

        void OnDestroy()
        {
            for (var i = 0; i < _binds.Count; i++)
                _binds[i].Dispose();
            _binds.Clear();
        }

        public void Show(MapModule module)
        {
            if (module == MapModule.None)
            {
                Hide();
                return;
            }

            BuildIfNeeded();
            Hook();
            _module = module;
            gameObject.SetActive(true);
            if (_panel != null)
                _panel.gameObject.SetActive(true);
            CloseMenu();
            Present();
        }

        public void Hide()
        {
            _module = MapModule.None;
            _expanded = false;
            _menuOpen = false;
            if (_menu != null)
                _menu.gameObject.SetActive(false);
            if (_panel != null)
                _panel.gameObject.SetActive(false);
            if (_blocker != null)
                _blocker.gameObject.SetActive(false);
        }

        public void SetExpanded(bool expanded)
        {
            _expanded = expanded;
            CloseMenu();
            Present();
        }

        public void ToggleExpanded()
        {
            SetExpanded(!_expanded);
        }

        public void ToggleMenu()
        {
            if (!IsOpen)
                return;

            _menuOpen = !_menuOpen;
            if (_menu != null)
                _menu.gameObject.SetActive(_menuOpen);
            Present();
        }

        public void Choose(int index)
        {
            if (!MapModuleIds.TryFromRail(index, out var module))
                return;

            var shell = GetComponentInParent<MapCommandShell>();
            if (shell != null)
            {
                shell.OpenManagement(module);
                return;
            }

            _module = module;
            CloseMenu();
            Present();
        }

        public void Place(MapCommandLayout.Metrics metrics, float screenWidth, float screenHeight, Rect safeArea)
        {
            BuildIfNeeded();
            var origin = safeArea.width < 1f || safeArea.height < 1f
                ? new Rect(0f, 0f, screenWidth, screenHeight)
                : safeArea;
            var scale = MapHudLayout.CanvasScale(screenWidth, screenHeight);
            if (scale < 0.0001f)
                scale = 1f;

            var touch = MapCommandLayout.TouchPixels(screenWidth, screenHeight);
            var open = IsOpen && _module != MapModule.None;
            _placedWidth = screenWidth;
            _placedHeight = screenHeight;
            _placedScale = scale;
            if (_blocker != null)
            {
                _blocker.gameObject.SetActive(open && _expanded);
                if (open && _expanded)
                    MapManagementLayout.Place(_blocker, MapManagementLayout.Blocker(metrics.Viewport), origin, scale);
            }

            if (_panel == null)
                return;

            _panel.gameObject.SetActive(open);
            if (!open)
                return;

            var frame = _expanded
                ? MapManagementLayout.Expanded(metrics.Viewport, metrics.FontPixels)
                : MapManagementLayout.Compact(metrics.Viewport, metrics.FontPixels, touch);
            MapManagementLayout.Place(_panel, frame, origin, scale);
            PlaceChrome(metrics, scale, touch, screenWidth, screenHeight);
            PlaceFleet(metrics, scale, touch);
            PlaceJobs(metrics, scale, touch);
            PlaceWarehouses(metrics, scale, touch);
            PlaceMarket(metrics, scale, touch);
            PlaceRoad(metrics, scale, touch);
            RaiseChrome();
        }

        public float PlacedCanvasScale => _placedScale;

        public Rect CloseHitPixels()
        {
            return MapManagementLayout.HitRect((RectTransform)_close.transform, _placedScale);
        }

        public Rect ExpandHitPixels()
        {
            return MapManagementLayout.HitRect((RectTransform)_expand.transform, _placedScale);
        }

        public Rect ChipHitPixels()
        {
            return MapManagementLayout.HitRect((RectTransform)_chip.transform, _placedScale);
        }

        public Rect ChoiceHitPixels(int index)
        {
            if (index < 0 || index >= _choices.Length || _choices[index] == null)
                return default;

            return MapManagementLayout.HitRect((RectTransform)_choices[index].transform, _placedScale);
        }

        public bool ContainsScreenPoint(Vector2 screen)
        {
            if (!IsOpen)
                return false;

            if (_expanded && _blocker != null && _blocker.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(_blocker, screen, null))
                return true;

            return _panel != null && _panel.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(_panel, screen, null);
        }

        void BuildIfNeeded()
        {
            if (_built)
                return;

            _built = true;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            _blocker = Graphic("ManagementBlocker", transform, new Color(0.01f, 0.03f, 0.05f, 0.45f), true);
            _panel = Graphic("ManagementPanel", transform, Surface, true);
            Stroke(_panel);
            _title = Label(_panel, "ManagementTitle", font, FontStyle.Bold, MapHudPalette.Text);
            _body = Label(_panel, "ManagementBody", font, FontStyle.Normal, MapHudPalette.TextMuted);
            _close = IconButton(_panel, "ManagementClose", out _closeIcon, out var closeAccess);
            closeAccess.enabled = false;
            _closeIcon.sprite = MapPanelIcons.Get(MapPanelIcons.Close);
            _closeIcon.color = MapHudPalette.Accent;
            _expand = IconButton(_panel, "ManagementExpand", out _expandIcon, out _expandLabel);
            _chip = Action(_panel, "ManagementChip", font, out _chipLabel);
            _chipLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _chipLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _menu = Graphic("ManagementMenu", _panel, Raised, true);
            _menu.gameObject.SetActive(false);
            for (var i = 0; i < _choices.Length; i++)
            {
                _choices[i] = Action(_menu, "ManagementChoice" + i, font, out _);
                var slot = i;
                _choices[i].onClick.AddListener(() => Choose(slot));
            }

            Track(OpsContent.TitleFleet, value => _titles[0] = value);
            Track(OpsContent.TitleJobs, value => _titles[1] = value);
            Track(OpsContent.TitleWarehouses, value => _titles[2] = value);
            Track(OpsContent.TitleMarket, value => _titles[3] = value);
            Track(OpsContent.TitleRoad, value => _titles[4] = value);
            Track(OpsContent.BodyFleet, value => _bodies[0] = value);
            Track(OpsContent.BodyJobs, value => _bodies[1] = value);
            Track(OpsContent.BodyWarehouses, value => _bodies[2] = value);
            Track(OpsContent.BodyMarket, value => _bodies[3] = value);
            Track(OpsContent.BodyRoad, value => _bodies[4] = value);
            Track(OpsContent.Expand, value => _expandCopy = value);
            Track(OpsContent.Collapse, value => _collapseCopy = value);
            _fleet = MapFleetView.Attach(_panel);
            _jobs = MapJobsView.Attach(_panel);
            _warehouses = MapWarehouseView.Attach(_panel);
            _market = MapMarketView.Attach(_panel);
            _road = MapRoadDeliveryView.Attach(_panel);
            Hide();
        }

        void Hook()
        {
            if (_listening)
                return;

            if (_close != null)
                _close.onClick.AddListener(RequestClose);
            if (_expand != null)
                _expand.onClick.AddListener(RequestExpand);
            if (_chip != null)
                _chip.onClick.AddListener(ToggleMenu);
            _listening = true;
        }

        void Unhook()
        {
            if (!_listening)
                return;

            if (_close != null)
                _close.onClick.RemoveListener(RequestClose);
            if (_expand != null)
                _expand.onClick.RemoveListener(RequestExpand);
            if (_chip != null)
                _chip.onClick.RemoveListener(ToggleMenu);
            _listening = false;
        }

        void RequestClose()
        {
            var shell = GetComponentInParent<MapCommandShell>();
            if (shell != null)
                shell.CloseContext();
            else
                Hide();
        }

        void RequestExpand()
        {
            var shell = GetComponentInParent<MapCommandShell>();
            if (shell != null)
                shell.SetManagementExpanded(!_expanded);
            else
                SetExpanded(!_expanded);
        }

        void Subscribe()
        {
            for (var i = 0; i < _binds.Count; i++)
                _binds[i].Subscribe();

            LocalizationSettings.SelectedLocaleChanged -= OnLocale;
            LocalizationSettings.SelectedLocaleChanged += OnLocale;
        }

        void Unsubscribe()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocale;
            for (var i = 0; i < _binds.Count; i++)
                _binds[i].Unsubscribe();
        }

        void OnLocale(Locale locale)
        {
            Present();
        }

        void Track(string key, System.Action<string> extra)
        {
            _binds.Add(new CopyBind(OpsContent.Table, key, extra));
        }

        void Present()
        {
            var index = MapModuleIds.RailIndex(_module);
            if (index < 0)
                index = 0;

            if (_title != null)
            {
                _title.enabled = false;
                _title.text = string.Empty;
            }
            if (_body != null)
                _body.text = _bodies[index] ?? string.Empty;
            if (_expandLabel != null)
            {
                _expandLabel.enabled = false;
                _expandLabel.text = _expanded ? _collapseCopy : _expandCopy;
            }

            if (_expandIcon != null)
            {
                _expandIcon.sprite = MapPanelIcons.Get(_expanded ? MapPanelIcons.Collapse : MapPanelIcons.Expand);
                _expandIcon.color = MapHudPalette.Accent;
            }

            if (_closeIcon != null)
            {
                _closeIcon.sprite = MapPanelIcons.Get(MapPanelIcons.Close);
                _closeIcon.color = MapHudPalette.Accent;
            }

            if (_chipLabel != null)
                _chipLabel.text = _titles[index] ?? string.Empty;
            if (_fleet != null)
            {
                var fleet = _module == MapModule.Fleet && IsOpen;
                _fleet.gameObject.SetActive(fleet);
                if (fleet)
                    _fleet.Refresh();
            }

            if (_jobs != null)
            {
                var jobs = _module == MapModule.Jobs && IsOpen;
                _jobs.gameObject.SetActive(jobs);
                if (jobs)
                    _jobs.Refresh();
            }

            if (_warehouses != null)
            {
                var warehouses = _module == MapModule.Warehouses && IsOpen;
                _warehouses.gameObject.SetActive(warehouses);
                if (warehouses)
                    _warehouses.Refresh();
            }

            if (_market != null)
            {
                var market = _module == MapModule.Market && IsOpen;
                _market.gameObject.SetActive(market);
                if (market)
                    _market.Refresh();
            }

            if (_road != null)
            {
                var road = _module == MapModule.Road && IsOpen;
                _road.gameObject.SetActive(road);
                if (road)
                    _road.Refresh();
            }

            if (_body != null)
                _body.enabled = _module != MapModule.Fleet
                    && _module != MapModule.Jobs
                    && _module != MapModule.Warehouses
                    && _module != MapModule.Market
                    && _module != MapModule.Road;
            if (_blocker != null)
                _blocker.gameObject.SetActive(IsOpen && _expanded);
            for (var i = 0; i < _choices.Length; i++)
            {
                var label = _choices[i] != null ? _choices[i].GetComponentInChildren<Text>(true) : null;
                if (label != null)
                    label.text = _titles[i] ?? string.Empty;
            }
        }

        void PlaceChrome(MapCommandLayout.Metrics metrics, float scale, float touchPixels, float screenWidth, float screenHeight)
        {
            var canvasTouch = MapManagementLayout.CanvasTouch(screenWidth, screenHeight, scale);
            var pad = Mathf.Max(8f, metrics.Gap / scale);
            var close = (RectTransform)_close.transform;
            close.anchorMin = new Vector2(1f, 1f);
            close.anchorMax = new Vector2(1f, 1f);
            close.pivot = new Vector2(1f, 1f);
            close.sizeDelta = new Vector2(canvasTouch, canvasTouch);
            close.anchoredPosition = new Vector2(-pad, -pad);

            var expand = (RectTransform)_expand.transform;
            expand.anchorMin = new Vector2(1f, 1f);
            expand.anchorMax = new Vector2(1f, 1f);
            expand.pivot = new Vector2(1f, 1f);
            expand.sizeDelta = new Vector2(canvasTouch, canvasTouch);
            expand.anchoredPosition = new Vector2(-pad - canvasTouch - pad, -pad);
            PlaceExpandIcon(canvasTouch);
            PlaceCloseIcon(canvasTouch);

            var chip = (RectTransform)_chip.transform;
            chip.anchorMin = new Vector2(0f, 1f);
            chip.anchorMax = new Vector2(0f, 1f);
            chip.pivot = new Vector2(0f, 1f);
            var chipWidth = Mathf.Max(canvasTouch, MapCommandLayout.TextWidth(OpsContent.LongestTitle, metrics.CanvasTitle) / scale + pad * 2f);
            var inner = _panel.sizeDelta.x - pad * 4f - canvasTouch * 2f;
            if (inner >= canvasTouch)
                chipWidth = Mathf.Min(chipWidth, inner);
            else
                chipWidth = canvasTouch;
            chip.sizeDelta = new Vector2(chipWidth, canvasTouch);
            chip.anchoredPosition = new Vector2(pad, -pad);

            if (_title != null)
            {
                var title = _title.rectTransform;
                title.anchorMin = new Vector2(0f, 1f);
                title.anchorMax = new Vector2(1f, 1f);
                title.pivot = new Vector2(0f, 1f);
                title.anchoredPosition = new Vector2(pad, -pad - canvasTouch - 4f);
                title.sizeDelta = new Vector2(-pad * 2f, metrics.CanvasTitle * 1.4f);
                _title.fontSize = metrics.CanvasTitle;
            }

            if (_body != null)
            {
                var body = _body.rectTransform;
                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.one;
                body.offsetMin = new Vector2(pad, pad);
                body.offsetMax = new Vector2(-pad, -(pad + canvasTouch + metrics.CanvasTitle * 1.6f + 8f));
                _body.fontSize = metrics.CanvasFont;
                _body.alignment = TextAnchor.UpperLeft;
            }

            if (_chipLabel != null)
                _chipLabel.fontSize = Mathf.Max(12, metrics.CanvasFont);

            if (_menu != null)
            {
                var row = canvasTouch;
                _menu.anchorMin = new Vector2(0f, 1f);
                _menu.anchorMax = new Vector2(0f, 1f);
                _menu.pivot = new Vector2(0f, 1f);
                _menu.sizeDelta = new Vector2(chipWidth, row * _choices.Length);
                _menu.anchoredPosition = new Vector2(pad, -pad - canvasTouch);
                for (var i = 0; i < _choices.Length; i++)
                {
                    var choice = (RectTransform)_choices[i].transform;
                    choice.anchorMin = new Vector2(0f, 1f);
                    choice.anchorMax = new Vector2(1f, 1f);
                    choice.pivot = new Vector2(0.5f, 1f);
                    choice.sizeDelta = new Vector2(0f, row);
                    choice.anchoredPosition = new Vector2(0f, -row * i);
                    var label = _choices[i].GetComponentInChildren<Text>(true);
                    if (label != null)
                    {
                        var labelRect = label.rectTransform;
                        labelRect.anchorMin = Vector2.zero;
                        labelRect.anchorMax = Vector2.one;
                        labelRect.offsetMin = new Vector2(pad, 0f);
                        labelRect.offsetMax = new Vector2(-pad, 0f);
                        label.alignment = TextAnchor.MiddleLeft;
                        label.horizontalOverflow = HorizontalWrapMode.Overflow;
                        label.verticalOverflow = VerticalWrapMode.Truncate;
                        label.fontSize = Mathf.Max(12, metrics.CanvasFont);
                    }
                }
            }
        }

        void PlaceFleet(MapCommandLayout.Metrics metrics, float scale, float touchPixels)
        {
            if (_fleet == null || _panel == null || _module != MapModule.Fleet || !IsOpen)
                return;

            var canvasTouch = MapManagementLayout.CanvasTouch(_placedWidth, _placedHeight, scale);
            var pad = Mathf.Max(8f, metrics.Gap / scale);
            var top = pad + canvasTouch + 8f;
            _fleet.Place(pad, top, metrics.CanvasFont, metrics.CanvasTitle, canvasTouch, scale, _expanded);
        }

        void PlaceJobs(MapCommandLayout.Metrics metrics, float scale, float touchPixels)
        {
            if (_jobs == null || _panel == null || _module != MapModule.Jobs || !IsOpen)
                return;

            var canvasTouch = MapManagementLayout.CanvasTouch(_placedWidth, _placedHeight, scale);
            var pad = Mathf.Max(8f, metrics.Gap / scale);
            var top = pad + canvasTouch + 8f;
            _jobs.Place(pad, top, metrics.CanvasFont, metrics.CanvasTitle, canvasTouch, scale, _expanded);
        }

        void PlaceWarehouses(MapCommandLayout.Metrics metrics, float scale, float touchPixels)
        {
            if (_warehouses == null || _panel == null || _module != MapModule.Warehouses || !IsOpen)
                return;

            var canvasTouch = MapManagementLayout.CanvasTouch(_placedWidth, _placedHeight, scale);
            var pad = Mathf.Max(8f, metrics.Gap / scale);
            var top = pad + canvasTouch + 8f;
            _warehouses.Place(pad, top, metrics.CanvasFont, metrics.CanvasTitle, canvasTouch, scale, _expanded);
        }

        void PlaceMarket(MapCommandLayout.Metrics metrics, float scale, float touchPixels)
        {
            if (_market == null || _panel == null || _module != MapModule.Market || !IsOpen)
                return;

            var canvasTouch = MapManagementLayout.CanvasTouch(_placedWidth, _placedHeight, scale);
            var pad = Mathf.Max(8f, metrics.Gap / scale);
            var top = pad + canvasTouch + 8f;
            _market.Place(pad, top, metrics.CanvasFont, metrics.CanvasTitle, canvasTouch, scale, _expanded);
        }

        void PlaceRoad(MapCommandLayout.Metrics metrics, float scale, float touchPixels)
        {
            if (_road == null || _panel == null || _module != MapModule.Road || !IsOpen)
                return;

            var canvasTouch = MapManagementLayout.CanvasTouch(_placedWidth, _placedHeight, scale);
            var pad = Mathf.Max(8f, metrics.Gap / scale);
            var top = pad + canvasTouch + 8f;
            _road.Place(pad, top, metrics.CanvasFont, metrics.CanvasTitle, canvasTouch, scale, _expanded);
        }

        void CloseMenu()
        {
            _menuOpen = false;
            if (_menu != null)
                _menu.gameObject.SetActive(false);
        }

        static RectTransform Graphic(string name, Transform parent, Color color, bool raycast)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return (RectTransform)item.transform;
        }

        void RaiseChrome()
        {
            if (_chip != null)
                _chip.transform.SetAsLastSibling();
            if (_expand != null)
                _expand.transform.SetAsLastSibling();
            if (_close != null)
                _close.transform.SetAsLastSibling();
            if (_menu != null)
                _menu.transform.SetAsLastSibling();
        }

        void PlaceCloseIcon(float canvasTouch)
        {
            if (_closeIcon == null)
                return;

            var icon = (RectTransform)_closeIcon.transform;
            var size = canvasTouch * 0.42f;
            icon.anchorMin = new Vector2(0.5f, 0.5f);
            icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
            icon.sizeDelta = new Vector2(size, size);
        }

        void PlaceExpandIcon(float canvasTouch)
        {
            if (_expandIcon == null)
                return;

            var icon = (RectTransform)_expandIcon.transform;
            var size = canvasTouch * 0.42f;
            icon.anchorMin = new Vector2(0.5f, 0.5f);
            icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
            icon.sizeDelta = new Vector2(size, size);
        }

        static Button IconButton(RectTransform parent, string name, out Image icon, out Text access)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            image.color = Raised;
            image.raycastTarget = true;
            var button = item.GetComponent<Button>();
            button.targetGraphic = image;
            var glyph = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glyph.hideFlags = HideFlags.DontSave;
            glyph.transform.SetParent(item.transform, false);
            icon = glyph.GetComponent<Image>();
            icon.sprite = MapPanelIcons.Get(MapPanelIcons.Expand);
            icon.color = MapHudPalette.Accent;
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            access = Label(item.transform, "Access", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), FontStyle.Bold, MapHudPalette.Text);
            access.enabled = false;
            access.raycastTarget = false;
            return button;
        }

        static Button Action(RectTransform parent, string name, Font font, out Text label)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            image.color = Raised;
            image.raycastTarget = true;
            var button = item.GetComponent<Button>();
            button.targetGraphic = image;
            label = Label(item.transform, "Label", font, FontStyle.Bold, MapHudPalette.Text);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return button;
        }

        static Text Label(Transform parent, string name, Font font, FontStyle style, Color color)
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
            return text;
        }

        static void Stroke(RectTransform panel)
        {
            var item = new GameObject("Stroke", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(panel, false);
            var image = item.GetComponent<Image>();
            image.color = MapHudPalette.Rule;
            image.raycastTarget = false;
            var rect = (RectTransform)item.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(2f, 0f);
            rect.anchoredPosition = Vector2.zero;
        }

        sealed class CopyBind
        {
            readonly LocalizedString _source;
            readonly System.Action<string> _extra;
            bool _subscribed;

            public CopyBind(string table, string key, System.Action<string> extra)
            {
                _source = new LocalizedString(table, key);
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
                _extra?.Invoke(value ?? string.Empty);
            }
        }
    }
}
