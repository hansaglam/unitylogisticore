using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class MapWorldFrameTests
    {
        const double CapeHornLatitude = -55.983;
        const double CapeHornLongitude = -67.266;
        const double GreenlandLatitude = 83.65;
        const double GreenlandLongitude = -37.0;
        const double PeninsulaLatitude = -63.22;
        const double PeninsulaLongitude = -57.0;

        static readonly int[][] Viewports =
        {
            new[] { 1920, 1080 },
            new[] { 1280, 720 },
            new[] { 960, 540 },
            new[] { 844, 390 },
            new[] { 932, 430 }
        };

        [Test]
        public void FullGeography_StillSpansTheMercatorWorld()
        {
            MapFrames.World(out var min, out var max);
            Assert.IsTrue(MapProjection.TryUnproject(min, out var south, out var west));
            Assert.IsTrue(MapProjection.TryUnproject(max, out var north, out var east));
            Assert.AreEqual(-MapProjection.MaxLatitude, south, 0.001d);
            Assert.AreEqual(MapProjection.MaxLatitude, north, 0.001d);
            Assert.AreEqual(180d, System.Math.Abs(west), 0.001d);
            Assert.AreEqual(180d, System.Math.Abs(east), 0.001d);
            Assert.AreEqual(-58d, MapFrames.WorldPresentationMinLatitude, 0.0001d);
        }

        [Test]
        public void Presentation_CutsTheSouthAndKeepsTheNorth()
        {
            MapFrames.World(out var fullMin, out var fullMax);
            MapFrames.WorldPresentation(out var min, out var max);
            Assert.AreEqual(fullMax.x, max.x, 0.001f);
            Assert.AreEqual(fullMin.x, min.x, 0.001f);
            Assert.AreEqual(fullMax.y, max.y, 0.001f);
            Assert.Greater(min.y, fullMin.y);
            Assert.IsTrue(MapProjection.TryUnproject(min, out var south, out _));
            Assert.AreEqual(MapFrames.WorldPresentationMinLatitude, south, 0.001d);

            Assert.IsTrue(Contains(min, max, Project(CapeHornLatitude, CapeHornLongitude)));
            Assert.IsTrue(Contains(min, max, Project(GreenlandLatitude, GreenlandLongitude)));
            Assert.IsFalse(Contains(min, max, Project(PeninsulaLatitude, PeninsulaLongitude)));
        }

        [Test]
        public void WorldFocus_ExcludesAntarcticaAndKeepsTheSouthernTip()
        {
            for (var i = 0; i < Viewports.Length; i++)
                ExpectWorldViewport(Viewports[i][0], Viewports[i][1]);
        }

        [Test]
        public void ZoomOutAndPan_StayInsideThePresentationFrame()
        {
            var cameraObject = new GameObject("world-frame-camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = 844f / 390f;
                var rig = cameraObject.AddComponent<MapCameraRig>();
                rig.ApplyImmediate(MapScope.Turkey);
                for (var i = 0; i < 24; i++)
                    rig.ZoomOut();

                var aspect = 844f / 390f;
                var maxSize = MapFrames.MaximumOrthographicSize(aspect);
                Assert.AreEqual(maxSize, rig.TargetOrthographicSize, 0.05f);
                AssertView(rig.TargetCenter, rig.TargetOrthographicSize, aspect);

                rig.ApplyImmediate(MapScope.World);
                rig.PanScreenDelta(new Vector2(0f, 1000000f));
                Assert.AreEqual(maxSize, camera.orthographicSize, 0.05f);
                AssertView(new Vector2(camera.transform.position.x, camera.transform.position.y), camera.orthographicSize, aspect);

                rig.Restore();
                Assert.AreEqual(maxSize, rig.TargetOrthographicSize, 0.05f);
                AssertView(rig.TargetCenter, rig.TargetOrthographicSize, aspect);
                Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void TurkeyFrame_AndCities_StayPut()
        {
            const float aspect = 844f / 390f;
            MapFrames.Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.TurkeyFocusPadding, out var center, out var size);
            var cameraObject = new GameObject("turkey-frame-camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = aspect;
                var rig = cameraObject.AddComponent<MapCameraRig>();
                rig.FocusTurkey();
                Assert.AreEqual(center.x, rig.TargetCenter.x, 0.02f);
                Assert.AreEqual(center.y, rig.TargetCenter.y, 0.02f);
                Assert.AreEqual(size, rig.TargetOrthographicSize, 0.02f);
                Assert.AreEqual(size * 0.46f, MapFrames.MinimumOrthographicSize(aspect), 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }

            Assert.AreEqual(8, MapCities.All.Length);
            Assert.AreEqual("ankara", MapCities.All[2].Id);
            Assert.AreEqual(39.929184d, MapCities.All[2].Latitude, 0.0000001d);
            Assert.AreEqual(32.862446d, MapCities.All[2].Longitude, 0.0000001d);
        }

        [Test]
        public void Resize_ReframesThePresentationWithoutShowingThePeninsula()
        {
            var cameraObject = new GameObject("resize-frame-camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = 1920f / 1080f;
                var rig = cameraObject.AddComponent<MapCameraRig>();
                rig.FocusWorld();
                var wide = rig.TargetOrthographicSize;
                camera.aspect = 932f / 430f;
                rig.FocusWorld();
                Assert.AreEqual(wide, rig.TargetOrthographicSize, 0.02f);
                AssertView(rig.TargetCenter, rig.TargetOrthographicSize, camera.aspect);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void WorldPresentation_StillClustersTheEightCities()
        {
            ExpectOneCluster(1920, 1080);
            ExpectOneCluster(844, 390);
            ExpectOneCluster(960, 540);
            var aspect = 16f / 9f;
            var worldZoom = MapFrames.MaximumOrthographicSize(aspect);
            MapFrames.Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.TurkeyFocusPadding, out _, out var turkey);
            Assert.AreEqual(0f, MapRegionalRelief.DetailWeight(worldZoom, turkey), 0.0001f);
            Assert.Greater(worldZoom, turkey * 3.5f);
        }

        static void ExpectWorldViewport(int width, int height)
        {
            var aspect = width / (float)height;
            var cameraObject = new GameObject("world-viewport");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = aspect;
                var rig = cameraObject.AddComponent<MapCameraRig>();
                rig.ApplyImmediate(MapScope.World);
                var maxSize = MapFrames.MaximumOrthographicSize(aspect);
                Assert.AreEqual(maxSize, camera.orthographicSize, 0.02f);
                MapFrames.WorldPresentation(out var min, out var max);
                MapViewMath.Fit(min, max, aspect, MapFrames.WorldFocusPadding, out var center, out var size);
                Assert.AreEqual(center.x, camera.transform.position.x, 0.02f);
                Assert.AreEqual(center.y, camera.transform.position.y, 0.02f);
                Assert.AreEqual(size, camera.orthographicSize, 0.02f);
                AssertView(center, size, aspect);
                MapFrames.World(out var fullMin, out var fullMax);
                MapViewMath.Fit(fullMin, fullMax, aspect, MapFrames.WorldFocusPadding, out _, out var oldSize);
                Assert.Less(size, oldSize);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        static void AssertView(Vector2 center, float size, float aspect)
        {
            Assert.IsTrue(Sees(center, size, aspect, CapeHornLatitude, CapeHornLongitude));
            Assert.IsTrue(Sees(center, size, aspect, GreenlandLatitude, GreenlandLongitude));
            Assert.IsFalse(Sees(center, size, aspect, PeninsulaLatitude, PeninsulaLongitude));
            Assert.IsFalse(Sees(center, size, aspect, -75d, 0d));
            Assert.IsTrue(MapProjection.TryUnproject(new Vector2(center.x, center.y - size), out var bottom, out _));
            Assert.Greater(bottom, -63d);
            Assert.Less(bottom, CapeHornLatitude);
        }

        static void ExpectOneCluster(int width, int height)
        {
            var aspect = width / (float)height;
            MapFrames.WorldPresentation(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, MapFrames.WorldFocusPadding, out var center, out var size);
            var units = MapMarkerLod.UnitsPerPixel(size, height);
            var screen = new Vector2[MapCities.All.Length];
            for (var i = 0; i < MapCities.All.Length; i++)
            {
                var city = MapCities.All[i];
                var world = MapProjection.Project(city.Latitude, city.Longitude);
                screen[i] = MapMarkerLod.ToScreen(world, center, units, width, height);
            }

            var together = new bool[screen.Length * screen.Length];
            var parent = new int[screen.Length];
            MapMarkerLod.Cluster(screen, screen.Length, false, together, parent);
            var roots = 0;
            var seen = new bool[screen.Length];
            for (var i = 0; i < parent.Length; i++)
            {
                if (seen[parent[i]])
                    continue;
                seen[parent[i]] = true;
                roots++;
            }

            Assert.AreEqual(1, roots);
            Assert.AreEqual(8, MapMarkerLod.ClusterSize(parent, parent.Length, 0));
        }

        static bool Sees(Vector2 center, float size, float aspect, double latitude, double longitude)
        {
            var point = Project(latitude, longitude);
            var halfWidth = size * aspect;
            return point.x >= center.x - halfWidth && point.x <= center.x + halfWidth &&
                   point.y >= center.y - size && point.y <= center.y + size;
        }

        static bool Contains(Vector2 min, Vector2 max, Vector2 point)
        {
            return point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y;
        }

        static Vector2 Project(double latitude, double longitude)
        {
            return MapProjection.Project(latitude, longitude);
        }
    }
}
