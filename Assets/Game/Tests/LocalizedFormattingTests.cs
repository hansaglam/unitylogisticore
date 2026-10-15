using System;
using System.Globalization;
using LogistiCore.Localization;
using NUnit.Framework;

namespace LogistiCore.Tests
{
    public class LocalizedFormattingTests
    {
        static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
        static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
        static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

        [Test]
        public void Number_UsesThePassedCulture()
        {
            Assert.AreEqual("1,234,567.89", LocalizedFormatting.Number(1234567.89d, English));
            Assert.AreEqual("1.234.567,89", LocalizedFormatting.Number(1234567.89d, Turkish));
            Assert.AreEqual("1.234.567,89", LocalizedFormatting.Number(1234567.89d, German));
        }

        [Test]
        public void Number_RejectsNonFiniteValues()
        {
            Assert.AreEqual(LocalizedFormatting.Missing, LocalizedFormatting.Number(double.NaN, English));
            Assert.AreEqual(LocalizedFormatting.Missing, LocalizedFormatting.Number(double.PositiveInfinity, German));
        }

        [Test]
        public void Currency_KeepsTheExplicitCodeAndDoesNotUseTheLocaleSymbol()
        {
            Assert.AreEqual("1,234.50 EUR", LocalizedFormatting.Currency(1234.5m, "EUR", English));
            Assert.AreEqual("1.234,50 EUR", LocalizedFormatting.Currency(1234.5m, "eur", German));
            Assert.AreEqual("1.234,50 TRY", LocalizedFormatting.Currency(1234.5m, "TRY", Turkish));
            Assert.AreEqual("10.00 USD", LocalizedFormatting.Currency(10m, "USD", English));
            Assert.AreEqual("10.00 EUR", LocalizedFormatting.Currency(10m, "EUR", English));
            Assert.That(LocalizedFormatting.Currency(10m, "EUR", English), Does.Not.Contain("$"));
            Assert.That(LocalizedFormatting.Currency(10m, "USD", German), Does.Not.Contain("€"));
            Assert.That(LocalizedFormatting.Currency(10m, "EUR", Turkish), Does.Not.Contain("₺"));
        }

        [Test]
        public void Currency_RejectsInvalidCodesAndKeepsNegativeAmounts()
        {
            Assert.AreEqual(LocalizedFormatting.Missing, LocalizedFormatting.Currency(1m, "", English));
            Assert.AreEqual(LocalizedFormatting.Missing, LocalizedFormatting.Currency(1m, "US", English));
            Assert.AreEqual(LocalizedFormatting.Missing, LocalizedFormatting.Currency(1m, "12A", English));
            Assert.AreEqual("-1.00 EUR", LocalizedFormatting.Currency(-1m, "EUR", English));
        }

        [Test]
        public void Percent_UsesThePassedCulture()
        {
            Assert.AreEqual(0.125d.ToString("P1", English), LocalizedFormatting.Percent(0.125d, English));
            Assert.AreEqual(0.125d.ToString("P1", German), LocalizedFormatting.Percent(0.125d, German));
            Assert.AreEqual(0.125d.ToString("P1", Turkish), LocalizedFormatting.Percent(0.125d, Turkish));
            Assert.That(LocalizedFormatting.Percent(0.125d, English), Does.Contain("12.5"));
            Assert.That(LocalizedFormatting.Percent(0.125d, German), Does.Contain("12,5"));
            Assert.AreEqual(LocalizedFormatting.Missing, LocalizedFormatting.Percent(double.NaN, English));
        }

        [Test]
        public void DistanceAndWeight_SwitchUnitsAtOneThousand()
        {
            Assert.AreEqual("850 m", LocalizedFormatting.Distance(850d, English));
            Assert.AreEqual("1.5 km", LocalizedFormatting.Distance(1500d, English));
            Assert.AreEqual("1,5 km", LocalizedFormatting.Distance(1500d, Turkish));
            Assert.AreEqual("1,5 km", LocalizedFormatting.Distance(1500d, German));
            Assert.AreEqual("999 kg", LocalizedFormatting.Weight(999d, English));
            Assert.AreEqual("1 t", LocalizedFormatting.Weight(1000d, English));
            Assert.AreEqual("2,4 t", LocalizedFormatting.Weight(2400d, German));
            Assert.AreEqual(LocalizedFormatting.Missing, LocalizedFormatting.Distance(double.NaN, English));
        }

        [Test]
        public void Duration_UsesLanguageUnitsWithoutDependingOnTheClock()
        {
            Assert.AreEqual("0m", LocalizedFormatting.Duration(TimeSpan.Zero, English));
            Assert.AreEqual("0dk", LocalizedFormatting.Duration(TimeSpan.Zero, Turkish));
            Assert.AreEqual("0Min", LocalizedFormatting.Duration(TimeSpan.Zero, German));
            Assert.AreEqual("45s", LocalizedFormatting.Duration(TimeSpan.FromSeconds(45), English));
            Assert.AreEqual("45sn", LocalizedFormatting.Duration(TimeSpan.FromSeconds(45), Turkish));
            Assert.AreEqual("1m 30s", LocalizedFormatting.Duration(TimeSpan.FromSeconds(90), English));
            Assert.AreEqual("2h 5m", LocalizedFormatting.Duration(new TimeSpan(2, 5, 0), English));
            Assert.AreEqual("2sa 5dk", LocalizedFormatting.Duration(new TimeSpan(2, 5, 0), Turkish));
            Assert.AreEqual("1T 2Std 5Min", LocalizedFormatting.Duration(new TimeSpan(1, 2, 5, 0), German));
            Assert.AreEqual(LocalizedFormatting.Missing, LocalizedFormatting.Duration(TimeSpan.FromMinutes(-1), English));
        }

        [Test]
        public void DateAndTime_UsesTheOffsetClockAndThePassedCulture()
        {
            var value = new DateTimeOffset(2026, 10, 16, 15, 30, 0, TimeSpan.Zero);
            Assert.AreEqual(value.ToString("g", English), LocalizedFormatting.DateAndTime(value, English));
            Assert.AreEqual(value.ToString("g", German), LocalizedFormatting.DateAndTime(value, German));
            Assert.AreNotEqual(
                LocalizedFormatting.DateAndTime(value, English),
                LocalizedFormatting.DateAndTime(value, German));
        }

        [Test]
        public void Formatting_RejectsANullCulture()
        {
            Assert.Throws<ArgumentNullException>(() => LocalizedFormatting.Number(1d, null));
            Assert.Throws<ArgumentNullException>(() => LocalizedFormatting.Currency(1m, "EUR", null));
            Assert.Throws<ArgumentOutOfRangeException>(() => LocalizedFormatting.Number(1d, English, 8));
        }

        [Test]
        public void CultureForLocale_MapsSupportedLanguagesAndFallsBackToEnglish()
        {
            Assert.AreEqual("tr-TR", LocalizedFormatting.CultureForLocale("tr-TR").Name);
            Assert.AreEqual("de-DE", LocalizedFormatting.CultureForLocale("de-AT").Name);
            Assert.AreEqual("en-US", LocalizedFormatting.CultureForLocale("fr-FR").Name);
            Assert.AreEqual("en-US", LocalizedFormatting.CultureForLocale(null).Name);
        }
    }
}
