using System;
using System.Collections.Generic;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class MapGlobalCityCatalogTests
    {
        [Test]
        public void InitialCatalogIncludesMultipleContinentsAndMoreThanLegacyMarkers()
        {
            Assert.Greater(MapGlobalCityCatalog.All.Length, 200);
            var countries = new HashSet<string>(StringComparer.Ordinal);
            var locations = new HashSet<MapBusinessLocation>();
            foreach (var city in MapGlobalCityCatalog.All)
            {
                Assert.IsTrue(city.Location.IsValid, city.DisplayName);
                Assert.IsTrue(locations.Add(city.Location), city.DisplayName);
                Assert.IsFalse(string.IsNullOrWhiteSpace(city.DisplayName));
                Assert.GreaterOrEqual(city.Latitude, -90d);
                Assert.LessOrEqual(city.Latitude, 90d);
                Assert.GreaterOrEqual(city.Longitude, -180d);
                Assert.LessOrEqual(city.Longitude, 180d);
                Assert.IsTrue(MapProjection.TryProject(city.Latitude, city.Longitude, out _));
                countries.Add(city.Location.CountryCode);
            }
            Assert.Greater(countries.Count, 150);
            Assert.IsTrue(countries.Contains("TUR"));
            Assert.IsTrue(countries.Contains("DEU"));
            Assert.IsTrue(countries.Contains("USA"));
            Assert.IsTrue(countries.Contains("JPN"));
            Assert.IsTrue(countries.Contains("BRA"));
            Assert.IsTrue(countries.Contains("AUS"));
        }

        [Test]
        public void ExistingEightTurkeyCityIdsAndCoordinatesRemainStable()
        {
            foreach (var old in MapCities.All)
            {
                Assert.IsTrue(MapGlobalCityCatalog.TryGet(new MapBusinessLocation("TUR", old.Id), out var found), old.Id);
                Assert.AreEqual(old.Latitude, found.Latitude, 0.0000001d, old.Id);
                Assert.AreEqual(old.Longitude, found.Longitude, 0.0000001d, old.Id);
            }
        }

        [Test]
        public void UnknownCityHasNoInventedCoordinatesOrAccess()
        {
            Assert.IsFalse(MapGlobalCityCatalog.TryGet(new MapBusinessLocation("DEU", "unknown_city"), out _));
            Assert.IsFalse(MapGlobalCityCatalog.TryGet(new MapBusinessLocation("", "berlin"), out _));
        }
    }

    public sealed class MapVerifiedRouteTests
    {
        static readonly MapBusinessLocation Ankara = new MapBusinessLocation("TUR", "ankara");
        static readonly MapBusinessLocation Istanbul = new MapBusinessLocation("TUR", "istanbul");

        static MapRouteGeoPoint[] ExampleGeometry() => new[]
        {
            new MapRouteGeoPoint(39.929184d, 32.862446d),
            new MapRouteGeoPoint(40.3d, 31.2d),
            new MapRouteGeoPoint(41.017602d, 28.974277d)
        };

        [Test]
        public void UnverifiedQuoteCannotBecomeMapRoute()
        {
            Assert.IsFalse(MapVerifiedRoute.TryCreate(false, "test-route", "test-provider",
                MapTransportMode.Road, Ankara, Istanbul, 490000d, 20500d,
                ExampleGeometry(), out _));
            Assert.IsFalse(MapVerifiedRoute.TryCreate(true, "test-route", "test-provider",
                MapTransportMode.Road, Ankara, new MapBusinessLocation("DEU", "unknown_city"),
                490000d, 20500d, ExampleGeometry(), out _));
            Assert.IsFalse(MapVerifiedRoute.TryCreate(true, "test-route", "test-provider",
                MapTransportMode.Road, Ankara, Ankara,
                490000d, 20500d, ExampleGeometry(), out _));
        }

        [Test]
        public void GeometryCanBeProjectedOnlyWhenQuoteIsTrustedAndWellFormed()
        {
            var points = ExampleGeometry();
            Assert.IsTrue(MapVerifiedRoute.TryCreate(true, "test-only-route", "test-provider",
                MapTransportMode.Road, Ankara, Istanbul, 490000d, 20500d, points,
                out var route));
            points[0] = new MapRouteGeoPoint(0d, 0d); // caller cannot mutate cached geometry
            Assert.AreEqual(39.929184d, route.PointAt(0).Latitude, 0.0000001d);
            Assert.IsTrue(route.TryProjectForDisplay(out var projected));
            Assert.AreEqual(3, projected.Length);
            Assert.IsTrue(MapProjection.TryProject(39.929184d, 32.862446d, out var start));
            Assert.AreEqual(start.x, projected[0].x, 0.001f);
            Assert.AreEqual(start.y, projected[0].y, 0.001f);
        }

        [Test]
        public void InvalidGeometryOrEstimateIsNeverAccepted()
        {
            Assert.IsFalse(MapVerifiedRoute.TryCreate(true, "t", "p",
                MapTransportMode.Road, Ankara, Istanbul, 0d, 90d,
                ExampleGeometry(), out _));
            Assert.IsFalse(MapVerifiedRoute.TryCreate(true, "t", "p",
                MapTransportMode.Road, Ankara, Istanbul, 100d, double.NaN,
                ExampleGeometry(), out _));
            Assert.IsFalse(MapVerifiedRoute.TryCreate(true, "t", "p",
                MapTransportMode.Road, Ankara, Istanbul, 100d, 90d,
                new[] { new MapRouteGeoPoint(99d, 0d), new MapRouteGeoPoint(1d, 2d) },
                out _));
        }

        [Test]
        public void DatelineRequiresDedicatedVisualSegmentationRatherThanFalseLine()
        {
            Assert.IsTrue(MapVerifiedRoute.TryCreate(true, "test-antimeridian", "test-provider",
                MapTransportMode.Sea, Ankara, Istanbul, 100d, 90d,
                new[] { new MapRouteGeoPoint(14d, 179.5d), new MapRouteGeoPoint(14d, -179.5d) },
                out var quote));
            Assert.IsFalse(quote.TryProjectForDisplay(out _));
        }
    }
}
