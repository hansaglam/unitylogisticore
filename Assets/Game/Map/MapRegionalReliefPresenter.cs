using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogistiCore.Map
{
    /// <summary>
    /// Runtime-only blend of the regional Türkiye relief onto the existing Relief renderer.
    /// The scene keeps its global material. This component never writes that asset.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class MapRegionalReliefPresenter : MonoBehaviour
    {
        public const string MapScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        public const string MaterialResource = "MapRegionalRelief";

        bool _globalOnly;
        Material _original;
        Material _runtime;
        MeshRenderer _relief;
        Camera _camera;
        float _applied = -1f;

        public bool GlobalOnly
        {
            get => _globalOnly;
            set
            {
                _globalOnly = value;
                _applied = -1f;
                Apply();
            }
        }

        public Material RuntimeMaterial => _runtime;
        public Material OriginalMaterial => _original;

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

        public static bool Accepts(string scenePath, int stampVersion, bool hasCamera, bool hasRelief, bool hasMaterial)
        {
            return scenePath == MapScenePath &&
                   stampVersion == MapSceneStamp.Current &&
                   hasCamera &&
                   hasRelief &&
                   hasMaterial;
        }

        public static Material InstantiateTemplate(Material template)
        {
            if (template == null)
                return null;
            return new Material(template);
        }

        public static MapRegionalReliefPresenter Attach(Scene scene)
        {
            return Attach(scene, Resources.Load<Material>(MaterialResource));
        }

        public static MapRegionalReliefPresenter Attach(Scene scene, Material template)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            if (!TryFind(scene, out var stamp, out var rig, out var relief))
                return null;
            if (!Accepts(scene.path, stamp.Version, true, true, relief.sharedMaterial != null))
                return null;

            var camera = rig.GetComponent<Camera>();
            if (camera == null)
                return null;

            var existing = camera.GetComponent<MapRegionalReliefPresenter>();
            if (existing != null)
                return existing;

            var runtime = InstantiateTemplate(template);
            if (runtime == null)
                return null;

            var presenter = camera.gameObject.AddComponent<MapRegionalReliefPresenter>();
            if (!presenter.TryUse(relief, camera, runtime))
            {
                DestroyObject(runtime);
                DestroyObject(presenter);
                return null;
            }

            return presenter;
        }

        public bool TryUse(MeshRenderer relief, Camera camera, Material runtime)
        {
            if (relief == null || camera == null || runtime == null || relief.sharedMaterial == null)
                return false;
            if (_runtime != null)
                return true;

            _original = relief.sharedMaterial;
            _relief = relief;
            _camera = camera;
            _runtime = runtime;
            _relief.sharedMaterial = _runtime;
            _applied = -1f;
            Apply();
            return true;
        }

        public void Release()
        {
            var relief = _relief;
            var original = _original;
            var runtime = _runtime;
            _runtime = null;
            _original = null;
            _relief = null;
            _camera = null;
            _applied = -1f;
            if (runtime != null)
                DestroyObject(runtime);
            if (relief != null)
                relief.sharedMaterial = original;
        }

        public float Evaluate()
        {
            if (_camera == null)
                return 0f;

            var aspect = _camera.aspect < 0.05f ? 16f / 9f : _camera.aspect;
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkeyFrame);
            ViewBounds(aspect, out var viewMin, out var viewMax);
            return MapRegionalRelief.BlendWeight(
                _camera.orthographicSize,
                turkeyFrame,
                MapRegionalRelief.IntersectsView(viewMin, viewMax),
                _globalOnly);
        }

        void LateUpdate()
        {
            Apply();
        }

        void OnDestroy()
        {
            Release();
        }

        void Apply()
        {
            if (_runtime == null)
                return;

            var weight = Evaluate();
            if (Mathf.Abs(weight - _applied) < 0.001f)
                return;

            _applied = weight;
            _runtime.SetFloat("_Detail", weight);
        }

        void ViewBounds(float aspect, out Vector2 min, out Vector2 max)
        {
            var halfHeight = _camera.orthographicSize;
            var halfWidth = halfHeight * aspect;
            var center = _camera.transform.position;
            min = new Vector2(center.x - halfWidth, center.y - halfHeight);
            max = new Vector2(center.x + halfWidth, center.y + halfHeight);
        }

        static bool TryFind(Scene scene, out MapSceneStamp stamp, out MapCameraRig rig, out MeshRenderer relief)
        {
            stamp = null;
            rig = null;
            relief = null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (stamp == null)
                    stamp = roots[i].GetComponentInChildren<MapSceneStamp>(true);
                if (rig == null)
                    rig = roots[i].GetComponentInChildren<MapCameraRig>(true);
                if (relief == null)
                {
                    var renderers = roots[i].GetComponentsInChildren<MeshRenderer>(true);
                    for (var r = 0; r < renderers.Length; r++)
                    {
                        if (renderers[r].gameObject.name == "Relief")
                        {
                            relief = renderers[r];
                            break;
                        }
                    }
                }
            }

            return stamp != null && rig != null && relief != null;
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
