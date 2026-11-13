using System;
using System.Collections.Generic;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class MapSchematicLinksTests
    {
        [Test]
        public void LinkCatalogue_IsIllustrativeNotARealRoadGraph()
        {
            Assert.IsFalse(MapSchematicLinks.IsRealRoadGeometry);
            Assert.GreaterOrEqual(MapSchematicLinks.Count, MapCities.All.Length - 1);
        }

        [Test]
        public void EveryLink_UsesKnownCitiesWithNoDuplicateOrSelfConnection()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < MapSchematicLinks.Count; i++)
            {
                var link = MapSchematicLinks.At(i);
                Assert.AreNotEqual(link.From, link.To, "Self link " + i);
                Assert.IsTrue(MapCities.TryGet(link.From, out _), "Unknown origin " + link.From);
                Assert.IsTrue(MapCities.TryGet(link.To, out _), "Unknown destination " + link.To);
                var a = string.CompareOrdinal(link.From, link.To) < 0 ? link.From : link.To;
                var b = a == link.From ? link.To : link.From;
                Assert.IsTrue(seen.Add(a + "|" + b), "Duplicated connection " + a + "-" + b);
            }
        }

        [Test]
        public void AllExistingCities_AreConnectedWithoutInventedNewCities()
        {
            var reached = new HashSet<string>(StringComparer.Ordinal) { MapCities.All[0].Id };
            bool changed;
            do
            {
                changed = false;
                for (var i = 0; i < MapSchematicLinks.Count; i++)
                {
                    var link = MapSchematicLinks.At(i);
                    if (reached.Contains(link.From))
                        changed |= reached.Add(link.To);
                    if (reached.Contains(link.To))
                        changed |= reached.Add(link.From);
                }
            } while (changed);

            Assert.AreEqual(MapCities.All.Length, reached.Count);
            foreach (var city in MapCities.All)
                Assert.IsTrue(reached.Contains(city.Id), "Disconnected " + city.Id);
        }

        [Test]
        public void ProjectedCurves_StartAndEndAtActualAuthoredCityCoordinates()
        {
            for (var i = 0; i < MapSchematicLinks.Count; i++)
            {
                var link = MapSchematicLinks.At(i);
                Assert.IsTrue(MapSchematicLinks.TryEndpoints(link, out var from, out var to));
                var positions = new Vector3[17];
                Assert.IsTrue(MapSchematicLinks.Sample(link, positions));
                Assert.AreEqual(from.x, positions[0].x, 0.0001f);
                Assert.AreEqual(from.y, positions[0].y, 0.0001f);
                Assert.AreEqual(to.x, positions[16].x, 0.0001f);
                Assert.AreEqual(to.y, positions[16].y, 0.0001f);
                for (var j = 0; j < positions.Length; j++)
                {
                    Assert.IsTrue(float.IsFinite(positions[j].x));
                    Assert.IsTrue(float.IsFinite(positions[j].y));
                    Assert.AreEqual(0f, positions[j].z);
                }
            }
        }

        [Test]
        public void Selection_HighlightsOnlyAdjacentLinks()
        {
            var adjacent = 0;
            var unrelated = 0;
            for (var i = 0; i < MapSchematicLinks.Count; i++)
            {
                var link = MapSchematicLinks.At(i);
                if (link.Touches("ankara"))
                    adjacent++;
                else
                    unrelated++;
            }

            Assert.Greater(adjacent, 1);
            Assert.Greater(unrelated, 1);
            Assert.IsFalse(MapSchematicLinks.At(0).Touches(null));
            Assert.IsFalse(MapSchematicLinks.At(0).Touches("unknown"));
        }
    }
}
