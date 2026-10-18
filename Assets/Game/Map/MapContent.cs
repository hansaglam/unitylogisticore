namespace LogistiCore.Map
{
    public static class MapContent
    {
        public const string Table = "Map";

        public const string Title = "map.title";
        public const string ViewWorld = "map.view.world";
        public const string ViewTurkey = "map.view.turkey";
        public const string FocusWorld = "map.action.focus_world";
        public const string FocusTurkey = "map.action.focus_turkey";
        public const string ZoomIn = "map.action.zoom_in";
        public const string ZoomOut = "map.action.zoom_out";
        public const string Restore = "map.action.restore";
        public const string Collapse = "map.action.collapse";
        public const string Expand = "map.action.expand";
        public const string InspectorTitle = "map.inspector.title";
        public const string InspectorScope = "map.inspector.scope";
        public const string InspectorCity = "map.inspector.city";
        public const string InspectorCoordinates = "map.inspector.coordinates";
        public const string SelectCity = "map.inspector.select_city";

        public static readonly string[] Keys =
        {
            Title,
            ViewWorld,
            ViewTurkey,
            FocusWorld,
            FocusTurkey,
            ZoomIn,
            ZoomOut,
            Restore,
            Collapse,
            Expand,
            InspectorTitle,
            InspectorScope,
            InspectorCity,
            InspectorCoordinates,
            SelectCity,
            "cities.izmir",
            "cities.istanbul",
            "cities.ankara",
            "cities.bursa",
            "cities.antalya",
            "cities.adana",
            "cities.diyarbakir",
            "cities.trabzon"
        };

        public static string CityKey(string cityId)
        {
            return "cities." + cityId;
        }
    }
}
