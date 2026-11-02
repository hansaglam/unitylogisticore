using LogistiCore.Localization;
using LogistiCore.UI;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// The map's only persistent control. It is created at runtime on the safe area
    /// and floats in the upper-left. World, Türkiye, zoom, and restore stay on
    /// <see cref="MapCameraRig"/>; the buttons that used to call them are gone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapHomeReturn : MonoBehaviour
    {
        Text _label;
        Text _mark;
        Button _button;
        RectTransform _rect;
        LocalizedString _source;
        bool _built;
        bool _subscribed;
        bool _listening;
        int _width = -1;
        int _height = -1;
        Rect _safe;
        bool _placed;
        Rect _screenRect;

        public Rect ScreenRect => _screenRect;
        public bool Placed => _placed;
        public Button Button => _button;

        public static MapHomeReturn Attach(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            if (scene.path != AppNavigation.MapScenePath && scene.name != AppNavigation.MapSceneName)
                return null;

            var roots = scene.GetRootGameObjects();
            Canvas canvas = null;
            RectTransform parent = null;
            for (var i = 0; i < roots.Length; i++)
            {
                var existing = roots[i].GetComponentInChildren<MapHomeReturn>(true);
                if (existing != null)
                    return existing;

                if (canvas == null)
                    canvas = roots[i].GetComponentInChildren<Canvas>(true);
            }

            if (canvas == null)
                return null;

            var safe = canvas.GetComponentInChildren<SafeAreaRect>(true);
            parent = safe != null ? safe.transform as RectTransform : canvas.transform as RectTransform;
            if (parent == null)
                return null;

            var item = new GameObject(
                "HomeReturn",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(MapHomeReturn));
            item.hideFlags = HideFlags.DontSave;
            if (item.scene != scene)
                SceneManager.MoveGameObjectToScene(item, scene);
            item.transform.SetParent(parent, false);
            var view = item.GetComponent<MapHomeReturn>();
            view.BuildIfNeeded();
            view.Apply(Screen.width, Screen.height, Screen.safeArea);
            view.EnsureHooked();
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

            EnsureHooked();
            ApplyCurrent();
        }

        void OnDisable()
        {
            if (_button != null && _listening)
            {
                _button.onClick.RemoveListener(OpenHome);
                _listening = false;
            }

            if (_source != null && _subscribed)
            {
                _source.StringChanged -= OnLabel;
                _subscribed = false;
            }
        }

        void OnDestroy()
        {
            if (_source != null)
                ((System.IDisposable)_source).Dispose();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            ApplyCurrent();
        }

        public void OpenHome()
        {
            AppNavigation.ShowHome();
        }

        public void Apply(float screenWidth, float screenHeight, Rect safeArea)
        {
            _placed = MapHomeReturnLayout.TryPlace(screenWidth, screenHeight, safeArea, out _screenRect);
            // Keep this object enabled. Deactivating it would drop the click callback
            // and stop LateUpdate, so a portrait-sized first frame could never recover
            // after the phone rotates to landscape.
            ShowGraphic(_placed);
            if (!_placed || _rect == null)
            {
                _width = screenWidth < 1f ? 0 : Mathf.RoundToInt(screenWidth);
                _height = screenHeight < 1f ? 0 : Mathf.RoundToInt(screenHeight);
                _safe = safeArea;
                return;
            }

            var safe = safeArea.width < 1f || safeArea.height < 1f
                ? new Rect(0f, 0f, screenWidth, screenHeight)
                : safeArea;
            var scale = MapHudLayout.CanvasScale(screenWidth, screenHeight);
            if (scale < 0.0001f)
                scale = 1f;

            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.zero;
            _rect.pivot = Vector2.zero;
            _rect.anchoredPosition = new Vector2((_screenRect.x - safe.xMin) / scale, (_screenRect.y - safe.yMin) / scale);
            _rect.sizeDelta = new Vector2(_screenRect.width / scale, _screenRect.height / scale);
            if (_label != null)
                _label.fontSize = MapHomeReturnLayout.LabelCanvasSize;
            if (_mark != null)
                _mark.fontSize = MapHomeReturnLayout.LabelCanvasSize;

            _width = screenWidth < 1f ? 0 : Mathf.RoundToInt(screenWidth);
            _height = screenHeight < 1f ? 0 : Mathf.RoundToInt(screenHeight);
            _safe = safeArea;
        }

        void BuildIfNeeded()
        {
            if (_built)
                return;

            _built = true;
            var item = gameObject;
            _rect = (RectTransform)item.transform;
            var image = item.GetComponent<Image>();
            image.color = new Color(MapHudPalette.Button.r, MapHudPalette.Button.g, MapHudPalette.Button.b, 0.88f);
            image.raycastTarget = true;
            _button = item.GetComponent<Button>();
            _button.targetGraphic = image;
            var colors = _button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = MapHudPalette.Highlighted;
            colors.pressedColor = MapHudPalette.Pressed;
            colors.selectedColor = MapHudPalette.ButtonActive;
            colors.fadeDuration = 0.08f;
            _button.colors = colors;
            var accent = new Color(MapHudPalette.Accent.r, MapHudPalette.Accent.g, MapHudPalette.Accent.b, 0.85f);
            Edge(item.transform, "EdgeTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f), accent);
            Edge(item.transform, "EdgeBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), accent);
            Edge(item.transform, "EdgeLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f), accent);
            Edge(item.transform, "EdgeRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f), accent);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.hideFlags = HideFlags.DontSave;
            labelObject.transform.SetParent(item.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(40f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
            _label = labelObject.GetComponent<Text>();
            _label.font = font;
            _label.fontStyle = FontStyle.Bold;
            _label.alignment = TextAnchor.MiddleLeft;
            _label.color = MapHudPalette.Text;
            _label.raycastTarget = false;
            _label.supportRichText = false;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.text = string.Empty;
            _mark = Glyph(item.transform, "Mark", font, accent);
            var markRect = _mark.rectTransform;
            markRect.anchorMin = new Vector2(0f, 0f);
            markRect.anchorMax = new Vector2(0f, 1f);
            markRect.pivot = new Vector2(0f, 0.5f);
            markRect.sizeDelta = new Vector2(36f, 0f);
            markRect.anchoredPosition = new Vector2(10f, 0f);
            _source = new LocalizedString(ShellContent.Table, ShellContent.Home);
            _rect.SetAsLastSibling();
        }

        static void Edge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Color color)
        {
            var edge = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            edge.hideFlags = HideFlags.DontSave;
            edge.transform.SetParent(parent, false);
            var rect = (RectTransform)edge.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            var image = edge.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static Text Glyph(Transform parent, string name, Font font, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>();
            text.font = font;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = "\u2190";
            return text;
        }

        void ApplyCurrent()
        {
            var width = Screen.width;
            var height = Screen.height;
            var safe = Screen.safeArea;
            if (_width == width && _height == height && _safe == safe)
                return;

            Apply(width, height, safe);
        }

        internal void EnsureHooked()
        {
            if (!Application.isPlaying)
                return;

            if (_button != null && !_listening)
            {
                _button.onClick.AddListener(OpenHome);
                _listening = true;
            }

            if (_source != null && !_subscribed)
            {
                _source.StringChanged += OnLabel;
                _subscribed = true;
            }
        }

        void ShowGraphic(bool visible)
        {
            if (_button != null)
                _button.interactable = visible;
            var image = _button != null ? _button.GetComponent<Image>() : null;
            if (image != null)
                image.enabled = visible;
            if (_label != null)
                _label.enabled = visible;
            if (_mark != null)
                _mark.enabled = visible;
        }

        void OnLabel(string value)
        {
            if (_label != null)
                _label.text = value ?? string.Empty;
        }
    }
}
