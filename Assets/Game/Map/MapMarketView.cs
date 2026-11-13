using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    [DisallowMultipleComponent]
    public sealed class MapMarketView : MonoBehaviour
    {
        public const int MaxRows = 20;
        public const int MaxCargos = 4;
        static readonly Color Row = MapPremiumTheme.Alpha(MapPremiumTheme.Card, 0.72f);
        static readonly Color RowSelected = MapPremiumTheme.Alpha(MapPremiumTheme.Selected, 0.94f);
        static readonly Color ChipOn = new Color(0.070f, 0.155f, 0.190f, 0.40f);
        static readonly Color ChipOff = new Color(0.045f, 0.086f, 0.125f, 0.18f);
        static readonly Color Ink = MapHudPalette.Text;
        static readonly Color InkMuted = MapHudPalette.TextMuted;

        RectTransform _status;
        Text _statusText;
        RectTransform _cats;
        readonly Button[] _catButtons = new Button[MaxCargos + 1];
        readonly Text[] _catLabels = new Text[MaxCargos + 1];
        readonly string[] _catIds = new string[MaxCargos + 1];
        RectTransform _list;
        ScrollRect _scroll;
        RectTransform _content;
        RectTransform _detail;
        Text _detailName;
        Text _detailBody;
        Button _city;
        Text _cityLabel;
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
        MapMarketSnapshot _shown;

        public MapMarketStatus Status => MapMarketStore.Current.Status;
        public string SelectedId => MapMarketStore.Current.SelectedId;
        public string CargoFilter => MapMarketStore.Current.CargoFilter;
        public string StatusText => _statusText != null ? _statusText.text : string.Empty;
        public string DetailName => _detailName != null && _detail.gameObject.activeInHierarchy ? _detailName.text : string.Empty;
        public string DetailBody => _detailBody != null && _detail.gameObject.activeInHierarchy ? _detailBody.text : string.Empty;
        public bool CityActive => _city != null && _city.gameObject.activeInHierarchy && _city.interactable;
        public bool DetailVisible => _detail != null && _detail.gameObject.activeInHierarchy;
        public bool ListVisible => _list != null && _list.gameObject.activeInHierarchy;
        public bool FixtureVisible => StatusText.IndexOf("FIXTURE", System.StringComparison.Ordinal) >= 0;
        public bool PriceVisible => DetailBody.IndexOf("LGC", System.StringComparison.Ordinal) >= 0;
        public bool TrendVisible => false;
        public float ListMinAnchorY => _list != null ? _list.anchorMin.y : 0f;
        public float BackWidth => _back != null ? ((RectTransform)_back.transform).rect.width : 0f;
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

        public static MapMarketView Attach(RectTransform panel)
        {
            if (panel == null)
                return null;
            var existing = panel.GetComponentInChildren<MapMarketView>(true);
            if (existing != null)
                return existing;
            var item = new GameObject("ManagementMarket", typeof(RectTransform));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(panel, false);
            var view = item.AddComponent<MapMarketView>();
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
            Present(MapMarketStore.Current);
            if (_placed)
                Layout();
        }

        public void Select(string conditionId)
        {
            MapMarketStore.Select(conditionId);
            if (!string.IsNullOrEmpty(conditionId))
                _detailPage = true;
            Refresh();
        }

        public void SetCargoFilter(string cargoId)
        {
            MapMarketStore.SetCargoFilter(cargoId ?? "");
            _detailPage = false;
            Refresh();
        }

        public void ShowList()
        {
            _detailPage = false;
            Refresh();
        }

        public bool TryFocusCity()
        {
            var snapshot = MapMarketStore.Current;
            if (!snapshot.TryGet(snapshot.SelectedId, out var item))
                return false;
            if (!MapCities.TryGet(item.CityId, out var city))
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

        public bool TryFocusGps()
        {
            var snapshot = MapMarketStore.Current;
            if (!snapshot.TryGet(snapshot.SelectedId, out var item) || !item.HasGps)
                return false;
            var shell = GetComponentInParent<MapCommandShell>();
            var rig = shell != null ? FindRig(shell) : null;
            if (rig == null)
                return false;
            rig.FocusWorldPoint(MapProjection.Project(item.Latitude, item.Longitude));
            return true;
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
            Present(MapMarketStore.Current);
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
            if (!_expanded && _shown.Status == MapMarketStatus.Ready && !string.IsNullOrEmpty(_shown.SelectedId))
                statusH = Mathf.Max(statusH, _font * 3.4f);
            _status.anchorMin = new Vector2(0f, 1f);
            _status.anchorMax = new Vector2(1f, 1f);
            _status.pivot = new Vector2(0.5f, 1f);
            _status.anchoredPosition = Vector2.zero;
            _status.sizeDelta = new Vector2(0f, statusH);
            _statusText.fontSize = Mathf.Max(11, Mathf.RoundToInt(_font));
            _statusText.alignment = TextAnchor.UpperLeft;

            var chipCount = 0;
            for (var i = 0; i < _catButtons.Length; i++)
            {
                if (_catButtons[i] != null && _catButtons[i].gameObject.activeSelf)
                    chipCount++;
            }

            var split = _expanded && root.rect.width * _scale >= 1200f;
            var stacked = _expanded && _detailPage && !split;
            var showCats = split && _shown.Status == MapMarketStatus.Ready && chipCount > 1 && !stacked;
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
                var shown = 0;
                for (var i = 0; i < _catButtons.Length; i++)
                {
                    if (_catButtons[i] == null || !_catButtons[i].gameObject.activeSelf)
                        continue;
                    var button = (RectTransform)_catButtons[i].transform;
                    button.anchorMin = new Vector2(shown / (float)chipCount, 0f);
                    button.anchorMax = new Vector2((shown + 1) / (float)chipCount, 1f);
                    button.offsetMin = new Vector2(shown == 0 ? 0f : gap * 0.5f, 0f);
                    button.offsetMax = new Vector2(shown == chipCount - 1 ? 0f : -gap * 0.5f, 0f);
                    _catLabels[i].fontSize = Mathf.Max(10, Mathf.RoundToInt(_font * 0.9f));
                    MapManagementLayout.ClipLabel(_catLabels[i]);
                    shown++;
                }
            }

            var ready = _shown.Status == MapMarketStatus.Ready;
            var showList = ready && !stacked;
            var showDetail = ready && !string.IsNullOrEmpty(_shown.SelectedId) && _expanded && (split || stacked);
            _list.gameObject.SetActive(showList);
            _detail.gameObject.SetActive(showDetail);
            _back.gameObject.SetActive(stacked);
            var topBlock = statusH + (showCats ? catH + 8f : 4f);
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
                        Mathf.Max(_touch * 2.4f, MapManagementLayout.ControlWidth(_backLabel.text, _font, _touch, 12f)),
                        Mathf.Max(_touch * 2.4f, root.rect.width * 0.55f)),
                    _touch);
                back.anchoredPosition = Vector2.zero;
            }

            var city = (RectTransform)_city.transform;
            city.anchorMin = new Vector2(0f, 0f);
            city.anchorMax = new Vector2(1f, 0f);
            city.pivot = new Vector2(0.5f, 0f);
            city.sizeDelta = new Vector2(0f, _touch);
            city.anchoredPosition = Vector2.zero;
            _cityLabel.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
            _detailName.fontSize = Mathf.Max(13, Mathf.RoundToInt(_titleSize));
            _detailBody.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
            _backLabel.fontSize = Mathf.Max(12, Mathf.RoundToInt(_font));
            var nameH = Mathf.Max(24f, _titleSize * 1.6f);
            _detailName.rectTransform.anchorMin = new Vector2(0f, 1f);
            _detailName.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailName.rectTransform.pivot = new Vector2(0f, 1f);
            _detailName.rectTransform.anchoredPosition = new Vector2(10f, -8f);
            _detailName.rectTransform.sizeDelta = new Vector2(-20f, nameH);
            _detailBody.rectTransform.anchorMin = new Vector2(0f, 0f);
            _detailBody.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailBody.rectTransform.offsetMin = new Vector2(10f, _touch + 8f);
            _detailBody.rectTransform.offsetMax = new Vector2(-10f, -(nameH + 16f));

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
            _status = Host("ManagementMarketStatus", root);
            _statusText = Label(_status, "Text", font, FontStyle.Normal, InkMuted);
            Inset(_statusText.rectTransform, 0f, 0f, 0f, 0f);
            _cats = Host("ManagementMarketCategories", root);
            for (var i = 0; i < _catButtons.Length; i++)
            {
                var slot = i;
                _catButtons[i] = Action(_cats, "ManagementMarketCat" + i, font, out _catLabels[i]);
                _catButtons[i].onClick.AddListener(() => SetCargoFilter(_catIds[slot]));
                _catButtons[i].gameObject.SetActive(false);
            }

            _list = Host("ManagementMarketList", root);
            var viewport = Host("ManagementMarketViewport", _list);
            var image = viewport.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            _content = Host("ManagementMarketContent", viewport);
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
                _rows[i] = Action(_content, "ManagementMarketRow" + i, font, out _rowTitle[i]);
                _rowMeta[i] = Label(_rows[i].transform, "Meta", font, FontStyle.Normal, InkMuted);
                _rowTitle[i].alignment = TextAnchor.MiddleLeft;
                _rowMeta[i].alignment = TextAnchor.MiddleLeft;
                Inset(_rowTitle[i].rectTransform, 10f, 22f, 4f, 8f);
                Inset(_rowMeta[i].rectTransform, 10f, 4f, 22f, 8f);
                _rows[i].onClick.AddListener(() => Select(_rowIds[slot]));
                _rows[i].gameObject.SetActive(false);
            }

            _detail = Host("ManagementMarketDetail", root);
            var detailImage = _detail.gameObject.AddComponent<Image>();
            detailImage.color = new Color(0.035f, 0.067f, 0.110f, 0.04f);
            detailImage.raycastTarget = true;
            _detailName = Label(_detail, "ManagementMarketDetailName", font, FontStyle.Bold, Ink);
            _detailBody = Label(_detail, "ManagementMarketDetailBody", font, FontStyle.Normal, InkMuted);
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
            _city = Action(_detail, "ManagementMarketCity", font, out _cityLabel);
            _city.onClick.AddListener(() => TryFocusCity());
            _back = Action(root, "ManagementMarketBack", font, out _backLabel);
            _back.onClick.AddListener(ShowList);
            _back.gameObject.SetActive(false);
            _detail.gameObject.SetActive(false);
        }

        void Present(MapMarketSnapshot snapshot)
        {
            _shown = snapshot;
            _statusText.text = StatusCopy(snapshot);
            FillCargos(snapshot);
            var shown = 0;
            for (var i = 0; i < MaxRows; i++)
            {
                MapMarketItem item;
                if (!NextInFilter(snapshot, ref shown, out item))
                {
                    _rows[i].gameObject.SetActive(false);
                    _rowIds[i] = "";
                    continue;
                }

                _rows[i].gameObject.SetActive(true);
                _rowIds[i] = item.ConditionId;
                _rowTitle[i].text = RowTitle(item);
                _rowMeta[i].text = RowMeta(item);
                _rows[i].GetComponent<Image>().color = item.ConditionId == snapshot.SelectedId ? RowSelected : Row;
            }

            MapMarketItem selected;
            if (snapshot.Status != MapMarketStatus.Ready || !snapshot.TryGet(snapshot.SelectedId, out selected))
            {
                _city.gameObject.SetActive(false);
                return;
            }

            _detailName.text = RowTitle(selected);
            _detailBody.text = DetailCopy(snapshot, selected);
            _city.gameObject.SetActive(selected.HasKnownCity);
            _city.interactable = selected.HasKnownCity;
            _cityLabel.text = Line(OpsContent.Table, OpsContent.MarketShowCity);
            _backLabel.text = Line(OpsContent.Table, OpsContent.MarketBack);
        }

        void FillCargos(MapMarketSnapshot snapshot)
        {
            _catIds[0] = "";
            _catLabels[0].text = Line(OpsContent.Table, OpsContent.MarketAll);
            _catButtons[0].gameObject.SetActive(snapshot.Status == MapMarketStatus.Ready);
            _catButtons[0].GetComponent<Image>().color = string.IsNullOrEmpty(snapshot.CargoFilter) ? ChipOn : ChipOff;
            var unique = 0;
            for (var i = 0; i < snapshot.Items.Length && unique < MaxCargos; i++)
            {
                var cargo = snapshot.Items[i].CargoId;
                if (string.IsNullOrEmpty(cargo))
                    continue;
                var seen = false;
                for (var u = 0; u < unique; u++)
                {
                    if (_catIds[u + 1] == cargo)
                        seen = true;
                }

                if (seen)
                    continue;
                unique++;
                _catIds[unique] = cargo;
                _catLabels[unique].text = cargo;
                _catButtons[unique].gameObject.SetActive(true);
                _catButtons[unique].GetComponent<Image>().color = snapshot.CargoFilter == cargo ? ChipOn : ChipOff;
            }

            for (var i = unique + 1; i < _catButtons.Length; i++)
                _catButtons[i].gameObject.SetActive(false);
        }

        static bool NextInFilter(MapMarketSnapshot snapshot, ref int index, out MapMarketItem item)
        {
            var seen = 0;
            for (var i = 0; i < snapshot.Items.Length; i++)
            {
                if (!snapshot.Items[i].InCargo(snapshot.CargoFilter))
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

        string StatusCopy(MapMarketSnapshot snapshot)
        {
            string text;
            switch (snapshot.Status)
            {
                case MapMarketStatus.Loading:
                    text = Line(OpsContent.Table, OpsContent.MarketLoading);
                    break;
                case MapMarketStatus.Error:
                    text = Line(OpsContent.Table, OpsContent.MarketError);
                    break;
                case MapMarketStatus.Empty:
                    text = Line(OpsContent.Table, OpsContent.MarketEmpty);
                    break;
                case MapMarketStatus.Ready:
                    text = snapshot.CountIn(snapshot.CargoFilter) == 0
                        ? Line(OpsContent.Table, OpsContent.MarketEmpty)
                        : Line(OpsContent.Table, OpsContent.MarketReady);
                    break;
                default:
                    text = Line(OpsContent.Table, OpsContent.BodyMarket);
                    break;
            }

            if (snapshot.Status == MapMarketStatus.Ready
                && !_expanded
                && snapshot.TryGet(snapshot.SelectedId, out var selected))
                text += "\n" + RowTitle(selected);
            if (snapshot.Synthetic)
                text = "FIXTURE\n" + text;
            return text;
        }

        string RowTitle(MapMarketItem item)
        {
            var city = CityCopy(item);
            if (string.IsNullOrEmpty(city) && string.IsNullOrEmpty(item.CargoId))
                return item.ConditionId;
            if (string.IsNullOrEmpty(city))
                return item.CargoId;
            if (string.IsNullOrEmpty(item.CargoId))
                return city;
            return city + " · " + item.CargoId;
        }

        string RowMeta(MapMarketItem item)
        {
            if (item.HasAdjustment)
                return Line(OpsContent.Table, OpsContent.MarketAdjustment) + " " + item.AdjustmentText;
            return Line(OpsContent.Table, OpsContent.MarketPriceUnavailable);
        }

        string DetailCopy(MapMarketSnapshot snapshot, MapMarketItem item)
        {
            var text = Line(OpsContent.Table, OpsContent.MarketCondition);
            var city = CityCopy(item);
            if (!string.IsNullOrEmpty(city))
                text += "\n" + Line(OpsContent.Table, OpsContent.MarketLocation) + " " + city;
            else
                text += "\n" + Line(OpsContent.Table, OpsContent.MarketCityUnknown);
            if (!string.IsNullOrEmpty(item.CargoId))
                text += "\n" + Line(OpsContent.Table, OpsContent.MarketCargo) + " " + item.CargoId;
            text += "\n" + Line(OpsContent.Table, OpsContent.MarketPriceUnavailable);
            if (item.HasAdjustment)
                text += "\n" + Line(OpsContent.Table, OpsContent.MarketAdjustment) + " " + item.AdjustmentText;
            if (item.HasDemandIndex)
                text += "\n" + Line(OpsContent.Table, OpsContent.MarketDemandIndex) + " " + item.DemandIndex;
            if (item.HasSupplyIndex)
                text += "\n" + Line(OpsContent.Table, OpsContent.MarketSupplyIndex) + " " + item.SupplyIndex;
            if (snapshot.HasPeriodEnd)
                text += "\n" + snapshot.PeriodEndsAt;
            return text;
        }

        string CityCopy(MapMarketItem item)
        {
            if (!item.HasKnownCity)
                return "";
            return Line(MapContent.Table, MapContent.CityKey(item.CityId));
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
            MapPremiumUi.StyleSurface(image, MapPremiumSurface.Card, true);
            image.color = Row;
            var button = item.GetComponent<Button>();
            button.targetGraphic = image;
            label = Label(item.transform, "Label", font, FontStyle.Bold, Ink);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            Inset(label.rectTransform, 4f, 0f, 0f, 4f);
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
