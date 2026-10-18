using System;
using System.Collections.Generic;
using System.IO;
using LogistiCore.Localization;
using LogistiCore.Map;
using LogistiCore.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.EditorTools
{
    public static class MapSceneSetup
    {
        public const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string PendingScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.pending.unity";

        const string TablesFolder = "Assets/Game/Localization/Tables";

        static string _sceneSavePath = ScenePath;

        static readonly Color Ocean = new Color(4f / 255f, 12f / 255f, 22f / 255f, 1f);
        static readonly Color Panel = new Color(6f / 255f, 14f / 255f, 24f / 255f, 0.9f);
        static readonly Color Button = new Color(8f / 255f, 20f / 255f, 32f / 255f, 1f);
        static readonly Color Ink = new Color(232f / 255f, 242f / 255f, 248f / 255f, 1f);
        static readonly Color Muted = new Color(150f / 255f, 178f / 255f, 198f / 255f, 1f);
        static readonly Color Cyan = new Color(34f / 255f, 199f / 255f, 242f / 255f, 1f);

        [MenuItem("LogistiCore/Map/Setup World Map")]
        public static void SetupFromMenu()
        {
            EnsureAll(openScene: true);
        }

        public static void EnsureAll(bool openScene)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[LogistiCore] Exit Play mode before creating the world map.");
                return;
            }

            FoundationSetup.EnsureLocalization();
            EnsureLocalization();
            MapGeographyBaker.EnsureBaked();
            EnsureScene();
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            if (openScene)
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void EnsureLocalization()
        {
            FoundationSetup.EnsureLocalization();
            var english = LocalizationEditorSettings.GetLocale(LocaleCodes.English);
            var turkish = LocalizationEditorSettings.GetLocale(LocaleCodes.Turkish);
            var german = LocalizationEditorSettings.GetLocale(LocaleCodes.German);
            if (english == null || turkish == null || german == null)
                throw new InvalidOperationException("Map strings need the English, Turkish, and German locales.");

            var collection = LocalizationEditorSettings.GetStringTableCollection(MapContent.Table);
            if (collection == null)
            {
                if (!AssetDatabase.IsValidFolder(TablesFolder))
                    throw new InvalidOperationException("Localization tables folder is missing.");

                collection = LocalizationEditorSettings.CreateStringTableCollection(MapContent.Table, TablesFolder);
            }

            var seed = BuildSeed();
            var englishTable = RequireTable(collection, english);
            var turkishTable = RequireTable(collection, turkish);
            var germanTable = RequireTable(collection, german);
            for (var i = 0; i < MapContent.Keys.Length; i++)
            {
                var key = MapContent.Keys[i];
                if (!seed.TryGetValue(key, out var row))
                    throw new InvalidOperationException("Missing map seed for " + key);

                SetEntry(englishTable, key, row.English);
                SetEntry(turkishTable, key, row.Turkish);
                SetEntry(germanTable, key, row.German);
            }

            TryPreload(englishTable);
            TryPreload(turkishTable);
            TryPreload(germanTable);
        }

        public enum MapSceneFileAction
        {
            None = 0,
            CreateMissing = 1,
            ReplaceWithBackup = 2
        }

        /// <summary>
        /// Editor startup never creates or replaces the map scene.
        /// An explicit setup request leaves a current scene alone, creates one only when
        /// the file is missing, and replaces an outdated file only after a snapshot.
        /// </summary>
        public static MapSceneFileAction FileActionFor(bool explicitRequest, bool sceneExists, bool stampMatches)
        {
            if (!explicitRequest || (sceneExists && stampMatches))
                return MapSceneFileAction.None;

            return sceneExists ? MapSceneFileAction.ReplaceWithBackup : MapSceneFileAction.CreateMissing;
        }

        enum StampRead
        {
            Missing = 0,
            Current = 1,
            Outdated = 2,
            Unreadable = 3
        }

        static void EnsureScene()
        {
            var exists = SceneAssetExists();
            var stampMatches = false;
            if (exists)
            {
                var read = ReadStamp();
                if (read == StampRead.Unreadable)
                    throw new InvalidOperationException("Save the open scene, then run LogistiCore/Map/Setup World Map.");

                stampMatches = read == StampRead.Current;
            }

            var action = FileActionFor(explicitRequest: true, exists, stampMatches);
            if (action == MapSceneFileAction.None)
                return;

            ValidatePrerequisites();

            string snapshot = null;
            if (action == MapSceneFileAction.ReplaceWithBackup)
                snapshot = BackupExistingScene();

            var savePath = action == MapSceneFileAction.ReplaceWithBackup ? PendingScenePath : ScenePath;
            _sceneSavePath = savePath;
            var replaced = false;
            try
            {
                CreateScene();
                var saved = AbsoluteAsset(savePath);
                if (!File.Exists(saved) || new FileInfo(saved).Length < 64)
                    throw new InvalidOperationException("The map scene was not saved.");

                if (action == MapSceneFileAction.ReplaceWithBackup)
                {
                    ReplaceSceneFilePreservingMeta(saved, AbsoluteAsset(ScenePath));
                    replaced = true;
                    DeletePendingScene();
                    AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
                    RestoreMetaGuid(snapshot);
                }
            }
            catch (Exception)
            {
                if (savePath != ScenePath)
                    DeletePendingScene();
                if (replaced && snapshot != null)
                    RestoreSnapshot(snapshot);
                throw;
            }
        }

        static void ValidatePrerequisites()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play mode before creating the world map.");

            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !Application.isBatchMode)
                throw new InvalidOperationException("Save the open scene, then run LogistiCore/Map/Setup World Map.");

            if (AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.LandFillMeshPath) == null ||
                AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.CoastMeshPath) == null ||
                AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.TurkeyMeshPath) == null ||
                AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.OceanMeshPath) == null ||
                AssetDatabase.LoadAssetAtPath<Material>(MapGeographyBaker.ReliefMaterialPath) == null ||
                AssetDatabase.LoadAssetAtPath<Material>(MapGeographyBaker.LineMaterialPath) == null ||
                LoadSprite(MapGeographyBaker.MarkerTexturePath) == null ||
                LoadSprite(MapGeographyBaker.MarkerGlowTexturePath) == null ||
                LoadSprite(MapGeographyBaker.VignetteTexturePath) == null)
                throw new InvalidOperationException("Map bake assets are incomplete.");
        }

        static StampRead ReadStamp()
        {
            if (!SceneAssetExists())
                return StampRead.Missing;

            var loaded = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = false;
            Scene scene;
            if (loaded.IsValid() && loaded.isLoaded)
            {
                scene = loaded;
            }
            else
            {
                var active = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path))
                {
                    if (!Application.isBatchMode)
                        return StampRead.Unreadable;

                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }

                var mode = string.IsNullOrEmpty(SceneManager.GetActiveScene().path)
                    ? OpenSceneMode.Single
                    : OpenSceneMode.Additive;
                scene = EditorSceneManager.OpenScene(ScenePath, mode);
                openedHere = mode == OpenSceneMode.Additive;
            }

            try
            {
                MapSceneStamp stamp = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    stamp = root.GetComponentInChildren<MapSceneStamp>(true);
                    if (stamp != null)
                        break;
                }

                return stamp != null && stamp.Version == MapSceneStamp.Current
                    ? StampRead.Current
                    : StampRead.Outdated;
            }
            finally
            {
                if (openedHere && SceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static bool SceneAssetExists()
        {
            return File.Exists(AbsoluteAsset(ScenePath)) ||
                   AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
        }

        static string BackupExistingScene()
        {
            var source = AbsoluteAsset(ScenePath);
            if (!File.Exists(source))
                throw new InvalidOperationException("The map scene disappeared before it could be backed up.");

            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LogistiCoreSceneSnapshots",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            var sceneCopy = Path.Combine(directory, "WorldMapVerticalSlice.unity");
            if (File.Exists(sceneCopy))
                throw new InvalidOperationException("A scene snapshot already exists at " + sceneCopy);

            File.Copy(source, sceneCopy, false);
            var meta = source + ".meta";
            if (File.Exists(meta))
                File.Copy(meta, sceneCopy + ".meta", false);

            return directory;
        }

        static void ReplaceSceneFilePreservingMeta(string savedPendingAbsolute, string sceneAbsolute)
        {
            var meta = sceneAbsolute + ".meta";
            var originalScene = File.ReadAllBytes(sceneAbsolute);
            var originalMeta = File.Exists(meta) ? File.ReadAllBytes(meta) : null;
            var originalGuid = originalMeta == null ? null : ReadGuid(meta);
            try
            {
                File.Copy(savedPendingAbsolute, sceneAbsolute, true);
                if (originalMeta != null)
                {
                    var guid = ReadGuid(meta);
                    if (guid != originalGuid)
                        File.WriteAllBytes(meta, originalMeta);
                }
            }
            catch (Exception)
            {
                File.WriteAllBytes(sceneAbsolute, originalScene);
                if (originalMeta != null)
                    File.WriteAllBytes(meta, originalMeta);
                throw;
            }
        }

        static void RestoreMetaGuid(string snapshotDirectory)
        {
            if (string.IsNullOrEmpty(snapshotDirectory))
                return;

            var metaCopy = Path.Combine(snapshotDirectory, "WorldMapVerticalSlice.unity.meta");
            if (!File.Exists(metaCopy))
                return;

            var expected = ReadGuid(metaCopy);
            var meta = AbsoluteAsset(ScenePath) + ".meta";
            if (expected != null && ReadGuid(meta) != expected)
                File.Copy(metaCopy, meta, true);
        }

        static void RestoreSnapshot(string directory)
        {
            var sceneCopy = Path.Combine(directory, "WorldMapVerticalSlice.unity");
            if (!File.Exists(sceneCopy))
                return;

            File.Copy(sceneCopy, AbsoluteAsset(ScenePath), true);
            var metaCopy = sceneCopy + ".meta";
            if (File.Exists(metaCopy))
                File.Copy(metaCopy, AbsoluteAsset(ScenePath) + ".meta", true);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
        }

        static void DeletePendingScene()
        {
            if (PendingScenePath == ScenePath)
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PendingScenePath) != null)
                AssetDatabase.DeleteAsset(PendingScenePath);

            var absolute = AbsoluteAsset(PendingScenePath);
            if (File.Exists(absolute))
                File.Delete(absolute);
            if (File.Exists(absolute + ".meta"))
                File.Delete(absolute + ".meta");
        }

        static string ReadGuid(string metaPath)
        {
            foreach (var line in File.ReadLines(metaPath))
            {
                if (line.StartsWith("guid:", StringComparison.Ordinal))
                    return line.Substring("guid:".Length).Trim();
            }

            return null;
        }

        static string AbsoluteAsset(string assetPath)
        {
            var project = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(project, assetPath);
        }

        static void CreateScene()
        {
            var reliefMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.LandFillMeshPath);
            var coastMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.CoastMeshPath);
            var turkeyMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.TurkeyMeshPath);
            var reliefMaterial = AssetDatabase.LoadAssetAtPath<Material>(MapGeographyBaker.ReliefMaterialPath);
            var lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(MapGeographyBaker.LineMaterialPath);
            var oceanMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.OceanMeshPath);
            var markerSprite = LoadSprite(MapGeographyBaker.MarkerTexturePath);
            var glowSprite = LoadSprite(MapGeographyBaker.MarkerGlowTexturePath);
            var vignetteSprite = LoadSprite(MapGeographyBaker.VignetteTexturePath);
            if (reliefMesh == null || coastMesh == null || turkeyMesh == null || oceanMesh == null ||
                reliefMaterial == null || lineMaterial == null || markerSprite == null || glowSprite == null || vignetteSprite == null)
                throw new InvalidOperationException("Map bake assets are incomplete.");

            var active = SceneManager.GetActiveScene();
            var replaceUntitled = string.IsNullOrEmpty(active.path);
            if (replaceUntitled && !Application.isBatchMode)
                throw new InvalidOperationException("Save the open scene, then run LogistiCore/Map/Setup World Map.");

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                replaceUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ocean;
            camera.orthographic = true;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            if (cameraObject.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<MapCameraRig>();
            cameraObject.AddComponent<MapGestures>();
            cameraObject.AddComponent<MapOrientation>();

            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, 16f / 9f, MapFrames.TurkeyFocusPadding, out var center, out var size);
            camera.transform.position = new Vector3(center.x, center.y, MapCameraRig.CameraZ);
            camera.orthographicSize = size;

            var map = new GameObject("Map");
            map.AddComponent<MapSceneStamp>();
            CreateMesh("Ocean", map.transform, oceanMesh, lineMaterial, -5);
            CreateMesh("Relief", map.transform, reliefMesh, reliefMaterial, 0);
            CreateMesh("Coastlines", map.transform, coastMesh, lineMaterial, 10);
            CreateMesh("TurkeyCoast", map.transform, turkeyMesh, lineMaterial, 12);

            var cities = new GameObject("Cities");
            cities.transform.SetParent(map.transform, false);
            var markerLayer = cities.AddComponent<MapMarkerLayer>();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var vignette = CreateUi("Vignette", canvasObject.transform);
            var vignetteImage = vignette.AddComponent<Image>();
            vignetteImage.sprite = vignetteSprite;
            vignetteImage.color = new Color(1f, 1f, 1f, 0.92f);
            vignetteImage.raycastTarget = false;
            Stretch(vignette.GetComponent<RectTransform>());

            var labelsRoot = CreateUi("CityLabels", canvasObject.transform);
            Stretch(labelsRoot.GetComponent<RectTransform>());

            var safe = CreateUi("SafeArea", canvasObject.transform);
            safe.AddComponent<SafeAreaRect>();
            Stretch(safe.GetComponent<RectTransform>());

            var font = BuiltinFont();
            var top = CreatePanel("TopBar", safe.transform, Panel);
            var topRect = top.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = Vector2.zero;
            topRect.sizeDelta = new Vector2(0f, 64f);
            AddAccent(top);
            var topLayout = top.AddComponent<HorizontalLayoutGroup>();
            topLayout.padding = new RectOffset(16, 12, 8, 8);
            topLayout.spacing = 12f;
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = true;

            var title = CreateText("Title", top.transform, font, 26, FontStyle.Bold, Ink, TextAnchor.MiddleLeft);
            var titleElement = title.gameObject.AddComponent<LayoutElement>();
            titleElement.flexibleWidth = 1f;
            titleElement.minHeight = 40f;
            var scope = CreateText("Scope", top.transform, font, 16, FontStyle.Normal, Cyan, TextAnchor.MiddleRight);
            var scopeElement = scope.gameObject.AddComponent<LayoutElement>();
            scopeElement.preferredWidth = 220f;
            scopeElement.minHeight = 40f;
            var collapse = CreateButton("Collapse", top.transform, font, 15);
            var collapseElement = collapse.Root.AddComponent<LayoutElement>();
            collapseElement.preferredWidth = 168f;
            collapseElement.minWidth = 120f;
            collapseElement.minHeight = 44f;
            collapseElement.preferredHeight = 44f;

            var rail = CreatePanel("CommandRail", safe.transform, Panel);
            var railRect = rail.GetComponent<RectTransform>();
            railRect.anchorMin = new Vector2(0f, 0f);
            railRect.anchorMax = new Vector2(0f, 1f);
            railRect.pivot = new Vector2(0f, 0.5f);
            railRect.offsetMin = new Vector2(0f, 44f);
            railRect.offsetMax = new Vector2(132f, -64f);
            var railLayout = rail.AddComponent<VerticalLayoutGroup>();
            railLayout.padding = new RectOffset(10, 10, 10, 10);
            railLayout.spacing = 8f;
            railLayout.childAlignment = TextAnchor.UpperCenter;
            railLayout.childControlWidth = true;
            railLayout.childControlHeight = true;
            railLayout.childForceExpandWidth = true;
            railLayout.childForceExpandHeight = false;
            AddAccent(rail);
            var world = CreateButton("World", rail.transform, font, 15);
            var turkey = CreateButton("Turkey", rail.transform, font, 15);
            var zoomIn = CreateButton("ZoomIn", rail.transform, font, 22);
            var zoomOut = CreateButton("ZoomOut", rail.transform, font, 22);
            var restore = CreateButton("Restore", rail.transform, font, 15);
            PrepareRailButton(world);
            PrepareRailButton(turkey);
            PrepareRailButton(zoomIn);
            PrepareRailButton(zoomOut);
            PrepareRailButton(restore);

            var inspector = CreatePanel("Inspector", safe.transform, Panel);
            var inspectorRect = inspector.GetComponent<RectTransform>();
            inspectorRect.anchorMin = new Vector2(1f, 0f);
            inspectorRect.anchorMax = new Vector2(1f, 1f);
            inspectorRect.pivot = new Vector2(1f, 0.5f);
            inspectorRect.offsetMin = new Vector2(-320f, 44f);
            inspectorRect.offsetMax = new Vector2(0f, -64f);
            var inspectorLayout = inspector.AddComponent<VerticalLayoutGroup>();
            inspectorLayout.padding = new RectOffset(16, 16, 16, 16);
            inspectorLayout.spacing = 4f;
            inspectorLayout.childAlignment = TextAnchor.UpperLeft;
            inspectorLayout.childControlWidth = true;
            inspectorLayout.childControlHeight = true;
            inspectorLayout.childForceExpandWidth = true;
            inspectorLayout.childForceExpandHeight = false;
            AddAccent(inspector);

            var inspectorTitle = CreateText("InspectorTitle", inspector.transform, font, 20, FontStyle.Bold, Ink, TextAnchor.MiddleLeft);
            AddHeight(inspectorTitle, 32f);
            var inspectorScopeLabel = CreateText("ScopeLabel", inspector.transform, font, 13, FontStyle.Normal, Muted, TextAnchor.MiddleLeft);
            AddHeight(inspectorScopeLabel, 20f);
            var inspectorScopeValue = CreateText("ScopeValue", inspector.transform, font, 16, FontStyle.Bold, Cyan, TextAnchor.MiddleLeft);
            AddHeight(inspectorScopeValue, 28f);
            var inspectorCityLabel = CreateText("CityLabel", inspector.transform, font, 13, FontStyle.Normal, Muted, TextAnchor.MiddleLeft);
            AddHeight(inspectorCityLabel, 20f);
            var inspectorCityValue = CreateText("CityValue", inspector.transform, font, 16, FontStyle.Normal, Ink, TextAnchor.MiddleLeft);
            AddHeight(inspectorCityValue, 28f);
            var coordinatesRow = CreateUi("Coordinates", inspector.transform);
            var coordinatesLayout = coordinatesRow.AddComponent<VerticalLayoutGroup>();
            coordinatesLayout.spacing = 2f;
            coordinatesLayout.childAlignment = TextAnchor.UpperLeft;
            coordinatesLayout.childControlWidth = true;
            coordinatesLayout.childControlHeight = true;
            coordinatesLayout.childForceExpandWidth = true;
            coordinatesLayout.childForceExpandHeight = false;
            var coordinatesElement = coordinatesRow.AddComponent<LayoutElement>();
            coordinatesElement.minHeight = 52f;
            coordinatesElement.preferredHeight = 52f;
            var inspectorCoordinatesLabel = CreateText("CoordinatesLabel", coordinatesRow.transform, font, 13, FontStyle.Normal, Muted, TextAnchor.MiddleLeft);
            AddHeight(inspectorCoordinatesLabel, 20f);
            var inspectorCoordinatesValue = CreateText("CoordinatesValue", coordinatesRow.transform, font, 16, FontStyle.Normal, Ink, TextAnchor.MiddleLeft);
            AddHeight(inspectorCoordinatesValue, 28f);

            var bottom = CreatePanel("Status", safe.transform, Panel);
            var bottomRect = bottom.GetComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.offsetMin = new Vector2(132f, 0f);
            bottomRect.offsetMax = new Vector2(0f, 44f);
            AddAccent(bottom);
            var status = CreateText("Status", bottom.transform, font, 16, FontStyle.Normal, Muted, TextAnchor.MiddleLeft);
            Stretch(status.rectTransform);
            status.rectTransform.offsetMin = new Vector2(16f, 0f);
            status.rectTransform.offsetMax = new Vector2(-16f, 0f);

            var markerComponents = new List<MapCityMarker>(MapCities.All.Length);
            var labelComponents = new List<Text>(MapCities.All.Length);
            for (var i = 0; i < MapCities.All.Length; i++)
            {
                var city = MapCities.All[i];
                var projected = MapProjection.Project(city.Latitude, city.Longitude);
                var markerObject = new GameObject("City_" + city.Id);
                markerObject.transform.SetParent(cities.transform, false);
                markerObject.transform.position = new Vector3(projected.x, projected.y, 0f);
                var marker = markerObject.AddComponent<MapCityMarker>();
                var markerSerialized = new SerializedObject(marker);
                markerSerialized.FindProperty("cityId").stringValue = city.Id;
                markerSerialized.ApplyModifiedPropertiesWithoutUndo();

                var glowObject = new GameObject("Glow");
                glowObject.transform.SetParent(markerObject.transform, false);
                glowObject.transform.localScale = new Vector3(2.8f, 2.8f, 1f);
                var glow = glowObject.AddComponent<SpriteRenderer>();
                glow.sprite = glowSprite;
                glow.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f);
                glow.sortingOrder = 18;

                var coreObject = new GameObject("Core");
                coreObject.transform.SetParent(markerObject.transform, false);
                var core = coreObject.AddComponent<SpriteRenderer>();
                core.sprite = markerSprite;
                core.color = Cyan;
                core.sortingOrder = 20;

                var plate = CreateUi("Plate_" + city.Id, labelsRoot.transform);
                var plateImage = plate.AddComponent<Image>();
                plateImage.color = new Color(0.02f, 0.05f, 0.09f, 0.78f);
                plateImage.raycastTarget = false;
                var plateRect = plate.GetComponent<RectTransform>();
                plateRect.anchorMin = new Vector2(0.5f, 0.5f);
                plateRect.anchorMax = new Vector2(0.5f, 0.5f);
                plateRect.sizeDelta = new Vector2(176f, 32f);
                plate.SetActive(false);
                var label = CreateText("Label_" + city.Id, plate.transform, font, 16, FontStyle.Bold, Ink, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(8f, 2f);
                label.rectTransform.offsetMax = new Vector2(-8f, -2f);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                markerComponents.Add(marker);
                labelComponents.Add(label);
            }

            var shell = safe.AddComponent<MapShellView>();
            var shellSerialized = new SerializedObject(shell);
            shellSerialized.FindProperty("cameraRig").objectReferenceValue = cameraObject.GetComponent<MapCameraRig>();
            shellSerialized.FindProperty("markers").objectReferenceValue = markerLayer;
            shellSerialized.FindProperty("titleText").objectReferenceValue = title;
            shellSerialized.FindProperty("scopeText").objectReferenceValue = scope;
            shellSerialized.FindProperty("worldLabel").objectReferenceValue = world.Label;
            shellSerialized.FindProperty("turkeyLabel").objectReferenceValue = turkey.Label;
            shellSerialized.FindProperty("zoomInLabel").objectReferenceValue = zoomIn.Label;
            shellSerialized.FindProperty("zoomOutLabel").objectReferenceValue = zoomOut.Label;
            shellSerialized.FindProperty("restoreLabel").objectReferenceValue = restore.Label;
            shellSerialized.FindProperty("collapseLabel").objectReferenceValue = collapse.Label;
            shellSerialized.FindProperty("inspectorTitle").objectReferenceValue = inspectorTitle;
            shellSerialized.FindProperty("inspectorScopeLabel").objectReferenceValue = inspectorScopeLabel;
            shellSerialized.FindProperty("inspectorScopeValue").objectReferenceValue = inspectorScopeValue;
            shellSerialized.FindProperty("inspectorCityLabel").objectReferenceValue = inspectorCityLabel;
            shellSerialized.FindProperty("inspectorCityValue").objectReferenceValue = inspectorCityValue;
            shellSerialized.FindProperty("inspectorCoordinatesLabel").objectReferenceValue = inspectorCoordinatesLabel;
            shellSerialized.FindProperty("inspectorCoordinatesValue").objectReferenceValue = inspectorCoordinatesValue;
            shellSerialized.FindProperty("statusText").objectReferenceValue = status;
            shellSerialized.FindProperty("inspectorPanel").objectReferenceValue = inspector;
            shellSerialized.FindProperty("coordinatesRow").objectReferenceValue = coordinatesRow;
            shellSerialized.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(world.Button.onClick, shell.FocusWorld);
            UnityEventTools.AddPersistentListener(turkey.Button.onClick, shell.FocusTurkey);
            UnityEventTools.AddPersistentListener(zoomIn.Button.onClick, shell.ZoomIn);
            UnityEventTools.AddPersistentListener(zoomOut.Button.onClick, shell.ZoomOut);
            UnityEventTools.AddPersistentListener(restore.Button.onClick, shell.Restore);
            UnityEventTools.AddPersistentListener(collapse.Button.onClick, shell.ToggleInspector);

            var layerSerialized = new SerializedObject(markerLayer);
            layerSerialized.FindProperty("mapCamera").objectReferenceValue = camera;
            layerSerialized.FindProperty("labelRoot").objectReferenceValue = labelsRoot.GetComponent<RectTransform>();
            var markersProperty = layerSerialized.FindProperty("markers");
            var labelsProperty = layerSerialized.FindProperty("labels");
            markersProperty.arraySize = markerComponents.Count;
            labelsProperty.arraySize = labelComponents.Count;
            for (var i = 0; i < markerComponents.Count; i++)
            {
                markersProperty.GetArrayElementAtIndex(i).objectReferenceValue = markerComponents[i];
                labelsProperty.GetArrayElementAtIndex(i).objectReferenceValue = labelComponents[i];
            }

            layerSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, _sceneSavePath))
                throw new InvalidOperationException("The map scene could not be saved.");
            if (SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == ScenePath)
                    return;
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            for (var i = 0; i < scenes.Length; i++)
                updated[i] = scenes[i];

            updated[scenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }

        static void AddAccent(GameObject panel)
        {
            var accent = CreateUi("Accent", panel.transform);
            var image = accent.AddComponent<Image>();
            image.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f);
            image.raycastTarget = false;
            var rect = accent.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 2f);
            var element = accent.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }

        static void CreateMesh(string name, Transform parent, Mesh mesh, Material material, int sortingOrder)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Sprite LoadSprite(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                    return sprite;
            }

            return null;
        }

        static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var item = CreateUi(name, parent);
            var image = item.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return item;
        }

        static GameObject CreateUi(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            return item;
        }

        static Text CreateText(string name, Transform parent, Font font, int size, FontStyle style, Color color, TextAnchor alignment)
        {
            var item = CreateUi(name, parent);
            var text = item.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        static LabeledButton CreateButton(string name, Transform parent, Font font, int size)
        {
            var item = CreateUi(name, parent);
            var image = item.AddComponent<Image>();
            image.color = Button;
            var button = item.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.75f, 0.92f, 1f, 1f);
            colors.pressedColor = new Color(0.45f, 0.82f, 1f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var label = CreateText("Label", item.transform, font, size, FontStyle.Bold, Ink, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(4f, 2f);
            label.rectTransform.offsetMax = new Vector2(-4f, -2f);
            return new LabeledButton(item, button, label);
        }

        static void PrepareRailButton(LabeledButton button)
        {
            var element = button.Root.AddComponent<LayoutElement>();
            element.minHeight = 64f;
            element.preferredHeight = 64f;
        }

        static void AddHeight(Text text, float height)
        {
            var element = text.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        static StringTable RequireTable(StringTableCollection collection, UnityEngine.Localization.Locale locale)
        {
            foreach (var table in collection.StringTables)
            {
                if (table != null && table.LocaleIdentifier.Code == locale.Identifier.Code)
                    return table;
            }

            var created = collection.AddNewTable(locale.Identifier) as StringTable;
            if (created == null)
                throw new InvalidOperationException("Could not create a map string table for " + locale.Identifier.Code);

            return created;
        }

        static void SetEntry(StringTable table, string key, string value)
        {
            var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
            entry.Value = value;
            entry.IsSmart = false;
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(table.SharedData);
        }

        static void TryPreload(StringTable table)
        {
            try
            {
                LocalizationEditorSettings.SetPreloadTableFlag(table, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LogistiCore] Could not preload " + table.name + ". " + exception.Message);
            }
        }

        static Dictionary<string, SeedRow> BuildSeed()
        {
            return new Dictionary<string, SeedRow>
            {
                { MapContent.Title, Row("Map", "Harita", "Karte") },
                { MapContent.ViewWorld, Row("World", "Dünya", "Welt") },
                { MapContent.ViewTurkey, Row("Turkey", "Türkiye", "Türkei") },
                { MapContent.FocusWorld, Row("World", "Dünya", "Welt") },
                { MapContent.FocusTurkey, Row("Turkey", "Türkiye", "Türkei") },
                { MapContent.ZoomIn, Row("+", "+", "+") },
                { MapContent.ZoomOut, Row("\u2212", "\u2212", "\u2212") },
                { MapContent.Restore, Row("Reset", "Sıfırla", "Zurück") },
                { MapContent.Collapse, Row("Hide", "Gizle", "Zuklappen") },
                { MapContent.Expand, Row("Show", "Göster", "Aufklappen") },
                { MapContent.InspectorTitle, Row("Geography", "Coğrafya", "Geografie") },
                { MapContent.InspectorScope, Row("Scope", "Kapsam", "Ausschnitt") },
                { MapContent.InspectorCity, Row("City", "Şehir", "Stadt") },
                { MapContent.InspectorCoordinates, Row("Coordinates", "Koordinatlar", "Koordinaten") },
                { MapContent.SelectCity, Row("Select a city", "Bir şehir seçin", "Stadt auswählen") },
                { "cities.izmir", Row("Izmir", "İzmir", "Izmir") },
                { "cities.istanbul", Row("Istanbul", "İstanbul", "Istanbul") },
                { "cities.ankara", Row("Ankara", "Ankara", "Ankara") },
                { "cities.bursa", Row("Bursa", "Bursa", "Bursa") },
                { "cities.antalya", Row("Antalya", "Antalya", "Antalya") },
                { "cities.adana", Row("Adana", "Adana", "Adana") },
                { "cities.diyarbakir", Row("Diyarbakir", "Diyarbakır", "Diyarbakir") },
                { "cities.trabzon", Row("Trabzon", "Trabzon", "Trabzon") }
            };
        }

        static SeedRow Row(string english, string turkish, string german)
        {
            return new SeedRow(english, turkish, german);
        }

        readonly struct SeedRow
        {
            public readonly string English;
            public readonly string Turkish;
            public readonly string German;

            public SeedRow(string english, string turkish, string german)
            {
                English = english;
                Turkish = turkish;
                German = german;
            }
        }

        readonly struct LabeledButton
        {
            public readonly GameObject Root;
            public readonly Button Button;
            public readonly Text Label;

            public LabeledButton(GameObject root, Button button, Text label)
            {
                Root = root;
                Button = button;
                Label = label;
            }
        }
    }
}
