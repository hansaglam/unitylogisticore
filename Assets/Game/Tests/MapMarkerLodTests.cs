using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LogistiCore.Tests
{
    public sealed class MapMarkerLodTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha256 = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string ReliefSha256 = "ac9c14157f50b3d2516994a5108cdfcf643f88d8f79bb905700795961616266d";

        [Test]
        public void WorldFrames_CollapseEveryCityIntoOneAggregate()
        {
            ExpectAggregate(1920, 1080);
            ExpectAggregate(844, 390);
            ExpectAggregate(960, 540);
        }

        [Test]
        public void TurkeyFrames_ShowTheEightCities()
        {
            ExpectIndividuals(1920, 1080);
            ExpectIndividuals(844, 390);
            ExpectIndividuals(960, 540);
        }

        [Test]
        public void Clustering_HoldsAGroupUntilTheSplitDistance()
        {
            var screen = new[] { Vector2.zero, new Vector2(20f, 0f) };
            var together = new bool[4];
            var parent = new int[2];
            MapMarkerLod.Cluster(screen, 2, false, together, parent);
            Assert.AreEqual(parent[0], parent[1]);

            screen[1] = new Vector2(36f, 0f);
            MapMarkerLod.Cluster(screen, 2, false, together, parent);
            Assert.AreEqual(parent[0], parent[1], "A pair that was grouped stays grouped through the gap between the thresholds.");

            screen[1] = new Vector2(50f, 0f);
            MapMarkerLod.Cluster(screen, 2, false, together, parent);
            Assert.AreNotEqual(parent[0], parent[1]);

            screen[1] = new Vector2(30f, 0f);
            MapMarkerLod.Cluster(screen, 2, false, together, parent);
            Assert.AreNotEqual(parent[0], parent[1], "Separated markers do not rejoin until they are inside the closer threshold.");
        }

        [Test]
        public void TightNeighbors_UseASmallerCoreAndHalo()
        {
            Assert.AreEqual(34f, MapMarkerLod.CorePixels(120f), 0.01f);
            Assert.AreEqual(MapMarkerLod.FullGlowScale, MapMarkerLod.GlowScale(34f), 0.01f);
            var tight = MapMarkerLod.CorePixels(37f);
            Assert.Less(tight, 20f);
            Assert.GreaterOrEqual(tight, 14f);
            Assert.AreEqual(MapMarkerLod.TightGlowScale, MapMarkerLod.GlowScale(tight), 0.01f);
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame);
        }

        [Test]
        public void Layer_ReplacesTheWorldGlowAndFocusesTurkeyWithoutSelectingACity()
        {
            var harness = new Harness(1920, 1080, turkey: false);
            try
            {
                harness.Layer.ApplyViewport(1920, 1080, snap: true);
                Assert.AreEqual(0, harness.Layer.IndividualCount);
                Assert.AreEqual(1, harness.Layer.ActiveAggregateCount);
                Assert.AreEqual(8, harness.Layer.LargestAggregateMembers);
                Assert.IsFalse(harness.Cores[0].enabled);
                Assert.IsFalse(harness.Glows[0].enabled);
                Assert.IsTrue(harness.Layer.TryAggregateHitPoint(out var hit, out var members));
                Assert.AreEqual(8, members);
                Assert.AreEqual("8", harness.CountText.text);
                Assert.IsFalse(harness.Plates[0].activeSelf);

                Assert.IsTrue(harness.Layer.TrySelect(hit, out var cityId));
                Assert.IsNull(cityId);
                Assert.IsNull(harness.Layer.SelectedId);
                Assert.AreEqual(MapScope.Turkey, harness.Rig.Scope);

                Assert.IsFalse(harness.Layer.TrySelect(new Vector2(2f, 2f), out _));
                Assert.IsNull(harness.Layer.SelectedId);
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void Layer_PreservesTheSelectedCityWhenTheLodChanges()
        {
            var harness = new Harness(1920, 1080, turkey: true);
            try
            {
                harness.Layer.ApplyViewport(1920, 1080, snap: true);
                Assert.AreEqual(8, harness.Layer.IndividualCount);
                Assert.AreEqual(0, harness.Layer.ActiveAggregateCount);
                Assert.IsTrue(harness.Cores[1].enabled);
                Assert.IsTrue(harness.Plates[1].activeSelf);

                var istanbul = harness.ScreenOf(1);
                Assert.IsTrue(harness.Layer.TryMarkerScreen(1, out var markerScreen));
                Assert.AreEqual(istanbul.x, markerScreen.x, 0.5f);
                Assert.AreEqual(istanbul.y, markerScreen.y, 0.5f);
                Assert.IsTrue(harness.Layer.TrySelect(markerScreen, out var selected));
                Assert.AreEqual("istanbul", selected);
                Assert.AreEqual("istanbul", harness.Layer.SelectedId);

                harness.Frame(1920, 1080, turkey: false);
                harness.Layer.ApplyViewport(1920, 1080, snap: true);
                Assert.AreEqual(0, harness.Layer.IndividualCount);
                Assert.AreEqual("istanbul", harness.Layer.SelectedId);
                Assert.IsFalse(harness.Cores[1].enabled);

                Assert.IsTrue(harness.Layer.TryAggregateHitPoint(out var hit, out _));
                Assert.IsTrue(harness.Layer.TrySelect(hit, out var aggregateCity));
                Assert.IsNull(aggregateCity);
                Assert.AreEqual("istanbul", harness.Layer.SelectedId);

                harness.Frame(844, 390, turkey: true);
                harness.Layer.ApplyViewport(844, 390, snap: true);
                Assert.AreEqual(8, harness.Layer.IndividualCount);
                Assert.AreEqual("istanbul", harness.Layer.SelectedId);
                Assert.IsTrue(harness.Plates[1].activeSelf);
                Assert.IsFalse(harness.Plates[3].activeSelf, "Bursa’s label hides while it sits on Istanbul’s label.");
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void Labels_ReadAtShortLandscapeAndKeepTheAggregateQuiet()
        {
            var scale = Harness.CanvasMatch(844f, 390f);
            Assert.GreaterOrEqual(MapMarkerLayer.LabelFontSize(scale, false) * scale, 13f);
            Assert.Greater(MapMarkerLayer.LabelFontSize(scale, true), MapMarkerLayer.LabelFontSize(scale, false));
            Assert.AreEqual(16, MapMarkerLayer.LabelFontSize(1f, false));
            Assert.Less(MapMarkerLayer.GlowVisualScale(MapMarkerLod.FullGlowScale, false), MapMarkerLod.FullGlowScale);
            Assert.Greater(
                MapMarkerLayer.GlowVisualScale(MapMarkerLod.FullGlowScale, true),
                MapMarkerLayer.GlowVisualScale(MapMarkerLod.FullGlowScale, false));
            Assert.AreEqual(48f, MapMarkerLod.MarkerHitPixels);

            var harness = new Harness(844, 390, turkey: true);
            try
            {
                var names = new[]
                {
                    "İzmir", "İstanbul", "Ankara", "Bursa", "Antalya", "Adana", "Diyarbakır", "Trabzon"
                };
                harness.UseCanvasScale(844, 390);
                for (var i = 0; i < names.Length; i++)
                    harness.Plates[i].GetComponentInChildren<Text>(true).text = names[i];

                harness.Layer.ApplyViewport(844, 390, snap: true);
                var ankara = harness.Plates[2].GetComponentInChildren<Text>(true);
                var izmir = harness.Plates[0].GetComponentInChildren<Text>(true);
                Assert.IsTrue(harness.Plates[2].activeSelf);
                Assert.IsFalse(harness.Plates[3].activeSelf, "Bursa still yields when it collides with Istanbul.");
                Assert.AreEqual(MapMarkerLayer.LabelFontSize(scale, false), ankara.fontSize);
                Assert.GreaterOrEqual(ankara.fontSize * scale, 13f);
                Assert.Less(ankara.rectTransform.parent.GetComponent<RectTransform>().sizeDelta.x * scale, 180f);
                Assert.AreEqual(FontStyle.Bold, ankara.fontStyle);

                var ankaraScreen = harness.ScreenOf(2);
                Assert.IsTrue(harness.Layer.TrySelect(ankaraScreen + new Vector2(40f, 0f), out var hit));
                Assert.AreEqual("ankara", hit);
                harness.Layer.ApplyViewport(844, 390, snap: true);
                Assert.Greater(ankara.fontSize, izmir.fontSize);
                Assert.IsTrue(harness.Plates[2].activeSelf);
                Assert.Less(harness.Glows[2].color.a, 0.45f);
                Assert.Less(harness.Cores[2].color.r, 0.9f);
                Assert.Greater(harness.Glows[2].transform.localScale.y, harness.Glows[0].transform.localScale.y);
                Assert.Less(harness.Glows[0].transform.localScale.y, MapMarkerLod.FullGlowScale);

                harness.Frame(844, 390, turkey: false);
                harness.Layer.ApplyViewport(844, 390, snap: true);
                Assert.AreEqual(0, harness.Layer.IndividualCount);
                Assert.AreEqual(8, harness.Layer.LargestAggregateMembers);
                for (var i = 0; i < harness.Plates.Length; i++)
                    Assert.IsFalse(harness.Plates[i].activeSelf, names[i]);
                Text aggregate = null;
                var captions = harness.Plates[0].transform.parent.GetComponentsInChildren<Text>(true);
                for (var i = 0; i < captions.Length; i++)
                {
                    if (captions[i].gameObject.name == "GroupCount")
                        aggregate = captions[i];
                }

                Assert.IsNotNull(aggregate);
                Assert.AreEqual("8", aggregate.text);
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void InitialTurkey_SelectsIzmirWithoutCoveringItWithTheRail()
        {
            Assert.AreEqual(34f, MapMarkerLod.AggregateScreenPixels);
            Assert.AreEqual(22f, MapMarkerLod.AggregateHitRadius);
            Assert.AreEqual(34f, MapMarkerLod.CoreScreenPixels);
            Assert.AreEqual(18f, MapCameraRig.CityClearancePixels, 0.01f);
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame);
            Assert.AreEqual(38.438096d, MapCities.All[0].Latitude, 0.0000001d);
            Assert.AreEqual(27.149848d, MapCities.All[0].Longitude, 0.0000001d);
            Assert.AreEqual("izmir", MapCities.All[0].Id);
            Assert.AreEqual("ankara", MapCities.All[2].Id);
            Assert.AreEqual("adana", MapCities.All[5].Id);

            var viewports = new[]
            {
                new Viewport(2868f, 1320f, new Rect(186f, 60f, 2496f, 1200f)),
                new Viewport(2868f, 1320f, new Rect(0f, 0f, 2868f, 1320f)),
                new Viewport(844f, 390f, new Rect(0f, 0f, 844f, 390f)),
                new Viewport(932f, 430f, new Rect(0f, 0f, 932f, 430f)),
                new Viewport(960f, 540f, new Rect(0f, 0f, 960f, 540f)),
                new Viewport(1280f, 720f, new Rect(0f, 0f, 1280f, 720f)),
                new Viewport(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f))
            };

            for (var v = 0; v < viewports.Length; v++)
                ExpectCitiesClearOfRail(viewports[v]);

            var phone = viewports[0];
            var metrics = MapHudLayout.Evaluate(phone.Width, phone.Height, phone.Safe, true);
            foreach (var label in new[] { "Türkiye", "Türkei", "Sıfırla", "Zurück", "Reset", "Dünya", "Welt" })
                Assert.IsTrue(MapHudLayout.FitsOneLine(label, metrics.RailLabelSize, metrics.RailTextWidth), label);

            var harness = new Harness((int)phone.Width, (int)phone.Height, turkey: true);
            try
            {
                harness.Frame((int)phone.Width, (int)phone.Height, turkey: true);
                var camera = harness.Rig.GetComponent<Camera>();
                var fitted = new Vector2(camera.transform.position.x, camera.transform.position.y);
                var size = camera.orthographicSize;
                var cleared = MapCameraRig.CenterClearOfRail(fitted, size, phone.Width, phone.Height, phone.Safe);
                Assert.AreEqual(fitted.y, cleared.y, 0.0001f);
                Assert.Less(cleared.x, fitted.x, "The camera moves west so İzmir draws to the right of the rail.");
                camera.transform.position = new Vector3(cleared.x, cleared.y, MapCameraRig.CameraZ);
                harness.Layer.ApplyViewport((int)phone.Width, (int)phone.Height, snap: true);

                var buttons = MapCameraRig.RailButtons(phone.Width, phone.Height, phone.Safe);
                Assert.AreEqual(5, buttons.Length);
                var restore = buttons[4];
                Assert.Greater(restore.width, 40f);
                Assert.Greater(restore.height, 40f);
                var izmir = harness.ScreenOf(0);
                Assert.IsFalse(Contains(restore, izmir));
                Assert.IsFalse(Contains(buttons[3], izmir), "Zoom out must not own İzmir’s tap.");
                for (var i = 0; i < MapCities.All.Length; i++)
                {
                    var point = harness.ScreenOf(i);
                    for (var b = 0; b < buttons.Length; b++)
                        Assert.IsFalse(Contains(buttons[b], point), MapCities.All[i].Id);
                }

                var before = camera.transform.position;
                var beforeSize = camera.orthographicSize;
                Assert.IsTrue(harness.Layer.TrySelect(izmir, out var selected));
                Assert.AreEqual("izmir", selected);
                Assert.AreEqual(before, camera.transform.position);
                Assert.AreEqual(beforeSize, camera.orthographicSize, 0.0001f);

                harness.Layer.ClearSelection();
                Assert.IsTrue(harness.Layer.TrySelect(harness.ScreenOf(2), out var ankara));
                Assert.AreEqual("ankara", ankara);
                Assert.IsTrue(harness.Layer.TrySelect(harness.ScreenOf(5), out var adana));
                Assert.AreEqual("adana", adana);
                Assert.AreEqual(beforeSize, camera.orthographicSize, 0.0001f);

                var unshiftedUnits = MapMarkerLod.UnitsPerPixel(size, phone.Height);
                var unshiftedIzmir = MapMarkerLod.ToScreen(
                    MapProjection.Project(MapCities.All[0].Latitude, MapCities.All[0].Longitude),
                    fitted,
                    unshiftedUnits,
                    phone.Width,
                    phone.Height);
                Assert.IsTrue(Contains(buttons[3], unshiftedIzmir), "The unshifted frame is the measured overlap.");
            }
            finally
            {
                harness.Dispose();
            }

            var rigObject = new GameObject("turkey-access-rig");
            try
            {
                var rigCamera = rigObject.AddComponent<Camera>();
                rigCamera.orthographic = true;
                rigCamera.aspect = 1920f / 1080f;
                var rig = rigObject.AddComponent<MapCameraRig>();
                rig.ApplyImmediate(MapScope.World);
                MapFrames.WorldPresentation(out var worldMin, out var worldMax);
                MapViewMath.Fit(worldMin, worldMax, rigCamera.aspect, MapFrames.WorldFocusPadding, out var worldCenter, out var worldSize);
                Assert.AreEqual(worldCenter.x, rigCamera.transform.position.x, 0.02f);
                Assert.AreEqual(worldCenter.y, rigCamera.transform.position.y, 0.02f);
                Assert.AreEqual(worldSize, rigCamera.orthographicSize, 0.02f);

                rig.ApplyImmediate(MapScope.Turkey);
                var panBefore = rigCamera.transform.position.x;
                rig.PanScreenDelta(new Vector2(40f, 0f));
                Assert.AreNotEqual(panBefore, rigCamera.transform.position.x);
                var zoomBefore = rig.TargetOrthographicSize;
                rig.ZoomAtScreen(new Vector2(10f, 10f), 1.25f);
                Assert.Less(rig.TargetOrthographicSize, zoomBefore);
                Assert.GreaterOrEqual(rig.TargetOrthographicSize, MapFrames.MinimumOrthographicSize(rigCamera.aspect) - 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rigObject);
            }
        }

        static void ExpectCitiesClearOfRail(Viewport viewport)
        {
            var aspect = viewport.Width / viewport.Height;
            MapFrames.Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.TurkeyFocusPadding, out var fitted, out var size);
            var cleared = MapCameraRig.CenterClearOfRail(fitted, size, viewport.Width, viewport.Height, viewport.Safe);
            Assert.AreEqual(fitted.y, cleared.y, 0.0001f);
            var units = MapMarkerLod.UnitsPerPixel(size, viewport.Height);
            var buttons = MapCameraRig.RailButtons(viewport.Width, viewport.Height, viewport.Safe);
            for (var i = 0; i < MapCities.All.Length; i++)
            {
                var city = MapCities.All[i];
                var world = MapProjection.Project(city.Latitude, city.Longitude);
                var screen = MapMarkerLod.ToScreen(world, cleared, units, viewport.Width, viewport.Height);
                Assert.Greater(screen.x, 0f, city.Id);
                Assert.Less(screen.x, viewport.Width, city.Id);
                Assert.Greater(screen.y, 0f, city.Id);
                Assert.Less(screen.y, viewport.Height, city.Id);
                for (var b = 0; b < buttons.Length; b++)
                {
                    Assert.IsFalse(Contains(buttons[b], screen), city.Id + " button " + b);
                    var gap = buttons[b].xMax + MapCameraRig.CityClearancePixels - screen.x;
                    var verticallyNear = screen.y >= buttons[b].yMin - MapCameraRig.CityClearancePixels
                        && screen.y <= buttons[b].yMax + MapCameraRig.CityClearancePixels;
                    if (verticallyNear)
                        Assert.LessOrEqual(gap, 0.5f, city.Id);
                }
            }
        }

        static bool Contains(Rect rect, Vector2 point)
        {
            return point.x >= rect.xMin && point.x <= rect.xMax && point.y >= rect.yMin && point.y <= rect.yMax;
        }

        readonly struct Viewport
        {
            public readonly float Width;
            public readonly float Height;
            public readonly Rect Safe;

            public Viewport(float width, float height, Rect safe)
            {
                Width = width;
                Height = height;
                Safe = safe;
            }
        }

        [Test]
        public void AggregateBadge_DrawsAtThirtyFourPixelsWithoutMovingOrResizingCities()
        {
            Assert.AreEqual(34f, MapMarkerLod.AggregateScreenPixels);
            Assert.AreEqual(22f, MapMarkerLod.AggregateHitRadius);
            Assert.AreEqual(34f, MapMarkerLod.CoreScreenPixels);
            Assert.AreEqual(26f, MapMarkerLod.JoinPixels);
            Assert.AreEqual(44f, MapMarkerLod.SplitPixels);
            Assert.AreEqual(1.08f, MapMarkerLod.CountryFrameFactor);
            Assert.AreEqual(48f, MapMarkerLod.MarkerHitPixels);
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Map/Generated/MapMarker.png");
            Assert.IsNotNull(sprite);
            Assert.AreEqual(1f, sprite.rect.width / sprite.pixelsPerUnit, 0.001f);
            Assert.AreEqual(1f, sprite.rect.height / sprite.pixelsPerUnit, 0.001f);
            Assert.AreEqual(0.5f, sprite.pivot.x / sprite.rect.width, 0.01f);
            Assert.AreEqual(0.5f, sprite.pivot.y / sprite.rect.height, 0.01f);

            var harness = new Harness(844, 390, turkey: false);
            try
            {
                ExpectAggregateDiameter(harness, 844, 390, sprite);
                ExpectAggregateDiameter(harness, 1920, 1080, sprite);

                harness.Frame(844, 390, turkey: false);
                harness.UseCanvasScale(844, 390);
                harness.Layer.ApplyViewport(844, 390, snap: true);
                Assert.AreEqual(0, harness.Layer.IndividualCount);
                Assert.AreEqual(1, harness.Layer.ActiveAggregateCount);
                Assert.AreEqual(8, harness.Layer.LargestAggregateMembers);
                Assert.AreEqual("8", harness.CountText.text);
                AssertGlyphOnRing(harness);
                Assert.IsTrue(harness.Layer.TryAggregateHitPoint(out var hit, out var members));
                Assert.AreEqual(8, members);

                var camera = harness.Rig.GetComponent<Camera>();
                var sizeBefore = camera.orthographicSize;
                var positionBefore = camera.transform.position;
                Assert.IsFalse(harness.Layer.TrySelect(hit + new Vector2(23f, 0f), out _));
                Assert.AreEqual(sizeBefore, camera.orthographicSize, 0.0001f);
                Assert.AreEqual(positionBefore, camera.transform.position);
                Assert.IsTrue(harness.Layer.TrySelect(hit + new Vector2(21f, 0f), out var aggregateCity));
                Assert.IsNull(aggregateCity);
                Assert.IsNull(harness.Layer.SelectedId);
                Assert.AreEqual(MapScope.Turkey, harness.Rig.Scope);

                harness.Frame(844, 390, turkey: true);
                harness.UseCanvasScale(844, 390);
                harness.Layer.ApplyViewport(844, 390, snap: true);
                Assert.AreEqual(8, harness.Layer.IndividualCount);
                Assert.AreEqual(0, harness.Layer.ActiveAggregateCount);
                AssertCityCoreFollowsItsOwnPixels(harness, 1, 390);
                var turkeySize = camera.orthographicSize;
                var turkeyPosition = camera.transform.position;
                Assert.IsTrue(harness.Layer.TrySelect(harness.ScreenOf(2), out var ankara));
                Assert.AreEqual("ankara", ankara);
                Assert.AreEqual("ankara", harness.Layer.SelectedId);
                Assert.AreEqual(turkeySize, camera.orthographicSize, 0.0001f);
                Assert.AreEqual(turkeyPosition, camera.transform.position);
            }
            finally
            {
                harness.Dispose();
            }
        }

        static void ExpectAggregateDiameter(Harness harness, int width, int height, Sprite sprite)
        {
            harness.Frame(width, height, turkey: false);
            harness.UseCanvasScale(width, height);
            harness.Layer.ApplyViewport(width, height, snap: true);
            var group = harness.Layer.transform.Find("CityGroup");
            Assert.IsNotNull(group);
            var camera = harness.Rig.GetComponent<Camera>();
            var units = MapMarkerLod.UnitsPerPixel(camera.orthographicSize, height);
            Assert.AreEqual(34f, group.localScale.x / units, 0.02f);
            Assert.AreEqual(group.localScale.x, group.localScale.y, 0.0001f);

            var sum = Vector2.zero;
            for (var i = 0; i < MapCities.All.Length; i++)
            {
                var city = MapCities.All[i];
                sum += MapProjection.Project(city.Latitude, city.Longitude);
            }

            var centroid = sum / MapCities.All.Length;
            Assert.AreEqual(centroid.x, group.position.x, 0.001f);
            Assert.AreEqual(centroid.y, group.position.y, 0.001f);

            var icon = group.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(icon);
            icon.sprite = sprite;
            Assert.AreEqual(centroid.x, icon.bounds.center.x, 0.05f);
            Assert.AreEqual(centroid.y, icon.bounds.center.y, 0.05f);
            var painted = icon.bounds.size.x / units;
            Assert.Greater(painted, 30f);
            Assert.LessOrEqual(painted, 34.05f);
        }

        static void AssertCityCoreFollowsItsOwnPixels(Harness harness, int index, int height)
        {
            var nearest = float.MaxValue;
            var origin = harness.ScreenOf(index);
            for (var i = 0; i < MapCities.All.Length; i++)
            {
                if (i == index)
                    continue;

                var distance = Vector2.Distance(origin, harness.ScreenOf(i));
                if (distance < nearest)
                    nearest = distance;
            }

            var pixels = MapMarkerLod.CorePixels(nearest);
            Assert.Less(pixels, MapMarkerLod.CoreScreenPixels);
            var camera = harness.Rig.GetComponent<Camera>();
            var expected = MapMarkerLod.ScaleForPixels(pixels, camera.orthographicSize, height);
            var marker = harness.Cores[index].transform.parent;
            Assert.AreEqual(expected, marker.localScale.x, 0.02f);
            Assert.AreEqual(expected, marker.localScale.y, 0.02f);
            Assert.AreNotEqual(MapMarkerLod.AggregateScreenPixels, pixels);
        }

        [Test]
        public void BadgeCount_SitsOnTheRingAtEveryWorldViewport()
        {
            Assert.AreEqual(26f, MapMarkerLod.JoinPixels);
            Assert.AreEqual(44f, MapMarkerLod.SplitPixels);
            Assert.AreEqual(34f, MapMarkerLod.AggregateScreenPixels);
            Assert.AreEqual(22f, MapMarkerLod.AggregateHitRadius);
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame);

            var harness = new Harness(1920, 1080, turkey: false);
            try
            {
                ExpectCenteredBadge(harness, 1920, 1080, "8");
                ExpectCenteredBadge(harness, 844, 390, "8");
                ExpectCenteredBadge(harness, 960, 540, "8");
                ExpectCenteredBadge(harness, 932, 430, "8");

                harness.Frame(1920, 1080, turkey: false);
                harness.UseCanvasScale(1920, 1080);
                harness.SetOrthographicSize(40f);
                harness.Layer.ApplyViewport(1920, 1080, snap: true);
                Assert.AreEqual("2", harness.CountText.text, "The digit follows the live cluster size.");
                AssertGlyphOnRing(harness);

                var center = harness.Rig.transform.position;
                harness.Rig.transform.position = center + new Vector3(12f, -6f, 0f);
                harness.Layer.ApplyViewport(1920, 1080, snap: true);
                AssertGlyphOnRing(harness);

                harness.Frame(844, 390, turkey: true);
                harness.UseCanvasScale(844, 390);
                harness.Layer.ApplyViewport(844, 390, snap: true);
                Assert.AreEqual(8, harness.Layer.IndividualCount);
                Assert.AreEqual(0, harness.Layer.ActiveAggregateCount);
            }
            finally
            {
                harness.Dispose();
            }
        }

        static void ExpectCenteredBadge(Harness harness, int width, int height, string count)
        {
            harness.Frame(width, height, turkey: false);
            harness.UseCanvasScale(width, height);
            harness.Layer.ApplyViewport(width, height, snap: true);
            Assert.AreEqual(0, harness.Layer.IndividualCount);
            Assert.AreEqual(count, harness.CountText.text);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), harness.CountText.rectTransform.pivot);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), harness.CountText.rectTransform.anchorMin);
            AssertGlyphOnRing(harness);
        }

        static void AssertGlyphOnRing(Harness harness)
        {
            Assert.IsTrue(harness.Layer.TryAggregateHitPoint(out var ring, out var members));
            Assert.AreEqual(members.ToString(), harness.CountText.text);
            var ink = MapMarkerLayer.GlyphCenterOffset(harness.CountText);
            var inkWorld = harness.CountText.rectTransform.TransformPoint(new Vector3(ink.x, ink.y, 0f));
            var inkScreen = RectTransformUtility.WorldToScreenPoint(null, inkWorld);
            Assert.AreEqual(ring.x, inkScreen.x, 1.5f);
            Assert.AreEqual(ring.y, inkScreen.y, 1.5f);

            var pivotScreen = RectTransformUtility.WorldToScreenPoint(null, harness.CountText.rectTransform.position);
            Assert.Less(Mathf.Abs(pivotScreen.y - ring.y), 6f);
            Assert.Less(Mathf.Abs(pivotScreen.x - ring.x), 6f);
        }

        [Test]
        public void PanBeyondTheSlop_IsNotATap()
        {
            Assert.IsTrue(MapGestures.IsTap(0f));
            Assert.IsTrue(MapGestures.IsTap(MapGestures.TapSlopPixels));
            Assert.IsFalse(MapGestures.IsTap(MapGestures.TapSlopPixels + 0.1f));
        }

        [Test]
        public void SceneReliefAndCities_StayUntouched()
        {
            Assert.AreEqual(SceneSha256, Sha256(ScenePath));
            Assert.AreEqual(ReliefSha256, Sha256("Assets/Game/Map/Resources/MapReliefRegion.png"));
            Assert.AreEqual(1024, MapRegionalRelief.Width);
            Assert.AreEqual(512, MapRegionalRelief.Height);
            Assert.AreEqual(8, MapCities.All.Length);
            Assert.AreEqual("ankara", MapCities.All[2].Id);
            Assert.AreEqual(39.929184d, MapCities.All[2].Latitude, 0.0000001d);
            Assert.AreEqual(32.862446d, MapCities.All[2].Longitude, 0.0000001d);
        }

        static void ExpectAggregate(int width, int height)
        {
            var aspect = width / (float)height;
            var screen = Project(aspect, width, height, turkey: false, out var size, out var turkeyFrame);
            Assert.IsFalse(MapMarkerLod.AtCountryScale(size, turkeyFrame));
            var together = new bool[screen.Length * screen.Length];
            var parent = new int[screen.Length];
            MapMarkerLod.Cluster(screen, screen.Length, false, together, parent);
            Assert.AreEqual(1, Distinct(parent));
            Assert.AreEqual(8, MapMarkerLod.ClusterSize(parent, parent.Length, 0));
        }

        static void ExpectIndividuals(int width, int height)
        {
            var aspect = width / (float)height;
            var screen = Project(aspect, width, height, turkey: true, out var size, out var turkeyFrame);
            Assert.IsTrue(MapMarkerLod.AtCountryScale(size, turkeyFrame));
            var together = new bool[screen.Length * screen.Length];
            var parent = new int[screen.Length];
            MapMarkerLod.Cluster(screen, screen.Length, true, together, parent);
            Assert.AreEqual(8, Distinct(parent));
        }

        static Vector2[] Project(float aspect, int width, int height, bool turkey, out float size, out float turkeyFrame)
        {
            Vector2 min;
            Vector2 max;
            if (turkey)
                MapFrames.Turkey(out min, out max);
            else
                MapFrames.World(out min, out max);
            var padding = turkey ? MapFrames.TurkeyFocusPadding : MapFrames.WorldFocusPadding;
            MapViewMath.Fit(min, max, aspect, padding, out var center, out size);
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out turkeyFrame);
            var units = MapMarkerLod.UnitsPerPixel(size, height);
            var screen = new Vector2[MapCities.All.Length];
            for (var i = 0; i < MapCities.All.Length; i++)
            {
                var city = MapCities.All[i];
                var world = MapProjection.Project(city.Latitude, city.Longitude);
                screen[i] = MapMarkerLod.ToScreen(world, center, units, width, height);
            }

            return screen;
        }

        static int Distinct(int[] parent)
        {
            var seen = new HashSet<int>();
            for (var i = 0; i < parent.Length; i++)
                seen.Add(parent[i]);
            return seen.Count;
        }

        static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return System.BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        sealed class Harness
        {
            public readonly MapMarkerLayer Layer;
            public readonly MapCameraRig Rig;
            public readonly SpriteRenderer[] Cores;
            public readonly SpriteRenderer[] Glows;
            public readonly GameObject[] Plates;
            public readonly Text CountText;
            public readonly CanvasScaler Scaler;
            readonly GameObject _cameraObject;
            readonly GameObject _layerObject;
            readonly GameObject _canvasObject;
            readonly Camera _camera;
            int _width;
            int _height;
            MapCityMarker[] _markers;

            public Harness(int width, int height, bool turkey)
            {
                _cameraObject = new GameObject("lod-camera");
                _camera = _cameraObject.AddComponent<Camera>();
                _camera.orthographic = true;
                _camera.pixelRect = new Rect(0f, 0f, width, height);
                Rig = _cameraObject.AddComponent<MapCameraRig>();

                _canvasObject = new GameObject("lod-canvas", typeof(RectTransform));
                var labelRoot = _canvasObject.GetComponent<RectTransform>();
                labelRoot.anchorMin = Vector2.zero;
                labelRoot.anchorMax = Vector2.one;
                labelRoot.pivot = new Vector2(0.5f, 0.5f);
                labelRoot.offsetMin = Vector2.zero;
                labelRoot.offsetMax = Vector2.zero;
                var canvas = _canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Scaler = _canvasObject.AddComponent<CanvasScaler>();
                Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                Scaler.referenceResolution = new Vector2(MapHudLayout.ReferenceWidth, MapHudLayout.ReferenceHeight);
                Scaler.matchWidthOrHeight = MapHudLayout.Match;
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null)
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica" }, 16);

                _layerObject = new GameObject("lod-markers");
                Layer = _layerObject.AddComponent<MapMarkerLayer>();
                var markers = new MapCityMarker[MapCities.All.Length];
                _markers = markers;
                var labels = new Text[MapCities.All.Length];
                Cores = new SpriteRenderer[MapCities.All.Length];
                Glows = new SpriteRenderer[MapCities.All.Length];
                Plates = new GameObject[MapCities.All.Length];

                for (var i = 0; i < MapCities.All.Length; i++)
                {
                    var city = MapCities.All[i];
                    var markerObject = new GameObject("City_" + city.Id);
                    markerObject.transform.SetParent(_layerObject.transform, false);
                    var world = MapProjection.Project(city.Latitude, city.Longitude);
                    markerObject.transform.position = new Vector3(world.x, world.y, 0f);
                    var marker = markerObject.AddComponent<MapCityMarker>();
                    var serialized = new SerializedObject(marker);
                    serialized.FindProperty("cityId").stringValue = city.Id;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    markers[i] = marker;

                    var coreObject = new GameObject("Core");
                    coreObject.transform.SetParent(markerObject.transform, false);
                    Cores[i] = coreObject.AddComponent<SpriteRenderer>();
                    var glowObject = new GameObject("Glow");
                    glowObject.transform.SetParent(markerObject.transform, false);
                    glowObject.transform.localScale = new Vector3(2.8f, 2.8f, 1f);
                    Glows[i] = glowObject.AddComponent<SpriteRenderer>();

                    var plate = new GameObject("Plate_" + city.Id, typeof(RectTransform));
                    plate.transform.SetParent(labelRoot, false);
                    Plates[i] = plate;
                    var labelObject = new GameObject("Label_" + city.Id, typeof(RectTransform));
                    labelObject.transform.SetParent(plate.transform, false);
                    labels[i] = labelObject.AddComponent<Text>();
                    labels[i].font = font;
                    labels[i].raycastTarget = false;
                }

                var layerSerialized = new SerializedObject(Layer);
                layerSerialized.FindProperty("mapCamera").objectReferenceValue = _camera;
                layerSerialized.FindProperty("labelRoot").objectReferenceValue = labelRoot;
                var markerProperty = layerSerialized.FindProperty("markers");
                var labelProperty = layerSerialized.FindProperty("labels");
                markerProperty.arraySize = markers.Length;
                labelProperty.arraySize = labels.Length;
                for (var i = 0; i < markers.Length; i++)
                {
                    markerProperty.GetArrayElementAtIndex(i).objectReferenceValue = markers[i];
                    labelProperty.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
                }

                layerSerialized.ApplyModifiedPropertiesWithoutUndo();
                Canvas.ForceUpdateCanvases();
                Frame(width, height, turkey);
                Layer.ApplyViewport(width, height, snap: true);
                CountText = _layerObject.GetComponentInChildren<Text>(true);
                if (CountText == null)
                {
                    var counts = labelRoot.GetComponentsInChildren<Text>(true);
                    for (var i = 0; i < counts.Length; i++)
                    {
                        if (counts[i].gameObject.name == "GroupCount")
                            CountText = counts[i];
                    }
                }
            }

            public void Frame(int width, int height, bool turkey)
            {
                _width = width;
                _height = height;
                var aspect = width / (float)height;
                _camera.aspect = aspect;
                Vector2 min;
                Vector2 max;
                if (turkey)
                    MapFrames.Turkey(out min, out max);
                else
                    MapFrames.World(out min, out max);
                var padding = turkey ? MapFrames.TurkeyFocusPadding : MapFrames.WorldFocusPadding;
                MapViewMath.Fit(min, max, aspect, padding, out var center, out var size);
                _camera.orthographicSize = size;
                _camera.transform.position = new Vector3(center.x, center.y, MapCameraRig.CameraZ);
            }

            public void SetOrthographicSize(float size)
            {
                _camera.orthographicSize = size;
            }

            public void UseCanvasScale(float width, float height)
            {
                Scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                Scaler.scaleFactor = CanvasMatch(width, height);
                Canvas.ForceUpdateCanvases();
            }

            public static float CanvasMatch(float width, float height)
            {
                var logWidth = Mathf.Log(width / MapHudLayout.ReferenceWidth, 2f);
                var logHeight = Mathf.Log(height / MapHudLayout.ReferenceHeight, 2f);
                return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, MapHudLayout.Match));
            }

            public Vector2 ScreenOf(int index)
            {
                var units = MapMarkerLod.UnitsPerPixel(_camera.orthographicSize, _height);
                var center = new Vector2(_camera.transform.position.x, _camera.transform.position.y);
                var world = _markers[index].transform.position;
                return MapMarkerLod.ToScreen(new Vector2(world.x, world.y), center, units, _width, _height);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_layerObject);
                Object.DestroyImmediate(_canvasObject);
                Object.DestroyImmediate(_cameraObject);
            }
        }
    }
}
