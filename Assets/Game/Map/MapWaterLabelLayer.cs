using LogistiCore.Localization;
using LogistiCore.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// Pooled sea and ocean names. Objects are created at runtime and are not
    /// saved into the scene. Touches pass through. Placement is geographic.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(80)]
    public sealed class MapWaterLabelLayer : MonoBehaviour
    {
        public readonly struct Placed
        {
            public readonly string Id;
            public readonly string Text;
            public readonly Vector2 Screen;
            public readonly Rect Bounds;
            public readonly bool Raycast;

            public Placed(string id, string text, Vector2 screen, Rect bounds, bool raycast)
            {
                Id = id;
                Text = text;
                Screen = screen;
                Bounds = bounds;
                Raycast = raycast;
            }
        }

        RectTransform _root;
        Canvas _canvas;
        Text[] _texts;
        int[] _slotWater;
        Vector2[] _world;
        Vector2[] _screen;
        float[] _measured;
        string[] _names;
        Text _measure;
        int _measuredFont = -1;
        float _measuredScale = -1f;
        string _measuredLocale = string.Empty;
        int[] _chosen;
        Rect[] _occupied;
        Rect[] _blockers;
        MapCountryLabelLayer.Placed[] _countryPlaced;
        bool[] _cityTogether;
        int[] _cityParent;
        Rect[] _cityObstacles;
        int _count;
        float _heldZoom = -1f;
        float _heldSize = -1f;
        string _locale = string.Empty;
        int _fontSize = -1;
        bool _built;

        public int VisibleCount => _count;
        public float Zoom => _heldZoom;

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

        public static MapWaterLabelLayer Attach(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            if (scene.path != AppNavigation.MapScenePath && scene.name != AppNavigation.MapSceneName)
                return null;

            var roots = scene.GetRootGameObjects();
            Canvas canvas = null;
            for (var i = 0; i < roots.Length; i++)
            {
                var existing = roots[i].GetComponentInChildren<MapWaterLabelLayer>(true);
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

            var item = new GameObject("WaterLabels", typeof(RectTransform));
            item.hideFlags = HideFlags.DontSave;
            item.SetActive(false);
            var layer = item.AddComponent<MapWaterLabelLayer>();
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
            var waters = MapWaterCatalog.All;
            for (var i = 0; i < count; i++)
            {
                var text = _texts[i];
                var water = _slotWater[i];
                var id = water >= 0 && water < waters.Length ? waters[water].Id : string.Empty;
                destination[i] = new Placed(
                    id,
                    text != null ? text.text : string.Empty,
                    water >= 0 && water < _screen.Length ? _screen[water] : Vector2.zero,
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
            var rawZoom = MapWaterLabels.Zoom(size, worldSize);
            var zoom = MapWaterLabels.HoldZoom(_heldZoom, _heldSize, size, rawZoom);
            _heldZoom = zoom;
            _heldSize = size;

            var metrics = MapCommandLayout.Evaluate(width, height, safe, contextOpen);
            var scale = MapHudLayout.CanvasScale(width, height);
            if (scale < 0.05f)
                scale = 1f;
            var fontSize = Mathf.Max(14, Mathf.RoundToInt(MapMarkerLayer.LabelFontSize(scale, false) * MapWaterLabels.FontScale));
            var canvasScale = _canvas != null && _canvas.scaleFactor > 0.05f ? _canvas.scaleFactor : scale;
            var glyph = fontSize * canvasScale * MapWaterLabels.GlyphFactor;
            var line = fontSize * canvasScale * MapWaterLabels.LineFactor;
            var units = MapMarkerLod.UnitsPerPixel(size, height);
            for (var i = 0; i < _world.Length; i++)
                _screen[i] = MapMarkerLod.ToScreen(_world[i], center, units, width, height);

            var language = string.IsNullOrEmpty(locale) ? LocaleCodes.English : locale;
            EnsureNames(language, fontSize, canvasScale);
            var rig = camera.GetComponent<MapCameraRig>();
            var scope = rig != null ? rig.Scope : MapScope.World;
            var blockerCount = CollectBlockers(center, size, width, height, canvasScale, aspect);

            _count = MapWaterLabels.Choose(
                zoom,
                scope,
                _screen,
                _names,
                glyph,
                _measured,
                line,
                metrics.Viewport,
                _blockers,
                blockerCount,
                _chosen,
                _occupied);

            var languageChanged = _locale != language;
            var fontChanged = _fontSize != fontSize;
            _locale = language;
            _fontSize = fontSize;
            var eventCamera = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera
                ? _canvas.worldCamera
                : null;
            var waters = MapWaterCatalog.All;
            for (var slot = 0; slot < _texts.Length; slot++)
            {
                var text = _texts[slot];
                if (text == null)
                    continue;

                if (slot >= _count)
                {
                    if (text.gameObject.activeSelf)
                        text.gameObject.SetActive(false);
                    _slotWater[slot] = -1;
                    continue;
                }

                var water = _chosen[slot];
                var entry = waters[water];
                if (!text.gameObject.activeSelf)
                    text.gameObject.SetActive(true);
                if (fontChanged)
                    text.fontSize = fontSize;
                if (languageChanged || _slotWater[slot] != water)
                {
                    text.text = _names != null && water < _names.Length ? _names[water] : MapWaterLabels.Name(entry, language);
                    _slotWater[slot] = water;
                }

                var textWidth = _measured != null && water < _measured.Length ? _measured[water] : line;
                text.rectTransform.sizeDelta = new Vector2(textWidth / canvasScale, line / canvasScale);
                var point = _occupied != null && slot < _occupied.Length ? _occupied[slot].center : _screen[water];
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, point, eventCamera, out var local))
                    text.rectTransform.anchoredPosition = local;
            }

            return _count;
        }

        int CollectBlockers(Vector2 center, float size, int width, int height, float canvasScale, float aspect)
        {
            var count = 0;
            var countries = _canvas != null ? _canvas.GetComponentInChildren<MapCountryLabelLayer>(true) : null;
            if (countries != null && _countryPlaced != null)
            {
                var placed = countries.CopyPlaced(_countryPlaced);
                for (var i = 0; i < placed && count < _blockers.Length; i++)
                {
                    _blockers[count] = _countryPlaced[i].Bounds;
                    count++;
                }
            }

            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkeyFrame);
            var cities = MapCountryLabels.CityObstacles(
                center,
                size,
                width,
                height,
                turkeyFrame,
                canvasScale,
                _cityTogether,
                _cityParent,
                _cityObstacles);
            for (var i = 0; i < cities && count < _blockers.Length; i++)
            {
                _blockers[count] = _cityObstacles[i];
                count++;
            }

            return count;
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

            var waters = MapWaterCatalog.All;
            _world = new Vector2[waters.Length];
            _screen = new Vector2[waters.Length];
            _measured = new float[waters.Length];
            _names = new string[waters.Length];
            for (var i = 0; i < waters.Length; i++)
            {
                if (!MapProjection.TryProject(waters[i].Latitude, waters[i].Longitude, out _world[i]))
                    _world[i] = new Vector2(float.NaN, float.NaN);
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var measureObject = new GameObject("WaterMeasure", typeof(RectTransform));
            measureObject.hideFlags = HideFlags.DontSave;
            measureObject.transform.SetParent(_root, false);
            _measure = measureObject.AddComponent<Text>();
            _measure.font = font;
            _measure.fontStyle = FontStyle.Italic;
            _measure.raycastTarget = false;
            _measure.color = new Color(0f, 0f, 0f, 0f);
            measureObject.SetActive(false);
            _texts = new Text[MapWaterLabels.Capacity];
            _slotWater = new int[MapWaterLabels.Capacity];
            _chosen = new int[MapWaterLabels.Capacity];
            _occupied = new Rect[MapWaterLabels.Capacity];
            _countryPlaced = new MapCountryLabelLayer.Placed[MapCountryLabels.Capacity];
            _cityObstacles = new Rect[MapCities.All.Length * 2];
            _cityTogether = new bool[MapCities.All.Length * MapCities.All.Length];
            _cityParent = new int[MapCities.All.Length];
            _blockers = new Rect[MapCountryLabels.Capacity + _cityObstacles.Length];
            var ink = new Color(0.60f, 0.74f, 0.82f, 0.90f);
            for (var i = 0; i < _texts.Length; i++)
            {
                _slotWater[i] = -1;
                var item = new GameObject("WaterName", typeof(RectTransform));
                item.hideFlags = HideFlags.DontSave;
                item.transform.SetParent(_root, false);
                var rect = item.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var text = item.AddComponent<Text>();
                text.font = font;
                text.fontStyle = FontStyle.Italic;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = ink;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                text.supportRichText = false;
                var shadow = item.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.02f, 0.05f, 0.08f, 0.55f);
                shadow.effectDistance = new Vector2(1f, -1f);
                shadow.useGraphicAlpha = true;
                item.SetActive(false);
                _texts[i] = text;
            }

            _built = true;
        }

        void EnsureNames(string language, int fontSize, float canvasScale)
        {
            if (_measure == null || _measured == null || _names == null)
                return;
            if (_measuredFont == fontSize && _measuredLocale == language && Mathf.Abs(_measuredScale - canvasScale) < 0.02f)
                return;

            _measure.gameObject.SetActive(true);
            _measure.fontSize = fontSize;
            var waters = MapWaterCatalog.All;
            for (var i = 0; i < waters.Length; i++)
            {
                var name = MapWaterLabels.Name(waters[i], language);
                _names[i] = name;
                _measure.text = name;
                if (_measure.font != null && !string.IsNullOrEmpty(name))
                    _measure.font.RequestCharactersInTexture(name, fontSize, _measure.fontStyle);
                var width = _measure.preferredWidth * canvasScale + MapWaterLabels.LabelPad;
                if (width < 8f)
                    width = MapWaterLabels.Width(name, fontSize * canvasScale * MapWaterLabels.GlyphFactor);
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
                if (_slotWater != null)
                    _slotWater[i] = -1;
            }
        }
    }
}
