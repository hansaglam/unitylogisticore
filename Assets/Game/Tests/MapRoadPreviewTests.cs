using System.Collections.Generic;
using LogistiCore.Backend;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    /// <summary>
    /// Deferred EditMode contracts: never asks OSRM for a route, and never
    /// synthesizes road geometry from capital-city center coordinates.
    /// </summary>
    public sealed class MapRoadPreviewTests
    {
        static MapBusinessLocation Ankara => new MapBusinessLocation("TUR", "ankara");
        static MapBusinessLocation Istanbul => new MapBusinessLocation("TUR", "istanbul");

        static Dictionary<string, object> Example()
        {
            return new Dictionary<string, object>
            {
                { "available", true },
                { "routeId", new string('a', 40) },
                { "provider", "local_osrm_driving" },
                { "mode", "road" },
                { "origin", City("TUR", "ankara") },
                { "destination", City("TUR", "istanbul") },
                { "distanceMeters", 453000.0d },
                { "durationSeconds", 22300.0d },
                { "geometry", new List<object>
                    {
                        Point(32.862446d, 39.929184d),
                        Point(31.2d, 40.4d),
                        Point(28.974277d, 41.017602d)
                    } },
                { "dispatchReady", false },
                { "purpose", "map_preview_only" },
                { "verification", "geometry_only" },
                { "attribution", "© OpenStreetMap contributors" }
            };
        }

        static Dictionary<string, object> City(string country, string city) =>
            new Dictionary<string, object>
            { { "countryCode", country }, { "cityKey", city } };

        static Dictionary<string, object> Point(double longitude, double latitude) =>
            new Dictionary<string, object>
            { { "longitude", longitude }, { "latitude", latitude } };

        [Test]
        public void RequestIsCountryScopedAndNeverContainsClientProvidedCoordinates()
        {
            Assert.IsTrue(ClientCallableCatalog.IsRead(ClientCallableCatalog.GetMyRoadRoutePreview));
            Assert.IsFalse(ClientCallableCatalog.IsCommand(ClientCallableCatalog.GetMyRoadRoutePreview));
            var request = MapRoadRoutePreviewParser.Request(Ankara, Istanbul);
            Assert.IsNotNull(request);
            Assert.AreEqual(3, request.Count);
            var origin = (Dictionary<string, object>)request["origin"];
            Assert.AreEqual("TUR", origin["countryCode"]);
            Assert.AreEqual("ankara", origin["cityKey"]);
            Assert.AreEqual(2, origin.Count);
            Assert.IsNull(MapRoadRoutePreviewParser.Request(Ankara, Ankara));
            Assert.IsNull(MapRoadRoutePreviewParser.Request(
                Ankara, new MapBusinessLocation("USA", "ne-q60")));
        }

        [Test]
        public void ParsedPreviewIsActualProvidedGeometryAndCannotDispatch()
        {
            var data = Example();
            Assert.IsTrue(MapRoadRoutePreviewParser.TryParse(data,
                out var route, out var noRoad));
            Assert.IsNotNull(route);
            Assert.IsFalse(noRoad);
            Assert.AreEqual(MapTransportMode.Road, route.Mode);
            Assert.AreEqual(Ankara, route.Origin);
            Assert.AreEqual(Istanbul, route.Destination);
            Assert.AreEqual(3, route.PointCount);
            Assert.AreEqual(39.929184d, route.PointAt(0).Latitude, 0.0000001d);
            Assert.AreEqual(453000d, route.DistanceMeters, 0.01d);
            Assert.IsTrue(route.TryProjectForDisplay(out Vector3[] projected));
            Assert.AreEqual(3, projected.Length);
        }

        [Test]
        public void NoRoadAndFailedBackendNeverCreateStraightLineFallback()
        {
            var noRoadResponse = new Dictionary<string, object>
            {
                { "available", false }, { "reason", "no_road_route" },
                { "purpose", "map_preview_only" }, { "dispatchReady", false }
            };
            Assert.IsTrue(MapRoadRoutePreviewParser.TryParse(noRoadResponse,
                out var route, out var noRoad));
            Assert.IsTrue(noRoad);
            Assert.IsNull(route);
            Assert.IsFalse(MapRoadRoutePreviewParser.TryParse(new Dictionary<string, object>(),
                out _, out _));
        }

        [Test]
        public void FakeRoadPermissionUnknownProviderAndMalformedWaypointsFail()
        {
            var input = Example();
            foreach (var pair in new[]
            {
                new KeyValuePair<string, object>("dispatchReady", true),
                new KeyValuePair<string, object>("provider", "invented_provider"),
                new KeyValuePair<string, object>("purpose", "payable_delivery"),
                new KeyValuePair<string, object>("verification", "truck_hgv_approved"),
                new KeyValuePair<string, object>("attribution", ""),
                new KeyValuePair<string, object>("routeId", "forged"),
                new KeyValuePair<string, object>("distanceMeters", double.NaN),
            })
            {
                var original = input[pair.Key];
                input[pair.Key] = pair.Value;
                Assert.IsFalse(MapRoadRoutePreviewParser.TryParse(input, out _, out _),
                    pair.Key);
                input[pair.Key] = original;
            }

            var points = (List<object>)input["geometry"];
            points[0] = Point(180d, 120d);
            Assert.IsFalse(MapRoadRoutePreviewParser.TryParse(input, out _, out _));
            input["geometry"] = new List<object> { Point(32d, 39d) };
            Assert.IsFalse(MapRoadRoutePreviewParser.TryParse(input, out _, out _));
        }

        [Test]
        public void EmptyLineComponentNeverSuggestsARealRoute()
        {
            var owner = new GameObject("RoadPreviewLayerTest");
            try
            {
                var visual = owner.AddComponent<MapRoadRoutePreviewLine>();
                Assert.IsFalse(visual.HasRoute);
                Assert.IsFalse(visual.Show(null));
                Assert.IsFalse(visual.HasRoute);
                visual.Clear();
                Assert.IsFalse(visual.HasRoute);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
