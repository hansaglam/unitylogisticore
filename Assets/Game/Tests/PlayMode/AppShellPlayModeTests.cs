using System.Collections;
using LogistiCore.Map;
using LogistiCore.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LogistiCore.Tests
{
    public sealed class AppShellPlayModeTests
    {
        int _eventSystemWarnings;

        [UnityTest]
        public IEnumerator HomeAndMap_TenCyclesKeepOneEventSystemAndTheRequestedLayout()
        {
            _eventSystemWarnings = 0;
            Application.logMessageReceived += OnLog;
            try
            {
                ReleaseEventSystems();
                yield return SceneManager.LoadSceneAsync(AppNavigation.HomeSceneName, LoadSceneMode.Single);
                yield return null;
                yield return null;

                // The bootstrap scene can overlap this first load. Product transitions
                // go through AppNavigation, which is what the cycles below exercise.
                _eventSystemWarnings = 0;
                AssertSettled(AppNavigation.HomeSceneName, ScreenLayoutMode.Portrait);

                for (var i = 0; i < 10; i++)
                {
                    var home = FindInActiveScene<HomeShell>();
                    Assert.IsNotNull(home, "Cycle " + i);
                    Assert.AreEqual(1, CountInActiveScene<HomeShell>());
                    Assert.IsNull(FindInActiveScene<MapHomeReturn>());
                    Assert.IsNull(FindInActiveScene<MapCommandShell>());
                    Assert.IsNotNull(home.MapButton);
                    Assert.IsTrue(home.MapButton.interactable);
                    yield return WaitForLabel(home.MapButton, "World Map", "Dünya Haritası", "Weltkarte");

                    home.MapButton.onClick.Invoke();
                    yield return null;
                    yield return null;

                    AssertSettled(AppNavigation.MapSceneName, ScreenLayoutMode.Landscape);
                    Assert.AreEqual(1, CountInActiveScene<MapCameraRig>());
                    Assert.AreEqual(0, CountInActiveScene<HomeShell>());
                    var back = FindInActiveScene<MapCommandShell>();
                    Assert.IsNotNull(back, "MapCommandShell missing on cycle " + i);
                    Assert.AreEqual(1, CountInActiveScene<MapCommandShell>());
                    Assert.AreEqual(0, CountInActiveScene<MapHomeReturn>());
                    Assert.IsTrue(back.gameObject.activeInHierarchy);
                    Assert.IsNotNull(back.HomeButton);
                    Assert.AreEqual(MapCommandShell.PanelState.Closed, back.State);
                    Assert.IsFalse(back.ContextOpen);
                    Assert.IsTrue(back.HomeButton.interactable);

                    if (i == 0)
                    {
                        back.Apply(1320f, 2868f, new Rect(0f, 0f, 1320f, 2868f));
                        Assert.IsTrue(back.gameObject.activeInHierarchy);
                        back.Apply(2868f, 1320f, new Rect(186f, 60f, 2496f, 1200f));
                        Assert.Greater(back.Viewport.width, 400f);
                        Assert.IsTrue(back.HomeButton.interactable);
                        Assert.GreaterOrEqual(back.HomeButton.GetComponent<RectTransform>().rect.height, 28f);
                    }

                    yield return WaitForLabel(back.HomeButton, "Home", "Ana Sayfa", "Startseite");
                    var homeLabel = back.HomeButton.transform.Find("Label").GetComponent<Text>();
                    var homeMark = back.HomeButton.transform.Find("Mark").GetComponent<Text>();
                    Assert.AreEqual(0f, homeLabel.color.a, 0.001f);
                    Assert.LessOrEqual(homeLabel.rectTransform.rect.width, 1f);
                    Assert.AreEqual("\u2190", homeMark.text);
                    Assert.Greater(homeMark.color.a, 0.5f);
                    if (i == 0)
                    {
                        Button mapRow = null;
                        var rows = back.GetComponentsInChildren<Button>(true);
                        for (var r = 0; r < rows.Length; r++)
                        {
                            if (rows[r].name == "CurrentMode")
                                mapRow = rows[r];
                        }

                        Assert.IsNotNull(mapRow);
                        yield return WaitForLabel(mapRow, "MAP", "HARİTA", "KARTE");
                        back.ShowModule(0);
                        yield return null;
                        Assert.AreEqual(MapCommandShell.PanelState.Module, back.State);
                        var bodyReady = 0f;
                        while (bodyReady < 8f && string.IsNullOrEmpty(back.ModuleBody))
                        {
                            bodyReady += Time.unscaledDeltaTime;
                            yield return null;
                        }

                        Assert.That(
                            back.ModuleBody,
                            Is.EqualTo("This section is not available yet.")
                                .Or.EqualTo("Bu bölüm henüz kullanıma hazır değil.")
                                .Or.EqualTo("Dieser Bereich ist noch nicht verfügbar."));
                        back.CloseContext();
                        Assert.AreEqual(MapCommandShell.PanelState.Closed, back.State);
                        Assert.IsFalse(back.ContextOpen);
                    }

                    back.HomeButton.onClick.Invoke();
                    yield return null;
                    yield return null;

                    AssertSettled(AppNavigation.HomeSceneName, ScreenLayoutMode.Portrait);
                    Assert.AreEqual(1, CountInActiveScene<HomeShell>());
                    Assert.IsNull(FindInActiveScene<MapHomeReturn>());
                    Assert.IsNull(FindInActiveScene<MapCommandShell>());
                    Assert.AreEqual(0, CountInActiveScene<MapCameraRig>());
                }

                Assert.AreEqual(0, _eventSystemWarnings, "EventSystem overlap warning during navigation.");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
        }

        [UnityTest]
        public IEnumerator HomeAndCompany_TenCyclesKeepOneEventSystemAndPortrait()
        {
            _eventSystemWarnings = 0;
            Application.logMessageReceived += OnLog;
            try
            {
                ReleaseEventSystems();
                yield return SceneManager.LoadSceneAsync(AppNavigation.HomeSceneName, LoadSceneMode.Single);
                yield return null;
                yield return null;
                _eventSystemWarnings = 0;

                for (var i = 0; i < 10; i++)
                {
                    var home = FindInActiveScene<HomeShell>();
                    Assert.IsNotNull(home, "Cycle " + i);
                    Assert.AreEqual(1, CountInActiveScene<HomeShell>());
                    Assert.AreEqual(2, home.ButtonCount);
                    Assert.IsNotNull(home.CompanyButton);
                    Assert.IsTrue(home.CompanyButton.interactable);
                    Assert.IsTrue(home.MapButton.interactable);
                    yield return WaitForLabel(home.CompanyButton, "Company", "Şirket", "Unternehmen");

                    home.CompanyButton.onClick.Invoke();
                    yield return null;
                    yield return null;

                    AssertSettled(AppNavigation.CompanySceneName, ScreenLayoutMode.Portrait);
                    Assert.AreEqual(1, CountInActiveScene<CompanyShell>());
                    Assert.AreEqual(0, CountInActiveScene<HomeShell>());
                    Assert.AreEqual(0, CountInActiveScene<MapHomeReturn>());
                    Assert.AreEqual(0, CountInActiveScene<MapCommandShell>());
                    Assert.AreEqual(0, CountInActiveScene<MapCameraRig>());
                    var company = FindInActiveScene<CompanyShell>();
                    Assert.IsNotNull(company);
                    Assert.IsNotNull(company.HomeButton);
                    Assert.IsTrue(company.HomeButton.interactable);
                    company.Apply(1320f, 2868f, new Rect(0f, 120f, 1320f, 2600f));
                    Assert.IsTrue(company.HomeButton.interactable);
                    yield return WaitForLabel(company.HomeButton, "Home", "Ana Sayfa", "Startseite");
                    var ready = 0f;
                    while (ready < 8f && string.IsNullOrEmpty(company.Body))
                    {
                        ready += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    Assert.That(
                        company.Body,
                        Is.EqualTo("Company data is not connected yet.")
                            .Or.EqualTo("Şirket verisi henüz bağlı değil.")
                            .Or.EqualTo("Unternehmensdaten sind noch nicht verbunden."));
                    StringAssert.DoesNotContain("no company", company.Body.ToLowerInvariant());

                    company.HomeButton.onClick.Invoke();
                    yield return null;
                    yield return null;

                    AssertSettled(AppNavigation.HomeSceneName, ScreenLayoutMode.Portrait);
                    Assert.AreEqual(1, CountInActiveScene<HomeShell>());
                    Assert.AreEqual(0, CountInActiveScene<CompanyShell>());
                }

                Assert.AreEqual(0, _eventSystemWarnings, "EventSystem overlap warning during company navigation.");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Warning && type != LogType.Error)
                return;
            if (condition == null)
                return;
            if (condition.IndexOf("Event System", System.StringComparison.Ordinal) >= 0
                || condition.IndexOf("event systems in the scene", System.StringComparison.Ordinal) >= 0)
                _eventSystemWarnings++;
        }

        static void ReleaseEventSystems()
        {
            for (var i = 0; i < 8 && EventSystem.current != null; i++)
                EventSystem.current.gameObject.SetActive(false);
        }

        static void AssertSettled(string sceneName, ScreenLayoutMode layout)
        {
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
            Assert.AreEqual(1, SceneManager.sceneCount);
            Assert.AreEqual(layout, ScreenLayouts.Requested);
            Assert.IsFalse(Screen.autorotateToPortraitUpsideDown);
            if (layout == ScreenLayoutMode.Portrait)
            {
                Assert.IsTrue(Screen.autorotateToPortrait);
                Assert.IsFalse(Screen.autorotateToLandscapeLeft);
                Assert.IsFalse(Screen.autorotateToLandscapeRight);
            }
            else
            {
                Assert.IsFalse(Screen.autorotateToPortrait);
                Assert.IsTrue(Screen.autorotateToLandscapeLeft);
                Assert.IsTrue(Screen.autorotateToLandscapeRight);
            }

            Assert.AreEqual(1, EnabledEventSystems());
            Assert.IsNotNull(EventSystem.current);
            Assert.AreEqual(SceneManager.GetActiveScene(), EventSystem.current.gameObject.scene);
        }

        static IEnumerator WaitForLabel(Button button, string english, string turkish, string german)
        {
            var ready = 0f;
            Text label = null;
            while (ready < 8f)
            {
                label = button.GetComponentInChildren<Text>(true);
                if (label != null && !string.IsNullOrEmpty(label.text))
                    break;
                ready += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(label);
            Assert.IsFalse(label.text.Contains("No translation found"), label.text);
            Assert.That(label.text, Is.EqualTo(english).Or.EqualTo(turkish).Or.EqualTo(german));
        }

        static int EnabledEventSystems()
        {
            var count = 0;
            var scenes = SceneManager.sceneCount;
            for (var s = 0; s < scenes; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                var roots = scene.GetRootGameObjects();
                for (var i = 0; i < roots.Length; i++)
                {
                    var systems = roots[i].GetComponentsInChildren<EventSystem>(true);
                    for (var n = 0; n < systems.Length; n++)
                    {
                        if (systems[n] != null && systems[n].isActiveAndEnabled)
                            count++;
                    }
                }
            }

            return count;
        }

        static T FindInActiveScene<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }

            return null;
        }

        static int CountInActiveScene<T>() where T : Component
        {
            var count = 0;
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                count += roots[i].GetComponentsInChildren<T>(true).Length;
            return count;
        }
    }
}
