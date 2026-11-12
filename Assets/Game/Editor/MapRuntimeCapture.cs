using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using LogistiCore.Localization;
using LogistiCore.Map;
using LogistiCore.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.EditorTools
{
    public static class MapRuntimeCapture
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string Root = "/tmp/logisticore-map-02c-04a";

        static readonly string[] RegionNames =
        {
            "Dünya",
            "Türkiye",
            "Avrupa",
            "Asya",
            "Afrika",
            "Kuzey Amerika",
            "Güney Amerika",
            "Okyanusya"
        };

        const string CaptureFlag = "LogistiCore.MapRuntimeCapture";
        const string RootKey = "LogistiCore.MapRuntimeCapture.Root";
        const string DensityKey = "LogistiCore.MapRuntimeCapture.Density";
        const string PlacementKey = "LogistiCore.MapRuntimeCapture.Placement";
        const string CityKey = "LogistiCore.MapRuntimeCapture.City";
        const string DropdownKey = "LogistiCore.MapRuntimeCapture.Dropdown";
        const string HitKey = "LogistiCore.MapRuntimeCapture.Hit";
        const string Ui01Key = "LogistiCore.MapRuntimeCapture.Ui01";
        const string Ui02Key = "LogistiCore.MapRuntimeCapture.Ui02";
        const string CopernicusKey = "LogistiCore.MapRuntimeCapture.Copernicus";
        const string R1Key = "LogistiCore.MapRuntimeCapture.R1";
        const string WaterKey = "LogistiCore.MapRuntimeCapture.Water";
        const string Ui04aKey = "LogistiCore.MapRuntimeCapture.Ui04a";
        const string Ui04aR1Key = "LogistiCore.MapRuntimeCapture.Ui04aR1";
        const string Ui03bKey = "LogistiCore.MapRuntimeCapture.Ui03b";
        const string Ui04bKey = "LogistiCore.MapRuntimeCapture.Ui04b";
        const string Ui04cKey = "LogistiCore.MapRuntimeCapture.Ui04c";
        const string Ui04dKey = "LogistiCore.MapRuntimeCapture.Ui04d";
        const string Ui04eKey = "LogistiCore.MapRuntimeCapture.Ui04e";
        const string Ui06aKey = "LogistiCore.MapRuntimeCapture.Ui06a";
        const string Ui06aR1Key = "LogistiCore.MapRuntimeCapture.Ui06aR1";
        const string Ios04aKey = "LogistiCore.MapRuntimeCapture.Ios04a";
        const string Ios04aR1Key = "LogistiCore.MapRuntimeCapture.Ios04aR1";
        const string DeviceIos04cKey = "LogistiCore.MapRuntimeCapture.DeviceIos04c";
        const string DeviceIos04cR1Key = "LogistiCore.MapRuntimeCapture.DeviceIos04cR1";
        const int DiagNone = 0;
        const int DiagGlobalOnly = 1;
        const int DiagCopernicusOnly = 2;
        const int DiagOldRegional = 3;
        const int DiagAlphaCoverage = 4;
        static Texture2D _diagNavy;
        static Texture2D _diagAlpha;
        static double _localeStart;
        static StringBuilder _notes;
        static CaptureShot[] _shots;
        static int _shotIndex;
        static int _shotFailures;
        static double _shotWait;
        static Scene _captureScene;
        static Canvas _canvas;
        static Camera _camera;
        static MapCommandShell _shell;
        static MapCountryLabelLayer _labels;
        static MapWaterLabelLayer _water;
        static MapMarkerLayer _markers;
        static SafeAreaRect _safeRect;
        static bool _safeEnabled;
        static RenderMode _savedMode;
        static Camera _savedCamera;
        static float _savedPlane;
        static RenderTexture _savedTarget;

        static string ActiveRoot()
        {
            var stored = SessionState.GetString(RootKey, string.Empty);
            return string.IsNullOrEmpty(stored) ? Root : stored;
        }

        [InitializeOnLoadMethod]
        static void ResumeCapture()
        {
            if (!SessionState.GetBool(CaptureFlag, false))
                return;

            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static void CaptureBaseline()
        {
            Begin(Root, false);
        }

        public static void CaptureDensityBefore()
        {
            Begin("/tmp/logisticore-map-02c-04b-01/before", true);
        }

        public static void CaptureDensityAfter()
        {
            Begin("/tmp/logisticore-map-02c-04b-01/after", true);
        }

        public static void CapturePlacementBefore()
        {
            BeginPlacement("/tmp/logisticore-map-02c-04b-01-r2/before");
        }

        public static void CapturePlacementAfter()
        {
            BeginPlacement("/tmp/logisticore-map-02c-04b-01-r2/after");
        }

        public static void CaptureMarginAfter()
        {
            BeginPlacement("/tmp/logisticore-map-02c-04b-01-r3/after");
        }

        public static void CaptureCityBefore()
        {
            BeginCity("/tmp/logisticore-map-02c-04b-02/before");
        }

        public static void CaptureCityAfter()
        {
            BeginCity("/tmp/logisticore-map-02c-04b-02/after");
        }

        static void BeginCity(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/report");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, true);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginPlacement(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/report");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, true);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        public static void CaptureHitArea()
        {
            BeginHit("/tmp/logisticore-map-02c-04b-03-r1");
        }

        static void BeginHit(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/report");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, true);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        public static void CaptureDropdown()
        {
            BeginDropdown("/tmp/logisticore-map-02c-04b-03");
        }

        public static void CaptureUi01()
        {
            BeginUi01("/tmp/logisticore-unity-ios-ui-01/after");
        }

        public static void CaptureUi02()
        {
            BeginUi02("/tmp/logisticore-unity-ios-ui-02/after");
        }

        public static void CaptureCopernicus()
        {
            BeginCopernicus("/tmp/logisticore-map-02c-03b-04b");
        }

        public static void CaptureR1Diagnostics()
        {
            BeginR1("/tmp/logisticore-map-02c-03b-04b-r1", false);
        }

        public static void CaptureR1After()
        {
            BeginR1("/tmp/logisticore-map-02c-03b-04b-r1", true);
        }

        public static void CaptureWater()
        {
            BeginWater("/tmp/logisticore-map-03a");
        }

        public static void CaptureWaterR1()
        {
            BeginWater("/tmp/logisticore-map-03a-r1/after");
        }

        public static void CaptureUi04a()
        {
            BeginUi04a("/tmp/logisticore-ui-04a");
        }

        public static void CaptureUi04aR1()
        {
            BeginUi04aR1("/tmp/logisticore-ui-04a-r1/after");
        }

        public static void CaptureUi03b()
        {
            BeginUi03b("/tmp/logisticore-ui-03b");
        }

        public static void CaptureUi04b()
        {
            BeginUi04b("/tmp/logisticore-ui-04b");
        }

        public static void CaptureUi04c()
        {
            BeginUi04c("/tmp/logisticore-ui-04c");
        }

        public static void CaptureUi04d()
        {
            BeginUi04d("/tmp/logisticore-ui-04d");
        }

        public static void CaptureUi04e()
        {
            BeginUi04e("/tmp/logisticore-ui-04e");
        }

        public static void CaptureUi06a()
        {
            BeginUi06a("/tmp/logisticore-ui-06a");
        }

        public static void CaptureUi06aR1()
        {
            BeginUi06aR1("/tmp/logisticore-ui-06a-r1");
        }

        public static void CaptureIos04a()
        {
            BeginIos04a("/tmp/logisticore-unity-ios-04a");
        }

        public static void CaptureIos04aR1()
        {
            BeginIos04aR1("/tmp/logisticore-unity-ios-04a-r1");
        }

        public static void CaptureIos04c()
        {
            BeginIos04c("/tmp/logisticore-unity-ios-04c");
        }

        public static void CaptureIos04cR1()
        {
            BeginIos04cR1("/tmp/logisticore-unity-ios-04c-r1");
        }

        public static void ProbeGraphics()
        {
            var dir = "/tmp/logisticore-unity-ios-04c-r1/logs";
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory("/tmp/logisticore-unity-ios-04c-r1/reports");
            var text = GraphicsProbeText();
            File.WriteAllText(dir + "/graphics-probe.txt", text);
            File.WriteAllText("/tmp/logisticore-unity-ios-04c-r1/logs/graphics-device.txt", text);
            EditorApplication.Exit(GraphicsCaptureAvailable() ? 0 : 7);
        }

        static void BeginUi04a(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/tests");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, true);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginUi04aR1(string root)
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory("/tmp/logisticore-ui-04a-r1/measurements");
            Directory.CreateDirectory("/tmp/logisticore-ui-04a-r1/reports");
            Directory.CreateDirectory("/tmp/logisticore-ui-04a-r1/tests");
            SessionState.SetString(RootKey, "/tmp/logisticore-ui-04a-r1/after");
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, true);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginUi03b(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/tests");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, true);
            SessionState.SetBool(Ui04bKey, false);
            SessionState.SetBool(Ui04cKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginUi04b(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/tests");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(Ui04bKey, true);
            SessionState.SetBool(Ui04cKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginUi04c(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/tests");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(Ui04bKey, false);
            SessionState.SetBool(Ui04cKey, true);
            SessionState.SetBool(Ui04dKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginUi04d(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/tests");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(Ui04bKey, false);
            SessionState.SetBool(Ui04cKey, false);
            SessionState.SetBool(Ui04dKey, true);
            SessionState.SetBool(Ui04eKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginUi04e(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/tests");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(Ui04bKey, false);
            SessionState.SetBool(Ui04cKey, false);
            SessionState.SetBool(Ui04dKey, false);
            SessionState.SetBool(Ui04eKey, true);
            SessionState.SetBool(Ui06aKey, false);
            SessionState.SetBool(Ui06aR1Key, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginUi06a(string root)
        {
            Directory.CreateDirectory(root + "/after");
            Directory.CreateDirectory(root + "/before");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/tests");
            var prior = "/tmp/logisticore-ui-04e/measurements/market-expanded-fixture-932-rejected.png";
            if (File.Exists(prior))
                File.Copy(prior, root + "/before/market-expanded-932-alpha.png", true);
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(Ui04bKey, false);
            SessionState.SetBool(Ui04cKey, false);
            SessionState.SetBool(Ui04dKey, false);
            SessionState.SetBool(Ui04eKey, false);
            SessionState.SetBool(Ui06aKey, true);
            SessionState.SetBool(Ui06aR1Key, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginUi06aR1(string root)
        {
            Directory.CreateDirectory(root + "/after");
            Directory.CreateDirectory(root + "/before");
            Directory.CreateDirectory(root + "/diagnostics");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/tests");
            CopyPriorMarketExpanded(root + "/before/market-expanded-932.png");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(Ui04bKey, false);
            SessionState.SetBool(Ui04cKey, false);
            SessionState.SetBool(Ui04dKey, false);
            SessionState.SetBool(Ui04eKey, false);
            SessionState.SetBool(Ui06aKey, false);
            SessionState.SetBool(Ui06aR1Key, true);
            SessionState.SetBool(Ios04aKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginIos04a(string root)
        {
            Directory.CreateDirectory(root + "/after");
            Directory.CreateDirectory(root + "/before");
            Directory.CreateDirectory(root + "/tests");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/measurements");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(Ui04bKey, false);
            SessionState.SetBool(Ui04cKey, false);
            SessionState.SetBool(Ui04dKey, false);
            SessionState.SetBool(Ui04eKey, false);
            SessionState.SetBool(Ui06aKey, false);
            SessionState.SetBool(Ui06aR1Key, false);
            SessionState.SetBool(Ios04aKey, true);
            SessionState.SetBool(Ios04aR1Key, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginIos04aR1(string root)
        {
            Directory.CreateDirectory(root + "/after");
            Directory.CreateDirectory(root + "/before");
            Directory.CreateDirectory(root + "/tests");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/measurements");
            var prior = "/tmp/logisticore-unity-ios-04a/after/02-fleet-rail-aligned-2868.png";
            if (File.Exists(prior))
                File.Copy(prior, root + "/before/rail-2868-top-tr.png", true);
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(Ui04bKey, false);
            SessionState.SetBool(Ui04cKey, false);
            SessionState.SetBool(Ui04dKey, false);
            SessionState.SetBool(Ui04eKey, false);
            SessionState.SetBool(Ui06aKey, false);
            SessionState.SetBool(Ui06aR1Key, false);
            SessionState.SetBool(Ios04aKey, false);
            SessionState.SetBool(Ios04aR1Key, true);
            SessionState.SetBool(DeviceIos04cKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginIos04c(string root)
        {
            Directory.CreateDirectory(root + "/after");
            Directory.CreateDirectory(root + "/before");
            Directory.CreateDirectory(root + "/tests");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/measurements");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(Ui04bKey, false);
            SessionState.SetBool(Ui04cKey, false);
            SessionState.SetBool(Ui04dKey, false);
            SessionState.SetBool(Ui04eKey, false);
            SessionState.SetBool(Ui06aKey, false);
            SessionState.SetBool(Ui06aR1Key, false);
            SessionState.SetBool(Ios04aKey, false);
            SessionState.SetBool(Ios04aR1Key, false);
            SessionState.SetBool(DeviceIos04cKey, true);
            SessionState.SetBool(DeviceIos04cR1Key, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginIos04cR1(string root)
        {
            Directory.CreateDirectory(root + "/after");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/tests");
            Directory.CreateDirectory(root + "/logs");
            Directory.CreateDirectory(root + "/measurements");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(Ui04bKey, false);
            SessionState.SetBool(Ui04cKey, false);
            SessionState.SetBool(Ui04dKey, false);
            SessionState.SetBool(Ui04eKey, false);
            SessionState.SetBool(Ui06aKey, false);
            SessionState.SetBool(Ui06aR1Key, false);
            SessionState.SetBool(Ios04aKey, false);
            SessionState.SetBool(Ios04aR1Key, false);
            SessionState.SetBool(DeviceIos04cKey, false);
            SessionState.SetBool(DeviceIos04cR1Key, true);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void CopyPriorMarketExpanded(string dest)
        {
            var priors = new[]
            {
                "/tmp/logisticore-ui-06a/after/market-expanded-932-alpha.png",
                "/tmp/logisticore-ui-06a/measurements/market-expanded-932-alpha-rejected.png",
                "/tmp/logisticore-ui-04e/measurements/market-expanded-fixture-932-rejected.png"
            };
            for (var i = 0; i < priors.Length; i++)
            {
                if (!File.Exists(priors[i]))
                    continue;
                File.Copy(priors[i], dest, true);
                return;
            }
        }

        static void BeginWater(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/report");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/tests");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, true);
            SessionState.SetBool(Ui04aKey, false);
            SessionState.SetBool(Ui04aR1Key, false);
            SessionState.SetBool(Ui03bKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginUi01(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/report");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, true);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginR1(string root, bool after)
        {
            Directory.CreateDirectory(root + "/before");
            Directory.CreateDirectory(root + "/diagnostics");
            Directory.CreateDirectory(root + "/after");
            Directory.CreateDirectory(root + "/reports");
            Directory.CreateDirectory(root + "/tests");
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, true);
            SessionState.SetBool(R1Key + ".After", after);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginCopernicus(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/report");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, true);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginUi02(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/report");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, true);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BeginDropdown(string root)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/report");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, false);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, true);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void Begin(string root, bool density)
        {
            Directory.CreateDirectory(root + "/captures");
            Directory.CreateDirectory(root + "/measurements");
            Directory.CreateDirectory(root + "/report");
            SessionState.SetString(RootKey, root);
            SessionState.SetBool(DensityKey, density);
            SessionState.SetBool(PlacementKey, false);
            SessionState.SetBool(CityKey, false);
            SessionState.SetBool(DropdownKey, false);
            SessionState.SetBool(HitKey, false);
            SessionState.SetBool(Ui01Key, false);
            SessionState.SetBool(Ui02Key, false);
            SessionState.SetBool(CopernicusKey, false);
            SessionState.SetBool(R1Key, false);
            SessionState.SetBool(WaterKey, false);
            SessionState.SetBool(CaptureFlag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(CaptureFlag, false))
                return;

            SessionState.SetBool(CaptureFlag, false);
            _localeStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += WaitForLocale;
        }

        static void WaitForLocale()
        {
            if (!LocaleService.IsReady)
            {
                if (EditorApplication.timeSinceStartup - _localeStart > 45d)
                {
                    EditorApplication.update -= WaitForLocale;
                    File.WriteAllText(ActiveRoot() + "/measurements/CAPTURE_VALIDATION.md", "Turkish locale did not become ready.");
                    EditorApplication.Exit(6);
                }

                return;
            }

            if (LocaleService.CurrentCode != "tr")
                LocaleService.TrySelect("tr");
            if (EditorApplication.timeSinceStartup - _localeStart < 20d && !ChipReads("Dünya"))
                return;

            EditorApplication.update -= WaitForLocale;
            RunCapture();
        }

        static bool ChipReads(string value)
        {
            var shell = Find<MapCommandShell>(SceneManager.GetActiveScene());
            return shell != null && ChipText(shell) == value;
        }

        static void RunCapture()
        {
            _notes = new StringBuilder();
            _notes.AppendLine("# Capture validation");
            _notes.AppendLine();
            try
            {
                if (!LocaleService.TrySelect("tr"))
                    throw new InvalidOperationException("Turkish locale was not ready.");

                _captureScene = SceneManager.GetActiveScene();
                if (_captureScene.path != ScenePath)
                    throw new InvalidOperationException("Play mode did not open the map scene.");
                _notes.AppendLine(GraphicsProbeText().Trim());
                if (!GraphicsCaptureAvailable())
                {
                    WriteGraphicsHold(_notes.ToString());
                    RestoreCapture();
                    EditorApplication.Exit(7);
                    return;
                }

                PrepareCapture(_captureScene, _notes);
                _shotIndex = 0;
                _shotFailures = 0;
                _shotWait = EditorApplication.timeSinceStartup;
                EditorApplication.update += StepShots;
            }
            catch (Exception exception)
            {
                _notes.AppendLine("Capture stopped: " + exception.GetType().Name);
                Debug.LogException(exception);
                File.WriteAllText(ActiveRoot() + "/measurements/CAPTURE_VALIDATION.md", _notes.ToString());
                EditorApplication.Exit(6);
            }
        }

        static void StepShots()
        {
            try
            {
                if (_shotIndex >= _shots.Length)
                {
                    EditorApplication.update -= StepShots;
                    FinishCapture();
                    return;
                }

                var shot = _shots[_shotIndex];
                if (!LocaleReady(shot))
                {
                    if (EditorApplication.timeSinceStartup - _shotWait > 8d)
                    {
                        _notes.AppendLine(shot.File + ": locale timeout chip=" + ChipText(_shell) + " expected=" + ExpectedChip(shot.Region, shot.Locale));
                        _shotFailures++;
                        _shotIndex++;
                        _shotWait = EditorApplication.timeSinceStartup;
                    }

                    return;
                }

                if (_shell.enabled)
                    _shell.enabled = false;
                if (!CaptureOne(_canvas, _camera, _shell, _labels, _water, _markers, _safeRect, shot, _notes))
                    _shotFailures++;
                _shotIndex++;
                _shotWait = EditorApplication.timeSinceStartup;
            }
            catch (Exception exception)
            {
                EditorApplication.update -= StepShots;
                _notes.AppendLine("Capture stopped: " + exception.GetType().Name);
                Debug.LogException(exception);
                RestoreCapture();
                File.WriteAllText(ActiveRoot() + "/measurements/CAPTURE_VALIDATION.md", _notes.ToString());
                EditorApplication.Exit(6);
            }
        }

        static void FinishCapture()
        {
            RestoreCapture();
            _notes.AppendLine();
            _notes.AppendLine(_shotFailures == 0
                ? "All required shots passed the HUD-over-map check."
                : _shotFailures + " shots failed the HUD-over-map check.");
            var code = _shotFailures == 0 ? 0 : 4;
            var after = Sha(ScenePath);
            _notes.AppendLine("Scene SHA after capture session: " + after);
            if (!string.Equals(after, SceneSha, StringComparison.OrdinalIgnoreCase))
            {
                _notes.AppendLine("SCENE HASH CHANGED");
                code = 5;
            }

            var validation = _notes.ToString();
            File.WriteAllText(ActiveRoot() + "/measurements/CAPTURE_VALIDATION.md", validation);
            var reports = ActiveRoot() + "/reports";
            if (Directory.Exists(reports))
                File.WriteAllText(reports + "/CAPTURE_VALIDATION.md", validation);
            EditorApplication.Exit(code);
        }

        static bool LocaleReady(CaptureShot shot)
        {
            var expected = ExpectedChip(shot.Region, shot.Locale);
            if (ChipText(_shell) == expected && LocaleService.CurrentCode == shot.Locale)
                return true;

            if (!_shell.enabled)
                _shell.enabled = true;
            if (LocaleService.CurrentCode != shot.Locale)
            {
                LocaleService.TrySelect(shot.Locale);
                return false;
            }

            _shell.SelectRegion(shot.Region);
            return ChipText(_shell) == expected;
        }

        static void PrepareCapture(Scene scene, StringBuilder notes)
        {
            _canvas = Find<Canvas>(scene);
            _camera = Find<Camera>(scene);
            _shell = MapCommandShell.Attach(scene);
            _labels = MapCountryLabelLayer.Attach(scene);
            _water = MapWaterLabelLayer.Attach(scene);
            _markers = Find<MapMarkerLayer>(scene);
            if (_canvas == null || _camera == null || _shell == null || _labels == null || _water == null || _markers == null)
                throw new InvalidOperationException("Map scene is missing a capture component.");
            if (_canvas.renderMode != RenderMode.ScreenSpaceOverlay || _canvas.worldCamera != null)
                throw new InvalidOperationException("Product canvas is not an overlay.");
            if (Count<Camera>(scene) != 1)
                throw new InvalidOperationException("Product scene does not have one camera.");

            FreezeProductSimulation(scene);

            _savedMode = _canvas.renderMode;
            _savedCamera = _canvas.worldCamera;
            _savedPlane = _canvas.planeDistance;
            _savedTarget = _camera.targetTexture;
            _safeRect = Find<SafeAreaRect>(scene);
            _safeEnabled = _safeRect != null && _safeRect.enabled;
            _shots = SessionState.GetBool(DeviceIos04cR1Key, false)
                ? Ios04cR1Shots()
                : SessionState.GetBool(DeviceIos04cKey, false)
                ? Ios04cShots()
                : SessionState.GetBool(Ios04aR1Key, false)
                ? Ios04aR1Shots()
                : SessionState.GetBool(Ios04aKey, false)
                ? Ios04aShots()
                : SessionState.GetBool(Ui06aR1Key, false)
                ? Ui06aR1Shots()
                : SessionState.GetBool(Ui06aKey, false)
                ? Ui06aShots()
                : SessionState.GetBool(Ui04eKey, false)
                ? Ui04eShots()
                : SessionState.GetBool(Ui04dKey, false)
                ? Ui04dShots()
                : SessionState.GetBool(Ui04cKey, false)
                ? Ui04cShots()
                : SessionState.GetBool(Ui04bKey, false)
                ? Ui04bShots()
                : SessionState.GetBool(Ui03bKey, false)
                ? Ui03bShots()
                : SessionState.GetBool(Ui04aR1Key, false)
                ? Ui04aR1Shots()
                : SessionState.GetBool(Ui04aKey, false)
                ? Ui04aShots()
                : SessionState.GetBool(WaterKey, false)
                ? WaterShots()
                : SessionState.GetBool(R1Key, false)
                ? (SessionState.GetBool(R1Key + ".After", false) ? R1AfterShots() : R1DiagnosticShots())
                : SessionState.GetBool(CopernicusKey, false)
                ? CopernicusShots()
                : SessionState.GetBool(Ui02Key, false)
                ? Ui02Shots()
                : SessionState.GetBool(Ui01Key, false)
                ? Ui01Shots()
                : SessionState.GetBool(CityKey, false)
                ? CityShots()
                : SessionState.GetBool(PlacementKey, false)
                ? PlacementShots()
                : SessionState.GetBool(DensityKey, false)
                ? DensityShots()
                : SessionState.GetBool(DropdownKey, false)
                ? DropdownShots()
                : SessionState.GetBool(HitKey, false) ? HitShots() : BaselineShots();

            notes.AppendLine("Locale: " + LocaleService.CurrentCode);
            notes.AppendLine("Product canvas: Screen Space Overlay. The map camera draws relief and ribbons. The HUD is drawn afterward by that same camera, limited to the UI layer, so ribbon depth cannot cover it.");
            notes.AppendLine();
        }

        static void RestoreCapture()
        {
            MapOperationStrip.ClearOverride();
            MapFleetStore.ClearOverride();
            MapJobsStore.ClearOverride();
            MapWarehouseStore.ClearOverride();
            MapMarketStore.ClearOverride();
            MapRoadDeliveryStore.ClearOverride();
            if (_canvas != null)
            {
                _canvas.renderMode = _savedMode;
                _canvas.worldCamera = _savedCamera;
                _canvas.planeDistance = _savedPlane;
            }

            if (_camera != null)
                _camera.targetTexture = _savedTarget;
            if (_safeRect != null)
                _safeRect.enabled = _safeEnabled;
        }

        static bool CaptureOne(
            Canvas canvas,
            Camera camera,
            MapCommandShell shell,
            MapCountryLabelLayer labels,
            MapWaterLabelLayer water,
            MapMarkerLayer markers,
            SafeAreaRect safeRect,
            CaptureShot shot,
            StringBuilder notes)
        {
            if (!GraphicsCaptureAvailable())
            {
                notes.AppendLine(shot.File + ": skipped, graphicsDeviceType=" + SystemInfo.graphicsDeviceType);
                return false;
            }

            var uiObject = new GameObject("MapCaptureUiCamera");
            uiObject.hideFlags = HideFlags.HideAndDontSave;
            var uiCamera = uiObject.AddComponent<Camera>();
            var map = new RenderTexture(shot.Width, shot.Height, 24, RenderTextureFormat.ARGB32);
            var ui = new RenderTexture(shot.Width, shot.Height, 24, RenderTextureFormat.ARGB32);
            Texture2D mapTex = null;
            Texture2D uiTex = null;
            Texture2D output = null;
            try
            {
                if (safeRect != null)
                    safeRect.enabled = false;
                var safeTransform = safeRect != null ? safeRect.GetComponent<RectTransform>() : null;
                if (safeTransform != null)
                {
                    SafeAreaLayout.CalculateAnchors(shot.Safe, shot.Width, shot.Height, out var anchorMin, out var anchorMax);
                    safeTransform.anchorMin = anchorMin;
                    safeTransform.anchorMax = anchorMax;
                    safeTransform.offsetMin = Vector2.zero;
                    safeTransform.offsetMax = Vector2.zero;
                }

                uiCamera.enabled = false;
                uiCamera.orthographic = true;
                uiCamera.clearFlags = CameraClearFlags.SolidColor;
                uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                uiCamera.cullingMask = 0;
                uiCamera.targetTexture = ui;
                uiCamera.depth = camera.depth + 1f;

                SelectLocale(shell, shot.Locale);
                shell.Apply(shot.Width, shot.Height, shot.Safe);
                if (!shell.SelectRegion(shot.Region))
                    throw new InvalidOperationException("Region " + shot.Region + " was rejected.");
                if (SessionState.GetBool(DeviceIos04cR1Key, false) || SessionState.GetBool(DeviceIos04cKey, false))
                    PoseIos04c(shell, shot);
                else if (SessionState.GetBool(Ios04aR1Key, false))
                    PoseIos04aR1(shell, shot);
                else if (SessionState.GetBool(Ios04aKey, false))
                    PoseIos04a(shell, shot);
                else if (SessionState.GetBool(Ui06aR1Key, false))
                    PoseUi06aR1(shell, shot);
                else if (SessionState.GetBool(Ui06aKey, false))
                    PoseUi06a(shell, shot);
                else if (SessionState.GetBool(Ui04eKey, false))
                    PoseUi04e(shell, shot);
                else if (SessionState.GetBool(Ui04dKey, false))
                    PoseUi04d(shell, shot);
                else if (SessionState.GetBool(Ui04cKey, false))
                    PoseUi04c(shell, shot);
                else if (SessionState.GetBool(Ui04bKey, false))
                    PoseUi04b(shell, shot);
                else if (SessionState.GetBool(Ui03bKey, false))
                    PoseUi03b(shell, shot);
                else if (SessionState.GetBool(Ui04aKey, false) || SessionState.GetBool(Ui04aR1Key, false))
                    PoseUi04a(shell, shot);
                else if (shot.Context)
                    shell.ShowModule(0);
                Pose(camera, shot);
                var presenter = camera.GetComponent<MapRegionalReliefPresenter>();
                ApplyDiagnostic(presenter, shot, notes);
                if (shot.Menu)
                {
                    if (!shell.RegionMenuOpen)
                        shell.ToggleRegionMenu();
                    SettleOpenMenu(shell);
                    Canvas.ForceUpdateCanvases();
                    if (shot.Scroll)
                        ScrollRegionMenu(shell, notes, shot.File, shot.ScrollPosition);
                    else
                        MeasureRegionMenu(shell, notes, shot.File);
                }

                var uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer < 0)
                    uiLayer = 5;
                var savedLayers = new System.Collections.Generic.List<int>();
                var layered = new System.Collections.Generic.List<GameObject>();
                SetLayer(canvas.transform, uiLayer, layered, savedLayers);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 8f;
                camera.targetTexture = map;
                camera.aspect = shot.Width / (float)shot.Height;
                camera.ResetProjectionMatrix();
                Canvas.ForceUpdateCanvases();
                var countryCount = labels.Refresh(shot.Width, shot.Height, shot.Safe, shot.Locale, shot.Menu);
                var waterCount = water != null ? water.Refresh(shot.Width, shot.Height, shot.Safe, shot.Locale, shot.Menu) : 0;
                markers.GetType().GetMethod("ApplyViewport", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(markers, new object[] { shot.Width, shot.Height, true });
                Canvas.ForceUpdateCanvases();
                var chip = ChipText(shell);
                var selection = MapCountryLabels.LastSelection;
                notes.AppendLine(
                    shot.File
                    + " labels country=" + countryCount
                    + " water=" + waterCount
                    + " in=" + selection.InRegion
                    + " out=" + selection.OutOfRegion
                    + " candidates=" + selection.Candidates
                    + " rejected=" + selection.RejectedOverlap
                    + " shifted=" + selection.Shifted
                    + " cities=" + ActiveCityLabels(canvas));

                var savedMask = camera.cullingMask;
                var savedClear = camera.clearFlags;
                var savedBackground = camera.backgroundColor;
                camera.cullingMask = savedMask & ~(1 << uiLayer);
                camera.Render();

                camera.targetTexture = ui;
                camera.ResetProjectionMatrix();
                camera.cullingMask = 1 << uiLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.Render();
                camera.cullingMask = savedMask;
                camera.clearFlags = savedClear;
                camera.backgroundColor = savedBackground;
                for (var i = 0; i < layered.Count; i++)
                    layered[i].layer = savedLayers[i];

                mapTex = Read(map);
                uiTex = Read(ui);
                var mapPixels = mapTex.GetPixels32();
                var uiPixels = uiTex.GetPixels32();
                var valid = HudSitsAboveMap(mapPixels, uiPixels, shot.Width, shot.Height, shell, shot, out var detail);
                output = Composite(mapPixels, uiPixels, shot.Width, shot.Height);
                if (SessionState.GetBool(Ui06aR1Key, false))
                    AnnotateUi06aR1(output, uiPixels, mapPixels, shell, shot, detail);
                var expected = ExpectedChip(shot.Region, shot.Locale);
                if (valid && chip == expected)
                {
                    WriteShotPng(shot.File, output.EncodeToPNG());
                    if (SessionState.GetBool(Ui04aR1Key, false) && shot.File.IndexOf("expand-icon") >= 0)
                        WriteExpandIconCrop(shot, output, shell);
                    notes.AppendLine(shot.File + ": PASS chip=" + chip + " " + detail);
                    return true;
                }

                WriteRejectedPng(shot.File, output.EncodeToPNG());
                notes.AppendLine(shot.File + ": REJECTED chip=" + chip + " expected=" + expected + " " + detail);
                return false;
            }
            finally
            {
                var presenter = camera != null ? camera.GetComponent<MapRegionalReliefPresenter>() : null;
                RestoreDiagnostic(presenter);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                camera.targetTexture = null;
                if (mapTex != null)
                    UnityEngine.Object.DestroyImmediate(mapTex);
                if (uiTex != null)
                    UnityEngine.Object.DestroyImmediate(uiTex);
                if (output != null)
                    UnityEngine.Object.DestroyImmediate(output);
                map.Release();
                UnityEngine.Object.DestroyImmediate(map);
                ui.Release();
                UnityEngine.Object.DestroyImmediate(ui);
                UnityEngine.Object.DestroyImmediate(uiObject);
            }
        }

        static void FreezeProductSimulation(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var behaviours = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
                for (var b = 0; b < behaviours.Length; b++)
                {
                    var behaviour = behaviours[b];
                    if (behaviour == null)
                        continue;
                    var name = behaviour.GetType().Name;
                    if (name == "MapCameraRig" || name == "MapMarkerLayer" || name == "MapCountryLabelLayer"
                        || name == "MapWaterLabelLayer"
                        || name == "SafeAreaRect" || name == "MapGestures" || name == "MapOrientation"
                        || name == "MapCommandShell")
                        behaviour.enabled = false;
                }
            }
        }

        static void SettleOpenMenu(MapCommandShell shell)
        {
            var menu = shell.transform.Find("RegionMenu");
            if (menu == null)
                return;

            var group = menu.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            menu.localScale = Vector3.one;
            var chevron = shell.transform.Find("Header/RegionChip/Chevron");
            if (chevron == null)
            {
                var transforms = shell.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == "Chevron")
                    {
                        chevron = transforms[i];
                        break;
                    }
                }
            }

            if (chevron != null)
                chevron.localEulerAngles = new Vector3(0f, 0f, 180f);
        }

        static void SetLayer(Transform root, int layer, System.Collections.Generic.List<GameObject> objects, System.Collections.Generic.List<int> layers)
        {
            objects.Add(root.gameObject);
            layers.Add(root.gameObject.layer);
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++)
                SetLayer(root.GetChild(i), layer, objects, layers);
        }

        static void Pose(Camera camera, CaptureShot shot)
        {
            var scope = (MapScope)shot.Region;
            var width = shot.Width;
            var height = shot.Height;
            var safe = shot.Safe;
            var closest = shot.Closest;
            var aspect = width / (float)height;
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            Vector2 center;
            float size;
            if (scope == MapScope.World || scope == MapScope.Turkey)
            {
                var boundsMin = scope == MapScope.World ? worldMin : turkeyMin;
                var boundsMax = scope == MapScope.World ? worldMax : turkeyMax;
                var padding = scope == MapScope.World ? MapFrames.WorldFocusPadding : MapFrames.TurkeyFocusPadding;
                MapViewMath.Fit(boundsMin, boundsMax, aspect, padding, out center, out size);
            }
            else
            {
                MapCameraRig.FrameScope(scope, aspect, width, height, safe, worldMin, worldMax, out center, out size);
            }

            size = MapViewMath.ClampSize(size, MapFrames.MinimumOrthographicSize(aspect), MapFrames.MaximumOrthographicSize(aspect));
            MapCameraRig.PresentationTravel(scope, size, width, height, safe, worldMin, worldMax, out var travelMin, out var travelMax);
            center = MapViewMath.ClampCenter(center, size, aspect, travelMin, travelMax);
            if (scope == MapScope.Turkey && !closest && shot.FocusSizeScale <= 0f)
                MapCameraRig.FitCitiesInside(center, size, aspect, width, height, safe, worldMin, worldMax, out center, out size);
            if (closest)
            {
                size = MapFrames.MinimumOrthographicSize(aspect);
                center = MapViewMath.ClampCenter(center, size, aspect, worldMin, worldMax);
            }

            if (shot.FocusSizeScale > 0f && MapProjection.TryProjectUnwrapped(shot.FocusLatitude, shot.FocusLongitude, out var focus))
            {
                MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkeySize);
                size = MapViewMath.ClampSize(turkeySize * shot.FocusSizeScale, MapFrames.MinimumOrthographicSize(aspect), MapFrames.MaximumOrthographicSize(aspect));
                MapCameraRig.PresentationTravel(scope, size, width, height, safe, worldMin, worldMax, out travelMin, out travelMax);
                center = MapViewMath.ClampCenter(focus, size, aspect, travelMin, travelMax);
            }

            camera.orthographic = true;
            camera.aspect = aspect;
            camera.orthographicSize = size;
            camera.transform.position = new Vector3(center.x, center.y, MapCameraRig.CameraZ);
            camera.ResetProjectionMatrix();
        }

        static void Match(Camera uiCamera, Camera camera, RenderTexture target)
        {
            uiCamera.orthographic = true;
            uiCamera.aspect = camera.aspect;
            uiCamera.orthographicSize = camera.orthographicSize;
            uiCamera.transform.position = camera.transform.position;
            uiCamera.transform.rotation = camera.transform.rotation;
            uiCamera.targetTexture = target;
            uiCamera.ResetProjectionMatrix();
        }

        static void ScrollRegionMenu(MapCommandShell shell, StringBuilder notes, string file, float position)
        {
            var scroll = RegionScroll(shell);
            if (scroll == null)
            {
                notes.AppendLine(file + " scroll: menu scroll rect was not found.");
                return;
            }

            if (!scroll.vertical)
            {
                notes.AppendLine(file + " scroll requested, but this menu has no overflow.");
                MeasureRegionMenu(shell, notes, file);
                return;
            }

            scroll.verticalNormalizedPosition = position;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = position;
            Canvas.ForceUpdateCanvases();
            MeasureRegionMenu(shell, notes, file);
        }

        static void MeasureRegionMenu(MapCommandShell shell, StringBuilder notes, string file)
        {
            var scroll = RegionScroll(shell);
            if (scroll == null || scroll.content == null || scroll.viewport == null)
                return;

            var late = typeof(ScrollRect).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            if (late != null)
                late.Invoke(scroll, null);
            var last = Named(shell.transform, "Region7");
            var name = last != null ? last.Find("Name")?.GetComponent<Text>() : null;
            var content = scroll.content.rect.height;
            var viewport = scroll.viewport.rect.height;
            var inside = last != null && RowInside(scroll.viewport, last);
            var bar = scroll.verticalScrollbar;
            notes.AppendLine(
                file
                + " menu content=" + content.ToString("0.0", CultureInfo.InvariantCulture)
                + " viewport=" + viewport.ToString("0.0", CultureInfo.InvariantCulture)
                + " overflow=" + (content - viewport).ToString("0.0", CultureInfo.InvariantCulture)
                + " normalized=" + scroll.verticalNormalizedPosition.ToString("0.00", CultureInfo.InvariantCulture)
                + " scrolling=" + scroll.vertical.ToString()
                + " bar=" + (bar != null && bar.gameObject.activeSelf ? bar.value.ToString("0.00", CultureInfo.InvariantCulture) : "off")
                + " lastInside=" + inside.ToString()
                + " label=" + (name != null ? name.text : string.Empty)
                + " text=" + (name != null ? name.preferredWidth.ToString("0.0", CultureInfo.InvariantCulture) : "0")
                + "/" + (name != null ? name.rectTransform.rect.width.ToString("0.0", CultureInfo.InvariantCulture) : "0"));
        }

        static RectTransform Named(Transform root, string name)
        {
            if (root == null)
                return null;

            var items = root.GetComponentsInChildren<RectTransform>(true);
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].name == name)
                    return items[i];
            }

            return null;
        }

        static ScrollRect RegionScroll(MapCommandShell shell)
        {
            var menu = shell != null ? shell.transform.Find("RegionMenu") : null;
            return menu != null ? menu.GetComponent<ScrollRect>() : null;
        }

        static bool RowInside(RectTransform viewport, RectTransform row)
        {
            var view = new Vector3[4];
            var item = new Vector3[4];
            viewport.GetWorldCorners(view);
            row.GetWorldCorners(item);
            for (var i = 0; i < item.Length; i++)
            {
                if (item[i].x < view[0].x - 1f || item[i].x > view[2].x + 1f || item[i].y < view[0].y - 1f || item[i].y > view[2].y + 1f)
                    return false;
            }

            return true;
        }

        static int ActiveCityLabels(Canvas canvas)
        {
            var labels = canvas.GetComponentsInChildren<Text>(true);
            var count = 0;
            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null || !label.gameObject.activeInHierarchy || string.IsNullOrEmpty(label.text))
                    continue;
                var parent = label.transform.parent;
                if (parent != null && parent.name.StartsWith("Plate_", System.StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        static string ChipText(MapCommandShell shell)
        {
            var transforms = shell.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name != "RegionChip")
                    continue;
                var name = transforms[i].Find("Name");
                var text = name != null ? name.GetComponent<Text>() : null;
                return text != null ? text.text : string.Empty;
            }

            return string.Empty;
        }

        static bool HudSitsAboveMap(Color32[] map, Color32[] ui, int width, int height, MapCommandShell shell, CaptureShot shot, out string detail)
        {
            var hx = width / 2;
            var hy = height / 2;
            var header = ui[(height - 6) * width + hx];
            var hole = ui[hy * width + hx];
            var mapHole = map[hy * width + hx];
            var headerDrawn = header.a > 180;
            var holeCovered = MapHudCoverage.OpaqueUiCover(hole.a, hole.r, mapHole.r);
            var nhood = "";
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var x = hx + dx;
                    var y = hy + dy;
                    if (x < 0 || y < 0 || x >= width || y >= height)
                        continue;
                    nhood += ui[y * width + x].a + (dx == 1 && dy == 1 ? "" : ",");
                }
            }

            var metrics = MapCommandLayout.Evaluate(shot.Width, shot.Height, shot.Safe, shell != null && shell.ContextOpen);
            var headerRect = shell != null ? shell.HeaderRect : metrics.Header;
            var footerRect = shell != null ? shell.SummaryRect : metrics.Summary;
            var railRect = shell != null ? shell.RailRect : metrics.Rail;
            var viewport = shell != null ? shell.Viewport : metrics.Viewport;
            var font = metrics.FontPixels;
            var touch = MapCommandLayout.TouchPixels(shot.Width, shot.Height);
            var panelOpen = shell != null && shell.ManagementOpen;
            var expanded = shell != null && shell.ManagementExpanded;
            MapHudCoverage.OverlayRects(viewport, font, touch, panelOpen, expanded, out var panel, out var blocker);
            var holePixel = MapHudCoverage.HolePixel(width, height);
            var kind = MapHudCoverage.Classify(headerRect, footerRect, railRect, panel, blocker, panelOpen, expanded, holePixel);
            var allowed = MapHudCoverage.HoleCoverageAllowed(kind, hole.a, hole.r, mapHole.r);
            detail = "headerA=" + header.a
                + " holeA=" + hole.a
                + " holeXY=" + hx + "," + hy
                + " holeKind=" + kind
                + " mapHole=" + mapHole.r + "," + mapHole.g + "," + mapHole.b
                + " nhoodA=" + nhood
                + " panel=" + RectText(panel)
                + " blocker=" + RectText(blocker)
                + " viewport=" + RectText(viewport)
                + " header=" + RectText(headerRect)
                + " footer=" + RectText(footerRect)
                + " rail=" + RectText(railRect);
            return headerDrawn && allowed && !(holeCovered && kind == MapHudSampleKind.Map);
        }

        static Texture2D Composite(Color32[] map, Color32[] ui, int width, int height)
        {
            var pixels = new Color32[map.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var cover = ui[i].a / 255f;
                pixels[i] = new Color32(
                    (byte)Mathf.RoundToInt(ui[i].r * cover + map[i].r * (1f - cover)),
                    (byte)Mathf.RoundToInt(ui[i].g * cover + map[i].g * (1f - cover)),
                    (byte)Mathf.RoundToInt(ui[i].b * cover + map[i].b * (1f - cover)),
                    255);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static Texture2D Read(RenderTexture target)
        {
            var active = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            texture.Apply(false, false);
            RenderTexture.active = active;
            return texture;
        }

        static void SelectLocale(MapCommandShell shell, string locale)
        {
            var code = string.IsNullOrEmpty(locale) ? "tr" : locale;
            if (LocaleService.CurrentCode == code)
                return;

            var wasEnabled = shell.enabled;
            if (!wasEnabled)
                shell.enabled = true;
            LocaleService.TrySelect(code);
            Canvas.ForceUpdateCanvases();
            if (!wasEnabled)
                shell.enabled = false;
        }

        static string ExpectedChip(int region, string locale)
        {
            if (locale == "en")
            {
                return region switch
                {
                    0 => "World",
                    1 => "Turkey",
                    2 => "Europe",
                    3 => "Asia",
                    4 => "Africa",
                    5 => "North America",
                    6 => "South America",
                    _ => "Oceania"
                };
            }

            if (locale == "de")
            {
                return region switch
                {
                    0 => "Welt",
                    1 => "Türkei",
                    2 => "Europa",
                    3 => "Asien",
                    4 => "Afrika",
                    5 => "Nordamerika",
                    6 => "Südamerika",
                    _ => "Ozeanien"
                };
            }

            return RegionNames[region];
        }

        static CaptureShot[] BaselineShots()
        {
            return new[]
            {
                Shot("world-1920", 0, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("turkey-1920", 1, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("europe-1920", 2, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("asia-1920", 3, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("africa-1920", 4, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("north-america-1920", 5, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("south-america-1920", 6, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("oceania-1920", 7, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("world-2868", 0, 2868, 1320, Phone(), false, false, false),
                Shot("turkey-2868", 1, 2868, 1320, Phone(), false, false, false),
                Shot("europe-2868", 2, 2868, 1320, Phone(), false, false, false),
                Shot("turkey-closest-2868", 1, 2868, 1320, Phone(), true, false, false),
                Shot("dropdown-open-1920", 0, 1920, 1080, Full(1920, 1080), false, true, false),
                Shot("dropdown-open-2868", 0, 2868, 1320, Phone(), false, true, false),
                Shot("dropdown-scrolled-932", 0, 932, 430, Full(932, 430), false, true, true)
            };
        }

        static CaptureShot[] DensityShots()
        {
            return new[]
            {
                Shot("world-1920", 0, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("europe-1920", 2, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("europe-2868", 2, 2868, 1320, Phone(), false, false, false),
                Shot("africa-1920", 4, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("africa-2868", 4, 2868, 1320, Phone(), false, false, false),
                Shot("south-america-1920", 6, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("asia-1920", 3, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("turkey-2868", 1, 2868, 1320, Phone(), false, false, false),
                Shot("north-america-1920", 5, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("oceania-1920", 7, 1920, 1080, Full(1920, 1080), false, false, false),
                Shot("europe-de-1920", 2, 1920, 1080, Full(1920, 1080), false, false, false, "de"),
                Shot("africa-en-1920", 4, 1920, 1080, Full(1920, 1080), false, false, false, "en")
            };
        }

        static CaptureShot[] CityShots()
        {
            return new[]
            {
                Shot("turkey-fit-1920", 1, 1920, 1080, Full(1920, 1080), false, false, false, "tr"),
                Shot("turkey-fit-2868", 1, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("turkey-fit-932", 1, 932, 430, Full(932, 430), false, false, false, "tr"),
                Shot("turkey-fit-844", 1, 844, 390, Full(844, 390), false, false, false, "tr"),
                Shot("turkey-closest-2868", 1, 2868, 1320, Phone(), true, false, false, "tr"),
                Shot("turkey-closest-1920", 1, 1920, 1080, Full(1920, 1080), true, false, false, "tr"),
                Shot("turkey-fit-en-1920", 1, 1920, 1080, Full(1920, 1080), false, false, false, "en"),
                Shot("turkey-fit-de-1920", 1, 1920, 1080, Full(1920, 1080), false, false, false, "de")
            };
        }

        static CaptureShot[] PlacementShots()
        {
            return new[]
            {
                Shot("europe-tr-1920", 2, 1920, 1080, Full(1920, 1080), false, false, false, "tr"),
                Shot("europe-tr-2868", 2, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("europe-de-1920", 2, 1920, 1080, Full(1920, 1080), false, false, false, "de"),
                Shot("europe-de-2868", 2, 2868, 1320, Phone(), false, false, false, "de"),
                Shot("europe-en-1920", 2, 1920, 1080, Full(1920, 1080), false, false, false, "en"),
                Shot("africa-tr-1920", 4, 1920, 1080, Full(1920, 1080), false, false, false, "tr"),
                Shot("africa-tr-2868", 4, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("south-america-tr-1920", 6, 1920, 1080, Full(1920, 1080), false, false, false, "tr"),
                Shot("asia-tr-1920", 3, 1920, 1080, Full(1920, 1080), false, false, false, "tr"),
                Shot("world-tr-1920", 0, 1920, 1080, Full(1920, 1080), false, false, false, "tr"),
                Shot("turkey-tr-2868", 1, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("north-america-tr-1920", 5, 1920, 1080, Full(1920, 1080), false, false, false, "tr")
            };
        }

        static CaptureShot Shot(string file, int region, int width, int height, Rect safe, bool closest, bool menu, bool scroll)
        {
            return Shot(file, region, width, height, safe, closest, menu, scroll, "tr");
        }

        static CaptureShot[] HitShots()
        {
            return new[]
            {
                Shot("dropdown-932", 0, 932, 430, Full(932, 430), false, true, false, "tr"),
                Shot("dropdown-960", 0, 960, 540, Full(960, 540), false, true, false, "tr"),
                Shot("dropdown-1280", 0, 1280, 720, Full(1280, 720), false, true, false, "tr"),
                Shot("dropdown-1920", 0, 1920, 1080, Full(1920, 1080), false, true, false, "tr"),
                Shot("dropdown-top-2868", 0, 2868, 1320, Phone(), false, true, false, "tr"),
                Shot("dropdown-bottom-2868", 0, 2868, 1320, Phone(), false, true, true, "tr", 0f),
                Shot("dropdown-bottom-844", 0, 844, 390, Full(844, 390), false, true, true, "tr", 0f),
                Shot("dropdown-bottom-932-safe", 0, 932, 430, new Rect(32f, 16f, 868f, 398f), false, true, true, "tr", 0f)
            };
        }

        static CaptureShot[] DropdownShots()
        {
            return new[]
            {
                Shot("dropdown-closed-2868", 0, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("dropdown-open-top-2868", 0, 2868, 1320, Phone(), false, true, false, "tr"),
                Shot("dropdown-open-middle-2868", 0, 2868, 1320, Phone(), false, true, true, "tr", 0.5f),
                Shot("dropdown-open-bottom-2868", 0, 2868, 1320, Phone(), false, true, true, "tr", 0f),
                Shot("dropdown-oceania-selected-2868", 7, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("dropdown-open-top-932", 0, 932, 430, Full(932, 430), false, true, false, "tr"),
                Shot("dropdown-open-bottom-932", 0, 932, 430, Full(932, 430), false, true, true, "tr", 0f),
                Shot("dropdown-open-top-844", 0, 844, 390, Full(844, 390), false, true, false, "tr"),
                Shot("dropdown-open-bottom-844", 0, 844, 390, Full(844, 390), false, true, true, "tr", 0f),
                Shot("dropdown-open-1920", 0, 1920, 1080, Full(1920, 1080), false, true, false, "tr"),
                Shot("dropdown-open-de-1920", 0, 1920, 1080, Full(1920, 1080), false, true, false, "de")
            };
        }

        static CaptureShot[] Ui01Shots()
        {
            return new[]
            {
                Shot("world-2868", 0, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("turkey-2868", 1, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("europe-2868", 2, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("oceania-2868", 7, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("turkey-1920", 1, 1920, 1080, Full(1920, 1080), false, false, false, "tr"),
                Shot("world-1920", 0, 1920, 1080, Full(1920, 1080), false, false, false, "tr"),
                Shot("turkey-932", 1, 932, 430, new Rect(32f, 16f, 868f, 398f), false, false, false, "tr"),
                Shot("world-844", 0, 844, 390, Full(844, 390), false, false, false, "tr"),
                Shot("context-filo-2868", 1, 2868, 1320, Phone(), false, false, false, "tr", 0f, true),
                Shot("dropdown-open-2868", 0, 2868, 1320, Phone(), false, true, false, "tr"),
                Shot("north-america-tr-2868", 5, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("north-america-en-2868", 5, 2868, 1320, Phone(), false, false, false, "en"),
                Shot("north-america-de-2868", 5, 2868, 1320, Phone(), false, false, false, "de"),
                Shot("world-844-safe", 0, 844, 390, Full(844, 390), false, false, false, "tr")
            };
        }

        static CaptureShot[] R1DiagnosticShots()
        {
            return new[]
            {
                DiagShot("before/turkey-fit-2868", false, 0f, 0f, 0f, DiagNone),
                DiagShot("before/turkey-close-2868", true, 0f, 0f, 0f, DiagNone),
                DiagShot("before/eastern-anatolia-2868", false, 37.920436f, 40.230040f, 0.55f, DiagNone),
                DiagShot("diagnostics/full-render", false, 0f, 0f, 0f, DiagNone),
                DiagShot("diagnostics/global-only", false, 0f, 0f, 0f, DiagGlobalOnly),
                DiagShot("diagnostics/copernicus-only", false, 0f, 0f, 0f, DiagCopernicusOnly),
                DiagShot("diagnostics/old-regional-fallback", false, 0f, 0f, 0f, DiagOldRegional),
                DiagShot("diagnostics/alpha-coverage", false, 0f, 0f, 0f, DiagAlphaCoverage)
            };
        }

        static CaptureShot[] R1AfterShots()
        {
            return new[]
            {
                DiagShot("after/turkey-fit-2868", false, 0f, 0f, 0f, DiagNone),
                DiagShot("after/turkey-close-2868", true, 0f, 0f, 0f, DiagNone),
                DiagShot("after/eastern-anatolia-2868", false, 37.920436f, 40.230040f, 0.55f, DiagNone),
                DiagShot("after/black-sea-2868", false, 40.979991f, 39.719994f, 0.55f, DiagNone),
                DiagShot("after/taurus-2868", false, 37.1f, 32.5f, 0.55f, DiagNone),
                Shot("after/world-2868", 0, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("after/asia-2868", 3, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("after/europe-2868", 2, 2868, 1320, Phone(), false, false, false, "tr")
            };
        }

        static CaptureShot[] CopernicusShots()
        {
            return new[]
            {
                Shot("turkey-fit-2868", 1, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("turkey-close-2868", 1, 2868, 1320, Phone(), true, false, false, "tr"),
                FocusShot("eastern-anatolia-2868", 1, 2868, 1320, Phone(), 37.920436f, 40.230040f, 0.55f),
                FocusShot("black-sea-2868", 1, 2868, 1320, Phone(), 40.979991f, 39.719994f, 0.55f),
                FocusShot("taurus-2868", 1, 2868, 1320, Phone(), 37.1f, 32.5f, 0.55f),
                FocusShot("western-anatolia-2868", 1, 2868, 1320, Phone(), 38.438096f, 27.149848f, 0.55f),
                Shot("world-2868", 0, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("asia-2868", 3, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("europe-2868", 2, 2868, 1320, Phone(), false, false, false, "tr")
            };
        }

        static CaptureShot[] WaterShots()
        {
            return new[]
            {
                Shot("turkey-fit-tr-2868", 1, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("turkey-fit-en-2868", 1, 2868, 1320, Phone(), false, false, false, "en"),
                Shot("turkey-fit-de-2868", 1, 2868, 1320, Phone(), false, false, false, "de"),
                FocusShot("turkey-close-marmara-2868", 1, 2868, 1320, Phone(), 40.70f, 27.65f, 0.55f),
                Shot("world-2868", 0, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("europe-2868", 2, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("asia-2868", 3, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("africa-2868", 4, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("north-america-2868", 5, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("south-america-2868", 6, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("oceania-2868", 7, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("turkey-fit-1920", 1, 1920, 1080, Full(1920, 1080), false, false, false, "tr"),
                Shot("world-1920", 0, 1920, 1080, Full(1920, 1080), false, false, false, "tr"),
                Shot("turkey-fit-932", 1, 932, 430, new Rect(32f, 16f, 868f, 398f), false, false, false, "tr"),
                Shot("europe-932", 2, 932, 430, new Rect(32f, 16f, 868f, 398f), false, false, false, "tr")
            };
        }

        static CaptureShot[] Ui04aR1Shots()
        {
            var phone = Phone();
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("fleet-compact-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("expand-icon-compact-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-expanded-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("expand-icon-expanded-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-compact-2868-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("fleet-compact-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("fleet-expanded-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("fleet-compact-932-de", 1, 932, 430, compact, false, false, false, "de"),
                Shot("jobs-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("map-after-close-2868", 1, 2868, 1320, phone, false, false, false, "tr")
            };
        }

        static void WriteExpandIconCrop(CaptureShot shot, Texture2D full, MapCommandShell shell)
        {
            if (full == null || shell == null || shell.Management == null || shell.Management.ExpandButton == null)
                return;

            var hit = shell.Management.ExpandHitPixels();
            var pad = Mathf.RoundToInt(Mathf.Max(hit.width, hit.height));
            var metrics = MapCommandLayout.Evaluate(shot.Width, shot.Height, shot.Safe, false);
            var touch = MapCommandLayout.TouchPixels(shot.Width, shot.Height);
            var frame = shot.File.IndexOf("expanded") >= 0
                ? MapManagementLayout.Expanded(metrics.Viewport, metrics.FontPixels)
                : MapManagementLayout.Compact(metrics.Viewport, metrics.FontPixels, touch);
            var scale = MapHudLayout.CanvasScale(shot.Width, shot.Height);
            var gap = Mathf.Max(8f, metrics.Gap);
            var xMax = Mathf.RoundToInt(frame.xMax - gap - touch * 0.5f);
            var yMax = Mathf.RoundToInt(frame.yMax - gap - touch * 0.5f);
            var size = pad * 3;
            var x = Mathf.Clamp(xMax - size / 2, 0, shot.Width - size);
            var y = Mathf.Clamp(shot.Height - yMax - size / 2, 0, shot.Height - size);
            if (size < 32 || x + size > full.width || y + size > full.height)
                return;

            var crop = new Texture2D(size, size, TextureFormat.RGBA32, false);
            crop.SetPixels(full.GetPixels(x, y, size, size));
            crop.Apply(false, false);
            WriteShotPng(shot.File + "-crop", crop.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(crop);
        }

        static CaptureShot[] Ui04aShots()
        {
            var phone = Phone();
            var full = Full(1920, 1080);
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("map-only-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-compact-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-expanded-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("warehouses-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("map-after-close-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-compact-2868-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("jobs-2868-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("fleet-1920-tr", 1, 1920, 1080, full, false, false, false, "tr"),
                Shot("jobs-1920-tr", 1, 1920, 1080, full, false, false, false, "tr"),
                Shot("compact-access-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("fleet-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("fleet-expanded-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("fleet-932-de", 1, 932, 430, compact, false, false, false, "de")
            };
        }

        static CaptureShot[] Ui04bShots()
        {
            var phone = Phone();
            var full1920 = Full(1920, 1080);
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("map-only-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-unconfigured-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-compact-fixture-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-expanded-fixture-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-selected-vehicle-fixture-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-empty-fixture-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-error-fixture-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-expanded-fixture-2868-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("jobs-compact-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("map-after-fleet-close-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-compact-fixture-1920-tr", 1, 1920, 1080, full1920, false, false, false, "tr"),
                Shot("fleet-expanded-fixture-1920-tr", 1, 1920, 1080, full1920, false, false, false, "tr"),
                Shot("fleet-unconfigured-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("fleet-compact-fixture-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("fleet-expanded-fixture-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("fleet-detail-fixture-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("fleet-expanded-fixture-932-de", 1, 932, 430, compact, false, false, false, "de")
            };
        }

        static void PoseUi04b(MapCommandShell shell, CaptureShot shot)
        {
            MapFleetStore.ClearOverride();
            var file = shot.File ?? string.Empty;
            if (file.Contains("fleet-empty-fixture"))
                MapFleetStore.Override(MapFleetFixtures.Empty());
            else if (file.Contains("fleet-error-fixture"))
                MapFleetStore.Override(MapFleetFixtures.Error());
            else if (file.Contains("fleet-selected-vehicle-fixture"))
                MapFleetStore.Override(MapFleetFixtures.ReadySelected());
            else if (file.Contains("fleet-detail-fixture") || file.Contains("fleet-compact-fixture") || file.Contains("fleet-expanded-fixture"))
                MapFleetStore.Override(MapFleetFixtures.ReadyThree());

            if (file.Contains("map-only") || file.Contains("after-fleet-close"))
            {
                shell.CloseContext();
                return;
            }

            if (file.Contains("jobs"))
            {
                shell.OpenManagement(MapModule.Jobs);
                return;
            }

            shell.OpenManagement(MapModule.Fleet);
            shell.SetManagementExpanded(file.Contains("expanded") || file.Contains("selected-vehicle") || file.Contains("detail"));
            if (file.Contains("detail") && shell.Management.Fleet != null)
            {
                shell.Management.Fleet.Select("starter_truck");
                shell.Management.Fleet.Refresh();
            }
        }

        static CaptureShot[] Ui04cShots()
        {
            var phone = Phone();
            var full1920 = Full(1920, 1080);
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("jobs-unconfigured-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-compact-fixture-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-expanded-fixture-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-active-fixture-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-completed-fixture-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-selected-fixture-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-empty-fixture-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-error-fixture-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-expanded-fixture-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("map-after-jobs-close", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-compact-fixture", 1, 1920, 1080, full1920, false, false, false, "tr"),
                Shot("jobs-expanded-fixture", 1, 1920, 1080, full1920, false, false, false, "tr"),
                Shot("jobs-unconfigured", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("jobs-compact-fixture-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("jobs-expanded-fixture-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("jobs-detail-fixture", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("jobs-expanded-fixture-de-932", 1, 932, 430, compact, false, false, false, "de"),
                Shot("fleet-jobs-regression", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-fleet-regression", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("footer-jobs-fixture", 1, 2868, 1320, phone, false, false, false, "tr")
            };
        }

        static void PoseUi04c(MapCommandShell shell, CaptureShot shot)
        {
            MapJobsStore.ClearOverride();
            MapFleetStore.ClearOverride();
            MapOperationStrip.ClearOverride();
            var file = shot.File ?? string.Empty;
            if (file.Contains("jobs-empty-fixture"))
                MapJobsStore.Override(MapJobsFixtures.Empty());
            else if (file.Contains("jobs-error-fixture"))
                MapJobsStore.Override(MapJobsFixtures.Error());
            else if (file.Contains("jobs-active-fixture"))
                MapJobsStore.Override(MapJobsFixtures.ActiveSelected());
            else if (file.Contains("jobs-completed-fixture"))
                MapJobsStore.Override(MapJobsFixtures.HistorySelected());
            else if (file.Contains("jobs-selected-fixture")
                || file.Contains("jobs-compact-fixture")
                || file.Contains("jobs-expanded-fixture")
                || file.Contains("jobs-detail-fixture")
                || file.Contains("footer-jobs")
                || file.Contains("fleet-jobs")
                || file.Contains("jobs-fleet"))
                MapJobsStore.Override(MapJobsFixtures.Mixed());

            if (file.Contains("after-jobs-close"))
            {
                shell.CloseContext();
                return;
            }

            if (file.Contains("footer-jobs"))
            {
                MapOperationStrip.Override(MapOperationSnapshot.Fixture(MapOperationStatus.SingleActive, 1, "izmir", "istanbul"));
                shell.RefreshStrip();
                shell.OpenJobsFromStrip();
                shell.SetManagementExpanded(true);
                if (shell.Management.Jobs != null)
                    shell.Management.Jobs.Select("offer_izmir_istanbul");
                return;
            }

            if (file.Contains("fleet-jobs"))
            {
                MapFleetStore.Override(MapFleetFixtures.ReadyThree());
                shell.OpenManagement(MapModule.Fleet);
                shell.SetManagementExpanded(true);
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("jobs-fleet"))
            {
                MapFleetStore.Override(MapFleetFixtures.ReadyThree());
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                shell.OpenManagement(MapModule.Fleet);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("jobs-unconfigured"))
            {
                shell.OpenManagement(MapModule.Jobs);
                return;
            }

            shell.OpenManagement(MapModule.Jobs);
            var expand = file.Contains("expanded")
                || file.Contains("selected")
                || file.Contains("detail")
                || file.Contains("active")
                || file.Contains("completed")
                || file.Contains("empty")
                || file.Contains("error");
            shell.SetManagementExpanded(expand);
            if (shell.Management.Jobs == null)
                return;
            if (file.Contains("jobs-active"))
                shell.Management.Jobs.SetCategory(MapJobCategory.Active);
            if (file.Contains("jobs-completed"))
                shell.Management.Jobs.SetCategory(MapJobCategory.History);
            if (file.Contains("jobs-selected") || file.Contains("jobs-detail"))
                shell.Management.Jobs.Select("offer_izmir_istanbul");
            shell.Management.Jobs.Refresh();
        }

        static CaptureShot[] Ui04dShots()
        {
            var phone = Phone();
            var full1920 = Full(1920, 1080);
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("warehouses-unconfigured-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("warehouses-compact-fixture-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("warehouses-expanded-fixture-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("warehouses-selected-fixture-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("warehouses-empty-fixture-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("warehouses-error-fixture-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("warehouses-missing-data-fixture-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("warehouses-expanded-fixture-de-2868", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("map-after-warehouses-close-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("warehouses-compact-fixture-1920", 1, 1920, 1080, full1920, false, false, false, "tr"),
                Shot("warehouses-expanded-fixture-1920", 1, 1920, 1080, full1920, false, false, false, "tr"),
                Shot("warehouses-unconfigured-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("warehouses-compact-fixture-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("warehouses-expanded-fixture-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("warehouses-detail-fixture-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("warehouses-expanded-fixture-de-932", 1, 932, 430, compact, false, false, false, "de"),
                Shot("warehouse-fleet-regression-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("warehouse-jobs-regression-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("map-only-after-warehouses-2868", 1, 2868, 1320, phone, false, false, false, "tr")
            };
        }

        static void PoseUi04d(MapCommandShell shell, CaptureShot shot)
        {
            MapWarehouseStore.ClearOverride();
            MapFleetStore.ClearOverride();
            MapJobsStore.ClearOverride();
            MapOperationStrip.ClearOverride();
            var file = shot.File ?? string.Empty;
            if (file.Contains("warehouses-empty-fixture"))
                MapWarehouseStore.Override(MapWarehouseFixtures.Empty());
            else if (file.Contains("warehouses-error-fixture"))
                MapWarehouseStore.Override(MapWarehouseFixtures.Error());
            else if (file.Contains("warehouses-missing-data"))
                MapWarehouseStore.Override(MapWarehouseFixtures.MissingData());
            else if (file.Contains("warehouses-compact-fixture")
                || file.Contains("warehouses-expanded-fixture")
                || file.Contains("warehouses-selected-fixture")
                || file.Contains("warehouses-detail-fixture")
                || file.Contains("warehouse-fleet")
                || file.Contains("warehouse-jobs"))
                MapWarehouseStore.Override(MapWarehouseFixtures.Mixed());

            if (file.Contains("after-warehouses") || file.Contains("map-only"))
            {
                shell.CloseContext();
                return;
            }

            if (file.Contains("warehouse-fleet"))
            {
                MapFleetStore.Override(MapFleetFixtures.ReadyThree());
                shell.OpenManagement(MapModule.Warehouses);
                shell.SetManagementExpanded(true);
                shell.OpenManagement(MapModule.Fleet);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("warehouse-jobs"))
            {
                MapJobsStore.Override(MapJobsFixtures.Mixed());
                shell.OpenManagement(MapModule.Warehouses);
                shell.SetManagementExpanded(true);
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("warehouses-unconfigured"))
            {
                shell.OpenManagement(MapModule.Warehouses);
                return;
            }

            shell.OpenManagement(MapModule.Warehouses);
            var expand = file.Contains("expanded")
                || file.Contains("selected")
                || file.Contains("detail")
                || file.Contains("empty")
                || file.Contains("error")
                || file.Contains("missing-data");
            shell.SetManagementExpanded(expand);
            if (shell.Management.Warehouses == null)
                return;
            if (file.Contains("warehouses-selected") || file.Contains("warehouses-detail"))
                shell.Management.Warehouses.Select("wh_izmir");
            if (file.Contains("warehouses-missing-data"))
                shell.Management.Warehouses.Select("wh_unknown_city");
            shell.Management.Warehouses.Refresh();
        }

        static CaptureShot[] Ui04eShots()
        {
            var phone = Phone();
            var full1920 = Full(1920, 1080);
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("market-unconfigured-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-compact-fixture-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-expanded-fixture-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-selected-fixture-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-empty-fixture-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-error-fixture-tr-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-missing-data-fixture-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-expanded-fixture-de-2868", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("map-after-market-close-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-compact-fixture-1920", 1, 1920, 1080, full1920, false, false, false, "tr"),
                Shot("market-expanded-fixture-1920", 1, 1920, 1080, full1920, false, false, false, "tr"),
                Shot("market-unconfigured-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("market-compact-fixture-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("market-expanded-fixture-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("market-detail-fixture-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("market-expanded-fixture-de-932", 1, 932, 430, compact, false, false, false, "de"),
                Shot("market-fleet-to-market-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-jobs-to-market-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-warehouses-to-market-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-to-jobs-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("map-only-after-market-2868", 1, 2868, 1320, phone, false, false, false, "tr")
            };
        }

        static void PoseUi04e(MapCommandShell shell, CaptureShot shot)
        {
            MapMarketStore.ClearOverride();
            MapWarehouseStore.ClearOverride();
            MapFleetStore.ClearOverride();
            MapJobsStore.ClearOverride();
            MapOperationStrip.ClearOverride();
            var file = shot.File ?? string.Empty;
            if (file.Contains("market-empty-fixture"))
                MapMarketStore.Override(MapMarketFixtures.Empty());
            else if (file.Contains("market-error-fixture"))
                MapMarketStore.Override(MapMarketFixtures.Error());
            else if (file.Contains("market-missing-data"))
                MapMarketStore.Override(MapMarketFixtures.MissingData());
            else if (file.Contains("market-compact-fixture")
                || file.Contains("market-expanded-fixture")
                || file.Contains("market-selected-fixture")
                || file.Contains("market-detail-fixture")
                || file.Contains("market-fleet-to-market")
                || file.Contains("market-jobs-to-market")
                || file.Contains("market-warehouses-to-market")
                || file.Contains("market-to-jobs"))
                MapMarketStore.Override(MapMarketFixtures.Mixed());

            if (file.Contains("after-market") || file.Contains("map-only"))
            {
                shell.CloseContext();
                return;
            }

            if (file.Contains("market-fleet-to-market"))
            {
                MapFleetStore.Override(MapFleetFixtures.ReadyThree());
                shell.OpenManagement(MapModule.Fleet);
                shell.SetManagementExpanded(true);
                shell.OpenManagement(MapModule.Market);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("market-jobs-to-market"))
            {
                MapJobsStore.Override(MapJobsFixtures.Mixed());
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                shell.OpenManagement(MapModule.Market);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("market-warehouses-to-market"))
            {
                MapWarehouseStore.Override(MapWarehouseFixtures.Mixed());
                shell.OpenManagement(MapModule.Warehouses);
                shell.SetManagementExpanded(true);
                shell.OpenManagement(MapModule.Market);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("market-to-jobs"))
            {
                MapJobsStore.Override(MapJobsFixtures.Mixed());
                shell.OpenManagement(MapModule.Market);
                shell.SetManagementExpanded(true);
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("market-unconfigured"))
            {
                shell.OpenManagement(MapModule.Market);
                shell.SetManagementExpanded(false);
                if (shell.Management.Market != null)
                    shell.Management.Market.ShowList();
                return;
            }

            shell.OpenManagement(MapModule.Market);
            var expand = file.Contains("expanded")
                || file.Contains("selected")
                || file.Contains("detail")
                || file.Contains("empty")
                || file.Contains("error")
                || file.Contains("missing-data");
            shell.SetManagementExpanded(expand);
            if (shell.Management.Market == null)
                return;
            if (file.Contains("market-missing-data"))
                shell.Management.Market.Select("synthetic_city/ore");
            else if (file.Contains("market-selected") || file.Contains("market-detail"))
                shell.Management.Market.Select("izmir/steel");
            else
                shell.Management.Market.ShowList();
            shell.Management.Market.Refresh();
        }

        static CaptureShot[] Ui06aShots()
        {
            var phone = Phone();
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("fleet-compact-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-expanded-2868-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("fleet-compact-932-de", 1, 932, 430, compact, false, false, false, "de"),
                Shot("jobs-compact-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-expanded-2868-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("jobs-detail-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("warehouses-compact-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("warehouses-expanded-2868-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("warehouses-compact-932-de", 1, 932, 430, compact, false, false, false, "de"),
                Shot("market-compact-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("market-expanded-2868-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("market-detail-932-de", 1, 932, 430, compact, false, false, false, "de"),
                Shot("market-expanded-932-alpha", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("map-only-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("map-after-panels-close", 1, 2868, 1320, phone, false, false, false, "tr")
            };
        }

        static void PoseUi06a(MapCommandShell shell, CaptureShot shot)
        {
            MapMarketStore.ClearOverride();
            MapWarehouseStore.ClearOverride();
            MapFleetStore.ClearOverride();
            MapJobsStore.ClearOverride();
            MapOperationStrip.ClearOverride();
            var file = shot.File ?? string.Empty;
            if (file.StartsWith("map-"))
            {
                if (file.Contains("after-panels"))
                {
                    MapMarketStore.Override(MapMarketFixtures.Mixed());
                    shell.OpenManagement(MapModule.Market);
                    shell.CloseContext();
                }
                else
                    shell.CloseContext();
                return;
            }

            if (file.StartsWith("fleet-"))
            {
                MapFleetStore.Override(MapFleetFixtures.ReadyThree());
                shell.OpenManagement(MapModule.Fleet);
                shell.SetManagementExpanded(file.Contains("expanded"));
                if (file.Contains("expanded"))
                    shell.Management.Fleet.Select("long_truck");
                return;
            }

            if (file.StartsWith("jobs-"))
            {
                MapJobsStore.Override(MapJobsFixtures.Mixed());
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(file.Contains("expanded") || file.Contains("detail"));
                if (file.Contains("detail") || file.Contains("expanded"))
                    shell.Management.Jobs.Select("offer_izmir_istanbul");
                return;
            }

            if (file.StartsWith("warehouses-"))
            {
                MapWarehouseStore.Override(MapWarehouseFixtures.Mixed());
                shell.OpenManagement(MapModule.Warehouses);
                shell.SetManagementExpanded(file.Contains("expanded"));
                if (file.Contains("expanded"))
                    shell.Management.Warehouses.Select("wh_long_de");
                return;
            }

            MapMarketStore.Override(MapMarketFixtures.Mixed());
            shell.OpenManagement(MapModule.Market);
            var expand = file.Contains("expanded") || file.Contains("detail");
            shell.SetManagementExpanded(expand);
            if (file.Contains("detail"))
                shell.Management.Market.Select("izmir/steel");
            else if (expand)
                shell.Management.Market.ShowList();
            shell.Management.Market.Refresh();
        }

        static CaptureShot[] Ui06aR1Shots()
        {
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("before/market-expanded-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("diagnostics/market-932-map-only", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("diagnostics/market-932-compact", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("diagnostics/market-932-layer-bounds", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("diagnostics/market-932-sample-points", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("after/market-expanded-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("after/market-expanded-2868", 1, 2868, 1320, Phone(), false, false, false, "de"),
                Shot("after/market-compact-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("after/fleet-expanded-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("after/jobs-expanded-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("after/warehouses-expanded-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("after/map-after-panel-close", 1, 932, 430, compact, false, false, false, "tr")
            };
        }

        static void PoseUi06aR1(MapCommandShell shell, CaptureShot shot)
        {
            MapMarketStore.ClearOverride();
            MapWarehouseStore.ClearOverride();
            MapFleetStore.ClearOverride();
            MapJobsStore.ClearOverride();
            MapOperationStrip.ClearOverride();
            var file = shot.File ?? string.Empty;
            if (file.Contains("map-only") || file.Contains("map-after-panel-close"))
            {
                if (file.Contains("map-after-panel-close"))
                {
                    MapMarketStore.Override(MapMarketFixtures.Mixed());
                    shell.OpenManagement(MapModule.Market);
                    shell.SetManagementExpanded(true);
                    shell.CloseContext();
                }
                else
                    shell.CloseContext();
                return;
            }

            if (file.Contains("fleet-"))
            {
                MapFleetStore.Override(MapFleetFixtures.ReadyThree());
                shell.OpenManagement(MapModule.Fleet);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("jobs-"))
            {
                MapJobsStore.Override(MapJobsFixtures.Mixed());
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("warehouses-"))
            {
                MapWarehouseStore.Override(MapWarehouseFixtures.Mixed());
                shell.OpenManagement(MapModule.Warehouses);
                shell.SetManagementExpanded(true);
                return;
            }

            MapMarketStore.Override(MapMarketFixtures.Mixed());
            shell.OpenManagement(MapModule.Market);
            var expand = file.Contains("expanded") || file.Contains("layer-bounds") || file.Contains("sample-points");
            shell.SetManagementExpanded(expand);
            if (shell.Management.Market != null)
            {
                shell.Management.Market.ShowList();
                shell.Management.Market.Refresh();
            }
        }

        static void AnnotateUi06aR1(Texture2D output, Color32[] ui, Color32[] map, MapCommandShell shell, CaptureShot shot, string detail)
        {
            var notes = ActiveRoot() + "/diagnostics/market-932-sample.txt";
            if (shot.Width == 932 && shot.File.Contains("932"))
            {
                var extra = detail;
                if (shot.File.Contains("layer-bounds") || shot.File.Contains("sample-points") || shot.File.Contains("before/") || shot.File.Contains("after/market-expanded-932"))
                    File.WriteAllText(notes, extra + Environment.NewLine);
            }

            if (!shot.File.Contains("layer-bounds") && !shot.File.Contains("sample-points"))
                return;

            var metrics = MapCommandLayout.Evaluate(shot.Width, shot.Height, shot.Safe, shell.ContextOpen);
            var touch = MapCommandLayout.TouchPixels(shot.Width, shot.Height);
            MapHudCoverage.OverlayRects(shell.Viewport, metrics.FontPixels, touch, shell.ManagementOpen, shell.ManagementExpanded, out var panel, out var blocker);
            if (shot.File.Contains("layer-bounds"))
            {
                StrokeRect(output, shell.HeaderRect, new Color32(0, 200, 255, 255));
                StrokeRect(output, shell.SummaryRect, new Color32(255, 0, 180, 255));
                StrokeRect(output, shell.RailRect, new Color32(255, 210, 0, 255));
                StrokeRect(output, shell.Viewport, new Color32(120, 120, 120, 255));
                if (blocker.width > 1f)
                    StrokeRect(output, blocker, new Color32(255, 140, 0, 255));
                if (panel.width > 1f)
                    StrokeRect(output, panel, new Color32(40, 255, 80, 255));
            }

            var hole = MapHudCoverage.HolePixel(shot.Width, shot.Height);
            Mark(output, hole, new Color32(255, 40, 40, 255));
            if (shot.File.Contains("sample-points"))
            {
                Mark(output, new Vector2(shell.HeaderRect.center.x, shell.HeaderRect.center.y), new Color32(0, 200, 255, 255));
                Mark(output, new Vector2(shell.SummaryRect.center.x, shell.SummaryRect.center.y), new Color32(255, 0, 180, 255));
                Mark(output, new Vector2(shell.RailRect.center.x, shell.RailRect.center.y), new Color32(255, 210, 0, 255));
                if (panel.width > 1f)
                    Mark(output, panel.center, new Color32(40, 255, 80, 255));
                var mapProbe = new Vector2(shell.Viewport.xMin + 8f, shell.Viewport.center.y);
                if (!MapHudCoverage.Contains(panel, mapProbe) && !MapHudCoverage.Contains(blocker, mapProbe))
                    Mark(output, mapProbe, new Color32(255, 255, 255, 255));
            }

            output.Apply(false, false);
        }

        static string RectText(Rect rect)
        {
            return "(" + rect.x.ToString("0.0") + "," + rect.y.ToString("0.0") + " " + rect.width.ToString("0.0") + "x" + rect.height.ToString("0.0") + ")";
        }

        static void StrokeRect(Texture2D texture, Rect rect, Color32 color)
        {
            if (texture == null || rect.width < 1f || rect.height < 1f)
                return;

            var x0 = Mathf.Clamp(Mathf.RoundToInt(rect.xMin), 0, texture.width - 1);
            var x1 = Mathf.Clamp(Mathf.RoundToInt(rect.xMax) - 1, 0, texture.width - 1);
            var y0 = Mathf.Clamp(Mathf.RoundToInt(rect.yMin), 0, texture.height - 1);
            var y1 = Mathf.Clamp(Mathf.RoundToInt(rect.yMax) - 1, 0, texture.height - 1);
            for (var x = x0; x <= x1; x++)
            {
                texture.SetPixel(x, y0, color);
                texture.SetPixel(x, y1, color);
            }

            for (var y = y0; y <= y1; y++)
            {
                texture.SetPixel(x0, y, color);
                texture.SetPixel(x1, y, color);
            }
        }

        static void Mark(Texture2D texture, Vector2 pixel, Color32 color)
        {
            if (texture == null)
                return;

            var cx = Mathf.RoundToInt(pixel.x);
            var cy = Mathf.RoundToInt(pixel.y);
            for (var d = -6; d <= 6; d++)
            {
                var x = cx + d;
                var y = cy + d;
                if (x >= 0 && x < texture.width && cy >= 0 && cy < texture.height)
                    texture.SetPixel(x, cy, color);
                if (cx >= 0 && cx < texture.width && y >= 0 && y < texture.height)
                    texture.SetPixel(cx, y, color);
            }
        }

        static CaptureShot[] Ui03bShots()
        {
            var phone = Phone();
            var full1920 = Full(1920, 1080);
            var full1280 = Full(1280, 720);
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("map-only-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-compact-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("fleet-expanded-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-compact-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("footer-unconfigured-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("footer-loading-fixture-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("footer-error-fixture-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("footer-idle-fixture-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("footer-single-fixture-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("footer-multiple-fixture-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("map-only-1920-tr", 1, 1920, 1080, full1920, false, false, false, "tr"),
                Shot("fleet-compact-1920-tr", 1, 1920, 1080, full1920, false, false, false, "tr"),
                Shot("map-only-1280-tr", 1, 1280, 720, full1280, false, false, false, "tr"),
                Shot("map-only-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("fleet-compact-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("fleet-expanded-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("map-only-2868-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("footer-unconfigured-2868-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("footer-single-fixture-2868-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("header-north-america-2868-de", 5, 2868, 1320, phone, false, false, false, "de")
            };
        }

        static void PoseUi03b(MapCommandShell shell, CaptureShot shot)
        {
            MapOperationStrip.ClearOverride();
            var file = shot.File ?? string.Empty;
            if (file.Contains("footer-loading-fixture"))
                MapOperationStrip.Override(MapOperationSnapshot.Fixture(MapOperationStatus.Loading));
            else if (file.Contains("footer-error-fixture"))
                MapOperationStrip.Override(MapOperationSnapshot.Fixture(MapOperationStatus.Error));
            else if (file.Contains("footer-idle-fixture"))
                MapOperationStrip.Override(MapOperationSnapshot.Fixture(MapOperationStatus.Idle));
            else if (file.Contains("footer-single-fixture"))
                MapOperationStrip.Override(MapOperationSnapshot.Fixture(MapOperationStatus.SingleActive, 1, "Ankara", "İzmir", ""));
            else if (file.Contains("footer-multiple-fixture"))
                MapOperationStrip.Override(MapOperationSnapshot.Fixture(MapOperationStatus.MultipleActive, 3));

            if (file.Contains("jobs"))
                shell.OpenManagement(MapModule.Jobs);
            else if (file.Contains("fleet"))
            {
                shell.OpenManagement(MapModule.Fleet);
                shell.SetManagementExpanded(file.Contains("expanded"));
            }
            else
                shell.CloseContext();

            shell.RefreshStrip();
        }

        static void PoseUi04a(MapCommandShell shell, CaptureShot shot)
        {
            var file = shot.File ?? string.Empty;
            if (file.Contains("after-close") || file.Contains("map-only") || file.Contains("compact-access"))
            {
                shell.CloseContext();
                return;
            }

            if (file.Contains("jobs"))
                shell.OpenManagement(MapModule.Jobs);
            else if (file.Contains("warehouses"))
                shell.OpenManagement(MapModule.Warehouses);
            else if (file.Contains("market"))
                shell.OpenManagement(MapModule.Market);
            else
                shell.OpenManagement(MapModule.Fleet);

            shell.SetManagementExpanded(file.Contains("expanded"));
        }

        static CaptureShot[] Ui02Shots()
        {
            return new[]
            {
                Shot("asia-2868", 3, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("asia-1920", 3, 1920, 1080, Full(1920, 1080), false, false, false, "tr"),
                Shot("world-2868", 0, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("turkey-2868", 1, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("europe-2868", 2, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("header-asia", 3, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("header-world", 0, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("header-north-america", 5, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("header-german", 5, 2868, 1320, Phone(), false, false, false, "de"),
                Shot("rail-2868", 1, 2868, 1320, Phone(), false, false, false, "tr"),
                Shot("rail-932", 1, 932, 430, new Rect(32f, 16f, 868f, 398f), false, false, false, "tr")
            };
        }

        static CaptureShot[] Ios04aShots()
        {
            var phone = Phone();
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("01-map-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("02-fleet-rail-aligned-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("03-jobs-compact-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("04-jobs-expanded-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("05-jobs-after-close-2868", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("06-region-menu-2868-tr", 1, 2868, 1320, phone, false, true, false, "tr"),
                Shot("07-region-menu-932-de", 1, 932, 430, compact, false, true, false, "de"),
                Shot("08-blacksea-turkiye-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("09-blacksea-turkiye-en", 1, 2868, 1320, phone, false, false, false, "en"),
                Shot("10-blacksea-turkiye-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("11-road-coming-soon-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("12-rail-coming-soon-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("13-sea-coming-soon-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("14-air-coming-soon-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("15-coming-soon-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("16-rail-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("17-jobs-expanded-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("18-popup-932", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("19-map-after-popup-close", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("20-map-after-all-close", 1, 2868, 1320, phone, false, false, false, "tr")
            };
        }

        static CaptureShot[] Ios04cR1Shots()
        {
            var phone = Phone();
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("01-road-compact-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("02-road-expanded-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("03-road-unconfigured-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("04-road-fixture-list-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("05-road-fixture-detail-2868-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("06-rail-coming-soon-2868-tr", 1, 2868, 1320, phone, false, false, true, "tr", 0.35f),
                Shot("07-rail-coming-soon-932-tr", 1, 932, 430, compact, false, false, true, "tr", 0f),
                Shot("08-rail-coming-soon-2868-de", 1, 2868, 1320, phone, false, false, true, "de", 0.35f),
                Shot("09-region-turkiye", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("10-region-north-america", 5, 2868, 1320, phone, false, false, false, "tr"),
                Shot("11-region-south-america", 6, 2868, 1320, phone, false, false, false, "tr"),
                Shot("12-region-world", 0, 2868, 1320, phone, false, false, false, "tr"),
                Shot("13-region-long-de", 5, 2868, 1320, phone, false, false, false, "de"),
                Shot("14-region-menu-open-de", 6, 932, 430, compact, false, true, true, "de", 0f),
                Shot("15-jobs-expanded", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("16-jobs-after-close", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("17-road-after-close", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("18-map-after-all-tests", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("19-header-kuzey-amerika", 5, 2868, 1320, phone, false, false, false, "tr"),
                Shot("20-rail-bottom-2868-tr", 1, 2868, 1320, phone, false, false, true, "tr", 0f),
                Shot("21-road-expanded-932-tr", 1, 932, 430, compact, false, false, false, "tr"),
                Shot("22-region-chip-932-de", 6, 932, 430, compact, false, false, false, "de")
            };
        }

        static CaptureShot[] Ios04cShots()
        {
            var phone = Phone();
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("01-road-compact-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("02-road-expanded-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("03-road-unconfigured-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("04-road-fixture-list-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("05-road-fixture-detail-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("06-rail-coming-soon-tr", 1, 2868, 1320, phone, false, false, true, "tr", 0.45f),
                Shot("07-sea-coming-soon-tr", 1, 2868, 1320, phone, false, false, true, "tr", 0.2f),
                Shot("08-air-coming-soon-tr", 1, 2868, 1320, phone, false, false, true, "tr", 0f),
                Shot("09-transport-rail-932-tr", 1, 932, 430, compact, false, false, true, "tr", 0f),
                Shot("10-region-turkiye", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("11-region-north-america", 5, 2868, 1320, phone, false, false, false, "tr"),
                Shot("12-region-south-america", 6, 2868, 1320, phone, false, false, false, "tr"),
                Shot("13-region-world", 0, 2868, 1320, phone, false, false, false, "tr"),
                Shot("14-region-long-de", 5, 2868, 1320, phone, false, false, false, "de"),
                Shot("15-region-menu-open-de", 6, 932, 430, compact, false, true, true, "de", 0f),
                Shot("16-jobs-compact", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("17-jobs-expanded", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("18-jobs-after-close", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("19-road-after-close-map", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("20-map-after-all-tests", 1, 2868, 1320, phone, false, false, false, "tr")
            };
        }

        static CaptureShot[] Ios04aR1Shots()
        {
            var phone = Phone();
            var compact = new Rect(32f, 16f, 868f, 398f);
            return new[]
            {
                Shot("rail-2868-top-tr", 1, 2868, 1320, phone, false, false, false, "tr", 1f),
                Shot("rail-2868-bottom-tr", 1, 2868, 1320, phone, false, false, true, "tr", 0f),
                Shot("rail-932-top-tr", 1, 932, 430, compact, false, false, false, "tr", 1f),
                Shot("rail-932-bottom-tr", 1, 932, 430, compact, false, false, true, "tr", 0f),
                Shot("region-2868-top-tr", 1, 2868, 1320, phone, false, true, false, "tr", 1f),
                Shot("region-2868-bottom-tr", 1, 2868, 1320, phone, false, true, true, "tr", 0f),
                Shot("region-932-top-de", 1, 932, 430, compact, false, true, false, "de", 1f),
                Shot("region-932-bottom-de", 1, 932, 430, compact, false, true, true, "de", 0f),
                Shot("air-popup-2868-tr", 1, 2868, 1320, phone, false, false, true, "tr", 0f),
                Shot("air-popup-932-tr", 1, 932, 430, compact, false, false, true, "tr", 0f),
                Shot("jobs-expanded-close-visible", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("jobs-after-close", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("blacksea-turkiye-tr", 1, 2868, 1320, phone, false, false, false, "tr"),
                Shot("blacksea-turkiye-de", 1, 2868, 1320, phone, false, false, false, "de"),
                Shot("map-after-all-tests", 1, 2868, 1320, phone, false, false, false, "tr")
            };
        }

        static void PoseIos04c(MapCommandShell shell, CaptureShot shot)
        {
            var file = shot.File ?? string.Empty;
            MapRoadDeliveryStore.ClearOverride();
            shell.CloseSoon();
            shell.CloseContext();
            shell.CloseRegionMenu();
            if (!shot.Menu)
            {
                var menu = Named(shell.transform, "RegionMenu");
                if (menu != null)
                    menu.gameObject.SetActive(false);
            }

            if (file.Contains("fixture-list"))
            {
                MapRoadDeliveryStore.Override(MapRoadFixtures.Many());
                shell.OpenManagement(MapModule.Road);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("fixture-detail"))
            {
                MapRoadDeliveryStore.Override(MapRoadFixtures.One());
                shell.OpenManagement(MapModule.Road);
                shell.SetManagementExpanded(true);
                if (shell.Management != null && shell.Management.Road != null)
                    shell.Management.Road.Select("d1");
                return;
            }

            if (file.Contains("road-compact"))
            {
                shell.OpenManagement(MapModule.Road);
                shell.SetManagementExpanded(false);
                return;
            }

            if (file.Contains("road-expanded") || file.Contains("road-unconfigured"))
            {
                shell.OpenManagement(MapModule.Road);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("jobs-compact"))
            {
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(false);
                return;
            }

            if (file.Contains("jobs-after-close"))
            {
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                shell.CloseContext();
                return;
            }

            if (file.Contains("jobs-expanded"))
            {
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("road-after-close") || file.Contains("after-all"))
            {
                shell.OpenManagement(MapModule.Road);
                shell.SetManagementExpanded(true);
                shell.CloseContext();
                return;
            }

            if (file.Contains("road-expanded-932") || file.Contains("21-road-expanded"))
            {
                shell.OpenManagement(MapModule.Road);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("rail-bottom"))
            {
                shell.ScrollRail(0f);
                return;
            }

            if (file.Contains("coming-soon") || file.Contains("transport-rail"))
                shell.ScrollRail(shot.Scroll ? shot.ScrollPosition : 0f);
        }

        static bool GraphicsCaptureAvailable()
        {
            return SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        }

        static string GraphicsProbeText()
        {
            return "graphicsDeviceType=" + SystemInfo.graphicsDeviceType
                + "\ngraphicsDeviceName=" + SystemInfo.graphicsDeviceName
                + "\ngraphicsDeviceVersion=" + SystemInfo.graphicsDeviceVersion
                + "\ngraphicsMemoryMB=" + SystemInfo.graphicsMemorySize
                + "\noperatingSystem=" + SystemInfo.operatingSystem
                + "\n";
        }

        static void WriteGraphicsHold(string notes)
        {
            Directory.CreateDirectory(ActiveRoot() + "/reports");
            Directory.CreateDirectory(ActiveRoot() + "/logs");
            File.WriteAllText(ActiveRoot() + "/logs/graphics-probe.txt", GraphicsProbeText());
            File.WriteAllText(
                ActiveRoot() + "/reports/CAPTURE_GPU_ROOT_CAUSE.md",
                "# CAPTURE GPU HOLD\n\n" + notes + "\n\nRenderTexture.Create is refused when GraphicsDeviceType.Null.\nDo not capture with -nographics.\n");
        }

        static void PoseIos04aR1(MapCommandShell shell, CaptureShot shot)
        {
            var file = shot.File ?? string.Empty;
            shell.CloseSoon();
            shell.CloseContext();
            shell.CloseRegionMenu();
            if (!shot.Menu)
            {
                var menu = Named(shell.transform, "RegionMenu");
                if (menu != null)
                    menu.gameObject.SetActive(false);
            }

            if (file.Contains("jobs-expanded"))
            {
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("jobs-after-close") || file.Contains("map-after-all"))
            {
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                shell.CloseContext();
                return;
            }

            if (file.Contains("air-popup"))
            {
                shell.ScrollRail(0f);
                shell.ShowModule(7);
                return;
            }

            if (file.StartsWith("rail-") && shot.Scroll)
                shell.ScrollRail(shot.ScrollPosition);
            else if (file.StartsWith("rail-"))
                shell.ScrollRail(1f);
        }

        static void PoseIos04a(MapCommandShell shell, CaptureShot shot)
        {
            var file = shot.File ?? string.Empty;
            shell.CloseSoon();
            shell.CloseContext();
            shell.CloseRegionMenu();
            if (!shot.Menu)
            {
                var menu = Named(shell.transform, "RegionMenu");
                if (menu != null)
                    menu.gameObject.SetActive(false);
            }
            if (file.Contains("jobs-compact"))
            {
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(false);
                return;
            }

            if (file.Contains("jobs-expanded") || file.Contains("17-jobs"))
            {
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                return;
            }

            if (file.Contains("fleet") || file.Contains("02-"))
            {
                shell.OpenManagement(MapModule.Fleet);
                shell.SetManagementExpanded(false);
                return;
            }

            if (file.Contains("road-coming"))
            {
                shell.ShowModule(4);
                return;
            }

            if (file.Contains("rail-coming"))
            {
                shell.ShowModule(5);
                return;
            }

            if (file.Contains("sea-coming"))
            {
                shell.ShowModule(6);
                return;
            }

            if (file.Contains("air-coming") || file.Contains("15-coming") || file.Contains("18-popup"))
            {
                shell.ShowModule(7);
                return;
            }

            if (file.Contains("after-popup-close"))
            {
                shell.ShowModule(4);
                shell.CloseSoon();
                return;
            }

            if (file.Contains("after-close") || file.Contains("after-all-close"))
            {
                shell.OpenManagement(MapModule.Jobs);
                shell.SetManagementExpanded(true);
                shell.CloseContext();
            }
        }

        static CaptureShot Shot(string file, int region, int width, int height, Rect safe, bool closest, bool menu, bool scroll, string locale, float scrollPosition = 0f, bool context = false)
        {
            return new CaptureShot(file, region, width, height, safe, closest, menu, scroll, locale, scrollPosition, context);
        }

        static CaptureShot FocusShot(string file, int region, int width, int height, Rect safe, float latitude, float longitude, float sizeScale)
        {
            return new CaptureShot(file, region, width, height, safe, false, false, false, "tr", 0f, false, latitude, longitude, sizeScale);
        }

        static CaptureShot DiagShot(string file, bool closest, float latitude, float longitude, float sizeScale, int diagnostic)
        {
            return new CaptureShot(file, 1, 2868, 1320, Phone(), closest, false, false, "tr", 0f, false, latitude, longitude, sizeScale, diagnostic);
        }

        static void WriteShotPng(string file, byte[] png)
        {
            var path = SessionState.GetBool(DeviceIos04cR1Key, false) || SessionState.GetBool(DeviceIos04cKey, false) || SessionState.GetBool(Ios04aR1Key, false) || SessionState.GetBool(Ios04aKey, false)
                ? ActiveRoot() + "/after/" + file + ".png"
                : SessionState.GetBool(Ui06aR1Key, false)
                ? ActiveRoot() + "/" + file + ".png"
                : SessionState.GetBool(Ui06aKey, false)
                ? ActiveRoot() + "/after/" + file + ".png"
                : SessionState.GetBool(R1Key, false) || SessionState.GetBool(Ui04aR1Key, false)
                ? ActiveRoot() + "/" + file + ".png"
                : ActiveRoot() + "/captures/" + file + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, png);
        }

        static void WriteRejectedPng(string file, byte[] png)
        {
            var name = file.Replace('/', '-') + "-rejected.png";
            File.WriteAllBytes(ActiveRoot() + "/measurements/" + name, png);
        }

        static void ApplyDiagnostic(MapRegionalReliefPresenter presenter, CaptureShot shot, StringBuilder notes)
        {
            if (presenter == null || presenter.RuntimeMaterial == null)
            {
                notes.AppendLine(shot.File + " diagnostic: presenter missing");
                return;
            }

            var material = presenter.RuntimeMaterial;
            var globalTex = material.GetTexture("_MainTex");
            var regionalTex = material.GetTexture("_RegionalTex");
            notes.AppendLine(
                shot.File
                + " relief global=" + (globalTex != null ? globalTex.name + " " + globalTex.width + "x" + globalTex.height : "null")
                + " regional=" + (regionalTex != null ? regionalTex.name + " " + regionalTex.width + "x" + regionalTex.height : "null")
                + " detail=" + material.GetFloat("_Detail").ToString("0.000", CultureInfo.InvariantCulture)
                + " min=" + material.GetVector("_RegionMin")
                + " max=" + material.GetVector("_RegionMax")
                + " diag=" + shot.Diagnostic);

            presenter.GlobalOnly = shot.Diagnostic == DiagGlobalOnly;
            if (shot.Diagnostic == DiagNone || shot.Diagnostic == DiagGlobalOnly)
            {
                material.SetFloat("_Detail", presenter.Evaluate());
                notes.AppendLine(shot.File + " detailAfterPose=" + material.GetFloat("_Detail").ToString("0.000", CultureInfo.InvariantCulture));
                return;
            }

            material.SetFloat("_Detail", 1f);
            if (shot.Diagnostic == DiagCopernicusOnly)
            {
                material.SetTexture("_MainTex", NavyTex());
                return;
            }

            if (shot.Diagnostic == DiagOldRegional)
            {
                var fallback = Resources.Load<Texture2D>(MapRegionalRelief.FallbackTextureResource);
                material.SetTexture("_RegionalTex", fallback);
                material.SetVector("_RegionMin", new Vector4(18.166666f, 30.433333f, 0f, 0f));
                material.SetVector("_RegionMax", new Vector4(52.3f, 47.5f, 0f, 0f));
                return;
            }

            if (shot.Diagnostic == DiagAlphaCoverage)
            {
                material.SetTexture("_MainTex", NavyTex());
                material.SetTexture("_RegionalTex", AlphaCoverageTex());
            }
        }

        static void RestoreDiagnostic(MapRegionalReliefPresenter presenter)
        {
            if (presenter == null || presenter.RuntimeMaterial == null)
                return;

            presenter.GlobalOnly = false;
            var material = presenter.RuntimeMaterial;
            var original = presenter.OriginalMaterial;
            if (original != null)
                material.SetTexture("_MainTex", original.GetTexture("_MainTex"));
            var copernicus = Resources.Load<Texture2D>(MapRegionalRelief.TextureResource);
            if (copernicus != null)
                material.SetTexture("_RegionalTex", copernicus);
            var halo = Resources.Load<Texture2D>(MapRegionalRelief.FallbackTextureResource);
            if (halo != null)
                material.SetTexture("_HaloTex", halo);
            material.SetVector("_RegionMin", MapRegionalRelief.RegionMinVector());
            material.SetVector("_RegionMax", MapRegionalRelief.RegionMaxVector());
            material.SetVector("_HaloMin", MapRegionalRelief.HaloMinVector());
            material.SetVector("_HaloMax", MapRegionalRelief.HaloMaxVector());
            material.SetFloat("_Detail", presenter.Evaluate());
        }

        static Texture2D NavyTex()
        {
            if (_diagNavy != null)
                return _diagNavy;
            _diagNavy = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _diagNavy.SetPixel(0, 0, new Color32(4, 12, 22, 255));
            _diagNavy.Apply(false, true);
            _diagNavy.hideFlags = HideFlags.HideAndDontSave;
            return _diagNavy;
        }

        static Texture2D AlphaCoverageTex()
        {
            if (_diagAlpha != null)
                return _diagAlpha;
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes("Assets/Game/Map/Resources/MapReliefCopernicus.png"));
            var pixels = source.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var a = pixels[i].a;
                pixels[i] = new Color32(a, (byte)(a > 8 ? 40 : 0), (byte)(255 - a), a > 0 ? (byte)255 : (byte)0);
            }

            _diagAlpha = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            _diagAlpha.wrapMode = TextureWrapMode.Clamp;
            _diagAlpha.filterMode = FilterMode.Bilinear;
            _diagAlpha.SetPixels32(pixels);
            _diagAlpha.Apply(false, true);
            _diagAlpha.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DestroyImmediate(source);
            return _diagAlpha;
        }

        static Rect Full(int width, int height)
        {
            return new Rect(0f, 0f, width, height);
        }

        static Rect Phone()
        {
            return new Rect(186f, 60f, 2496f, 1200f);
        }

        static T Find<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var item = roots[i].GetComponentInChildren<T>(true);
                if (item != null)
                    return item;
            }

            return null;
        }

        static int Count<T>(Scene scene) where T : Component
        {
            var count = 0;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                count += roots[i].GetComponentsInChildren<T>(true).Length;
            return count;
        }

        static string Sha(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var text = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                    text.Append(hash[i].ToString("x2"));
                return text.ToString();
            }
        }

        readonly struct CaptureShot
        {
            public readonly string File;
            public readonly int Region;
            public readonly int Width;
            public readonly int Height;
            public readonly Rect Safe;
            public readonly bool Closest;
            public readonly bool Menu;
            public readonly bool Scroll;
            public readonly string Locale;
            public readonly float ScrollPosition;
            public readonly bool Context;
            public readonly float FocusLatitude;
            public readonly float FocusLongitude;
            public readonly float FocusSizeScale;
            public readonly int Diagnostic;

            public CaptureShot(string file, int region, int width, int height, Rect safe, bool closest, bool menu, bool scroll, string locale, float scrollPosition = 0f, bool context = false, float focusLatitude = float.NaN, float focusLongitude = float.NaN, float focusSizeScale = 0f, int diagnostic = 0)
            {
                File = file;
                Region = region;
                Width = width;
                Height = height;
                Safe = safe;
                Closest = closest;
                Menu = menu;
                Scroll = scroll;
                Locale = string.IsNullOrEmpty(locale) ? "tr" : locale;
                ScrollPosition = scrollPosition;
                Context = context;
                FocusLatitude = focusLatitude;
                FocusLongitude = focusLongitude;
                FocusSizeScale = focusSizeScale;
                Diagnostic = diagnostic;
            }
        }
    }
}
