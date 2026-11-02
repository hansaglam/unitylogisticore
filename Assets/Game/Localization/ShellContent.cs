namespace LogistiCore.Localization
{
    /// <summary>
    /// Player-facing copy for the portrait shell. Values live in the Foundation string table.
    /// </summary>
    public static class ShellContent
    {
        public const string Table = LocalizationContent.Table;

        public const string Brand = "shell.brand";
        public const string Heading = "shell.home.heading";
        public const string Support = "shell.home.support";
        public const string Offline = "shell.home.offline";
        public const string WorldMap = "shell.action.world_map";
        public const string MapCaption = "shell.home.map_caption";
        public const string Home = "shell.action.home";
        public const string CompanyHeading = "shell.company.heading";
        public const string CompanyStatus = "shell.company.status";
        public const string CompanyUnconnected = "shell.company.unconnected";
        public const string CompanyAction = "shell.action.company";
        public const string CompanyCaption = "shell.home.company_caption";

        public const string LongestWorldMapLabel = "Dünya Haritası";
        public const string LongestHomeLabel = "Startseite";
        public const string LongestCompanyLabel = "Unternehmen";
        public const string LongestHeading = "Operationszentrum";
        public const string LongestMapCaption = "Şehirleri ve dünya haritasını keşfet.";

        public static readonly string[] Keys =
        {
            Brand,
            Heading,
            Support,
            Offline,
            WorldMap,
            MapCaption,
            Home,
            CompanyHeading,
            CompanyStatus,
            CompanyUnconnected,
            CompanyAction,
            CompanyCaption
        };

        public readonly struct Line
        {
            public readonly string Key;
            public readonly string English;
            public readonly string Turkish;
            public readonly string German;

            public Line(string key, string english, string turkish, string german)
            {
                Key = key;
                English = english;
                Turkish = turkish;
                German = german;
            }
        }

        public static readonly Line[] Lines =
        {
            new Line(Brand, "LOGISTICORE", "LOGISTICORE", "LOGISTICORE"),
            new Line(Heading, "Operations Center", "Operasyon Merkezi", "Operationszentrum"),
            new Line(Support, "Select a destination.", "Bir hedef seçin.", "Ziel auswählen."),
            new Line(
                Offline,
                "No live operations are connected.",
                "Canlı operasyon bağlantısı yok.",
                "Keine Live-Operationen verbunden."),
            new Line(WorldMap, "World Map", "Dünya Haritası", "Weltkarte"),
            new Line(MapCaption, "Explore cities and the world map.", "Şehirleri ve dünya haritasını keşfet.", "Entdecke Städte und die Weltkarte."),
            new Line(Home, "Home", "Ana Sayfa", "Startseite"),
            new Line(CompanyHeading, "Company", "Şirket", "Unternehmen"),
            new Line(CompanyStatus, "Not connected yet", "Henüz bağlı değil", "Noch nicht verbunden"),
            new Line(
                CompanyUnconnected,
                "Company data is not connected yet.",
                "Şirket verisi henüz bağlı değil.",
                "Unternehmensdaten sind noch nicht verbunden."),
            new Line(CompanyAction, "Company", "Şirket", "Unternehmen"),
            new Line(CompanyCaption, "Open the company overview.", "Şirket özetini açın.", "Übersicht öffnen.")
        };
    }
}
