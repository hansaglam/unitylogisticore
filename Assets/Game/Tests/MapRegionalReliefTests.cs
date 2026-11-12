using System.IO;
using System.Security.Cryptography;
using LogistiCore.EditorTools;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class MapRegionalReliefTests
    {
        const string TexturePath = "Assets/Game/Map/Resources/MapReliefCopernicus.png";
        const string FallbackTexturePath = "Assets/Game/Map/Resources/MapReliefRegion.png";
        const string ShaderPath = "Assets/Game/Map/MapRegionalRelief.shader";
        const string MaterialPath = "Assets/Game/Map/Resources/MapRegionalRelief.mat";
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha256 = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string CopernicusSha256 = "c7dd32b19a119a298fe348320be4dd7f5dbb10f48825c0afe62ba25b9458ff12";
        const string FallbackSha256 = "803d2e945c4afef11b38ef1264896fd42d1c3dde80eedd852be6323188f2faa8";

        [Test]
        public void Bounds_CoverTurkeyWithPaddingOutsideTheFeather()
        {
            Assert.Greater(MapFrames.TurkeyMinLongitude - MapRegionalRelief.MinLongitude, MapRegionalRelief.FeatherDegrees);
            Assert.Greater(MapRegionalRelief.MaxLongitude - MapFrames.TurkeyMaxLongitude, MapRegionalRelief.FeatherDegrees);
            Assert.Greater(MapFrames.TurkeyMinLatitude - MapRegionalRelief.MinLatitude, MapRegionalRelief.FeatherDegrees);
            Assert.Greater(MapRegionalRelief.MaxLatitude - MapFrames.TurkeyMaxLatitude, MapRegionalRelief.FeatherDegrees);
            Assert.IsTrue(MapRegionalRelief.Contains(MapFrames.TurkeyMinLatitude, MapFrames.TurkeyMinLongitude));
            Assert.IsTrue(MapRegionalRelief.Contains(MapFrames.TurkeyMaxLatitude, MapFrames.TurkeyMaxLongitude));
            Assert.AreEqual(25.269d, MapRegionalRelief.MinLongitude, 1e-9);
            Assert.AreEqual(45.217d, MapRegionalRelief.MaxLongitude, 1e-9);
            Assert.AreEqual(35.431d, MapRegionalRelief.MinLatitude, 1e-9);
            Assert.AreEqual(42.493d, MapRegionalRelief.MaxLatitude, 1e-9);
            Assert.AreEqual(2048, MapRegionalRelief.Width);
            Assert.AreEqual(725, MapRegionalRelief.Height);
            Assert.AreEqual(0.22f, MapRegionalRelief.FeatherDegrees, 0.0001f);
            Assert.AreEqual(0.5f, MapRegionalRelief.EdgeFeather(MapRegionalRelief.MaxLatitude - 0.11d, 35d), 0.001f);
            Assert.AreEqual(0f, MapRegionalRelief.EdgeFeather(MapRegionalRelief.MinLatitude, 35d), 0.0001f);
            Assert.AreEqual(1f, MapRegionalRelief.EdgeFeather(39.929184d, 32.862446d), 0.0001f);
        }

        [Test]
        public void Uv_IncreasesNorthAndEastWithoutCrossingTheDateline()
        {
            Assert.IsTrue(MapRegionalRelief.TryUv(39.929184d, 32.862446d, out var ankara));
            Assert.IsTrue(MapRegionalRelief.TryUv(36.891928d, 32.862446d, out var south));
            Assert.IsTrue(MapRegionalRelief.TryUv(39.929184d, 27.149848d, out var west));
            Assert.Greater(ankara.y, south.y);
            Assert.Greater(ankara.x, west.x);
            Assert.IsTrue(MapRegionalRelief.TryUv(MapRegionalRelief.MinLatitude, MapRegionalRelief.MinLongitude, out var southwest));
            Assert.IsTrue(MapRegionalRelief.TryUv(MapRegionalRelief.MaxLatitude, MapRegionalRelief.MaxLongitude, out var northeast));
            Assert.AreEqual(0f, southwest.x, 0.0001f);
            Assert.AreEqual(0f, southwest.y, 0.0001f);
            Assert.AreEqual(1f, northeast.x, 0.0001f);
            Assert.AreEqual(1f, northeast.y, 0.0001f);
            Assert.IsFalse(MapRegionalRelief.TryUv(48.85d, 2.35d, out _));
            Assert.IsFalse(MapRegionalRelief.TryUv(0d, -30d, out _));
            Assert.IsFalse(MapRegionalRelief.TryUv(39d, 18.2d, out _));
        }

        [Test]
        public void Cities_SitInsideTheCopernicusPlate()
        {
            for (var i = 0; i < MapCities.All.Length; i++)
            {
                var city = MapCities.All[i];
                Assert.IsTrue(MapRegionalRelief.Contains(city.Latitude, city.Longitude), city.Id);
                Assert.IsTrue(MapRegionalRelief.TryUv(city.Latitude, city.Longitude, out var uv), city.Id);
                Assert.Greater(uv.x, 0f, city.Id);
                Assert.Less(uv.x, 1f, city.Id);
                Assert.Greater(uv.y, 0f, city.Id);
                Assert.Less(uv.y, 1f, city.Id);
                Assert.AreEqual(1f, MapRegionalRelief.EdgeFeather(city.Latitude, city.Longitude), 0.0001f, city.Id);
            }

            Assert.IsTrue(MapRegionalRelief.TryUv(41.017602d, 28.974277d, out var istanbul));
            Assert.IsTrue(MapRegionalRelief.TryUv(36.891928d, 30.698030d, out var antalya));
            Assert.Greater(istanbul.y, antalya.y);
            Assert.IsTrue(MapRegionalRelief.TryPixelCenter(41.017602d, 28.974277d, out var ix, out var iy));
            Assert.AreEqual(380, ix);
            Assert.AreEqual(151, iy);
        }

        [Test]
        public void Detail_FadesWithZoomAndKeepsTheExistingClamp()
        {
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame);
            const float aspect = 16f / 9f;
            MapFrames.Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.TurkeyFocusPadding, out _, out var frame);
            var closest = MapFrames.MinimumOrthographicSize(aspect);
            Assert.AreEqual(frame * 0.46f, closest, 0.0001f);
            Assert.AreEqual(1f, MapRegionalRelief.DetailWeight(frame, frame), 0.0001f);
            Assert.AreEqual(1f, MapRegionalRelief.DetailWeight(closest, frame), 0.0001f);
            Assert.AreEqual(0f, MapRegionalRelief.DetailWeight(frame * 3.5f, frame), 0.0001f);
            Assert.AreEqual(0f, MapRegionalRelief.DetailWeight(MapFrames.MaximumOrthographicSize(aspect), frame), 0.0001f);

            var previous = MapRegionalRelief.DetailWeight(frame * 3.5f, frame);
            for (var i = 1; i <= 20; i++)
            {
                var size = Mathf.Lerp(frame * 3.5f, frame * 1.15f, i / 20f);
                var weight = MapRegionalRelief.DetailWeight(size, frame);
                Assert.GreaterOrEqual(weight, previous - 0.0001f);
                Assert.Less(weight - previous, 0.2f);
                previous = weight;
            }

            Assert.AreEqual(0f, MapRegionalRelief.BlendWeight(frame * 3.5f, frame, true, false), 0.0001f);
            Assert.AreEqual(0f, MapRegionalRelief.BlendWeight(closest, frame, false, false), 0.0001f);
            Assert.AreEqual(0f, MapRegionalRelief.BlendWeight(closest, frame, true, true), 0.0001f);
            Assert.AreEqual(1f, MapRegionalRelief.BlendWeight(closest, frame, true, false), 0.0001f);
        }

        [Test]
        public void View_OutsideTheRegionDoesNotIntersect()
        {
            MapFrames.ProjectedBounds(10d, 50d, -100d, -40d, out var min, out var max);
            Assert.IsFalse(MapRegionalRelief.IntersectsView(min, max));
            MapRegionalRelief.ProjectedBounds(out var regionMin, out var regionMax);
            Assert.IsTrue(MapRegionalRelief.IntersectsView(regionMin, regionMax));
            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            Assert.IsTrue(MapRegionalRelief.IntersectsView(turkeyMin, turkeyMax));
            MapFrames.World(out var worldMin, out var worldMax);
            Assert.IsTrue(MapRegionalRelief.IntersectsView(worldMin, worldMax));
        }

        [Test]
        public void Texture_MatchesTurkeyOceanAndLandSamples()
        {
            Assert.AreEqual(CopernicusSha256, Sha256(TexturePath));
            Assert.AreEqual(FallbackSha256, Sha256(FallbackTexturePath));

            var bytes = File.ReadAllBytes(TexturePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(texture.LoadImage(bytes));
                Assert.AreEqual(MapRegionalRelief.Width, texture.width);
                Assert.AreEqual(MapRegionalRelief.Height, texture.height);
                var pixels = texture.GetPixels32();

                Assert.IsTrue(MapRegionalRelief.TryPixelCenter(39.929184d, 32.862446d, out var ax, out var ay));
                Assert.IsTrue(MapRegionalRelief.TryPixelCenter(41.017602d, 28.974277d, out var ix, out var iy));
                Assert.IsTrue(MapRegionalRelief.TryPixelCenter(41.8d, 32.0d, out var bx, out var by));
                Assert.IsTrue(MapRegionalRelief.TryPixelCenter(36.0d, 30.5d, out var mx, out var my));
                var ankara = Pixel(pixels, texture.width, texture.height, ax, ay);
                var istanbul = Pixel(pixels, texture.width, texture.height, ix, iy);
                var izmir = SampleCity(pixels, texture.width, texture.height, "izmir");
                var bursa = SampleCity(pixels, texture.width, texture.height, "bursa");
                var antalya = SampleCity(pixels, texture.width, texture.height, "antalya");
                var adana = SampleCity(pixels, texture.width, texture.height, "adana");
                var diyarbakir = SampleCity(pixels, texture.width, texture.height, "diyarbakir");
                var trabzon = SampleCity(pixels, texture.width, texture.height, "trabzon");
                var blackSea = Pixel(pixels, texture.width, texture.height, bx, by);
                var mediterranean = Pixel(pixels, texture.width, texture.height, mx, my);
                var northWest = Pixel(pixels, texture.width, texture.height, 0, 0);
                var southEast = Pixel(pixels, texture.width, texture.height, texture.width - 1, texture.height - 1);

                Assert.AreEqual(46, ankara.r);
                Assert.AreEqual(74, ankara.g);
                Assert.AreEqual(95, ankara.b);
                Assert.AreEqual(255, ankara.a);
                Assert.AreEqual(81, istanbul.a);
                Assert.AreEqual(255, izmir.a);
                Assert.AreEqual(255, bursa.a);
                Assert.AreEqual(255, antalya.a);
                Assert.AreEqual(255, adana.a);
                Assert.AreEqual(255, diyarbakir.a);
                Assert.AreEqual(255, trabzon.a);
                Assert.AreEqual(0, blackSea.a);
                Assert.AreEqual(0, mediterranean.a);
                Assert.AreEqual(0, northWest.a);
                Assert.AreEqual(0, southEast.a);
                Assert.AreEqual(0, pixels[0].a);
                Assert.AreEqual(0, pixels[texture.width - 1].a);
                Assert.Greater(diyarbakir.g, izmir.g);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Importer_DisablesCpuReadbackAndKeepsTheShaderBound()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            Assert.IsNotNull(importer);
            Assert.IsFalse(importer.isReadable);
            Assert.IsFalse(importer.alphaIsTransparency);
            Assert.IsTrue(importer.mipmapEnabled);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Assert.IsNotNull(imported);
            Assert.AreEqual(2048, imported.width);
            Assert.AreEqual(725, imported.height);
            Assert.IsFalse(imported.isReadable);
            var ios = importer.GetPlatformTextureSettings("iPhone");
            var android = importer.GetPlatformTextureSettings("Android");
            Assert.IsTrue(ios.overridden);
            Assert.IsTrue(android.overridden);
            Assert.AreEqual(TextureImporterFormat.ASTC_6x6, ios.format);
            Assert.AreEqual(TextureImporterFormat.ASTC_6x6, android.format);
            Assert.AreEqual(2048, ios.maxTextureSize);
            Assert.AreEqual(2048, android.maxTextureSize);

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader);
            Assert.AreEqual("LogistiCore/Map/RegionalRelief", shader.name);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader));

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material);
            Assert.AreEqual(shader, material.shader);
            Assert.AreEqual(MapGeographyBaker.ReliefTexturePath, AssetDatabase.GetAssetPath(material.GetTexture("_MainTex")));
            Assert.AreEqual(TexturePath, AssetDatabase.GetAssetPath(material.GetTexture("_RegionalTex")));
            Assert.AreEqual(FallbackTexturePath, AssetDatabase.GetAssetPath(material.GetTexture("_HaloTex")));
            Assert.AreNotEqual(FallbackTexturePath, AssetDatabase.GetAssetPath(material.GetTexture("_RegionalTex")));
            Assert.Greater(material.GetFloat("_Detail"), 0.5f);
            var regionMin = material.GetVector("_RegionMin");
            var regionMax = material.GetVector("_RegionMax");
            Assert.AreEqual((float)MapRegionalRelief.MinLongitude, regionMin.x, 0.0001f);
            Assert.AreEqual((float)MapRegionalRelief.MinLatitude, regionMin.y, 0.0001f);
            Assert.AreEqual((float)MapRegionalRelief.MaxLongitude, regionMax.x, 0.0001f);
            Assert.AreEqual((float)MapRegionalRelief.MaxLatitude, regionMax.y, 0.0001f);
            Assert.AreEqual((float)MapRegionalRelief.HaloMinLongitude, material.GetVector("_HaloMin").x, 0.0001f);
            Assert.AreEqual((float)MapRegionalRelief.HaloMaxLatitude, material.GetVector("_HaloMax").y, 0.0001f);
        }

        [Test]
        public void Halo_CoversEasternNeighborsTheCopernicusPlateLeavesToGlobal()
        {
            Assert.IsFalse(MapRegionalRelief.Contains(39.0d, 46.0d));
            Assert.IsTrue(MapRegionalRelief.HaloContains(39.0d, 46.0d));
            Assert.IsTrue(MapRegionalRelief.HaloContains(38.5d, 44.9d));
            Assert.IsTrue(MapRegionalRelief.Contains(39.929184d, 32.862446d));
            Assert.Greater(MapRegionalRelief.HaloMaxLongitude, MapRegionalRelief.MaxLongitude);
            Assert.Less(MapRegionalRelief.HaloMinLongitude, MapRegionalRelief.MinLongitude);
        }

        [Test]
        public void Scene_KeepsGlobalReliefMaterialAndCityMarkers()
        {
            Assert.AreEqual(SceneSha256, Sha256(ScenePath));
            Assert.AreEqual(MapSceneSetup.ScenePath, MapRegionalReliefPresenter.MapScenePath);
            Assert.IsFalse(MapRegionalReliefPresenter.Accepts(MapSceneSetup.ScenePath, 0, true, true, true));
            Assert.IsFalse(MapRegionalReliefPresenter.Accepts("Assets/Game/Scenes/FoundationVerification.unity", MapSceneStamp.Current, true, true, true));
            Assert.IsNull(MapRegionalReliefPresenter.InstantiateTemplate(null));

            var sceneText = File.ReadAllText(ScenePath);
            StringAssert.Contains("guid: 743f2fffa97ca4ec8ada7c14dca9922f", sceneText);
            StringAssert.DoesNotContain("guid: b1c2d3e4f5a64789a0b1c2d3e4f50617", sceneText);
            StringAssert.DoesNotContain("guid: f0b2d5c39e1a4c7b8d6e0f2a4b6c8d91", sceneText);

            var scene = OpenMapScene();
            try
            {
                var markers = new System.Collections.Generic.List<MapCityMarker>();
                MeshRenderer relief = null;
                MapSceneStamp stamp = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (stamp == null)
                        stamp = root.GetComponentInChildren<MapSceneStamp>(true);
                    markers.AddRange(root.GetComponentsInChildren<MapCityMarker>(true));
                    var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                    for (var i = 0; i < renderers.Length; i++)
                    {
                        if (renderers[i].gameObject.name == "Relief")
                            relief = renderers[i];
                    }
                }

                Assert.IsNotNull(stamp);
                Assert.AreEqual(MapSceneStamp.Current, stamp.Version);
                Assert.AreEqual(8, markers.Count);
                Assert.AreEqual(MapCities.All.Length, markers.Count);
                for (var i = 0; i < markers.Count; i++)
                {
                    Assert.IsTrue(MapCities.TryGet(markers[i].CityId, out var city));
                    var expected = MapProjection.Project(city.Latitude, city.Longitude);
                    Assert.AreEqual(expected.x, markers[i].transform.position.x, 0.02f);
                    Assert.AreEqual(expected.y, markers[i].transform.position.y, 0.02f);
                }

                Assert.IsNotNull(relief);
                Assert.AreEqual("Universal Render Pipeline/2D/Sprite-Unlit-Default", relief.sharedMaterial.shader.name);
                Assert.IsNotNull(relief.GetComponent<MeshFilter>().sharedMesh);
            }
            finally
            {
                CloseIfLoaded(scene);
            }

            Assert.AreEqual(SceneSha256, Sha256(ScenePath));
        }

        [Test]
        public void Attach_UsesOnePresenterAndRestoresTheGlobalMaterial()
        {
            var scene = OpenMapScene();
            try
            {
                Assert.IsNull(MapRegionalReliefPresenter.Attach(scene, null));
                MeshRenderer relief = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                    for (var i = 0; i < renderers.Length; i++)
                    {
                        if (renderers[i].gameObject.name == "Relief")
                            relief = renderers[i];
                    }
                }

                Assert.IsNotNull(relief);
                var original = relief.sharedMaterial;
                Assert.AreEqual("Universal Render Pipeline/2D/Sprite-Unlit-Default", original.shader.name);

                var presenter = MapRegionalReliefPresenter.Attach(scene);
                var again = MapRegionalReliefPresenter.Attach(scene);
                Assert.IsNotNull(presenter);
                Assert.AreSame(presenter, again);
                var camera = presenter.GetComponent<Camera>();
                Assert.AreEqual(1, camera.GetComponents<MapRegionalReliefPresenter>().Length);
                Assert.AreEqual("LogistiCore/Map/RegionalRelief", relief.sharedMaterial.shader.name);
                Assert.AreNotEqual(original, relief.sharedMaterial);
                Assert.AreEqual(original, presenter.OriginalMaterial);
                Assert.AreEqual(TexturePath, AssetDatabase.GetAssetPath(presenter.RuntimeMaterial.GetTexture("_RegionalTex")));
                Assert.AreEqual(FallbackTexturePath, AssetDatabase.GetAssetPath(presenter.RuntimeMaterial.GetTexture("_HaloTex")));
                Assert.AreEqual((float)MapRegionalRelief.MinLongitude, presenter.RuntimeMaterial.GetVector("_RegionMin").x, 0.0001f);
                Assert.AreEqual((float)MapRegionalRelief.MaxLatitude, presenter.RuntimeMaterial.GetVector("_RegionMax").y, 0.0001f);
                Assert.AreEqual((float)MapRegionalRelief.HaloMinLongitude, presenter.RuntimeMaterial.GetVector("_HaloMin").x, 0.0001f);
                Assert.AreEqual("Universal Render Pipeline/2D/Sprite-Unlit-Default", original.shader.name);

                const float aspect = 16f / 9f;
                MapFrames.World(out var worldMin, out var worldMax);
                MapViewMath.Fit(worldMin, worldMax, aspect, MapFrames.WorldFocusPadding, out var worldCenter, out var worldSize);
                camera.orthographicSize = worldSize;
                camera.transform.position = new Vector3(worldCenter.x, worldCenter.y, MapCameraRig.CameraZ);
                Assert.AreEqual(0f, presenter.Evaluate(), 0.001f);

                MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
                MapViewMath.Fit(turkeyMin, turkeyMax, aspect, MapFrames.TurkeyFocusPadding, out var turkeyCenter, out var turkeySize);
                camera.orthographicSize = turkeySize * MapFrames.ClosestRelativeToTurkeyFrame;
                camera.transform.position = new Vector3(turkeyCenter.x, turkeyCenter.y, MapCameraRig.CameraZ);
                Assert.AreEqual(1f, presenter.Evaluate(), 0.001f);

                MapFrames.ProjectedBounds(10d, 50d, -100d, -40d, out var awayMin, out var awayMax);
                MapViewMath.Fit(awayMin, awayMax, aspect, 1f, out var awayCenter, out var awaySize);
                camera.orthographicSize = awaySize;
                camera.transform.position = new Vector3(awayCenter.x, awayCenter.y, MapCameraRig.CameraZ);
                Assert.AreEqual(0f, presenter.Evaluate(), 0.001f);

                camera.orthographicSize = turkeySize;
                camera.transform.position = new Vector3(turkeyCenter.x, turkeyCenter.y, MapCameraRig.CameraZ);
                presenter.GlobalOnly = true;
                Assert.AreEqual(0f, presenter.Evaluate(), 0.001f);
                Assert.AreEqual(0f, presenter.RuntimeMaterial.GetFloat("_Detail"), 0.001f);
                presenter.GlobalOnly = false;
                Assert.AreEqual(1f, presenter.RuntimeMaterial.GetFloat("_Detail"), 0.001f);

                presenter.Release();
                Assert.AreEqual(original, relief.sharedMaterial);
                Object.DestroyImmediate(presenter);
                Assert.AreEqual(original, relief.sharedMaterial);
                Assert.AreEqual(0, camera.GetComponents<MapRegionalReliefPresenter>().Length);
            }
            finally
            {
                CloseIfLoaded(scene);
            }

            Assert.AreEqual(SceneSha256, Sha256(ScenePath));
            var saved = AssetDatabase.LoadAssetAtPath<Material>(MapGeographyBaker.ReliefMaterialPath);
            Assert.AreEqual("Universal Render Pipeline/2D/Sprite-Unlit-Default", saved.shader.name);
        }

        [Test]
        public void MissingMaterial_LeavesARendererOnItsOriginalMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            var original = new Material(shader);
            var host = new GameObject("regional-relief-fallback");
            var camera = host.AddComponent<Camera>();
            var renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = original;
            var presenter = host.AddComponent<MapRegionalReliefPresenter>();
            try
            {
                Assert.IsFalse(presenter.TryUse(renderer, camera, null));
                Assert.AreEqual(original, renderer.sharedMaterial);
                var runtime = new Material(shader);
                Assert.IsTrue(presenter.TryUse(renderer, camera, runtime));
                Assert.AreEqual(runtime, renderer.sharedMaterial);
                presenter.Release();
                Assert.AreEqual(original, renderer.sharedMaterial);
                Assert.IsTrue(runtime == null);
                Assert.IsNotNull(original);
            }
            finally
            {
                if (host != null)
                    Object.DestroyImmediate(host);
                if (original != null)
                    Object.DestroyImmediate(original);
            }
        }

        [Test]
        public void OtherScenes_DoNotReceiveThePresenter()
        {
            var scene = EditorSceneManager.OpenScene(FoundationSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                Assert.IsNull(MapRegionalReliefPresenter.Attach(scene));
                var presenters = 0;
                foreach (var root in scene.GetRootGameObjects())
                    presenters += root.GetComponentsInChildren<MapRegionalReliefPresenter>(true).Length;
                Assert.AreEqual(0, presenters);
            }
            finally
            {
                CloseIfLoaded(scene);
            }
        }

        static UnityEngine.SceneManagement.Scene OpenMapScene()
        {
            var loaded = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                if (UnityEngine.SceneManagement.SceneManager.sceneCount == 1)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.CloseScene(loaded, true);
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        static void CloseIfLoaded(UnityEngine.SceneManagement.Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded && UnityEngine.SceneManagement.SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
        }

        static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return System.BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }

        static Color32 SampleCity(Color32[] pixels, int width, int height, string id)
        {
            Assert.IsTrue(MapCities.TryGet(id, out var city));
            Assert.IsTrue(MapRegionalRelief.TryPixelCenter(city.Latitude, city.Longitude, out var x, out var yFromNorth));
            return Pixel(pixels, width, height, x, yFromNorth);
        }

        static Color32 Pixel(Color32[] pixels, int width, int height, int x, int yFromNorth)
        {
            var y = height - 1 - yFromNorth;
            return pixels[y * width + x];
        }
    }
}
