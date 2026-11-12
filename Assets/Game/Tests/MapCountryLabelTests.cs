using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LogistiCore.Tests
{
    public sealed class MapCountryLabelTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";

        [Test]
        public void Names_UseTheNaturalEarthFieldsForEachLanguage()
        {
            var turkey = Country("TUR");
            Assert.AreEqual("Turkey", MapCountryLabels.Name(turkey, "en"));
            Assert.AreEqual("Türkiye", MapCountryLabels.Name(turkey, "tr"));
            Assert.AreEqual("Türkei", MapCountryLabels.Name(turkey, "de"));
            Assert.AreEqual("Türkiye", MapCountryLabels.Name(turkey, "tr-TR"));

            var china = Country("CHN");
            Assert.AreEqual("China", MapCountryLabels.Name(china, "en"));
            Assert.AreEqual("Çin Halk Cumhuriyeti", MapCountryLabels.Name(china, "tr"));
            Assert.AreEqual("Volksrepublik China", MapCountryLabels.Name(china, "de"));

            var france = Country("FRA");
            Assert.AreEqual("France", MapCountryLabels.Name(france, "en"));
            Assert.AreEqual("Fransa", MapCountryLabels.Name(france, "tr"));
            Assert.AreEqual("Frankreich", MapCountryLabels.Name(france, "de"));

            Assert.AreEqual("Norveç", MapCountryLabels.Name(Country("NOR"), "tr"));
            Assert.AreEqual("Norwegen", MapCountryLabels.Name(Country("NOR"), "de"));
            Assert.AreEqual("Kosova", MapCountryLabels.Name(Country("KOS"), "tr"));
            Assert.AreEqual("Kuzey Kıbrıs Türk Cumhuriyeti", MapCountryLabels.Name(Country("CYN"), "tr"));
            Assert.AreEqual(242, MapCountryCatalog.All.Length);
        }

        [Test]
        public void Anchors_UseNaturalEarthPointsRatherThanAMultiPartCentre()
        {
            var france = Country("FRA");
            Assert.Greater(france.Longitude, -5f);
            Assert.Less(france.Longitude, 10f);
            Assert.Greater(france.Latitude, 42f);
            Assert.Less(france.Latitude, 51f);

            var unitedStates = Country("USA");
            Assert.Greater(unitedStates.Longitude, -125f);
            Assert.Less(unitedStates.Longitude, -70f);
            Assert.Greater(unitedStates.Latitude, 25f);
            Assert.Less(unitedStates.Latitude, 50f);

            var russia = Country("RUS");
            Assert.Greater(russia.Longitude, 20f);
            Assert.Less(russia.Longitude, 90f);

            var indonesia = Country("IDN");
            Assert.Greater(indonesia.Longitude, 95f);
            Assert.Less(indonesia.Longitude, 120f);
            Assert.Greater(indonesia.Latitude, -8f);
            Assert.Less(indonesia.Latitude, 6f);

            var chile = Country("CHL");
            Assert.Less(chile.Latitude, -20f);
            Assert.Greater(chile.Longitude, -76f);
            Assert.Less(chile.Longitude, -68f);
        }

        [Test]
        public void Zoom_FollowsNaturalEarthLabelWindows()
        {
            var aspect = 1920f / 1080f;
            var world = MapFrames.MaximumOrthographicSize(aspect);
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkey);

            var worldZoom = MapCountryLabels.Zoom(world, world, turkey);
            var europe = FrameSize(-12d, 42d, 30d, 62d, aspect, 1.08f);
            var europeZoom = MapCountryLabels.Zoom(europe, world, turkey);
            var cityZoom = MapCountryLabels.Zoom(turkey, world, turkey);

            Assert.AreEqual(-1f, cityZoom, 0.001f);
            Assert.AreEqual(0, MapCountryLabels.CountShown(cityZoom));
            Assert.Greater(worldZoom, 0f);
            Assert.IsTrue(MapCountryLabels.Shows(Country("TUR"), worldZoom));
            Assert.IsTrue(MapCountryLabels.Shows(Country("FRA"), worldZoom));
            Assert.IsFalse(MapCountryLabels.Shows(Country("BEL"), worldZoom));
            Assert.IsFalse(MapCountryLabels.Shows(Country("ALB"), worldZoom));
            Assert.Greater(europeZoom, worldZoom);
            Assert.IsTrue(MapCountryLabels.Shows(Country("BEL"), europeZoom));
            Assert.IsTrue(MapCountryLabels.Shows(Country("GRC"), europeZoom));
            Assert.Less(MapCountryLabels.CountShown(worldZoom), 50);
            Assert.Greater(MapCountryLabels.CountShown(europeZoom), MapCountryLabels.CountShown(worldZoom));
            Assert.LessOrEqual(MapCountryLabels.CountShown(worldZoom), MapCountryLabels.Capacity);
        }

        [Test]
        public void Collision_KeepsTheMoreImportantAnchorInsideTheViewport()
        {
            var screen = new Vector2[MapCountryCatalog.All.Length];
            for (var i = 0; i < screen.Length; i++)
                screen[i] = new Vector2(-4000f, -4000f);

            var france = MapCountryCatalog.IndexOf("FRA");
            var italy = MapCountryCatalog.IndexOf("ITA");
            var albania = MapCountryCatalog.IndexOf("ALB");
            screen[france] = new Vector2(500f, 400f);
            screen[italy] = new Vector2(500f, 400f);
            screen[albania] = new Vector2(40f, 400f);
            var viewport = new Rect(100f, 80f, 1600f, 900f);
            var chosen = new int[MapCountryLabels.Capacity];
            var occupied = new Rect[MapCountryLabels.Capacity];
            var zoom = Country("ALB").MinLabel + 0.1f;
            var count = MapCountryLabels.Choose(zoom, "en", screen, 10f, null, 18f, viewport, chosen, occupied);

            Assert.Greater(count, 0);
            Assert.LessOrEqual(count, MapCountryLabels.Capacity);
            Assert.IsTrue(Contains(chosen, count, france));
            Assert.IsFalse(Contains(chosen, count, italy));
            Assert.IsFalse(Contains(chosen, count, albania), "Albania's point sits in the rail, outside the viewport.");

            screen[albania] = new Vector2(900f, 500f);
            count = MapCountryLabels.Choose(zoom, "tr", screen, 10f, null, 18f, viewport, chosen, occupied);
            Assert.IsTrue(Contains(chosen, count, france));
            Assert.IsTrue(Contains(chosen, count, albania));
            Assert.AreEqual("Fransa", MapCountryLabels.Name(Country("FRA"), "tr"));
            for (var i = 0; i < count; i++)
            {
                Assert.IsFalse(MapCountryLabels.Overlaps(occupied[i], new Rect(0f, 0f, 100f, 1080f)));
                Assert.GreaterOrEqual(occupied[i].xMin, viewport.xMin);
                Assert.LessOrEqual(occupied[i].xMax, viewport.xMax);
            }
        }

        [Test]
        public void Pan_MovesTheAnchorByTheCameraAndZoomHoldsATinyStep()
        {
            var aspect = 16f / 9f;
            var world = MapFrames.MaximumOrthographicSize(aspect);
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkey);
            var zoom = MapCountryLabels.Zoom(world, world, turkey);
            var held = MapCountryLabels.HoldZoom(zoom, world, world * 1.02f, MapCountryLabels.Zoom(world * 1.02f, world, turkey));
            Assert.AreEqual(zoom, held, 0.0001f);
            var stepped = MapCountryLabels.HoldZoom(zoom, world, world * 0.8f, MapCountryLabels.Zoom(world * 0.8f, world, turkey));
            Assert.Greater(stepped, zoom);
            Assert.AreEqual(-1f, MapCountryLabels.HoldZoom(zoom, world, turkey, -1f), 0.001f);

            MapFrames.WorldPresentation(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.WorldFocusPadding, out var center, out var size);
            var france = Country("FRA");
            var anchor = MapProjection.Project(france.Latitude, france.Longitude);
            var units = MapMarkerLod.UnitsPerPixel(size, 1080f);
            var before = MapMarkerLod.ToScreen(anchor, center, units, 1920f, 1080f);
            var after = MapMarkerLod.ToScreen(anchor, center + new Vector2(10f, 0f), units, 1920f, 1080f);
            Assert.AreEqual(-10f / units, after.x - before.x, 0.01f);
            Assert.AreEqual(0f, after.y - before.y, 0.01f);
        }

        [Test]
        public void Choose_StaysBoundedAcrossRepeatedCalls()
        {
            var aspect = 1920f / 1080f;
            var world = MapFrames.MaximumOrthographicSize(aspect);
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkey);
            MapFrames.WorldPresentation(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.WorldFocusPadding, out var center, out var size);
            var screen = Project(center, size, 1920f, 1080f);
            var viewport = new Rect(180f, 70f, 1560f, 940f);
            var chosen = new int[MapCountryLabels.Capacity];
            var occupied = new Rect[MapCountryLabels.Capacity];
            var zoom = MapCountryLabels.Zoom(size, world, turkey);
            var watch = Stopwatch.StartNew();
            var last = 0;
            for (var i = 0; i < 200; i++)
                last = MapCountryLabels.Choose(zoom, "tr", screen, 12f, null, 18f, viewport, chosen, occupied);
            watch.Stop();
            var avoid = Stopwatch.StartNew();
            var resolved = new Vector2[screen.Length];
            var held = new Vector2[screen.Length];
            var obstacles = new Rect[16];
            var together = new bool[MapCities.All.Length * MapCities.All.Length];
            var parent = new int[MapCities.All.Length];
            var avoided = 0;
            for (var i = 0; i < 200; i++)
            {
                var blocks = MapCountryLabels.CityObstacles(center, size, 1920f, 1080f, turkey, 1f, together, parent, obstacles);
                var chosenCount = MapCountryLabels.Choose(zoom, "tr", screen, 12f, null, 18f, viewport, chosen, occupied);
                avoided = MapCountryLabels.AvoidObstacles(
                    chosenCount,
                    chosen,
                    screen,
                    "tr",
                    12f,
                    null,
                    18f,
                    viewport,
                    occupied,
                    obstacles,
                    blocks,
                    MapCountryLabels.MaxNudgeWorld / MapMarkerLod.UnitsPerPixel(size, 1080f),
                    resolved,
                    held);
            }

            avoid.Stop();
            TestContext.WriteLine(
                "MAP02A choose200=" + watch.Elapsed.TotalMilliseconds.ToString("0.00")
                + "ms visible=" + last
                + " avoid200=" + avoid.Elapsed.TotalMilliseconds.ToString("0.00")
                + "ms kept=" + avoided
                + " eligible=" + MapCountryLabels.CountShown(zoom));
            Assert.Greater(last, 0);
            Assert.LessOrEqual(last, MapCountryLabels.Capacity);
            Assert.Less(last, 80);
        }

        [Test]
        public void Australia_StaysOnItsAnchorWhileTheCameraFliesToOceania()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                var camera = CameraIn(scene);
                var layer = MapCountryLabelLayer.Attach(scene);
                var safe = new Rect(0f, 0f, 1920f, 1080f);
                Pose(camera, MapScope.World, 1920f, 1080f, safe, out var worldCenter, out var worldSize);
                layer.Refresh(1920, 1080, safe, "tr", false);
                var worldLabel = LabelScreen(layer, "AUS");
                Assert.Greater(worldLabel.x, 1000f, "Australia was not drawn on the world frame.");
                AssertNearAnchor(worldLabel, "AUS", worldCenter, worldSize, 1920f, 1080f);

                Pose(camera, MapScope.Oceania, 1920f, 1080f, safe, out var endCenter, out var endSize);
                var endUnits = MapMarkerLod.UnitsPerPixel(endSize, 1080f);
                var endAnchor = MapMarkerLod.ToScreen(Anchor("AUS"), endCenter, endUnits, 1920f, 1080f);
                var samples = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
                Vector2 previous = worldLabel;
                for (var i = 0; i < samples.Length; i++)
                {
                    MapCameraRig.SampleRegionTransition(worldCenter, worldSize, endCenter, endSize, samples[i], out var center, out var size);
                    camera.orthographicSize = size;
                    camera.transform.position = new Vector3(center.x, center.y, MapCameraRig.CameraZ);
                    layer.Refresh(1920, 1080, safe, "tr", false);
                    var shown = LabelScreen(layer, "AUS");
                    if (shown == Vector2.zero)
                        continue;

                    AssertNearAnchor(shown, "AUS", center, size, 1920f, 1080f);
                    if (samples[i] <= 0.5f)
                        Assert.Greater(Vector2.Distance(shown, endAnchor), 80f, "t=" + samples[i]);
                    if (i > 0 && previous != Vector2.zero)
                        Assert.LessOrEqual(Vector2.Distance(shown, endAnchor), Vector2.Distance(previous, endAnchor) + 1f, "t=" + samples[i]);
                    previous = shown;
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void Pose(Camera camera, MapScope scope, float width, float height, Rect safe, out Vector2 center, out float size)
        {
            var aspect = width / height;
            camera.orthographic = true;
            camera.aspect = aspect;
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            if (scope == MapScope.World)
                MapViewMath.Fit(worldMin, worldMax, aspect, MapFrames.WorldFocusPadding, out center, out size);
            else
                MapCameraRig.FrameScope(scope, aspect, width, height, safe, worldMin, worldMax, out center, out size);

            size = MapViewMath.ClampSize(size, MapFrames.MinimumOrthographicSize(aspect), MapFrames.MaximumOrthographicSize(aspect));
            MapCameraRig.PresentationTravel(scope, size, width, height, safe, worldMin, worldMax, out var travelMin, out var travelMax);
            center = MapViewMath.ClampCenter(center, size, aspect, travelMin, travelMax);
            camera.orthographicSize = size;
            camera.transform.position = new Vector3(center.x, center.y, MapCameraRig.CameraZ);
        }

        static Vector2 Anchor(string code)
        {
            var entry = Country(code);
            return MapProjection.Project(entry.Latitude, entry.Longitude);
        }

        static Vector2 LabelScreen(MapCountryLabelLayer layer, string code)
        {
            var placed = new MapCountryLabelLayer.Placed[MapCountryLabels.Capacity];
            var count = layer.CopyPlaced(placed);
            for (var i = 0; i < count; i++)
            {
                if (placed[i].Code == code)
                    return placed[i].Screen;
            }

            return Vector2.zero;
        }

        static void AssertNearAnchor(Vector2 shown, string code, Vector2 center, float size, float width, float height)
        {
            if (shown == Vector2.zero)
                return;

            var units = MapMarkerLod.UnitsPerPixel(size, height);
            var expected = MapMarkerLod.ToScreen(Anchor(code), center, units, width, height);
            var limit = Mathf.Max(MapCountryLabels.MaxNudgeWorld, MapCountryLabels.MaxPresentationWorld);
            var nudge = units > 0.000001f ? limit / units + 1.5f : 2f;
            Assert.AreEqual(expected.x, shown.x, nudge, code);
            Assert.AreEqual(expected.y, shown.y, nudge, code);
        }

        [Test]
        public void MapScene_PlacesLabelsWithoutRaycastsOrSceneWrites()
        {
            Assert.AreEqual(SceneSha, Sha(ScenePath));
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
                var layer = MapCountryLabelLayer.Attach(scene);
                var again = MapCountryLabelLayer.Attach(scene);
                Assert.AreSame(layer, again);
                Assert.AreEqual(HideFlags.DontSave, layer.gameObject.hideFlags);

                var visible = layer.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "tr", false);
                var pooled = layer.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length;
                var placed = new MapCountryLabelLayer.Placed[MapCountryLabels.Capacity];
                var copied = layer.CopyPlaced(placed);
                Assert.AreEqual(visible, copied);
                Assert.Greater(visible, 0);
                Assert.LessOrEqual(visible, MapCountryLabels.Capacity);
                AssertMissing(placed, copied, "TUR");
                AssertPlaced(placed, copied, "FRA", "Fransa");
                AssertScreen(placed, copied, center, size, 1920f, 1080f);
                AssertClearOfCities(placed, copied, center, size, 1920f, 1080f, 1f);
                AssertAggregateCaption(layer, "Türkiye", center, size, 1920f, 1080f, placed, copied);

                var english = layer.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "en", false);
                copied = layer.CopyPlaced(placed);
                Assert.AreEqual(english, copied);
                AssertMissing(placed, copied, "TUR");
                AssertPlaced(placed, copied, "FRA", "France");
                AssertAggregateCaption(layer, "Turkey", center, size, 1920f, 1080f, placed, copied);

                var german = layer.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "de", false);
                copied = layer.CopyPlaced(placed);
                Assert.AreEqual(german, copied);
                AssertMissing(placed, copied, "TUR");
                AssertPlaced(placed, copied, "FRA", "Frankreich");
                AssertAggregateCaption(layer, "Türkei", center, size, 1920f, 1080f, placed, copied);
                Assert.AreEqual(pooled, layer.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length);
                AssertUnique(placed, copied);
                for (var i = 0; i < copied; i++)
                    Assert.IsFalse(placed[i].Raycast, placed[i].Code);

                var markers = MarkerLayer(scene);
                markers.ApplyViewport(1920, 1080, true);
                Assert.AreEqual(34f, MapMarkerLod.AggregateScreenPixels, 0.01f);
                Assert.AreEqual(22f, MapMarkerLod.AggregateHitRadius, 0.01f);
                Assert.IsTrue(markers.TryAggregateHitPoint(out var hit, out var members));
                Assert.AreEqual(8, members);
                Assert.IsTrue(markers.TrySelect(hit, out var selectedCity));
                Assert.IsNull(selectedCity);
                Assert.AreEqual(MapScope.Turkey, camera.GetComponent<MapCameraRig>().Scope);
                Assert.IsFalse(layer.AggregateCaption.Raycast);

                var graphics = layer.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                for (var i = 0; i < graphics.Length; i++)
                    Assert.IsFalse(graphics[i].raycastTarget, graphics[i].name);
                Assert.AreEqual(0, layer.GetComponentsInChildren<Collider>(true).Length);
                Assert.AreEqual(0, layer.GetComponentsInChildren<Collider2D>(true).Length);
                Assert.AreEqual(8, Count<MapCityMarker>(scene));
                Assert.AreEqual(1, Count<EventSystem>(scene));

                MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
                MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out var turkeyCenter, out var turkeySize);
                MapFrames.WorldPresentation(out var worldMin, out var worldMax);
                MapCameraRig.FitCitiesInside(
                    turkeyCenter,
                    turkeySize,
                    aspect,
                    1920f,
                    1080f,
                    new Rect(0f, 0f, 1920f, 1080f),
                    worldMin,
                    worldMax,
                    out turkeyCenter,
                    out turkeySize);
                camera.orthographicSize = turkeySize;
                camera.transform.position = new Vector3(turkeyCenter.x, turkeyCenter.y, MapCameraRig.CameraZ);
                Assert.AreEqual(0, layer.Refresh(1920, 1080, new Rect(0f, 0f, 1920f, 1080f), "tr", false));
                Assert.IsFalse(layer.AggregateCaption.Visible);
                Assert.AreEqual(8, Count<MapCityMarker>(scene));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
        }

        [Test]
        public void WorldAggregate_HidesTheCountryNameThatSitsOnTheBadge()
        {
            var view = WorldView(1920f, 1080f);
            var obstacles = Obstacles(view, 1f);
            Assert.GreaterOrEqual(obstacles.Count, 1);
            var aggregate = Largest(obstacles.Rects, obstacles.Count);
            Assert.AreEqual(MapMarkerLod.AggregateScreenPixels + MapCountryLabels.MarkerPadding * 2f, aggregate.width, 0.1f);
            Assert.AreEqual(34f, MapMarkerLod.AggregateScreenPixels, 0.01f);
            Assert.AreEqual(22f, MapMarkerLod.AggregateHitRadius, 0.01f);

            foreach (var locale in new[] { "tr", "en", "de" })
            {
                var placed = Place(view, locale, obstacles.Rects, obstacles.Count, null);
                Assert.IsFalse(Has(placed, "TUR"), locale);
                Assert.IsTrue(Has(placed, "FRA"), locale);
                Assert.IsFalse(AnyHit(placed, obstacles.Rects, obstacles.Count), locale);
                var france = Find(placed, "FRA");
                var anchor = ScreenOf("FRA", view);
                Assert.AreEqual(anchor.x, france.Screen.x, 0.6f, locale);
                Assert.AreEqual(anchor.y, france.Screen.y, 0.6f, locale);
                var name = MapCountryLabels.Name(Country("TUR"), locale);
                Assert.Greater(MapCountryLabels.Width(name, 12f), MapCountryLabels.Width("TR", 12f));
            }
        }

        [Test]
        public void EuropeMarkers_MoveTurkiyeOffTheVisibleCity()
        {
            var view = EuropeView(1920f, 1080f);
            var obstacles = Obstacles(view, 1f);
            Assert.Greater(obstacles.Count, 1, "Europe scale shows individual cities and at least one aggregate.");
            var before = Place(view, "tr", System.Array.Empty<Rect>(), 0, null);
            var turkeyBefore = Find(before, "TUR");
            Assert.IsTrue(Hits(turkeyBefore.Bounds, obstacles.Rects, obstacles.Count), "The unshifted Türkiye label overlaps a visible city marker.");

            var after = Place(view, "tr", obstacles.Rects, obstacles.Count, null);
            Assert.IsTrue(Has(after, "TUR"));
            var turkey = Find(after, "TUR");
            Assert.IsFalse(Hits(turkey.Bounds, obstacles.Rects, obstacles.Count));
            var anchor = ScreenOf("TUR", view);
            var shift = Vector2.Distance(turkey.Screen, anchor);
            var maxNudge = MapCountryLabels.MaxNudgeWorld / view.Units;
            Assert.Greater(shift, 1f);
            Assert.LessOrEqual(shift, maxNudge + 0.5f);
            var viewport = MapCommandLayout.Evaluate(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f), false).Viewport;
            Assert.GreaterOrEqual(turkey.Bounds.xMin, viewport.xMin);
            Assert.GreaterOrEqual(turkey.Bounds.yMin, viewport.yMin);
            Assert.LessOrEqual(turkey.Bounds.xMax, viewport.xMax);
            Assert.LessOrEqual(turkey.Bounds.yMax, viewport.yMax);
            Assert.AreEqual("Türkiye", turkey.Text);
            Assert.IsTrue(Has(Place(view, "en", obstacles.Rects, obstacles.Count, null), "TUR"));
            Assert.IsTrue(Has(Place(view, "de", obstacles.Rects, obstacles.Count, null), "TUR"));
        }

        [Test]
        public void PlateAndBlockedNudge_HideTheNameWithoutMovingTheMarker()
        {
            var screen = new Vector2[MapCountryCatalog.All.Length];
            for (var i = 0; i < screen.Length; i++)
                screen[i] = new Vector2(-4000f, -4000f);
            var france = MapCountryCatalog.IndexOf("FRA");
            var spain = MapCountryCatalog.IndexOf("ESP");
            screen[france] = new Vector2(400f, 400f);
            screen[spain] = new Vector2(700f, 400f);
            var viewport = new Rect(80f, 60f, 1200f, 800f);
            var plate = new Rect(360f, 430f, 120f, 28f);
            var marker = new Rect(382f, 382f, 36f, 36f);
            var obstacles = new[] { marker, plate };
            var placed = PlaceAt(screen, "en", 2.5f, 24f, 10f, 18f, viewport, obstacles, 2, null);
            Assert.IsFalse(Has(placed, "FRA"));
            Assert.IsTrue(Has(placed, "ESP"));
            var spainLabel = Find(placed, "ESP");
            Assert.AreEqual(screen[spain].x, spainLabel.Screen.x, 0.1f);
            Assert.AreEqual(screen[spain].y, spainLabel.Screen.y, 0.1f);
            Assert.IsFalse(Hits(spainLabel.Bounds, obstacles, 2));
        }

        [Test]
        public void Nudge_StaysPutAcrossATinyPanAndReturnsWhenTheMarkerClears()
        {
            var view = EuropeView(1920f, 1080f);
            var obstacles = Obstacles(view, 1f);
            var held = new Vector2[MapCountryCatalog.All.Length];
            var first = Place(view, "tr", obstacles.Rects, obstacles.Count, held);
            var again = Place(view, "tr", obstacles.Rects, obstacles.Count, held);
            var turkey = Find(first, "TUR");
            var repeat = Find(again, "TUR");
            Assert.AreEqual(turkey.Screen.x, repeat.Screen.x, 0.01f);
            Assert.AreEqual(turkey.Screen.y, repeat.Screen.y, 0.01f);

            var shifted = view;
            shifted.Center += new Vector2(12f, -6f);
            var movedObstacles = Obstacles(shifted, 1f);
            var moved = Place(shifted, "tr", movedObstacles.Rects, movedObstacles.Count, held);
            var turkeyMoved = Find(moved, "TUR");
            var anchorDelta = ScreenOf("TUR", shifted) - ScreenOf("TUR", view);
            var labelDelta = turkeyMoved.Screen - turkey.Screen;
            Assert.AreEqual(anchorDelta.x, labelDelta.x, 0.6f);
            Assert.AreEqual(anchorDelta.y, labelDelta.y, 0.6f);

            var clear = Place(view, "tr", System.Array.Empty<Rect>(), 0, held);
            Assert.AreEqual(0f, Vector2.Distance(Find(clear, "TUR").Screen, ScreenOf("TUR", view)), 0.6f);
        }

        [Test]
        public void TurkeyScale_StillHidesCountryNamesAndKeepsEightCities()
        {
            var aspect = 1920f / 1080f;
            var world = MapFrames.MaximumOrthographicSize(aspect);
            MapFrames.Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.TurkeyFocusPadding, out var center, out var size);
            Assert.AreEqual(-1f, MapCountryLabels.Zoom(size, world, size), 0.001f);
            var view = new View(center, size, 1920f, 1080f, MapMarkerLod.UnitsPerPixel(size, 1080f));
            var obstacles = Obstacles(view, 1f);
            Assert.GreaterOrEqual(obstacles.Count, 8);
            var placed = Place(view, "tr", obstacles.Rects, obstacles.Count, null);
            Assert.AreEqual(0, placed.Count);
            Assert.AreEqual(8, MapCities.All.Length);
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame, 0.0001f);
            var cluster = ClusterOf(view);
            Assert.IsFalse(cluster.Full);
            Assert.IsFalse(MapCountryLabels.AggregateCaptionApplies(cluster.Full, false));
        }

        [Test]
        public void WorldAggregateCaption_SitsBesideTheBadgeInEachLanguage()
        {
            Assert.AreEqual(34f, MapMarkerLod.AggregateScreenPixels, 0.01f);
            Assert.AreEqual(22f, MapMarkerLod.AggregateHitRadius, 0.01f);
            Assert.AreEqual(8f, MapCountryLabels.AggregateCaptionGap, 0.01f);
            foreach (var locale in new[] { "tr", "en", "de" })
            {
                var expected = locale == "tr" ? "Türkiye" : locale == "de" ? "Türkei" : "Turkey";
                AssertCaption(WorldView(1920f, 1080f), locale, expected, new Rect(0f, 0f, 1920f, 1080f));
                AssertCaption(WorldView(2868f, 1320f), locale, expected, new Rect(186f, 60f, 2496f, 1200f));
            }
        }

        [Test]
        public void WorldAggregateCaption_HidesWhenEverySideIsBlocked()
        {
            var badge = new Vector2(400f, 300f);
            var width = MapCountryLabels.Width("Türkiye", 12f);
            var line = 18f;
            var viewport = new Rect(0f, 0f, 800f, 600f);
            var alongX = width * 0.5f + MapMarkerLod.AggregateScreenPixels * 0.5f + MapCountryLabels.AggregateCaptionGap;
            var alongY = line * 0.5f + MapMarkerLod.AggregateScreenPixels * 0.5f + MapCountryLabels.AggregateCaptionGap;
            var blockers = new[]
            {
                MapCountryLabels.LabelRect(badge + new Vector2(alongX, 0f), width, line),
                MapCountryLabels.LabelRect(badge + new Vector2(-alongX, 0f), width, line),
                MapCountryLabels.LabelRect(badge + new Vector2(0f, alongY), width, line),
                MapCountryLabels.LabelRect(badge + new Vector2(0f, -alongY), width, line)
            };
            var side = MapCountryLabels.PlaceAggregateCaption(badge, width, line, viewport, blockers, 4, -1, out _, out _);
            Assert.AreEqual(-1, side);
            Assert.AreEqual(34f, MapMarkerLod.AggregateScreenPixels, 0.01f);
            Assert.AreEqual(22f, MapMarkerLod.AggregateHitRadius, 0.01f);
        }

        [Test]
        public void WorldAggregateCaption_StaysOffTheCountryNameAndTheHud()
        {
            var europe = EuropeView(1920f, 1080f);
            var europeCluster = ClusterOf(europe);
            var europePlaced = Place(europe, "tr", europeCluster.Rects, europeCluster.Count, null);
            Assert.IsFalse(europeCluster.Full);
            Assert.IsTrue(Has(europePlaced, "TUR"));
            Assert.AreEqual(1, CountCode(europePlaced, "TUR"));
            Assert.IsFalse(MapCountryLabels.AggregateCaptionApplies(true, true));
            var duplicate = MapCountryLabels.PlaceAggregateCaption(
                new Vector2(900f, 400f),
                80f,
                18f,
                new Rect(0f, 0f, 1920f, 1080f),
                null,
                0,
                -1,
                out _,
                out _);
            Assert.GreaterOrEqual(duplicate, 0);
            Assert.AreEqual(-1, CaptionSide(europe, "tr", europePlaced, europeCluster, -1, new Rect(0f, 0f, 1920f, 1080f), out _, out _));

            var compact = WorldView(932f, 430f);
            var compactCluster = ClusterOf(compact);
            Assert.IsTrue(compactCluster.Full);
            var safe = new Rect(32f, 16f, 868f, 398f);
            var compactPlaced = Place(compact, "de", compactCluster.Rects, compactCluster.Count, null, safe);
            Assert.IsFalse(Has(compactPlaced, "TUR"));
            var viewport = MapCommandLayout.Evaluate(932f, 430f, safe, false).Viewport;
            var name = MapCountryLabels.Name(Country("TUR"), "de");
            Assert.AreEqual("Türkei", name);
            var side = MapCountryLabels.PlaceAggregateCaption(
                compactCluster.Badge,
                MapCountryLabels.Width(name, 12f),
                18f,
                viewport,
                BoundsOf(compactPlaced),
                compactPlaced.Count,
                -1,
                out var screen,
                out var bounds);
            Assert.GreaterOrEqual(side, 0);
            AssertClearCaption(screen, bounds, compactCluster.Badge, MapCountryLabels.Width(name, 12f), 18f, viewport, compactPlaced);
        }

        [Test]
        public void WorldAggregateCaption_KeepsItsSideAcrossAPan()
        {
            var view = WorldView(1920f, 1080f);
            var cluster = ClusterOf(view);
            var placed = Place(view, "tr", cluster.Rects, cluster.Count, null);
            var full = new Rect(0f, 0f, view.Width, view.Height);
            var side = CaptionSide(view, "tr", placed, cluster, -1, full, out var screen, out _);
            Assert.GreaterOrEqual(side, 0);
            var again = CaptionSide(view, "tr", placed, cluster, side, full, out var repeat, out _);
            Assert.AreEqual(side, again);
            Assert.AreEqual(screen.x, repeat.x, 0.01f);
            Assert.AreEqual(screen.y, repeat.y, 0.01f);

            var shifted = view;
            shifted.Center += new Vector2(18f, -9f);
            var movedCluster = ClusterOf(shifted);
            var movedPlaced = Place(shifted, "tr", movedCluster.Rects, movedCluster.Count, null);
            var movedSide = CaptionSide(shifted, "tr", movedPlaced, movedCluster, side, new Rect(0f, 0f, shifted.Width, shifted.Height), out var moved, out _);
            Assert.AreEqual(side, movedSide);
            var badgeDelta = movedCluster.Badge - cluster.Badge;
            var captionDelta = moved - screen;
            Assert.AreEqual(badgeDelta.x, captionDelta.x, 0.6f);
            Assert.AreEqual(badgeDelta.y, captionDelta.y, 0.6f);
        }

        [Test]
        public void Density_KeepsPriorityDeterministicAndSeparatesStackedNames()
        {
            const float line = 18f;
            const float zoom = 4f;
            var gapY = MapCountryLabels.GapY(line, zoom);
            Assert.Greater(gapY, MapCountryLabels.CollisionGapY);
            Assert.AreEqual(MapCountryLabels.CollisionGapY, MapCountryLabels.GapY(line, MapCountryLabels.WorldZoom), 0.001f);
            Assert.AreEqual(MapCountryLabels.CollisionGapX, MapCountryLabels.GapX(1f, zoom), 0.001f);

            var screen = new Vector2[MapCountryCatalog.All.Length];
            for (var i = 0; i < screen.Length; i++)
                screen[i] = new Vector2(-4000f, -4000f);

            var france = MapCountryCatalog.IndexOf("FRA");
            var italy = MapCountryCatalog.IndexOf("ITA");
            var spain = MapCountryCatalog.IndexOf("ESP");
            screen[france] = new Vector2(640f, 420f);
            screen[italy] = new Vector2(640f, 420f + line + gapY);
            var viewport = new Rect(80f, 40f, 1760f, 980f);
            var chosen = new int[MapCountryLabels.Capacity];
            var occupied = new Rect[MapCountryLabels.Capacity];
            var stacked = MapCountryLabels.Choose(zoom, "tr", screen, 12f, null, line, viewport, chosen, occupied);
            Assert.IsTrue(Contains(chosen, stacked, france));
            Assert.IsFalse(Contains(chosen, stacked, italy));

            var repeat = new int[MapCountryLabels.Capacity];
            var repeatOccupied = new Rect[MapCountryLabels.Capacity];
            var again = MapCountryLabels.Choose(zoom, "tr", screen, 12f, null, line, viewport, repeat, repeatOccupied);
            Assert.AreEqual(stacked, again);
            for (var i = 0; i < stacked; i++)
            {
                Assert.AreEqual(chosen[i], repeat[i]);
                Assert.AreEqual(occupied[i].x, repeatOccupied[i].x, 0.01f);
                Assert.AreEqual(occupied[i].y, repeatOccupied[i].y, 0.01f);
            }

            screen[italy] = new Vector2(640f, 420f + line + gapY * 2f + line);
            var separated = MapCountryLabels.Choose(zoom, "de", screen, 12f, null, line, viewport, chosen, occupied);
            Assert.IsTrue(Contains(chosen, separated, france));
            Assert.IsTrue(Contains(chosen, separated, italy));
            Assert.AreEqual("Frankreich", MapCountryLabels.Name(Country("FRA"), "de"));
            Assert.AreEqual("Italien", MapCountryLabels.Name(Country("ITA"), "de"));
            AssertNoOverlap(occupied, separated);
            AssertUniqueIndices(chosen, separated);

            screen[spain] = new Vector2(760f, 420f);
            var measured = new float[MapCountryCatalog.All.Length];
            measured[france] = 48f;
            measured[spain] = 48f;
            var narrow = MapCountryLabels.Choose(zoom, "en", screen, 12f, measured, line, viewport, chosen, occupied);
            Assert.IsTrue(Contains(chosen, narrow, france));
            Assert.IsTrue(Contains(chosen, narrow, spain));

            measured[france] = 160f;
            measured[spain] = 160f;
            var wide = MapCountryLabels.Choose(zoom, "de", screen, 12f, measured, line, viewport, chosen, occupied);
            Assert.IsTrue(Contains(chosen, wide, france));
            Assert.IsFalse(Contains(chosen, wide, spain));
            Assert.AreEqual("France", MapCountryLabels.Name(Country("FRA"), "en"));
            Assert.AreEqual("Spanien", MapCountryLabels.Name(Country("ESP"), "de"));
        }

        [Test]
        public void Density_RegionFramesStayInsideTheHudAndOnTheirAnchors()
        {
            var aspect = 1920f / 1080f;
            var safe = new Rect(0f, 0f, 1920f, 1080f);
            var phone = new Rect(186f, 60f, 2496f, 1200f);
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkeyFrame);
            var worldSize = MapFrames.MaximumOrthographicSize(aspect);
            var scopes = new[]
            {
                MapScope.World,
                MapScope.Turkey,
                MapScope.Europe,
                MapScope.Asia,
                MapScope.Africa,
                MapScope.NorthAmerica,
                MapScope.SouthAmerica,
                MapScope.Oceania
            };

            var majors = 0;
            for (var s = 0; s < scopes.Length; s++)
            {
                Vector2 center;
                float size;
                if (scopes[s] == MapScope.World)
                    MapViewMath.Fit(worldMin, worldMax, aspect, MapFrames.WorldFocusPadding, out center, out size);
                else
                    MapCameraRig.FrameScope(scopes[s], aspect, 1920f, 1080f, safe, worldMin, worldMax, out center, out size);

                size = MapViewMath.ClampSize(size, MapFrames.MinimumOrthographicSize(aspect), worldSize);
                var zoom = MapCountryLabels.Zoom(size, worldSize, turkeyFrame);
                var screen = Project(center, size, 1920f, 1080f);
                var viewport = MapCommandLayout.Evaluate(1920f, 1080f, safe, false).Viewport;
                var chosen = new int[MapCountryLabels.Capacity];
                var occupied = new Rect[MapCountryLabels.Capacity];
                var count = MapCountryLabels.Choose(zoom, "tr", screen, 12f, null, 18f, viewport, chosen, occupied);
                Assert.LessOrEqual(count, MapCountryLabels.Capacity, scopes[s].ToString());
                AssertNoOverlap(occupied, count);
                AssertUniqueIndices(chosen, count);
                for (var i = 0; i < count; i++)
                {
                    Assert.GreaterOrEqual(occupied[i].xMin, viewport.xMin, scopes[s].ToString());
                    Assert.GreaterOrEqual(occupied[i].yMin, viewport.yMin, scopes[s].ToString());
                    Assert.LessOrEqual(occupied[i].xMax, viewport.xMax, scopes[s].ToString());
                    Assert.LessOrEqual(occupied[i].yMax, viewport.yMax, scopes[s].ToString());
                    Assert.AreEqual(screen[chosen[i]].x, occupied[i].center.x, 0.05f);
                    Assert.AreEqual(screen[chosen[i]].y, occupied[i].center.y, 0.05f);
                }

                if (zoom < 0f)
                    Assert.AreEqual(0, count, scopes[s].ToString());
                if (scopes[s] == MapScope.World)
                    Assert.Greater(count, 8);

                if (scopes[s] == MapScope.Europe || scopes[s] == MapScope.Africa || scopes[s] == MapScope.SouthAmerica || scopes[s] == MapScope.Asia)
                    Assert.Greater(count, 0, scopes[s].ToString());
                if (Contains(chosen, count, MapCountryCatalog.IndexOf("DEU"))
                    || Contains(chosen, count, MapCountryCatalog.IndexOf("FRA"))
                    || Contains(chosen, count, MapCountryCatalog.IndexOf("GBR")))
                    majors++;
            }

            Assert.Greater(majors, 0);

            var phoneViewport = MapCommandLayout.Evaluate(2868f, 1320f, phone, false).Viewport;
            MapCameraRig.FrameScope(MapScope.Europe, 2868f / 1320f, 2868f, 1320f, phone, worldMin, worldMax, out var phoneCenter, out var phoneSize);
            phoneSize = MapViewMath.ClampSize(phoneSize, MapFrames.MinimumOrthographicSize(2868f / 1320f), MapFrames.MaximumOrthographicSize(2868f / 1320f));
            var phoneZoom = MapCountryLabels.Zoom(phoneSize, MapFrames.MaximumOrthographicSize(2868f / 1320f), turkeyFrame);
            var phoneScreen = Project(phoneCenter, phoneSize, 2868f, 1320f);
            var phoneChosen = new int[MapCountryLabels.Capacity];
            var phoneOccupied = new Rect[MapCountryLabels.Capacity];
            var phoneCount = MapCountryLabels.Choose(phoneZoom, "tr", phoneScreen, 14f, null, 22f, phoneViewport, phoneChosen, phoneOccupied);
            Assert.Greater(phoneCount, 0);
            AssertNoOverlap(phoneOccupied, phoneCount);
            for (var i = 0; i < phoneCount; i++)
            {
                Assert.GreaterOrEqual(phoneOccupied[i].xMin, phoneViewport.xMin);
                Assert.LessOrEqual(phoneOccupied[i].xMax, phoneViewport.xMax);
                Assert.GreaterOrEqual(phoneOccupied[i].yMin, phoneViewport.yMin);
                Assert.LessOrEqual(phoneOccupied[i].yMax, phoneViewport.yMax);
            }
        }

        [Test]
        public void Region_PrefersItsContinentAndStillKeepsAClearOutsideName()
        {
            var turkey = Country("TUR");
            Assert.AreEqual(MapCountryCatalog.MapContinent.Asia, turkey.Continent);
            Assert.AreEqual(34.5083f, turkey.Longitude, 0.0001f);
            Assert.AreEqual(39.3454f, turkey.Latitude, 0.0001f);
            Assert.AreEqual(2.5523f, Country("FRA").Longitude, 0.0001f);
            Assert.AreEqual(MapCountryCatalog.MapContinent.Europe, Country("FRA").Continent);
            Assert.AreEqual(MapCountryCatalog.MapContinent.NorthAmerica, Country("GRL").Continent);
            Assert.AreEqual(MapCountryCatalog.MapContinent.SouthAmerica, Country("BRA").Continent);
            Assert.IsFalse(MapCountryLabels.TryScopeContinent(MapScope.World, out _));
            Assert.IsFalse(MapCountryLabels.TryScopeContinent(MapScope.Turkey, out _));
            Assert.IsTrue(MapCountryLabels.TryScopeContinent(MapScope.Africa, out var africaContinent));
            Assert.AreEqual(MapCountryCatalog.MapContinent.Africa, africaContinent);

            var screen = new Vector2[MapCountryCatalog.All.Length];
            for (var i = 0; i < screen.Length; i++)
                screen[i] = new Vector2(-4000f, -4000f);
            var france = MapCountryCatalog.IndexOf("FRA");
            var brazil = MapCountryCatalog.IndexOf("BRA");
            var nigeria = MapCountryCatalog.IndexOf("NGA");
            screen[france] = new Vector2(500f, 400f);
            screen[brazil] = new Vector2(500f, 400f);
            var viewport = new Rect(40f, 40f, 1400f, 800f);
            var chosen = new int[MapCountryLabels.Capacity];
            var occupied = new Rect[MapCountryLabels.Capacity];
            const float zoom = 4f;
            const float line = 18f;

            var europe = MapCountryLabels.Choose(zoom, "de", screen, 12f, null, line, viewport, chosen, occupied, MapScope.Europe);
            Assert.IsTrue(Contains(chosen, europe, france));
            Assert.IsFalse(Contains(chosen, europe, brazil));
            Assert.AreEqual("Frankreich", MapCountryLabels.Name(Country("FRA"), "de"));
            Assert.Greater(MapCountryLabels.LastSelection.RejectedOverlap, 0);

            var south = MapCountryLabels.Choose(zoom, "en", screen, 12f, null, line, viewport, chosen, occupied, MapScope.SouthAmerica);
            Assert.IsTrue(Contains(chosen, south, brazil));
            Assert.IsFalse(Contains(chosen, south, france));
            Assert.AreEqual("Brazil", MapCountryLabels.Name(Country("BRA"), "en"));

            var world = MapCountryLabels.Choose(zoom, "tr", screen, 12f, null, line, viewport, chosen, occupied, MapScope.World);
            Assert.IsTrue(Contains(chosen, world, brazil));
            Assert.IsFalse(Contains(chosen, world, france));
            Assert.AreEqual(MapCountryLabels.LastSelection.Visible, MapCountryLabels.LastSelection.InRegion);
            Assert.AreEqual(0, MapCountryLabels.LastSelection.OutOfRegion);

            var repeatChosen = new int[chosen.Length];
            var repeatOccupied = new Rect[occupied.Length];
            var repeat = MapCountryLabels.Choose(zoom, "tr", screen, 12f, null, line, viewport, repeatChosen, repeatOccupied, MapScope.World);
            Assert.AreEqual(world, repeat);
            for (var i = 0; i < repeat; i++)
                Assert.AreEqual(chosen[i], repeatChosen[i]);

            screen[nigeria] = new Vector2(500f, 400f);
            screen[brazil] = new Vector2(980f, 400f);
            var africa = MapCountryLabels.Choose(zoom, "en", screen, 12f, null, line, viewport, chosen, occupied, MapScope.Africa);
            Assert.IsTrue(Contains(chosen, africa, nigeria));
            Assert.IsTrue(Contains(chosen, africa, brazil));
            Assert.Greater(MapCountryLabels.LastSelection.InRegion, 0);
            Assert.Greater(MapCountryLabels.LastSelection.OutOfRegion, 0);
            AssertNoOverlap(occupied, africa);

            var wide = new float[screen.Length];
            wide[france] = 48f;
            wide[brazil] = 220f;
            screen[france] = new Vector2(640f, 360f);
            screen[brazil] = new Vector2(760f, 360f);
            screen[nigeria] = new Vector2(-4000f, -4000f);
            var german = MapCountryLabels.Choose(zoom, "de", screen, 12f, wide, line, viewport, chosen, occupied, MapScope.Europe);
            Assert.IsTrue(Contains(chosen, german, france));
            Assert.IsFalse(Contains(chosen, german, brazil));

            var phoneLine = MapCountryLabels.ReferenceLine * 2f;
            var desktopGap = MapCountryLabels.GapY(line, zoom);
            var phoneGap = MapCountryLabels.GapY(phoneLine, zoom);
            Assert.Greater(desktopGap, MapCountryLabels.CollisionGapY);
            Assert.AreEqual(line * MapCountryLabels.GapYFactor, desktopGap, 0.05f);
            Assert.Greater(phoneGap, desktopGap);
            Assert.Greater(phoneGap, phoneLine * 0.5f);
            var phoneSafe = new Rect(186f, 60f, 2496f, 1200f);
            var phoneViewport = MapCommandLayout.Evaluate(2868f, 1320f, phoneSafe, false).Viewport;
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            MapCameraRig.FrameScope(MapScope.Europe, 2868f / 1320f, 2868f, 1320f, phoneSafe, worldMin, worldMax, out var center, out var size);
            var phoneScreen = Project(center, size, 2868f, 1320f);
            var phoneChosen = new int[MapCountryLabels.Capacity];
            var phoneOccupied = new Rect[MapCountryLabels.Capacity];
            var phoneCount = MapCountryLabels.Choose(zoom, "tr", phoneScreen, 14f, null, phoneLine, phoneViewport, phoneChosen, phoneOccupied, MapScope.Europe);
            Assert.Greater(phoneCount, 0);
            Assert.Greater(MapCountryLabels.LastSelection.InRegion, MapCountryLabels.LastSelection.OutOfRegion);
            AssertNoOverlap(phoneOccupied, phoneCount);
            for (var i = 0; i < phoneCount; i++)
            {
                Assert.GreaterOrEqual(phoneOccupied[i].xMin, phoneViewport.xMin);
                Assert.LessOrEqual(phoneOccupied[i].xMax, phoneViewport.xMax);
            }
        }

        [Test]
        public void Placement_MovesABlockedNeighbourOnlyInsideItsAnchorCell()
        {
            var germany = MapCountryCatalog.IndexOf("DEU");
            var france = MapCountryCatalog.IndexOf("FRA");
            var britain = MapCountryCatalog.IndexOf("GBR");
            var screen = new Vector2[MapCountryCatalog.All.Length];
            for (var i = 0; i < screen.Length; i++)
                screen[i] = new Vector2(-4000f, -4000f);

            screen[germany] = new Vector2(640f, 420f);
            screen[france] = new Vector2(640f, 400f);
            screen[britain] = new Vector2(-4000f, -4000f);
            var line = 18f;
            var viewport = new Rect(0f, 0f, 1920f, 1080f);
            var chosen = new int[MapCountryLabels.Capacity];
            var occupied = new Rect[MapCountryLabels.Capacity];
            var shifts = new Vector2[MapCountryLabels.Capacity];
            var held = new Vector2[screen.Length];
            var maxShift = 40f;
            var first = MapCountryLabels.Choose(4f, "tr", screen, 12f, null, line, viewport, chosen, occupied, MapScope.Europe, maxShift, shifts, held);
            Assert.IsTrue(Contains(chosen, first, germany));
            Assert.IsTrue(Contains(chosen, first, france));
            Assert.IsFalse(Contains(chosen, first, britain));
            var franceSlot = SlotOf(chosen, first, france);
            Assert.AreEqual(screen[germany].x, occupied[SlotOf(chosen, first, germany)].center.x, 0.05f);
            Assert.Less(occupied[franceSlot].center.y, screen[france].y);
            Assert.LessOrEqual(shifts[franceSlot].magnitude, maxShift + 0.05f);
            Assert.IsTrue(MapCountryLabels.CloserToOwnAnchor(occupied[franceSlot].center, screen[france], screen, france));
            Assert.Greater(MapCountryLabels.LastSelection.Shifted, 0);
            AssertNoOverlap(occupied, first);

            var repeatChosen = new int[MapCountryLabels.Capacity];
            var repeatOccupied = new Rect[MapCountryLabels.Capacity];
            var repeatShifts = new Vector2[MapCountryLabels.Capacity];
            var repeat = MapCountryLabels.Choose(4f, "tr", screen, 12f, null, line, viewport, repeatChosen, repeatOccupied, MapScope.Europe, maxShift, repeatShifts, held);
            Assert.AreEqual(first, repeat);
            Assert.AreEqual(occupied[franceSlot].center.y, repeatOccupied[SlotOf(repeatChosen, repeat, france)].center.y, 0.01f);

            var world = MapCountryLabels.Choose(4f, "tr", screen, 12f, null, line, viewport, chosen, occupied, MapScope.World, 0f, shifts, held);
            Assert.IsTrue(Contains(chosen, world, germany));
            Assert.IsFalse(Contains(chosen, world, france));
            Assert.AreEqual(0, MapCountryLabels.LastSelection.Shifted);
            Assert.AreEqual(0, MapCountryLabels.LastSelection.OutOfRegion);

            screen[britain] = new Vector2(640f, 347f);
            var crowded = MapCountryLabels.Choose(4f, "de", screen, 12f, null, line, viewport, chosen, occupied, MapScope.Europe, 8f, shifts, held);
            Assert.IsTrue(Contains(chosen, crowded, germany));
            Assert.IsFalse(Contains(chosen, crowded, france));
            AssertNoOverlap(occupied, crowded);

            var wide = new float[screen.Length];
            wide[germany] = 90f;
            wide[france] = 70f;
            screen[france] = new Vector2(640f, 370f);
            screen[britain] = new Vector2(-4000f, -4000f);
            var german = MapCountryLabels.Choose(4f, "de", screen, 12f, wide, line, viewport, chosen, occupied, MapScope.Europe, maxShift, shifts, held);
            Assert.IsTrue(Contains(chosen, german, france));
            Assert.AreEqual("Fransa", MapCountryLabels.Name(Country("FRA"), "tr"));
            Assert.AreEqual("Frankreich", MapCountryLabels.Name(Country("FRA"), "de"));
            var germanSlot = SlotOf(chosen, german, france);
            Assert.LessOrEqual(shifts[germanSlot].magnitude, maxShift + 0.05f);
            AssertNoOverlap(occupied, german);
        }

        [Test]
        public void Placement_EuropeFrameKeepsFranceOnFranceAndInsideThePhoneHole()
        {
            var safe = new Rect(0f, 0f, 1920f, 1080f);
            var phone = new Rect(186f, 60f, 2496f, 1200f);
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            MapCameraRig.FrameScope(MapScope.Europe, 1920f / 1080f, 1920f, 1080f, safe, worldMin, worldMax, out var center, out var size);
            var units = MapMarkerLod.UnitsPerPixel(size, 1080f);
            var zoom = ZoomOf(size, 1920f / 1080f);
            var screen = Project(center, size, 1920f, 1080f);
            var viewport = MapCommandLayout.Evaluate(1920f, 1080f, safe, false).Viewport;
            var chosen = new int[MapCountryLabels.Capacity];
            var occupied = new Rect[MapCountryLabels.Capacity];
            var shifts = new Vector2[MapCountryLabels.Capacity];
            var held = new Vector2[screen.Length];
            var maxShift = MapCountryLabels.MaxPresentationWorld / units;
            var count = MapCountryLabels.Choose(zoom, "tr", screen, 12f, null, 18f, viewport, chosen, occupied, MapScope.Europe, maxShift, shifts, held);
            Assert.Greater(count, 0);
            Assert.IsTrue(Contains(chosen, count, MapCountryCatalog.IndexOf("FRA")));
            Assert.IsTrue(Contains(chosen, count, MapCountryCatalog.IndexOf("DEU")));
            AssertNoOverlap(occupied, count);
            AssertUniqueIndices(chosen, count);
            for (var i = 0; i < count; i++)
            {
                var anchor = screen[chosen[i]];
                Assert.LessOrEqual(shifts[i].magnitude, maxShift + 0.05f);
                Assert.IsTrue(MapCountryLabels.CloserToOwnAnchor(occupied[i].center, anchor, screen, chosen[i]));
                Assert.GreaterOrEqual(occupied[i].xMin, viewport.xMin);
                Assert.LessOrEqual(occupied[i].xMax, viewport.xMax);
            }

            var france = SlotOf(chosen, count, MapCountryCatalog.IndexOf("FRA"));
            Assert.LessOrEqual(occupied[france].center.y, screen[MapCountryCatalog.IndexOf("FRA")].y + 0.05f);
            var again = MapCountryLabels.Choose(zoom, "tr", screen, 12f, null, 18f, viewport, chosen, occupied, MapScope.Europe, maxShift, shifts, held);
            Assert.AreEqual(count, again);

            var sizes = new[]
            {
                new Vector2(844f, 390f),
                new Vector2(932f, 430f),
                new Vector2(960f, 540f),
                new Vector2(1280f, 720f),
                new Vector2(1920f, 1080f)
            };
            for (var s = 0; s < sizes.Length; s++)
            {
                var width = sizes[s].x;
                var height = sizes[s].y;
                var hole = new Rect(0f, 0f, width, height);
                MapCameraRig.FrameScope(MapScope.Europe, width / height, width, height, hole, worldMin, worldMax, out var framedCenter, out var framedSize);
                var framedUnits = MapMarkerLod.UnitsPerPixel(framedSize, height);
                var framedZoom = ZoomOf(framedSize, width / height);
                var framed = Project(framedCenter, framedSize, width, height);
                var framedView = MapCommandLayout.Evaluate(width, height, hole, false).Viewport;
                var framedShift = MapCountryLabels.MaxPresentationWorld / framedUnits;
                for (var h = 0; h < held.Length; h++)
                    held[h] = Vector2.zero;
                var framedCount = MapCountryLabels.Choose(framedZoom, "de", framed, 12f, null, 18f, framedView, chosen, occupied, MapScope.Europe, framedShift, shifts, held);
                AssertNoOverlap(occupied, framedCount);
                AssertUniqueIndices(chosen, framedCount);
                for (var i = 0; i < framedCount; i++)
                {
                    Assert.LessOrEqual(shifts[i].magnitude, framedShift + 0.05f, sizes[s].ToString());
                    Assert.GreaterOrEqual(occupied[i].xMin, framedView.xMin);
                    Assert.LessOrEqual(occupied[i].xMax, framedView.xMax);
                    Assert.GreaterOrEqual(occupied[i].yMin, framedView.yMin);
                    Assert.LessOrEqual(occupied[i].yMax, framedView.yMax);
                }
            }

            var phoneView = MapCommandLayout.Evaluate(2868f, 1320f, phone, false).Viewport;
            MapCameraRig.FrameScope(MapScope.Europe, 2868f / 1320f, 2868f, 1320f, phone, worldMin, worldMax, out var phoneCenter, out var phoneSize);
            var phoneUnits = MapMarkerLod.UnitsPerPixel(phoneSize, 1320f);
            var phoneScreen = Project(phoneCenter, phoneSize, 2868f, 1320f);
            var phoneShift = MapCountryLabels.MaxPresentationWorld / phoneUnits;
            for (var h = 0; h < held.Length; h++)
                held[h] = Vector2.zero;
            var phoneZoom = ZoomOf(phoneSize, 2868f / 1320f);
            var phoneCount = MapCountryLabels.Choose(phoneZoom, "en", phoneScreen, 14f, null, 36f, phoneView, chosen, occupied, MapScope.Europe, phoneShift, shifts, held);
            Assert.Greater(phoneCount, 0);
            AssertNoOverlap(occupied, phoneCount);
            for (var i = 0; i < phoneCount; i++)
            {
                Assert.GreaterOrEqual(occupied[i].xMin, phoneView.xMin);
                Assert.LessOrEqual(occupied[i].xMax, phoneView.xMax);
                Assert.GreaterOrEqual(occupied[i].yMin, phoneView.yMin);
                Assert.LessOrEqual(occupied[i].yMax, phoneView.yMax);
                Assert.IsTrue(MapCountryLabels.CloserToOwnAnchor(occupied[i].center, phoneScreen[chosen[i]], phoneScreen, chosen[i]));
            }
        }

        [Test]
        public void Margin_KeepsSeparatedEuropeanNamesCenteredAndMeasuresRegionalCost()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Assert.IsNotNull(font);
            var names = new[]
            {
                "Almanya", "Fransa", "İspanya", "Birleşik Krallık", "İtalya",
                "Germany", "France", "Spain", "United Kingdom", "Italy",
                "Deutschland", "Frankreich", "Spanien", "Vereinigtes Königreich", "Italien"
            };
            for (var i = 0; i < names.Length; i++)
            {
                var ink = GlyphBounds(font, names[i], 16);
                TestContext.WriteLine(
                    "glyph16 " + names[i]
                    + " advance=" + ink.x.ToString("0.0")
                    + " ink=" + ink.y.ToString("0.0")
                    + " minY=" + ink.z.ToString("0.0")
                    + " maxY=" + ink.w.ToString("0.0"));
                Assert.Greater(ink.x, 8f, names[i]);
                Assert.Greater(ink.y, 4f, names[i]);
            }

            var phoneInk = GlyphBounds(font, "Fransa", 29);
            TestContext.WriteLine(
                "glyph29 Fransa advance=" + phoneInk.x.ToString("0.0")
                + " ink=" + phoneInk.y.ToString("0.0")
                + " minY=" + phoneInk.z.ToString("0.0")
                + " maxY=" + phoneInk.w.ToString("0.0"));
            Assert.AreEqual(MapCountryLabels.CollisionGapY, MapCountryLabels.GapY(18f, MapCountryLabels.WorldZoom), 0.01f);

            var safe = new Rect(0f, 0f, 1920f, 1080f);
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            MapCameraRig.FrameScope(MapScope.Europe, 1920f / 1080f, 1920f, 1080f, safe, worldMin, worldMax, out var center, out var size);
            var zoom = ZoomOf(size, 1920f / 1080f);
            var screen = Project(center, size, 1920f, 1080f);
            var viewport = MapCommandLayout.Evaluate(1920f, 1080f, safe, false).Viewport;
            var chosen = new int[MapCountryLabels.Capacity];
            var occupied = new Rect[MapCountryLabels.Capacity];
            var count = MapCountryLabels.Choose(zoom, "tr", screen, 12f, null, 18.4f, viewport, chosen, occupied, MapScope.Europe);
            Assert.IsTrue(Contains(chosen, count, MapCountryCatalog.IndexOf("FRA")));
            Assert.IsTrue(Contains(chosen, count, MapCountryCatalog.IndexOf("ESP")));
            Assert.IsTrue(Contains(chosen, count, MapCountryCatalog.IndexOf("DEU")));
            Assert.IsTrue(Contains(chosen, count, MapCountryCatalog.IndexOf("GBR")));
            Assert.AreEqual(0, MapCountryLabels.LastSelection.Shifted);
            Assert.AreEqual(0, MapCountryLabels.LastEffort.CandidateAttempts);
            Assert.AreEqual(0, MapCountryLabels.LastEffort.AnchorComparisons);
            AssertNoOverlap(occupied, count);
            AssertUniqueIndices(chosen, count);
            for (var i = 0; i < count; i++)
            {
                Assert.AreEqual(screen[chosen[i]].x, occupied[i].center.x, 0.05f);
                Assert.AreEqual(screen[chosen[i]].y, occupied[i].center.y, 0.05f);
            }

            var repeat = MapCountryLabels.Choose(zoom, "tr", screen, 12f, null, 18.4f, viewport, chosen, occupied, MapScope.Europe);
            Assert.AreEqual(count, repeat);
            var german = MapCountryLabels.Choose(zoom, "de", screen, 12f, null, 18.4f, viewport, chosen, occupied, MapScope.Europe);
            Assert.IsTrue(Contains(chosen, german, MapCountryCatalog.IndexOf("FRA")));
            Assert.IsTrue(Contains(chosen, german, MapCountryCatalog.IndexOf("ESP")));
            Assert.IsTrue(Contains(chosen, german, MapCountryCatalog.IndexOf("DEU")));
            var english = MapCountryLabels.Choose(zoom, "en", screen, 12f, null, 18.4f, viewport, chosen, occupied, MapScope.Europe);
            Assert.IsTrue(Contains(chosen, english, MapCountryCatalog.IndexOf("FRA")));
            Assert.IsTrue(Contains(chosen, english, MapCountryCatalog.IndexOf("ESP")));
            Assert.IsTrue(Contains(chosen, english, MapCountryCatalog.IndexOf("GBR")));
            AssertNoOverlap(occupied, english);

            var phone = new Rect(186f, 60f, 2496f, 1200f);
            MapCameraRig.FrameScope(MapScope.Europe, 2868f / 1320f, 2868f, 1320f, phone, worldMin, worldMax, out var phoneCenter, out var phoneSize);
            var phoneZoom = ZoomOf(phoneSize, 2868f / 1320f);
            var phoneScreen = Project(phoneCenter, phoneSize, 2868f, 1320f);
            var phoneView = MapCommandLayout.Evaluate(2868f, 1320f, phone, false).Viewport;
            var phoneCount = MapCountryLabels.Choose(phoneZoom, "tr", phoneScreen, 14f, null, 45f, phoneView, chosen, occupied, MapScope.Europe);
            Assert.Greater(phoneCount, 0);
            Assert.AreEqual(0, MapCountryLabels.LastSelection.Shifted);
            AssertNoOverlap(occupied, phoneCount);
            for (var i = 0; i < phoneCount; i++)
            {
                Assert.GreaterOrEqual(occupied[i].xMin, phoneView.xMin);
                Assert.LessOrEqual(occupied[i].xMax, phoneView.xMax);
                Assert.GreaterOrEqual(occupied[i].yMin, phoneView.yMin);
                Assert.LessOrEqual(occupied[i].yMax, phoneView.yMax);
            }

            var scopes = new[] { MapScope.Europe, MapScope.Africa, MapScope.SouthAmerica };
            for (var s = 0; s < scopes.Length; s++)
            {
                MapCameraRig.FrameScope(scopes[s], 1920f / 1080f, 1920f, 1080f, safe, worldMin, worldMax, out var framedCenter, out var framedSize);
                var framedZoom = ZoomOf(framedSize, 1920f / 1080f);
                var framed = Project(framedCenter, framedSize, 1920f, 1080f);
                var framedView = MapCommandLayout.Evaluate(1920f, 1080f, safe, false).Viewport;
                var units = MapMarkerLod.UnitsPerPixel(framedSize, 1080f);
                var shift = MapCountryLabels.MaxPresentationWorld / units;
                var held = new Vector2[framed.Length];
                var slot = new Vector2[MapCountryLabels.Capacity];
                MapCountryLabels.Choose(framedZoom, "tr", framed, 12f, null, 18.4f, framedView, chosen, occupied, scopes[s]);
                var watch = Stopwatch.StartNew();
                var visible = 0;
                for (var i = 0; i < 200; i++)
                    visible = MapCountryLabels.Choose(framedZoom, "tr", framed, 12f, null, 18.4f, framedView, chosen, occupied, scopes[s]);
                watch.Stop();
                var centered = MapCountryLabels.LastEffort;
                long before = GC.GetTotalMemory(true);
                for (var i = 0; i < 200; i++)
                    MapCountryLabels.Choose(framedZoom, "tr", framed, 12f, null, 18.4f, framedView, chosen, occupied, scopes[s]);
                long after = GC.GetTotalMemory(false);
                var shiftWatch = Stopwatch.StartNew();
                for (var i = 0; i < 200; i++)
                    MapCountryLabels.Choose(framedZoom, "tr", framed, 12f, null, 18.4f, framedView, chosen, occupied, scopes[s], shift, slot, held);
                shiftWatch.Stop();
                var shifted = MapCountryLabels.LastEffort;
                TestContext.WriteLine(
                    "MAP02R3 " + scopes[s]
                    + " centered200=" + watch.Elapsed.TotalMilliseconds.ToString("0.00")
                    + "ms visible=" + visible
                    + " blocked=" + centered.Blocked
                    + " attempts=" + centered.CandidateAttempts
                    + " anchors=" + centered.AnchorComparisons
                    + " bytes=" + (after - before)
                    + " shift200=" + shiftWatch.Elapsed.TotalMilliseconds.ToString("0.00")
                    + "ms attempts=" + shifted.CandidateAttempts
                    + " anchors=" + shifted.AnchorComparisons
                    + " shifted=" + MapCountryLabels.LastSelection.Shifted);
                Assert.AreEqual(0, centered.CandidateAttempts, scopes[s].ToString());
                Assert.AreEqual(0, centered.AnchorComparisons, scopes[s].ToString());
                Assert.Greater(shifted.AnchorComparisons, 0, scopes[s].ToString());
                Assert.Less(after - before, 8192, scopes[s].ToString());
            }
        }

        static Vector4 GlyphBounds(Font font, string name, int size)
        {
            font.RequestCharactersInTexture(name, size, FontStyle.Bold);
            var advance = 0;
            var minY = int.MaxValue;
            var maxY = int.MinValue;
            for (var i = 0; i < name.Length; i++)
            {
                Assert.IsTrue(font.GetCharacterInfo(name[i], out var info, size, FontStyle.Bold), name);
                advance += info.advance;
                if (info.minY < minY)
                    minY = info.minY;
                if (info.maxY > maxY)
                    maxY = info.maxY;
            }

            return new Vector4(advance, maxY - minY, minY, maxY);
        }

        static float ZoomOf(float size, float aspect)
        {
            MapFrames.Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkey);
            return MapCountryLabels.Zoom(size, MapFrames.MaximumOrthographicSize(aspect), turkey);
        }

        static int SlotOf(int[] chosen, int count, int index)
        {
            for (var i = 0; i < count; i++)
            {
                if (chosen[i] == index)
                    return i;
            }

            Assert.Fail("Missing chosen index " + index);
            return -1;
        }

        static void AssertNoOverlap(Rect[] occupied, int count)
        {
            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                    Assert.IsFalse(MapCountryLabels.Overlaps(occupied[i], occupied[j]));
            }
        }

        static void AssertUniqueIndices(int[] chosen, int count)
        {
            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                    Assert.AreNotEqual(chosen[i], chosen[j]);
            }
        }

        static void AssertUnique(MapCountryLabelLayer.Placed[] placed, int count)
        {
            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                    Assert.AreNotEqual(placed[i].Code, placed[j].Code);
            }
        }

        static void AssertMissing(MapCountryLabelLayer.Placed[] placed, int count, string code)
        {
            for (var i = 0; i < count; i++)
                Assert.AreNotEqual(code, placed[i].Code);
        }

        static void AssertClearOfCities(MapCountryLabelLayer.Placed[] placed, int count, Vector2 center, float size, float width, float height, float canvasScale)
        {
            var obstacles = Obstacles(new View(center, size, width, height, MapMarkerLod.UnitsPerPixel(size, height)), canvasScale);
            for (var i = 0; i < count; i++)
                Assert.IsFalse(Hits(placed[i].Bounds, obstacles.Rects, obstacles.Count), placed[i].Code);
        }

        struct View
        {
            public Vector2 Center;
            public float Size;
            public float Width;
            public float Height;
            public float Units;

            public View(Vector2 center, float size, float width, float height, float units)
            {
                Center = center;
                Size = size;
                Width = width;
                Height = height;
                Units = units;
            }
        }

        struct ObstacleSet
        {
            public Rect[] Rects;
            public int Count;
        }

        struct PlacedSet
        {
            public MapCountryLabelLayer.Placed[] Items;
            public int Count;
        }

        static View WorldView(float width, float height)
        {
            var aspect = width / height;
            MapFrames.WorldPresentation(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.WorldFocusPadding, out var center, out var size);
            return new View(center, size, width, height, MapMarkerLod.UnitsPerPixel(size, height));
        }

        static View EuropeView(float width, float height)
        {
            var aspect = width / height;
            MapFrames.ProjectedBounds(28d, 62d, -12d, 42d, out var min, out var max);
            MapViewMath.Fit(min, max, aspect, 1.08f, out var center, out var size);
            return new View(center, size, width, height, MapMarkerLod.UnitsPerPixel(size, height));
        }

        static ObstacleSet Obstacles(View view, float canvasScale)
        {
            var aspect = view.Width / view.Height;
            MapFrames.Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkey);
            var together = new bool[MapCities.All.Length * MapCities.All.Length];
            var parent = new int[MapCities.All.Length];
            var rects = new Rect[16];
            var count = MapCountryLabels.CityObstacles(
                view.Center,
                view.Size,
                view.Width,
                view.Height,
                turkey,
                canvasScale,
                together,
                parent,
                rects);
            return new ObstacleSet { Rects = rects, Count = count };
        }

        struct Cluster
        {
            public Rect[] Rects;
            public int Count;
            public Vector2 Badge;
            public bool Full;
        }

        static Cluster ClusterOf(View view)
        {
            var aspect = view.Width / view.Height;
            MapFrames.Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkey);
            var together = new bool[MapCities.All.Length * MapCities.All.Length];
            var parent = new int[MapCities.All.Length];
            var rects = new Rect[16];
            var count = MapCountryLabels.CityObstacles(
                view.Center,
                view.Size,
                view.Width,
                view.Height,
                turkey,
                1f,
                together,
                parent,
                rects);
            var full = MapCountryLabels.TryWorldAggregateScreen(
                view.Center,
                view.Units,
                view.Width,
                view.Height,
                parent,
                out var badge);
            return new Cluster { Rects = rects, Count = count, Badge = badge, Full = full };
        }

        static PlacedSet Place(View view, string locale, Rect[] obstacles, int obstacleCount, Vector2[] held)
        {
            return Place(view, locale, obstacles, obstacleCount, held, new Rect(0f, 0f, view.Width, view.Height));
        }

        static PlacedSet Place(View view, string locale, Rect[] obstacles, int obstacleCount, Vector2[] held, Rect safe)
        {
            var aspect = view.Width / view.Height;
            var world = MapFrames.MaximumOrthographicSize(aspect);
            MapFrames.Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkey);
            var zoom = MapCountryLabels.Zoom(view.Size, world, turkey);
            var screen = Project(view.Center, view.Size, view.Width, view.Height);
            var viewport = MapCommandLayout.Evaluate(view.Width, view.Height, safe, false).Viewport;
            return PlaceAt(screen, locale, zoom, MapCountryLabels.MaxNudgeWorld / view.Units, 12f, 18f, viewport, obstacles, obstacleCount, held);
        }

        static void AssertCaption(View view, string locale, string text, Rect safe)
        {
            var cluster = ClusterOf(view);
            Assert.IsTrue(cluster.Full, locale);
            var placed = Place(view, locale, cluster.Rects, cluster.Count, null, safe);
            Assert.IsFalse(Has(placed, "TUR"), locale);
            Assert.AreEqual(1, CountCode(placed, "TUR") + 1, locale);
            var side = CaptionSide(view, locale, placed, cluster, -1, safe, out var screen, out var bounds);
            Assert.GreaterOrEqual(side, 0, locale + " " + text);
            var width = MapCountryLabels.Width(text, 12f);
            var viewport = MapCommandLayout.Evaluate(view.Width, view.Height, safe, false).Viewport;
            AssertClearCaption(screen, bounds, cluster.Badge, width, 18f, viewport, placed);
            var again = CaptionSide(view, locale, placed, cluster, side, safe, out var repeat, out _);
            Assert.AreEqual(side, again, locale);
            Assert.AreEqual(screen.x, repeat.x, 0.01f, locale);
            Assert.AreEqual(screen.y, repeat.y, 0.01f, locale);
        }

        static int CaptionSide(View view, string locale, PlacedSet placed, Cluster cluster, int held, Rect safe, out Vector2 screen, out Rect bounds)
        {
            var viewport = MapCommandLayout.Evaluate(view.Width, view.Height, safe, false).Viewport;
            if (!MapCountryLabels.AggregateCaptionApplies(cluster.Full, Has(placed, "TUR")))
            {
                screen = Vector2.zero;
                bounds = new Rect();
                return -1;
            }

            var name = MapCountryLabels.Name(Country("TUR"), locale);
            return MapCountryLabels.PlaceAggregateCaption(
                cluster.Badge,
                MapCountryLabels.Width(name, 12f),
                18f,
                viewport,
                BoundsOf(placed),
                placed.Count,
                held,
                out screen,
                out bounds);
        }

        static void AssertClearCaption(Vector2 screen, Rect bounds, Vector2 badge, float width, float line, Rect viewport, PlacedSet placed)
        {
            var horizontal = Mathf.Abs(screen.x - badge.x) >= Mathf.Abs(screen.y - badge.y);
            var reach = (horizontal ? width : line) * 0.5f + MapMarkerLod.AggregateScreenPixels * 0.5f + MapCountryLabels.AggregateCaptionGap;
            Assert.AreEqual(reach, horizontal ? Mathf.Abs(screen.x - badge.x) : Mathf.Abs(screen.y - badge.y), 0.6f);
            Assert.AreEqual(0f, horizontal ? screen.y - badge.y : screen.x - badge.x, 0.6f);
            var ink = new Rect(screen.x - width * 0.5f, screen.y - line * 0.5f, width, line);
            var badgeRect = new Rect(
                badge.x - MapMarkerLod.AggregateScreenPixels * 0.5f,
                badge.y - MapMarkerLod.AggregateScreenPixels * 0.5f,
                MapMarkerLod.AggregateScreenPixels,
                MapMarkerLod.AggregateScreenPixels);
            Assert.IsFalse(MapCountryLabels.Overlaps(ink, badgeRect));
            var clearance = horizontal
                ? Mathf.Abs(screen.x - badge.x) - width * 0.5f
                : Mathf.Abs(screen.y - badge.y) - line * 0.5f;
            Assert.GreaterOrEqual(clearance, MapMarkerLod.AggregateHitRadius);
            Assert.GreaterOrEqual(bounds.xMin, viewport.xMin - 0.1f);
            Assert.GreaterOrEqual(bounds.yMin, viewport.yMin - 0.1f);
            Assert.LessOrEqual(bounds.xMax, viewport.xMax + 0.1f);
            Assert.LessOrEqual(bounds.yMax, viewport.yMax + 0.1f);
            for (var i = 0; i < placed.Count; i++)
                Assert.IsFalse(MapCountryLabels.Overlaps(bounds, placed.Items[i].Bounds), placed.Items[i].Code);
        }

        static void AssertAggregateCaption(
            MapCountryLabelLayer layer,
            string text,
            Vector2 center,
            float size,
            float width,
            float height,
            MapCountryLabelLayer.Placed[] placed,
            int count)
        {
            var caption = layer.AggregateCaption;
            Assert.IsTrue(caption.Visible, text);
            Assert.AreEqual(text, caption.Text);
            Assert.IsFalse(caption.Raycast, text);
            Assert.GreaterOrEqual(caption.Side, 0, text);
            var inkWidth = caption.Bounds.width - MapCountryLabels.CollisionGapX * 2f;
            var inkHeight = caption.Bounds.height - MapCountryLabels.CollisionGapY * 2f;
            var view = new View(center, size, width, height, MapMarkerLod.UnitsPerPixel(size, height));
            var cluster = ClusterOf(view);
            Assert.IsTrue(cluster.Full, text);
            var viewport = MapCommandLayout.Evaluate(width, height, new Rect(0f, 0f, width, height), false).Viewport;
            var horizontal = Mathf.Abs(caption.Screen.x - cluster.Badge.x) >= Mathf.Abs(caption.Screen.y - cluster.Badge.y);
            var reach = (horizontal ? inkWidth : inkHeight) * 0.5f + MapMarkerLod.AggregateScreenPixels * 0.5f + MapCountryLabels.AggregateCaptionGap;
            Assert.AreEqual(reach, horizontal ? Mathf.Abs(caption.Screen.x - cluster.Badge.x) : Mathf.Abs(caption.Screen.y - cluster.Badge.y), 1.5f, text);
            var ink = new Rect(caption.Screen.x - inkWidth * 0.5f, caption.Screen.y - inkHeight * 0.5f, inkWidth, inkHeight);
            var badgeRect = new Rect(cluster.Badge.x - 17f, cluster.Badge.y - 17f, 34f, 34f);
            Assert.IsFalse(MapCountryLabels.Overlaps(ink, badgeRect), text);
            Assert.GreaterOrEqual(caption.Bounds.xMin, viewport.xMin - 0.5f, text);
            Assert.GreaterOrEqual(caption.Bounds.yMin, viewport.yMin - 0.5f, text);
            Assert.LessOrEqual(caption.Bounds.xMax, viewport.xMax + 0.5f, text);
            Assert.LessOrEqual(caption.Bounds.yMax, viewport.yMax + 0.5f, text);
            for (var i = 0; i < count; i++)
                Assert.IsFalse(MapCountryLabels.Overlaps(caption.Bounds, placed[i].Bounds), placed[i].Code);
        }

        static Rect[] BoundsOf(PlacedSet placed)
        {
            var rects = new Rect[placed.Count];
            for (var i = 0; i < placed.Count; i++)
                rects[i] = placed.Items[i].Bounds;
            return rects;
        }

        static int CountCode(PlacedSet placed, string code)
        {
            var count = 0;
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed.Items[i].Code == code)
                    count++;
            }

            return count;
        }

        static MapMarkerLayer MarkerLayer(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var layer = roots[i].GetComponentInChildren<MapMarkerLayer>(true);
                if (layer != null)
                    return layer;
            }

            Assert.Fail("Map marker layer is missing.");
            return null;
        }

        static PlacedSet PlaceAt(
            Vector2[] screen,
            string locale,
            float zoom,
            float maxNudge,
            float glyph,
            float line,
            Rect viewport,
            Rect[] obstacles,
            int obstacleCount,
            Vector2[] held)
        {
            var chosen = new int[MapCountryLabels.Capacity];
            var occupied = new Rect[MapCountryLabels.Capacity];
            var resolved = new Vector2[screen.Length];
            var count = MapCountryLabels.Choose(zoom, locale, screen, glyph, null, line, viewport, chosen, occupied);
            count = MapCountryLabels.AvoidObstacles(
                count,
                chosen,
                screen,
                locale,
                glyph,
                null,
                line,
                viewport,
                occupied,
                obstacles,
                obstacleCount,
                maxNudge,
                resolved,
                held);
            var items = new MapCountryLabelLayer.Placed[count];
            for (var i = 0; i < count; i++)
            {
                var entry = MapCountryCatalog.All[chosen[i]];
                items[i] = new MapCountryLabelLayer.Placed(
                    entry.Code,
                    MapCountryLabels.Name(entry, locale),
                    resolved[chosen[i]],
                    occupied[i],
                    false);
            }

            return new PlacedSet { Items = items, Count = count };
        }

        static Vector2 ScreenOf(string code, View view)
        {
            var entry = Country(code);
            var world = MapProjection.Project(entry.Latitude, entry.Longitude);
            return MapMarkerLod.ToScreen(world, view.Center, view.Units, view.Width, view.Height);
        }

        static bool Has(PlacedSet placed, string code)
        {
            return FindIndex(placed, code) >= 0;
        }

        static MapCountryLabelLayer.Placed Find(PlacedSet placed, string code)
        {
            var index = FindIndex(placed, code);
            Assert.GreaterOrEqual(index, 0, code);
            return placed.Items[index];
        }

        static int FindIndex(PlacedSet placed, string code)
        {
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed.Items[i].Code == code)
                    return i;
            }

            return -1;
        }

        static bool AnyHit(PlacedSet placed, Rect[] obstacles, int count)
        {
            for (var i = 0; i < placed.Count; i++)
            {
                if (Hits(placed.Items[i].Bounds, obstacles, count))
                    return true;
            }

            return false;
        }

        static bool Hits(Rect rect, Rect[] obstacles, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (MapCountryLabels.Overlaps(rect, obstacles[i]))
                    return true;
            }

            return false;
        }

        static Rect Largest(Rect[] rects, int count)
        {
            var best = rects[0];
            for (var i = 1; i < count; i++)
            {
                if (rects[i].width * rects[i].height > best.width * best.height)
                    best = rects[i];
            }

            return best;
        }

        static void AssertPlaced(MapCountryLabelLayer.Placed[] placed, int count, string code, string text)
        {
            for (var i = 0; i < count; i++)
            {
                if (placed[i].Code != code)
                    continue;
                Assert.AreEqual(text, placed[i].Text, code);
                return;
            }

            Assert.Fail("Missing country label " + code);
        }

        static void AssertScreen(MapCountryLabelLayer.Placed[] placed, int count, Vector2 center, float size, float width, float height)
        {
            var units = MapMarkerLod.UnitsPerPixel(size, height);
            for (var i = 0; i < count; i++)
            {
                var entry = Country(placed[i].Code);
                var world = MapProjection.Project(entry.Latitude, entry.Longitude);
                var screen = MapMarkerLod.ToScreen(world, center, units, width, height);
                Assert.AreEqual(screen.x, placed[i].Screen.x, 0.01f, placed[i].Code);
                Assert.AreEqual(screen.y, placed[i].Screen.y, 0.01f, placed[i].Code);
            }
        }

        static Vector2[] Project(Vector2 center, float size, float width, float height)
        {
            var units = MapMarkerLod.UnitsPerPixel(size, height);
            var screen = new Vector2[MapCountryCatalog.All.Length];
            for (var i = 0; i < screen.Length; i++)
            {
                var entry = MapCountryCatalog.All[i];
                var world = MapProjection.Project(entry.Latitude, entry.Longitude);
                screen[i] = MapMarkerLod.ToScreen(world, center, units, width, height);
            }

            return screen;
        }

        static float FrameSize(double minLongitude, double maxLongitude, double minLatitude, double maxLatitude, float aspect, float padding)
        {
            MapFrames.ProjectedBounds(minLatitude, maxLatitude, minLongitude, maxLongitude, out var min, out var max);
            MapViewMath.Fit(min, max, aspect, padding, out _, out var size);
            return size;
        }

        static MapCountryCatalog.Entry Country(string code)
        {
            var index = MapCountryCatalog.IndexOf(code);
            Assert.GreaterOrEqual(index, 0, code);
            return MapCountryCatalog.All[index];
        }

        static bool Contains(int[] values, int count, int index)
        {
            for (var i = 0; i < count; i++)
            {
                if (values[i] == index)
                    return true;
            }

            return false;
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
                var text = new System.Text.StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                    text.Append(hash[i].ToString("x2"));
                return text.ToString();
            }
        }
    }
}
