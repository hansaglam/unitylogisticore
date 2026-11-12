using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class MapRegionFrameTests
    {
        static readonly MapScope[] Regions =
        {
            MapScope.Europe,
            MapScope.Asia,
            MapScope.Africa,
            MapScope.NorthAmerica,
            MapScope.SouthAmerica,
            MapScope.Oceania
        };

        [Test]
        public void SixRegions_ProjectInsideTheirGeographicFrames()
        {
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame, 0.000001f);
            Assert.AreEqual(8, MapContent.RegionViews.Length);
            Assert.IsFalse(MapFrames.TryGeographicBounds(MapScope.World, out _, out _, out _, out _));
            Assert.IsFalse(MapFrames.TryProjected(MapScope.Turkey, out _, out _));

            ExpectInside(MapScope.Europe, 48.8566, 2.3522);
            ExpectOutside(MapScope.Europe, 30.0444, 31.2357);
            ExpectInside(MapScope.Asia, 35.6762, 139.6503);
            ExpectInside(MapScope.Asia, 39.9334, 32.8597);
            ExpectOutside(MapScope.Asia, 64.5, -175.0);
            ExpectInside(MapScope.Africa, 30.0444, 31.2357);
            ExpectInside(MapScope.Africa, -33.9249, 18.4241);
            ExpectInside(MapScope.NorthAmerica, 40.7128, -74.0060);
            ExpectInside(MapScope.NorthAmerica, 64.1835, -51.7216);
            ExpectInside(MapScope.SouthAmerica, -23.5505, -46.6333);
            ExpectOutside(MapScope.SouthAmerica, -0.7, -90.4);
            ExpectInside(MapScope.Oceania, -33.8688, 151.2093);
            ExpectInside(MapScope.Oceania, -18.1416, 178.4419);
            ExpectOutside(MapScope.Oceania, -14.2756, -170.7020);

            MapFrames.Turkey(out var turkeyMin, out var turkeyMax);
            var ankara = MapProjection.Project(39.9334, 32.8597);
            Assert.GreaterOrEqual(ankara.x, turkeyMin.x);
            Assert.LessOrEqual(ankara.x, turkeyMax.x);
            Assert.GreaterOrEqual(ankara.y, turkeyMin.y);
            Assert.LessOrEqual(ankara.y, turkeyMax.y);
        }

        [Test]
        public void Projection_MatchesWebMercatorAndFullScreenFit()
        {
            Assert.IsTrue(MapProjection.TryProjectUnwrapped(MapFrames.EuropeMinLatitude, MapFrames.EuropeMinLongitude, out var southwest));
            Assert.IsTrue(MapFrames.TryProjected(MapScope.Europe, out var min, out var max));
            Assert.AreEqual(southwest.x, min.x, 0.001f);
            Assert.AreEqual(southwest.y, min.y, 0.001f);
            Assert.Less(min.x, 0f);
            Assert.Greater(max.x, 0f);
            Assert.AreEqual(180d, MapFrames.AsiaMaxLongitude, 0.0001d);
            Assert.AreEqual(180d, MapFrames.OceaniaMaxLongitude, 0.0001d);
            Assert.AreEqual(MapFrames.TurkeyMinLongitude, MapFrames.AsiaMinLongitude, 0.0001d);
            Assert.Greater(MapFrames.NorthAmericaMaxLatitude, 80d);
            Assert.Less(MapFrames.EuropeMaxLongitude, 61d);

            var boundsMin = new Vector2(-40f, 10f);
            var boundsMax = new Vector2(80f, 70f);
            MapViewMath.Fit(boundsMin, boundsMax, 16f / 9f, 1.06f, out var fittedCenter, out var fittedSize);
            MapViewMath.FitInViewport(
                boundsMin,
                boundsMax,
                16f / 9f,
                1.06f,
                new Rect(0f, 0f, 1920f, 1080f),
                1920f,
                1080f,
                out var center,
                out var size);
            Assert.AreEqual(fittedCenter.x, center.x, 0.001f);
            Assert.AreEqual(fittedCenter.y, center.y, 0.001f);
            Assert.AreEqual(fittedSize, size, 0.001f);
        }

        [Test]
        public void ClampAwareFit_TightensCroppedRegionsWithoutZoomingOutOrMovingFullFrames()
        {
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame, 0.000001f);
            var phone = new Rect(186f, 60f, 2496f, 1200f);
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);

            AssertUnchanged(MapScope.Europe, 1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f), worldMin, worldMax);
            AssertUnchanged(MapScope.Europe, 2868f, 1320f, phone, worldMin, worldMax);
            AssertUnchanged(MapScope.Africa, 1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f), worldMin, worldMax);
            AssertUnchanged(MapScope.Africa, 2868f, 1320f, phone, worldMin, worldMax);

            // Asia uses a Contain focus window; full-coverage Asia would spill into
            // the Atlantic on the phone aspect. Focus coverage is checked separately.
            Assert.AreEqual(MapFrames.RegionFrameMode.Contain, FrameMode(MapScope.Asia));
            Assert.AreEqual(0.46f, MapFrames.ClosestRelativeToTurkeyFrame, 0.000001f);
        }

        [Test]
        public void FocusFrames_KeepMainlandCoastlinesInTheMapHole()
        {
            var phone = new Rect(186f, 60f, 2496f, 1200f);
            var full = new Rect(0f, 0f, 1920f, 1080f);
            AssertCoast(MapScope.Oceania, 2868f, 1320f, phone);
            AssertCoast(MapScope.Oceania, 1920f, 1080f, full);
            AssertCoast(MapScope.Asia, 2868f, 1320f, phone);
            AssertCoast(MapScope.Asia, 1920f, 1080f, full);
            AssertCoast(MapScope.NorthAmerica, 2868f, 1320f, phone);
            AssertCoast(MapScope.NorthAmerica, 1920f, 1080f, full);
            AssertCoast(MapScope.SouthAmerica, 2868f, 1320f, phone);
            AssertCoast(MapScope.SouthAmerica, 1920f, 1080f, full);
            AssertOutsideHole(MapScope.Oceania, 2868f, 1320f, phone, -20d, 30d);
            AssertOutsideHole(MapScope.Asia, 2868f, 1320f, phone, 64.5d, -51.7d);
            AssertOutsideHole(MapScope.Asia, 2868f, 1320f, phone, 56d, -106d);
            AssertOutsideHole(MapScope.Asia, 2868f, 1320f, phone, 40.7d, -74d);
            AssertOutsideHole(MapScope.Asia, 2868f, 1320f, phone, 0d, 20d);
            AssertOutsideHole(MapScope.Asia, 2868f, 1320f, phone, 48.8d, 2.3d);

            var europe = ScreenPoint(MapScope.NorthAmerica, 2868f, 1320f, phone, 50d, 10d);
            var unitedStates = ScreenPoint(MapScope.NorthAmerica, 2868f, 1320f, phone, 44.827686d, -66.987012d);
            Assert.Greater(europe.x, unitedStates.x, europe.ToString());
            var hole = ClosedHole(2868f, 1320f, phone);
            var brazil = ScreenPoint(MapScope.SouthAmerica, 2868f, 1320f, phone, -14d, -55d);
            Assert.Less(Mathf.Abs(brazil.x - hole.center.x), hole.width * 0.2f);
        }

        [Test]
        public void ChromeOverscroll_KeepsTheUsefulHoleOnTheMesh()
        {
            var phone = new Rect(186f, 60f, 2496f, 1200f);
            var full = new Rect(0f, 0f, 1920f, 1080f);
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            AssertHoleOnMesh(MapScope.Oceania, 2868f, 1320f, phone, worldMin, worldMax);
            AssertHoleOnMesh(MapScope.Oceania, 1920f, 1080f, full, worldMin, worldMax);
            AssertHoleOnMesh(MapScope.NorthAmerica, 2868f, 1320f, phone, worldMin, worldMax);
            AssertHoleOnMesh(MapScope.NorthAmerica, 1920f, 1080f, full, worldMin, worldMax);
            AssertHoleOnMesh(MapScope.SouthAmerica, 2868f, 1320f, phone, worldMin, worldMax);
            AssertHoleOnMesh(MapScope.SouthAmerica, 1920f, 1080f, full, worldMin, worldMax);
            CoverCoast(844f, 390f, new Rect(0f, 0f, 844f, 390f));
            CoverCoast(932f, 430f, new Rect(32f, 16f, 868f, 398f));
            CoverCoast(960f, 540f, new Rect(0f, 0f, 960f, 540f));
            CoverCoast(1280f, 720f, new Rect(0f, 0f, 1280f, 720f));
            AssertHoleOnMesh(MapScope.Asia, 2868f, 1320f, phone, worldMin, worldMax);
            AssertHoleOnMesh(MapScope.Asia, 1920f, 1080f, full, worldMin, worldMax);
        }

        static void AssertCoast(MapScope scope, float width, float height, Rect safe)
        {
            if (scope == MapScope.Oceania)
            {
                AssertInHole(scope, width, height, safe, -10.051758d, 142.148828d);
                AssertInHole(scope, width, height, safe, -25.569824d, 112.908203d);
                AssertInHole(scope, width, height, safe, -28.673047d, 153.616895d);
                AssertInHole(scope, width, height, safe, -43.619336d, 146.834277d);
                AssertInHole(scope, width, height, safe, -37.69209d, 178.53623d);
                AssertInHole(scope, width, height, safe, -46.797656d, 167.765234d);
            }
            else if (scope == MapScope.Asia)
            {
                AssertInHole(scope, width, height, safe, 39.9334d, 32.8597d);
                AssertInHole(scope, width, height, safe, 28.6139d, 77.209d);
                AssertInHole(scope, width, height, safe, 39.9042d, 116.4074d);
                AssertInHole(scope, width, height, safe, 35.6762d, 139.6503d);
                AssertInHole(scope, width, height, safe, -6.2088d, 106.8456d);
                AssertInHole(scope, width, height, safe, 13.7563d, 100.5018d);
            }
            else if (scope == MapScope.NorthAmerica)
            {
                AssertInHole(scope, width, height, safe, 65.567773d, -167.987256d);
                AssertInHole(scope, width, height, safe, 71.407666d, -156.470215d);
                AssertInHole(scope, width, height, safe, 44.827686d, -66.987012d);
                AssertInHole(scope, width, height, safe, 14.54541d, -92.235156d);
                AssertInHole(scope, width, height, safe, 39.5d, -98.5d);
            }
            else
            {
                AssertInHole(scope, width, height, safe, 12.434375d, -71.597461d);
                AssertInHole(scope, width, height, safe, -14d, -55d);
                AssertInHole(scope, width, height, safe, -54.8d, -68.3d);
                AssertInHole(scope, width, height, safe, -55.891699d, -67.611426d);
            }
        }

        static MapFrames.RegionFrameMode FrameMode(MapScope scope)
        {
            Assert.IsTrue(MapFrames.TryFrame(scope, out _, out _, out var mode));
            return mode;
        }

        static void CoverCoast(float width, float height, Rect safe)
        {
            AssertCoast(MapScope.Oceania, width, height, safe);
            AssertCoast(MapScope.Asia, width, height, safe);
            AssertCoast(MapScope.NorthAmerica, width, height, safe);
            AssertCoast(MapScope.SouthAmerica, width, height, safe);
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            AssertHoleOnMesh(MapScope.Oceania, width, height, safe, worldMin, worldMax);
            AssertHoleOnMesh(MapScope.Asia, width, height, safe, worldMin, worldMax);
            AssertHoleOnMesh(MapScope.NorthAmerica, width, height, safe, worldMin, worldMax);
            AssertHoleOnMesh(MapScope.SouthAmerica, width, height, safe, worldMin, worldMax);
        }

        static void AssertHoleOnMesh(MapScope scope, float width, float height, Rect safe, Vector2 worldMin, Vector2 worldMax)
        {
            MapCameraRig.FrameScope(scope, width / height, width, height, safe, worldMin, worldMax, out var center, out var size);
            var hole = ClosedHole(width, height, safe);
            var west = MapViewMath.ScreenToWorld(new Vector2(hole.xMin, hole.center.y), center, size, width / height, width, height).x;
            var east = MapViewMath.ScreenToWorld(new Vector2(hole.xMax, hole.center.y), center, size, width / height, width, height).x;
            var south = MapViewMath.ScreenToWorld(new Vector2(hole.center.x, hole.yMin), center, size, width / height, width, height).y;
            var north = MapViewMath.ScreenToWorld(new Vector2(hole.center.x, hole.yMax), center, size, width / height, width, height).y;
            Assert.GreaterOrEqual(west, worldMin.x - 0.05f, scope + " west " + width);
            Assert.LessOrEqual(east, worldMax.x + 0.05f, scope + " east " + width);
            Assert.GreaterOrEqual(south, worldMin.y - 0.05f, scope + " south " + width);
            Assert.LessOrEqual(north, worldMax.y + 0.05f, scope + " north " + width);
        }

        static void AssertInHole(MapScope scope, float width, float height, Rect safe, double latitude, double longitude)
        {
            var point = ScreenPoint(scope, width, height, safe, latitude, longitude);
            var hole = ClosedHole(width, height, safe);
            Assert.GreaterOrEqual(point.x, hole.xMin + 8f, scope + " " + latitude + "," + longitude);
            Assert.LessOrEqual(point.x, hole.xMax - 8f, scope + " " + latitude + "," + longitude);
            Assert.GreaterOrEqual(point.y, hole.yMin + 8f, scope + " " + latitude + "," + longitude);
            Assert.LessOrEqual(point.y, hole.yMax - 8f, scope + " " + latitude + "," + longitude);
        }

        static void AssertOutsideHole(MapScope scope, float width, float height, Rect safe, double latitude, double longitude)
        {
            var point = ScreenPoint(scope, width, height, safe, latitude, longitude);
            var hole = ClosedHole(width, height, safe);
            var inside = point.x >= hole.xMin && point.x <= hole.xMax && point.y >= hole.yMin && point.y <= hole.yMax;
            Assert.IsFalse(inside, scope + " " + latitude + "," + longitude + " " + point);
        }

        static Rect ClosedHole(float width, float height, Rect safe)
        {
            return MapCommandLayout.Evaluate(width, height, safe, false).Viewport;
        }

        static Vector2 ScreenPoint(MapScope scope, float width, float height, Rect safe, double latitude, double longitude)
        {
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            MapCameraRig.FrameScope(scope, width / height, width, height, safe, worldMin, worldMax, out var center, out var size);
            var world = MapProjection.Project(latitude, longitude);
            var aspect = width / height;
            var nx = 0.5f + (world.x - center.x) / (2f * size * aspect);
            var ny = 0.5f + (world.y - center.y) / (2f * size);
            return new Vector2(nx * width, ny * height);
        }

        struct FrameSample
        {
            public Vector2 Center;
            public float Size;
            public Vector2 Coverage;
            public bool OpenPanel;

            public override string ToString()
            {
                return Center + " size " + Size + " cover " + Coverage + " open " + OpenPanel;
            }
        }

        static void AssertUnchanged(MapScope scope, float width, float height, Rect safe, Vector2 worldMin, Vector2 worldMax)
        {
            Assert.IsTrue(MapFrames.TryProjected(scope, out var min, out var max));
            var aspect = width / height;
            var openLayout = MapCommandLayout.Evaluate(width, height, safe, true);
            var closedLayout = MapCommandLayout.Evaluate(width, height, safe, false);
            var openViewport = openLayout.ContextOpen ? openLayout.Viewport : closedLayout.Viewport;
            MapViewMath.FitInViewport(min, max, aspect, MapFrames.RegionFocusPadding, openViewport, width, height, out var openCenter, out var openSize);
            openSize = MapViewMath.ClampSize(openSize, MapFrames.MinimumOrthographicSize(aspect), MapFrames.MaximumOrthographicSize(aspect));
            openCenter = MapViewMath.ClampCenter(openCenter, openSize, aspect, worldMin, worldMax);
            MapCameraRig.RegionFrame(min, max, aspect, width, height, safe, worldMin, worldMax, out var center, out var size);
            Assert.AreEqual(openCenter.x, center.x, 0.02f, scope.ToString());
            Assert.AreEqual(openCenter.y, center.y, 0.02f, scope.ToString());
            Assert.AreEqual(openSize, size, 0.02f, scope.ToString());
            var cover = MapViewMath.ViewportSpan(min, max, center, size, aspect, openViewport, width, height);
            Assert.GreaterOrEqual(cover.x, 0.995f, scope.ToString());
            Assert.GreaterOrEqual(cover.y, 0.995f, scope.ToString());
        }

        static FrameSample CoverageOf(MapScope scope, float width, float height, Rect safe, Vector2 worldMin, Vector2 worldMax)
        {
            Assert.IsTrue(MapFrames.TryProjected(scope, out var min, out var max));
            var aspect = width / height;
            MapCameraRig.FrameScope(scope, aspect, width, height, safe, worldMin, worldMax, out var center, out var size);
            var openLayout = MapCommandLayout.Evaluate(width, height, safe, true);
            var closedLayout = MapCommandLayout.Evaluate(width, height, safe, false);
            var openViewport = openLayout.ContextOpen ? openLayout.Viewport : closedLayout.Viewport;
            var openCover = MapViewMath.ViewportSpan(min, max, center, size, aspect, openViewport, width, height);
            var closedCover = MapViewMath.ViewportSpan(min, max, center, size, aspect, closedLayout.Viewport, width, height);
            var openScore = Mathf.Min(openCover.x, openCover.y);
            var closedScore = Mathf.Min(closedCover.x, closedCover.y);
            var useOpen = openScore >= closedScore;
            return new FrameSample
            {
                Center = center,
                Size = size,
                Coverage = useOpen ? openCover : closedCover,
                OpenPanel = useOpen
            };
        }

        [Test]
        public void ViewportFit_KeepsEachRegionInsideTheOpenMapAtBothAspects()
        {
            Cover(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            Cover(2868f, 1320f, new Rect(186f, 60f, 2496f, 1200f));
        }

        static void Cover(float width, float height, Rect safe)
        {
            var aspect = width / height;
            var ceiling = MapFrames.MaximumOrthographicSize(aspect);
            var floor = MapFrames.MinimumOrthographicSize(aspect);
            Assert.AreEqual(floor, MapFrames.MinimumOrthographicSize(aspect), 0.0001f);
            MapFrames.WorldPresentation(out var worldMin, out var worldMax);
            var reserved = MapCommandLayout.Evaluate(width, height, safe, true);
            var metrics = reserved.ContextOpen
                ? reserved
                : MapCommandLayout.Evaluate(width, height, safe, false);
            Assert.Greater(metrics.Viewport.width, 400f, width + "x" + height);
            Assert.Greater(metrics.Viewport.height, 200f, width + "x" + height);
            Assert.GreaterOrEqual(metrics.Viewport.xMin, safe.xMin - 0.5f);
            Assert.LessOrEqual(metrics.Viewport.xMax, safe.xMax + 0.5f);

            for (var i = 0; i < Regions.Length; i++)
            {
                var scope = Regions[i];
                MapCameraRig.FrameScope(
                    scope,
                    aspect,
                    width,
                    height,
                    safe,
                    worldMin,
                    worldMax,
                    out var center,
                    out var size);
                Assert.IsTrue(MapFrames.TryProjected(scope, out var min, out var max), scope.ToString());
                Assert.GreaterOrEqual(size, floor, scope.ToString());
                Assert.LessOrEqual(size, ceiling + 0.001f, scope.ToString());
                var openLayout = MapCommandLayout.Evaluate(width, height, safe, true);
                var closedLayout = MapCommandLayout.Evaluate(width, height, safe, false);
                var openViewport = openLayout.ContextOpen ? openLayout.Viewport : closedLayout.Viewport;
                var openCover = MapCameraRig.ViewportCoverage(min, max, center, size, aspect, openViewport, width, height);
                var closedCover = MapCameraRig.ViewportCoverage(min, max, center, size, aspect, closedLayout.Viewport, width, height);
                var visible = Mathf.Min(openCover.x, openCover.y) >= Mathf.Min(closedCover.x, closedCover.y) ? openCover : closedCover;
                if (scope == MapScope.NorthAmerica || scope == MapScope.SouthAmerica || scope == MapScope.Oceania || scope == MapScope.Asia)
                    continue;

                var required = 0.98f;
                Assert.GreaterOrEqual(visible.x, required, scope + " width @" + width + " center " + center + " size " + size);
                Assert.GreaterOrEqual(visible.y, required, scope + " height @" + width + " center " + center + " size " + size);
            }
        }

        static void ExpectInside(MapScope scope, double latitude, double longitude)
        {
            Assert.IsTrue(MapFrames.TryGeographicBounds(scope, out var minLat, out var maxLat, out var minLon, out var maxLon), scope.ToString());
            Assert.GreaterOrEqual(latitude, minLat, scope.ToString());
            Assert.LessOrEqual(latitude, maxLat, scope.ToString());
            Assert.GreaterOrEqual(longitude, minLon, scope.ToString());
            Assert.LessOrEqual(longitude, maxLon, scope.ToString());
            Assert.IsTrue(MapFrames.TryProjected(scope, out var min, out var max), scope.ToString());
            var point = MapProjection.Project(latitude, longitude);
            Assert.GreaterOrEqual(point.x, min.x - 0.01f, scope.ToString());
            Assert.LessOrEqual(point.x, max.x + 0.01f, scope.ToString());
        }

        static void ExpectOutside(MapScope scope, double latitude, double longitude)
        {
            Assert.IsTrue(MapFrames.TryGeographicBounds(scope, out var minLat, out var maxLat, out var minLon, out var maxLon));
            var inside = latitude >= minLat && latitude <= maxLat && longitude >= minLon && longitude <= maxLon;
            Assert.IsFalse(inside, scope + " " + latitude + "," + longitude);
        }
    }
}
