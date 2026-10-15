namespace LogistiCore.Localization
{
    public static class LocalizationContent
    {
        public const string Table = "Foundation";

        public const string Title = "ui.verification.title";
        public const string Body = "ui.verification.body";
        public const string Greeting = "ui.verification.greeting";
        public const string Deliveries = "ui.verification.deliveries";
        public const string ActiveLocale = "ui.verification.active_locale";
        public const string LabelNumber = "ui.verification.label_number";
        public const string LabelCurrency = "ui.verification.label_currency";
        public const string LabelPercent = "ui.verification.label_percent";
        public const string LabelDistance = "ui.verification.label_distance";
        public const string LabelWeight = "ui.verification.label_weight";
        public const string LabelDuration = "ui.verification.label_duration";
        public const string LabelDateTime = "ui.verification.label_datetime";
        public const string Portrait = "ui.verification.portrait";
        public const string Landscape = "ui.verification.landscape";
        public const string LanguageEnglish = "ui.language.en";
        public const string LanguageTurkish = "ui.language.tr";
        public const string LanguageGerman = "ui.language.de";

        public static readonly string[] Keys =
        {
            Title,
            Body,
            Greeting,
            Deliveries,
            ActiveLocale,
            LabelNumber,
            LabelCurrency,
            LabelPercent,
            LabelDistance,
            LabelWeight,
            LabelDuration,
            LabelDateTime,
            Portrait,
            Landscape,
            LanguageEnglish,
            LanguageTurkish,
            LanguageGerman
        };

        public static bool IsSmart(string key)
        {
            return key == Greeting || key == Deliveries || key == ActiveLocale;
        }
    }
}
