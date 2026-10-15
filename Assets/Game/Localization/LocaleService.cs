using System.Globalization;
using LogistiCore;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace LogistiCore.Localization
{
    public static class LocaleService
    {
        public const string PreferenceKey = "logisti.locale";

        public static bool IsReady
        {
            get
            {
                if (!LocalizationSettings.InitializationOperation.IsDone)
                    return false;

                return LocalizationSettings.SelectedLocale != null;
            }
        }

        public static string CurrentCode
        {
            get
            {
                var locale = LocalizationSettings.SelectedLocale;
                if (locale == null)
                    return LocaleCodes.English;

                return LocaleCodes.NormalizeSelection(locale.Identifier.Code);
            }
        }

        public static CultureInfo CurrentCulture => LocalizedFormatting.CultureForLocale(CurrentCode);

        public static bool TrySelect(string languageCode)
        {
            if (!IsReady)
            {
                GameLog.Info("Locale", "Ignored a language change because localization is still initializing.");
                return false;
            }

            var resolved = LocaleCodes.NormalizeSelection(languageCode);
            var locale = LocalizationSettings.AvailableLocales.GetLocale(resolved);
            if (locale == null && resolved != LocaleCodes.English)
            {
                resolved = LocaleCodes.English;
                locale = LocalizationSettings.AvailableLocales.GetLocale(resolved);
            }

            if (locale == null)
            {
                GameLog.Error("Locale", "English locale is not available.");
                return false;
            }

            LocalizationSettings.SelectedLocale = locale;
            GameLog.Info("Locale", "Selected " + resolved + ".");
            return true;
        }
    }
}
