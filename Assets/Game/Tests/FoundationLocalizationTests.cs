using LogistiCore.EditorTools;
using LogistiCore.Localization;
using LogistiCore.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;

namespace LogistiCore.Tests
{
    public class FoundationLocalizationTests
    {
        [Test]
        public void LocalesAndVerificationStrings_ExistForEnglishTurkishAndGerman()
        {
            FoundationSetup.EnsureLocalization();

            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationContent.Table);
            Assert.IsNotNull(collection);

            foreach (var key in LocalizationContent.Keys)
            {
                foreach (var table in collection.StringTables)
                {
                    var entry = table.GetEntry(key);
                    Assert.IsNotNull(entry, key + " missing for " + table.LocaleIdentifier.Code);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Value), key);
                    Assert.AreEqual(LocalizationContent.IsSmart(key), entry.IsSmart, key);
                }
            }

            var english = Value(collection, "en", LocalizationContent.Body);
            var turkish = Value(collection, "tr", LocalizationContent.Body);
            var german = Value(collection, "de", LocalizationContent.Body);
            Assert.AreNotEqual(english, turkish);
            Assert.AreNotEqual(english, german);
            Assert.AreNotEqual(turkish, german);
            Assert.That(Value(collection, "tr", LocalizationContent.Greeting), Does.Contain("Hoş geldin"));
            Assert.That(Value(collection, "de", LocalizationContent.Greeting), Does.Contain("Willkommen"));
            Assert.That(Value(collection, "de", LocalizationContent.Deliveries), Does.Contain("Lieferung"));
            Assert.That(Value(collection, "en", LocalizationContent.Greeting), Does.Contain("{player}"));
        }

        [Test]
        public void StartupSelectors_PreferSavedLocaleThenDeviceThenEnglish()
        {
            FoundationSetup.EnsureLocalization();

            var selectors = LocalizationEditorSettings.ActiveLocalizationSettings.GetStartupLocaleSelectors();
            Assert.AreEqual(4, selectors.Count);
            Assert.IsInstanceOf<CommandLineLocaleSelector>(selectors[0]);
            Assert.IsInstanceOf<PlayerPrefLocaleSelector>(selectors[1]);
            Assert.AreEqual(LocaleService.PreferenceKey, ((PlayerPrefLocaleSelector)selectors[1]).PlayerPreferenceKey);
            Assert.IsInstanceOf<SystemLocaleSelector>(selectors[2]);
            Assert.IsInstanceOf<SpecificLocaleSelector>(selectors[3]);
            Assert.AreEqual(LocaleCodes.English, ((SpecificLocaleSelector)selectors[3]).LocaleId.Code);

            var turkish = LocalizationEditorSettings.GetLocale(LocaleCodes.Turkish);
            var german = LocalizationEditorSettings.GetLocale(LocaleCodes.German);
            var english = LocalizationEditorSettings.GetLocale(LocaleCodes.English);
            Assert.AreEqual(LocaleCodes.English, turkish.Metadata.GetMetadata<FallbackLocale>().Locale.Identifier.Code);
            Assert.AreEqual(LocaleCodes.English, german.Metadata.GetMetadata<FallbackLocale>().Locale.Identifier.Code);
            Assert.IsNull(english.Metadata.GetMetadata<FallbackLocale>());
            Assert.IsNull(LocalizationEditorSettings.GetLocale("fr"));
        }

        [Test]
        public void VerificationScene_ContainsTheFoundationCheck()
        {
            FoundationSetup.EnsureAll(openScene: false);

            var scene = EditorSceneManager.OpenScene(FoundationSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                FoundationVerificationView view = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    view = root.GetComponentInChildren<FoundationVerificationView>(true);
                    if (view != null)
                        break;
                }

                Assert.IsNotNull(view);
                var serialized = new SerializedObject(view);
                Assert.IsNotNull(serialized.FindProperty("bodyText").objectReferenceValue);
                Assert.IsNotNull(serialized.FindProperty("englishButton").objectReferenceValue);
                Assert.IsNotNull(serialized.FindProperty("landscapeButton").objectReferenceValue);
                Assert.IsNotNull(view.GetComponent<SafeAreaRect>());
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static string Value(StringTableCollection collection, string code, string key)
        {
            foreach (var table in collection.StringTables)
            {
                if (table.LocaleIdentifier.Code == code)
                    return table.GetEntry(key).Value;
            }

            Assert.Fail("Missing table " + code);
            return null;
        }
    }
}
