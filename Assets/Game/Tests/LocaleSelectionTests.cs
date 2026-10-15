using LogistiCore.Localization;
using LogistiCore.UI;
using NUnit.Framework;
using UnityEngine;

namespace LogistiCore.Tests
{
    public class LocaleSelectionTests
    {
        [TestCase("en", "en")]
        [TestCase("EN", "en")]
        [TestCase("tr-TR", "tr")]
        [TestCase("de_AT", "de")]
        [TestCase("fr", "en")]
        [TestCase("fr-FR", "en")]
        [TestCase("", "en")]
        [TestCase(null, "en")]
        public void NormalizeSelection_AcceptsSupportedLanguagesAndFallsBackToEnglish(string requested, string expected)
        {
            Assert.AreEqual(expected, LocaleCodes.NormalizeSelection(requested));
        }

        [Test]
        public void SupportedSet_IsEnglishTurkishAndGerman()
        {
            Assert.IsTrue(LocaleCodes.IsSupported("en-GB"));
            Assert.IsTrue(LocaleCodes.IsSupported("tr"));
            Assert.IsTrue(LocaleCodes.IsSupported("de-DE"));
            Assert.IsFalse(LocaleCodes.IsSupported("es"));
            Assert.IsFalse(LocaleCodes.IsSupported(" "));
        }
    }

    public class SafeAreaLayoutTests
    {
        [Test]
        public void FullScreenSafeArea_StretchesTheRect()
        {
            SafeAreaLayout.CalculateAnchors(new Rect(0f, 0f, 1000f, 2000f), 1000f, 2000f, out var min, out var max);
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
        }

        [Test]
        public void Insets_AreNormalizedFromTheBottomLeft()
        {
            SafeAreaLayout.CalculateAnchors(new Rect(100f, 50f, 800f, 1400f), 1000f, 1600f, out var min, out var max);
            Assert.AreEqual(0.1f, min.x, 0.0001f);
            Assert.AreEqual(50f / 1600f, min.y, 0.0001f);
            Assert.AreEqual(0.9f, max.x, 0.0001f);
            Assert.AreEqual(1450f / 1600f, max.y, 0.0001f);
        }

        [Test]
        public void InvalidScreenOrSafeArea_StretchesTheRect()
        {
            SafeAreaLayout.CalculateAnchors(new Rect(0f, 0f, 100f, 100f), 0f, 100f, out var min, out var max);
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);

            SafeAreaLayout.CalculateAnchors(new Rect(10f, 10f, 0f, 10f), 100f, 100f, out min, out max);
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
        }

        [Test]
        public void SafeAreaOutsideTheScreen_IsClamped()
        {
            SafeAreaLayout.CalculateAnchors(new Rect(-20f, -10f, 2000f, 3000f), 1000f, 2000f, out var min, out var max);
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
        }
    }
}
