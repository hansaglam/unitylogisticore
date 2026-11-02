using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LogistiCore.Map
{
    /// <summary>
    /// Swaps the map's coastline renderers onto the premium ribbon meshes.
    /// The scene asset keeps the original hardware lines. This component never writes it.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class MapRibbonPresenter : MonoBehaviour
    {
        public const string MapScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        public const string CoastResource = "MapRibbonCoast";
        public const string TurkeyResource = "MapRibbonTurkey";
        public const string MaterialResource = "MapRibbonLine";

        MeshFilter _coast;
        MeshFilter _turkey;
        MeshRenderer _coastRenderer;
        MeshRenderer _turkeyRenderer;
        Mesh _originalCoast;
        Mesh _originalTurkey;
        Material _originalCoastMaterial;
        Material _originalTurkeyMaterial;
        Mesh _coastRibbon;
        Mesh _turkeyRibbon;
        Material _runtime;
        float _appliedOrtho = -1f;
        bool _premium;

        public bool ShowingPremium => _premium;
        public Material RuntimeMaterial => _runtime;

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

        public static bool Accepts(string scenePath, int stampVersion, bool hasCoast, bool hasTurkey)
        {
            return scenePath == MapScenePath &&
                   stampVersion == MapSceneStamp.Current &&
                   hasCoast &&
                   hasTurkey;
        }

        public static MapRibbonPresenter Attach(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            if (!TryFind(scene, out var stamp, out var camera, out var coast, out var turkey))
                return null;
            if (!Accepts(scene.path, stamp.Version, true, true))
                return null;

            var existing = camera.GetComponent<MapRibbonPresenter>();
            if (existing != null)
                return existing;

            var coastRibbon = Resources.Load<Mesh>(CoastResource);
            var turkeyRibbon = Resources.Load<Mesh>(TurkeyResource);
            var template = Resources.Load<Material>(MaterialResource);
            if (coastRibbon == null || turkeyRibbon == null || template == null)
                return null;

            var runtime = new Material(template) { hideFlags = HideFlags.DontSave };
            ApplyStyle(runtime);
            var presenter = camera.gameObject.AddComponent<MapRibbonPresenter>();
            presenter.hideFlags = HideFlags.DontSave;
            presenter.Bind(coast, turkey, coastRibbon, turkeyRibbon, runtime);
            presenter.ShowPremium();
            return presenter;
        }

        public void Bind(
            MeshFilter coast,
            MeshFilter turkey,
            Mesh coastRibbon,
            Mesh turkeyRibbon,
            Material runtime)
        {
            Release();
            _coast = coast;
            _turkey = turkey;
            _coastRenderer = coast != null ? coast.GetComponent<MeshRenderer>() : null;
            _turkeyRenderer = turkey != null ? turkey.GetComponent<MeshRenderer>() : null;
            _coastRibbon = coastRibbon;
            _turkeyRibbon = turkeyRibbon;
            _runtime = runtime;
            if (coast != null)
                _originalCoast = coast.sharedMesh;
            if (_coastRenderer != null)
                _originalCoastMaterial = _coastRenderer.sharedMaterial;
            if (turkey != null)
                _originalTurkey = turkey.sharedMesh;
            if (_turkeyRenderer != null)
                _originalTurkeyMaterial = _turkeyRenderer.sharedMaterial;
        }

        public bool ShowPremium()
        {
            if (_coast == null || _turkey == null || _coastRenderer == null || _turkeyRenderer == null)
                return false;
            if (_coastRibbon == null || _turkeyRibbon == null || _runtime == null)
                return false;

            ApplyStyle(_runtime);
            _coast.sharedMesh = _coastRibbon;
            _turkey.sharedMesh = _turkeyRibbon;
            _coastRenderer.sharedMaterial = _runtime;
            _turkeyRenderer.sharedMaterial = _runtime;
            _premium = true;
            _appliedOrtho = -1f;
            return true;
        }

        public void ShowOriginal()
        {
            _premium = false;
            RestoreRenderers();
        }

        public void ApplyZoom(float orthographicSize)
        {
            if (_runtime == null || !_premium)
                return;
            if (Mathf.Abs(orthographicSize - _appliedOrtho) < 0.0001f)
                return;

            _appliedOrtho = orthographicSize;
            _runtime.SetFloat("_OrthoSize", orthographicSize);
        }

        public void ApplyZoomFromCamera(Camera camera)
        {
            var owner = GetComponent<Camera>();
            if (owner == null || camera != owner || !camera.orthographic)
                return;

            ApplyZoom(camera.orthographicSize);
        }

        public void Release()
        {
            var runtime = _runtime;
            _runtime = null;
            _premium = false;
            _appliedOrtho = -1f;
            if (runtime != null)
                DestroyObject(runtime);

            RestoreRenderers();
            _coast = null;
            _turkey = null;
            _coastRenderer = null;
            _turkeyRenderer = null;
            _originalCoast = null;
            _originalTurkey = null;
            _originalCoastMaterial = null;
            _originalTurkeyMaterial = null;
            _coastRibbon = null;
            _turkeyRibbon = null;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        }

        void OnDestroy()
        {
            Release();
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            ApplyZoomFromCamera(camera);
        }

        void RestoreRenderers()
        {
            if (_coast != null)
                _coast.sharedMesh = _originalCoast;
            if (_coastRenderer != null)
                _coastRenderer.sharedMaterial = _originalCoastMaterial;
            if (_turkey != null)
                _turkey.sharedMesh = _originalTurkey;
            if (_turkeyRenderer != null)
                _turkeyRenderer.sharedMaterial = _originalTurkeyMaterial;
        }

        static void ApplyStyle(Material material)
        {
            material.SetFloat("_SoftnessPixels", MapRibbonStyle.SoftnessPixels);
            material.SetFloat("_SmallRingWorld", MapRibbonStyle.SmallRingWorld);
            material.SetFloat("_DetailOrtho", MapRibbonStyle.DetailOrtho);
            material.SetFloat("_BorderNear", MapRibbonStyle.BorderFadeNear);
            material.SetFloat("_BorderFar", MapRibbonStyle.BorderFadeFar);
            material.SetFloat("_BorderFloor", MapRibbonStyle.BorderFadeFloor);
            material.SetVector("_TurkeyMul", MapRibbonStyle.TurkeyPresentation);
        }

        static bool TryFind(
            Scene scene,
            out MapSceneStamp stamp,
            out Camera camera,
            out MeshFilter coast,
            out MeshFilter turkey)
        {
            stamp = null;
            camera = null;
            coast = null;
            turkey = null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (stamp == null)
                    stamp = roots[i].GetComponentInChildren<MapSceneStamp>(true);
                if (camera == null)
                {
                    var rig = roots[i].GetComponentInChildren<MapCameraRig>(true);
                    if (rig != null)
                        camera = rig.GetComponent<Camera>();
                }

                if (coast != null && turkey != null)
                    continue;

                var filters = roots[i].GetComponentsInChildren<MeshFilter>(true);
                for (var f = 0; f < filters.Length; f++)
                {
                    var name = filters[f].gameObject.name;
                    if (coast == null && name == "Coastlines")
                        coast = filters[f];
                    else if (turkey == null && name == "TurkeyCoast")
                        turkey = filters[f];
                }
            }

            return stamp != null && camera != null && coast != null && turkey != null;
        }

        static void DestroyObject(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
