namespace LogistiCore.Map
{
    public enum MapModule
    {
        None = 0,
        Fleet = 1,
        Jobs = 2,
        Warehouses = 3,
        Market = 4,
        Road = 5
    }

    public static class MapModuleIds
    {
        public const int FirstRail = 0;
        public const int LastRail = 4;
        public const int FirstTransport = 5;
        public const int LastTransport = 7;

        public static bool IsTransport(int railIndex)
        {
            return railIndex >= FirstTransport && railIndex <= LastTransport;
        }

        public static bool TryFromRail(int railIndex, out MapModule module)
        {
            if (railIndex < FirstRail || railIndex > LastRail)
            {
                module = MapModule.None;
                return false;
            }

            module = (MapModule)(railIndex + 1);
            return true;
        }

        public static int RailIndex(MapModule module)
        {
            if (module == MapModule.None)
                return -1;

            return (int)module - 1;
        }
    }
}
