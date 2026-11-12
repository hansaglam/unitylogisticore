using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    [DisallowMultipleComponent]
    public sealed class MapWarehouseView : MonoBehaviour
    {
        public const int MaxRows = 20;
        static readonly Color Row = new Color(0.045f, 0.086f, 0.125f, 0.22f);
        static readonly Color RowSelected = new Color(0.070f, 0.155f, 0.190f, 0.28f);
        static readonly Color Ink = MapHudPalette.Text;
        static readonly Color InkMuted = MapHudPalette.TextMuted;

        RectTransform _status;
        Text _statusText;
        RectTransform _list;
        ScrollRect _scroll;
        RectTransform _content;
        RectTransform _detail;
        Text _detailName;
        Text _detailBody;
        Button _city;
        Text _cityLabel;
        Button _gps;
        Text _gpsLabel;
        Button _back;
        Text _backLabel;
        readonly Button[] _rows = new Button[MaxRows];
        readonly Text[] _rowTitle = new Text[MaxRows];
        readonly Text[] _rowMeta = new Text[MaxRows];
        readonly string[] _rowIds = new string[MaxRows];
        bool _built;
        bool _detailPage;
        bool _placed;
        bool _expanded;
        float _scale = 1f;
        float _pad;
        float _top;
        float _font;
        float _titleSize;
        float _touch;
        MapWarehouseSnapshot _shown;

        public MapWarehouseStatus Status => MapWarehouseStore.Current.Status;
        public string SelectedId => MapWarehouseStore.Current.SelectedId;
        public string StatusText => _statusText != null ? _statusText.text : string.Empty;
        public string DetailName => _detailName != null && _detail.gameObject.activeInHierarchy ? _detailName.text : string.Empty;
        public string DetailBody => _detailBody != null && _detail.gameObject.activeInHierarchy ? _detailBody.text : string.Empty;
        public bool CityActive => _city != null && _city.gameObject.activeInHierarchy && _city.interactable;
        public bool GpsActive => _gps != null && _gps.gameObject.activeInHierarchy && _gps.interactable;
        public bool DetailVisible => _detail != null && _detail.gameObject.activeInHierarchy;
        public bool ListVisible => _list != null && _list.gameObject.activeInHierarchy;
        public bool FixtureVisible => StatusText.IndexOf("FIXTURE", System.StringComparison.Ordinal) >= 0;
        public bool OffersVisible => false;
        public int MarkerCount => GetComponentsInChildren<LineRenderer>(true).Length;
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

        public static MapWarehouseView Attach(RectTransform panel)
        {
            if (panel == null)
                return null;
            var existing = panel.GetComponentInChildren<MapWarehouseView>(true);
            if (existing != null)
                return existing;
            var item = new GameObject("ManagementWarehouses", typeof(RectTransform));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(panel, false);
            var view = item.AddComponent<MapWarehouseView>();
            view.BuildIfNeeded();
            return view;
        }

        void Awake()
        {
            BuildIfNeeded();
        }

        public void Refresh()
        {
            BuildIfNeeded();
            Present(MapWarehouseStore.Current);
            if (_placed)
                Layout();
        }

        public void Select(string warehouseId)
        {
            MapWarehouseStore.Select(warehouseId);
            if (!string.IsNullOrEmpty(warehouseId))
                _detailPage = true;
            Refresh();
        }

        public void ShowList()
        {
            _detailPage = false;
            Refresh();
        }

        public bool TryFocusCity()
        {
            return Focus(false);
        }

        public bool TryFocusGps()
        {
            return Focus(true);
        }

        public Rect RowHitPixels(int index)
        {
            if (index < 0 || index >= _rows.Length || _rows[index] == null)
                return default;
            return MapManagementLayout.HitRect((RectTransform)_rows[index].transform, Scale());
        }

        public Rect CityHitPixels()
        {
            if (_city == null)
                return default;
            return MapManagementLayout.HitRect((RectTransform)_city.transform, Scale());
        }

        public void Place(float pad, float top, float font, float title, float touch, float scale, bool expanded)
        {
            BuildIfNeeded();
            _pad = pad;
            _top = top;
            _font = font;
            _titleSize = title;
            _touch = touch;
            _scale = scale < 0.0001f ? 1f : scale;
            _expanded = expanded;
            _placed = true;
            Present(MapWarehouseStore.Current);
            Layout();
        }

        void Layout()
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = new Vector2(_pad, _pad);
            root.offsetMax = new Vector2(-_pad, -_top);

            var statusH = Mathf.Max(_touch * 0.55f, _font * 1.6f);
            if (_shown.Synthetic)
                statusH = Mathf.Max(statusH, _font * 2.4f);
            if (!_expanded && _shown.Status == MapWarehouseStatus.Ready && !string.IsNullOrEmpty(_shown.SelectedId))
                statusH = Mathf.Max(statusH, _font * 3.4f);
            _status.anchorMin = new Vector2(0f, 1f);
            _status.anchorMax = new Vector2(1f, 1f);
            _status.pivot = new Vector2(0.5f, 1f);
            _status.anchoredPosition = Vector2.zero;
            _status.sizeDelta = new Vector2(0f, statusH);
            _statusText.fontSize = Mathf.Max(11, Mathf.RoundToInt(_font));
            _statusText.alignment = TextAnchor.UpperLeft;

            var split = _expanded && root.rect.width * _scale >= 1200f;
            var stacked = _expanded && _detailPage && !split;
            var ready = _shown.Status == MapWarehouseStatus.Ready;
            var showList = ready && !stacked;
            var showDetail = ready && !string.IsNullOrEmpty(_shown.SelectedId) && _expanded && (split || stacked);
            _list.gameObject.SetActive(showList);
            _detail.gameObject.SetActive(showDetail);
            _back.gameObject.SetActive(stacked);
            if (showList)
            {
                _list.anchorMin = new Vector2(0f, 0f);
                _list.anchorMax = showDetail && split ? new Vector2(0.42f, 1f) : new Vector2(1f, 1f);
                _list.offsetMin = Vector2.zero;
                _list.offsetMax = new Vector2(showDetail && split ? -6f : 0f, -statusH - 4f);
            }

            if (showDetail)
            {
                _detail.anchorMin = split ? new Vector2(0.42f, 0f) : new Vector2(0f, 0f);
                _detail.anchorMax = Vector2.one;
                _detail.offsetMin = new Vector2(split ? 6f : 0f, stacked ? _touch + 8f : 0f);
                _detail.offsetMax = new Vector2(0f, -statusH - 4f);
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

            var city = (RectTransform)_city.transform;
            var gps = (RectTransform)_gps.transform;
            city.anchorMin = new Vector2(0f, 0f);
            city.anchorMax = new Vector2(_gps.gameObject.activeSelf ? 0.5f : 1f, 0f);
            city.pivot = new Vector2(0f, 0f);
            city.sizeDelta = new Vector2(_gps.gameObject.activeSelf ? -4f : 0f, _touch);
            city.anchoredPosition = Vector2.zero;
            gps.anchorMin = new Vector2(0.5f, 0f);
            gps.anchorMax = new Vector2(1f, 0f);
            gps.pivot = new Vector2(1f, 0f);
            gps.sizeDelta = new Vector2(-4f, _touch);
            gps.anchoredPosition = Vector2.zero;
            _cityLabel.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
            _gpsLabel.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
            _detailName.fontSize = Mathf.Max(13, Mathf.RoundToInt(_titleSize));
            _detailBody.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
            _backLabel.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));

            var used = 0;
            for (var i = 0; i < _rows.Length; i++)
            {
                if (_rows[i] == null || !_rows[i].gameObject.activeSelf)
                    continue;
                var row = (RectTransform)_rows[i].transform;
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(0f, _touch);
                row.anchoredPosition = new Vector2(0f, -used * (_touch + 4f));
                _rowTitle[i].fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
                _rowMeta[i].fontSize = Mathf.Max(11, Mathf.RoundToInt(_font * 0.92f));
                MapManagementLayout.ClipLabel(_rowTitle[i]);
                MapManagementLayout.ClipLabel(_rowMeta[i]);
                used++;
            }

            _content.sizeDelta = new Vector2(0f, used * (_touch + 4f));
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
            _status = Host("ManagementWarehouseStatus", root);
            _statusText = Label(_status, "Text", font, FontStyle.Normal, InkMuted);
            Inset(_statusText.rectTransform, 0f, 0f, 0f, 0f);
            _list = Host("ManagementWarehouseList", root);
            var viewport = Host("ManagementWarehouseViewport", _list);
            var image = viewport.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            _content = Host("ManagementWarehouseContent", viewport);
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
                _rows[i] = Action(_content, "ManagementWarehouseRow" + i, font, out _rowTitle[i]);
                _rowMeta[i] = Label(_rows[i].transform, "Meta", font, FontStyle.Normal, InkMuted);
                _rowTitle[i].alignment = TextAnchor.MiddleLeft;
                _rowMeta[i].alignment = TextAnchor.MiddleLeft;
                Inset(_rowTitle[i].rectTransform, 10f, 22f, 4f, 8f);
                Inset(_rowMeta[i].rectTransform, 10f, 4f, 22f, 8f);
                _rows[i].onClick.AddListener(() => Select(_rowIds[slot]));
                _rows[i].gameObject.SetActive(false);
            }

            _detail = Host("ManagementWarehouseDetail", root);
            var detailImage = _detail.gameObject.AddComponent<Image>();
            detailImage.color = new Color(0.035f, 0.067f, 0.110f, 0.04f);
            detailImage.raycastTarget = true;
            _detailName = Label(_detail, "ManagementWarehouseDetailName", font, FontStyle.Bold, Ink);
            _detailBody = Label(_detail, "ManagementWarehouseDetailBody", font, FontStyle.Normal, InkMuted);
            _detailName.alignment = TextAnchor.UpperLeft;
            _detailBody.alignment = TextAnchor.UpperLeft;
            _detailName.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailName.rectTransform.anchorMin = new Vector2(0f, 1f);
            _detailName.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailName.rectTransform.pivot = new Vector2(0f, 1f);
            _detailName.rectTransform.anchoredPosition = new Vector2(10f, -8f);
            _detailName.rectTransform.sizeDelta = new Vector2(-20f, 28f);
            _detailBody.rectTransform.anchorMin = new Vector2(0f, 0.58f);
            _detailBody.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailBody.rectTransform.offsetMin = new Vector2(10f, 0f);
            _detailBody.rectTransform.offsetMax = new Vector2(-10f, -36f);
            _city = Action(_detail, "ManagementWarehouseCity", font, out _cityLabel);
            _gps = Action(_detail, "ManagementWarehouseGps", font, out _gpsLabel);
            _city.onClick.AddListener(() => TryFocusCity());
            _gps.onClick.AddListener(() => TryFocusGps());
            _back = Action(root, "ManagementWarehouseBack", font, out _backLabel);
            _back.onClick.AddListener(ShowList);
            _back.gameObject.SetActive(false);
            _detail.gameObject.SetActive(false);
            _gps.gameObject.SetActive(false);
        }

        void Present(MapWarehouseSnapshot snapshot)
        {
            _shown = snapshot;
            _statusText.text = StatusCopy(snapshot);
            var shown = 0;
            for (var i = 0; i < MaxRows; i++)
            {
                MapWarehouseItem item;
                if (!NextOwned(snapshot, ref shown, out item))
                {
                    _rows[i].gameObject.SetActive(false);
                    _rowIds[i] = "";
                    continue;
                }

                _rows[i].gameObject.SetActive(true);
                _rowIds[i] = item.WarehouseId;
                _rowTitle[i].text = item.DisplayName;
                _rowMeta[i].text = RowMeta(item);
                _rows[i].GetComponent<Image>().color = item.WarehouseId == snapshot.SelectedId ? RowSelected : Row;
            }

            MapWarehouseItem selected;
            if (snapshot.Status != MapWarehouseStatus.Ready || !snapshot.TryGetOwned(snapshot.SelectedId, out selected))
            {
                _city.gameObject.SetActive(false);
                _gps.gameObject.SetActive(false);
                return;
            }

            _detailName.text = selected.DisplayName;
            _detailBody.text = DetailCopy(selected);
            _city.gameObject.SetActive(selected.HasKnownCity);
            _city.interactable = selected.HasKnownCity;
            _gps.gameObject.SetActive(selected.HasGps);
            _gps.interactable = selected.HasGps;
            _cityLabel.text = Line(OpsContent.Table, OpsContent.WarehousesShowCity);
            _gpsLabel.text = Line(OpsContent.Table, OpsContent.WarehousesShowPosition);
            _backLabel.text = Line(OpsContent.Table, OpsContent.WarehousesBack);
        }

        static bool NextOwned(MapWarehouseSnapshot snapshot, ref int index, out MapWarehouseItem item)
        {
            var seen = 0;
            for (var i = 0; i < snapshot.Items.Length; i++)
            {
                if (!snapshot.Items[i].Owned)
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

        string StatusCopy(MapWarehouseSnapshot snapshot)
        {
            string text;
            switch (snapshot.Status)
            {
                case MapWarehouseStatus.Loading:
                    text = Line(OpsContent.Table, OpsContent.WarehousesLoading);
                    break;
                case MapWarehouseStatus.Error:
                    text = Line(OpsContent.Table, OpsContent.WarehousesError);
                    break;
                case MapWarehouseStatus.Empty:
                    text = Line(OpsContent.Table, OpsContent.WarehousesEmpty);
                    break;
                case MapWarehouseStatus.Ready:
                    text = snapshot.OwnedCount == 0
                        ? Line(OpsContent.Table, OpsContent.WarehousesEmpty)
                        : Line(OpsContent.Table, OpsContent.WarehousesReady);
                    break;
                default:
                    text = Line(OpsContent.Table, OpsContent.BodyWarehouses);
                    break;
            }

            if (snapshot.Status == MapWarehouseStatus.Ready
                && !_expanded
                && snapshot.TryGetOwned(snapshot.SelectedId, out var selected))
                text += "\n" + selected.DisplayName;
            if (snapshot.Synthetic)
                text = "FIXTURE\n" + text;
            return text;
        }

        string RowMeta(MapWarehouseItem item)
        {
            var status = SiteCopy(item);
            var city = CityCopy(item);
            if (string.IsNullOrEmpty(city))
                return status;
            return status + " · " + city;
        }

        string DetailCopy(MapWarehouseItem item)
        {
            var text = SiteCopy(item);
            text += "\n" + Line(OpsContent.Table, OpsContent.WarehousesOwned);
            var city = CityCopy(item);
            if (!string.IsNullOrEmpty(city))
                text += "\n" + Line(OpsContent.Table, OpsContent.WarehousesLocation) + " " + city;
            else
                text += "\n" + Line(OpsContent.Table, OpsContent.WarehousesCityUnknown);
            if (item.HasMatchingCapacity)
                text += "\n" + Line(OpsContent.Table, OpsContent.WarehousesCapacity) + " " + item.CapacitySummary;
            else if (item.HasCapacityUnits)
                text += "\n" + Line(OpsContent.Table, OpsContent.WarehousesCapacity) + " " + item.CapacityUnits;
            else if (item.HasUsedCapacity)
                text += "\n" + Line(OpsContent.Table, OpsContent.WarehousesUsedUnknown);
            if (item.HasLevel)
                text += "\n" + Line(OpsContent.Table, OpsContent.WarehousesLevel) + " " + item.Level;
            return text;
        }

        string SiteCopy(MapWarehouseItem item)
        {
            if (item.SiteStatus == MapWarehouseSiteStatus.Operational)
                return Line(OpsContent.Table, OpsContent.WarehousesOperational);
            if (item.SiteStatus == MapWarehouseSiteStatus.OutOfService)
                return Line(OpsContent.Table, OpsContent.WarehousesOutOfService);
            return Line(OpsContent.Table, OpsContent.WarehousesUnknown);
        }

        string CityCopy(MapWarehouseItem item)
        {
            if (!item.HasKnownCity)
                return "";
            return Line(MapContent.Table, MapContent.CityKey(item.CityId));
        }

        bool Focus(bool gps)
        {
            var snapshot = MapWarehouseStore.Current;
            if (!snapshot.TryGetOwned(snapshot.SelectedId, out var item))
                return false;
            var shell = GetComponentInParent<MapCommandShell>();
            var rig = shell != null ? FindRig(shell) : null;
            if (rig == null)
                return false;
            if (gps)
            {
                if (!item.HasGps)
                    return false;
                rig.FocusWorldPoint(MapProjection.Project(item.Latitude, item.Longitude));
                return true;
            }

            if (!MapCities.TryGet(item.CityId, out var city))
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

        static string Line(string tableName, string key)
        {
            var database = LocalizationSettings.StringDatabase;
            var locale = LocalizationSettings.SelectedLocale;
            var table = locale != null ? database.GetTable(tableName, locale) as StringTable : null;
            var entry = table != null ? table.GetEntry(key) : null;
            var text = entry != null ? entry.GetLocalizedString() : "";
            return string.IsNullOrWhiteSpace(text) ? key : text;
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
