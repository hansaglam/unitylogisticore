using LogistiCore.Localization;
using LogistiCore.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// Pooled country names for the world map. Objects are created once and are not
    /// saved into the scene. Text is assigned only when the country or the language
    /// changes. Pan updates positions of the retained objects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapCountryLabelLayer : MonoBehaviour
    {
        public readonly struct Placed
        {
            public readonly string Code;
            public readonly string Text;
            public readonly Vector2 Screen;
            public readonly Rect Bounds;
            public readonly bool Raycast;

            public Placed(string code, string text, Vector2 screen, Rect bounds, bool raycast)
            {
                Code = code;
                Text = text;
                Screen = screen;
                Bounds = bounds;
                Raycast = raycast;
            }
        }

        public readonly struct Caption
        {
            public readonly bool Visible;
            public readonly string Text;
            public readonly Vector2 Screen;
            public readonly Rect Bounds;
            public readonly int Side;
            public readonly bool Raycast;

            public Caption(bool visible, string text, Vector2 screen, Rect bounds, int side, bool raycast)
            {
                Visible = visible;
                Text = text;
                Screen = screen;
                Bounds = bounds;
                Side = side;
                Raycast = raycast;
            }
        }

        const int CountryCount = 242;

        RectTransform _root;
        Canvas _canvas;
        Text[] _texts;
        int[] _slotCountry;
        Vector2[] _world;
        Vector2[] _screen;
        float[] _measured;
        Text _measure;
        int _measuredFont = -1;
        float _measuredScale = -1f;
        string _measuredLocale = string.Empty;
        int[] _chosen;
        Rect[] _occupied;
        Vector2[] _resolved;
        Vector2[] _heldNudge;
        MapScope _heldScope;
        Rect[] _obstacles;
        bool[] _cityTogether;
        int[] _cityParent;
        int _count;
        float _heldZoom = -1f;
        float _heldSize = -1f;
        string _locale = string.Empty;
        int _fontSize = -1;
        bool _built;
        Text _caption;
        int _captionSide = -1;
        string _captionLocale = string.Empty;
        Vector2 _captionScreen;
        Rect _captionBounds;
        bool _captionVisible;

        public int VisibleCount => _count;
        public float Zoom => _heldZoom;

        public Caption AggregateCaption => new Caption(
            _captionVisible,
            _caption != null ? _caption.text : string.Empty,
            _captionScreen,
            _captionBounds,
            _captionSide,
            _caption != null && _caption.raycastTarget);

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

        public static MapCountryLabelLayer Attach(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            if (scene.path != AppNavigation.MapScenePath && scene.name != AppNavigation.MapSceneName)
                return null;

            var roots = scene.GetRootGameObjects();
            Canvas canvas = null;
            for (var i = 0; i < roots.Length; i++)
            {
                var existing = roots[i].GetComponentInChildren<MapCountryLabelLayer>(true);
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

            var item = new GameObject("CountryLabels", typeof(RectTransform));
            item.hideFlags = HideFlags.DontSave;
            item.SetActive(false);
            var layer = item.AddComponent<MapCountryLabelLayer>();
            if (item.scene != scene)
                SceneManager.MoveGameObjectToScene(item, scene);
            item.transform.SetParent(parent, false);
            item.SetActive(true);
            return layer;
        }

        void Awake()
        {
            Build();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || _canvas == null)
                return;

            var camera = Camera;
            if (camera == null)
                return;

            var shell = _canvas.GetComponentInChildren<MapCommandShell>(true);
            Refresh(Screen.width, Screen.height, Screen.safeArea, LocaleService.CurrentCode, shell != null && shell.ContextOpen);
        }

        public int CopyPlaced(Placed[] destination)
        {
            if (destination == null || _texts == null)
                return 0;

            var count = _count < destination.Length ? _count : destination.Length;
            for (var i = 0; i < count; i++)
            {
                var text = _texts[i];
                var country = _slotCountry[i];
                var code = country >= 0 && country < MapCountryCatalog.All.Length
                    ? MapCountryCatalog.All[country].Code
                    : string.Empty;
                destination[i] = new Placed(
                    code,
                    text != null ? text.text : string.Empty,
                    country >= 0 && _resolved != null && country < _resolved.Length ? _resolved[country] : Vector2.zero,
                    _occupied != null && i < _occupied.Length ? _occupied[i] : new Rect(),
                    text != null && text.raycastTarget);
            }

            return count;
        }

        public int Refresh(int width, int height, Rect safe, string locale, bool contextOpen)
        {
            Build();
            if (_texts == null || width < 2 || height < 2)
            {
                _count = 0;
                HideAll();
                return 0;
            }

            var camera = Camera;
            if (camera == null)
            {
                _count = 0;
                HideAll();
                return 0;
            }

            var aspect = width / (float)height;
            var size = camera.orthographicSize;
            var center = new Vector2(camera.transform.position.x, camera.transform.position.y);
            var worldSize = MapFrames.MaximumOrthographicSize(aspect);
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkeyFrame);
            var rawZoom = MapCountryLabels.Zoom(size, worldSize, turkeyFrame);
            var zoom = MapCountryLabels.HoldZoom(_heldZoom, _heldSize, size, rawZoom);
            _heldZoom = zoom;
            _heldSize = size;

            var metrics = MapCommandLayout.Evaluate(width, height, safe, contextOpen);
            var scale = MapHudLayout.CanvasScale(width, height);
            if (scale < 0.05f)
                scale = 1f;
            var fontSize = MapMarkerLayer.LabelFontSize(scale, false);
            var canvasScale = _canvas != null && _canvas.scaleFactor > 0.05f ? _canvas.scaleFactor : scale;
            var glyph = fontSize * canvasScale * MapCountryLabels.GlyphFactor;
            var line = fontSize * canvasScale * MapCountryLabels.LineFactor;
            var units = MapMarkerLod.UnitsPerPixel(size, height);
            for (var i = 0; i < _world.Length; i++)
                _screen[i] = MapMarkerLod.ToScreen(_world[i], center, units, width, height);

            var language = string.IsNullOrEmpty(locale) ? LocaleCodes.English : locale;
            EnsureWidths(language, fontSize, canvasScale);
            var rig = camera.GetComponent<MapCameraRig>();
            var scope = rig != null ? rig.Scope : MapScope.World;
            if (scope != _heldScope)
            {
                _heldScope = scope;
                if (_heldNudge != null)
                    System.Array.Clear(_heldNudge, 0, _heldNudge.Length);
            }

            _count = MapCountryLabels.Choose(
                zoom,
                language,
                _screen,
                glyph,
                _measured,
                line,
                metrics.Viewport,
                _chosen,
                _occupied,
                scope);
            var obstacleCount = MapCountryLabels.CityObstacles(
                center,
                size,
                width,
                height,
                turkeyFrame,
                canvasScale,
                _cityTogether,
                _cityParent,
                _obstacles);
            var maxNudge = units > 0.000001f ? MapCountryLabels.MaxNudgeWorld / units : 0f;
            _count = MapCountryLabels.AvoidObstacles(
                _count,
                _chosen,
                _screen,
                language,
                glyph,
                _measured,
                line,
                metrics.Viewport,
                _occupied,
                _obstacles,
                obstacleCount,
                maxNudge,
                _resolved,
                _heldNudge);
            var languageChanged = _locale != language;
            var fontChanged = _fontSize != fontSize;
            _locale = language;
            _fontSize = fontSize;
            var eventCamera = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera
                ? _canvas.worldCamera
                : null;
            PlaceAggregateCaption(language, fontSize, canvasScale, line, metrics.Viewport, center, units, width, height, eventCamera);

            for (var slot = 0; slot < _texts.Length; slot++)
            {
                var text = _texts[slot];
                if (text == null)
                    continue;

                if (slot >= _count)
                {
                    if (text.gameObject.activeSelf)
                        text.gameObject.SetActive(false);
                    _slotCountry[slot] = -1;
                    continue;
                }

                var country = _chosen[slot];
                var entry = MapCountryCatalog.All[country];
                if (!text.gameObject.activeSelf)
                    text.gameObject.SetActive(true);
                if (fontChanged)
                    text.fontSize = fontSize;
                if (languageChanged || _slotCountry[slot] != country)
                {
                    text.text = MapCountryLabels.Name(entry, language);
                    _slotCountry[slot] = country;
                }

                var textWidth = _measured != null && country < _measured.Length ? _measured[country] : line;
                text.rectTransform.sizeDelta = new Vector2(textWidth / canvasScale, line / canvasScale);
                var point = _resolved != null && country < _resolved.Length ? _resolved[country] : _screen[country];
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, point, eventCamera, out var local))
                    text.rectTransform.anchoredPosition = local;
            }

            return _count;
        }

        void PlaceAggregateCaption(
            string language,
            int fontSize,
            float canvasScale,
            float line,
            Rect viewport,
            Vector2 cameraCenter,
            float units,
            int width,
            int height,
            Camera eventCamera)
        {
            if (_caption == null)
                return;

            var full = MapCountryLabels.IsFullAggregate(_cityParent, MapCities.All.Length);
            var badge = Vector2.zero;
            if (full)
            {
                full = MapCountryLabels.TryWorldAggregateScreen(
                    cameraCenter,
                    units,
                    width,
                    height,
                    _cityParent,
                    out badge);
            }

            var turkey = MapCountryLabels.TurkeyIndex;
            var turkeyShown = false;
            if (turkey >= 0)
            {
                for (var i = 0; i < _count; i++)
                {
                    if (_chosen[i] == turkey)
                    {
                        turkeyShown = true;
                        break;
                    }
                }
            }

            if (!MapCountryLabels.AggregateCaptionApplies(full, turkeyShown))
            {
                HideCaption();
                return;
            }

            var captionWidth = line;
            if (_measured != null && turkey >= 0 && turkey < _measured.Length && _measured[turkey] > 1f)
                captionWidth = _measured[turkey];

            var side = MapCountryLabels.PlaceAggregateCaption(
                badge,
                captionWidth,
                line,
                viewport,
                _occupied,
                _count,
                _captionSide,
                out _captionScreen,
                out _captionBounds);
            if (side < 0)
            {
                _captionSide = -1;
                HideCaption();
                return;
            }

            _captionSide = side;
            _captionVisible = true;
            if (!_caption.gameObject.activeSelf)
                _caption.gameObject.SetActive(true);
            if (_caption.fontSize != fontSize)
                _caption.fontSize = fontSize;
            if (_captionLocale != language && turkey >= 0)
            {
                _caption.text = MapCountryLabels.Name(MapCountryCatalog.All[turkey], language);
                _captionLocale = language;
            }

            _caption.rectTransform.sizeDelta = new Vector2(captionWidth / canvasScale, line / canvasScale);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, _captionScreen, eventCamera, out var local))
                _caption.rectTransform.anchoredPosition = local;
        }

        void HideCaption()
        {
            _captionVisible = false;
            _captionScreen = Vector2.zero;
            _captionBounds = new Rect();
            if (_caption != null && _caption.gameObject.activeSelf)
                _caption.gameObject.SetActive(false);
        }

        Camera Camera
        {
            get
            {
                var scene = gameObject.scene;
                if (!scene.IsValid())
                    return null;

                var roots = scene.GetRootGameObjects();
                for (var i = 0; i < roots.Length; i++)
                {
                    var rig = roots[i].GetComponentInChildren<MapCameraRig>(true);
                    if (rig != null)
                        return rig.GetComponent<Camera>();
                }

                return null;
            }
        }

        void Build()
        {
            if (_built)
                return;

            _canvas = GetComponentInParent<Canvas>();
            _root = transform as RectTransform;
            if (_root == null)
                return;

            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);

            var countries = MapCountryCatalog.All;
            _world = new Vector2[countries.Length];
            _screen = new Vector2[countries.Length];
            _measured = new float[countries.Length];
            for (var i = 0; i < countries.Length; i++)
            {
                if (!MapProjection.TryProject(countries[i].Latitude, countries[i].Longitude, out _world[i]))
                    _world[i] = new Vector2(float.NaN, float.NaN);
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var measureObject = new GameObject("CountryMeasure", typeof(RectTransform));
            measureObject.hideFlags = HideFlags.DontSave;
            measureObject.transform.SetParent(_root, false);
            _measure = measureObject.AddComponent<Text>();
            _measure.font = font;
            _measure.fontStyle = FontStyle.Bold;
            _measure.raycastTarget = false;
            _measure.color = new Color(0f, 0f, 0f, 0f);
            measureObject.SetActive(false);
            _texts = new Text[MapCountryLabels.Capacity];
            _slotCountry = new int[MapCountryLabels.Capacity];
            _chosen = new int[MapCountryLabels.Capacity];
            _occupied = new Rect[MapCountryLabels.Capacity];
            _resolved = new Vector2[countries.Length];
            _heldNudge = new Vector2[countries.Length];
            _obstacles = new Rect[MapCities.All.Length * 2];
            _cityTogether = new bool[MapCities.All.Length * MapCities.All.Length];
            _cityParent = new int[MapCities.All.Length];
            for (var i = 0; i < _texts.Length; i++)
            {
                _slotCountry[i] = -1;
                var item = new GameObject("CountryName", typeof(RectTransform));
                item.hideFlags = HideFlags.DontSave;
                item.transform.SetParent(_root, false);
                var rect = item.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var text = item.AddComponent<Text>();
                text.font = font;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(0.96f, 0.98f, 1f, 1f);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                text.supportRichText = false;
                var shadow = item.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.02f, 0.05f, 0.08f, 0.92f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);
                shadow.useGraphicAlpha = true;
                item.SetActive(false);
                _texts[i] = text;
            }

            var captionObject = new GameObject("AggregateCountry", typeof(RectTransform));
            captionObject.hideFlags = HideFlags.DontSave;
            captionObject.transform.SetParent(_root, false);
            var captionRect = captionObject.GetComponent<RectTransform>();
            captionRect.anchorMin = new Vector2(0.5f, 0.5f);
            captionRect.anchorMax = new Vector2(0.5f, 0.5f);
            captionRect.pivot = new Vector2(0.5f, 0.5f);
            _caption = captionObject.AddComponent<Text>();
            _caption.font = font;
            _caption.fontStyle = FontStyle.Bold;
            _caption.alignment = TextAnchor.MiddleCenter;
            _caption.color = new Color(0.96f, 0.98f, 1f, 1f);
            _caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            _caption.verticalOverflow = VerticalWrapMode.Overflow;
            _caption.raycastTarget = false;
            _caption.supportRichText = false;
            var captionShadow = captionObject.AddComponent<Shadow>();
            captionShadow.effectColor = new Color(0.02f, 0.05f, 0.08f, 0.92f);
            captionShadow.effectDistance = new Vector2(1.5f, -1.5f);
            captionShadow.useGraphicAlpha = true;
            captionObject.SetActive(false);

            _built = true;
            if (countries.Length != CountryCount)
                Debug.LogWarning("Country catalog length is " + countries.Length + ".");
        }

        void EnsureWidths(string language, int fontSize, float canvasScale)
        {
            if (_measure == null || _measured == null)
                return;
            if (_measuredFont == fontSize && _measuredLocale == language && Mathf.Abs(_measuredScale - canvasScale) < 0.02f)
                return;

            _measure.gameObject.SetActive(true);
            _measure.fontSize = fontSize;
            var countries = MapCountryCatalog.All;
            for (var i = 0; i < countries.Length && i < _measured.Length; i++)
            {
                var name = MapCountryLabels.Name(countries[i], language);
                _measure.text = name;
                if (_measure.font != null && !string.IsNullOrEmpty(name))
                    _measure.font.RequestCharactersInTexture(name, fontSize, _measure.fontStyle);
                var width = _measure.preferredWidth * canvasScale + MapCountryLabels.LabelPad;
                if (width < 8f)
                    width = MapCountryLabels.Width(name, fontSize * canvasScale * MapCountryLabels.GlyphFactor);
                _measured[i] = width;
            }

            _measure.text = string.Empty;
            _measure.gameObject.SetActive(false);
            _measuredFont = fontSize;
            _measuredScale = canvasScale;
            _measuredLocale = language;
        }

        void HideAll()
        {
            if (_texts == null)
                return;

            for (var i = 0; i < _texts.Length; i++)
            {
                if (_texts[i] != null && _texts[i].gameObject.activeSelf)
                    _texts[i].gameObject.SetActive(false);
                if (_slotCountry != null)
                    _slotCountry[i] = -1;
            }

            HideCaption();
        }
    }
}
