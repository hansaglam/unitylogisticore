using System.IO;
using System.Security.Cryptography;
using LogistiCore.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public sealed class MapCountryColorTests
    {
        const string ScenePath = "Assets/Game/Scenes/WorldMapVerticalSlice.unity";
        const string SceneSha = "e437dc5fbf47f9e7f55b7d76e347fdce63fe1c06bd4506ba931ecfff8bd678a0";
        const string LandSha = "3fd92747aa7a3c45772e1377928d6a0b969311a85fa4e75a9efdcfba603be463";
        const string ShaderSha = "e201abb3effec643ec74b82e1aa19d596dacf207c84aaec03e1a0a59e00646ac";

        [Test]
        public void Palette_SeparatesTurkeyFromSlateLandWithoutRepaintingTheSea()
        {
            Assert.AreEqual(4, MapGeographyBaker.Ocean.r);
            Assert.AreEqual(12, MapGeographyBaker.Ocean.g);
            Assert.AreEqual(22, MapGeographyBaker.Ocean.b);
            Assert.Greater(MapGeographyBaker.LandDark.b, MapGeographyBaker.Ocean.b + 16);
            Assert.Greater(MapGeographyBaker.TurkeyLight.g, MapGeographyBaker.LandLight.g);
            Assert.Greater(MapGeographyBaker.TurkeyLight.b - MapGeographyBaker.TurkeyLight.r, MapGeographyBaker.LandLight.b - MapGeographyBaker.LandLight.r);
            Assert.Less(MapGeographyBaker.TurkeyLight.g, (byte)160);

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(MapGeographyBaker.ReliefTexturePath)));
                var ocean = Sample(texture, 30d, -30d);
                var ankara = Sample(texture, 39.929184d, 32.862446d);
                var athens = Sample(texture, 37.98d, 23.73d);
                var sofia = Sample(texture, 42.70d, 23.32d);
                var paris = Sample(texture, 48.85d, 2.35d);
                var pontic = Sample(texture, 40.6d, 39.5d);
                var taurus = Sample(texture, 37.2d, 33.5d);

                Assert.AreEqual(MapGeographyBaker.Ocean, ocean);
                Assert.AreEqual(255, ocean.a);
                Assert.Less(ankara.g, 100);
                Assert.Greater(ankara.g, athens.g);
                Assert.Greater(ankara.g, sofia.g);
                Assert.Greater(ankara.g, paris.g);
                Assert.Greater(ankara.b - ankara.r, athens.b - athens.r);
                Assert.Greater(ankara.b - ankara.r, sofia.b - sofia.r);
                Assert.GreaterOrEqual(Mathf.Abs(taurus.g - pontic.g), 12);
                Assert.AreNotEqual(ocean, ankara);
                Assert.AreNotEqual(ankara, athens);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            Assert.AreEqual(SceneSha, Sha(ScenePath));
            Assert.AreEqual(LandSha, Sha("Assets/Game/Map/Generated/MapLandFill.asset"));
            Assert.AreEqual(ShaderSha, Sha("Assets/Game/Map/MapRegionalRelief.shader"));
            Assert.AreEqual(97087, UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/Map/Generated/MapLandFill.asset").vertexCount);
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

        static string Sha(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = sha.ComputeHash(stream);
                return System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
