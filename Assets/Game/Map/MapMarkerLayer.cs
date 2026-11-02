using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// Eight city markers. At world scale they collapse into count badges.
    /// Labels stay a constant screen size and hide at world framing.
    /// Positions update while the camera moves. They do not rebuild geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapMarkerLayer : MonoBehaviour
    {
        [SerializeField] Camera mapCamera;
        [SerializeField] RectTransform labelRoot;
        [SerializeField] MapCityMarker[] markers;
        [SerializeField] Text[] labels;

        readonly List<Binding> _bindings = new List<Binding>(8);
        readonly List<GroupMark> _groups = new List<GroupMark>(4);
        string _selectedId;
        Vector3 _lastCameraPosition;
        float _lastSize = -1f;
        int _lastWidth;
        int _lastHeight;
        bool _subscribed;
        bool _fading;
        MarkerView[] _views;
        Vector2[] _world;
        Vector2[] _screen;
        int[] _cluster;
        bool[] _together;
        bool[] _individual;
        float[] _shown;
        Canvas _canvas;
        Sprite _iconSprite;
        Vector2[] _labelCenter;
        Vector2[] _labelHalf;
        bool[] _labelOpen;
        int[] _fittedFont;
        float[] _fittedWidth;
        string[] _fittedText;
        Image[] _plates;

        public string SelectedId => _selectedId;
        public int MarkerCount => markers == null ? 0 : markers.Length;

        internal int IndividualCount
        {
            get
            {
                var count = 0;
                if (_individual == null)
                    return 0;
                for (var i = 0; i < _individual.Length; i++)
                {
                    if (_individual[i])
                        count++;
                }

                return count;
            }
        }

        internal int ActiveAggregateCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Active)
                        count++;
                }

                return count;
            }
        }

        internal int LargestAggregateMembers
        {
            get
            {
                var largest = 0;
                for (var i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Active && _groups[i].Members > largest)
                        largest = _groups[i].Members;
                }

                return largest;
            }
        }

        internal bool TryAggregateHitPoint(out Vector2 screen, out int members)
        {
            for (var i = 0; i < _groups.Count; i++)
            {
                if (!_groups[i].Active)
                    continue;

                screen = _groups[i].Screen;
                members = _groups[i].Members;
                return true;
            }

            screen = default;
            members = 0;
            return false;
        }

        internal bool TryMarkerScreen(int index, out Vector2 screen)
        {
            if (_screen == null || index < 0 || index >= _screen.Length)
            {
                screen = default;
                return false;
            }

            screen = _screen[index];
            return true;
        }

        public event Action SelectionChanged;

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (mapCamera == null)
                mapCamera = Camera.main;

            Subscribe();
            ApplyViewport(Screen.width, Screen.height, snap: true);
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void OnDestroy()
        {
            for (var i = 0; i < _groups.Count; i++)
                _groups[i].DestroyMark();
            _groups.Clear();
        }

        void LateUpdate()
        {
            ApplyViewport(Screen.width, Screen.height, snap: false);
        }

        public bool TrySelect(Vector2 screen, out string cityId)
        {
            cityId = null;
            if (markers == null || mapCamera == null)
                return false;

            EnsureCapacity();
            if (_screen == null)
                ApplyViewport(Screen.width < 1 ? 1 : Screen.width, Screen.height < 1 ? 1 : Screen.height, snap: true);

            var bestGroup = -1;
            var bestGroupDistance = MapMarkerLod.AggregateHitRadius * MapMarkerLod.AggregateHitRadius;
            for (var i = 0; i < _groups.Count; i++)
            {
                var group = _groups[i];
                if (!group.Active)
                    continue;

                var delta = group.Screen - screen;
                var distance = delta.sqrMagnitude;
                if (distance <= bestGroupDistance)
                {
                    bestGroupDistance = distance;
                    bestGroup = i;
                }
            }

            if (bestGroup >= 0)
            {
                var rig = mapCamera.GetComponent<MapCameraRig>();
                if (rig != null)
                    rig.FocusTurkey();
                return true;
            }

            var best = MapMarkerLod.MarkerHitPixels * MapMarkerLod.MarkerHitPixels;
            MapCityMarker chosen = null;
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null || _individual == null || i >= _individual.Length || !_individual[i])
                    continue;

                var point = _screen[i];
                var dx = point.x - screen.x;
                var dy = point.y - screen.y;
                var distance = dx * dx + dy * dy;
                if (distance < best)
                {
                    best = distance;
                    chosen = marker;
                }
            }

            if (chosen == null)
                return false;

            SetSelected(chosen.CityId);
            cityId = chosen.CityId;
            return true;
        }

        public void ClearSelection()
        {
            if (_selectedId == null)
                return;

            SetSelected(null);
        }

        public bool TryGetLocalizedName(string cityId, out string name)
        {
            name = null;
            for (var i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i].CityId == cityId)
                {
                    name = _bindings[i].Value;
                    return !string.IsNullOrEmpty(name);
                }
            }

            return false;
        }

        internal void ApplyViewport(int width, int height, bool snap)
        {
            if (mapCamera == null || markers == null)
                return;

            EnsureCapacity();
            var position = mapCamera.transform.position;
            var size = mapCamera.orthographicSize;
            if (!snap && !_fading &&
                position == _lastCameraPosition &&
                Mathf.Approximately(size, _lastSize) &&
                width == _lastWidth &&
                height == _lastHeight)
            {
                return;
            }

            _lastCameraPosition = position;
            _lastSize = size;
            _lastWidth = width;
            _lastHeight = height;

            var count = markers.Length;
            var center = new Vector2(position.x, position.y);
            var units = MapMarkerLod.UnitsPerPixel(size, height);
            for (var i = 0; i < count; i++)
            {
                var marker = markers[i];
                if (marker == null)
                {
                    _world[i] = center;
                    _screen[i] = new Vector2(width * 0.5f, height * 0.5f);
                    continue;
                }

                var world = marker.transform.position;
                _world[i] = new Vector2(world.x, world.y);
                _screen[i] = MapMarkerLod.ToScreen(_world[i], center, units, width, height);
            }

            var aspect = mapCamera.aspect < 0.05f ? (height < 1 ? 1f : width / (float)height) : mapCamera.aspect;
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkeyFrame);
            MapMarkerLod.Cluster(_screen, count, MapMarkerLod.AtCountryScale(size, turkeyFrame), _together, _cluster);

            var groupSlot = 0;
            for (var i = 0; i < count; i++)
            {
                var members = MapMarkerLod.ClusterSize(_cluster, count, i);
                _individual[i] = members < 2;
                if (!_individual[i] && _cluster[i] == i)
                {
                    var group = RentGroup(groupSlot);
                    groupSlot++;
                    group.Active = true;
                    group.Members = members;
                    var worldCentroid = MapMarkerLod.Centroid(_world, _cluster, count, i);
                    group.Screen = MapMarkerLod.ToScreen(worldCentroid, center, units, width, height);
                    PlaceGroup(group, worldCentroid, size, height);
                }
            }

            for (var i = groupSlot; i < _groups.Count; i++)
            {
                _groups[i].Active = false;
                _groups[i].Members = 0;
            }

            var step = snap ? 1f : Mathf.Min(0.05f, Time.unscaledDeltaTime) * 6f;
            _fading = false;
            for (var i = 0; i < count; i++)
            {
                var target = _individual[i] ? 1f : 0f;
                _shown[i] = snap ? target : Mathf.MoveTowards(_shown[i], target, step);
                if (Mathf.Abs(_shown[i] - target) > 0.01f)
                    _fading = true;
                PlaceMarker(i, size, height);
            }

            for (var i = 0; i < _groups.Count; i++)
            {
                var group = _groups[i];
                var target = group.Active ? 1f : 0f;
                group.Shown = snap ? target : Mathf.MoveTowards(group.Shown, target, step);
                if (Mathf.Abs(group.Shown - target) > 0.01f)
                    _fading = true;
                ApplyGroupFade(group);
            }

            PlaceLabels(count, width, height, size <= turkeyFrame * 1.35f);
        }

        void SetSelected(string cityId)
        {
            _selectedId = cityId;
            SelectionChanged?.Invoke();
            _lastSize = -1f;
            ApplyViewport(_lastWidth < 1 ? Screen.width : _lastWidth, _lastHeight < 1 ? Screen.height : _lastHeight, snap: true);
        }

        void Subscribe()
        {
            if (_subscribed || markers == null)
                return;

            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                var label = labels != null && i < labels.Length ? labels[i] : null;
                if (marker == null)
                    continue;

                var binding = new Binding
                {
                    CityId = marker.CityId,
                    Source = new LocalizedString(MapContent.Table, MapContent.CityKey(marker.CityId)),
                    Target = label
                };
                binding.Handler = value =>
                {
                    binding.Value = value ?? string.Empty;
                    if (binding.Target != null)
                        binding.Target.text = binding.Value;
                };
                binding.Source.StringChanged += binding.Handler;
                _bindings.Add(binding);
            }

            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed)
                return;

            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Source.StringChanged -= _bindings[i].Handler;

            _bindings.Clear();
            _subscribed = false;
        }

        void EnsureCapacity()
        {
            var count = markers.Length;
            if (_views != null && _views.Length == count)
                return;

            _views = new MarkerView[count];
            _world = new Vector2[count];
            _screen = new Vector2[count];
            _cluster = new int[count];
            _together = new bool[count * count];
            _individual = new bool[count];
            _shown = new float[count];
            for (var i = 0; i < count; i++)
            {
                var marker = markers[i];
                var view = new MarkerView { Marker = marker };
                if (marker != null)
                {
                    var renderers = marker.GetComponentsInChildren<SpriteRenderer>(true);
                    for (var r = 0; r < renderers.Length; r++)
                    {
                        var renderer = renderers[r];
                        if (renderer.gameObject.name == "Glow")
                            view.Glow = renderer;
                        else if (renderer.gameObject.name == "Core")
                            view.Core = renderer;
                    }

                    if (_iconSprite == null && view.Core != null)
                        _iconSprite = view.Core.sprite;
                }

                _views[i] = view;
                _individual[i] = true;
                _shown[i] = 1f;
            }
        }

        void PlaceMarker(int index, float orthographicSize, float screenHeight)
        {
            var view = _views[index];
            if (view.Marker == null)
                return;

            var nearest = MapMarkerLod.NearestPixels(_screen, _screen.Length, index);
            var core = MapMarkerLod.CorePixels(nearest);
            var selected = view.Marker.CityId == _selectedId;
            var scale = MapMarkerLod.ScaleForPixels(core * (selected ? MapMarkerLod.SelectedMultiplier : 1f), orthographicSize, screenHeight);
            view.Marker.transform.localScale = new Vector3(scale, scale, 1f);
            if (view.Glow != null)
            {
                var glowScale = GlowVisualScale(MapMarkerLod.GlowScale(core), selected);
                view.Glow.transform.localScale = new Vector3(glowScale, glowScale, 1f);
            }

            var shown = _shown[index];
            var visible = shown > 0.01f;
            Paint(view.Core, selected, false, shown, visible);
            Paint(view.Glow, selected, true, shown, visible);
        }

        static void Paint(SpriteRenderer renderer, bool selected, bool glow, float shown, bool visible)
        {
            if (renderer == null)
                return;

            if (renderer.enabled != visible)
                renderer.enabled = visible;
            if (!visible)
                return;

            Color color;
            if (glow)
            {
                color = selected
                    ? new Color(0.16f, 0.58f, 0.70f, 0.30f)
                    : new Color(0.10f, 0.50f, 0.62f, 0.18f);
            }
            else
            {
                color = selected
                    ? new Color(0.74f, 0.92f, 0.97f, 1f)
                    : new Color(0.20f, 0.74f, 0.86f, 1f);
            }

            color.a *= shown;
            renderer.color = color;
        }

        GroupMark RentGroup(int slot)
        {
            while (_groups.Count <= slot)
                _groups.Add(CreateGroup());
            return _groups[slot];
        }

        GroupMark CreateGroup()
        {
            var root = new GameObject("CityGroup");
            root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(transform, false);
            var icon = root.AddComponent<SpriteRenderer>();
            icon.sprite = _iconSprite;
            icon.sortingOrder = 20;
            icon.enabled = false;

            Text count = null;
            Font font = null;
            if (labels != null)
            {
                for (var i = 0; i < labels.Length; i++)
                {
                    if (labels[i] != null && labels[i].font != null)
                    {
                        font = labels[i].font;
                        break;
                    }
                }
            }

            if (labelRoot != null && font != null)
            {
                var textObject = new GameObject("GroupCount", typeof(RectTransform));
                textObject.hideFlags = HideFlags.DontSave;
                textObject.transform.SetParent(labelRoot, false);
                count = textObject.AddComponent<Text>();
                count.font = font;
                count.alignment = TextAnchor.MiddleCenter;
                count.raycastTarget = false;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                count.verticalOverflow = VerticalWrapMode.Overflow;
                count.fontSize = 15;
                count.color = new Color(0.90f, 0.97f, 1f, 0.95f);
                var rect = count.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(64f, 64f);
                textObject.SetActive(false);
            }

            return new GroupMark { Root = root, Icon = icon, Count = count };
        }

        void PlaceGroup(GroupMark group, Vector2 world, float orthographicSize, float screenHeight)
        {
            var scale = MapMarkerLod.ScaleForPixels(MapMarkerLod.AggregateScreenPixels, orthographicSize, screenHeight);
            group.Root.transform.position = new Vector3(world.x, world.y, 0f);
            group.Root.transform.localScale = new Vector3(scale, scale, 1f);
            if (group.Count == null)
                return;

            if (group.CaptionMembers != group.Members)
            {
                group.CaptionMembers = group.Members;
                group.Caption = group.Members.ToString();
                group.Count.text = group.Caption;
                group.InkReady = false;
            }

            if (_canvas == null && labelRoot != null)
                _canvas = labelRoot.GetComponentInParent<Canvas>();
            var canvasScale = _canvas != null && _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;
            var fontSize = Mathf.RoundToInt(14f / canvasScale);
            if (fontSize < 12)
                fontSize = 12;
            if (group.Count.fontSize != fontSize)
            {
                group.Count.fontSize = fontSize;
                group.InkReady = false;
            }

            if (!group.InkReady)
            {
                group.InkOffset = GlyphCenterOffset(group.Count);
                group.InkReady = true;
            }
        }

        internal static Vector2 GlyphCenterOffset(Text text)
        {
            if (text == null || text.font == null || string.IsNullOrEmpty(text.text))
                return Vector2.zero;

            var rect = text.rectTransform.rect;
            var area = rect.width > 1f && rect.height > 1f
                ? rect.size
                : text.rectTransform.sizeDelta;
            text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            var settings = text.GetGenerationSettings(area);
            var generator = text.cachedTextGenerator;
            generator.Populate(text.text, settings);
            var count = generator.vertexCount;
            if (count < 4)
                return Vector2.zero;

            var verts = generator.verts;
            var min = verts[0].position;
            var max = min;
            for (var i = 1; i < count; i++)
            {
                var position = verts[i].position;
                if (position.x < min.x)
                    min.x = position.x;
                if (position.y < min.y)
                    min.y = position.y;
                if (position.x > max.x)
                    max.x = position.x;
                if (position.y > max.y)
                    max.y = position.y;
            }

            return new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
        }

        static void ApplyGroupFade(GroupMark group)
        {
            var visible = group.Shown > 0.01f;
            if (group.Icon != null)
            {
                if (group.Icon.enabled != visible)
                    group.Icon.enabled = visible;
                if (visible)
                    group.Icon.color = new Color(0.62f, 0.86f, 0.94f, 0.90f * group.Shown);
            }

            if (group.Count == null)
                return;

            if (group.Count.gameObject.activeSelf != visible)
                group.Count.gameObject.SetActive(visible);
            if (!visible || group.Count.rectTransform == null)
                return;

            var root = group.Count.rectTransform.parent as RectTransform;
            if (root != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(root, group.Screen, null, out var local))
            {
                local -= group.InkOffset;
                if ((group.Count.rectTransform.anchoredPosition - local).sqrMagnitude > 0.01f)
                    group.Count.rectTransform.anchoredPosition = local;
            }
            var color = group.Count.color;
            color.a = 0.95f * group.Shown;
            group.Count.color = color;
        }

        internal const float LabelScreenPixels = 14f;
        internal const float SelectedLabelScreenPixels = 16f;

        internal static int LabelFontSize(float canvasScale, bool selected)
        {
            var scale = canvasScale < 0.05f ? 1f : canvasScale;
            var target = selected ? SelectedLabelScreenPixels : LabelScreenPixels;
            var size = Mathf.RoundToInt(target / scale);
            var floor = selected ? 17 : 16;
            if (size < floor)
                size = floor;
            if (size > 48)
                size = 48;
            return size;
        }

        internal static float GlowVisualScale(float authoredScale, bool selected)
        {
            var tight = authoredScale < MapMarkerLod.FullGlowScale - 0.05f;
            var factor = tight
                ? (selected ? 0.88f : 0.78f)
                : (selected ? 0.58f : 0.46f);
            return authoredScale * factor;
        }

        void PlaceLabels(int count, int width, int height, bool countryVisible)
        {
            if (labels == null)
                return;

            var scale = MapHudLayout.CanvasScale(width, height);
            if (scale < 0.05f)
                scale = 1f;
            EnsureLabelCache();

            for (var i = 0; i < labels.Length; i++)
            {
                _labelOpen[i] = false;
                var label = labels[i];
                if (label == null)
                    continue;

                var show = countryVisible && i < count && markers[i] != null && _individual[i] && ShowsLabel(i, count);
                if (!show)
                {
                    SetLabelVisible(label, false);
                    continue;
                }

                var selected = markers[i].CityId == _selectedId;
                var fontSize = LabelFontSize(scale, selected);
                var plateWidth = FitLabel(i, label, fontSize, selected);
                var half = new Vector2(plateWidth * scale * 0.5f, (fontSize + 8f) * scale * 0.5f);
                var center = _screen[i];
                center.y += 16f + half.y + 6f;
                if (!KeepOnScreen(ref center, half, width, height, selected))
                {
                    SetLabelVisible(label, false);
                    continue;
                }

                _labelOpen[i] = true;
                _labelCenter[i] = center;
                _labelHalf[i] = half;
                label.color = selected
                    ? new Color(0.94f, 0.98f, 1f, 1f)
                    : new Color(0.90f, 0.95f, 0.98f, 0.96f);
            }

            for (var i = 0; i < labels.Length; i++)
            {
                if (!_labelOpen[i] || markers[i] == null || markers[i].CityId == _selectedId)
                    continue;

                for (var j = 0; j < labels.Length; j++)
                {
                    if (i == j || !_labelOpen[j] || !LabelOverlaps(i, j))
                        continue;

                    var otherSelected = markers[j] != null && markers[j].CityId == _selectedId;
                    if (otherSelected || j < i)
                    {
                        _labelOpen[i] = false;
                        break;
                    }
                }
            }

            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null)
                    continue;

                SetLabelVisible(label, _labelOpen[i]);
                if (!_labelOpen[i] || labelRoot == null)
                    continue;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(labelRoot, _labelCenter[i], null, out var local))
                {
                    var plate = label.rectTransform.parent as RectTransform;
                    if (plate != null && plate != labelRoot)
                        plate.anchoredPosition = local;
                    else
                        label.rectTransform.anchoredPosition = local;
                }
            }
        }

        void EnsureLabelCache()
        {
            if (labels == null)
                return;
            if (_labelOpen != null && _labelOpen.Length == labels.Length)
                return;

            var count = labels.Length;
            _labelCenter = new Vector2[count];
            _labelHalf = new Vector2[count];
            _labelOpen = new bool[count];
            _fittedFont = new int[count];
            _fittedWidth = new float[count];
            _fittedText = new string[count];
            _plates = new Image[count];
            for (var i = 0; i < count; i++)
            {
                if (labels[i] == null || labels[i].transform.parent == null)
                    continue;
                _plates[i] = labels[i].transform.parent.GetComponent<Image>();
            }
        }

        float FitLabel(int index, Text label, int fontSize, bool selected)
        {
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.alignment = TextAnchor.MiddleCenter;
            if (label.fontSize != fontSize)
                label.fontSize = fontSize;

            var width = MeasuredLabelWidth(index, label) + 16f;
            var minimum = fontSize * 2.2f;
            var maximum = fontSize * 11f;
            if (width < minimum)
                width = minimum;
            if (width > maximum)
                width = maximum;
            var height = fontSize + 8f;
            var plate = label.rectTransform.parent as RectTransform;
            if (plate != null && plate != labelRoot)
            {
                var size = plate.sizeDelta;
                if (Mathf.Abs(size.x - width) > 0.5f || Mathf.Abs(size.y - height) > 0.5f)
                    plate.sizeDelta = new Vector2(width, height);
            }

            if (_plates != null && index < _plates.Length && _plates[index] != null)
            {
                var plateColor = new Color(0.012f, 0.028f, 0.05f, selected ? 0.70f : 0.56f);
                if (_plates[index].color != plateColor)
                    _plates[index].color = plateColor;
            }

            return width;
        }

        float MeasuredLabelWidth(int index, Text label)
        {
            var text = label.text;
            if (string.IsNullOrEmpty(text))
                return label.fontSize * 2f;
            if (_fittedFont[index] == label.fontSize && _fittedText[index] == text)
                return _fittedWidth[index];

            var width = label.preferredWidth;
            var cap = label.fontSize * text.Length * 1.2f;
            var floor = label.fontSize * text.Length * 0.42f;
            if (width < floor || width > cap)
                width = text.Length * label.fontSize * 0.62f;
            _fittedFont[index] = label.fontSize;
            _fittedText[index] = text;
            _fittedWidth[index] = width;
            return width;
        }

        static bool KeepOnScreen(ref Vector2 center, Vector2 half, float width, float height, bool selected)
        {
            var minX = 6f + half.x;
            var maxX = width - 6f - half.x;
            var minY = 6f + half.y;
            var maxY = height - 6f - half.y;
            if (maxX < minX)
                maxX = minX;
            if (maxY < minY)
                maxY = minY;

            var rail = width * 0.09f;
            var header = height - height * 0.1f;
            var inChrome = center.x < rail || center.y > header;
            var outside = center.x < minX || center.x > maxX || center.y < minY || center.y > maxY || inChrome;
            if (!outside)
                return true;
            if (!selected)
                return false;

            center.x = Mathf.Clamp(center.x, Mathf.Max(minX, rail), maxX);
            center.y = Mathf.Clamp(center.y, minY, Mathf.Min(maxY, header));
            return true;
        }

        bool LabelOverlaps(int a, int b)
        {
            var delta = _labelCenter[a] - _labelCenter[b];
            return Mathf.Abs(delta.x) < _labelHalf[a].x + _labelHalf[b].x + 6f &&
                   Mathf.Abs(delta.y) < _labelHalf[a].y + _labelHalf[b].y + 4f;
        }

        bool ShowsLabel(int index, int count)
        {
            if (markers[index].CityId == _selectedId)
                return true;

            for (var i = 0; i < count; i++)
            {
                if (i == index || !_individual[i])
                    continue;

                var distance = Vector2.Distance(_screen[index], _screen[i]);
                if (distance >= 72f)
                    continue;

                if (markers[i] != null && markers[i].CityId == _selectedId)
                    return false;
                if (i < index)
                    return false;
            }

            return true;
        }

        static void SetLabelVisible(Text label, bool visible)
        {
            var parent = label.transform.parent;
            var target = parent != null && parent.name.StartsWith("Plate_", System.StringComparison.Ordinal)
                ? parent.gameObject
                : label.gameObject;
            if (target.activeSelf != visible)
                target.SetActive(visible);
        }

        struct MarkerView
        {
            public MapCityMarker Marker;
            public SpriteRenderer Core;
            public SpriteRenderer Glow;
        }

        sealed class GroupMark
        {
            public GameObject Root;
            public SpriteRenderer Icon;
            public Text Count;
            public int Members;
            public int CaptionMembers = -1;
            public string Caption = string.Empty;
            public Vector2 InkOffset;
            public bool InkReady;
            public Vector2 Screen;
            public float Shown;
            public bool Active;

            public void DestroyMark()
            {
                if (Root != null)
                    DestroyObject(Root);
                if (Count != null)
                    DestroyObject(Count.gameObject);
            }

            static void DestroyObject(UnityEngine.Object target)
            {
                if (target == null)
                    return;
                if (Application.isPlaying)
                    Destroy(target);
                else
                    DestroyImmediate(target);
            }
        }

        sealed class Binding
        {
            public string CityId;
            public LocalizedString Source;
            public Text Target;
            public string Value = string.Empty;
            public LocalizedString.ChangeHandler Handler;
        }
    }
}
