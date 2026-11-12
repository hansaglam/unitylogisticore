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
                    Assert.IsNull(back.HomeButton);
                    Assert.IsNotNull(back.RegionButton);
                    Assert.IsFalse(back.RegionMenuOpen);
                    Assert.AreEqual(MapCommandShell.PanelState.Closed, back.State);
                    Assert.IsFalse(back.ContextOpen);
                    Assert.IsTrue(back.RegionButton.interactable);

                    if (i == 0)
                    {
                        back.Apply(1320f, 2868f, new Rect(0f, 0f, 1320f, 2868f));
                        Assert.IsTrue(back.gameObject.activeInHierarchy);
                        back.Apply(2868f, 1320f, new Rect(186f, 60f, 2496f, 1200f));
                        Assert.Greater(back.Viewport.width, 400f);
                        Assert.IsTrue(back.RegionButton.interactable);
                        Assert.GreaterOrEqual(back.RegionButton.GetComponent<RectTransform>().rect.height, 28f);
                    }
                    if (i == 0)
                    {
                        Button fleetRow = null;
                        var rows = back.GetComponentsInChildren<Button>(true);
                        for (var r = 0; r < rows.Length; r++)
                        {
                            if (rows[r].name == "Mode0")
                                fleetRow = rows[r];
                            Assert.AreNotEqual("CurrentMode", rows[r].name);
                        }

                        Assert.IsNotNull(fleetRow);
                        var page = false;
                        var named = back.GetComponentsInChildren<Transform>(true);
                        for (var n = 0; n < named.Length; n++)
                        {
                            if (named[n].name == "Page")
                                page = true;
                        }

                        Assert.IsFalse(page);
                        yield return WaitForLabel(fleetRow, "Fleet", "Filo", "Flotte");
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
                            Is.EqualTo("No live fleet data connection.")
                                .Or.EqualTo("Canlı filo verisi bağlantısı yok.")
                                .Or.EqualTo("Keine Live-Fuhrparkverbindung."));
                        Assert.IsTrue(back.ManagementOpen);
                        Assert.AreEqual(MapModule.Fleet, back.ActiveModule);
                        back.CloseContext();
                        Assert.AreEqual(MapCommandShell.PanelState.Closed, back.State);
                        Assert.IsFalse(back.ContextOpen);
                    }

                    back.OpenHome();
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

        [UnityTest]
        public IEnumerator RegionNavigation_RendersLocalesAndFocusesEveryPreset()
        {
            ReleaseEventSystems();
            yield return SceneManager.LoadSceneAsync(AppNavigation.MapSceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var shell = FindInActiveScene<MapCommandShell>();
            var rig = FindInActiveScene<MapCameraRig>();
            var markers = FindInActiveScene<MapMarkerLayer>();
            Assert.IsNotNull(shell);
            Assert.IsNotNull(rig);
            Assert.IsNotNull(markers);
            Assert.AreEqual(1, EnabledEventSystems());

            var ready = 0f;
            while (!LogistiCore.Localization.LocaleService.IsReady && ready < 8f)
            {
                ready += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(LogistiCore.Localization.LocaleService.IsReady, "LocaleService did not initialize in Play Mode.");
            Assert.IsNotNull(FindSceneText("Logisti"));
            Assert.IsNotNull(FindSceneText("Core"));

            var locales = new[] { "tr", "en", "de" };
            var names = new[]
            {
                new[] { "Dünya", "Türkiye", "Avrupa", "Asya", "Afrika", "Kuzey Amerika", "Güney Amerika", "Okyanusya" },
                new[] { "World", "Turkey", "Europe", "Asia", "Africa", "North America", "South America", "Oceania" },
                new[] { "Welt", "Türkei", "Europa", "Asien", "Afrika", "Nordamerika", "Südamerika", "Ozeanien" }
            };
            var france = new[] { "Fransa", "France", "Frankreich" };
            var istanbul = new[] { "İstanbul", "Istanbul", "Istanbul" };
            for (var locale = 0; locale < locales.Length; locale++)
            {
                Assert.IsTrue(LogistiCore.Localization.LocaleService.TrySelect(locales[locale]), locales[locale]);
                yield return null;
                yield return null;
                shell.ToggleRegionMenu();
                Assert.IsTrue(shell.RegionMenuOpen, locales[locale]);
                for (var i = 0; i < 8; i++)
                {
                    var row = FindNamed(shell.transform, "Region" + i);
                    var label = FindNamed(row, "Name").GetComponent<Text>();
                    var waited = 0f;
                    while (label.text != names[locale][i] && waited < 4f)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    Assert.AreEqual(names[locale][i], label.text, locales[locale] + " row " + i);
                }

                shell.ToggleRegionMenu();
                Assert.IsFalse(shell.RegionMenuOpen);
                var rail = FindInActiveScene<MapCommandShell>().RegionButton;
                Assert.IsNotNull(rail);
                var fleet = FindNamed(shell.transform, "Mode0");
                yield return WaitForLabel(fleet.GetComponent<Button>(), "Fleet", "Filo", "Flotte");
            }

            Assert.IsTrue(LogistiCore.Localization.LocaleService.TrySelect("en"));
            yield return null;
            var europeCenter = Vector2.zero;
            var europeSize = 0f;
            for (var i = 0; i < 8; i++)
            {
                var started = Time.realtimeSinceStartup;
                Assert.IsTrue(shell.SelectRegion(i), "region " + i);
                Assert.IsFalse(shell.RegionMenuOpen);
                Assert.AreEqual((MapScope)i, rig.Scope);
                Assert.AreEqual(i, shell.RegionIndex);
                if (i == 0)
                {
                    var posed = new Vector2(rig.transform.position.x, rig.transform.position.y);
                    Assert.Greater((posed - rig.TargetCenter).sqrMagnitude, 1f, "World selection snapped.");
                }

                yield return WaitForFrame(rig);
                Assert.Less(Time.realtimeSinceStartup - started, 3f, "region " + i + " stalled");
                var chip = FindNamed(FindNamed(shell.transform, "RegionChip"), "Name");
                Assert.AreEqual(names[1][i], chip.GetComponent<Text>().text, "chip " + i);
                if (i == 2)
                {
                    europeCenter = rig.TargetCenter;
                    europeSize = rig.TargetOrthographicSize;
                    var seen = 0f;
                    var country = FindSceneText(france[1]);
                    while (country == null && seen < 3f)
                    {
                        seen += Time.unscaledDeltaTime;
                        country = FindSceneText(france[1]);
                        if (country == null)
                            country = FindSceneText("Germany");
                        yield return null;
                    }

                    Assert.IsNotNull(country, "No country name was drawn at the Europe frame.");
                }

                if (i == 1)
                {
                    var seen = 0f;
                    var city = FindSceneText(istanbul[1]);
                    while (city == null && seen < 3f)
                    {
                        seen += Time.unscaledDeltaTime;
                        city = FindSceneText(istanbul[1]);
                        if (city == null)
                            city = FindSceneText("Ankara");
                        yield return null;
                    }

                    Assert.IsNotNull(city, "No Turkish city name was drawn at the Turkey frame.");
                }
            }

            Assert.IsTrue(shell.SelectRegion(2));
            yield return WaitForFrame(rig);
            rig.PanScreenDelta(new Vector2(80f, -30f));
            Assert.IsTrue(rig.UserAdjusted);
            Assert.AreEqual(2, shell.RegionIndex);
            var panned = rig.TargetCenter;
            rig.ZoomAtScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 1.15f);
            Assert.IsTrue(shell.SelectRegion(2));
            Assert.AreEqual(europeCenter, rig.TargetCenter);
            Assert.AreEqual(europeSize, rig.TargetOrthographicSize, 0.01f);
            Assert.AreNotEqual(panned, rig.TargetCenter);
            Assert.IsFalse(rig.UserAdjusted);

            Assert.IsTrue(shell.SelectRegion(7));
            var flightStart = new Vector2(rig.transform.position.x, rig.transform.position.y);
            Assert.Greater((flightStart - rig.TargetCenter).sqrMagnitude, 1f, "Oceania selection snapped.");
            yield return null;
            rig.PanScreenDelta(new Vector2(24f, -12f));
            var cancelled = new Vector2(rig.transform.position.x, rig.transform.position.y);
            Assert.IsTrue(rig.UserAdjusted);
            yield return null;
            var stayed = new Vector2(rig.transform.position.x, rig.transform.position.y);
            Assert.AreEqual(cancelled.x, stayed.x, 0.001f, "Pan did not cancel the region flight.");
            Assert.AreEqual(cancelled.y, stayed.y, 0.001f, "Pan did not cancel the region flight.");
            Assert.IsTrue(shell.SelectRegion(5));
            yield return null;
            rig.ZoomAtScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 1.12f);
            var pinched = new Vector2(rig.transform.position.x, rig.transform.position.y);
            Assert.IsTrue(rig.UserAdjusted);
            yield return null;
            var heldPinch = new Vector2(rig.transform.position.x, rig.transform.position.y);
            Assert.AreEqual(pinched.x, heldPinch.x, 0.001f, "Pinch did not cancel the region flight.");
            Assert.AreEqual(pinched.y, heldPinch.y, 0.001f, "Pinch did not cancel the region flight.");
            Assert.IsTrue(shell.SelectRegion(2));
            Assert.AreEqual(2, shell.RegionIndex);

            var fleetButton = FindNamed(shell.transform, "Mode0").GetComponent<Button>();
            fleetButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(MapCommandShell.PanelState.Module, shell.State);
            Assert.AreEqual(2, shell.RegionIndex, "Opening the fleet panel moved the region.");
            var close = FindNamed(shell.transform, "Close").GetComponent<Button>();
            close.onClick.Invoke();
            yield return null;
            Assert.AreEqual(MapCommandShell.PanelState.Closed, shell.State);
            Assert.AreEqual(MapScope.Europe, rig.Scope);

            Assert.IsTrue(shell.SelectRegion(1));
            yield return WaitForFrame(rig);
            Assert.IsTrue(MapCities.TryGet("ankara", out var ankara));
            var projected = MapProjection.Project(ankara.Latitude, ankara.Longitude);
            var camera = rig.GetComponent<Camera>();
            var units = MapMarkerLod.UnitsPerPixel(camera.orthographicSize, Screen.height);
            var point = MapMarkerLod.ToScreen(
                projected,
                new Vector2(rig.transform.position.x, rig.transform.position.y),
                units,
                Screen.width,
                Screen.height);
            Assert.IsTrue(markers.TrySelect(point, out var selected));
            Assert.AreEqual("ankara", selected);
            yield return null;
            Assert.AreEqual(MapCommandShell.PanelState.City, shell.State);
            Assert.AreEqual(1, shell.RegionIndex);
            close = FindNamed(shell.transform, "Close").GetComponent<Button>();
            close.onClick.Invoke();
            yield return null;
            Assert.AreEqual(MapCommandShell.PanelState.Closed, shell.State);
            Assert.AreEqual(1, EnabledEventSystems());
            Assert.IsTrue(shell.RegionButton.interactable);
        }


        static IEnumerator WaitForFrame(MapCameraRig rig)
        {
            var camera = rig.GetComponent<Camera>();
            var waited = 0f;
            while (waited < 3f)
            {
                var center = new Vector2(rig.transform.position.x, rig.transform.position.y);
                if ((center - rig.TargetCenter).sqrMagnitude < 0.01f
                    && Mathf.Abs(camera.orthographicSize - rig.TargetOrthographicSize) < 0.05f)
                    yield break;

                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindNamed(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        static Transform FindSceneText(string value)
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var found = FindTextObject(roots[i].transform, value);
                if (found != null)
                    return found;
            }

            return null;
        }

        static Transform FindText(Transform root, string value)
        {
            return FindTextObject(root, value);
        }

        static Transform FindTextObject(Transform root, string value)
        {
            if (root == null)
                return null;
            var text = root.GetComponent<Text>();
            if (text != null && text.text == value && text.gameObject.activeInHierarchy)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindTextObject(root.GetChild(i), value);
                if (found != null)
                    return found;
            }

            return null;
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
