using UnityEngine;

namespace LogistiCore.Map
{
    public enum MapScope
    {
        World = 0,
        Turkey = 1,
        Europe = 2,
        Asia = 3,
        Africa = 4,
        NorthAmerica = 5,
        SouthAmerica = 6,
        Oceania = 7
    }

    /// <summary>
    /// Geographic frames. Türkiye limits are the Natural Earth 50m ADM0_A3=TUR polygon extent.
    /// </summary>
    public static class MapFrames
    {
        public const double TurkeyMinLongitude = 25.668945;
        public const double TurkeyMaxLongitude = 44.817188;
        public const double TurkeyMinLatitude = 35.831445;
        public const double TurkeyMaxLatitude = 42.093262;

        public const float TurkeyFocusPadding = 1.28f;
        public const float WorldFocusPadding = 1.02f;
        public const float RegionFocusPadding = 1.06f;

        // Continent frames are axis-aligned bounds of Natural Earth 50m rings.
        // Europe keeps continental Europe, Iceland, the Azores and European Russia
        // west of 60°E, including Svalbard. The Canaries are omitted: an
        // axis-aligned box around them would also cover North Africa.
        // Asia adds Asian Russia east of 40°E. The Chukotka fragment west of
        // the antimeridian is not on this side of the map.
        // Oceania is the contiguous positive-longitude cluster (Australia through
        // Fiji and Guam). Samoa, the Cook Islands, Niue, eastern Kiribati and
        // the Chatham Islands sit past the antimeridian and are outside this frame.
        // South America omits the Galápagos and Easter Island. Africa omits
        // sub-antarctic specks south of 36°S.
        public const double EuropeMinLatitude = 34.934473;
        public const double EuropeMaxLatitude = 81.854199;
        public const double EuropeMinLongitude = -31.282959;
        public const double EuropeMaxLongitude = 59.982324;
        public const double AsiaMinLatitude = -12.199805;
        public const double AsiaMaxLatitude = 81.854199;
        public const double AsiaMinLongitude = 25.668945;
        public const double AsiaMaxLongitude = 180d;
        public const double AfricaMinLatitude = -34.785742;
        public const double AfricaMaxLatitude = 37.340381;
        public const double AfricaMinLongitude = -25.341553;
        public const double AfricaMaxLongitude = 51.390234;
        public const double NorthAmericaMinLatitude = 7.220068;
        public const double NorthAmericaMaxLatitude = 83.599609;
        public const double NorthAmericaMinLongitude = -171.819385;
        public const double NorthAmericaMaxLongitude = -11.425537;
        public const double SouthAmericaMinLatitude = -55.891699;
        public const double SouthAmericaMaxLatitude = 12.434375;
        public const double SouthAmericaMinLongitude = -81.336621;
        public const double SouthAmericaMaxLongitude = -34.805469;
        public const double OceaniaMinLatitude = -47.263672;
        public const double OceaniaMaxLatitude = 18.806787;
        public const double OceaniaMinLongitude = 112.908203;
        public const double OceaniaMaxLongitude = 180d;

        /// <summary>
        /// Initial Oceania window from Natural Earth 50m coastlines.
        /// North is Cape York (-10.052°, 142.149°E). West is Australia's west
        /// coast. East is the North Island east coast (178.536°E). South is the
        /// South Island south coast (-46.798°, 167.765°E). Stewart Island and
        /// New Guinea stay in the country geometry and outside this window.
        /// </summary>
        public const double OceaniaFocusMinLatitude = -46.797656d;
        public const double OceaniaFocusMaxLatitude = -10.051758d;
        public const double OceaniaFocusMinLongitude = 112.908203d;
        public const double OceaniaFocusMaxLongitude = 178.53623d;

        /// <summary>
        /// Initial North America window from Natural Earth 50m.
        /// West is the Alaska mainland at 167.987°W, east of the Aleutian chain.
        /// North is the Alaska north coast at 71.408°N. South is Mexico at
        /// 14.545°N. East is the continental United States at 66.987°W.
        /// Aleutians west of 168°W and northern Greenland stay in the data.
        /// </summary>
        public const double NorthAmericaFocusMinLatitude = 14.54541d;
        public const double NorthAmericaFocusMaxLatitude = 71.407666d;
        public const double NorthAmericaFocusMinLongitude = -167.987256d;
        public const double NorthAmericaFocusMaxLongitude = -66.987012d;

        /// <summary>
        /// Initial Asia camera window. Full AsiaCoverage includes arctic Russia to
        /// 81.85°N; on a wide phone that height-driven fit spills west through the
        /// Atlantic into Canada and Greenland. This window keeps Türkiye through
        /// Japan and Indonesia dominant while arctic Russia and Chukotka stay in
        /// the country geometry outside the first frame.
        /// </summary>
        // North is capped near 42°N so phone aspect is width-driven. A taller
        // window (arctic / Kazakhstan) forces a height fit that spills west into
        // Africa and Europe. Jakarta (−6.2°) stays inside the south edge.
        public const double AsiaFocusMinLatitude = -8d;
        public const double AsiaFocusMaxLatitude = 42d;
        // West of ~30°E still shows East Africa in the phone hole; keep Ankara (32.9°E).
        public const double AsiaFocusMinLongitude = 30d;
        public const double AsiaFocusMaxLongitude = 145.817383d;

        /// <summary>
        /// South edge of the default World camera. Cape Horn is near 56°S.
        /// The Antarctic Peninsula begins near 63°S. Full coverage stays in World().
        /// </summary>
        public const double WorldPresentationMinLatitude = -58d;

        /// <summary>
        /// Closest orthographic size, as a fraction of the Türkiye frame.
        /// 0.46 is a regional city cluster. It stops zoom short of street scale,
        /// where the 50m relief would only blur.
        /// </summary>
        public const float ClosestRelativeToTurkeyFrame = 0.46f;

        /// <summary>
        /// Full Web Mercator span, about ±85°. Mesh generation and coverage use this.
        /// The default World camera uses WorldPresentation().
        /// </summary>
        public static void World(out Vector2 min, out Vector2 max)
        {
            ProjectedBounds(-MapProjection.MaxLatitude, MapProjection.MaxLatitude, -180d, 180d, out min, out max);
        }

        /// <summary>
        /// Default World camera. Longitude stays -180..180. The south is cut at
        /// WorldPresentationMinLatitude so the polar ice is outside the frame.
        /// </summary>
        public static void WorldPresentation(out Vector2 min, out Vector2 max)
        {
            ProjectedBounds(WorldPresentationMinLatitude, MapProjection.MaxLatitude, -180d, 180d, out min, out max);
        }

        public static void Turkey(out Vector2 min, out Vector2 max)
        {
            ProjectedBounds(TurkeyMinLatitude, TurkeyMaxLatitude, TurkeyMinLongitude, TurkeyMaxLongitude, out min, out max);
        }

        public static bool TryGeographicBounds(
            MapScope scope,
            out double minLatitude,
            out double maxLatitude,
            out double minLongitude,
            out double maxLongitude)
        {
            switch (scope)
            {
                case MapScope.Europe:
                    minLatitude = EuropeMinLatitude;
                    maxLatitude = EuropeMaxLatitude;
                    minLongitude = EuropeMinLongitude;
                    maxLongitude = EuropeMaxLongitude;
                    return true;
                case MapScope.Asia:
                    minLatitude = AsiaMinLatitude;
                    maxLatitude = AsiaMaxLatitude;
                    minLongitude = AsiaMinLongitude;
                    maxLongitude = AsiaMaxLongitude;
                    return true;
                case MapScope.Africa:
                    minLatitude = AfricaMinLatitude;
                    maxLatitude = AfricaMaxLatitude;
                    minLongitude = AfricaMinLongitude;
                    maxLongitude = AfricaMaxLongitude;
                    return true;
                case MapScope.NorthAmerica:
                    minLatitude = NorthAmericaMinLatitude;
                    maxLatitude = NorthAmericaMaxLatitude;
                    minLongitude = NorthAmericaMinLongitude;
                    maxLongitude = NorthAmericaMaxLongitude;
                    return true;
                case MapScope.SouthAmerica:
                    minLatitude = SouthAmericaMinLatitude;
                    maxLatitude = SouthAmericaMaxLatitude;
                    minLongitude = SouthAmericaMinLongitude;
                    maxLongitude = SouthAmericaMaxLongitude;
                    return true;
                case MapScope.Oceania:
                    minLatitude = OceaniaMinLatitude;
                    maxLatitude = OceaniaMaxLatitude;
                    minLongitude = OceaniaMinLongitude;
                    maxLongitude = OceaniaMaxLongitude;
                    return true;
                default:
                    minLatitude = 0d;
                    maxLatitude = 0d;
                    minLongitude = 0d;
                    maxLongitude = 0d;
                    return false;
            }
        }

        public enum RegionFrameMode
        {
            Coverage = 0,
            Contain = 1,
            Width = 2,
            ClosedCoverage = 3
        }

        /// <summary>
        /// Bounds and fit mode for the initial camera. Geographic extents stay
        /// on <see cref="TryProjected"/>. South America uses that extent.
        /// Oceania and North America use the mainland windows above.
        /// All three are contained in the closed hole. Chrome-covered
        /// overscroll is applied by the camera rig, not by these bounds.
        /// </summary>
        public static bool TryFrame(MapScope scope, out Vector2 min, out Vector2 max, out RegionFrameMode mode)
        {
            if (scope == MapScope.Oceania)
            {
                ProjectedBounds(
                    OceaniaFocusMinLatitude,
                    OceaniaFocusMaxLatitude,
                    OceaniaFocusMinLongitude,
                    OceaniaFocusMaxLongitude,
                    out min,
                    out max);
                mode = RegionFrameMode.Contain;
                return true;
            }

            if (scope == MapScope.Asia)
            {
                ProjectedBounds(
                    AsiaFocusMinLatitude,
                    AsiaFocusMaxLatitude,
                    AsiaFocusMinLongitude,
                    AsiaFocusMaxLongitude,
                    out min,
                    out max);
                mode = RegionFrameMode.Contain;
                return true;
            }

            if (scope == MapScope.NorthAmerica)
            {
                ProjectedBounds(
                    NorthAmericaFocusMinLatitude,
                    NorthAmericaFocusMaxLatitude,
                    NorthAmericaFocusMinLongitude,
                    NorthAmericaFocusMaxLongitude,
                    out min,
                    out max);
                mode = RegionFrameMode.Contain;
                return true;
            }

            if (scope == MapScope.SouthAmerica && TryProjected(scope, out min, out max))
            {
                mode = RegionFrameMode.ClosedCoverage;
                return true;
            }

            mode = RegionFrameMode.Coverage;
            return TryProjected(scope, out min, out max);
        }

        public static bool TryProjected(MapScope scope, out Vector2 min, out Vector2 max)
        {
            if (!TryGeographicBounds(scope, out var minLatitude, out var maxLatitude, out var minLongitude, out var maxLongitude))
            {
                min = Vector2.zero;
                max = Vector2.zero;
                return false;
            }

            ProjectedBounds(minLatitude, maxLatitude, minLongitude, maxLongitude, out min, out max);
            return true;
        }

        public static float MinimumOrthographicSize(float aspect)
        {
            Turkey(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, TurkeyFocusPadding, out _, out var turkeyFrame);
            var closest = turkeyFrame * ClosestRelativeToTurkeyFrame;
            if (closest < 0.0001f)
                closest = 0.0001f;
            return closest;
        }

        public static float MaximumOrthographicSize(float aspect)
        {
            WorldPresentation(out var min, out var max);
            MapViewMath.Fit(min, max, aspect, WorldFocusPadding, out _, out var size);
            return size;
        }

        public static void ProjectedBounds(
            double minLatitude,
            double maxLatitude,
            double minLongitude,
            double maxLongitude,
            out Vector2 min,
            out Vector2 max)
        {
            if (!MapProjection.TryProjectUnwrapped(minLatitude, minLongitude, out var southwest) ||
                !MapProjection.TryProjectUnwrapped(maxLatitude, maxLongitude, out var northeast))
            {
                southwest = Vector2.zero;
                northeast = Vector2.zero;
            }

            min = southwest;
            max = northeast;
        }
    }
}
