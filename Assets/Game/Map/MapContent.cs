namespace LogistiCore.Map
{
    public static class MapContent
    {
        public const string Table = "Map";

        public const string Title = "map.title";
        public const string ViewWorld = "map.view.world";
        public const string ViewTurkey = "map.view.turkey";
        public const string ViewEurope = "map.view.europe";
        public const string ViewAsia = "map.view.asia";
        public const string ViewAfrica = "map.view.africa";
        public const string ViewNorthAmerica = "map.view.north_america";
        public const string ViewSouthAmerica = "map.view.south_america";
        public const string ViewOceania = "map.view.oceania";
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
        public const string SoonBody = "map.soon.body";
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
        public const string WaterBlackSea = "map.water.black_sea";
        public const string WaterAegean = "map.water.aegean_sea";
        public const string WaterMediterranean = "map.water.mediterranean_sea";
        public const string WaterMarmara = "map.water.marmara_sea";
        public const string WaterCaspian = "map.water.caspian_sea";
        public const string WaterAdriatic = "map.water.adriatic_sea";
        public const string WaterRedSea = "map.water.red_sea";
        public const string WaterNorthSea = "map.water.north_sea";
        public const string WaterBaltic = "map.water.baltic_sea";
        public const string WaterArabian = "map.water.arabian_sea";
        public const string WaterSouthChina = "map.water.south_china_sea";
        public const string WaterJapan = "map.water.japan_sea";
        public const string WaterCaribbean = "map.water.caribbean_sea";
        public const string WaterBering = "map.water.bering_sea";
        public const string WaterCoral = "map.water.coral_sea";
        public const string WaterTasman = "map.water.tasman_sea";
        public const string OceanAtlantic = "map.ocean.atlantic";
        public const string OceanPacific = "map.ocean.pacific";
        public const string OceanIndian = "map.ocean.indian";
        public const string OceanArctic = "map.ocean.arctic";
        public const string OceanSouthern = "map.ocean.southern";

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

        public static readonly string[] RegionViews =
        {
            ViewWorld,
            ViewTurkey,
            ViewEurope,
            ViewAsia,
            ViewAfrica,
            ViewNorthAmerica,
            ViewSouthAmerica,
            ViewOceania
        };

        public static readonly string[] Keys =
        {
            Title,
            ViewWorld,
            ViewTurkey,
            ViewEurope,
            ViewAsia,
            ViewAfrica,
            ViewNorthAmerica,
            ViewSouthAmerica,
            ViewOceania,
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
            SoonBody,
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
            "cities.trabzon",
            WaterBlackSea,
            WaterAegean,
            WaterMediterranean,
            WaterMarmara,
            WaterCaspian,
            WaterAdriatic,
            WaterRedSea,
            WaterNorthSea,
            WaterBaltic,
            WaterArabian,
            WaterSouthChina,
            WaterJapan,
            WaterCaribbean,
            WaterBering,
            WaterCoral,
            WaterTasman,
            OceanAtlantic,
            OceanPacific,
            OceanIndian,
            OceanArctic,
            OceanSouthern
        };

        public static readonly string[] WaterKeys =
        {
            WaterBlackSea,
            WaterAegean,
            WaterMediterranean,
            WaterMarmara,
            WaterCaspian,
            WaterAdriatic,
            WaterRedSea,
            WaterNorthSea,
            WaterBaltic,
            WaterArabian,
            WaterSouthChina,
            WaterJapan,
            WaterCaribbean,
            WaterBering,
            WaterCoral,
            WaterTasman,
            OceanAtlantic,
            OceanPacific,
            OceanIndian,
            OceanArctic,
            OceanSouthern
        };

        public static string CityKey(string cityId)
        {
            return "cities." + cityId;
        }
    }
}
