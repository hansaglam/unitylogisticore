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
        public const string Close = "map.action.close";
        public const string ModuleUnavailable = "map.context.unavailable";
        public const string InspectorTitle = "map.inspector.title";
        public const string InspectorScope = "map.inspector.scope";
        public const string InspectorCity = "map.inspector.city";
        public const string InspectorCoordinates = "map.inspector.coordinates";
        public const string SelectCity = "map.inspector.select_city";
        public const string SummaryUnconnected = "map.summary.unconnected";
        public const string ModeSoon = "map.mode.soon";
        public const string ModeFleet = "map.mode.fleet";
        public const string ModeJobs = "map.mode.jobs";
        public const string ModeWarehouses = "map.mode.warehouses";
        public const string ModeMarket = "map.mode.market";
        public const string ModeRoad = "map.mode.road";
        public const string ModeRail = "map.mode.rail";
        public const string ModeSea = "map.mode.sea";
        public const string ModeAir = "map.mode.air";
        public const string RailMap = "map.rail.map";
        public const string RailFleet = "map.rail.fleet";
        public const string RailJobs = "map.rail.jobs";
        public const string RailWarehouses = "map.rail.warehouses";
        public const string RailMarket = "map.rail.market";
        public const string RailRoad = "map.rail.road";
        public const string RailRail = "map.rail.rail";
        public const string RailSea = "map.rail.sea";
        public const string RailAir = "map.rail.air";

        public static readonly string[] FutureModes =
        {
            ModeFleet,
            ModeJobs,
            ModeWarehouses,
            ModeMarket,
            ModeRoad,
            ModeRail,
            ModeSea,
            ModeAir
        };

        public static readonly string[] RailLabels =
        {
            RailMap,
            RailFleet,
            RailJobs,
            RailWarehouses,
            RailMarket,
            RailRoad,
            RailRail,
            RailSea,
            RailAir
        };

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
            Close,
            ModuleUnavailable,
            InspectorTitle,
            InspectorScope,
            InspectorCity,
            InspectorCoordinates,
            SelectCity,
            SummaryUnconnected,
            ModeSoon,
            ModeFleet,
            ModeJobs,
            ModeWarehouses,
            ModeMarket,
            ModeRoad,
            ModeRail,
            ModeSea,
            ModeAir,
            RailMap,
            RailFleet,
            RailJobs,
            RailWarehouses,
            RailMarket,
            RailRoad,
            RailRail,
            RailSea,
            RailAir,
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
