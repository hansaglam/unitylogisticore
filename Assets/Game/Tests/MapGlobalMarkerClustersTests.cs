using System.Collections.Generic;
using LogistiCore.Map;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class MapGlobalMarkerClustersTests
    {
        static readonly MapGlobalCity[] Cities =
        {
            new MapGlobalCity("TUR", "istanbul", "İstanbul", 41.01d, 28.97d, 2),
            new MapGlobalCity("TUR", "ankara", "Ankara", 39.93d, 32.86d, 1),
            new MapGlobalCity("DEU", "ne-q64", "Berlin", 52.52d, 13.40d, 3),
            new MapGlobalCity("GRC", "ne-q1524", "Athens", 37.98d, 23.73d, 2)
        };

        [Test]
        public void NearbyPinsClusterAndPreferTheMoreImportantCity()
        {
            var positions = new[] { new Vector2(22, 22), new Vector2(27, 26),
                new Vector2(160, 150), new Vector2(600, 600) };
            var output = new List<MapGlobalMarkerGroup>();
            Assert.IsTrue(MapGlobalMarkerClusters.TryBuild(Cities, positions,
                new Rect(0, 0, 200, 200), 40f, 3, output));
            Assert.AreEqual(2, output.Count);
            Assert.AreEqual(2, output[0].Members);
            Assert.AreEqual("ankara", output[0].Representative.Location.CityKey);
            Assert.AreEqual(new Vector2(27, 26), output[0].ScreenPosition);
            Assert.AreEqual("Berlin", output[1].Representative.DisplayName);
        }

        [Test]
        public void ZoomRankAndViewportFilterDoNotGrantAccess()
        {
            var output = new List<MapGlobalMarkerGroup>();
            Assert.IsTrue(MapGlobalMarkerClusters.TryBuild(Cities,
                new[] { new Vector2(22, 22), new Vector2(27, 26),
                    new Vector2(160, 150), new Vector2(600, 600) },
                new Rect(0, 0, 200, 200), 40f, 1, output));
            Assert.AreEqual(1, output.Count);
            Assert.AreEqual("ankara", output[0].Representative.Location.CityKey);
            Assert.AreEqual(1, output[0].Members);
        }

        [Test]
        public void InvalidArgumentsDoNotDestroyPreviousResult()
        {
            var output = new List<MapGlobalMarkerGroup>
            {
                new MapGlobalMarkerGroup(Cities[0], Vector2.zero, 1)
            };
            Assert.IsFalse(MapGlobalMarkerClusters.TryBuild(Cities,
                new[] { Vector2.zero }, new Rect(0, 0, 100, 100), 40f, 5, output));
            Assert.IsFalse(MapGlobalMarkerClusters.TryBuild(Cities,
                new Vector2[4], new Rect(0, 0, 100, 100), 0f, 5, output));
            Assert.AreEqual(1, output.Count);
        }

        [Test]
        public void NegativeScreenPixelsHaveSeparateStableCells()
        {
            var output = new List<MapGlobalMarkerGroup>();
            var positions = new[] { new Vector2(-1f, -1f), new Vector2(1f, 1f),
                new Vector2(300, 300), new Vector2(400, 400) };
            Assert.IsTrue(MapGlobalMarkerClusters.TryBuild(Cities, positions,
                new Rect(-20, -20, 100, 100), 32f, 5, output));
            Assert.AreEqual(2, output.Count);
            Assert.AreEqual(1, output[0].Members);
            Assert.AreEqual(1, output[1].Members);
        }
    }
}
