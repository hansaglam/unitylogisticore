using System.IO;
using System.Security.Cryptography;
using LogistiCore.Backend;
using LogistiCore.EditorTools;
using LogistiCore.Localization;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LogistiCore.Tests
{
    public sealed class MapTopHudTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        const string ProjectSettingsSha = "a9dc1d5a46c849d1a049955afc63dadd6fcab98de7082dccfb656787940b3c4a";

        MapCommandShell _shell;
        GameObject _root;
        string _previousLocale;

        [SetUp]
        public void SetUp()
        {
            MapCompanyEconomyRead.ResetForTests();
            MapJobsStore.ClearOverride();
            MapFleetStore.ClearOverride();
            MapRoadDeliveryStore.ClearOverride();
            MapSceneSetup.EnsureLocalization();
            _previousLocale = LocaleService.CurrentCode;
            LocaleService.TrySelect(LocaleCodes.English);
            _root = new GameObject("TopHudRoot", typeof(RectTransform), typeof(Canvas), typeof(MapCommandShell));
            _shell = _root.GetComponent<MapCommandShell>();
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
        }

        [TearDown]
        public void TearDown()
        {
            MapCompanyEconomyRead.ResetForTests();
            MapOperationStrip.ClearOverride();
            if (_root != null)
                Object.DestroyImmediate(_root);
            _root = null;
            _shell = null;
            if (LocaleService.IsReady)
                LocaleService.TrySelect(LocaleCodes.English);
        }

        [Test]
        public void SingleHeaderInstance_OnRepeatedApply()
        {
            _shell.Apply(1280f, 720f, new Rect(0f, 0f, 1280f, 720f));
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            Assert.AreEqual(1, CountNamed(_shell.transform, "CommandHeader"));
            Assert.AreEqual(1, CountNamed(_shell.transform, MapTopHud.LevelRootName));
            Assert.AreEqual(1, CountNamed(_shell.transform, MapTopHud.BalanceRootName));
            Assert.AreEqual(1, CountNamed(_shell.transform, "Brand"));
            Assert.AreEqual(1, CountNamed(_shell.transform, "RegionChip"));
        }

        [Test]
        public void SignedOut_DoesNotShowBalance()
        {
            MapCompanyStore.TryApplyTrusted(ReadyEconomy("1501000", "LGC", 2, 1, 1));
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            MapCompanyEconomyRead.SignOut();
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            var economy = Find(_shell.transform, "Economy").GetComponent<Text>().text;
            StringAssert.DoesNotContain("15010", economy);
            StringAssert.DoesNotContain("LGC", economy);
            Assert.IsFalse(Find(_shell.transform, "HudReputation").gameObject.activeSelf);
            Assert.IsFalse(Find(_shell.transform, "XpFill").gameObject.activeSelf);
        }

        [Test]
        public void MissingBalance_FailClosed()
        {
            MapCompanyStore.TryApplyTrusted(ReadyEconomy("", "LGC", 2, 1, 1));
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            var economy = Find(_shell.transform, "Economy").GetComponent<Text>().text;
            Assert.AreEqual("—", economy);
            StringAssert.DoesNotContain("₺", economy);
        }

        [Test]
        public void ReadyEconomy_FormatsLgcWithoutInventingXpBar()
        {
            MapCompanyStore.TryApplyTrusted(ReadyEconomy("1501000", "LGC", 2, 1, 1));
            LocaleService.TrySelect(LocaleCodes.English);
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            Assert.AreEqual("15,010.00 LGC", Find(_shell.transform, "Economy").GetComponent<Text>().text);
            StringAssert.Contains("1", Find(_shell.transform, "LevelValue").GetComponent<Text>().text);
            StringAssert.Contains("1", Find(_shell.transform, "Progress").GetComponent<Text>().text);
            Assert.IsFalse(Find(_shell.transform, "XpFill").gameObject.activeSelf);
            Assert.IsFalse(Find(_shell.transform, "HudReputation").gameObject.activeSelf);
            Assert.IsNull(Find(_shell.transform, "HeaderMeta"));
        }

        [Test]
        public void Locale_FormatsBalanceTrEnDe()
        {
            MapCompanyStore.TryApplyTrusted(ReadyEconomy("1501000", "LGC", 2, 4, 2));
            LocaleService.TrySelect(LocaleCodes.English);
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            Assert.AreEqual("15,010.00 LGC", Find(_shell.transform, "Economy").GetComponent<Text>().text);
            LocaleService.TrySelect(LocaleCodes.Turkish);
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            Assert.AreEqual("15.010,00 LGC", Find(_shell.transform, "Economy").GetComponent<Text>().text);
            StringAssert.Contains("Seviye", Find(_shell.transform, "LevelLabel").GetComponent<Text>().text);
            LocaleService.TrySelect(LocaleCodes.German);
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            Assert.AreEqual("15.010,00 LGC", Find(_shell.transform, "Economy").GetComponent<Text>().text);
            StringAssert.Contains("Stufe", Find(_shell.transform, "LevelLabel").GetComponent<Text>().text);
            Assert.IsFalse(Find(_shell.transform, "LevelLabel").GetComponent<Text>().text.Contains("ops.hud"));
        }

        [Test]
        public void UidSwitch_ClearsPreviousEconomy()
        {
            MapCompanyStore.TryApplyTrusted(ReadyEconomy("1501000", "LGC", 2, 1, 1));
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            MapCompanyEconomyRead.HandleAuth("next-uid", default, new ClientReadSession());
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            StringAssert.DoesNotContain("15010", Find(_shell.transform, "Economy").GetComponent<Text>().text);
        }

        [Test]
        public void Layouts_DoNotOverlapPriorityClusters()
        {
            MapCompanyStore.TryApplyTrusted(ReadyEconomy("1501000", "LGC", 2, 1, 1));
            var frames = new[]
            {
                new Vector4(1920f, 1080f, 0f, 0f),
                new Vector4(1280f, 720f, 0f, 0f),
                new Vector4(844f, 390f, 0f, 0f),
                new Vector4(2868f, 1320f, 186f, 60f)
            };
            var safes = new[]
            {
                new Rect(0f, 0f, 1920f, 1080f),
                new Rect(0f, 0f, 1280f, 720f),
                new Rect(0f, 0f, 844f, 390f),
                new Rect(186f, 60f, 2496f, 1200f)
            };
            for (var i = 0; i < frames.Length; i++)
            {
                _shell.Apply(frames[i].x, frames[i].y, safes[i]);
                Canvas.ForceUpdateCanvases();
                Assert.IsFalse(_shell.RegionChipOverlapsBrand(), frames[i].ToString());
                var core = Find(_shell.transform, "BrandCore") as RectTransform;
                var chip = Find(_shell.transform, "RegionChip") as RectTransform;
                var level = Find(_shell.transform, MapTopHud.LevelRootName) as RectTransform;
                var balance = Find(_shell.transform, MapTopHud.BalanceRootName) as RectTransform;
                Assert.IsFalse(MapTopHud.Overlaps(core, chip), "core/chip " + frames[i]);
                if (level.gameObject.activeInHierarchy)
                {
                    Assert.IsFalse(MapTopHud.Overlaps(chip, level), "chip/level " + frames[i]);
                    Assert.IsFalse(MapTopHud.Overlaps(level, balance), "level/balance " + frames[i]);
                }

                Assert.IsFalse(MapTopHud.Overlaps(chip, balance), "chip/balance " + frames[i]);
                var header = Find(_shell.transform, "CommandHeader") as RectTransform;
                var canvas = _shell.GetComponent<Canvas>();
                var scale = MapHudLayout.CanvasScale(frames[i].x, frames[i].y);
                var metrics = MapCommandLayout.Evaluate(frames[i].x, frames[i].y, safes[i], false);
                var trailing = MapTopHud.MeasureTrailing(header, metrics, scale, canvas);
                var leftPad = safes[i].xMin / Mathf.Max(0.01f, scale);
                Assert.GreaterOrEqual(balance.anchoredPosition.x, leftPad - 1f, frames[i].ToString());
                Assert.LessOrEqual(
                    balance.anchoredPosition.x + balance.sizeDelta.x,
                    header.rect.width - trailing + 2f,
                    frames[i].ToString());
                var amount = Find(_shell.transform, "Economy").GetComponent<Text>();
                Assert.GreaterOrEqual(amount.rectTransform.sizeDelta.x, amount.preferredWidth - 1f, frames[i].ToString());
                Assert.GreaterOrEqual(amount.fontSize, MapTopHud.MinReadableFont, frames[i].ToString());
            }
        }

        [Test]
        public void Balance_StaysLeftOfLocaleChromeAndKeepsFullAmount()
        {
            MapCompanyStore.TryApplyTrusted(ReadyEconomy("1501000", "LGC", 2, 1, 1));
            LocaleService.TrySelect(LocaleCodes.Turkish);
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            var locale = CreateLocaleChrome(Find(_shell.transform, "CommandHeader"));
            _shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            Canvas.ForceUpdateCanvases();
            var balance = Find(_shell.transform, MapTopHud.BalanceRootName) as RectTransform;
            var amount = Find(_shell.transform, "Economy").GetComponent<Text>();
            Assert.AreEqual("15.010,00 LGC", amount.text);
            Assert.AreEqual(HorizontalWrapMode.Overflow, amount.horizontalOverflow);
            Assert.GreaterOrEqual(amount.rectTransform.sizeDelta.x, amount.preferredWidth - 1f);
            Assert.IsFalse(MapTopHud.Overlaps(balance, locale));
            var header = Find(_shell.transform, "CommandHeader") as RectTransform;
            Assert.LessOrEqual(
                balance.anchoredPosition.x + balance.sizeDelta.x,
                header.rect.width - locale.sizeDelta.x + 1f);
            Assert.GreaterOrEqual(Find(_shell.transform, "LevelValue").GetComponent<Text>().fontSize, MapTopHud.MinReadableFont);
        }

        [Test]
        public void LongBalance_StaysReadableWithoutEllipsis()
        {
            MapCompanyStore.TryApplyTrusted(ReadyEconomy("150100000", "LGC", 2, 1, 1));
            LocaleService.TrySelect(LocaleCodes.Turkish);
            _shell.Apply(844f, 390f, new Rect(0f, 0f, 844f, 390f));
            CreateLocaleChrome(Find(_shell.transform, "CommandHeader"));
            _shell.Apply(844f, 390f, new Rect(0f, 0f, 844f, 390f));
            Canvas.ForceUpdateCanvases();
            var amount = Find(_shell.transform, "Economy").GetComponent<Text>();
            Assert.AreEqual("1.501.000,00 LGC", amount.text);
            Assert.AreEqual(HorizontalWrapMode.Overflow, amount.horizontalOverflow);
            Assert.GreaterOrEqual(amount.rectTransform.sizeDelta.x, amount.preferredWidth - 1f);
            Assert.GreaterOrEqual(amount.fontSize, MapTopHud.MinReadableFont);
            var level = Find(_shell.transform, MapTopHud.LevelRootName);
            if (level == null || !level.gameObject.activeInHierarchy)
            {
                Assert.IsFalse(Find(_shell.transform, MapTopHud.RuleName).gameObject.activeInHierarchy);
                var badge = Find(_shell.transform, "LevelBadge");
                Assert.IsTrue(badge == null || !badge.gameObject.activeInHierarchy);
            }
            else
            {
                Assert.IsTrue(Find(level, "LevelLabel").gameObject.activeSelf || Find(level, "Progress").gameObject.activeSelf);
            }
        }

        [Test]
        public void CompactHud_HidesLevelGroupWithoutLeftoverBadge()
        {
            MapCompanyStore.TryApplyTrusted(ReadyEconomy("1501000", "LGC", 2, 1, 1));
            LocaleService.TrySelect(LocaleCodes.Turkish);
            CreateLocaleChrome(Find(_shell.transform, "CommandHeader"));
            _shell.Apply(844f, 390f, new Rect(0f, 0f, 844f, 390f));
            Canvas.ForceUpdateCanvases();
            var level = Find(_shell.transform, MapTopHud.LevelRootName);
            var rule = Find(_shell.transform, MapTopHud.RuleName);
            var badge = Find(_shell.transform, "LevelBadge");
            var balance = Find(_shell.transform, MapTopHud.BalanceRootName);
            Assert.IsTrue(balance.gameObject.activeInHierarchy);
            if (!level.gameObject.activeInHierarchy)
            {
                Assert.IsFalse(rule.gameObject.activeInHierarchy);
                Assert.IsFalse(badge.gameObject.activeInHierarchy);
                Assert.IsFalse(Find(_shell.transform, "LevelValue").gameObject.activeInHierarchy);
            }
            else
            {
                Assert.IsTrue(Find(level, "LevelLabel").gameObject.activeSelf || Find(level, "Progress").gameObject.activeSelf);
                Assert.IsFalse(MapTopHud.Overlaps(level as RectTransform, balance as RectTransform));
            }
        }

        [Test]
        public void ProtectedFiles_UnchangedAndNoFakeChrome()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            Assert.AreEqual(ProjectSettingsSha, Sha(ProjectSettingsPath));
            Assert.IsNull(Find(_shell.transform, "BrandMark"));
            Assert.IsNull(Find(_shell.transform, "HudNotify"));
            Assert.IsNull(Find(_shell.transform, "HudClock"));
            Assert.IsFalse(ClientCommandAuthorization.AllowsLiveFunctions(null, default));
        }

        [Test]
        public void SceneHeader_KeepsRailAndSingleShell()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var shell = MapCommandShell.Attach(scene);
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                Assert.AreEqual(1, Count<MapCommandShell>(scene));
                Assert.AreEqual(1, CountNamed(shell.transform, "CommandHeader"));
                Assert.AreEqual(1, CountNamed(shell.transform, "ModuleRail"));
                Assert.IsNotNull(Find(shell.transform, "Mode0"));
                shell.ShowModule(0);
                Assert.IsTrue(shell.ManagementOpen);
                Assert.AreEqual(MapModule.Fleet, shell.ActiveModule);
                shell.ShowModule(1);
                Assert.AreEqual(MapModule.Jobs, shell.ActiveModule);
                shell.CloseContext();
                Assert.AreEqual(SceneSha, Sha(ScenePath));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static RectTransform CreateLocaleChrome(Transform header)
        {
            var item = new GameObject("LocaleMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(header, false);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(140f, 32f);
            rect.anchoredPosition = Vector2.zero;
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.text = "Turkish";
            text.alignment = TextAnchor.MiddleRight;
            text.raycastTarget = false;
            return rect;
        }

        static ClientEconomyState ReadyEconomy(string balance, string currency, int scale, int xp, int level)
        {
            return new ClientEconomyState(
                true,
                true,
                "co_test",
                balance,
                currency,
                scale,
                xp,
                level,
                "cfg",
                "rules");
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        static int CountNamed(Transform root, string name)
        {
            var count = 0;
            var items = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].name == name)
                    count++;
            }

            return count;
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
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var text = "";
                for (var i = 0; i < hash.Length; i++)
                    text += hash[i].ToString("x2");
                return text;
            }
        }
    }
}
