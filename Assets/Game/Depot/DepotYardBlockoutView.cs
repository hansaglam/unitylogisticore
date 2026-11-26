using System.Collections.Generic;
using UnityEngine;

namespace LogistiCore.Depot
{
    /// <summary>
    /// Art-directed STATIC 2.5D blockout: fixed authored polygons, no RNG,
    /// generated skylines, fake cargo or vehicle animation. This is a
    /// temporary Editor prototype, NOT final commercial building artwork.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DepotYardBlockoutView : MonoBehaviour
    {
        [SerializeField] DepotVisualStage editorPreviewStage = DepotVisualStage.ReservedParcel;

        static readonly Color32 Tarmac = new Color32(30, 52, 63, 255);
        static readonly Color32 Road = new Color32(22, 39, 49, 255);
        static readonly Color32 Lane = new Color32(189, 172, 127, 255);
        static readonly Color32 Parcel = new Color32(49, 78, 89, 255);
        static readonly Color32 Perimeter = new Color32(115, 156, 165, 255);
        static readonly Color32 Curb = new Color32(81, 117, 130, 255);
        static readonly Color32 WallBright = new Color32(111, 144, 157, 255);
        static readonly Color32 WallShade = new Color32(70, 103, 122, 255);
        static readonly Color32 Roof = new Color32(58, 90, 111, 255);
        static readonly Color32 RoofEdge = new Color32(127, 166, 179, 255);
        static readonly Color32 Dock = new Color32(31, 49, 62, 255);
        static readonly Color32 Readied = new Color32(218, 181, 113, 255);
        // Authored, fixed-perspective depot material family. These are geometry
        // details, not textures, fictional equipment or generated variations.
        static readonly Color32 Shadow = new Color32(18, 33, 43, 255);
        static readonly Color32 Concrete = new Color32(94, 119, 124, 255);
        static readonly Color32 ConcreteEdge = new Color32(149, 170, 167, 255);
        static readonly Color32 Steel = new Color32(148, 174, 178, 255);
        static readonly Color32 RoofRib = new Color32(78, 112, 130, 255);
        static readonly Color32 Vent = new Color32(37, 65, 79, 255);
        static readonly Color32 DockSeal = new Color32(19, 33, 44, 255);
        static readonly Color32 Safety = new Color32(205, 171, 105, 255);

        readonly List<Vector3> _vertices = new List<Vector3>(140);
        readonly List<Color32> _colors = new List<Color32>(140);
        readonly List<int> _triangles = new List<int>(200);
        Mesh _mesh;
        Material _material;
        MeshFilter _filter;
        MeshRenderer _renderer;
        DepotVisualStage _stage;
        bool _initialized;

        public DepotVisualStage Stage => _stage;
        public bool ShowsWarehouse => _stage == DepotVisualStage.StorageOperational;
        public bool ShowsVehicle => false; // No verified vehicle location exists yet.
        public int AuthoredTriangleCount => _triangles.Count / 3;
        public bool ShowsLoadingDockArt => ShowsWarehouse;

        void Awake()
        {
            if (Application.isPlaying)
                SetStage(DepotVisualStage.Unverified);
        }

        void OnEnable()
        {
            if (!Application.isPlaying)
                SetStage(editorPreviewStage);
            else if (!_initialized)
                SetStage(DepotVisualStage.Unverified);
        }

        /// <summary>Runtime state comes ONLY from DepotVisualStagePolicy.</summary>
        public void SetStage(DepotVisualStage stage)
        {
            _stage = stage;
            Rebuild();
        }

        void Rebuild()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            if (_filter == null || _renderer == null)
                return;

            if (_material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null)
                {
                    _renderer.enabled = false;
                    return;
                }
                _material = new Material(shader)
                {
                    name = "Depot2D_AuthoredBlockout",
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (_material.HasProperty("_MainTex"))
                    _material.SetTexture("_MainTex", Texture2D.whiteTexture);
            }

            if (_mesh == null)
            {
                _mesh = new Mesh
                {
                    name = "Depot2D_AuthoredGeometry",
                    hideFlags = HideFlags.HideAndDontSave
                };
                _mesh.MarkDynamic();
            }

            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();

            // An empty *neutral* yard exists as a stage backdrop. It is not
            // an account-owned depot if account verification has not succeeded.
            Quad(V(-8f, -4.1f), V(-0.2f, -1.1f), V(8f, -3.9f), V(0f, -7f), Road);
            Quad(V(-7.4f, -2.1f), V(-0.3f, 0.5f), V(7.2f, -2.2f), V(0.2f, -4.8f), Tarmac);
            for (var i = 0; i < 5; i++)
            {
                var x = -5.2f + i * 2.4f;
                Strip(V(x, -4.7f + i * 0.04f),
                    V(x + 0.8f, -4.42f + i * 0.04f), 0.038f, Lane);
            }

            if (_stage != DepotVisualStage.Unverified)
                BuildParcel(_stage);
            if (_stage == DepotVisualStage.StorageOperational)
                BuildWarehouse();
            else if (_stage == DepotVisualStage.ActivationReady)
                BuildPreparedPad();

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
            _filter.sharedMesh = _mesh;
            _renderer.sharedMaterial = _material;
            _renderer.sortingOrder = 0;
            _renderer.enabled = true;
            _initialized = true;
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            var start = _vertices.Count;
            _vertices.Add(new Vector3(a.x, a.y, 0f));
            _vertices.Add(new Vector3(b.x, b.y, 0f));
            _vertices.Add(new Vector3(c.x, c.y, 0f));
            _vertices.Add(new Vector3(d.x, d.y, 0f));
            for (var i = 0; i < 4; i++) _colors.Add(color);
            _triangles.Add(start); _triangles.Add(start + 1); _triangles.Add(start + 2);
            _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 3);
        }

        void Strip(Vector2 a, Vector2 b, float thickness, Color32 color)
        {
            var axis = (b - a).normalized;
            var perpendicular = new Vector2(-axis.y, axis.x) * thickness * 0.5f;
            Quad(a + perpendicular, b + perpendicular,
                b - perpendicular, a - perpendicular, color);
        }

        void BuildParcel(DepotVisualStage stage)
        {
            var a = V(-3.7f, -0.3f);
            var b = V(0.4f, 1.15f);
            var c = V(4.7f, -0.35f);
            var d = V(0.6f, -1.9f);
            Quad(a, b, c, d, Parcel);
            var color = stage == DepotVisualStage.ActivationReady ? Readied : Perimeter;
            Strip(a, b, 0.055f, color);
            Strip(b, c, 0.055f, color);
            Strip(c, d, 0.055f, color);
            Strip(d, a, 0.055f, color);

            // Three surveyed corners and access curb are fixed physical
            // fixtures only. No trucks or fake operating loading bays.
            foreach (var corner in new[] { a, b, c, d })
            {
                Quad(corner + V(-0.11f, -0.055f), corner + V(0f, 0.08f),
                    corner + V(0.11f, -0.055f), corner + V(0f, -0.18f), Shadow);
                Strip(corner + V(0f, -0.12f), corner + V(0f, 0.10f),
                    0.042f, stage == DepotVisualStage.ActivationReady ? Readied : Steel);
            }

            // Road edge follows the same 3/4 projection as the warehouse.
            Strip(V(0.2f, -2.65f), V(4.8f, -1.05f), 0.16f, Curb);
            Strip(V(0.2f, -2.79f), V(4.8f, -1.19f), 0.038f, ConcreteEdge);
            for (var i = 0; i < 3; i++)
            {
                var t = 0.19f + i * 0.29f;
                var start = Vector2.Lerp(V(0.3f, -2.2f), V(4.4f, -0.82f), t);
                Strip(start, start + V(-0.8f, -0.45f), 0.035f, Perimeter);
            }
        }

        void BuildPreparedPad()
        {
            // Prepared foundation only, never drawn as a working warehouse.
            Quad(V(-3.25f, -0.32f), V(0.45f, 0.98f),
                V(4.15f, -0.36f), V(0.45f, -1.58f),
                new Color32(64, 91, 101, 255));
            Strip(V(-3.25f, -0.32f), V(0.45f, 0.98f), 0.035f, Readied);
            Strip(V(0.45f, 0.98f), V(4.15f, -0.36f), 0.035f, Readied);
            // An unoccupied slab with visible expansion joints; it cannot
            // be mistaken for an operational warehouse.
            for (var i = 1; i < 4; i++)
            {
                var t = i / 4f;
                var north = Vector2.Lerp(V(-3.25f, -0.32f), V(0.45f, 0.98f), t);
                var south = Vector2.Lerp(V(0.45f, -1.58f), V(4.15f, -0.36f), t);
                Strip(north, south, 0.025f, ConcreteEdge);
            }
        }

        void BuildWarehouse()
        {
            // The shadow and loading apron visually ground the building.
            // Neither carries cargo data or implies a dispatched vehicle.
            Quad(V(-3.42f, -0.18f), V(0.42f, -1.94f),
                V(4.72f, -0.70f), V(0.76f, 0.22f), Shadow);
            Quad(V(0.35f, -1.75f), V(4.27f, -0.54f),
                V(4.88f, -1.04f), V(0.89f, -2.38f), Concrete);
            Strip(V(0.89f, -2.38f), V(4.88f, -1.04f), 0.055f, ConcreteEdge);

            // Near wall, darker side wall, then roof; all vertices are authored
            // together to maintain one unchanging mobile isometric projection.
            Quad(V(0.4f, -0.2f), V(4.2f, 0.95f),
                V(4.2f, -0.52f), V(0.4f, -1.75f), WallBright);
            Quad(V(-3.2f, 1.42f), V(0.4f, -0.2f),
                V(0.4f, -1.75f), V(-3.2f, -0.14f), WallShade);
            Quad(V(-3.2f, 1.42f), V(0.55f, 2.45f),
                V(4.2f, 0.95f), V(0.4f, -0.2f), Roof);
            Strip(V(-3.2f, 1.42f), V(0.55f, 2.45f), 0.075f, RoofEdge);
            Strip(V(0.55f, 2.45f), V(4.2f, 0.95f), 0.075f, RoofEdge);
            Strip(V(4.2f, 0.95f), V(0.4f, -0.2f), 0.065f, RoofEdge);
            // Four hand-placed standing seams and two static roof vents.
            // Keep the roof perspective and the screen-space weight unified.
            for (var i = 1; i <= 4; i++)
            {
                var t = i / 5f;
                var rear = Vector2.Lerp(V(-3.2f, 1.42f), V(0.55f, 2.45f), t);
                var front = Vector2.Lerp(V(0.4f, -0.2f), V(4.2f, 0.95f), t);
                Strip(rear, front, 0.024f, RoofRib);
            }
            for (var i = 0; i < 2; i++)
            {
                var x = -1.10f + i * 1.50f;
                Quad(V(x, 1.29f), V(x + 0.48f, 1.42f),
                    V(x + 0.78f, 1.29f), V(x + 0.26f, 1.14f), Vent);
                Strip(V(x, 1.29f), V(x + 0.48f, 1.42f), 0.032f, Steel);
            }

            // Closed loading dock doors. No palettes, loaded trucks or
            // forklifts appear without an inventory / asset position record.
            var edgeStart = V(0.45f, -1.70f);
            var edgeEnd = V(4.15f, -0.53f);
            for (var i = 0; i < 3; i++)
            {
                var t = 0.12f + i * 0.30f;
                var foot = Vector2.Lerp(edgeStart, edgeEnd, t);
                var right = foot + (edgeEnd - edgeStart) * 0.20f;
                Quad(foot + V(-0.07f, -0.04f), right + V(0.07f, -0.02f),
                    right + V(0.07f, 0.98f), foot + V(-0.07f, 0.98f), DockSeal);
                Quad(foot, right, right + V(0f, 0.9f),
                    foot + V(0f, 0.9f), Dock);
                // Closed sectional panels communicate no loading activity.
                for (var panel = 1; panel < 4; panel++)
                {
                    var y = panel * 0.20f;
                    Strip(foot + V(0f, y), right + V(0f, y + 0.02f),
                        0.017f, Steel);
                }
                Strip(foot, right, 0.055f, Curb);
                // Stationary wheel guards, not loading equipment.
                var front = Vector2.Lerp(foot, right, 0.5f);
                Strip(front + V(0.15f, -0.12f), front + V(0.29f, -0.22f),
                    0.055f, Safety);
            }
        }

        void OnDisable()
        {
            if (_filter != null) _filter.sharedMesh = null;
            if (_renderer != null) _renderer.sharedMaterial = null;
            Release();
        }

        void OnDestroy()
        {
            Release();
        }

        void Release()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
                _mesh = null;
            }
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
                _material = null;
            }
            _initialized = false;
        }
    }
}
