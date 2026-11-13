using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// Fleet list/detail inside MapManagementPanel. No purchases, no fake runtime vehicles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapFleetView : MonoBehaviour
    {
        public const int MaxRows = 20;
        static readonly Color Row = MapPremiumTheme.Alpha(MapPremiumTheme.Card, 0.72f);
        static readonly Color RowSelected = MapPremiumTheme.Alpha(MapPremiumTheme.Selected, 0.94f);

        RectTransform _status;
        Text _statusText;
        RectTransform _list;
        ScrollRect _scroll;
        RectTransform _content;
        RectTransform _detail;
        Text _detailName;
        Text _detailBody;
        Button _focus;
        Text _focusLabel;
        Button _back;
        Text _backLabel;
        readonly Button[] _rows = new Button[MaxRows];
        readonly Text[] _rowTitle = new Text[MaxRows];
        readonly Text[] _rowMeta = new Text[MaxRows];
        readonly string[] _rowIds = new string[MaxRows];
        bool _built;
        bool _placed;
        bool _detailPage;
        bool _expanded;
        float _pad;
        float _top;
        float _font;
        float _title;
        float _touch;
        float _scale = 1f;
        MapFleetSnapshot _shown;

        public MapFleetStatus Status => MapFleetStore.Current.Status;
        public string SelectedId => MapFleetStore.Current.SelectedId;
        public string StatusText => _statusText != null ? _statusText.text : string.Empty;
        public string DetailName => _detailName != null && _detail.gameObject.activeSelf ? _detailName.text : string.Empty;
        public string DetailBody => _detailBody != null && _detail.gameObject.activeSelf ? _detailBody.text : string.Empty;
        public bool FocusActive => _focus != null && _focus.gameObject.activeInHierarchy && _focus.interactable;
        public bool DetailVisible => _detail != null && _detail.gameObject.activeInHierarchy;
        public bool ListVisible => _list != null && _list.gameObject.activeSelf;

        public string RowId(int index)
        {
            if (index < 0 || index >= _rowIds.Length)
                return "";
            return _rowIds[index] ?? "";
        }

        public string RowTitle(int index)
        {
            if (index < 0 || index >= _rowTitle.Length || _rowTitle[index] == null || !_rows[index].gameObject.activeSelf)
                return "";
            return _rowTitle[index].text ?? "";
        }

        public string RowMetaText(int index)
        {
            if (index < 0 || index >= _rowMeta.Length || _rowMeta[index] == null || !_rows[index].gameObject.activeSelf)
                return "";
            return _rowMeta[index].text ?? "";
        }
        public int ActiveRowCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _rows.Length; i++)
                {
                    if (_rows[i] != null && _rows[i].gameObject.activeSelf)
                        count++;
                }

                return count;
            }
        }

        public static MapFleetView Attach(RectTransform panel)
        {
            if (panel == null)
                return null;

            var existing = panel.GetComponentInChildren<MapFleetView>(true);
            if (existing != null)
                return existing;

            var item = new GameObject("ManagementFleet", typeof(RectTransform));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(panel, false);
            var view = item.AddComponent<MapFleetView>();
            view.BuildIfNeeded();
            return view;
        }

        void Awake()
        {
            BuildIfNeeded();
        }

        void OnEnable()
        {
            MapFleetStateRead.Applied -= OnFleetApplied;
            MapFleetStateRead.Applied += OnFleetApplied;
            MapFleetStateRead.TryRefresh();
            Refresh();
        }

        void OnDisable()
        {
            MapFleetStateRead.Applied -= OnFleetApplied;
        }

        void OnFleetApplied()
        {
            Refresh();
        }

        public void Refresh()
        {
            BuildIfNeeded();
            Present(MapFleetStore.Current);
            if (_placed)
                Layout();
        }

        public void Select(string assetId)
        {
            MapFleetStore.Select(assetId);
            if (!string.IsNullOrEmpty(assetId))
                _detailPage = true;
            Refresh();
        }

        public void ShowList()
        {
            _detailPage = false;
            Refresh();
        }

        public bool TryFocusSelected()
        {
            var snapshot = MapFleetStore.Current;
            if (!snapshot.TryGet(snapshot.SelectedId, out var asset))
                return false;

            var shell = GetComponentInParent<MapCommandShell>();
            var rig = shell != null ? FindRig(shell) : null;
            if (rig == null)
                return false;

            if (asset.HasGps)
            {
                rig.FocusWorldPoint(MapProjection.Project(asset.Latitude, asset.Longitude));
                return true;
            }

            if (asset.HasKnownCity && MapCities.TryGet(asset.LocationCityId, out var city))
            {
                if (shell != null)
                    shell.ShowCity(city.Id);
                rig.FocusWorldPoint(MapProjection.Project(city.Latitude, city.Longitude));
                return true;
            }

            return false;
        }

        public Rect RowHitPixels(int index)
        {
            if (index < 0 || index >= _rows.Length || _rows[index] == null)
                return default;

            var panel = GetComponentInParent<MapManagementPanel>();
            var scale = panel != null ? panel.PlacedCanvasScale : _scale;
            return MapManagementLayout.HitRect((RectTransform)_rows[index].transform, scale);
        }

        public Rect FocusHitPixels()
        {
            var panel = GetComponentInParent<MapManagementPanel>();
            var scale = panel != null ? panel.PlacedCanvasScale : _scale;
            return MapManagementLayout.HitRect((RectTransform)_focus.transform, scale);
        }

        public void Place(float pad, float top, float font, float title, float touch, float scale, bool expanded)
        {
            BuildIfNeeded();
            _pad = pad;
            _top = top;
            _font = font;
            _title = title;
            _touch = touch;
            _scale = scale < 0.0001f ? 1f : scale;
            _expanded = expanded;
            _placed = true;
            Present(MapFleetStore.Current);
            Layout();
        }

        void Layout()
        {
            if (!_placed || _list == null || _status == null)
                return;

            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = new Vector2(_pad, _pad);
            root.offsetMax = new Vector2(-_pad, -_top);

            var statusH = Mathf.Max(_touch * 0.55f, _font * 1.6f);
            _status.anchorMin = new Vector2(0f, 1f);
            _status.anchorMax = new Vector2(1f, 1f);
            _status.pivot = new Vector2(0.5f, 1f);
            _status.anchoredPosition = Vector2.zero;
            _status.sizeDelta = new Vector2(0f, statusH);
            _statusText.fontSize = Mathf.Max(11, Mathf.RoundToInt(_font));
            _statusText.alignment = TextAnchor.UpperLeft;

            var split = _expanded && root.rect.width >= 560f;
            var stackedDetail = _expanded && _detailPage && !split;
            var ready = _shown.Status == MapFleetStatus.Ready && _shown.Assets != null && _shown.Assets.Length > 0;
            var showList = !stackedDetail && ready;
            var showDetail = ready
                && _expanded
                && !string.IsNullOrEmpty(_shown.SelectedId)
                && (split || stackedDetail);

            _list.gameObject.SetActive(showList);
            _detail.gameObject.SetActive(showDetail);
            _back.gameObject.SetActive(stackedDetail && showDetail);
            if (showList)
            {
                _list.anchorMin = new Vector2(0f, 0f);
                _list.anchorMax = showDetail && split ? new Vector2(0.42f, 1f) : new Vector2(1f, 1f);
                _list.offsetMin = Vector2.zero;
                _list.offsetMax = new Vector2(showDetail && split ? -6f : 0f, -statusH - 6f);
            }

            if (showDetail)
            {
                _detail.anchorMin = split ? new Vector2(0.42f, 0f) : new Vector2(0f, 0f);
                _detail.anchorMax = Vector2.one;
                _detail.offsetMin = new Vector2(split ? 6f : 0f, stackedDetail ? _touch + 8f : 0f);
                _detail.offsetMax = new Vector2(0f, -statusH - 6f);
            }

            if (stackedDetail && showDetail)
            {
                var back = (RectTransform)_back.transform;
                back.anchorMin = new Vector2(0f, 0f);
                back.anchorMax = new Vector2(0f, 0f);
                back.pivot = new Vector2(0f, 0f);
                back.sizeDelta = new Vector2(Mathf.Max(_touch, 120f), _touch);
                back.anchoredPosition = Vector2.zero;
            }

            var focus = (RectTransform)_focus.transform;
            focus.anchorMin = new Vector2(0f, 0f);
            focus.anchorMax = new Vector2(1f, 0f);
            focus.pivot = new Vector2(0.5f, 0f);
            focus.sizeDelta = new Vector2(0f, _touch);
            focus.anchoredPosition = Vector2.zero;
            _focusLabel.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
            _detailName.fontSize = Mathf.Max(13, Mathf.RoundToInt(_title));
            _detailBody.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
            _backLabel.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));

            var rowH = Mathf.Max(_touch, _font * 2.35f + 18f);
            var used = 0;
            for (var i = 0; i < _rows.Length; i++)
            {
                if (_rows[i] == null || !_rows[i].gameObject.activeSelf)
                    continue;

                var row = (RectTransform)_rows[i].transform;
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(0f, rowH);
                row.anchoredPosition = new Vector2(0f, -used * (rowH + 6f));
                _rowTitle[i].fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
                _rowMeta[i].fontSize = Mathf.Max(11, Mathf.RoundToInt(_font * 0.92f));
                MapManagementLayout.ClipLabel(_rowTitle[i]);
                MapManagementLayout.ClipLabel(_rowMeta[i]);
                used++;
            }

            _content.sizeDelta = new Vector2(0f, used * (rowH + 6f));
            _scroll.vertical = showList && _content.sizeDelta.y > _list.rect.height + 1f;
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

            _statusText = Label(root, "ManagementFleetStatus", font, FontStyle.Normal, MapHudPalette.TextMuted);
            _status = _statusText.rectTransform;
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusText.verticalOverflow = VerticalWrapMode.Truncate;

            _list = Host("ManagementFleetList", root);
            var viewport = Host("ManagementFleetViewport", _list);
            var image = viewport.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            _content = Host("ManagementFleetContent", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _scroll = _list.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 24f;
            for (var i = 0; i < MaxRows; i++)
            {
                var slot = i;
                _rows[i] = Action(_content, "ManagementFleetRow" + i, font, out _rowTitle[i]);
                _rowMeta[i] = Label(_rows[i].transform, "Meta", font, FontStyle.Normal, MapHudPalette.TextMuted);
                _rowTitle[i].alignment = TextAnchor.UpperLeft;
                _rowTitle[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                _rowMeta[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                _rowTitle[i].verticalOverflow = VerticalWrapMode.Truncate;
                _rowMeta[i].verticalOverflow = VerticalWrapMode.Truncate;
                _rowMeta[i].alignment = TextAnchor.MiddleLeft;
                Inset(_rowTitle[i].rectTransform, 10f, 24f, 6f, 8f);
                Inset(_rowMeta[i].rectTransform, 10f, 6f, 26f, 8f);
                _rows[i].onClick.AddListener(() => Select(_rowIds[slot]));
                _rows[i].gameObject.SetActive(false);
            }

            _detail = Host("ManagementFleetDetail", root);
            var detailImage = _detail.gameObject.AddComponent<Image>();
            detailImage.color = new Color(0.035f, 0.067f, 0.110f, 0.08f);
            detailImage.raycastTarget = true;
            _detailName = Label(_detail, "ManagementFleetDetailName", font, FontStyle.Bold, MapHudPalette.Text);
            _detailBody = Label(_detail, "ManagementFleetDetailBody", font, FontStyle.Normal, MapHudPalette.TextMuted);
            _detailName.alignment = TextAnchor.UpperLeft;
            _detailBody.alignment = TextAnchor.UpperLeft;
            Inset(_detailName.rectTransform, 10f, 0f, 8f, 10f);
            _detailName.rectTransform.anchorMin = new Vector2(0f, 1f);
            _detailName.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailName.rectTransform.pivot = new Vector2(0f, 1f);
            _detailName.rectTransform.sizeDelta = new Vector2(-20f, 32f);
            Inset(_detailBody.rectTransform, 10f, 52f, 52f, 10f);
            _focus = Action(_detail, "ManagementFleetFocus", font, out _focusLabel);
            _focus.onClick.AddListener(() => TryFocusSelected());
            _back = Action(root, "ManagementFleetBack", font, out _backLabel);
            _back.onClick.AddListener(ShowList);
            _back.gameObject.SetActive(false);
            _detail.gameObject.SetActive(false);
        }

        void Present(MapFleetSnapshot snapshot)
        {
            _shown = snapshot;
            if (_statusText != null)
                _statusText.text = StatusCopy(snapshot);

            var assets = snapshot.Assets;
            var count = assets != null ? assets.Length : 0;
            var shown = 0;
            for (var i = 0; i < MaxRows; i++)
            {
                MapFleetAsset asset = default;
                var has = NextAsset(snapshot, ref shown, out asset);
                if (!has)
                {
                    _rows[i].gameObject.SetActive(false);
                    _rowIds[i] = "";
                    continue;
                }

                _rows[i].gameObject.SetActive(true);
                _rowIds[i] = asset.AssetId;
                _rowTitle[i].text = KindCopy(asset.Kind);
                _rowMeta[i].text = RowMeta(asset);
                var image = _rows[i].GetComponent<Image>();
                image.color = asset.AssetId == snapshot.SelectedId ? RowSelected : Row;
            }

            var ready = snapshot.Status == MapFleetStatus.Ready && count > 0;
            if (_list != null)
                _list.gameObject.SetActive(ready && !(_expanded && _detailPage));

            if (!ready)
            {
                if (_detail != null)
                    _detail.gameObject.SetActive(false);
                if (_back != null)
                    _back.gameObject.SetActive(false);
                return;
            }

            MapFleetAsset selected;
            var hasSelected = snapshot.TryGet(snapshot.SelectedId, out selected);
            if (!hasSelected)
            {
                if (_detail != null)
                    _detail.gameObject.SetActive(false);
                if (_focus != null)
                    _focus.gameObject.SetActive(false);
                return;
            }

            _detailName.text = KindCopy(selected.Kind);
            _detailBody.text = DetailCopy(selected);
            var canFocus = selected.HasGps || selected.HasKnownCity;
            _focus.gameObject.SetActive(canFocus);
            _focus.interactable = canFocus;
            if (canFocus)
                _focusLabel.text = selected.HasGps ? Line(OpsContent.FleetShowGps) : Line(OpsContent.FleetShowCity);
            _backLabel.text = Line(OpsContent.FleetBack);
        }

        static bool NextAsset(MapFleetSnapshot snapshot, ref int index, out MapFleetAsset asset)
        {
            var seen = 0;
            for (var i = 0; i < snapshot.Assets.Length; i++)
            {
                if (seen == index)
                {
                    index++;
                    asset = snapshot.Assets[i];
                    return true;
                }

                seen++;
            }

            asset = default;
            return false;
        }

        string StatusCopy(MapFleetSnapshot snapshot)
        {
            switch (snapshot.Status)
            {
                case MapFleetStatus.Loading:
                    return Line(OpsContent.FleetLoading);
                case MapFleetStatus.Error:
                    return Line(OpsContent.FleetError);
                case MapFleetStatus.Empty:
                    return Line(OpsContent.FleetEmpty);
                case MapFleetStatus.Ready:
                    return Line(OpsContent.FleetReady);
                default:
                    return Line(OpsContent.BodyFleet);
            }
        }

        string RowMeta(MapFleetAsset asset)
        {
            return StateCopy(asset);
        }

        string DetailCopy(MapFleetAsset asset)
        {
            var text = KindCopy(asset.Kind) + "\n" + StateCopy(asset);
            if (asset.HasAssignment)
                text += "\n" + Line(OpsContent.FleetAssigned);
            else
                text += "\n" + Line(OpsContent.FleetUnassigned);
            if (asset.HasCapacity)
                text += "\n" + Line(OpsContent.FleetCapacity) + " " + asset.Capacity;
            if (asset.HasGps)
                text += "\n" + Line(OpsContent.FleetLocationGps);
            else if (asset.HasKnownCity)
                text += "\n" + Line(OpsContent.FleetLocationCity);
            else
                text += "\n" + Line(OpsContent.FleetLocationUnknown);
            return text;
        }

        string KindCopy(MapFleetKind kind)
        {
            switch (kind)
            {
                case MapFleetKind.Truck:
                    return Line(OpsContent.FleetKindTruck);
                case MapFleetKind.Driver:
                    return Line(OpsContent.FleetKindDriver);
                case MapFleetKind.Trailer:
                    return Line(OpsContent.FleetKindTrailer);
                default:
                    return Line(OpsContent.FleetUnknown);
            }
        }

        string StateCopy(MapFleetAsset asset)
        {
            if (asset.Status == MapFleetAssetStatus.OutOfService)
                return Line(OpsContent.FleetOutOfService);
            if (asset.Available)
                return Line(OpsContent.FleetAvailable);
            if (asset.HasAssignment)
                return Line(OpsContent.FleetBusy);
            return Line(OpsContent.FleetUnknown);
        }

        static string Line(string key)
        {
            var database = LocalizationSettingsDatabase();
            if (database == null)
                return key;

            var locale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
            var table = locale != null
                ? database.GetTable(OpsContent.Table, locale) as UnityEngine.Localization.Tables.StringTable
                : null;
            var entry = table != null ? table.GetEntry(key) : null;
            var text = entry != null ? entry.GetLocalizedString() : "";
            return string.IsNullOrWhiteSpace(text) ? key : text;
        }

        static UnityEngine.Localization.Settings.LocalizedStringDatabase LocalizationSettingsDatabase()
        {
            return UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase;
        }

        static MapCameraRig FindRig(MapCommandShell shell)
        {
            var scene = shell.gameObject.scene;
            if (!scene.IsValid())
                return null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var rig = roots[i].GetComponentInChildren<MapCameraRig>(true);
                if (rig != null)
                    return rig;
            }

            return null;
        }

        static RectTransform Host(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            return (RectTransform)item.transform;
        }

        static Button Action(Transform parent, string name, Font font, out Text label)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            MapPremiumUi.StyleSurface(image, MapPremiumSurface.Card, true);
            image.color = Row;
            var button = item.GetComponent<Button>();
            button.targetGraphic = image;
            label = Label(item.transform, "Label", font, FontStyle.Bold, MapHudPalette.Text);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            Inset(label.rectTransform, 8f, 0f, 0f, 8f);
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
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        static void Inset(RectTransform rect, float left, float bottom, float top, float right)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
