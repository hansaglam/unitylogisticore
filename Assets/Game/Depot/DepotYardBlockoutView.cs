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

            // Yard access road and three unoccupied future loading positions.
            Strip(V(0.2f, -2.65f), V(4.8f, -1.05f), 0.16f, Curb);
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
        }

        void BuildWarehouse()
        {
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

            // Closed loading dock doors. No palettes, loaded trucks or
            // forklifts appear without an inventory / asset position record.
            var edgeStart = V(0.45f, -1.70f);
            var edgeEnd = V(4.15f, -0.53f);
            for (var i = 0; i < 3; i++)
            {
                var t = 0.12f + i * 0.30f;
                var foot = Vector2.Lerp(edgeStart, edgeEnd, t);
                var right = foot + (edgeEnd - edgeStart) * 0.20f;
                Quad(foot, right, right + V(0f, 0.9f),
                    foot + V(0f, 0.9f), Dock);
                Strip(foot, right, 0.05f, Curb);
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
