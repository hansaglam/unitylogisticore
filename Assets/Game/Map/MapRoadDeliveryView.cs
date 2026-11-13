using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    [DisallowMultipleComponent]
    public sealed class MapRoadDeliveryView : MonoBehaviour
    {
        public const int MaxRows = 20;
        static readonly Color Row = MapPremiumTheme.Alpha(MapPremiumTheme.Card, 0.72f);
        static readonly Color RowSelected = MapPremiumTheme.Alpha(MapPremiumTheme.Selected, 0.94f);
        static readonly Color Ink = MapHudPalette.Text;
        static readonly Color InkMuted = MapHudPalette.TextMuted;

        RectTransform _status;
        Text _statusText;
        Text _fixtureMark;
        RectTransform _list;
        ScrollRect _scroll;
        RectTransform _content;
        RectTransform _detail;
        Text _detailName;
        Text _detailBody;
        Button _origin;
        Text _originLabel;
        Button _destination;
        Text _destinationLabel;
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
        float _title;
        float _touch;
        MapRoadDeliverySnapshot _shown;

        public MapRoadDeliveryStatus Status => MapRoadDeliveryStore.Current.Status;
        public string SelectedId => MapRoadDeliveryStore.Current.SelectedId;
        public string StatusText => _statusText != null ? _statusText.text : string.Empty;
        public string DetailName => _detailName != null && _detail.gameObject.activeInHierarchy ? _detailName.text : string.Empty;
        public string DetailBody => _detailBody != null && _detail.gameObject.activeInHierarchy ? _detailBody.text : string.Empty;
        public bool OriginActive => _origin != null && _origin.gameObject.activeInHierarchy && _origin.interactable;
        public bool DestinationActive => _destination != null && _destination.gameObject.activeInHierarchy && _destination.interactable;
        public bool DetailVisible => _detail != null && _detail.gameObject.activeInHierarchy;
        public bool ListVisible => _list != null && _list.gameObject.activeInHierarchy;
        public bool FixtureVisible => StatusText.IndexOf("FIXTURE", System.StringComparison.Ordinal) >= 0;
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

        public static MapRoadDeliveryView Attach(RectTransform panel)
        {
            if (panel == null)
                return null;
            var existing = panel.GetComponentInChildren<MapRoadDeliveryView>(true);
            if (existing != null)
                return existing;
            var item = new GameObject("ManagementRoad", typeof(RectTransform));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(panel, false);
            var view = item.AddComponent<MapRoadDeliveryView>();
            view.BuildIfNeeded();
            return view;
        }

        void Awake()
        {
            BuildIfNeeded();
        }

        void OnEnable()
        {
            MapRoadDeliveriesRead.Applied -= OnRoadApplied;
            MapRoadDeliveriesRead.Applied += OnRoadApplied;
            MapRoadDeliveriesRead.TryRefresh();
            Refresh();
        }

        void OnDisable()
        {
            MapRoadDeliveriesRead.Applied -= OnRoadApplied;
        }

        void OnRoadApplied()
        {
            Refresh();
        }

        public void Refresh()
        {
            BuildIfNeeded();
            Present(MapRoadDeliveryStore.Current);
            if (_placed)
                Layout();
        }

        public void Select(string deliveryId)
        {
            MapRoadDeliveryStore.Select(deliveryId);
            if (!string.IsNullOrEmpty(deliveryId))
                _detailPage = true;
            Refresh();
        }

        public void ShowList()
        {
            _detailPage = false;
            Refresh();
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
            Present(MapRoadDeliveryStore.Current);
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
            if (!_expanded && _shown.Status == MapRoadDeliveryStatus.Ready && !string.IsNullOrEmpty(_shown.SelectedId))
                statusH = Mathf.Max(statusH, _font * 3.4f);
            _status.anchorMin = new Vector2(0f, 1f);
            _status.anchorMax = new Vector2(1f, 1f);
            _status.pivot = new Vector2(0.5f, 1f);
            _status.anchoredPosition = Vector2.zero;
            _status.sizeDelta = new Vector2(0f, statusH);
            _statusText.fontSize = Mathf.Max(11, Mathf.RoundToInt(_font));
            _statusText.alignment = TextAnchor.UpperLeft;
            _fixtureMark.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));

            var split = _expanded && root.rect.width * _scale >= 1200f;
            var stacked = _expanded && _detailPage && !split;
            var ready = _shown.Status == MapRoadDeliveryStatus.Ready;
            var showList = ready && !stacked;
            var showDetail = ready && !string.IsNullOrEmpty(_shown.SelectedId) && _expanded && (split || stacked);
            _list.gameObject.SetActive(showList);
            _detail.gameObject.SetActive(showDetail);
            _back.gameObject.SetActive(stacked);
            var topBlock = statusH + 4f;
            if (showList)
            {
                _list.anchorMin = new Vector2(0f, 0f);
                _list.anchorMax = showDetail && split ? new Vector2(0.42f, 1f) : new Vector2(1f, 1f);
                _list.offsetMin = Vector2.zero;
                _list.offsetMax = new Vector2(showDetail && split ? -6f : 0f, -topBlock);
            }

            if (showDetail)
            {
                _detail.anchorMin = split ? new Vector2(0.42f, 0f) : new Vector2(0f, 0f);
                _detail.anchorMax = Vector2.one;
                _detail.offsetMin = new Vector2(split ? 6f : 0f, stacked ? _touch + 8f : 0f);
                _detail.offsetMax = new Vector2(0f, -topBlock);
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
            origin.sizeDelta = new Vector2(-4f, _touch);
            origin.anchoredPosition = Vector2.zero;
            dest.anchorMin = new Vector2(0.5f, 0f);
            dest.anchorMax = new Vector2(1f, 0f);
            dest.pivot = new Vector2(1f, 0f);
            dest.sizeDelta = new Vector2(-4f, _touch);
            dest.anchoredPosition = Vector2.zero;
            _originLabel.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
            _destinationLabel.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
            _detailName.fontSize = Mathf.Max(13, Mathf.RoundToInt(_title));
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
            _status = Host("ManagementRoadStatus", root);
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
            _list = Host("ManagementRoadList", root);
            var viewport = Host("ManagementRoadViewport", _list);
            var image = viewport.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            _content = Host("ManagementRoadContent", viewport);
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
                _rows[i] = Action(_content, "ManagementRoadRow" + i, font, out _rowTitle[i]);
                _rowMeta[i] = Label(_rows[i].transform, "Meta", font, FontStyle.Normal, InkMuted);
                _rowTitle[i].alignment = TextAnchor.MiddleLeft;
                _rowMeta[i].alignment = TextAnchor.MiddleLeft;
                Inset(_rowTitle[i].rectTransform, 10f, 22f, 4f, 8f);
                Inset(_rowMeta[i].rectTransform, 10f, 4f, 22f, 8f);
                _rows[i].onClick.AddListener(() => Select(_rowIds[slot]));
                _rows[i].gameObject.SetActive(false);
            }

            _detail = Host("ManagementRoadDetail", root);
            _detailName = Label(_detail, "Name", font, FontStyle.Bold, Ink);
            _detailBody = Label(_detail, "Body", font, FontStyle.Normal, InkMuted);
            Inset(_detailName.rectTransform, 8f, 0f, 0f, 8f);
            var body = _detailBody.rectTransform;
            body.anchorMin = new Vector2(0f, 0f);
            body.anchorMax = new Vector2(1f, 1f);
            body.offsetMin = new Vector2(8f, _touch + 8f);
            body.offsetMax = new Vector2(-8f, -28f);
            _origin = Action(_detail, "ManagementRoadOrigin", font, out _originLabel);
            _destination = Action(_detail, "ManagementRoadDestination", font, out _destinationLabel);
            _origin.onClick.AddListener(() => TryFocusOrigin());
            _destination.onClick.AddListener(() => TryFocusDestination());
            _back = Action(root, "ManagementRoadBack", font, out _backLabel);
            _back.onClick.AddListener(ShowList);
            _detail.gameObject.SetActive(false);
            _back.gameObject.SetActive(false);
        }

        void Present(MapRoadDeliverySnapshot snapshot)
        {
            _shown = snapshot;
            if (_statusText != null)
                _statusText.text = StatusCopy(snapshot);
            if (_fixtureMark != null)
                _fixtureMark.gameObject.SetActive(snapshot.Synthetic);
            var ready = snapshot.Status == MapRoadDeliveryStatus.Ready;
            for (var i = 0; i < _rows.Length; i++)
            {
                var show = ready && i < snapshot.Items.Length && (_expanded || i < 3);
                _rows[i].gameObject.SetActive(show);
                if (!show)
                {
                    _rowIds[i] = "";
                    continue;
                }

                var item = snapshot.Items[i];
                _rowIds[i] = item.DeliveryId;
                _rowTitle[i].text = RouteCopy(item);
                _rowMeta[i].text = MetaCopy(item);
                var image = _rows[i].GetComponent<Image>();
                if (image != null)
                    image.color = item.DeliveryId == snapshot.SelectedId ? RowSelected : Row;
            }

            if (_originLabel != null)
                _originLabel.text = Line(OpsContent.RoadShowOrigin);
            if (_destinationLabel != null)
                _destinationLabel.text = Line(OpsContent.RoadShowDestination);
            if (_backLabel != null)
                _backLabel.text = Line(OpsContent.RoadBack);

            MapRoadDeliveryItem selected = default;
            var has = ready && snapshot.TryGet(snapshot.SelectedId, out selected);
            if (_detailName != null)
                _detailName.text = has ? RouteCopy(selected) : string.Empty;
            if (_detailBody != null)
                _detailBody.text = has ? DetailCopy(selected) : string.Empty;
            if (_origin != null)
                _origin.interactable = has && selected.HasOriginCity;
            if (_destination != null)
                _destination.interactable = has && selected.HasDestinationCity;
            if (_origin != null)
                _origin.gameObject.SetActive(has);
            if (_destination != null)
                _destination.gameObject.SetActive(has);
        }

        string StatusCopy(MapRoadDeliverySnapshot snapshot)
        {
            var mark = snapshot.Synthetic ? "FIXTURE · " : "";
            switch (snapshot.Status)
            {
                case MapRoadDeliveryStatus.Loading:
                    return mark + Line(OpsContent.RoadLoading);
                case MapRoadDeliveryStatus.Empty:
                    return mark + Line(OpsContent.RoadEmpty);
                case MapRoadDeliveryStatus.Error:
                    return mark + Line(OpsContent.RoadError);
                case MapRoadDeliveryStatus.Ready:
                    return mark + Line(OpsContent.RoadReady);
                default:
                    return mark + Line(OpsContent.BodyRoad);
            }
        }

        string MetaCopy(MapRoadDeliveryItem item)
        {
            var status = LifecycleCopy(item.Lifecycle);
            if (item.HasTruck)
                return status + " · " + Line(OpsContent.FleetKindTruck) + " " + item.TruckId;
            return status + " · " + Line(OpsContent.RoadTruckUnknown);
        }

        string DetailCopy(MapRoadDeliveryItem item)
        {
            var text = LifecycleCopy(item.Lifecycle);
            if (item.HasTruck)
                text += "\n" + Line(OpsContent.FleetKindTruck) + " " + item.TruckId;
            else
                text += "\n" + Line(OpsContent.RoadTruckUnknown);
            text += "\n" + RouteCopy(item);
            if (item.HasArrival)
                text += "\n" + item.ExpectedArrivalAt;
            else
                text += "\n" + Line(OpsContent.RoadArrivalUnknown);
            if (!item.HasOriginCity)
                text += "\n" + Line(OpsContent.JobsOriginUnknown);
            if (!item.HasDestinationCity)
                text += "\n" + Line(OpsContent.JobsDestinationUnknown);
            return text;
        }

        string LifecycleCopy(MapRoadLifecycle lifecycle)
        {
            if (lifecycle == MapRoadLifecycle.Active)
                return Line(OpsContent.RoadActive);
            if (lifecycle == MapRoadLifecycle.Completed)
                return Line(OpsContent.JobsDeliveryCompleted);
            return Line(OpsContent.RoadUnknown);
        }

        bool FocusCity(bool origin)
        {
            var snapshot = MapRoadDeliveryStore.Current;
            if (!snapshot.TryGet(snapshot.SelectedId, out var item))
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

        string RouteCopy(MapRoadDeliveryItem item)
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

        static string Line(string tableName, string key)
        {
            var database = LocalizationSettings.StringDatabase;
            var locale = LocalizationSettings.SelectedLocale;
            var table = locale != null ? database.GetTable(tableName, locale) as StringTable : null;
            var entry = table != null ? table.GetEntry(key) : null;
            var text = entry != null ? entry.GetLocalizedString() : "";
            return string.IsNullOrWhiteSpace(text) ? key : text;
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
            MapPremiumUi.StyleSurface(image, MapPremiumSurface.Card, true);
            image.color = Row;
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
