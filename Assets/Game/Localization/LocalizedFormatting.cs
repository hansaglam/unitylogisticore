using System;
using System.Globalization;

namespace LogistiCore.Localization
{
    /// <summary>
    /// Display formatting only. Domain values stay numeric, and currency is never converted.
    /// Language locales map to a formatting culture: en-US, tr-TR, de-DE.
    /// The currency ISO code is independent of that culture.
    /// </summary>
    public static class LocalizedFormatting
    {
        public const string Missing = "\u2014";

        public static CultureInfo CultureForLocale(string localeCode)
        {
            switch (LocaleCodes.Normalize(localeCode))
            {
                case LocaleCodes.Turkish:
                    return CultureInfo.GetCultureInfo("tr-TR");
                case LocaleCodes.German:
                    return CultureInfo.GetCultureInfo("de-DE");
                default:
                    return CultureInfo.GetCultureInfo("en-US");
            }
        }

        public static string Number(double value, CultureInfo culture, int decimalPlaces = 2)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));
            if (decimalPlaces < 0 || decimalPlaces > 6)
                throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
            if (!double.IsFinite(value))
                return Missing;

            return value.ToString("N" + decimalPlaces.ToString(CultureInfo.InvariantCulture), culture);
        }

        public static string Currency(decimal amount, string currencyCode, CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));

            var code = NormalizeCurrencyCode(currencyCode);
            if (code == null)
                return Missing;

            return amount.ToString("N2", culture) + " " + code;
        }

        public static string Percent(double ratio, CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));
            if (!double.IsFinite(ratio))
                return Missing;

            return ratio.ToString("P1", culture);
        }

        public static string Distance(double meters, CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));
            if (!double.IsFinite(meters))
                return Missing;

            if (Math.Abs(meters) >= 1000d)
                return (meters / 1000d).ToString("0.#", culture) + " km";

            return meters.ToString("0.#", culture) + " m";
        }

        public static string Weight(double kilograms, CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));
            if (!double.IsFinite(kilograms))
                return Missing;

            if (Math.Abs(kilograms) >= 1000d)
                return (kilograms / 1000d).ToString("0.#", culture) + " t";

            return kilograms.ToString("0.#", culture) + " kg";
        }

        public static string Duration(TimeSpan value, CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));
            if (value < TimeSpan.Zero)
                return Missing;

            var units = UnitsFor(culture);
            if (value.Ticks == 0)
                return "0" + units.Minute;

            if (value.Days == 0 && value.Hours == 0 && value.Minutes == 0)
                return value.Seconds.ToString(CultureInfo.InvariantCulture) + units.Second;

            return Join(
                value.Days, units.Day,
                value.Hours, units.Hour,
                value.Minutes, units.Minute,
                value.Seconds, units.Second);
        }

        /// <summary>
        /// Formats the offset's own clock time. It does not convert to the device time zone.
        /// </summary>
        public static string DateAndTime(DateTimeOffset value, CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));

            return value.ToString("g", culture);
        }

        static string NormalizeCurrencyCode(string currencyCode)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
                return null;

            var trimmed = currencyCode.Trim();
            if (trimmed.Length != 3)
                return null;

            for (var i = 0; i < trimmed.Length; i++)
            {
                if (!char.IsLetter(trimmed[i]))
                    return null;
            }

            return trimmed.ToUpperInvariant();
        }

        static string Join(int days, string dayUnit, int hours, string hourUnit, int minutes, string minuteUnit, int seconds, string secondUnit)
        {
            var result = string.Empty;
            Append(ref result, days, dayUnit);
            Append(ref result, hours, hourUnit);
            Append(ref result, minutes, minuteUnit);
            Append(ref result, seconds, secondUnit);
            return result;
        }

        static void Append(ref string result, int amount, string unit)
        {
            if (amount <= 0)
                return;

            var piece = amount.ToString(CultureInfo.InvariantCulture) + unit;
            result = result.Length == 0 ? piece : result + " " + piece;
        }

        static DurationUnits UnitsFor(CultureInfo culture)
        {
            switch (culture.TwoLetterISOLanguageName)
            {
                case LocaleCodes.Turkish:
                    return new DurationUnits("g", "sa", "dk", "sn");
                case LocaleCodes.German:
                    return new DurationUnits("T", "Std", "Min", "Sek");
                default:
                    return new DurationUnits("d", "h", "m", "s");
            }
        }

        readonly struct DurationUnits
        {
            public readonly string Day;
            public readonly string Hour;
            public readonly string Minute;
            public readonly string Second;

            public DurationUnits(string day, string hour, string minute, string second)
            {
                Day = day;
                Hour = hour;
                Minute = minute;
                Second = second;
            }
        }
    }
}
