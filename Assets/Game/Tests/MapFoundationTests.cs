using System.Collections.Generic;
using LogistiCore.EditorTools;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogistiCore.Tests
{
    public class MapFoundationTests
    {
        [Test]
        public void Projection_RoundTripsGeographicPoints()
        {
            double[] samples =
            {
                41.017602, 28.974277,
                39.929184, 32.862446,
                0d, 0d,
                -33.8688, 151.2093,
                35.831445, 25.668945
            };

            for (var i = 0; i < samples.Length; i += 2)
            {
                Assert.IsTrue(MapProjection.TryProject(samples[i], samples[i + 1], out var world));
                Assert.IsTrue(MapProjection.TryUnproject(world, out var latitude, out var longitude));
                Assert.AreEqual(samples[i], latitude, 0.0001d);
                Assert.AreEqual(samples[i + 1], longitude, 0.0001d);
            }
        }

        [Test]
        public void Projection_ClampsLatitudeAndWrapsLongitude()
        {
            Assert.IsTrue(MapProjection.TryProject(89d, 10d, out var clamped));
            Assert.IsTrue(MapProjection.TryProject(MapProjection.MaxLatitude, 10d, out var limit));
            Assert.AreEqual(limit.x, clamped.x, 0.0001f);
            Assert.AreEqual(limit.y, clamped.y, 0.0001f);

            Assert.IsTrue(MapProjection.TryProject(10d, 190d, out var wrapped));
            Assert.IsTrue(MapProjection.TryProject(10d, -170d, out var canonical));
            Assert.AreEqual(canonical.x, wrapped.x, 0.0001f);
            Assert.AreEqual(canonical.y, wrapped.y, 0.0001f);
            Assert.AreEqual(-170d, MapProjection.WrapLongitude(190d), 0.000001d);
            Assert.AreEqual(170d, MapProjection.WrapLongitude(-190d), 0.000001d);
            Assert.IsTrue(MapProjection.TryProjectUnwrapped(0d, 180d, out var east));
            Assert.IsTrue(MapProjection.TryProjectUnwrapped(0d, -180d, out var west));
            Assert.Greater(east.x, 0f);
            Assert.Less(west.x, 0f);
        }

        [Test]
        public void Projection_RejectsNonFiniteInput()
        {
            Assert.IsFalse(MapProjection.TryProject(double.NaN, 0d, out _));
            Assert.IsFalse(MapProjection.TryProject(0d, double.PositiveInfinity, out _));
            Assert.IsFalse(MapProjection.TryUnproject(new Vector2(float.NaN, 0f), out _, out _));
        }

        [Test]
        public void ViewMath_FitsWorldAndTurkeyInsideTheCamera()
        {
            float[] aspects = { 16f / 9f, 19.5f / 9f, 20f / 9f };
            for (var i = 0; i < aspects.Length; i++)
            {
                AssertFrameContains(MapScope.World, aspects[i]);
                AssertFrameContains(MapScope.Turkey, aspects[i]);
            }
        }

        [Test]
        public void ViewMath_ClampsZoomAndKeepsThePointerStable()
        {
            MapFrames.World(out var min, out var max);
            var aspect = 16f / 9f;
            var maxSize = MapFrames.MaximumOrthographicSize(aspect);
            var minSize = MapFrames.MinimumOrthographicSize(aspect);
            Assert.Greater(maxSize, minSize);
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkeyFrame);
            Assert.GreaterOrEqual(minSize, turkeyFrame * 0.40f);
            Assert.Less(minSize, turkeyFrame);
            Assert.Greater(maxSize, turkeyFrame);

            var center = (min + max) * 0.5f;
            var size = maxSize * 0.45f;
            var pointer = new Vector2(640f, 420f);
            MapViewMath.ZoomAbout(
                center,
                size,
                pointer,
                1.35f,
                aspect,
                1920f,
                1080f,
                minSize,
                maxSize,
                min,
                max,
                out var zoomedCenter,
                out var zoomedSize);

            Assert.Less(zoomedSize, size);
            Assert.GreaterOrEqual(zoomedSize, minSize);
            Assert.LessOrEqual(zoomedSize, maxSize);
            var before = MapViewMath.ScreenToWorld(pointer, center, size, aspect, 1920f, 1080f);
            var after = MapViewMath.ScreenToWorld(pointer, zoomedCenter, zoomedSize, aspect, 1920f, 1080f);
            Assert.AreEqual(before.x, after.x, 0.02f);
            Assert.AreEqual(before.y, after.y, 0.02f);

            var tooClose = MapViewMath.ClampSize(minSize * 0.01f, minSize, maxSize);
            var tooFar = MapViewMath.ClampSize(maxSize * 4f, minSize, maxSize);
            Assert.AreEqual(minSize, tooClose, 0.0001f);
            Assert.AreEqual(maxSize, tooFar, 0.0001f);
        }

        [Test]
        public void CameraRig_RefusesZoomPastTheRegionalLimit()
        {
            var cameraObject = new UnityEngine.GameObject("MapCameraRigTest");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = 16f / 9f;
                var rig = cameraObject.AddComponent<MapCameraRig>();
                rig.FocusTurkey();
                var aspect = 16f / 9f;
                var minSize = MapFrames.MinimumOrthographicSize(aspect);
                var maxSize = MapFrames.MaximumOrthographicSize(aspect);
                for (var i = 0; i < 24; i++)
                    rig.ZoomIn();

                Assert.GreaterOrEqual(rig.TargetOrthographicSize, minSize - 0.0001f);
                Assert.Less(rig.TargetOrthographicSize, minSize * 1.08f);

                rig.FocusWorld();
                for (var i = 0; i < 12; i++)
                    rig.ZoomOut();

                Assert.LessOrEqual(rig.TargetOrthographicSize, maxSize + 0.0001f);
                Assert.Greater(rig.TargetOrthographicSize, maxSize * 0.92f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Cities_AreTheEightTurkishPresentationMarkers()
        {
            var expected = new HashSet<string>
            {
                "izmir", "istanbul", "ankara", "bursa", "antalya", "adana", "diyarbakir", "trabzon"
            };
            Assert.AreEqual(8, MapCities.All.Length);
            var seen = new HashSet<string>();
            for (var i = 0; i < MapCities.All.Length; i++)
            {
                var city = MapCities.All[i];
                Assert.IsTrue(seen.Add(city.Id));
                Assert.IsTrue(expected.Contains(city.Id));
                Assert.Greater(city.Latitude, MapFrames.TurkeyMinLatitude);
                Assert.Less(city.Latitude, MapFrames.TurkeyMaxLatitude);
                Assert.Greater(city.Longitude, MapFrames.TurkeyMinLongitude);
                Assert.Less(city.Longitude, MapFrames.TurkeyMaxLongitude);
                Assert.IsTrue(MapProjection.TryProject(city.Latitude, city.Longitude, out var world));
                Assert.IsTrue(float.IsFinite(world.x) && float.IsFinite(world.y));
            }

            Assert.AreEqual(8, seen.Count);
        }

        [Test]
        public void MapStrings_ExistInEnglishTurkishAndGerman()
        {
            MapSceneSetup.EnsureLocalization();
            var collection = LocalizationEditorSettings.GetStringTableCollection(MapContent.Table);
            Assert.IsNotNull(collection);

            foreach (var key in MapContent.Keys)
            {
                foreach (var table in collection.StringTables)
                {
                    var entry = table.GetEntry(key);
                    Assert.IsNotNull(entry, key + " missing for " + table.LocaleIdentifier.Code);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Value), key);
                    Assert.IsFalse(entry.IsSmart, key);
                }
            }

            Assert.AreEqual("Map", Value(collection, "en", MapContent.Title));
            Assert.AreEqual("Harita", Value(collection, "tr", MapContent.Title));
            Assert.AreEqual("Karte", Value(collection, "de", MapContent.Title));
            Assert.AreEqual("Turkey", Value(collection, "en", MapContent.ViewTurkey));
            Assert.AreEqual("Türkiye", Value(collection, "tr", MapContent.ViewTurkey));
            Assert.AreEqual("Türkei", Value(collection, "de", MapContent.ViewTurkey));
            Assert.AreEqual("İzmir", Value(collection, "tr", "cities.izmir"));
            Assert.AreEqual("İstanbul", Value(collection, "tr", "cities.istanbul"));
            Assert.AreEqual("Diyarbakır", Value(collection, "tr", "cities.diyarbakir"));
            Assert.AreEqual("Stadt auswählen", Value(collection, "de", MapContent.SelectCity));
            Assert.AreNotEqual(
                Value(collection, "en", MapContent.SelectCity),
                Value(collection, "tr", MapContent.SelectCity));
        }

        [Test]
        public void Bake_LandMaskSeparatesOceanFromTurkeyAndGreece()
        {
            MapGeographyBaker.EnsureBaked();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MapGeographyBaker.ReliefTexturePath);
            Assert.IsNotNull(texture);
            Assert.AreEqual(MapGeographyBaker.ReliefWidth, texture.width);
            Assert.AreEqual(MapGeographyBaker.ReliefHeight, texture.height);
            Assert.IsTrue(texture.isReadable);

            var ocean = Sample(texture, 30d, -30d);
            var mediterranean = Sample(texture, 34d, 18d);
            var ankara = Sample(texture, 39.929184d, 32.862446d);
            var paris = Sample(texture, 48.85d, 2.35d);
            Assert.AreEqual(4, ocean.r, "Atlantic pixel should stay the baked ocean color.");
            Assert.AreEqual(12, ocean.g);
            Assert.AreEqual(22, ocean.b);
            Assert.AreEqual(255, ocean.a, "Ocean pixels stay opaque. A transparent halo is the old glow artifact.");
            Assert.IsTrue(Same(ocean, mediterranean), "The Mediterranean south of Türkiye should stay ocean.");
            var blackSea = Sample(texture, 42.4d, 35.2d);
            var southCoastWater = Sample(texture, 36.0d, 31.0d);
            var aegean = Sample(texture, 38.5d, 25.5d);
            Assert.IsTrue(Same(ocean, blackSea), "The Black Sea north of the Turkish coast must not carry a glow band.");
            Assert.AreEqual(255, blackSea.a);
            Assert.IsTrue(Same(ocean, southCoastWater), "The Mediterranean immediately south of Türkiye must stay ocean.");
            Assert.IsTrue(Same(ocean, aegean), "The Aegean west of Türkiye must stay ocean.");
            Assert.IsFalse(Same(ocean, ankara), "Ankara should be land, not ocean.");
            Assert.IsFalse(Same(ocean, paris), "Paris should be land, not ocean.");
            Assert.Less(ankara.g, 100, "Türkiye should not be painted as a bright cyan polygon.");
            var pontic = Sample(texture, 40.6d, 39.5d);
            var taurus = Sample(texture, 37.2d, 33.5d);
            Assert.GreaterOrEqual(Mathf.Abs(taurus.g - pontic.g), 12, "Mountain and valley shades in Türkiye should separate.");

            var coast = AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.CoastMeshPath);
            var turkey = AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.TurkeyMeshPath);
            var grid = AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.ReliefMeshPath);
            var land = AssetDatabase.LoadAssetAtPath<Mesh>(MapGeographyBaker.LandFillMeshPath);
            Assert.IsNotNull(coast);
            Assert.IsNotNull(turkey);
            Assert.IsNotNull(grid);
            Assert.IsNotNull(land);
            Assert.Greater(coast.vertexCount, 10000);
            Assert.Greater(turkey.vertexCount, 200);
            Assert.AreEqual((MapGeographyBaker.GridColumns + 1) * (MapGeographyBaker.GridRows + 1), grid.vertexCount);
            Assert.Greater(land.triangles.Length / 3, 50000);
            Assert.AreEqual(0, land.triangles.Length % 3);
            Assert.IsTrue(MeshContains(land, 39.929184d, 32.862446d), "Ankara should sit inside the Türkiye fill.");
            Assert.IsFalse(MeshContains(land, 43.2d, 34.0d), "The Black Sea should stay outside the land fill.");
            Assert.IsFalse(MeshContains(land, 30d, -30d), "The Atlantic should stay outside the land fill.");
            Assert.IsTrue(MeshContains(land, 48.85d, 2.35d), "Paris should sit inside the land fill.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(MapGeographyBaker.ReliefMaterialPath);
            Assert.IsNotNull(material);
            Assert.AreEqual("Universal Render Pipeline/2D/Sprite-Unlit-Default", material.shader.name);
        }

        [Test]
        public void SceneSetup_StartupDoesNotCreateOrReplaceTheScene()
        {
            Assert.AreEqual(
                MapSceneSetup.MapSceneFileAction.None,
                MapSceneSetup.FileActionFor(explicitRequest: false, sceneExists: true, stampMatches: true));
            Assert.AreEqual(
                MapSceneSetup.MapSceneFileAction.None,
                MapSceneSetup.FileActionFor(explicitRequest: false, sceneExists: true, stampMatches: false));
            Assert.AreEqual(
                MapSceneSetup.MapSceneFileAction.None,
                MapSceneSetup.FileActionFor(explicitRequest: false, sceneExists: false, stampMatches: false));
            Assert.AreEqual(
                MapSceneSetup.MapSceneFileAction.None,
                MapSceneSetup.FileActionFor(explicitRequest: true, sceneExists: true, stampMatches: true));
            Assert.AreEqual(
                MapSceneSetup.MapSceneFileAction.CreateMissing,
                MapSceneSetup.FileActionFor(explicitRequest: true, sceneExists: false, stampMatches: false));
            Assert.AreEqual(
                MapSceneSetup.MapSceneFileAction.ReplaceWithBackup,
                MapSceneSetup.FileActionFor(explicitRequest: true, sceneExists: true, stampMatches: false));
        }

        [Test]
        public void Scene_ContainsLandscapeShellCameraAndEightCities()
        {
            MapSceneSetup.EnsureAll(openScene: false);
            var scene = EditorSceneManager.OpenScene(MapSceneSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                MapShellView shell = null;
                MapCameraRig rig = null;
                MapOrientation orientation = null;
                MapSceneStamp stamp = null;
                MapMarkerLayer markers = null;
                var cities = new List<MapCityMarker>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (shell == null)
                        shell = root.GetComponentInChildren<MapShellView>(true);
                    if (rig == null)
                        rig = root.GetComponentInChildren<MapCameraRig>(true);
                    if (orientation == null)
                        orientation = root.GetComponentInChildren<MapOrientation>(true);
                    if (stamp == null)
                        stamp = root.GetComponentInChildren<MapSceneStamp>(true);
                    if (markers == null)
                        markers = root.GetComponentInChildren<MapMarkerLayer>(true);
                    cities.AddRange(root.GetComponentsInChildren<MapCityMarker>(true));
                }

                Assert.IsNotNull(shell);
                Assert.IsNotNull(rig);
                Assert.IsNotNull(orientation);
                Assert.IsNotNull(stamp);
                Assert.AreEqual(MapSceneStamp.Current, stamp.Version);
                Assert.IsNotNull(markers);
                Assert.AreEqual(8, markers.MarkerCount);
                Assert.AreEqual(8, cities.Count);

                var camera = rig.GetComponent<Camera>();
                Assert.IsTrue(camera.orthographic);
                Assert.IsNotNull(rig.GetComponent<MapGestures>());
                var ids = new HashSet<string>();
                for (var i = 0; i < cities.Count; i++)
                {
                    Assert.IsTrue(ids.Add(cities[i].CityId));
                    Assert.AreNotEqual(Vector3.zero, cities[i].transform.position);
                }

                MeshRenderer reliefRenderer = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                    for (var i = 0; i < renderers.Length; i++)
                    {
                        if (renderers[i].gameObject.name == "Relief")
                            reliefRenderer = renderers[i];
                    }
                }

                Assert.IsNotNull(reliefRenderer);
                Assert.IsNotNull(reliefRenderer.sharedMaterial);
                Assert.AreEqual("Universal Render Pipeline/2D/Sprite-Unlit-Default", reliefRenderer.sharedMaterial.shader.name);
                Assert.IsNotNull(reliefRenderer.GetComponent<MeshFilter>().sharedMesh);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void AssertFrameContains(MapScope scope, float aspect)
        {
            Vector2 min;
            Vector2 max;
            float padding;
            if (scope == MapScope.World)
            {
                MapFrames.World(out min, out max);
                padding = MapFrames.WorldFocusPadding;
            }
            else
            {
                MapFrames.Turkey(out min, out max);
                padding = MapFrames.TurkeyFocusPadding;
            }

            MapViewMath.Fit(min, max, aspect, padding, out var center, out var size);
            Assert.Greater(size, 0f);
            Assert.IsTrue(float.IsFinite(size) && float.IsFinite(center.x) && float.IsFinite(center.y));
            var halfWidth = size * aspect;
            Assert.LessOrEqual(max.x, center.x + halfWidth + 0.001f);
            Assert.GreaterOrEqual(min.x, center.x - halfWidth - 0.001f);
            Assert.LessOrEqual(max.y, center.y + size + 0.001f);
            Assert.GreaterOrEqual(min.y, center.y - size - 0.001f);
        }

        static bool MeshContains(Mesh mesh, double latitude, double longitude)
        {
            Assert.IsTrue(MapProjection.TryProject(latitude, longitude, out var point));
            var vertices = mesh.vertices;
            var indices = mesh.GetIndices(0);
            for (var i = 0; i + 2 < indices.Length; i += 3)
            {
                var a = vertices[indices[i]];
                var b = vertices[indices[i + 1]];
                var c = vertices[indices[i + 2]];
                var ab = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
                var bc = (c.x - b.x) * (point.y - b.y) - (c.y - b.y) * (point.x - b.x);
                var ca = (a.x - c.x) * (point.y - c.y) - (a.y - c.y) * (point.x - c.x);
                var negative = ab < -0.00001f || bc < -0.00001f || ca < -0.00001f;
                var positive = ab > 0.00001f || bc > 0.00001f || ca > 0.00001f;
                if (!(negative && positive))
                    return true;
            }

            return false;
        }

        static Color32 Sample(Texture2D texture, double latitude, double longitude)
        {
            var pixels = texture.GetPixels32();
            var x = (int)((longitude + 180d) / 360d * texture.width);
            var y = (int)((latitude + 90d) / 180d * texture.height);
            if (x < 0)
                x = 0;
            if (y < 0)
                y = 0;
            if (x >= texture.width)
                x = texture.width - 1;
            if (y >= texture.height)
                y = texture.height - 1;
            return pixels[y * texture.width + x];
        }

        static bool Same(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b;
        }

        static string Value(UnityEditor.Localization.StringTableCollection collection, string locale, string key)
        {
            foreach (var table in collection.StringTables)
            {
                if (table != null && table.LocaleIdentifier.Code == locale)
                    return table.GetEntry(key).Value;
            }

            return null;
        }
    }
}
