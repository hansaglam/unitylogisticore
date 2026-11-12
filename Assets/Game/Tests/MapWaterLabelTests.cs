using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using LogistiCore.EditorTools;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace LogistiCore.Tests
{
    public sealed class MapWaterLabelTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        [Test]
        public void Catalog_UsesInWaterAnchorsAndStableKeys()
        {
            Assert.AreEqual(21, MapWaterCatalog.All.Length);
            Assert.AreEqual(21, MapContent.WaterKeys.Length);
            Assert.AreEqual(MapContent.WaterBlackSea, Water("black_sea").Key);
            Assert.AreEqual(33.8f, Water("black_sea").Longitude, 0.08f);
            Assert.AreEqual(41.95f, Water("black_sea").Latitude, 0.05f);
            Assert.Greater(Water("black_sea").Latitude, 41.75f);
            Assert.Less(Water("black_sea").Latitude, 42.3f);
            Assert.Less(Water("aegean_sea").Longitude, 27.1f);
            Assert.Greater(Water("aegean_sea").Longitude, 26.2f);
            Assert.Greater(Water("aegean_sea").Latitude, 36.4f);
            Assert.Less(Water("aegean_sea").Latitude, 37.2f);
            Assert.Less(Water("atlantic").Longitude, -20f);
            Assert.Greater(Water("atlantic").Longitude, -50f);
            Assert.Greater(Water("mediterranean_sea").Latitude, 35.7f);
            Assert.Less(Water("mediterranean_sea").Latitude, 36.4f);
            Assert.Greater(Water("marmara_sea").Longitude, 27.2f);
            Assert.Less(Water("marmara_sea").Longitude, 28.4f);
            Assert.AreEqual(40.70f, Water("marmara_sea").Latitude, 0.08f);
            Assert.Less(Water("caspian_sea").Longitude, 54f);
            Assert.Greater(Water("caspian_sea").Longitude, 48f);
            Assert.Less(Mathf.Abs(Water("pacific").Longitude), 180f);
            Assert.Greater(Water("arctic").Latitude, 75f);
            Assert.Less(Water("southern").Latitude, -50f);
            for (var i = 0; i < MapWaterCatalog.All.Length; i++)
            {
                var entry = MapWaterCatalog.All[i];
                Assert.IsTrue(MapProjection.TryProject(entry.Latitude, entry.Longitude, out var world), entry.Id);
                Assert.IsTrue(float.IsFinite(world.x) && float.IsFinite(world.y), entry.Id);
                Assert.Contains(entry.Key, MapContent.Keys);
            }
        }

        [Test]
        public void Names_ComeFromTheMapTableForTurkishEnglishAndGerman()
        {
            MapSceneSetup.EnsureLocalization();
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            var collection = LocalizationEditorSettings.GetStringTableCollection(MapContent.Table);
            Assert.AreEqual("Karadeniz", Value(collection, "tr", MapContent.WaterBlackSea));
            Assert.AreEqual("Black Sea", Value(collection, "en", MapContent.WaterBlackSea));
            Assert.AreEqual("Schwarzes Meer", Value(collection, "de", MapContent.WaterBlackSea));
            Assert.AreEqual("Ege Denizi", Value(collection, "tr", MapContent.WaterAegean));
            Assert.AreEqual("Aegean Sea", Value(collection, "en", MapContent.WaterAegean));
            Assert.AreEqual("Ägäisches Meer", Value(collection, "de", MapContent.WaterAegean));
            Assert.AreEqual("Akdeniz", Value(collection, "tr", MapContent.WaterMediterranean));
            Assert.AreEqual("Mediterranean Sea", Value(collection, "en", MapContent.WaterMediterranean));
            Assert.AreEqual("Mittelmeer", Value(collection, "de", MapContent.WaterMediterranean));
            Assert.AreEqual("Marmara Denizi", Value(collection, "tr", MapContent.WaterMarmara));
            Assert.AreEqual("Sea of Marmara", Value(collection, "en", MapContent.WaterMarmara));
            Assert.AreEqual("Marmarameer", Value(collection, "de", MapContent.WaterMarmara));
            Assert.AreEqual("Atlas Okyanusu", Value(collection, "tr", MapContent.OceanAtlantic));
            Assert.AreEqual("Büyük Okyanus", Value(collection, "tr", MapContent.OceanPacific));
            Assert.AreEqual("Pacific Ocean", Value(collection, "en", MapContent.OceanPacific));
            Assert.AreEqual("Pazifischer Ozean", Value(collection, "de", MapContent.OceanPacific));
            Assert.AreEqual("Güney Çin Denizi", Value(collection, "tr", MapContent.WaterSouthChina));
            Assert.AreEqual("Südchinesisches Meer", Value(collection, "de", MapContent.WaterSouthChina));
            Assert.AreEqual("Kızıldeniz", Value(collection, "tr", MapContent.WaterRedSea));

            Assert.AreEqual("Karadeniz", MapWaterLabels.Name(Water("black_sea"), "tr"));
            Assert.AreEqual("Black Sea", MapWaterLabels.Name(Water("black_sea"), "en"));
            Assert.AreEqual("Ägäisches Meer", MapWaterLabels.Name(Water("aegean_sea"), "de"));
            Assert.AreEqual("Mittelmeer", MapWaterLabels.Name(Water("mediterranean_sea"), "de-DE"));
        }

        [Test]
        public void Zoom_ShowsOceansAtWorldAndTurkeySeasWithoutCityHandoff()
        {
            var aspect = 2868f / 1320f;
            var world = MapFrames.MaximumOrthographicSize(aspect);
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkey);
            var closest = turkey * 0.46f;
            var worldZoom = MapWaterLabels.Zoom(world, world);
            var turkeyZoom = MapWaterLabels.Zoom(turkey, world);
            var closeZoom = MapWaterLabels.Zoom(closest, world);
            var countryTurkey = MapCountryLabels.Zoom(turkey, world, turkey);

            Assert.AreEqual(-1f, countryTurkey, 0.001f);
            Assert.Greater(turkeyZoom, worldZoom);
            Assert.Greater(closeZoom, turkeyZoom);
            TestContext.WriteLine("water zoom world=" + worldZoom + " turkey=" + turkeyZoom + " close=" + closeZoom);
            Assert.IsTrue(MapWaterLabels.Shows(Water("atlantic"), worldZoom, MapScope.World));
            Assert.IsTrue(MapWaterLabels.Shows(Water("pacific"), worldZoom, MapScope.World));
            Assert.IsFalse(MapWaterLabels.Shows(Water("black_sea"), worldZoom, MapScope.World));
            Assert.IsFalse(MapWaterLabels.Shows(Water("marmara_sea"), worldZoom, MapScope.World));
            Assert.IsTrue(MapWaterLabels.Shows(Water("black_sea"), turkeyZoom, MapScope.Turkey));
            Assert.IsTrue(MapWaterLabels.Shows(Water("aegean_sea"), turkeyZoom, MapScope.Turkey));
            Assert.IsTrue(MapWaterLabels.Shows(Water("mediterranean_sea"), turkeyZoom, MapScope.Turkey));
            Assert.IsFalse(MapWaterLabels.Shows(Water("marmara_sea"), turkeyZoom, MapScope.Turkey));
            Assert.IsFalse(MapWaterLabels.Shows(Water("atlantic"), turkeyZoom, MapScope.Turkey));
            Assert.IsTrue(MapWaterLabels.Shows(Water("marmara_sea"), closeZoom, MapScope.Turkey));
            Assert.IsFalse(MapWaterLabels.Shows(Water("black_sea"), turkeyZoom, MapScope.World));
            var held = MapWaterLabels.HoldZoom(turkeyZoom, turkey, turkey * 1.02f, MapWaterLabels.Zoom(turkey * 1.02f, world));
            Assert.AreEqual(turkeyZoom, held, 0.0001f);
        }

        [Test]
        public void Collision_YieldsToCitiesAndKeepsTheHigherRank()
        {
            var waters = MapWaterCatalog.All;
            var screen = new Vector2[waters.Length];
            var names = new string[waters.Length];
            var measured = new float[waters.Length];
            for (var i = 0; i < waters.Length; i++)
            {
                screen[i] = new Vector2(-4000f, -4000f);
                names[i] = waters[i].Id;
                measured[i] = 80f;
            }

            var black = MapWaterCatalog.IndexOf("black_sea");
            var aegean = MapWaterCatalog.IndexOf("aegean_sea");
            screen[black] = new Vector2(640f, 400f);
            screen[aegean] = new Vector2(640f, 400f);
            var viewport = new Rect(100f, 80f, 1600f, 900f);
            var chosen = new int[MapWaterLabels.Capacity];
            var occupied = new Rect[MapWaterLabels.Capacity];
            var zoom = 5.1f;
            var count = MapWaterLabels.Choose(
                zoom,
                MapScope.Turkey,
                screen,
                names,
                10f,
                measured,
                18f,
                viewport,
                null,
                0,
                chosen,
                occupied);
            Assert.AreEqual(1, count);
            Assert.AreEqual(black, chosen[0]);

            var istanbul = new Rect(600f, 380f, 80f, 40f);
            count = MapWaterLabels.Choose(
                zoom,
                MapScope.Turkey,
                screen,
                names,
                10f,
                measured,
                18f,
                viewport,
                new[] { istanbul },
                1,
                chosen,
                occupied);
            Assert.AreEqual(0, count);

            screen[aegean] = new Vector2(900f, 500f);
            count = MapWaterLabels.Choose(
                zoom,
                MapScope.Turkey,
                screen,
                names,
                10f,
                measured,
                18f,
                viewport,
                new[] { istanbul },
                1,
                chosen,
                occupied);
            Assert.AreEqual(1, count);
            Assert.AreEqual(aegean, chosen[0]);
            Assert.IsFalse(MapCountryLabels.Overlaps(occupied[0], istanbul));
            Assert.GreaterOrEqual(occupied[0].xMin, viewport.xMin);
        }

        [Test]
        public void SafeArea_HidesLabelsThatLeaveTheViewport()
        {
            var waters = MapWaterCatalog.All;
            var screen = new Vector2[waters.Length];
            var names = new string[waters.Length];
            for (var i = 0; i < waters.Length; i++)
            {
                screen[i] = new Vector2(-4000f, -4000f);
                names[i] = waters[i].Id;
            }

            var atlantic = MapWaterCatalog.IndexOf("atlantic");
            screen[atlantic] = new Vector2(40f, 400f);
            var viewport = new Rect(120f, 80f, 1600f, 900f);
            var chosen = new int[MapWaterLabels.Capacity];
            var occupied = new Rect[MapWaterLabels.Capacity];
            var count = MapWaterLabels.Choose(
                2.1f,
                MapScope.World,
                screen,
                names,
                10f,
                null,
                18f,
                viewport,
                null,
                0,
                chosen,
                occupied);
            Assert.AreEqual(0, count);
            screen[atlantic] = new Vector2(800f, 500f);
            count = MapWaterLabels.Choose(
                2.1f,
                MapScope.World,
                screen,
                names,
                10f,
                null,
                18f,
                viewport,
                null,
                0,
                chosen,
                occupied);
            Assert.AreEqual(1, count);
            Assert.AreEqual(atlantic, chosen[0]);
        }

        [Test]
        public void Placement_IsDeterministicAndBounded()
        {
            var waters = MapWaterCatalog.All;
            var screen = new Vector2[waters.Length];
            var names = new string[waters.Length];
            for (var i = 0; i < waters.Length; i++)
            {
                screen[i] = new Vector2(400f + (i % 5) * 120f, 300f + (i / 5) * 80f);
                names[i] = waters[i].Id;
            }

            var viewport = new Rect(80f, 60f, 1800f, 1000f);
            var chosen = new int[MapWaterLabels.Capacity];
            var occupied = new Rect[MapWaterLabels.Capacity];
            var first = MapWaterLabels.Choose(2.2f, MapScope.World, screen, names, 9f, null, 16f, viewport, null, 0, chosen, occupied);
            var firstIds = Snapshot(chosen, first);
            var second = MapWaterLabels.Choose(2.2f, MapScope.World, screen, names, 9f, null, 16f, viewport, null, 0, chosen, occupied);
            Assert.AreEqual(first, second);
            CollectionAssert.AreEqual(firstIds, Snapshot(chosen, second));
            Assert.LessOrEqual(first, MapWaterLabels.Capacity);

            var watch = Stopwatch.StartNew();
            var last = 0;
            for (var i = 0; i < 200; i++)
                last = MapWaterLabels.Choose(2.2f, MapScope.World, screen, names, 9f, null, 16f, viewport, null, 0, chosen, occupied);
            watch.Stop();
            TestContext.WriteLine("MAP03A choose200=" + watch.Elapsed.TotalMilliseconds.ToString("0.00") + "ms visible=" + last);
            Assert.Less(watch.Elapsed.TotalMilliseconds, 50d);
        }

        [Test]
        public void TurkeyFit2868_ShowsAegeanClearOfIzmirAndTheRail()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            MapSceneSetup.EnsureLocalization();
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var camera = CameraIn(scene);
                var shell = MapCommandShell.Attach(scene);
                var countries = MapCountryLabelLayer.Attach(scene);
                var layer = MapWaterLabelLayer.Attach(scene);
                var safe = PhoneSafe();
                shell.Apply(2868f, 1320f, safe);
                Assert.IsTrue(shell.SelectRegion(1));
                PoseCapture(camera, MapScope.Turkey, 2868, 1320, safe);
                countries.Refresh(2868, 1320, safe, "tr", false);
                var visible = layer.Refresh(2868, 1320, safe, "tr", false);
                var placed = new MapWaterLabelLayer.Placed[MapWaterLabels.Capacity];
                var copied = layer.CopyPlaced(placed);
                Assert.AreEqual(visible, copied);
                TestContext.WriteLine(
                    "2868 turkey water=" + Ids(placed, copied)
                    + " aegean=" + MapWaterLabels.ReasonOf(MapWaterCatalog.IndexOf("aegean_sea"))
                    + " black=" + MapWaterLabels.ReasonOf(MapWaterCatalog.IndexOf("black_sea"))
                    + " med=" + MapWaterLabels.ReasonOf(MapWaterCatalog.IndexOf("mediterranean_sea"))
                    + " zoom=" + layer.Zoom);
                AssertPlaced(placed, copied, "black_sea", "Karadeniz");
                AssertPlaced(placed, copied, "aegean_sea", "Ege Denizi");
                AssertPlaced(placed, copied, "mediterranean_sea", "Akdeniz");
                AssertClearOfIzmir(placed, copied, camera, 2868, 1320, safe);

                visible = layer.Refresh(2868, 1320, safe, "de", false);
                copied = layer.CopyPlaced(placed);
                Assert.AreEqual(visible, copied);
                AssertPlaced(placed, copied, "aegean_sea", "Ägäisches Meer");
                var viewport = MapCommandLayout.Evaluate(2868f, 1320f, safe, false).Viewport;
                for (var i = 0; i < copied; i++)
                {
                    if (placed[i].Id != "aegean_sea")
                        continue;
                    Assert.GreaterOrEqual(placed[i].Bounds.xMin, viewport.xMin - 0.5f);
                    Assert.LessOrEqual(placed[i].Bounds.xMax, viewport.xMax + 0.5f);
                    Assert.GreaterOrEqual(placed[i].Bounds.xMin, viewport.xMin + MapWaterLabels.ViewportInset - 0.5f, "Aegean sat on the rail.");
                }

                PoseCapture(camera, MapScope.Turkey, 1920, 1080, Full(1920, 1080));
                countries.Refresh(1920, 1080, Full(1920, 1080), "tr", false);
                layer.Refresh(1920, 1080, Full(1920, 1080), "tr", false);
                copied = layer.CopyPlaced(placed);
                TestContext.WriteLine("1920 turkey water=" + Ids(placed, copied));
                AssertPlaced(placed, copied, "aegean_sea", "Ege Denizi");
                AssertPlaced(placed, copied, "black_sea", "Karadeniz");
                AssertPlaced(placed, copied, "mediterranean_sea", "Akdeniz");

                PoseCapture(camera, MapScope.Turkey, 932, 430, new Rect(32f, 16f, 868f, 398f));
                countries.Refresh(932, 430, new Rect(32f, 16f, 868f, 398f), "tr", false);
                var compact = new Rect(32f, 16f, 868f, 398f);
                layer.Refresh(932, 430, compact, "tr", false);
                copied = layer.CopyPlaced(placed);
                TestContext.WriteLine("932 turkey water=" + Ids(placed, copied));
                AssertPlaced(placed, copied, "aegean_sea", "Ege Denizi");
                AssertPlaced(placed, copied, "black_sea", "Karadeniz");
                AssertPlaced(placed, copied, "mediterranean_sea", "Akdeniz");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void WorldAtlantic_DoesNotOverlapRenderedCountryRects()
        {
            MapSceneSetup.EnsureLocalization();
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var camera = CameraIn(scene);
                var shell = MapCommandShell.Attach(scene);
                var countries = MapCountryLabelLayer.Attach(scene);
                var layer = MapWaterLabelLayer.Attach(scene);
                var safe = PhoneSafe();
                shell.Apply(2868f, 1320f, safe);
                Assert.IsTrue(shell.SelectRegion(0));
                PoseCapture(camera, MapScope.World, 2868, 1320, safe);
                var countryCount = countries.Refresh(2868, 1320, safe, "tr", false);
                var countryPlaced = new MapCountryLabelLayer.Placed[MapCountryLabels.Capacity];
                countryCount = countries.CopyPlaced(countryPlaced);
                layer.Refresh(2868, 1320, safe, "tr", false);
                var placed = new MapWaterLabelLayer.Placed[MapWaterLabels.Capacity];
                var copied = layer.CopyPlaced(placed);
                TestContext.WriteLine(
                    "world water=" + Ids(placed, copied)
                    + " atlantic=" + MapWaterLabels.ReasonOf(MapWaterCatalog.IndexOf("atlantic"))
                    + " countries=" + countryCount);
                if (HasId(placed, copied, "atlantic"))
                    AssertPlaced(placed, copied, "atlantic", "Atlas Okyanusu");
                for (var i = 0; i < copied; i++)
                {
                    for (var c = 0; c < countryCount; c++)
                    {
                        Assert.IsFalse(
                            MapCountryLabels.Overlaps(placed[i].Bounds, countryPlaced[c].Bounds),
                            placed[i].Id + " vs " + countryPlaced[c].Code);
                    }
                    Assert.IsFalse(placed[i].Raycast);
                }

                layer.Refresh(2868, 1320, safe, "de", false);
                copied = layer.CopyPlaced(placed);
                if (HasId(placed, copied, "atlantic"))
                {
                    AssertPlaced(placed, copied, "atlantic", "Atlantischer Ozean");
                    var viewport = MapCommandLayout.Evaluate(2868f, 1320f, safe, false).Viewport;
                    for (var i = 0; i < copied; i++)
                    {
                        if (placed[i].Id != "atlantic")
                            continue;
                        Assert.GreaterOrEqual(placed[i].Bounds.xMin, viewport.xMin - 0.5f);
                        Assert.LessOrEqual(placed[i].Bounds.xMax, viewport.xMax + 0.5f);
                    }
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EuropeSeas_AreHiddenByTheAuthoredCameraNotByBrokenLod()
        {
            MapSceneSetup.EnsureLocalization();
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var camera = CameraIn(scene);
                var shell = MapCommandShell.Attach(scene);
                var countries = MapCountryLabelLayer.Attach(scene);
                var layer = MapWaterLabelLayer.Attach(scene);
                var safe = PhoneSafe();
                shell.Apply(2868f, 1320f, safe);
                Assert.IsTrue(shell.SelectRegion(2));
                PoseCapture(camera, MapScope.Europe, 2868, 1320, safe);
                countries.Refresh(2868, 1320, safe, "tr", false);
                layer.Refresh(2868, 1320, safe, "tr", false);
                var ids = new[] { "north_sea", "baltic_sea", "mediterranean_sea", "adriatic_sea", "black_sea", "arctic" };
                for (var i = 0; i < ids.Length; i++)
                {
                    var index = MapWaterCatalog.IndexOf(ids[i]);
                    TestContext.WriteLine(ids[i] + "=" + MapWaterLabels.ReasonOf(index) + " zoom=" + layer.Zoom);
                    Assert.AreNotEqual(MapWaterLabels.Reject.Scope, MapWaterLabels.ReasonOf(index), ids[i]);
                }

                Assert.AreNotEqual(MapWaterLabels.Reject.Zoom, MapWaterLabels.ReasonOf(MapWaterCatalog.IndexOf("north_sea")));
                Assert.AreNotEqual(MapWaterLabels.Reject.Zoom, MapWaterLabels.ReasonOf(MapWaterCatalog.IndexOf("mediterranean_sea")));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Font_CoversTurkishEnglishAndGermanGlyphs()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Assert.IsNotNull(font);
            var sample = "Karadeniz Ege Denizi Akdeniz Marmara Ägäisches Meer Südchinesisches Meer Kızıldeniz Büyük Okyanus";
            font.RequestCharactersInTexture(sample, 16, FontStyle.Italic);
            for (var i = 0; i < sample.Length; i++)
            {
                if (sample[i] == ' ')
                    continue;
                Assert.IsTrue(font.HasCharacter(sample[i]), "missing glyph " + sample[i]);
            }
        }

        [Test]
        public void MapScene_PlacesWaterLabelsWithoutRaycastsOrSceneWrites()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
            MapSceneSetup.EnsureLocalization();
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var camera = CameraIn(scene);
                Assert.IsNotNull(camera);
                var aspect = 1920f / 1080f;
                camera.orthographic = true;
                camera.aspect = aspect;
                MapFrames.WorldPresentation(out var min, out var max);
                MapViewMath.Fit(min, max, aspect, MapFrames.WorldFocusPadding, out var center, out var size);
                camera.orthographicSize = size;
                camera.transform.position = new Vector3(center.x, center.y, MapCameraRig.CameraZ);

                var shell = MapCommandShell.Attach(scene);
                shell.Apply(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
                Assert.IsTrue(shell.SelectRegion(0));
                center = new Vector2(camera.transform.position.x, camera.transform.position.y);
                size = camera.orthographicSize;
                var countries = MapCountryLabelLayer.Attach(scene);
                countries.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "tr", false);
                var layer = MapWaterLabelLayer.Attach(scene);
                var again = MapWaterLabelLayer.Attach(scene);
                Assert.AreSame(layer, again);
                Assert.AreEqual(HideFlags.DontSave, layer.gameObject.hideFlags);

                var visible = layer.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "tr", false);
                var placed = new MapWaterLabelLayer.Placed[MapWaterLabels.Capacity];
                var copied = layer.CopyPlaced(placed);
                Assert.AreEqual(visible, copied);
                Assert.Greater(visible, 0);
                Assert.LessOrEqual(visible, MapWaterLabels.Capacity);
                AssertPlaced(placed, copied, "atlantic", "Atlas Okyanusu");
                AssertMissing(placed, copied, "black_sea");
                AssertScreen(placed, copied, center, size, 1920f, 1080f);
                for (var i = 0; i < copied; i++)
                    Assert.IsFalse(placed[i].Raycast, placed[i].Id);

                var english = layer.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "en", false);
                copied = layer.CopyPlaced(placed);
                Assert.AreEqual(english, copied);
                AssertPlaced(placed, copied, "atlantic", "Atlantic Ocean");

                Assert.IsTrue(shell.SelectRegion(1));
                center = new Vector2(camera.transform.position.x, camera.transform.position.y);
                size = camera.orthographicSize;
                countries.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "tr", false);
                visible = layer.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "tr", false);
                copied = layer.CopyPlaced(placed);
                Assert.AreEqual(visible, copied);
                AssertPlaced(placed, copied, "black_sea", "Karadeniz");
                AssertMissing(placed, copied, "atlantic");
                AssertMissing(placed, copied, "marmara_sea");
                AssertScreen(placed, copied, center, size, 1920f, 1080f);

                var german = layer.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "de", false);
                copied = layer.CopyPlaced(placed);
                Assert.AreEqual(german, copied);
                AssertPlaced(placed, copied, "mediterranean_sea", "Mittelmeer");

                var closest = MapFrames.MinimumOrthographicSize(1920f / 1080f);
                camera.orthographicSize = closest;
                Assert.AreEqual(closest, camera.orthographicSize, 0.0001f);
                countries.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "tr", false);
                visible = layer.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "tr", false);
                copied = layer.CopyPlaced(placed);
                Assert.AreEqual(visible, copied);
                var closeZoom = layer.Zoom;
                Assert.IsTrue(MapWaterLabels.Shows(Water("marmara_sea"), closeZoom, MapScope.Turkey), "close zoom " + closeZoom);
                var marmaraShown = HasId(placed, copied, "marmara_sea");
                if (marmaraShown)
                    AssertPlaced(placed, copied, "marmara_sea", "Marmara Denizi");
                TestContext.WriteLine("turkey-close water=" + Ids(placed, copied) + " marmara=" + marmaraShown);

                Assert.AreEqual(SceneSha, Sha(ScenePath));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static MapWaterCatalog.Entry Water(string id)
        {
            var index = MapWaterCatalog.IndexOf(id);
            Assert.GreaterOrEqual(index, 0, id);
            return MapWaterCatalog.All[index];
        }

        static string[] Snapshot(int[] chosen, int count)
        {
            var ids = new string[count];
            for (var i = 0; i < count; i++)
                ids[i] = MapWaterCatalog.All[chosen[i]].Id;
            return ids;
        }

        static void AssertPlaced(MapWaterLabelLayer.Placed[] placed, int count, string id, string text)
        {
            for (var i = 0; i < count; i++)
            {
                if (placed[i].Id == id)
                {
                    Assert.AreEqual(text, placed[i].Text, id);
                    return;
                }
            }

            Assert.Fail("missing water label " + id);
        }

        static void AssertMissing(MapWaterLabelLayer.Placed[] placed, int count, string id)
        {
            for (var i = 0; i < count; i++)
                Assert.AreNotEqual(id, placed[i].Id);
        }

        static bool HasId(MapWaterLabelLayer.Placed[] placed, int count, string id)
        {
            for (var i = 0; i < count; i++)
            {
                if (placed[i].Id == id)
                    return true;
            }

            return false;
        }

        static string Ids(MapWaterLabelLayer.Placed[] placed, int count)
        {
            var text = new System.Text.StringBuilder();
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                    text.Append(',');
                text.Append(placed[i].Id);
            }

            return text.ToString();
        }

        static void AssertScreen(MapWaterLabelLayer.Placed[] placed, int count, Vector2 center, float size, float width, float height)
        {
            var units = MapMarkerLod.UnitsPerPixel(size, height);
            for (var i = 0; i < count; i++)
            {
                var entry = Water(placed[i].Id);
                var world = MapProjection.Project(entry.Latitude, entry.Longitude);
                var screen = MapMarkerLod.ToScreen(world, center, units, width, height);
                Assert.AreEqual(screen.x, placed[i].Screen.x, 0.01f, placed[i].Id);
                Assert.AreEqual(screen.y, placed[i].Screen.y, 0.01f, placed[i].Id);
            }
        }

        static Rect PhoneSafe()
        {
            return new Rect(186f, 60f, 2496f, 1200f);
        }

        static Rect Full(int width, int height)
        {
            return new Rect(0f, 0f, width, height);
        }

        static void PoseCapture(Camera camera, MapScope scope, int width, int height, Rect safe)
        {
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
            if (scope == MapScope.Turkey)
                MapCameraRig.FitCitiesInside(center, size, aspect, width, height, safe, worldMin, worldMax, out center, out size);

            camera.orthographic = true;
            camera.aspect = aspect;
            camera.orthographicSize = size;
            camera.transform.position = new Vector3(center.x, center.y, MapCameraRig.CameraZ);
        }

        static void AssertClearOfIzmir(
            MapWaterLabelLayer.Placed[] placed,
            int count,
            Camera camera,
            int width,
            int height,
            Rect safe)
        {
            var aspect = width / (float)height;
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkeyFrame);
            var scale = MapHudLayout.CanvasScale(width, height);
            var together = new bool[MapCities.All.Length * MapCities.All.Length];
            var parent = new int[MapCities.All.Length];
            var obstacles = new Rect[MapCities.All.Length * 2];
            var obstacleCount = MapCountryLabels.CityObstacles(
                new Vector2(camera.transform.position.x, camera.transform.position.y),
                camera.orthographicSize,
                width,
                height,
                turkeyFrame,
                scale,
                together,
                parent,
                obstacles);
            var viewport = MapCommandLayout.Evaluate(width, height, safe, false).Viewport;
            for (var i = 0; i < count; i++)
            {
                if (placed[i].Id != "aegean_sea")
                    continue;
                Assert.Greater(placed[i].Bounds.xMin, viewport.xMin, "Aegean overlapped the rail.");
                for (var o = 0; o < obstacleCount; o++)
                    Assert.IsFalse(MapCountryLabels.Overlaps(placed[i].Bounds, obstacles[o]), "Aegean overlapped a city plate.");
            }
        }

        static Camera CameraIn(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var rig = roots[i].GetComponentInChildren<MapCameraRig>(true);
                if (rig != null)
                    return rig.GetComponent<Camera>();
            }

            return null;
        }

        static string Value(UnityEditor.Localization.StringTableCollection collection, string code, string key)
        {
            foreach (var table in collection.StringTables)
            {
                if (table.LocaleIdentifier.Code != code)
                    continue;
                var entry = table.GetEntry(key);
                return entry != null ? entry.Value : null;
            }

            return null;
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
