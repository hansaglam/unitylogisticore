namespace LogistiCore.Localization
{
    public static class LocaleCodes
    {
        public const string English = "en";
        public const string Turkish = "tr";
        public const string German = "de";

        public static string Normalize(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var trimmed = code.Trim();
            var separator = trimmed.IndexOfAny(new[] { '-', '_' });
            if (separator > 0)
                trimmed = trimmed.Substring(0, separator);

            return trimmed.ToLowerInvariant();
        }

        public static bool IsSupported(string code)
        {
            var normalized = Normalize(code);
            return normalized == English || normalized == Turkish || normalized == German;
        }

        /// <summary>
        /// An explicit selection must be a supported language. Anything else falls back to English.
        /// Startup device detection is handled by the Localization startup selectors, not this method.
        /// </summary>
        public static string NormalizeSelection(string languageCode)
        {
            return IsSupported(languageCode) ? Normalize(languageCode) : English;
        }
    }
}
