using UnityEngine;

namespace LogistiCore.Map
{
    // Visual-only identity. Do not use these entries for city ownership, routing or economy.
    // The ids deliberately match the existing MapCities marker catalog.
    public enum MapCityLandmark : byte
    {
        None,
        Tower,
        HistoricTower,
        ClockTower,
        Mountain,
        Minaret,
        BridgeAndDome,
        CityWalls,
        CoastalMountains,
        LatticeTower,
        NeedleTower,
        SailRoof,
        Skyscrapers,
        Pyramids
    }

    public enum MapCityHorizon : byte
    {
        Urban,
        Hills,
        Coast
    }

    public readonly struct MapCityAtmosphere
    {
        public readonly string CityId;
        public readonly MapCityLandmark Landmark;
        public readonly MapCityHorizon Horizon;
        public readonly Color32 SkyLow;
        public readonly Color32 SkyHigh;
        public readonly Color32 Silhouette;
        public readonly Color32 Accent;
        public readonly int SkylineSeed;

        public MapCityAtmosphere(string cityId, MapCityLandmark landmark, MapCityHorizon horizon,
            Color32 skyLow, Color32 skyHigh, Color32 silhouette, Color32 accent, int skylineSeed)
        {
            CityId = cityId;
            Landmark = landmark;
            Horizon = horizon;
            SkyLow = skyLow;
            SkyHigh = skyHigh;
            Silhouette = silhouette;
            Accent = accent;
            SkylineSeed = skylineSeed;
        }
    }

    /// <summary>
    /// Lightweight city visual presets. These are stylized silhouettes, not
    /// photographic landmarks or a substitute for authored facility scenes.
    /// Additional cities can be added without changing the world projection.
    /// </summary>
    public static class MapCityAtmosphereCatalog
    {
        public static readonly MapCityAtmosphere[] All =
        {
            new MapCityAtmosphere("ankara", MapCityLandmark.Tower, MapCityHorizon.Hills,
                new Color32(30, 100, 135, 255), new Color32(9, 34, 66, 255),
                new Color32(13, 40, 59, 255), new Color32(248, 177, 93, 255), 11),
            new MapCityAtmosphere("istanbul", MapCityLandmark.HistoricTower, MapCityHorizon.Coast,
                new Color32(28, 95, 139, 255), new Color32(12, 33, 71, 255),
                new Color32(14, 39, 65, 255), new Color32(99, 205, 219, 255), 23),
            new MapCityAtmosphere("izmir", MapCityLandmark.ClockTower, MapCityHorizon.Coast,
                new Color32(40, 116, 143, 255), new Color32(16, 56, 94, 255),
                new Color32(16, 55, 70, 255), new Color32(255, 191, 109, 255), 31),
            new MapCityAtmosphere("bursa", MapCityLandmark.Mountain, MapCityHorizon.Hills,
                new Color32(37, 104, 120, 255), new Color32(12, 44, 70, 255),
                new Color32(15, 53, 61, 255), new Color32(139, 213, 181, 255), 43),
            new MapCityAtmosphere("antalya", MapCityLandmark.Minaret, MapCityHorizon.Coast,
                new Color32(26, 112, 155, 255), new Color32(9, 49, 87, 255),
                new Color32(14, 55, 68, 255), new Color32(246, 193, 121, 255), 47),
            new MapCityAtmosphere("adana", MapCityLandmark.BridgeAndDome, MapCityHorizon.Urban,
                new Color32(45, 107, 132, 255), new Color32(16, 47, 77, 255),
                new Color32(17, 47, 65, 255), new Color32(246, 181, 108, 255), 59),
            new MapCityAtmosphere("diyarbakir", MapCityLandmark.CityWalls, MapCityHorizon.Hills,
                new Color32(88, 99, 113, 255), new Color32(30, 40, 70, 255),
                new Color32(34, 48, 64, 255), new Color32(223, 163, 105, 255), 67),
            new MapCityAtmosphere("trabzon", MapCityLandmark.CoastalMountains, MapCityHorizon.Coast,
                new Color32(29, 97, 118, 255), new Color32(9, 42, 70, 255),
                new Color32(12, 48, 57, 255), new Color32(139, 200, 186, 255), 79)
        };

        public static bool TryGet(string cityId, out MapCityAtmosphere atmosphere)
        {
            if (!string.IsNullOrEmpty(cityId))
            {
                for (var i = 0; i < All.Length; i++)
                {
                    if (All[i].CityId == cityId)
                    {
                        atmosphere = All[i];
                        return true;
                    }
                }
            }

            atmosphere = default;
            return false;
        }
    }
}
