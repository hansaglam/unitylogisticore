using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// Cartographic view of a provider-supplied *preview* polyline. Unlike
    /// MapSchematicLinks this never invents intermediate road geometry.
    /// No vehicle, depot, delivery or payment is simulated.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapRoadRoutePreviewLine : MonoBehaviour
    {
        LineRenderer _line;
        Material _material;
        Camera _camera;
        MapCameraRig _rig;
        bool _hasRoute;
        float _lastSize = -1f;
        int _lastHeight = -1;

        public bool HasRoute => _hasRoute;

        public static MapRoadRoutePreviewLine Ensure(MapMarkerLayer markers, Camera camera)
        {
            if (!Application.isPlaying || markers == null || camera == null)
                return null;
            var view = markers.GetComponent<MapRoadRoutePreviewLine>();
            if (view == null)
                view = markers.gameObject.AddComponent<MapRoadRoutePreviewLine>();
            view.Bind(camera);
            return view;
        }

        public void Bind(Camera camera)
        {
            _camera = camera;
            _rig = camera != null ? camera.GetComponent<MapCameraRig>() : null;
        }

        public bool Show(MapVerifiedRoute route)
        {
            Clear();
            if (route == null || route.Mode != MapTransportMode.Road ||
                _rig == null || _camera == null ||
                !route.TryProjectForDisplay(out var points))
                return false;

            if (_line == null && !CreateLine())
                return false;

            _line.positionCount = points.Length;
            _line.SetPositions(points);
            _hasRoute = true;
            _lastSize = -1f;
            Refresh();
            return true;
        }

        bool CreateLine()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                return false;

            _material = new Material(shader)
            {
                name = "VerifiedRoadPreview_VisualOnly",
                hideFlags = HideFlags.HideAndDontSave
            };
            var lineObject = new GameObject("ActualRoadPreview_NoDispatch");
            lineObject.hideFlags = HideFlags.DontSave;
            lineObject.transform.SetParent(transform, false);
            _line = lineObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.sharedMaterial = _material;
            _line.textureMode = LineTextureMode.Stretch;
            _line.numCornerVertices = 2;
            _line.numCapVertices = 2;
            _line.sortingOrder = 18; // Below city markers, above decorative schematic.
            _line.startColor = new Color(0.18f, 0.83f, 0.88f, 0.94f);
            _line.endColor = new Color(0.18f, 0.83f, 0.88f, 0.94f);
            _line.positionCount = 0;
            _line.enabled = false;
            return true;
        }

        void LateUpdate()
        {
            if (_hasRoute && _camera != null)
                Refresh();
        }

        void Refresh()
        {
            if (_line == null || _rig == null || _camera == null)
                return;

            var visible = _hasRoute &&
                (_rig.Scope == MapScope.Turkey || _rig.Scope == MapScope.Europe);
            _line.enabled = visible;
            if (!visible)
                return;

            var size = _camera.orthographicSize;
            var height = Screen.height;
            if (Mathf.Approximately(_lastSize, size) && _lastHeight == height)
                return;
            _lastSize = size;
            _lastHeight = height;
            // Consistent ~3px line weight across map zoom, not a thick glow.
            var worldWidth = MapMarkerLod.ScaleForPixels(3f, size, height);
            _line.widthMultiplier = Mathf.Clamp(worldWidth, 0.0001f, 2.5f);
        }

        public void Clear()
        {
            _hasRoute = false;
            if (_line != null)
            {
                _line.enabled = false;
                _line.positionCount = 0;
            }
        }

        void OnDisable()
        {
            if (_line != null)
                _line.enabled = false;
        }

        void OnDestroy()
        {
            if (_line != null)
                Destroy(_line.gameObject);
            if (_material != null)
                Destroy(_material);
            _line = null;
            _material = null;
        }
    }
}
