using System.IO;
using System.Security.Cryptography;
using LogistiCore.EditorTools;
using LogistiCore.Localization;
using LogistiCore.Map;
using LogistiCore.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogistiCore.Tests
{
    public sealed class AppShellTests
    {
        const string MapSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        static readonly Vector2[] PortraitViewports =
        {
            new Vector2(393f, 852f),
            new Vector2(430f, 932f),
            new Vector2(440f, 956f),
            new Vector2(360f, 800f)
        };

        static readonly Vector2[] LandscapeViewports =
        {
            new Vector2(844f, 390f),
            new Vector2(932f, 430f),
            new Vector2(2868f, 1320f)
        };

        [Test]
        public void ApplicationBuild_EnablesHomeThenTheMapAndDisablesTestScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.AreEqual(AppNavigation.HomeScenePath, scenes[0].path);
            Assert.IsTrue(scenes[0].enabled);
            Assert.AreEqual(AppNavigation.MapScenePath, scenes[1].path);
            Assert.IsTrue(scenes[1].enabled);
            Assert.AreEqual(AppNavigation.CompanyScenePath, scenes[2].path);
            Assert.IsTrue(scenes[2].enabled);

            var sample = false;
            var verification = false;
            var enabled = 0;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled)
                {
                    enabled++;
                    StringAssert.DoesNotContain("SampleScene.unity", scenes[i].path);
                    StringAssert.DoesNotContain("FoundationVerification.unity", scenes[i].path);
                }

                if (scenes[i].path == "Assets/Scenes/SampleScene.unity")
                {
                    sample = true;
                    Assert.IsFalse(scenes[i].enabled);
                }

                if (scenes[i].path == "Assets/Game/Scenes/FoundationVerification.unity")
                {
                    verification = true;
                    Assert.IsFalse(scenes[i].enabled);
                }
            }

            Assert.IsTrue(sample);
            Assert.IsTrue(verification);
            Assert.AreEqual(3, enabled);
        }

        [Test]
        public void Routes_RequestPortraitHomeAndLandscapeMap()
        {
            Assert.AreEqual(LoadSceneMode.Single, AppNavigation.NavigationMode);
            Assert.AreEqual(AppNavigation.HomeSceneName, AppNavigation.HomeDestination.SceneName);
            Assert.AreEqual(ScreenLayoutMode.Portrait, AppNavigation.HomeDestination.Layout);
            Assert.AreEqual(AppNavigation.MapSceneName, AppNavigation.MapDestination.SceneName);
            Assert.AreEqual(ScreenLayoutMode.Landscape, AppNavigation.MapDestination.Layout);
            Assert.AreEqual(34f, MapMarkerLod.AggregateScreenPixels);
            Assert.AreEqual(22f, MapMarkerLod.AggregateHitRadius);
            Assert.AreEqual(18f, MapCameraRig.CityClearancePixels);
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame);

            AppNavigation.BlockSceneLoad = true;
            try
            {
                for (var i = 0; i < 10; i++)
                {
                    AppNavigation.ShowHome();
                    Assert.AreEqual(ScreenLayoutMode.Portrait, ScreenLayouts.Requested);
                    Assert.AreEqual(AppNavigation.HomeSceneName, AppNavigation.LastDestination.SceneName);
                    AppNavigation.ShowMap();
                    Assert.AreEqual(ScreenLayoutMode.Landscape, ScreenLayouts.Requested);
                    Assert.AreEqual(AppNavigation.MapSceneName, AppNavigation.LastDestination.SceneName);
                    AppNavigation.ShowHome();
                    Assert.AreEqual(ScreenLayoutMode.Portrait, ScreenLayouts.Requested);
                    AppNavigation.ShowCompany();
                    Assert.AreEqual(ScreenLayoutMode.Portrait, ScreenLayouts.Requested);
                    Assert.AreEqual(AppNavigation.CompanySceneName, AppNavigation.LastDestination.SceneName);
                    AppNavigation.ShowHome();
                    Assert.AreEqual(ScreenLayoutMode.Portrait, ScreenLayouts.Requested);
                }
            }
            finally
            {
                AppNavigation.BlockSceneLoad = false;
            }
        }

        [Test]
        public void HomeLayout_StaysInsidePortraitSafeAreas()
        {
            for (var i = 0; i < PortraitViewports.Length; i++)
            {
                var viewport = PortraitViewports[i];
                ExpectHome(viewport.x, viewport.y, new Rect(0f, 0f, viewport.x, viewport.y));
                ExpectHome(viewport.x, viewport.y, new Rect(0f, 34f, viewport.x, viewport.y - 93f));
                ExpectCompany(viewport.x, viewport.y, new Rect(0f, 0f, viewport.x, viewport.y));
                ExpectCompany(viewport.x, viewport.y, new Rect(0f, 34f, viewport.x, viewport.y - 93f));
            }
        }

        [Test]
        public void MapCommand_FitsTheLandscapeViewports()
        {
            for (var i = 0; i < LandscapeViewports.Length; i++)
            {
                var viewport = LandscapeViewports[i];
                ExpectCommand(viewport.x, viewport.y, new Rect(0f, 0f, viewport.x, viewport.y));
            }

            ExpectCommand(2868f, 1320f, new Rect(186f, 60f, 2496f, 1200f));
            ExpectCommand(844f, 390f, new Rect(28f, 12f, 788f, 366f));
            ExpectCommand(932f, 430f, new Rect(32f, 16f, 868f, 398f));
            ExpectCommand(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            ExpectCommand(1280f, 720f, new Rect(0f, 0f, 1280f, 720f));
            ExpectCommand(960f, 540f, new Rect(0f, 0f, 960f, 540f));
        }

        [Test]
        public void ShellCopy_ExistsInEnglishTurkishAndGermanWithoutChangingTheMap()
        {
            FoundationSetup.EnsureLocalization();

            var shell = LocalizationEditorSettings.GetStringTableCollection(ShellContent.Table);
            Assert.IsNotNull(shell);
            for (var i = 0; i < ShellContent.Lines.Length; i++)
            {
                var line = ShellContent.Lines[i];
                Assert.AreEqual(line.English, Value(shell, "en", line.Key));
                Assert.AreEqual(line.Turkish, Value(shell, "tr", line.Key));
                Assert.AreEqual(line.German, Value(shell, "de", line.Key));
                Assert.IsFalse(string.IsNullOrWhiteSpace(line.English));
                if (line.Key == ShellContent.Brand)
                    continue;

                Assert.AreNotEqual(line.English, line.Turkish, line.Key);
                Assert.AreNotEqual(line.English, line.German, line.Key);
            }

            var map = LocalizationEditorSettings.GetStringTableCollection(MapContent.Table);
            Assert.AreEqual("Map", Value(map, "en", MapContent.Title));
            Assert.AreEqual("Harita", Value(map, "tr", MapContent.Title));
            Assert.AreEqual("Karte", Value(map, "de", MapContent.Title));
            Assert.AreEqual("World", Value(map, "en", MapContent.ViewWorld));
            Assert.AreEqual("Dünya", Value(map, "tr", MapContent.ViewWorld));
            Assert.AreEqual("Welt", Value(map, "de", MapContent.ViewWorld));

            var unconnected = Value(shell, "en", ShellContent.CompanyUnconnected);
            StringAssert.DoesNotContain("no company", unconnected.ToLowerInvariant());
            StringAssert.DoesNotContain("0", unconnected);
            StringAssert.DoesNotContain("0", Value(shell, "tr", ShellContent.CompanyUnconnected));
            StringAssert.DoesNotContain("0", Value(shell, "de", ShellContent.CompanyUnconnected));
        }

        [Test]
        public void HomeAndMap_CanBeEnteredTenTimesWithoutDuplicatingOwners()
        {
            Assert.AreEqual(MapSha, Sha(AppNavigation.MapScenePath));
            var mapText = File.ReadAllText(AppNavigation.MapScenePath);
            StringAssert.DoesNotContain("HomeReturn", mapText);
            StringAssert.DoesNotContain("HomeShell", mapText);

            var listed = false;
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == AppNavigation.HomeScenePath)
                {
                    listed = scenes[i].enabled;
                    Assert.AreEqual(0, i);
                }

                if (scenes[i].path == AppNavigation.MapScenePath)
                    Assert.IsTrue(scenes[i].enabled);
            }

            Assert.IsTrue(listed);

            for (var i = 0; i < 10; i++)
            {
                var home = EditorSceneManager.OpenScene(AppNavigation.HomeScenePath, OpenSceneMode.Additive);
                try
                {
                    EditorSceneManager.SetActiveScene(home);
                    HomeShell shell = null;
                    var roots = home.GetRootGameObjects();
                    for (var r = 0; r < roots.Length; r++)
                    {
                        shell = roots[r].GetComponentInChildren<HomeShell>(true);
                        if (shell != null)
                            break;
                    }

                    Assert.IsNotNull(shell);
                    shell.BuildIfNeeded();
                    shell.BuildIfNeeded();
                    shell.Apply(393f, 852f, new Rect(0f, 34f, 393f, 759f));
                    Assert.AreEqual(1, Count<EventSystem>(home));
                    Assert.AreEqual(1, Count<Canvas>(home));
                    Assert.AreEqual(1, Count<Camera>(home));
                    Assert.AreEqual(2, shell.ButtonCount);
                    Assert.IsNotNull(shell.MapButton);
                    Assert.IsNotNull(shell.CompanyButton);
                    Assert.AreEqual(string.Empty, shell.MapButton.GetComponentInChildren<Text>(true).text);
                    Assert.AreEqual(2, shell.GetComponentsInChildren<Button>(true).Length);
                    Assert.AreEqual(0, shell.MapButton.onClick.GetPersistentEventCount());
                    Assert.AreEqual(0, shell.CompanyButton.onClick.GetPersistentEventCount());
                    AssertTouchesOnlyTheCard(shell.MapButton);
                    AssertTouchesOnlyTheCard(shell.CompanyButton);
                    Assert.AreNotEqual(shell.MapButton.colors.normalColor, shell.MapButton.colors.pressedColor);
                    Assert.IsNotNull(FindNamed(shell.MapButton.transform, "Motif"));
                    Assert.IsNotNull(FindNamed(shell.CompanyButton.transform, "Motif"));
                    Assert.IsNotNull(FindNamed(shell.transform, "Rule"));
                    Assert.IsNull(FindNamed(shell.transform, "Notice").GetComponent<Button>());
                    var height = shell.MapButton.GetComponent<LayoutElement>().preferredHeight;
                    var companyHeight = shell.CompanyButton.GetComponent<LayoutElement>().preferredHeight;
                    var metrics = HomeShellLayout.Evaluate(393f, 852f, new Rect(0f, 34f, 393f, 759f));
                    Assert.AreEqual(metrics.ButtonHeight / metrics.CanvasScale, height, 0.1f);
                    Assert.AreEqual(metrics.CompanyButton.height / metrics.CanvasScale, companyHeight, 0.1f);
                    Assert.Greater(height, companyHeight);
                }
                finally
                {
                    if (home.IsValid() && home.isLoaded)
                        EditorSceneManager.CloseScene(home, true);
                }

                Assert.AreEqual(0, ObjectCount<HomeShell>());

                var map = EditorSceneManager.OpenScene(AppNavigation.MapScenePath, OpenSceneMode.Additive);
                try
                {
                    EditorSceneManager.SetActiveScene(map);
                    var systems = Count<EventSystem>(map);
                    var cameras = Count<MapCameraRig>(map);
                    Assert.AreEqual(1, systems);
                    Assert.AreEqual(1, cameras);
                    var before = Count<MapCommandShell>(map);
                    Assert.AreEqual(0, before);
                    Assert.AreEqual(0, Count<MapHomeReturn>(map));
                    var view = MapCommandShell.Attach(map);
                    var again = MapCommandShell.Attach(map);
                    Assert.AreSame(view, again);
                    Assert.AreEqual(1, Count<MapCommandShell>(map));
                    Assert.AreEqual(0, Count<MapHomeReturn>(map));
                    Assert.AreEqual(systems, Count<EventSystem>(map));
                    Assert.AreEqual(cameras, Count<MapCameraRig>(map));
                    view.Apply(2868f, 1320f, new Rect(186f, 60f, 2496f, 1200f));
                    Assert.Greater(view.Viewport.width, 400f);
                    Assert.AreEqual(MapCommandShell.PanelState.Closed, view.State);
                    Assert.IsFalse(view.ContextOpen);
                    Assert.IsNull(FindNamed(view.transform, "Collapse"));
                    Assert.IsNull(view.HomeButton);
                    Assert.IsNotNull(view.RegionButton);
                    Assert.IsTrue(view.RegionButton.interactable);
                    Assert.IsNull(FindNamed(view.transform, "TopBar"));
                    Assert.IsNull(FindNamed(view.transform, "CommandRail"));
                }
                finally
                {
                    if (map.IsValid() && map.isLoaded)
                        EditorSceneManager.CloseScene(map, true);
                }

                Assert.AreEqual(0, ObjectCount<MapCommandShell>());
                Assert.AreEqual(0, ObjectCount<MapHomeReturn>());
                Assert.AreEqual(0, ObjectCount<MapCameraRig>());
            }

            Assert.AreEqual(MapSha, Sha(AppNavigation.MapScenePath));
        }

        [Test]
        public void CompanyScene_ResolvesAPortraitShellWithoutChangingTheMap()
        {
            Assert.AreEqual(MapSha, Sha(AppNavigation.MapScenePath));
            var companyBefore = Sha(AppNavigation.CompanyScenePath);
            var company = EditorSceneManager.OpenScene(AppNavigation.CompanyScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(company);
                CompanyShell shell = null;
                var roots = company.GetRootGameObjects();
                for (var i = 0; i < roots.Length; i++)
                {
                    shell = roots[i].GetComponentInChildren<CompanyShell>(true);
                    if (shell != null)
                        break;
                }

                Assert.IsNotNull(shell);
                shell.BuildIfNeeded();
                shell.Apply(430f, 932f, new Rect(0f, 54f, 430f, 820f));
                Assert.AreEqual(1, Count<CompanyShell>(company));
                Assert.AreEqual(1, Count<EventSystem>(company));
                Assert.AreEqual(1, Count<Canvas>(company));
                Assert.AreEqual(0, Count<MapCameraRig>(company));
                Assert.AreEqual(0, Count<HomeShell>(company));
                Assert.IsNotNull(shell.HomeButton);
                Assert.AreEqual(1, shell.GetComponentsInChildren<Button>(true).Length);
                Assert.IsNotNull(FindNamed(shell.transform, "Overview"));
                Assert.AreEqual(string.Empty, shell.Body);
                Assert.AreEqual(0, shell.HomeButton.onClick.GetPersistentEventCount());
                AssertTouchesOnlyTheCard(shell.HomeButton);
                Assert.AreNotEqual(shell.HomeButton.colors.normalColor, shell.HomeButton.colors.pressedColor);
                Assert.IsNotNull(FindNamed(shell.transform, "Motif"));
                Assert.IsNotNull(FindNamed(shell.transform, "Rule"));
                Assert.IsNotNull(FindNamed(shell.HomeButton.transform, "Label"));
                Assert.AreEqual(ScreenLayoutMode.Portrait, AppNavigation.CompanyDestination.Layout);
            }
            finally
            {
                if (company.IsValid() && company.isLoaded)
                    EditorSceneManager.CloseScene(company, true);
            }

            Assert.AreEqual(companyBefore, Sha(AppNavigation.CompanyScenePath));
            Assert.AreEqual(MapSha, Sha(AppNavigation.MapScenePath));
            Assert.AreEqual(0, ObjectCount<CompanyShell>());
        }

        static void ExpectHome(float width, float height, Rect safe)
        {
            var metrics = HomeShellLayout.Evaluate(width, height, safe);
            Assert.IsTrue(HomeShellLayout.Contains(safe, metrics.Content), width + "x" + height);
            Assert.IsTrue(HomeShellLayout.Contains(safe, metrics.Button), width + "x" + height);
            Assert.IsTrue(HomeShellLayout.Contains(safe, metrics.CompanyButton), width + "x" + height);
            Assert.IsTrue(HomeShellLayout.Contains(safe, metrics.Notice), width + "x" + height);
            Assert.IsTrue(HomeShellLayout.Contains(metrics.Content, metrics.CompanyButton), width + "x" + height);
            Assert.IsTrue(HomeShellLayout.Contains(metrics.Content, metrics.Notice), width + "x" + height);
            Assert.GreaterOrEqual(metrics.Button.yMin, metrics.CompanyButton.yMax - 0.01f);
            Assert.GreaterOrEqual(metrics.CompanyButton.yMin, metrics.Notice.yMax - 0.01f);
            Assert.Greater(metrics.Notice.yMin, metrics.Content.yMin + 8f);
            Assert.Greater(metrics.Button.center.y, metrics.Content.center.y);
            Assert.Greater(metrics.Button.height, metrics.CompanyButton.height);
            Assert.Greater(metrics.CompanyButton.height, metrics.Notice.height);
            Assert.GreaterOrEqual(metrics.Button.height, 96f);
            Assert.GreaterOrEqual(metrics.CompanyButton.height, HomeShellLayout.MinButtonPixels);
            Assert.Less(metrics.Button.height, metrics.Content.height * 0.55f);
            Assert.IsFalse(metrics.Overflow);
            var renderedHeading = metrics.HeadingSize * metrics.CanvasScale;
            var renderedLabel = metrics.ButtonLabelSize * metrics.CanvasScale;
            Assert.GreaterOrEqual(renderedHeading, 24f);
            Assert.LessOrEqual(renderedHeading, 32f);
            Assert.GreaterOrEqual(renderedLabel, 16f);
            Assert.LessOrEqual(renderedLabel, 21f);
            var canvasWidth = metrics.Content.width / metrics.CanvasScale;
            var cardText = HomeShellLayout.CardTextWidth(metrics.Content.width, metrics.CanvasScale);
            Assert.IsTrue(HomeShellLayout.LabelFits(ShellContent.LongestWorldMapLabel, metrics.ButtonLabelSize, cardText));
            Assert.IsTrue(HomeShellLayout.LabelFits(ShellContent.LongestCompanyLabel, metrics.ButtonLabelSize, cardText));
            Assert.IsTrue(
                CaptionFits(ShellContent.LongestMapCaption, metrics.CaptionSize, cardText, 2),
                width + "x" + height + " " + ShellContent.LongestMapCaption);
            Assert.IsTrue(HomeShellLayout.LabelFits(ShellContent.LongestHeading, metrics.HeadingSize, canvasWidth));
            Assert.IsTrue(HomeShellLayout.LabelFits("Startseite", metrics.HeadingSize, canvasWidth));
            Assert.IsTrue(HomeShellLayout.LabelFits(ShellContent.LongestCompanyLabel, metrics.HeadingSize, canvasWidth));
        }

        static void ExpectCompany(float width, float height, Rect safe)
        {
            var metrics = CompanyShellLayout.Evaluate(width, height, safe);
            Assert.IsTrue(HomeShellLayout.Contains(safe, metrics.Content), width + "x" + height);
            Assert.IsTrue(HomeShellLayout.Contains(safe, metrics.HomeButton), width + "x" + height);
            Assert.IsTrue(HomeShellLayout.Contains(metrics.Content, metrics.Notice), width + "x" + height);
            Assert.IsTrue(HomeShellLayout.Contains(metrics.Content, metrics.HomeButton), width + "x" + height);
            Assert.GreaterOrEqual(metrics.Notice.yMin, metrics.HomeButton.yMax - 0.01f);
            Assert.Greater(metrics.HomeButton.yMin, metrics.Content.yMin + 4f);
            Assert.GreaterOrEqual(metrics.HomeButton.height, HomeShellLayout.MinButtonPixels);
            Assert.LessOrEqual(metrics.HomeButton.height, 64.1f);
            Assert.Greater(metrics.Notice.height, metrics.BodySize * metrics.CanvasScale * 2f);
            Assert.Less(metrics.Notice.height, metrics.Content.height * 0.7f);
            Assert.IsFalse(metrics.Overflow);
            var canvasWidth = metrics.Notice.width / metrics.CanvasScale;
            Assert.IsTrue(HomeShellLayout.LabelFits("Unternehmensdaten", metrics.BodySize, canvasWidth - 56f));
            Assert.IsTrue(HomeShellLayout.LabelFits("Noch nicht verbunden", metrics.StatusSize, canvasWidth - 56f));
            Assert.IsTrue(HomeShellLayout.LabelFits(ShellContent.LongestHomeLabel, metrics.ButtonLabelSize, metrics.HomeButton.width / metrics.CanvasScale));
            Assert.GreaterOrEqual(metrics.ButtonLabelSize * metrics.CanvasScale, 16f);
        }

        [Test]
        public void PhoneFramebuffer_KeepsReadableTypeAndTouchTargets()
        {
            var safe = new Rect(0f, 120f, 1320f, 2600f);
            var home = HomeShellLayout.Evaluate(1320f, 2868f, safe);
            Assert.IsTrue(HomeShellLayout.Contains(safe, home.Button));
            Assert.IsTrue(HomeShellLayout.Contains(safe, home.CompanyButton));
            Assert.IsTrue(HomeShellLayout.Contains(safe, home.Notice));
            Assert.GreaterOrEqual(home.Button.height, 132f);
            Assert.GreaterOrEqual(home.CompanyButton.height, 132f);
            Assert.GreaterOrEqual(home.ButtonLabelSize * home.CanvasScale, 48f);
            Assert.GreaterOrEqual(home.HeadingSize * home.CanvasScale, 72f);
            Assert.IsFalse(home.Overflow);
            Assert.Greater(home.Notice.yMin, home.Content.yMin);

            var company = CompanyShellLayout.Evaluate(1320f, 2868f, safe);
            Assert.IsTrue(HomeShellLayout.Contains(safe, company.Notice));
            Assert.IsTrue(HomeShellLayout.Contains(safe, company.HomeButton));
            Assert.GreaterOrEqual(company.Notice.yMin, company.HomeButton.yMax - 0.01f);
            Assert.GreaterOrEqual(company.HomeButton.height, 132f);
            Assert.Greater(company.HomeButton.yMin, company.Content.yMin);
            Assert.IsFalse(company.Overflow);
        }

        [Test]
        public void PortraitShells_RouteThroughTheExistingNavigation()
        {
            var home = EditorSceneManager.OpenScene(AppNavigation.HomeScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(home);
                var shell = FindShell<HomeShell>(home);
                shell.BuildIfNeeded();
                shell.BuildIfNeeded();
                Assert.AreEqual(2, shell.ButtonCount);
                Assert.AreEqual(1, Count<EventSystem>(home));
                AppNavigation.BlockSceneLoad = true;
                shell.OpenMap();
                Assert.AreEqual(AppNavigation.MapSceneName, AppNavigation.LastDestination.SceneName);
                Assert.AreEqual(ScreenLayoutMode.Landscape, ScreenLayouts.Requested);
                shell.OpenCompany();
                Assert.AreEqual(AppNavigation.CompanySceneName, AppNavigation.LastDestination.SceneName);
                Assert.AreEqual(ScreenLayoutMode.Portrait, ScreenLayouts.Requested);
            }
            finally
            {
                AppNavigation.BlockSceneLoad = false;
                if (home.IsValid() && home.isLoaded)
                    EditorSceneManager.CloseScene(home, true);
            }

            var company = EditorSceneManager.OpenScene(AppNavigation.CompanyScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(company);
                var shell = FindShell<CompanyShell>(company);
                shell.BuildIfNeeded();
                Assert.AreEqual(1, Count<EventSystem>(company));
                Assert.AreEqual(0, Count<HomeShell>(company));
                AppNavigation.BlockSceneLoad = true;
                shell.OpenHome();
                Assert.AreEqual(AppNavigation.HomeSceneName, AppNavigation.LastDestination.SceneName);
                Assert.AreEqual(ScreenLayoutMode.Portrait, ScreenLayouts.Requested);
            }
            finally
            {
                AppNavigation.BlockSceneLoad = false;
                if (company.IsValid() && company.isLoaded)
                    EditorSceneManager.CloseScene(company, true);
            }

            Assert.AreEqual(MapSha, Sha(AppNavigation.MapScenePath));
        }

        static void ExpectCommand(float width, float height, Rect safe)
        {
            ExpectCommandState(width, height, safe, false);
            ExpectCommandState(width, height, safe, true);
        }

        static void ExpectCommandState(float width, float height, Rect safe, bool requestedOpen)
        {
            var metrics = MapCommandLayout.Evaluate(width, height, safe, requestedOpen);
            Assert.AreEqual(0f, metrics.Header.xMin, 0.5f, width + "x" + height);
            Assert.AreEqual(width, metrics.Header.xMax, 0.5f, width + "x" + height);
            Assert.AreEqual(height, metrics.Header.yMax, 0.5f, width + "x" + height);
            Assert.LessOrEqual(metrics.Header.yMin, safe.yMax - 28f, width + "x" + height);
            Assert.AreEqual(0f, metrics.Summary.xMin, 0.5f, width + "x" + height);
            Assert.AreEqual(width, metrics.Summary.xMax, 0.5f, width + "x" + height);
            Assert.AreEqual(0f, metrics.Summary.yMin, 0.5f, width + "x" + height);
            Assert.Greater(metrics.Summary.yMax, safe.yMin, width + "x" + height);
            Assert.Less(metrics.Summary.yMax - safe.yMin, safe.height * 0.1f, width + "x" + height);
            Assert.AreEqual(0f, metrics.Rail.xMin, 0.5f, width + "x" + height);
            var railContent = metrics.Rail.width - Mathf.Max(0f, safe.xMin);
            Assert.GreaterOrEqual(railContent, MapCommandLayout.RailTouchPixels(metrics.FontPixels) - 0.5f, width + "x" + height);
            Assert.LessOrEqual(railContent, MapCommandLayout.RailContentPixels(metrics.FontPixels) + 0.6f, width + "x" + height);
            Assert.Less(railContent, MapCommandLayout.TextWidth(MapCommandLayout.LongestMode, metrics.FontPixels), width + "x" + height);
            Assert.AreEqual(metrics.Rail.xMax, metrics.Viewport.xMin, 0.5f, width + "x" + height);
            Assert.IsTrue(HomeShellLayout.Contains(safe, metrics.Viewport), width + "x" + height);
            Assert.IsTrue(HomeShellLayout.Contains(safe, metrics.Home), metrics.Home.ToString());
            Assert.IsTrue(HomeShellLayout.Contains(metrics.Header, metrics.Home), metrics.Home.ToString());
            Assert.GreaterOrEqual(metrics.Home.height, 28f, width + "x" + height);
            Assert.GreaterOrEqual(metrics.Home.width, 28f, width + "x" + height);
            Assert.LessOrEqual(metrics.Home.width, Mathf.Max(metrics.Home.height, MapCommandLayout.MinTouchPixels) + 0.5f, width + "x" + height);
            Assert.Greater(metrics.Viewport.width, safe.width * 0.4f, width + "x" + height);
            Assert.Greater(metrics.Viewport.height, safe.height * 0.55f, width + "x" + height);
            if (height < 640f || width < 1100f)
                Assert.IsFalse(metrics.FutureModes, width + "x" + height);
            else
                Assert.IsTrue(metrics.FutureModes, width + "x" + height);
            Assert.IsFalse(Overlaps(metrics.Viewport, metrics.Header));
            Assert.IsFalse(Overlaps(metrics.Viewport, metrics.Rail));
            Assert.IsFalse(Overlaps(metrics.Viewport, metrics.Summary));
            Assert.IsFalse(Overlaps(metrics.Viewport, metrics.Context));
            Assert.IsFalse(MapCommandLayout.LabelFits(MapCommandLayout.LongestHome, metrics.FontPixels, metrics.Home.width), width.ToString());
            Assert.IsTrue(MapCommandLayout.LabelFits(MapCommandLayout.LongestSummary, metrics.FontPixels, safe.width - 32f), width.ToString());
            if (!requestedOpen)
            {
                Assert.IsFalse(metrics.ContextOpen, width + "x" + height);
                Assert.Less(metrics.Context.width, 1f, width + "x" + height);
                Assert.AreEqual(safe.xMax, metrics.Viewport.xMax, 1f, width + "x" + height);
                return;
            }

            Assert.IsTrue(metrics.ContextOpen, width + "x" + height);
            Assert.AreEqual(width, metrics.Context.xMax, 0.5f, width + "x" + height);
            var contextSafe = Mathf.Min(metrics.Context.xMax, safe.xMax) - Mathf.Max(metrics.Context.xMin, safe.xMin);
            var inner = contextSafe - metrics.FontPixels * 2.4f;
            Assert.Greater(inner, 72f, width.ToString());
            var lines = Mathf.Ceil(MapCommandLayout.TextWidth(MapCommandLayout.LongestUnavailable, metrics.FontPixels) / inner);
            Assert.LessOrEqual(lines * metrics.FontPixels * 1.35f, metrics.Context.height * 0.62f, width.ToString());
        }

        static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax - 0.5f && a.xMax > b.xMin + 0.5f && a.yMin < b.yMax - 0.5f && a.yMax > b.yMin + 0.5f;
        }

        static void AssertTouchesOnlyTheCard(Button button)
        {
            Assert.IsTrue(button.interactable);
            var graphics = button.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null)
                    continue;
                if (graphic.gameObject == button.gameObject)
                    Assert.IsTrue(graphic.raycastTarget);
                else
                    Assert.IsFalse(graphic.raycastTarget, graphic.name);
            }
        }

        static T FindShell<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var shell = roots[i].GetComponentInChildren<T>(true);
                if (shell != null)
                    return shell;
            }

            Assert.Fail("Missing " + typeof(T).Name);
            return null;
        }

        static Transform FindNamed(Transform root, string name)
        {
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

        static int Count<T>(Scene scene) where T : Component
        {
            var count = 0;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                count += roots[i].GetComponentsInChildren<T>(true).Length;

            return count;
        }

        static int ObjectCount<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
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

        static bool CaptionFits(string text, int fontSize, float width, int maxLines)
        {
            if (string.IsNullOrEmpty(text) || fontSize < 1 || width <= 1f || maxLines < 1)
                return false;

            var advance = fontSize * 0.62f;
            var words = text.Split(' ');
            var lines = 1;
            var line = 0f;
            for (var i = 0; i < words.Length; i++)
            {
                var word = words[i].Length * advance;
                if (word > width)
                    return false;

                var gap = line > 0f ? advance : 0f;
                if (line + gap + word > width)
                {
                    lines++;
                    line = word;
                }
                else
                {
                    line += gap + word;
                }
            }

            return lines <= maxLines;
        }

        static string Sha(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var text = new System.Text.StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                    text.Append(hash[i].ToString("x2"));
                return text.ToString();
            }
        }
    }
}
