using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogistiCore.Map
{
    /// <summary>
    /// Runtime-only cartographic presentation of MapSchematicLinks. It NEVER
    /// claims a road, active delivery or contractual route. No scene, prefab,
    /// Firebase, gameplay or asset import changes are required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapPremiumNetworkOverlay : MonoBehaviour
    {
        const int CurvePoints = 17;
        const int RingPoints = 33;
        const int RouteGlowOrder = 12;
        const int RouteCoreOrder = 13;
        const int CityRingOrder = 19;
        const float MaxTurkeyFrameFactor = 1.55f;

        sealed class LinkView
        {
            public MapSchematicLink Link;
            public LineRenderer Glow;
            public LineRenderer Line;
        }

        sealed class CityView
        {
            public string CityId;
            public LineRenderer Ring;
            public readonly Vector3[] Points = new Vector3[RingPoints];
        }

        MapMarkerLayer _markers;
        Camera _camera;
        MapCameraRig _rig;
        GameObject _visuals;
        Material _lineMaterial;
        LinkView[] _links;
        CityView[] _cities;
        bool _bound;
        bool _subscribed;
        float _lastOrtho = -1f;
        int _lastHeight = -1;
        MapScope _lastScope = MapScope.World;
        string _lastSelected;

        public static void Ensure(MapMarkerLayer markers, Camera camera)
        {
            if (!Application.isPlaying || markers == null)
                return;

            var overlay = markers.GetComponent<MapPremiumNetworkOverlay>();
            if (overlay == null)
                overlay = markers.gameObject.AddComponent<MapPremiumNetworkOverlay>();
            overlay.Bind(markers, camera);
        }

        public void Bind(MapMarkerLayer markers, Camera camera)
        {
            if (markers == null)
                return;

            Unsubscribe();
            _markers = markers;
            _camera = camera != null ? camera : Camera.main;
            _rig = _camera != null ? _camera.GetComponent<MapCameraRig>() : null;
            if (_rig == null)
                return; // Fail closed: no known regional presentation context.

            if (!_bound)
            {
                _bound = true;
                Build();
            }

            Subscribe();
            Refresh(true);
        }

        void OnEnable()
        {
            Subscribe();
            Refresh(true);
        }

        void OnDisable()
        {
            Unsubscribe();
            if (_visuals != null)
                _visuals.SetActive(false);
        }

        void OnDestroy()
        {
            Unsubscribe();
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !_bound || _rig == null || _camera == null)
                return;

            // Only presentation values change: there is no game-state polling.
            var selected = _markers != null ? _markers.SelectedId : null;
            if (Mathf.Abs(_camera.orthographicSize - _lastOrtho) > 0.0001f ||
                Screen.height != _lastHeight || _rig.Scope != _lastScope ||
                !string.Equals(selected, _lastSelected, StringComparison.Ordinal))
                Refresh(false);
        }

        void Subscribe()
        {
            if (_subscribed || _markers == null || !isActiveAndEnabled)
                return;
            _markers.SelectionChanged += OnSelectionChanged;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed)
                return;
            if (_markers != null)
                _markers.SelectionChanged -= OnSelectionChanged;
            _subscribed = false;
        }

        void OnSelectionChanged()
        {
            Refresh(true);
        }

        void Build()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            // In stripped player builds, a shader already present on the authored
            // city sprites is preferable to forcing a new GraphicsSettings asset.
            if (shader == null && _markers != null)
            {
                var existing = _markers.GetComponentInChildren<SpriteRenderer>(true);
                if (existing != null && existing.sharedMaterial != null)
                    shader = existing.sharedMaterial.shader;
            }
            if (shader == null)
            {
                Debug.LogWarning("[LogistiCore] VISUAL-01 schematic overlay unavailable: no compatible sprite shader.");
                return;
            }

            _lineMaterial = new Material(shader)
            {
                name = "LogistiCoreSchematicConnections",
                hideFlags = HideFlags.HideAndDontSave
            };

            _visuals = new GameObject("SchematicConnections_ViewOnly");
            _visuals.hideFlags = HideFlags.DontSave;
            _visuals.transform.SetParent(transform, false);

            _links = new LinkView[MapSchematicLinks.Count];
            for (int i = 0; i < _links.Length; i++)
            {
                var link = MapSchematicLinks.At(i);
                var points = new Vector3[CurvePoints];
                if (!MapSchematicLinks.Sample(link, points))
                    continue;

                var view = new LinkView
                {
                    Link = link,
                    Glow = CreateLine("ConnectionGlow_" + i, RouteGlowOrder, false),
                    Line = CreateLine("Connection_" + i, RouteCoreOrder, false)
                };
                view.Glow.positionCount = points.Length;
                view.Glow.SetPositions(points);
                view.Line.positionCount = points.Length;
                view.Line.SetPositions(points);
                _links[i] = view;
            }

            _cities = new CityView[MapCities.All.Length];
            for (int i = 0; i < _cities.Length; i++)
            {
                var city = MapCities.All[i];
                if (!MapProjection.TryProject(city.Latitude, city.Longitude, out var point))
                    continue;
                var ring = CreateLine("CityHalo_" + city.Id, CityRingOrder, true);
                ring.transform.position = new Vector3(point.x, point.y, 0f);
                ring.positionCount = RingPoints;
                _cities[i] = new CityView { CityId = city.Id, Ring = ring };
            }

            _visuals.SetActive(false);
        }

        LineRenderer CreateLine(string name, int order, bool local)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(_visuals.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _lineMaterial;
            line.useWorldSpace = !local;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.sortingOrder = order;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.generateLightingData = false;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.loop = false;
            return line;
        }

        void Refresh(bool force)
        {
            if (_camera == null || _rig == null || _visuals == null || _links == null)
                return;

            var ortho = _camera.orthographicSize;
            var height = Mathf.Max(1, Screen.height);
            var selected = _markers != null ? _markers.SelectedId : null;
            if (!force && Mathf.Abs(ortho - _lastOrtho) < 0.0001f &&
                height == _lastHeight && _lastScope == _rig.Scope &&
                string.Equals(_lastSelected, selected, StringComparison.Ordinal))
                return;

            _lastOrtho = ortho;
            _lastHeight = height;
            _lastScope = _rig.Scope;
            _lastSelected = selected;

            MapFrames.Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, Mathf.Max(0.1f, _camera.aspect),
                MapFrames.TurkeyFocusPadding, out _, out var fitSize);

            // No glowing routes at world scale, on other continents or in the
            // aggregated country-badge state. The graph is a local city preview.
            bool show = _rig.Scope == MapScope.Turkey &&
                        ortho <= fitSize * MaxTurkeyFrameFactor;
            if (_visuals.activeSelf != show)
                _visuals.SetActive(show);
            if (!show)
                return;

            float units = MapMarkerLod.UnitsPerPixel(ortho, height);
            bool hasSelection = !string.IsNullOrEmpty(selected);
            for (int i = 0; i < _links.Length; i++)
            {
                var view = _links[i];
                if (view == null)
                    continue;
                bool highlighted = hasSelection && view.Link.Touches(selected);
                float lineAlpha = hasSelection ? (highlighted ? 0.93f : 0.13f) : 0.38f;
                float glowAlpha = hasSelection ? (highlighted ? 0.30f : 0.025f) : 0.075f;
                SetLine(view.Line, MapPremiumTheme.Alpha(MapPremiumTheme.Cyan, lineAlpha),
                    units * (highlighted ? 2.8f : 1.55f));
                SetLine(view.Glow, MapPremiumTheme.Alpha(MapPremiumTheme.Cyan, glowAlpha),
                    units * (highlighted ? 9f : 5f));
            }

            for (int i = 0; i < _cities.Length; i++)
            {
                var city = _cities[i];
                if (city == null)
                    continue;
                bool active = string.Equals(city.CityId, selected, StringComparison.Ordinal);
                var points = city.Points;
                float radius = units * (active ? 25f : 19f);
                for (var j = 0; j < RingPoints; j++)
                {
                    float angle = j * Mathf.PI * 2f / (RingPoints - 1);
                    points[j] = new Vector3(Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius, 0f);
                }
                city.Ring.SetPositions(points);
                SetLine(city.Ring, MapPremiumTheme.Alpha(MapPremiumTheme.Cyan,
                        active ? 0.96f : (hasSelection ? 0.32f : 0.66f)),
                    units * (active ? 2.2f : 1.15f));
            }
        }

        static void SetLine(LineRenderer line, Color color, float width)
        {
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
        }
    }
}
