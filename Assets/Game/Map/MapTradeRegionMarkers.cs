using System;
using System.Collections.Generic;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Runtime overlay for the Turkey + Europe opening region. Uses the existing
    /// geographic projection, map camera and marker art; pooled markers represent
    /// visible city points or screen-space clusters. Never fabricates shipping
    /// routes, owned depots, enabled contracts or territory access.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapTradeRegionMarkers : MonoBehaviour
    {
        const float GridPixels = 40f;
        const float HitPixels = 27f;
        const int MaxVisibleMarkers = 180;

        sealed class MarkerSlot
        {
            public GameObject Root;
            public SpriteRenderer Icon;
            public TextMesh CountText;
            public Vector2 Screen;
            public Vector2 World;
            public MapBusinessLocation Location;
            public int Members;
        }

        struct RegionCity
        {
            public MapGlobalCity City;
            public Vector2 World;
        }

        struct Cluster
        {
            public Vector2 ScreenSum;
            public Vector2 WorldSum;
            public int Count;
            public MapBusinessLocation OnlyCity;
        }

        readonly List<RegionCity> _cities = new List<RegionCity>(240);
        readonly List<Cluster> _clusters = new List<Cluster>(160);
        readonly Dictionary<long, int> _grid = new Dictionary<long, int>(200);
        readonly List<MarkerSlot> _slots = new List<MarkerSlot>(64);
        MapMarkerLayer _legacy;
        Camera _camera;
        MapCameraRig _rig;
        GameObject _root;
        Sprite _markerSprite;
        Font _font;
        Vector3 _lastCameraPosition;
        float _lastSize = -1f;
        int _lastWidth = -1;
        int _lastHeight = -1;
        MapScope _lastScope = MapScope.World;
        int _visible;

        public int CityCount => _cities.Count;
        public int VisibleMarkerCount => _visible;

        public static void Ensure(MapMarkerLayer markerLayer, Camera camera)
        {
            if (!Application.isPlaying || markerLayer == null || camera == null)
                return;

            var overlay = markerLayer.GetComponent<MapTradeRegionMarkers>();
            if (overlay == null)
                overlay = markerLayer.gameObject.AddComponent<MapTradeRegionMarkers>();
            overlay.Bind(markerLayer, camera);
        }

        public void Bind(MapMarkerLayer markerLayer, Camera camera)
        {
            _legacy = markerLayer;
            _camera = camera;
            _rig = camera != null ? camera.GetComponent<MapCameraRig>() : null;
            if (_rig == null || _root != null)
                return;

            foreach (var spriteRenderer in markerLayer.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (spriteRenderer != null && spriteRenderer.name == "Core" && spriteRenderer.sprite != null)
                {
                    _markerSprite = spriteRenderer.sprite;
                    break;
                }
            }
            if (_markerSprite == null)
                return; // No fake or missing sprite dependencies.
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            foreach (var city in MapGlobalCityCatalog.All)
            {
                if (!MapTradeRegionCatalog.Includes(city))
                    continue;
                // Existing eight markers retain their authored visuals and
                // localized hit targets; no duplicate clickable marker.
                if (city.Location.CountryCode == "TUR" &&
                    MapCities.TryGet(city.Location.CityKey, out _))
                    continue;
                if (!MapProjection.TryProject(city.Latitude, city.Longitude, out var world))
                    continue;
                _cities.Add(new RegionCity { City = city, World = world });
            }

            _root = new GameObject("TradeRegionCityMarkers_Runtime");
            _root.hideFlags = HideFlags.DontSave;
            _root.transform.SetParent(transform, false);
            Refresh(true);
        }

        void OnEnable()
        {
            Refresh(true);
        }

        void OnDisable()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        void OnDestroy()
        {
            if (_root != null)
                Destroy(_root);
            _root = null;
        }

        void LateUpdate()
        {
            Refresh(false);
        }

        bool IsVisibleScope()
        {
            return _rig != null &&
                (_rig.Scope == MapScope.Turkey || _rig.Scope == MapScope.Europe);
        }

        void Refresh(bool force)
        {
            if (_root == null || _camera == null || _rig == null || !Application.isPlaying)
                return;

            if (!IsVisibleScope())
            {
                _root.SetActive(false);
                _visible = 0;
                return;
            }

            if (!_root.activeSelf)
                _root.SetActive(true);

            var position = _camera.transform.position;
            var size = _camera.orthographicSize;
            var width = Screen.width;
            var height = Screen.height;
            if (width <= 0 || height <= 0)
                return;
            if (!force && position == _lastCameraPosition &&
                Mathf.Approximately(size, _lastSize) &&
                _lastWidth == width && _lastHeight == height &&
                _lastScope == _rig.Scope)
                return;

            _lastCameraPosition = position;
            _lastSize = size;
            _lastWidth = width;
            _lastHeight = height;
            _lastScope = _rig.Scope;
            var unitsPerPixel = MapMarkerLod.UnitsPerPixel(size, height);
            var center = new Vector2(position.x, position.y);

            _grid.Clear();
            _clusters.Clear();
            foreach (var city in _cities)
            {
                if (_rig.Scope == MapScope.Turkey &&
                    city.City.Location.CountryCode != "TUR")
                    continue;

                var screen = MapMarkerLod.ToScreen(city.World, center,
                    unitsPerPixel, width, height);
                if (screen.x < -HitPixels || screen.x > width + HitPixels ||
                    screen.y < -HitPixels || screen.y > height + HitPixels)
                    continue;

                var gx = Mathf.FloorToInt(screen.x / GridPixels);
                var gy = Mathf.FloorToInt(screen.y / GridPixels);
                var key = ((long)gx << 32) ^ (uint)gy;
                if (!_grid.TryGetValue(key, out var index))
                {
                    index = _clusters.Count;
                    _grid.Add(key, index);
                    _clusters.Add(new Cluster
                    {
                        ScreenSum = screen,
                        WorldSum = city.World,
                        Count = 1,
                        OnlyCity = city.City.Location
                    });
                }
                else
                {
                    var cluster = _clusters[index];
                    cluster.ScreenSum += screen;
                    cluster.WorldSum += city.World;
                    cluster.Count++;
                    cluster.OnlyCity = default;
                    _clusters[index] = cluster;
                }
            }

            _visible = Mathf.Min(MaxVisibleMarkers, _clusters.Count);
            for (var i = 0; i < _visible; i++)
            {
                var slot = Rent(i);
                var cluster = _clusters[i];
                slot.Members = cluster.Count;
                slot.Location = cluster.OnlyCity;
                slot.Screen = cluster.ScreenSum / cluster.Count;
                slot.World = cluster.WorldSum / cluster.Count;
                slot.Root.SetActive(true);
                slot.Root.transform.position = new Vector3(slot.World.x, slot.World.y, 0f);
                var pixels = cluster.Count > 1
                    ? 30f + Mathf.Min(12f, (cluster.Count - 1) * 2f)
                    : 16f;
                var scale = MapMarkerLod.ScaleForPixels(pixels, size, height);
                slot.Root.transform.localScale = new Vector3(scale, scale, 1f);
                slot.Icon.color = cluster.Count > 1
                    ? new Color(0.95f, 0.72f, 0.37f, 0.97f)
                    : new Color(0.25f, 0.74f, 0.83f, 0.92f);
                if (slot.CountText != null)
                {
                    slot.CountText.gameObject.SetActive(cluster.Count > 1);
                    if (cluster.Count > 1)
                        slot.CountText.text = cluster.Count.ToString();
                }
            }

            for (var i = _visible; i < _slots.Count; i++)
                _slots[i].Root.SetActive(false);
        }

        MarkerSlot Rent(int index)
        {
            if (index < _slots.Count)
                return _slots[index];

            var root = new GameObject("TradeCityMarker_" + index);
            root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(_root.transform, false);
            var icon = root.AddComponent<SpriteRenderer>();
            icon.sprite = _markerSprite;
            icon.sortingOrder = 21;
            var result = new MarkerSlot { Root = root, Icon = icon };
            if (_font != null)
            {
                var badge = new GameObject("ClusterSize");
                badge.hideFlags = HideFlags.DontSave;
                badge.transform.SetParent(root.transform, false);
                badge.transform.localScale = Vector3.one * 0.20f;
                var text = badge.AddComponent<TextMesh>();
                text.font = _font;
                text.fontSize = 48;
                text.characterSize = 0.10f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = new Color(0.06f, 0.18f, 0.28f, 1f);
                var renderer = badge.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sortingOrder = 22;
                result.CountText = text;
                badge.SetActive(false);
            }
            _slots.Add(result);
            return result;
        }

        /// <summary>
        /// A cluster tap zooms toward its center. A single-city tap provides a
        /// catalog ID for the city inspector. Called only after UI hit blocking.
        /// </summary>
        public bool TrySelect(Vector2 screenPoint, out MapBusinessLocation location)
        {
            location = default;
            if (!IsVisibleScope() || _root == null || !_root.activeSelf)
                return false;
            Refresh(false);
            var closest = -1;
            var bestDistance = HitPixels * HitPixels;
            for (var i = 0; i < _visible; i++)
            {
                var marker = _slots[i];
                var distance = (screenPoint - marker.Screen).sqrMagnitude;
                if (distance > bestDistance)
                    continue;
                closest = i;
                bestDistance = distance;
            }

            if (closest < 0)
                return false;

            var selected = _slots[closest];
            if (selected.Members > 1)
            {
                _rig.FocusWorldPoint(selected.World);
                _rig.ZoomAtScreen(
                    new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 1.85f);
                return true;
            }

            location = selected.Location;
            return location.IsValid;
        }
    }
}
