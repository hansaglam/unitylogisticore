using System;
using System.Collections.Generic;
using System.IO;
using LogistiCore.Localization;
using LogistiCore.UI;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.EditorTools
{
    /// <summary>
    /// Idempotent localization and verification-scene setup.
    /// Menu: LogistiCore/Foundation/Setup.
    /// Editor startup does not create, delete, or replace the verification scene.
    /// Re-running setup updates the foundation strings and leaves an existing scene in place.
    /// LogistiCore/Foundation/Recreate Verification Scene replaces that scene only after a snapshot
    /// and a successful temporary save.
    /// </summary>
    public static class FoundationSetup
    {
        public const string ScenePath = "Assets/Game/Scenes/FoundationVerification.unity";
        public const string PendingScenePath = "Assets/Game/Scenes/FoundationVerification.pending.unity";

        const string SettingsPath = "Assets/Game/Localization/Localization Settings.asset";
        const string LocalesFolder = "Assets/Game/Localization/Locales";
        const string TablesFolder = "Assets/Game/Localization/Tables";
        const string ViewScriptPath = "Assets/Game/UI/FoundationVerificationView.cs";
        const string SafeAreaScriptPath = "Assets/Game/UI/SafeAreaRect.cs";

        static string _sceneSavePath = ScenePath;

        static readonly Color Navy = new Color(0.043f, 0.090f, 0.161f, 1f);
        static readonly Color NavyButton = new Color(0.122f, 0.220f, 0.337f, 1f);
        static readonly Color Ink = new Color(0.925f, 0.945f, 0.925f, 1f);

        [MenuItem("LogistiCore/Foundation/Setup")]
        public static void SetupFromMenu()
        {
            EnsureAll(openScene: true);
        }

        [MenuItem("LogistiCore/Foundation/Recreate Verification Scene")]
        public static void RecreateFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[LogistiCore] Exit Play mode before recreating the verification scene.");
                return;
            }

            EnsureScene(replaceExisting: true);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static bool IsLocalizationReady
        {
            get
            {
                if (LocalizationEditorSettings.ActiveLocalizationSettings == null)
                    return false;
                if (LocalizationEditorSettings.GetLocale(LocaleCodes.English) == null)
                    return false;
                if (LocalizationEditorSettings.GetLocale(LocaleCodes.Turkish) == null)
                    return false;
                if (LocalizationEditorSettings.GetLocale(LocaleCodes.German) == null)
                    return false;

                return LocalizationEditorSettings.GetStringTableCollection(LocalizationContent.Table) != null;
            }
        }

        public static bool IsVerificationSceneReady =>
            AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;

        public static void EnsureAll(bool openScene)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[LogistiCore] Exit Play mode before running foundation setup.");
                return;
            }

            EnsureLocalization();
            EnsureScene(replaceExisting: false);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            if (openScene)
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void EnsureLocalization()
        {
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            var english = EnsureLocale(LocaleCodes.English);
            var turkish = EnsureLocale(LocaleCodes.Turkish);
            var german = EnsureLocale(LocaleCodes.German);
            ConfigureFallback(turkish, english);
            ConfigureFallback(german, english);
            ClearFallback(english);
            ConfigureSelectors();

            var collection = EnsureCollection();
            WriteEntries(collection, english, turkish, german);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
        }

        public enum FoundationSceneFileAction
        {
            None = 0,
            CreateMissing = 1,
            ReplaceWithBackup = 2
        }

        /// <summary>
        /// Editor startup never creates or replaces the verification scene.
        /// An explicit setup request leaves an existing scene alone and creates one only when the file is missing.
        /// An explicit recreate request replaces an existing file only after a snapshot.
        /// </summary>
        public static FoundationSceneFileAction FileActionFor(bool explicitRequest, bool sceneExists, bool replaceExisting)
        {
            if (!explicitRequest || (sceneExists && !replaceExisting))
                return FoundationSceneFileAction.None;

            return sceneExists
                ? FoundationSceneFileAction.ReplaceWithBackup
                : FoundationSceneFileAction.CreateMissing;
        }

        static void EnsureScene(bool replaceExisting)
        {
            var exists = SceneAssetExists();
            var action = FileActionFor(explicitRequest: true, exists, replaceExisting);
            if (action == FoundationSceneFileAction.None)
                return;

            ValidatePrerequisites();

            string snapshot = null;
            if (action == FoundationSceneFileAction.ReplaceWithBackup)
                snapshot = BackupExistingScene();

            var savePath = action == FoundationSceneFileAction.ReplaceWithBackup ? PendingScenePath : ScenePath;
            _sceneSavePath = savePath;
            var replaced = false;
            try
            {
                CreateVerificationScene();
                var saved = AbsoluteAsset(savePath);
                if (!File.Exists(saved) || new FileInfo(saved).Length < 64)
                    throw new InvalidOperationException("The verification scene was not saved.");

                if (action == FoundationSceneFileAction.ReplaceWithBackup)
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
                throw new InvalidOperationException("Exit Play mode before creating the verification scene.");

            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !Application.isBatchMode)
                throw new InvalidOperationException("Save the open scene, then run LogistiCore/Foundation/Recreate Verification Scene.");

            if (AssetDatabase.LoadAssetAtPath<MonoScript>(ViewScriptPath) == null ||
                AssetDatabase.LoadAssetAtPath<MonoScript>(SafeAreaScriptPath) == null)
                throw new InvalidOperationException("Foundation verification scripts are missing.");
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
                throw new InvalidOperationException("The verification scene disappeared before it could be backed up.");

            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LogistiCoreFoundationSceneSnapshots",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            var sceneCopy = Path.Combine(directory, "FoundationVerification.unity");
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

            var metaCopy = Path.Combine(snapshotDirectory, "FoundationVerification.unity.meta");
            if (!File.Exists(metaCopy))
                return;

            var expected = ReadGuid(metaCopy);
            var meta = AbsoluteAsset(ScenePath) + ".meta";
            if (expected != null && ReadGuid(meta) != expected)
                File.Copy(metaCopy, meta, true);
        }

        static void RestoreSnapshot(string directory)
        {
            var sceneCopy = Path.Combine(directory, "FoundationVerification.unity");
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

        static LocalizationSettings EnsureSettings()
        {
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings != null)
                return settings;

            EnsureFolder("Assets/Game/Localization");
            settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            return settings;
        }

        static Locale EnsureLocale(string code)
        {
            var existing = LocalizationEditorSettings.GetLocale(code);
            if (existing != null)
                return existing;

            EnsureFolder(LocalesFolder);
            var path = LocalesFolder + "/" + code + ".asset";
            var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null)
            {
                locale = Locale.CreateLocale(new LocaleIdentifier(code));
                locale.name = code;
                AssetDatabase.CreateAsset(locale, path);
            }

            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        static void ConfigureFallback(Locale locale, Locale english)
        {
            var fallback = locale.Metadata.GetMetadata<FallbackLocale>();
            if (fallback == null)
            {
                fallback = new FallbackLocale();
                locale.Metadata.AddMetadata(fallback);
            }

            fallback.Locale = english;
            EditorUtility.SetDirty(locale);
        }

        static void ClearFallback(Locale english)
        {
            var fallback = english.Metadata.GetMetadata<FallbackLocale>();
            if (fallback == null)
                return;

            english.Metadata.RemoveMetadata(fallback);
            EditorUtility.SetDirty(english);
        }

        static void ConfigureSelectors()
        {
            var settings = EnsureSettings();
            var selectors = settings.GetStartupLocaleSelectors();
            selectors.Clear();
            selectors.Add(new CommandLineLocaleSelector());
            selectors.Add(new PlayerPrefLocaleSelector { PlayerPreferenceKey = LocaleService.PreferenceKey });
            selectors.Add(new SystemLocaleSelector());
            selectors.Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier(LocaleCodes.English) });
            EditorUtility.SetDirty(settings);
        }

        static StringTableCollection EnsureCollection()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationContent.Table);
            if (collection != null)
                return collection;

            EnsureFolder(TablesFolder);
            return LocalizationEditorSettings.CreateStringTableCollection(LocalizationContent.Table, TablesFolder);
        }

        static void WriteEntries(StringTableCollection collection, Locale english, Locale turkish, Locale german)
        {
            var seed = BuildSeed();
            var englishTable = RequireTable(collection, english);
            var turkishTable = RequireTable(collection, turkish);
            var germanTable = RequireTable(collection, german);

            for (var i = 0; i < LocalizationContent.Keys.Length; i++)
            {
                var key = LocalizationContent.Keys[i];
                if (!seed.TryGetValue(key, out var row))
                    throw new InvalidOperationException("Missing foundation seed for " + key);

                var smart = LocalizationContent.IsSmart(key);
                SetEntry(englishTable, key, row.English, smart);
                SetEntry(turkishTable, key, row.Turkish, smart);
                SetEntry(germanTable, key, row.German, smart);
            }

            TryPreload(englishTable);
            TryPreload(turkishTable);
            TryPreload(germanTable);
        }

        static StringTable RequireTable(StringTableCollection collection, Locale locale)
        {
            var table = FindTable(collection, locale.Identifier.Code);
            if (table != null)
                return table;

            table = collection.AddNewTable(locale.Identifier) as StringTable;
            if (table == null)
                throw new InvalidOperationException("Could not create a string table for " + locale.Identifier.Code);

            return table;
        }

        static StringTable FindTable(StringTableCollection collection, string code)
        {
            foreach (var table in collection.StringTables)
            {
                if (table != null && table.LocaleIdentifier.Code == code)
                    return table;
            }

            return null;
        }

        static void SetEntry(StringTable table, string key, string value, bool smart)
        {
            var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
            entry.Value = value;
            entry.IsSmart = smart;
            EditorUtility.SetDirty(table);
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
                { LocalizationContent.Title, Row("LogistiCore", "LogistiCore", "LogistiCore") },
                { LocalizationContent.Body, Row(
                    "Foundation check. Language, format, and screen layout.",
                    "Temel kontrol. Dil, biçim ve ekran düzeni.",
                    "Grundprüfung. Sprache, Format und Bildschirmausrichtung.") },
                { LocalizationContent.Greeting, Row(
                    "Welcome, {player}.",
                    "Hoş geldin, {player}.",
                    "Willkommen, {player}.") },
                { LocalizationContent.Deliveries, Row(
                    "{count:plural:{} delivery|{} deliveries}",
                    "{count:plural:{} teslimat|{} teslimat}",
                    "{count:plural:{} Lieferung|{} Lieferungen}") },
                { LocalizationContent.ActiveLocale, Row(
                    "Locale: {code}",
                    "Dil: {code}",
                    "Sprache: {code}") },
                { LocalizationContent.LabelNumber, Row("Number", "Sayı", "Zahl") },
                { LocalizationContent.LabelCurrency, Row("Currency", "Para birimi", "Währung") },
                { LocalizationContent.LabelPercent, Row("Percent", "Yüzde", "Prozent") },
                { LocalizationContent.LabelDistance, Row("Distance", "Mesafe", "Entfernung") },
                { LocalizationContent.LabelWeight, Row("Weight", "Ağırlık", "Gewicht") },
                { LocalizationContent.LabelDuration, Row("Duration", "Süre", "Dauer") },
                { LocalizationContent.LabelDateTime, Row("Date", "Tarih", "Datum") },
                { LocalizationContent.Portrait, Row("Portrait", "Dikey", "Hochformat") },
                { LocalizationContent.Landscape, Row("Landscape", "Yatay", "Querformat") },
                { LocalizationContent.LanguageEnglish, Row("English", "English", "English") },
                { LocalizationContent.LanguageTurkish, Row("Türkçe", "Türkçe", "Türkçe") },
                { LocalizationContent.LanguageGerman, Row("Deutsch", "Deutsch", "Deutsch") }
            };
        }

        static SeedRow Row(string english, string turkish, string german)
        {
            return new SeedRow(english, turkish, german);
        }

        static void CreateVerificationScene()
        {
            EnsureFolder("Assets/Game/Scenes");
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                if (!Application.isBatchMode)
                    throw new InvalidOperationException("Save the open scene, then run LogistiCore/Foundation/Recreate Verification Scene.");

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Navy;
            camera.orthographic = true;
            if (cameraObject.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.AddComponent<AudioListener>();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var column = CreateUiObject("SafeArea", canvasObject.transform);
            column.AddComponent<SafeAreaRect>();
            var background = column.AddComponent<Image>();
            background.color = Navy;
            background.raycastTarget = false;
            Stretch(column.GetComponent<RectTransform>());
            var layout = column.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 48, 48);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var font = BuiltinFont();
            var title = CreateText("Title", column.transform, font, 64, FontStyle.Bold);
            var body = CreateText("Body", column.transform, font, 36, FontStyle.Normal);
            var languageRow = CreateRow("Languages", column.transform);
            var english = CreateButton("English", languageRow, font);
            var turkish = CreateButton("Turkish", languageRow, font);
            var german = CreateButton("German", languageRow, font);
            var layoutRow = CreateRow("Layouts", column.transform);
            var portrait = CreateButton("Portrait", layoutRow, font);
            var landscape = CreateButton("Landscape", layoutRow, font);

            var view = column.AddComponent<FoundationVerificationView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("bodyText").objectReferenceValue = body;
            serialized.FindProperty("englishLabel").objectReferenceValue = english.Label;
            serialized.FindProperty("turkishLabel").objectReferenceValue = turkish.Label;
            serialized.FindProperty("germanLabel").objectReferenceValue = german.Label;
            serialized.FindProperty("portraitLabel").objectReferenceValue = portrait.Label;
            serialized.FindProperty("landscapeLabel").objectReferenceValue = landscape.Label;
            serialized.FindProperty("englishButton").objectReferenceValue = english.Button;
            serialized.FindProperty("turkishButton").objectReferenceValue = turkish.Button;
            serialized.FindProperty("germanButton").objectReferenceValue = german.Button;
            serialized.FindProperty("portraitButton").objectReferenceValue = portrait.Button;
            serialized.FindProperty("landscapeButton").objectReferenceValue = landscape.Button;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, _sceneSavePath))
                throw new InvalidOperationException("The verification scene was not saved.");

            if (scene.IsValid() && SceneManager.sceneCount > 1)
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

        static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            return item;
        }

        static Text CreateText(string name, Transform parent, Font font, int size, FontStyle style)
        {
            var item = CreateUiObject(name, parent);
            var text = item.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Ink;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        static Transform CreateRow(string name, Transform parent)
        {
            var item = CreateUiObject(name, parent);
            var row = item.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 16f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = false;
            var element = item.AddComponent<LayoutElement>();
            element.minHeight = 96f;
            element.preferredHeight = 96f;
            return item.transform;
        }

        static LabeledButton CreateButton(string name, Transform parent, Font font)
        {
            var item = CreateUiObject(name, parent);
            var image = item.AddComponent<Image>();
            image.color = NavyButton;
            var button = item.AddComponent<Button>();
            button.targetGraphic = image;
            var element = item.AddComponent<LayoutElement>();
            element.minHeight = 96f;
            var label = CreateText("Label", item.transform, font, 32, FontStyle.Bold);
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch(label.rectTransform);
            return new LabeledButton(button, label);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var slash = path.LastIndexOf('/');
            var parent = path.Substring(0, slash);
            var name = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
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
            public readonly Button Button;
            public readonly Text Label;

            public LabeledButton(Button button, Text label)
            {
                Button = button;
                Label = label;
            }
        }
    }
}
