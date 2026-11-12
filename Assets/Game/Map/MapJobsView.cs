using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    [DisallowMultipleComponent]
    public sealed class MapJobsView : MonoBehaviour
    {
        public const int MaxRows = 20;
        static readonly Color Row = new Color(0.045f, 0.086f, 0.125f, 0.22f);
        static readonly Color RowSelected = new Color(0.090f, 0.210f, 0.255f, 0.55f);
        static readonly Color ChipOn = new Color(0.090f, 0.210f, 0.255f, 0.62f);
        static readonly Color ChipOff = new Color(0.045f, 0.086f, 0.125f, 0.18f);
        static readonly Color Ink = MapHudPalette.Text;
        static readonly Color InkMuted = MapHudPalette.TextMuted;

        RectTransform _status;
        Text _statusText;
        Text _fixtureMark;
        RectTransform _cats;
        readonly Button[] _catButtons = new Button[3];
        readonly Text[] _catLabels = new Text[3];
        RectTransform _list;
        ScrollRect _scroll;
        RectTransform _content;
        RectTransform _detail;
        Text _detailName;
        Text _detailStatus;
        Text _detailCargoLabel;
        Text _detailCargoValue;
        Text _detailRewardLabel;
        Text _detailRewardValue;
        Text _detailBody;
        Button _origin;
        Text _originLabel;
        Button _destination;
        Text _destinationLabel;
        Button _back;
        Text _backLabel;
        Button _detailClose;
        Image _detailCloseIcon;
        Button _accept;
        Text _acceptLabel;
        Button _startHold;
        Text _startHoldLabel;
        RectTransform _equip;
        Text _equipTitle;
        readonly Text[] _equipKind = new Text[3];
        readonly Text[] _equipMeta = new Text[3];
        readonly Button[] _equipSelect = new Button[3];
        readonly Text[] _equipSelectLabel = new Text[3];
        readonly MapFleetKind[] _equipKinds = new MapFleetKind[3];
        readonly string[] _equipIds = new string[3];
        int _equipCount;
        readonly Button[] _rows = new Button[MaxRows];
        readonly Text[] _rowTitle = new Text[MaxRows];
        readonly Text[] _rowMeta = new Text[MaxRows];
        readonly string[] _rowIds = new string[MaxRows];
        bool _built;
        bool _watchingLocalization;
        AsyncOperationHandle _localizationInit;
        bool _detailOpen;
        bool _placed;
        bool _expanded;
        float _scale = 1f;
        float _pad;
        float _top;
        float _font;
        float _title;
        float _touch;
        MapJobsSnapshot _shown;

        public MapJobsStatus Status => MapJobsStore.Current.Status;
        public MapJobCategory Category => MapJobsStore.Current.Category;
        public string SelectedId => MapJobsStore.Current.SelectedId;
        public string StatusText => _statusText != null ? _statusText.text : string.Empty;
        public string DetailName => _detailName != null && _detail.gameObject.activeInHierarchy ? _detailName.text : string.Empty;
        public string DetailBody
        {
            get
            {
                if (_detail == null || !_detail.gameObject.activeInHierarchy)
                    return string.Empty;
                return JoinDetail(
                    Field(_detailStatus),
                    Field(_detailCargoLabel),
                    Field(_detailCargoValue),
                    Field(_detailRewardLabel),
                    Field(_detailRewardValue),
                    Field(_detailBody),
                    Field(_equipTitle));
            }
        }
        public bool OriginActive => _origin != null && _origin.gameObject.activeInHierarchy && _origin.interactable;
        public bool DestinationActive => _destination != null && _destination.gameObject.activeInHierarchy && _destination.interactable;
        public bool DetailVisible => _detail != null && _detail.gameObject.activeInHierarchy;
        public bool DetailCloseVisible => _detailClose != null && _detailClose.gameObject.activeInHierarchy;
        public bool AcceptVisible => _accept != null && _accept.gameObject.activeInHierarchy;
        public bool AcceptInteractable => _accept != null && _accept.gameObject.activeInHierarchy && _accept.interactable;
        public string AcceptText => _acceptLabel != null && AcceptVisible ? _acceptLabel.text : string.Empty;
        public bool EquipmentVisible => _equip != null && _equip.gameObject.activeInHierarchy;
        public int EquipmentRowCount => EquipmentVisible ? _equipCount : 0;
        public bool StartDeliveryHoldVisible =>
            _startHold != null && _startHold.gameObject.activeInHierarchy && _startHold.interactable;

        public string EquipmentKindText(int index)
        {
            return EquipmentVisible && index >= 0 && index < _equipCount && _equipKind[index] != null
                ? _equipKind[index].text
                : string.Empty;
        }

        public string EquipmentMetaText(int index)
        {
            return EquipmentVisible && index >= 0 && index < _equipCount && _equipMeta[index] != null
                ? _equipMeta[index].text
                : string.Empty;
        }

        public bool EquipmentSelectInteractable(int index)
        {
            return EquipmentVisible && index >= 0 && index < _equipCount && _equipSelect[index] != null
                && _equipSelect[index].gameObject.activeInHierarchy
                && _equipSelect[index].interactable;
        }

        public bool TrySelectEquipment(int index)
        {
            if (!EquipmentVisible || index < 0 || index >= _equipCount)
                return false;
            var snapshot = MapJobsStore.Current;
            if (!snapshot.TryGet(snapshot.SelectedId, out var item))
                return false;
            if (!MapJobsEquipmentSelection.TrySelect(item, MapFleetStore.Current, _equipKinds[index], _equipIds[index]))
                return false;
            Refresh();
            return true;
        }

        public bool ListVisible => _list != null && _list.gameObject.activeInHierarchy;
        public bool FixtureVisible => StatusText.IndexOf("FIXTURE", System.StringComparison.Ordinal) >= 0;
        public bool ScrollEnabled => _scroll != null && _scroll.vertical;
        public int RouteGraphicCount => GetComponentsInChildren<LineRenderer>(true).Length;
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

        public string RowTitleText(int index)
        {
            return index >= 0 && index < _rowTitle.Length && _rowTitle[index] != null && _rows[index] != null
                && _rows[index].gameObject.activeSelf
                ? _rowTitle[index].text
                : string.Empty;
        }

        public string RowMetaText(int index)
        {
            return index >= 0 && index < _rowMeta.Length && _rowMeta[index] != null && _rows[index] != null
                && _rows[index].gameObject.activeSelf
                ? _rowMeta[index].text
                : string.Empty;
        }

        public bool RowTitleOverlapsMeta(int index)
        {
            if (index < 0 || index >= _rows.Length || _rows[index] == null || !_rows[index].gameObject.activeSelf)
                return false;
            return VerticalOverlap(_rowTitle[index].rectTransform, _rowMeta[index].rectTransform);
        }

        public bool DetailBlocksOverlap()
        {
            if (_detail == null || !_detail.gameObject.activeInHierarchy)
                return false;
            Text[] chain =
            {
                _detailName,
                _detailStatus,
                _detailCargoLabel,
                _detailCargoValue,
                _detailRewardLabel,
                _detailRewardValue,
                _detailBody,
                _equipTitle,
                _equipKind[0],
                _equipMeta[0],
                _equipKind[1],
                _equipMeta[1],
                _equipKind[2],
                _equipMeta[2]
            };
            for (var i = 0; i < chain.Length; i++)
            {
                if (!VisibleText(chain[i]))
                    continue;
                for (var j = i + 1; j < chain.Length; j++)
                {
                    if (!VisibleText(chain[j]))
                        continue;
                    if (VerticalOverlap(chain[i].rectTransform, chain[j].rectTransform))
                        return true;
                }
            }

            return false;
        }

        public static MapJobsView Attach(RectTransform panel)
        {
            if (panel == null)
                return null;
            var existing = panel.GetComponentInChildren<MapJobsView>(true);
            if (existing != null)
                return existing;
            var item = new GameObject("ManagementJobs", typeof(RectTransform));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(panel, false);
            var view = item.AddComponent<MapJobsView>();
            view.BuildIfNeeded();
            return view;
        }

        void Awake()
        {
            BuildIfNeeded();
        }

        void OnEnable()
        {
            MapJobsContractsRead.Applied -= OnJobsApplied;
            MapJobsContractsRead.Applied += OnJobsApplied;
            MapJobsAccept.Changed -= OnJobsApplied;
            MapJobsAccept.Changed += OnJobsApplied;
            MapFleetStateRead.Applied -= OnJobsApplied;
            MapFleetStateRead.Applied += OnJobsApplied;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            WatchLocalization();
            MapJobsContractsRead.TryRefresh();
            Refresh();
        }

        void OnDisable()
        {
            MapJobsContractsRead.Applied -= OnJobsApplied;
            MapJobsAccept.Changed -= OnJobsApplied;
            MapFleetStateRead.Applied -= OnJobsApplied;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            StopWatchingLocalization();
        }

        void OnJobsApplied()
        {
            if (this == null || !isActiveAndEnabled)
                return;
            Refresh();
        }

        public void Refresh()
        {
            BuildIfNeeded();
            Present(MapJobsStore.Current);
            if (_placed)
                Layout();
        }

        public void Select(string contractId)
        {
            MapJobsStore.Select(contractId);
            if (!string.IsNullOrEmpty(contractId))
                _detailOpen = true;
            Refresh();
        }

        public void SetCategory(MapJobCategory category)
        {
            MapJobsStore.SetCategory(category);
            _detailOpen = false;
            Refresh();
        }

        public void ShowList()
        {
            _detailOpen = false;
            Refresh();
        }

        public void CloseDetail()
        {
            _detailOpen = false;
            ClearDetail();
            Refresh();
        }

        public bool TryAccept()
        {
            var snapshot = MapJobsStore.Current;
            if (!_detailOpen || string.IsNullOrEmpty(snapshot.SelectedId))
                return false;
            return MapJobsAccept.TryBegin(snapshot.SelectedId);
        }

        public bool TryFocusOrigin()
        {
            return FocusCity(true);
        }

        public bool TryFocusDestination()
        {
            return FocusCity(false);
        }

        public Rect RowHitPixels(int index)
        {
            if (index < 0 || index >= _rows.Length || _rows[index] == null)
                return default;
            return MapManagementLayout.HitRect((RectTransform)_rows[index].transform, Scale());
        }

        public Rect CategoryHitPixels(int index)
        {
            if (index < 0 || index >= _catButtons.Length || _catButtons[index] == null)
                return default;
            return MapManagementLayout.HitRect((RectTransform)_catButtons[index].transform, Scale());
        }

        public Rect OriginHitPixels()
        {
            if (_origin == null)
                return default;
            return MapManagementLayout.HitRect((RectTransform)_origin.transform, Scale());
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
            Present(MapJobsStore.Current);
            Layout();
        }

        public void SimulateStaleRuntimeUi()
        {
            TearDownRuntimeUi();
            _built = true;
        }

        public bool RuntimeUiIntact => UiIntact();

        public int RuntimeCategoryHostCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _catButtons.Length; i++)
                {
                    if (_catButtons[i] != null)
                        count++;
                }

                return count;
            }
        }

        void Layout()
        {
            if (!UiIntact())
                return;
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = new Vector2(_pad, _pad);
            root.offsetMax = new Vector2(-_pad, -_top);

            var type = ReadType();
            _statusText.fontSize = type.Support;
            _statusText.alignment = TextAnchor.UpperLeft;
            _statusText.fontStyle = FontStyle.Normal;
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusText.verticalOverflow = VerticalWrapMode.Truncate;
            var readyRows = _shown.Status == MapJobsStatus.Ready && _shown.CountIn(_shown.Category) > 0;
            var statusH = 0f;
            if (!string.IsNullOrEmpty(_statusText.text))
            {
                statusH = Mathf.Max(_font * 1.35f, type.Support * 1.4f);
                if (_shown.Synthetic)
                    statusH = Mathf.Max(statusH, type.Support * 2.4f);
                if (!_expanded && readyRows && !string.IsNullOrEmpty(_shown.SelectedId))
                    statusH = Mathf.Max(statusH, type.Route * 2.2f);
                if (_shown.Status != MapJobsStatus.Ready)
                    statusH = Mathf.Max(statusH, type.Support * 2.0f);
            }
            _status.anchorMin = new Vector2(0f, 1f);
            _status.anchorMax = new Vector2(1f, 1f);
            _status.pivot = new Vector2(0.5f, 1f);
            _status.anchoredPosition = Vector2.zero;
            _status.sizeDelta = new Vector2(0f, statusH);
            _fixtureMark.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));

            var showCats = _expanded && _shown.Status == MapJobsStatus.Ready;
            _cats.gameObject.SetActive(showCats);
            var catH = showCats ? _touch : 0f;
            if (showCats)
            {
                _cats.anchorMin = new Vector2(0f, 1f);
                _cats.anchorMax = new Vector2(1f, 1f);
                _cats.pivot = new Vector2(0.5f, 1f);
                _cats.anchoredPosition = new Vector2(0f, -statusH - 4f);
                _cats.sizeDelta = new Vector2(0f, catH);
                var gap = 4f;
                for (var i = 0; i < 3; i++)
                {
                    var button = (RectTransform)_catButtons[i].transform;
                    button.anchorMin = new Vector2(i / 3f, 0f);
                    button.anchorMax = new Vector2((i + 1) / 3f, 1f);
                    button.offsetMin = new Vector2(i == 0 ? 0f : gap * 0.5f, 0f);
                    button.offsetMax = new Vector2(i == 2 ? 0f : -gap * 0.5f, 0f);
                    _catLabels[i].fontSize = type.Tab;
                    _catLabels[i].fontStyle = FontStyle.Bold;
                    if (_catLabels[i].rectTransform.rect.width > 8f)
                        MapManagementLayout.ClipLabel(_catLabels[i]);
                }
            }

            var split = _expanded && root.rect.width * _scale >= 1200f;
            var stacked = _expanded && _detailOpen && !split;
            var ready = _shown.Status == MapJobsStatus.Ready;
            var inCategory = ready ? _shown.CountIn(_shown.Category) : 0;
            var hasSelection = ready && !string.IsNullOrEmpty(_shown.SelectedId);
            var showList = ready && !stacked && inCategory > 0;
            var showDetail = hasSelection && _detailOpen && _expanded && (split || stacked) && inCategory > 0;
            _list.gameObject.SetActive(showList);
            _detail.gameObject.SetActive(showDetail);
            if (_detailClose != null)
                _detailClose.gameObject.SetActive(showDetail);
            _back.gameObject.SetActive(stacked && showDetail);
            var topBlock = statusH + (showCats ? catH + 8f : (statusH > 0f ? 6f : 0f));
            if (showList)
            {
                _list.anchorMin = new Vector2(0f, 0f);
                _list.anchorMax = showDetail && split ? new Vector2(0.40f, 1f) : new Vector2(1f, 1f);
                _list.offsetMin = Vector2.zero;
                _list.offsetMax = new Vector2(showDetail && split ? -8f : 0f, -topBlock);
            }

            if (showDetail)
            {
                _detail.anchorMin = split ? new Vector2(0.40f, 0f) : new Vector2(0f, 0f);
                _detail.anchorMax = Vector2.one;
                _detail.offsetMin = new Vector2(split ? 10f : 0f, stacked ? _touch + 8f : 0f);
                _detail.offsetMax = new Vector2(0f, -topBlock);
                var close = Mathf.Max(28f, _touch * 0.62f);
                if (_detailClose != null)
                {
                    var closeRect = (RectTransform)_detailClose.transform;
                    closeRect.anchorMin = new Vector2(1f, 1f);
                    closeRect.anchorMax = new Vector2(1f, 1f);
                    closeRect.pivot = new Vector2(1f, 1f);
                    closeRect.sizeDelta = new Vector2(close, close);
                    closeRect.anchoredPosition = new Vector2(-4f, -4f);
                    if (_detailCloseIcon != null)
                    {
                        var icon = (RectTransform)_detailCloseIcon.transform;
                        icon.anchorMin = new Vector2(0.5f, 0.5f);
                        icon.anchorMax = new Vector2(0.5f, 0.5f);
                        icon.pivot = new Vector2(0.5f, 0.5f);
                        icon.anchoredPosition = Vector2.zero;
                        icon.sizeDelta = new Vector2(close * 0.46f, close * 0.46f);
                    }
                }

                var routeOn = _origin.gameObject.activeSelf || _destination.gameObject.activeSelf;
                var acceptOn = _accept != null && _accept.gameObject.activeSelf;
                var extraBottom = 0f;
                if (routeOn)
                    extraBottom += _touch + 8f;
                if (acceptOn)
                    extraBottom += _touch + 8f;
                _detail.offsetMin = new Vector2(split ? 10f : 0f, (stacked ? _touch + 8f : 0f) + extraBottom);
                if (acceptOn)
                {
                    var accept = (RectTransform)_accept.transform;
                    accept.anchorMin = new Vector2(0f, 0f);
                    accept.anchorMax = new Vector2(1f, 0f);
                    accept.pivot = new Vector2(0.5f, 0f);
                    accept.sizeDelta = new Vector2(0f, _touch);
                    accept.anchoredPosition = new Vector2(0f, routeOn ? _touch + 8f : 0f);
                    _acceptLabel.fontSize = type.Tab;
                    if (_acceptLabel.rectTransform.rect.width > 8f)
                        MapManagementLayout.ClipLabel(_acceptLabel);
                }

                LayoutDetail(type, close + 8f);
            }

            if (stacked)
            {
                var back = (RectTransform)_back.transform;
                back.anchorMin = new Vector2(0f, 0f);
                back.anchorMax = new Vector2(0f, 0f);
                back.pivot = new Vector2(0f, 0f);
                back.sizeDelta = new Vector2(
                    Mathf.Min(
                        Mathf.Max(_touch * 2f, MapManagementLayout.ControlWidth(_backLabel.text, _font, _touch, 12f)),
                        Mathf.Max(_touch * 2f, root.rect.width * 0.48f)),
                    _touch);
                back.anchoredPosition = Vector2.zero;
            }

            var origin = (RectTransform)_origin.transform;
            var dest = (RectTransform)_destination.transform;
            origin.anchorMin = new Vector2(0f, 0f);
            origin.anchorMax = new Vector2(0.5f, 0f);
            origin.pivot = new Vector2(0f, 0f);
            origin.sizeDelta = new Vector2(-6f, _touch);
            origin.anchoredPosition = Vector2.zero;
            dest.anchorMin = new Vector2(0.5f, 0f);
            dest.anchorMax = new Vector2(1f, 0f);
            dest.pivot = new Vector2(1f, 0f);
            dest.sizeDelta = new Vector2(-6f, _touch);
            dest.anchoredPosition = Vector2.zero;
            _originLabel.fontSize = type.Tab;
            _destinationLabel.fontSize = type.Tab;
            if (_originLabel.rectTransform.rect.width > 8f)
                MapManagementLayout.ClipLabel(_originLabel);
            if (_destinationLabel.rectTransform.rect.width > 8f)
                MapManagementLayout.ClipLabel(_destinationLabel);
            _backLabel.fontSize = type.Tab;

            var cursor = 0f;
            for (var i = 0; i < _rows.Length; i++)
            {
                if (_rows[i] == null || !_rows[i].gameObject.activeSelf)
                    continue;
                var rowH = LayoutRow(i, type);
                var row = (RectTransform)_rows[i].transform;
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(0f, rowH);
                row.anchoredPosition = new Vector2(0f, -cursor);
                cursor += rowH + 6f;
            }

            _content.sizeDelta = new Vector2(0f, cursor);
            _scroll.vertical = showList && _content.sizeDelta.y > _list.rect.height + 1f;
            _scroll.horizontal = false;
        }

        readonly struct JobsType
        {
            public readonly int Route;
            public readonly int Tab;
            public readonly int Value;
            public readonly int Label;
            public readonly int Support;
            public readonly int Reward;

            public JobsType(int route, int tab, int value, int label, int support, int reward)
            {
                Route = route;
                Tab = tab;
                Value = value;
                Label = label;
                Support = support;
                Reward = reward;
            }
        }

        JobsType ReadType()
        {
            var body = Mathf.Clamp(Mathf.RoundToInt(_font), 14, 19);
            var route = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(_title, _font * 1.35f)), 18, 24);
            var tab = Mathf.Clamp(Mathf.RoundToInt(_font * 1.12f), 15, 19);
            var label = Mathf.Clamp(Mathf.RoundToInt(_font * 0.92f), 13, 16);
            var reward = Mathf.Clamp(Mathf.RoundToInt(_font * 1.2f), 16, 20);
            return new JobsType(route, tab, body, label, label, reward);
        }

        float LayoutRow(int index, JobsType type)
        {
            _rowTitle[index].fontSize = type.Route;
            _rowTitle[index].fontStyle = FontStyle.Bold;
            _rowTitle[index].alignment = TextAnchor.UpperLeft;
            _rowTitle[index].horizontalOverflow = HorizontalWrapMode.Wrap;
            _rowTitle[index].verticalOverflow = VerticalWrapMode.Truncate;
            _rowMeta[index].fontSize = type.Support;
            _rowMeta[index].fontStyle = FontStyle.Normal;
            _rowMeta[index].alignment = TextAnchor.UpperLeft;
            _rowMeta[index].horizontalOverflow = HorizontalWrapMode.Wrap;
            _rowMeta[index].verticalOverflow = VerticalWrapMode.Truncate;
            var pad = 8f;
            var gap = 5f;
            var titleH = Mathf.Max(type.Route * 1.22f, 18f);
            var metaH = Mathf.Max(type.Support * 1.22f, 16f);
            PlaceTop(_rowTitle[index].rectTransform, pad, pad, titleH);
            PlaceTop(_rowMeta[index].rectTransform, pad, pad + titleH + gap, metaH);
            return Mathf.Max(_touch, pad + titleH + gap + metaH + pad);
        }

        void LayoutDetail(JobsType type, float closeReserve)
        {
            var y = 8f;
            y = StackDetail(_detailName, type.Route, FontStyle.Bold, Ink, y, 2.6f, closeReserve);
            y = StackDetail(_detailStatus, type.Value, FontStyle.Bold, Ink, y + 6f, 1.5f, 0f);
            y = StackDetail(_detailCargoLabel, type.Label, FontStyle.Normal, InkMuted, y + 8f, 1.35f, 0f);
            y = StackDetail(_detailCargoValue, type.Value, FontStyle.Normal, Ink, y + 2f, 1.7f, 0f);
            y = StackDetail(_detailRewardLabel, type.Label, FontStyle.Normal, InkMuted, y + 8f, 1.35f, 0f);
            y = StackDetail(_detailRewardValue, type.Reward, FontStyle.Bold, MapHudPalette.Accent, y + 2f, 1.5f, 0f);
            if (_detailBody != null)
            {
                _detailBody.fontSize = type.Support;
                _detailBody.fontStyle = FontStyle.Normal;
                _detailBody.color = InkMuted;
                _detailBody.alignment = TextAnchor.UpperLeft;
                _detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
                _detailBody.verticalOverflow = VerticalWrapMode.Truncate;
                var extra = string.IsNullOrEmpty(_detailBody.text) ? 0f : Mathf.Min(_detailBody.preferredHeight + 4f, type.Support * 6f);
                PlaceTop(_detailBody.rectTransform, 10f, y + 8f, Mathf.Max(extra, 0f));
                _detailBody.gameObject.SetActive(extra > 0f);
                if (extra > 0f)
                    y += 8f + extra;
            }

            LayoutEquipment(type, y + 8f);
        }

        void LayoutEquipment(JobsType type, float y)
        {
            if (_equip == null || !_equip.gameObject.activeSelf)
                return;
            var host = _equip;
            host.anchorMin = new Vector2(0f, 1f);
            host.anchorMax = new Vector2(1f, 1f);
            host.pivot = new Vector2(0.5f, 1f);
            host.anchoredPosition = new Vector2(0f, -y);
            var cursor = 0f;
            cursor = StackDetail(_equipTitle, type.Label, FontStyle.Bold, Ink, cursor, 1.35f, 0f);
            for (var i = 0; i < _equipKind.Length; i++)
            {
                if (_equipKind[i] == null || !_equipKind[i].transform.parent.gameObject.activeSelf)
                    continue;
                var row = (RectTransform)_equipKind[i].transform.parent;
                var rowH = Mathf.Max(28f, type.Value * 2.1f);
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(0f, rowH);
                row.anchoredPosition = new Vector2(0f, -(cursor + 4f));
                PlaceTop(_equipKind[i].rectTransform, 0f, 0f, type.Value * 1.2f, 80f);
                _equipKind[i].fontSize = type.Value;
                PlaceTop(_equipMeta[i].rectTransform, 0f, type.Value * 1.2f, type.Support * 1.2f, 80f);
                _equipMeta[i].fontSize = type.Support;
                if (_equipSelect[i] != null)
                {
                    var select = (RectTransform)_equipSelect[i].transform;
                    select.anchorMin = new Vector2(1f, 0.5f);
                    select.anchorMax = new Vector2(1f, 0.5f);
                    select.pivot = new Vector2(1f, 0.5f);
                    select.sizeDelta = new Vector2(72f, Mathf.Max(24f, _touch * 0.55f));
                    select.anchoredPosition = new Vector2(0f, 0f);
                    _equipSelectLabel[i].fontSize = type.Tab - 2;
                    if (_equipSelectLabel[i].rectTransform.rect.width > 8f)
                        MapManagementLayout.ClipLabel(_equipSelectLabel[i]);
                }

                cursor += rowH + 4f;
            }

            host.sizeDelta = new Vector2(0f, cursor);
        }

        float StackDetail(Text label, int size, FontStyle style, Color color, float y, float lines, float rightReserve)
        {
            if (label == null)
                return y;
            var show = !string.IsNullOrEmpty(label.text);
            label.gameObject.SetActive(show);
            if (!show)
                return y;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            var height = Mathf.Max(size * 1.2f, Mathf.Min(label.preferredHeight + 2f, size * lines));
            PlaceTop(label.rectTransform, 10f, y, height, rightReserve);
            return y + height;
        }

        static void PlaceTop(RectTransform rect, float pad, float y, float height, float rightReserve = 0f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(-(pad * 2f + rightReserve), height);
        }

        void BuildIfNeeded()
        {
            if (_built && UiIntact())
                return;
            if (_built)
                TearDownRuntimeUi();
            _built = true;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            _status = Host("ManagementJobsStatus", root);
            _statusText = Label(_status, "Text", font, FontStyle.Normal, InkMuted);
            Inset(_statusText.rectTransform, 0f, 0f, 0f, 0f);
            _fixtureMark = Label(root, "Fixture", font, FontStyle.Bold, MapHudPalette.Accent);
            _fixtureMark.alignment = TextAnchor.MiddleLeft;
            _fixtureMark.text = "FIXTURE";
            _fixtureMark.horizontalOverflow = HorizontalWrapMode.Overflow;
            var mark = _fixtureMark.rectTransform;
            mark.anchorMin = new Vector2(0f, 1f);
            mark.anchorMax = new Vector2(0f, 1f);
            mark.pivot = new Vector2(0f, 1f);
            mark.anchoredPosition = new Vector2(0f, 0f);
            mark.sizeDelta = new Vector2(120f, 18f);
            _cats = Host("ManagementJobsCategories", root);
            for (var i = 0; i < 3; i++)
            {
                var slot = i;
                _catButtons[i] = Action(_cats, "ManagementJobsCat" + i, font, out _catLabels[i]);
                _catButtons[i].onClick.AddListener(() => SetCategory((MapJobCategory)slot));
            }

            _list = Host("ManagementJobsList", root);
            var viewport = Host("ManagementJobsViewport", _list);
            var image = viewport.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            _content = Host("ManagementJobsContent", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _scroll = _list.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            for (var i = 0; i < MaxRows; i++)
            {
                var slot = i;
                _rows[i] = Action(_content, "ManagementJobsRow" + i, font, out _rowTitle[i]);
                _rowMeta[i] = Label(_rows[i].transform, "Meta", font, FontStyle.Normal, InkMuted);
                _rowTitle[i].alignment = TextAnchor.UpperLeft;
                _rowMeta[i].alignment = TextAnchor.UpperLeft;
                _rows[i].onClick.AddListener(() => Select(_rowIds[slot]));
                _rows[i].gameObject.SetActive(false);
            }

            _detail = Host("ManagementJobsDetail", root);
            var detailImage = _detail.gameObject.AddComponent<Image>();
            detailImage.color = new Color(0.035f, 0.067f, 0.110f, 0.04f);
            detailImage.raycastTarget = true;
            _detailName = Label(_detail, "ManagementJobsDetailName", font, FontStyle.Bold, Ink);
            _detailStatus = Label(_detail, "ManagementJobsDetailStatus", font, FontStyle.Bold, Ink);
            _detailCargoLabel = Label(_detail, "ManagementJobsDetailCargoLabel", font, FontStyle.Normal, InkMuted);
            _detailCargoValue = Label(_detail, "ManagementJobsDetailCargoValue", font, FontStyle.Normal, Ink);
            _detailRewardLabel = Label(_detail, "ManagementJobsDetailRewardLabel", font, FontStyle.Normal, InkMuted);
            _detailRewardValue = Label(_detail, "ManagementJobsDetailRewardValue", font, FontStyle.Bold, MapHudPalette.Accent);
            _detailBody = Label(_detail, "ManagementJobsDetailBody", font, FontStyle.Normal, InkMuted);
            _detailName.alignment = TextAnchor.UpperLeft;
            _detailStatus.alignment = TextAnchor.UpperLeft;
            _detailCargoLabel.alignment = TextAnchor.UpperLeft;
            _detailCargoValue.alignment = TextAnchor.UpperLeft;
            _detailRewardLabel.alignment = TextAnchor.UpperLeft;
            _detailRewardValue.alignment = TextAnchor.UpperLeft;
            _detailBody.alignment = TextAnchor.UpperLeft;
            _origin = Action(_detail, "ManagementJobsOrigin", font, out _originLabel);
            _destination = Action(_detail, "ManagementJobsDestination", font, out _destinationLabel);
            _origin.onClick.AddListener(() => TryFocusOrigin());
            _destination.onClick.AddListener(() => TryFocusDestination());
            _accept = Action(_detail, "ManagementJobsAccept", font, out _acceptLabel);
            _accept.onClick.AddListener(() => TryAccept());
            _accept.gameObject.SetActive(false);
            _startHold = Action(_detail, "ManagementJobsStartHold", font, out _startHoldLabel);
            _startHold.interactable = false;
            _startHold.gameObject.SetActive(false);
            _equip = Host("ManagementJobsEquipment", _detail);
            _equipTitle = Label(_equip, "Title", font, FontStyle.Bold, Ink);
            _equipTitle.alignment = TextAnchor.UpperLeft;
            for (var i = 0; i < 3; i++)
            {
                var slot = i;
                var row = Host("ManagementJobsEquip" + i, _equip);
                _equipKind[i] = Label(row, "Kind", font, FontStyle.Bold, Ink);
                _equipMeta[i] = Label(row, "Meta", font, FontStyle.Normal, InkMuted);
                _equipKind[i].alignment = TextAnchor.UpperLeft;
                _equipMeta[i].alignment = TextAnchor.UpperLeft;
                _equipSelect[i] = Action(row, "ManagementJobsEquipSelect" + i, font, out _equipSelectLabel[i]);
                _equipSelect[i].onClick.AddListener(() => TrySelectEquipment(slot));
                row.gameObject.SetActive(false);
            }
            _equip.gameObject.SetActive(false);
            _back = Action(root, "ManagementJobsBack", font, out _backLabel);
            _back.onClick.AddListener(ShowList);
            _back.gameObject.SetActive(false);
            var closeGo = new GameObject("ManagementJobsDetailClose", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeGo.hideFlags = HideFlags.DontSave;
            closeGo.transform.SetParent(_detail, false);
            var closeImage = closeGo.GetComponent<Image>();
            closeImage.color = new Color(0.045f, 0.086f, 0.125f, 0.55f);
            closeImage.raycastTarget = true;
            _detailClose = closeGo.GetComponent<Button>();
            _detailClose.targetGraphic = closeImage;
            _detailClose.onClick.AddListener(CloseDetail);
            var glyph = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glyph.hideFlags = HideFlags.DontSave;
            glyph.transform.SetParent(closeGo.transform, false);
            _detailCloseIcon = glyph.GetComponent<Image>();
            _detailCloseIcon.sprite = MapPanelIcons.Get(MapPanelIcons.Close);
            _detailCloseIcon.color = MapHudPalette.Accent;
            _detailCloseIcon.raycastTarget = false;
            _detailCloseIcon.preserveAspect = true;
            _detailClose.gameObject.SetActive(false);
            _detail.gameObject.SetActive(false);
        }

        void Present(MapJobsSnapshot snapshot)
        {
            BuildIfNeeded();
            if (!UiIntact())
                return;
            _shown = snapshot;
            _statusText.text = StatusCopy(snapshot);
            if (_fixtureMark != null)
                _fixtureMark.gameObject.SetActive(false);
            var cats = new[] { OpsContent.JobsAvailable, OpsContent.JobsActive, OpsContent.JobsHistory };
            for (var i = 0; i < 3; i++)
            {
                _catLabels[i].text = Line(cats[i]);
                _catButtons[i].GetComponent<Image>().color = snapshot.Category == (MapJobCategory)i ? ChipOn : ChipOff;
            }

            var shown = 0;
            for (var i = 0; i < MaxRows; i++)
            {
                MapJobsItem item;
                if (!NextInCategory(snapshot, ref shown, out item))
                {
                    _rows[i].gameObject.SetActive(false);
                    _rowIds[i] = "";
                    continue;
                }

                _rows[i].gameObject.SetActive(true);
                _rowIds[i] = item.ContractId;
                _rowTitle[i].text = RouteCopy(item);
                _rowMeta[i].text = RowMeta(item);
                _rows[i].GetComponent<Image>().color = item.ContractId == snapshot.SelectedId ? RowSelected : Row;
            }

            MapJobsItem selected;
            if (!_detailOpen
                || snapshot.Status != MapJobsStatus.Ready
                || string.IsNullOrEmpty(snapshot.SelectedId)
                || !snapshot.TryGet(snapshot.SelectedId, out selected)
                || !selected.InCategory(snapshot.Category))
            {
                ClearDetail();
                return;
            }

            _detailName.text = RouteCopy(selected);
            var statusLabel = Line(OpsContent.JobsStatusField);
            var statusValue = ContractCopy(selected);
            _detailStatus.text = statusLabel.Length > 0 && statusValue.Length > 0
                ? statusLabel + ": " + statusValue
                : statusValue;
            _detailCargoLabel.text = selected.HasCargo || selected.HasQuantity ? Line(OpsContent.JobsCargo) : "";
            _detailCargoValue.text = CargoLine(selected);
            if (selected.HasSafeReward)
            {
                _detailRewardLabel.text = Line(OpsContent.JobsReward);
                _detailRewardValue.text = RewardCopy(selected);
            }
            else if (selected.HasReward)
            {
                _detailRewardLabel.text = Line(OpsContent.JobsReward);
                _detailRewardValue.text = Line(OpsContent.JobsRewardUnavailable);
            }
            else
            {
                _detailRewardLabel.text = "";
                _detailRewardValue.text = "";
            }

            _detailBody.text = DetailCopy(selected);
            _origin.gameObject.SetActive(selected.HasOriginCity);
            _origin.interactable = selected.HasOriginCity;
            _destination.gameObject.SetActive(selected.HasDestinationCity);
            _destination.interactable = selected.HasDestinationCity;
            _originLabel.text = Line(OpsContent.JobsShowOrigin);
            _destinationLabel.text = Line(OpsContent.JobsShowDestination);
            _backLabel.text = Line(OpsContent.JobsBack);
            var showAccept = MapJobsAccept.CanShow(selected, snapshot);
            if (_accept != null)
            {
                _accept.gameObject.SetActive(showAccept);
                _accept.interactable = showAccept && MapJobsAccept.CanInteract(selected, snapshot);
                _acceptLabel.text = Line(OpsContent.JobsAccept);
            }

            if (_startHold != null)
            {
                _startHold.gameObject.SetActive(false);
                _startHold.interactable = false;
            }

            PresentEquipment(selected, snapshot);
        }

        void PresentEquipment(MapJobsItem selected, MapJobsSnapshot snapshot)
        {
            var show = MapJobsEquipmentSelection.CanShow(selected, snapshot);
            if (_equip == null)
                return;
            _equip.gameObject.SetActive(show);
            _equipCount = 0;
            if (!show)
                return;
            MapJobsEquipmentSelection.Sync(selected, snapshot, MapFleetStore.Current);
            _equipTitle.text = Line(OpsContent.JobsEquipment);
            if (!MapJobsCandidateJoin.TryResolve(selected, MapFleetStore.Current, out var slots))
            {
                _equip.gameObject.SetActive(false);
                return;
            }

            _equipCount = slots.Length;
            for (var i = 0; i < 3; i++)
            {
                var row = _equipKind[i] != null ? _equipKind[i].transform.parent.gameObject : null;
                if (i >= slots.Length)
                {
                    if (row != null)
                        row.SetActive(false);
                    _equipIds[i] = "";
                    continue;
                }

                var slot = slots[i];
                row.SetActive(true);
                _equipKinds[i] = slot.Kind;
                _equipIds[i] = slot.AssetId;
                _equipKind[i].text = KindCopy(slot.Kind);
                _equipMeta[i].text = SlotMeta(slot);
                var selectedSlot = MapJobsEquipmentSelection.IsSelected(slot.Kind, slot.AssetId);
                _equipSelect[i].gameObject.SetActive(true);
                _equipSelect[i].interactable = slot.Selectable;
                _equipSelectLabel[i].text = Line(selectedSlot ? OpsContent.JobsEquipmentSelected : OpsContent.JobsEquipmentSelect);
            }
        }

        string KindCopy(MapFleetKind kind)
        {
            if (kind == MapFleetKind.Truck)
                return Line(OpsContent.FleetKindTruck);
            if (kind == MapFleetKind.Driver)
                return Line(OpsContent.FleetKindDriver);
            if (kind == MapFleetKind.Trailer)
                return Line(OpsContent.FleetKindTrailer);
            return "";
        }

        string SlotMeta(MapJobsCandidateSlot slot)
        {
            if (slot.MissingFromFleet)
                return Line(OpsContent.JobsEquipmentMissing);
            if (slot.InFleet && !slot.KindMatches)
                return Line(OpsContent.JobsEquipmentMismatch);
            if (slot.Assigned)
                return Line(OpsContent.JobsEquipmentAssigned);
            if (slot.Selectable)
                return Line(OpsContent.JobsEquipmentAvailable);
            return Line(OpsContent.JobsEquipmentUnavailable);
        }

        static bool NextInCategory(MapJobsSnapshot snapshot, ref int index, out MapJobsItem item)
        {
            var seen = 0;
            for (var i = 0; i < snapshot.Items.Length; i++)
            {
                if (!snapshot.Items[i].InCategory(snapshot.Category))
                    continue;
                if (seen == index)
                {
                    index++;
                    item = snapshot.Items[i];
                    return true;
                }

                seen++;
            }

            item = default;
            return false;
        }

        string StatusCopy(MapJobsSnapshot snapshot)
        {
            string text;
            switch (snapshot.Status)
            {
                case MapJobsStatus.Loading:
                    text = Line(OpsContent.JobsLoading);
                    break;
                case MapJobsStatus.Error:
                    text = Line(OpsContent.JobsError);
                    break;
                case MapJobsStatus.Empty:
                    text = Line(OpsContent.JobsEmpty);
                    break;
                case MapJobsStatus.Ready:
                    text = snapshot.CountIn(snapshot.Category) == 0
                        ? Line(OpsContent.JobsCategoryEmpty)
                        : "";
                    break;
                default:
                    text = Line(OpsContent.BodyJobs);
                    break;
            }

            if (snapshot.Status == MapJobsStatus.Ready
                && !_expanded
                && snapshot.TryGet(snapshot.SelectedId, out var selected)
                && selected.InCategory(snapshot.Category))
                text += "\n" + RouteCopy(selected);
            if (snapshot.Synthetic)
                text = "FIXTURE\n" + text;
            if (!string.IsNullOrEmpty(MapJobsAccept.MessageKey))
            {
                var accept = Line(MapJobsAccept.MessageKey);
                if (!string.IsNullOrEmpty(accept))
                    text = string.IsNullOrEmpty(text) ? accept : text + "\n" + accept;
            }

            return text;
        }

        string RowMeta(MapJobsItem item)
        {
            var cargo = item.HasCargo ? CargoCopy(item) : "";
            var qty = QuantityCopy(item);
            if (cargo.Length > 0 && qty.Length > 0)
                return cargo + " · " + qty;
            if (cargo.Length > 0)
                return cargo;
            if (qty.Length > 0)
                return qty;
            return ContractCopy(item);
        }

        string DetailCopy(MapJobsItem item)
        {
            var text = "";
            if (item.Expired)
                text = AppendLine(text, Line(OpsContent.JobsExpired));
            if (item.HasOfferExpiry)
                text = AppendLine(text, item.ExpiresAt);
            if (item.HasRequirements)
            {
                var req = Line(OpsContent.JobsRequirements);
                if (!string.IsNullOrEmpty(item.RequirementTruck))
                    req += " " + item.RequirementTruck;
                if (!string.IsNullOrEmpty(item.RequirementDriver))
                    req += " " + item.RequirementDriver;
                if (!string.IsNullOrEmpty(item.RequirementTrailer))
                    req += " " + item.RequirementTrailer;
                text = AppendLine(text, req);
            }

            if (item.DeliveryStatus != MapJobDeliveryStatus.None)
                text = AppendLine(text, DeliveryCopy(item.DeliveryStatus));
            if (item.HasArrival)
                text = AppendLine(text, item.ExpectedArrivalAt);
            if (item.HasVehicle)
                text = AppendLine(text, Line(OpsContent.JobsVehicle) + " " + item.AssignedVehicleId);
            if (!item.HasOriginCity)
                text = AppendLine(text, Line(OpsContent.JobsOriginUnknown));
            if (!item.HasDestinationCity)
                text = AppendLine(text, Line(OpsContent.JobsDestinationUnknown));
            return text;
        }

        string ContractCopy(MapJobsItem item)
        {
            switch (item.ContractStatus)
            {
                case MapJobContractStatus.Offered:
                    return Line(OpsContent.JobsOffered);
                case MapJobContractStatus.Accepted:
                    return Line(OpsContent.JobsAccepted);
                case MapJobContractStatus.InProgress:
                    return Line(OpsContent.JobsInProgress);
                case MapJobContractStatus.Completed:
                    return Line(OpsContent.JobsCompleted);
                case MapJobContractStatus.Failed:
                    return Line(OpsContent.JobsFailed);
                default:
                    return Line(OpsContent.JobsUnknown);
            }
        }

        string DeliveryCopy(MapJobDeliveryStatus status)
        {
            if (status == MapJobDeliveryStatus.Active)
                return Line(OpsContent.JobsDeliveryActive);
            if (status == MapJobDeliveryStatus.Completed)
                return Line(OpsContent.JobsDeliveryCompleted);
            return "";
        }

        bool FocusCity(bool origin)
        {
            var snapshot = MapJobsStore.Current;
            if (!snapshot.TryGet(snapshot.SelectedId, out var item) || !item.InCategory(snapshot.Category))
                return false;
            var id = origin ? item.OriginCityId : item.DestinationCityId;
            if (!MapCities.TryGet(id, out var city))
                return false;
            var shell = GetComponentInParent<MapCommandShell>();
            var rig = shell != null ? FindRig(shell) : null;
            if (rig == null)
                return false;
            if (shell != null)
                shell.ShowCity(city.Id);
            rig.FocusWorldPoint(MapProjection.Project(city.Latitude, city.Longitude));
            return true;
        }

        float Scale()
        {
            var panel = GetComponentInParent<MapManagementPanel>();
            return panel != null ? panel.PlacedCanvasScale : _scale;
        }

        string RouteCopy(MapJobsItem item)
        {
            var origin = CityCopy(item.OriginCityId, OpsContent.JobsOriginUnknown);
            var dest = CityCopy(item.DestinationCityId, OpsContent.JobsDestinationUnknown);
            if (!item.HasOriginCity && !item.HasDestinationCity)
                return origin;
            return origin + " → " + dest;
        }

        string CityCopy(string cityId, string unknownKey)
        {
            if (string.IsNullOrEmpty(cityId) || !MapCities.TryGet(cityId, out _))
                return Line(unknownKey);
            return Line(MapContent.Table, MapContent.CityKey(cityId));
        }

        string RewardCopy(MapJobsItem item)
        {
            var sep = LocaleService.CurrentCode == LocaleCodes.English ? "." : ",";
            return MapJobsMoney.TryFormat(item.Reward, item.Currency, item.Scale, sep, out var text) ? text : "";
        }

        string QuantityCopy(MapJobsItem item)
        {
            if (!item.HasQuantity)
                return "";
            var thousands = LocaleService.CurrentCode == LocaleCodes.English ? "," : ".";
            var qty = item.Quantity;
            if (MapJobsMoney.TryGroupDigits(qty, thousands, out var grouped))
                qty = grouped;
            if (string.IsNullOrEmpty(item.Unit))
                return qty;
            return qty + " " + item.Unit;
        }

        string CargoCopy(MapJobsItem item)
        {
            if (!item.HasCargo)
                return "";
            var key = OpsContent.CargoKey(item.CargoId);
            if (!string.IsNullOrEmpty(key))
            {
                var named = Line(key);
                if (LooksLikeKey(named) || string.IsNullOrEmpty(named))
                    named = Line(OpsContent.JobsCargoUnknown);
                if (!LooksLikeKey(named) && !string.IsNullOrEmpty(named))
                    return named;
            }

            var unknown = Line(OpsContent.JobsCargoUnknown);
            return LooksLikeKey(unknown) ? "" : unknown;
        }

        string CargoLine(MapJobsItem item)
        {
            var cargo = CargoCopy(item);
            var qty = QuantityCopy(item);
            if (cargo.Length > 0 && qty.Length > 0)
                return cargo + " · " + qty;
            if (cargo.Length > 0)
                return cargo;
            return qty;
        }

        void ClearDetail()
        {
            SetText(_detailName, "");
            SetText(_detailStatus, "");
            SetText(_detailCargoLabel, "");
            SetText(_detailCargoValue, "");
            SetText(_detailRewardLabel, "");
            SetText(_detailRewardValue, "");
            SetText(_detailBody, "");
            if (_origin != null)
                _origin.gameObject.SetActive(false);
            if (_destination != null)
                _destination.gameObject.SetActive(false);
            if (_accept != null)
                _accept.gameObject.SetActive(false);
            if (_startHold != null)
                _startHold.gameObject.SetActive(false);
            if (_equip != null)
                _equip.gameObject.SetActive(false);
            _equipCount = 0;
        }

        void OnLocaleChanged(Locale locale)
        {
            if (this == null || !isActiveAndEnabled)
                return;
            Refresh();
        }

        void WatchLocalization()
        {
            if (LocaleService.IsReady)
                return;
            var operation = LocalizationSettings.InitializationOperation;
            if (operation.IsDone)
                return;
            if (_watchingLocalization)
                return;
            _watchingLocalization = true;
            _localizationInit = operation;
            _localizationInit.Completed += OnLocalizationInitialized;
        }

        void StopWatchingLocalization()
        {
            if (!_watchingLocalization)
                return;
            _localizationInit.Completed -= OnLocalizationInitialized;
            _watchingLocalization = false;
        }

        void OnLocalizationInitialized(AsyncOperationHandle handle)
        {
            handle.Completed -= OnLocalizationInitialized;
            _watchingLocalization = false;
            if (this != null && isActiveAndEnabled)
                Refresh();
        }

        bool UiIntact()
        {
            if (!Alive(_status) || !Alive(_statusText) || !Alive(_cats) || !Alive(_list)
                || !Alive(_content) || !Alive(_detail) || !Alive(_detailName) || !Alive(_detailStatus)
                || !Alive(_detailCargoLabel) || !Alive(_detailCargoValue)
                || !Alive(_detailRewardLabel) || !Alive(_detailRewardValue) || !Alive(_detailBody)
                || !Alive(_origin) || !Alive(_originLabel) || !Alive(_destination) || !Alive(_destinationLabel)
                || !Alive(_back) || !Alive(_backLabel) || !Alive(_detailClose) || !Alive(_detailCloseIcon)
                || !Alive(_accept) || !Alive(_acceptLabel)
                || !Alive(_startHold) || !Alive(_startHoldLabel)
                || !Alive(_equip) || !Alive(_equipTitle)
                || !Alive(_scroll))
                return false;
            for (var i = 0; i < _equipKind.Length; i++)
            {
                if (!Alive(_equipKind[i]) || !Alive(_equipMeta[i]) || !Alive(_equipSelect[i]) || !Alive(_equipSelectLabel[i]))
                    return false;
            }
            for (var i = 0; i < _catButtons.Length; i++)
            {
                if (!Alive(_catButtons[i]) || !Alive(_catLabels[i]))
                    return false;
            }

            for (var i = 0; i < _rows.Length; i++)
            {
                if (!Alive(_rows[i]) || !Alive(_rowTitle[i]) || !Alive(_rowMeta[i]))
                    return false;
            }

            return true;
        }

        void TearDownRuntimeUi()
        {
            for (var i = 0; i < _catButtons.Length; i++)
            {
                if (_catButtons[i] != null)
                    _catButtons[i].onClick.RemoveAllListeners();
                _catButtons[i] = null;
                _catLabels[i] = null;
            }

            for (var i = 0; i < _rows.Length; i++)
            {
                if (_rows[i] != null)
                    _rows[i].onClick.RemoveAllListeners();
                _rows[i] = null;
                _rowTitle[i] = null;
                _rowMeta[i] = null;
                _rowIds[i] = "";
            }

            if (_origin != null)
                _origin.onClick.RemoveAllListeners();
            if (_destination != null)
                _destination.onClick.RemoveAllListeners();
            if (_back != null)
                _back.onClick.RemoveAllListeners();
            if (_detailClose != null)
                _detailClose.onClick.RemoveAllListeners();
            if (_accept != null)
                _accept.onClick.RemoveAllListeners();
            if (_startHold != null)
                _startHold.onClick.RemoveAllListeners();
            for (var i = 0; i < _equipSelect.Length; i++)
            {
                if (_equipSelect[i] != null)
                    _equipSelect[i].onClick.RemoveAllListeners();
                _equipSelect[i] = null;
                _equipSelectLabel[i] = null;
                _equipKind[i] = null;
                _equipMeta[i] = null;
                _equipIds[i] = "";
            }
            Release(_status != null ? _status.gameObject : null);
            Release(_fixtureMark != null ? _fixtureMark.gameObject : null);
            Release(_cats != null ? _cats.gameObject : null);
            Release(_list != null ? _list.gameObject : null);
            Release(_detail != null ? _detail.gameObject : null);
            Release(_back != null ? _back.gameObject : null);
            _status = null;
            _statusText = null;
            _fixtureMark = null;
            _cats = null;
            _list = null;
            _scroll = null;
            _content = null;
            _detail = null;
            _detailName = null;
            _detailStatus = null;
            _detailCargoLabel = null;
            _detailCargoValue = null;
            _detailRewardLabel = null;
            _detailRewardValue = null;
            _detailBody = null;
            _origin = null;
            _originLabel = null;
            _destination = null;
            _destinationLabel = null;
            _back = null;
            _backLabel = null;
            _detailClose = null;
            _detailCloseIcon = null;
            _accept = null;
            _acceptLabel = null;
            _startHold = null;
            _startHoldLabel = null;
            _equip = null;
            _equipTitle = null;
            _equipCount = 0;
            _built = false;
        }

        static bool Alive(Object item)
        {
            return item != null;
        }

        static void Release(GameObject item)
        {
            if (item == null)
                return;
            Object.DestroyImmediate(item);
        }

        static string Line(string tableName, string key)
        {
            if (!LocaleService.IsReady)
                return string.Empty;
            var database = LocalizationSettings.StringDatabase;
            var locale = LocalizationSettings.SelectedLocale;
            if (database == null || locale == null)
                return string.Empty;
            var handle = database.GetTableAsync(tableName, locale);
            if (!handle.IsDone)
                return string.Empty;
            var table = handle.Result as StringTable;
            var entry = table != null ? table.GetEntry(key) : null;
            var text = entry != null ? entry.GetLocalizedString() : "";
            if (string.IsNullOrWhiteSpace(text) || LooksLikeKey(text))
                return string.Empty;
            return text;
        }

        static bool LooksLikeKey(string text)
        {
            return !string.IsNullOrEmpty(text)
                && text.StartsWith("ops.", System.StringComparison.Ordinal);
        }

        static string Field(Text label)
        {
            return label != null ? label.text : "";
        }

        static void SetText(Text label, string value)
        {
            if (label != null)
                label.text = value ?? "";
        }

        static string JoinDetail(params string[] parts)
        {
            var text = "";
            for (var i = 0; i < parts.Length; i++)
                text = AppendLine(text, parts[i]);
            return text;
        }

        static string AppendLine(string text, string line)
        {
            if (string.IsNullOrEmpty(line))
                return text ?? "";
            if (string.IsNullOrEmpty(text))
                return line;
            return text + "\n" + line;
        }

        static bool VisibleText(Text label)
        {
            return label != null && label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text);
        }

        static bool VerticalOverlap(RectTransform a, RectTransform b)
        {
            if (a == null || b == null)
                return false;
            var ra = WorldRect(a);
            var rb = WorldRect(b);
            if (ra.height < 1f || rb.height < 1f)
                return false;
            var overlapX = Mathf.Min(ra.xMax, rb.xMax) - Mathf.Max(ra.xMin, rb.xMin);
            var overlapY = Mathf.Min(ra.yMax, rb.yMax) - Mathf.Max(ra.yMin, rb.yMin);
            return overlapX > 1f && overlapY > 1f;
        }

        static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = corners[0];
            var max = corners[2];
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static string Line(string key)
        {
            return Line(OpsContent.Table, key);
        }

        static MapCameraRig FindRig(MapCommandShell shell)
        {
            var roots = shell.gameObject.scene.GetRootGameObjects();
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
            image.color = Row;
            image.raycastTarget = true;
            var button = item.GetComponent<Button>();
            button.targetGraphic = image;
            label = Label(item.transform, "Label", font, FontStyle.Bold, Ink);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            Inset(label.rectTransform, 6f, 0f, 0f, 6f);
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
