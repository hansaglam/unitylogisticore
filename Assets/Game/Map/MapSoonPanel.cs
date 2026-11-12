using System.Collections.Generic;
using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// One reusable Coming Soon card for transport rail entries. No gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapSoonPanel : MonoBehaviour
    {
        static readonly Color Surface = new Color(0.027f, 0.055f, 0.094f, 0.96f);
        static readonly Color Raised = new Color(0.055f, 0.102f, 0.138f, 1f);
        static readonly Color Dim = new Color(0.01f, 0.03f, 0.05f, 0.35f);

        RectTransform _blocker;
        RectTransform _card;
        Button _close;
        Image _closeIcon;
        Text _title;
        Text _body;
        Text _mode;
        bool _built;
        bool _listening;
        int _railIndex = -1;
        float _placedScale = 1f;
        readonly List<CopyBind> _binds = new List<CopyBind>(4);
        readonly string[] _modes = new string[4];
        string _titleCopy = string.Empty;
        string _bodyCopy = string.Empty;
        string _closeCopy = string.Empty;

        public bool IsOpen => _card != null && _card.gameObject.activeSelf;
        public int RailIndex => _railIndex;
        public Button CloseButton => _close;
        public string TitleText => _title != null ? _title.text : string.Empty;
        public string BodyText => _body != null ? _body.text : string.Empty;
        public string ModeText => _mode != null ? _mode.text : string.Empty;
        public Sprite CloseSprite => _closeIcon != null ? _closeIcon.sprite : null;
        public RectTransform CardRect => _card;
        public RectTransform BlockerRect => _blocker;

        public static MapSoonPanel Attach(MapCommandShell shell)
        {
            var existing = shell.GetComponentInChildren<MapSoonPanel>(true);
            if (existing != null)
                return existing;

            var item = new GameObject("MapSoonPanel", typeof(RectTransform), typeof(MapSoonPanel));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(shell.transform, false);
            var panel = item.GetComponent<MapSoonPanel>();
            panel.BuildIfNeeded();
            return panel;
        }

        public void Show(int railIndex)
        {
            BuildIfNeeded();
            Hook();
            _railIndex = railIndex;
            _card.gameObject.SetActive(true);
            if (_blocker != null)
                _blocker.gameObject.SetActive(true);
            Present();
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            _railIndex = -1;
            if (_card != null)
                _card.gameObject.SetActive(false);
            if (_blocker != null)
                _blocker.gameObject.SetActive(false);
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
            _placedScale = scale;
            var touch = MapCommandLayout.TouchPixels(screenWidth, screenHeight);
            var canvasTouch = touch / scale;
            var pad = Mathf.Max(10f, metrics.Gap / scale);
            var width = Mathf.Clamp(metrics.Viewport.width * 0.42f, touch * 4.2f, metrics.Viewport.width - 16f);
            var height = Mathf.Max(touch * 3.4f, metrics.FontPixels * 8f);
            if (height > metrics.Viewport.height - 16f)
                height = Mathf.Max(touch * 2.6f, metrics.Viewport.height - 16f);
            var x = metrics.Viewport.xMax - 8f - width;
            if (x < metrics.Viewport.xMin + 8f)
                x = metrics.Viewport.xMin + 8f;
            var y = metrics.Viewport.yMin + (metrics.Viewport.height - height) * 0.5f;
            var frame = new Rect(x, y, width, height);
            MapManagementLayout.Place(_card, frame, origin, scale);
            if (_blocker != null)
                MapManagementLayout.Place(_blocker, metrics.Viewport, origin, scale);

            var close = (RectTransform)_close.transform;
            close.anchorMin = new Vector2(1f, 1f);
            close.anchorMax = new Vector2(1f, 1f);
            close.pivot = new Vector2(1f, 1f);
            close.sizeDelta = new Vector2(canvasTouch, canvasTouch);
            close.anchoredPosition = new Vector2(-pad, -pad);
            if (_closeIcon != null)
            {
                var icon = (RectTransform)_closeIcon.transform;
                var size = canvasTouch * 0.42f;
                icon.anchorMin = new Vector2(0.5f, 0.5f);
                icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.pivot = new Vector2(0.5f, 0.5f);
                icon.anchoredPosition = Vector2.zero;
                icon.sizeDelta = new Vector2(size, size);
            }

            if (_title != null)
            {
                _title.fontSize = metrics.CanvasTitle;
                Stretch(_title.rectTransform, pad, pad + canvasTouch + 4f, pad + canvasTouch, metrics.CanvasTitle * 1.4f);
            }

            if (_mode != null)
            {
                _mode.fontSize = metrics.CanvasFont;
                Stretch(_mode.rectTransform, pad, pad + canvasTouch + metrics.CanvasTitle * 1.5f + 4f, pad, metrics.CanvasFont * 1.3f);
            }

            if (_body != null)
            {
                _body.fontSize = metrics.CanvasFont;
                var top = pad + canvasTouch + metrics.CanvasTitle * 1.5f + metrics.CanvasFont * 1.4f + 8f;
                var body = _body.rectTransform;
                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.one;
                body.offsetMin = new Vector2(pad, pad);
                body.offsetMax = new Vector2(-pad, -top);
            }
        }

        public Rect CloseHitPixels()
        {
            return MapManagementLayout.HitRect((RectTransform)_close.transform, _placedScale);
        }

        public bool ContainsScreenPoint(Vector2 screen)
        {
            if (!IsOpen)
                return false;

            if (_blocker != null && _blocker.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(_blocker, screen, null))
                return true;

            return _card != null && RectTransformUtility.RectangleContainsScreenPoint(_card, screen, null);
        }

        void OnEnable()
        {
            Hook();
            Subscribe();
        }

        void OnDisable()
        {
            Unhook();
            Unsubscribe();
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
            _blocker = Graphic("SoonBlocker", transform, Dim, true);
            _card = Graphic("SoonCard", transform, Surface, true);
            var stroke = new GameObject("Stroke", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            stroke.hideFlags = HideFlags.DontSave;
            stroke.transform.SetParent(_card, false);
            var edge = stroke.GetComponent<Image>();
            edge.color = MapHudPalette.Rule;
            edge.raycastTarget = false;
            var edgeRect = (RectTransform)stroke.transform;
            edgeRect.anchorMin = Vector2.zero;
            edgeRect.anchorMax = new Vector2(0f, 1f);
            edgeRect.pivot = new Vector2(0f, 0.5f);
            edgeRect.sizeDelta = new Vector2(2f, 0f);
            _title = Label(_card, "SoonTitle", font, FontStyle.Bold, MapHudPalette.Text);
            _mode = Label(_card, "SoonMode", font, FontStyle.Bold, MapHudPalette.Accent);
            _body = Label(_card, "SoonBody", font, FontStyle.Normal, MapHudPalette.TextMuted);
            _close = IconClose(_card, font, out _closeIcon);
            Track(MapContent.ModeSoon, value => _titleCopy = value);
            Track(MapContent.SoonBody, value => _bodyCopy = value);
            Track(MapContent.Close, value => _closeCopy = value);
            Track(MapContent.ModeRoad, value => _modes[0] = value);
            Track(MapContent.ModeRail, value => _modes[1] = value);
            Track(MapContent.ModeSea, value => _modes[2] = value);
            Track(MapContent.ModeAir, value => _modes[3] = value);
            Hide();
        }

        void Present()
        {
            _titleCopy = Localized(MapContent.ModeSoon, _titleCopy);
            _bodyCopy = Localized(MapContent.SoonBody, _bodyCopy);
            _modes[0] = Localized(MapContent.ModeRoad, _modes[0]);
            _modes[1] = Localized(MapContent.ModeRail, _modes[1]);
            _modes[2] = Localized(MapContent.ModeSea, _modes[2]);
            _modes[3] = Localized(MapContent.ModeAir, _modes[3]);
            if (_title != null)
                _title.text = _titleCopy ?? string.Empty;
            if (_body != null)
                _body.text = _bodyCopy ?? string.Empty;
            var slot = _railIndex - MapModuleIds.FirstTransport;
            if (_mode != null)
                _mode.text = slot >= 0 && slot < _modes.Length ? _modes[slot] ?? string.Empty : string.Empty;
            if (_closeIcon != null)
            {
                _closeIcon.sprite = MapPanelIcons.Get(MapPanelIcons.Close);
                _closeIcon.color = MapHudPalette.Accent;
            }
        }

        void Hook()
        {
            if (_listening)
                return;
            if (_close != null)
                _close.onClick.AddListener(RequestClose);
            _listening = true;
        }

        void Unhook()
        {
            if (!_listening)
                return;
            if (_close != null)
                _close.onClick.RemoveListener(RequestClose);
            _listening = false;
        }

        void RequestClose()
        {
            var shell = GetComponentInParent<MapCommandShell>();
            if (shell != null)
                shell.CloseSoon();
            else
                Hide();
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

        static string Localized(string key, string fallback)
        {
            var database = LocalizationSettings.StringDatabase;
            var locale = LocalizationSettings.SelectedLocale;
            if (database == null || locale == null || string.IsNullOrEmpty(key))
                return fallback ?? string.Empty;

            var table = database.GetTable(MapContent.Table, locale) as UnityEngine.Localization.Tables.StringTable;
            var entry = table != null ? table.GetEntry(key) : null;
            if (entry == null)
                return fallback ?? string.Empty;

            var text = entry.GetLocalizedString();
            return string.IsNullOrWhiteSpace(text) ? (fallback ?? string.Empty) : text;
        }

        void Track(string key, System.Action<string> extra)
        {
            _binds.Add(new CopyBind(MapContent.Table, key, extra));
        }

        static void Stretch(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(-(left + right), height);
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

        static Text Label(RectTransform parent, string name, Font font, FontStyle style, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>();
            text.font = font;
            text.fontStyle = style;
            text.alignment = TextAnchor.UpperLeft;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static Button IconClose(RectTransform parent, Font font, out Image icon)
        {
            var item = new GameObject("SoonClose", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
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
            icon.sprite = MapPanelIcons.Get(MapPanelIcons.Close);
            icon.color = MapHudPalette.Accent;
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            var access = Label(item.GetComponent<RectTransform>(), "Access", font, FontStyle.Bold, MapHudPalette.Text);
            access.enabled = false;
            return button;
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
                _source.RefreshString();
            }

            public void Unsubscribe()
            {
                if (!_subscribed)
                    return;
                _source.StringChanged -= OnChanged;
                _subscribed = false;
            }

            void OnChanged(string value)
            {
                _extra?.Invoke(value ?? string.Empty);
            }
        }
    }
}
