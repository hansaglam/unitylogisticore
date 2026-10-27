using UnityEngine;

namespace LogistiCore.Map
{
    public enum MapRibbonRole
    {
        Coast = 0,
        Border = 1,
        Turkey = 2
    }

    /// <summary>
    /// Screen-pixel widths for the premium coast and border ribbons.
    /// The geographic centerline does not depend on these values.
    /// </summary>
    public static class MapRibbonStyle
    {
        public const float CoastWidthPixels = 1.5f;
        public const float BorderWidthPixels = 1.15f;
        public const float TurkeyWidthPixels = 2f;
        public const float SoftnessPixels = 0.65f;
        public const float SmallRingWorld = 0.35f;
        public const float DetailOrtho = 40f;

        /// <summary>
        /// Borders are fully visible at and below this orthographic size.
        /// Türkiye framing is about 12. World framing is about 224.
        /// </summary>
        public const float BorderFadeNear = 28f;

        /// <summary>
        /// Borders reach their quiet floor at and above this orthographic size.
        /// </summary>
        public const float BorderFadeFar = 110f;

        public const float BorderFadeFloor = 0.16f;

        public static readonly Color32 Coast = new Color32(118, 150, 164, 200);
        public static readonly Color32 Border = new Color32(86, 112, 126, 125);
        public static readonly Color32 Turkey = new Color32(104, 178, 196, 215);

        /// <summary>
        /// Multiplies the baked Türkiye vertex color before it is linearized.
        /// Coastlines and borders do not use it. Width and geometry stay as baked.
        /// </summary>
        public static readonly Vector4 TurkeyPresentation = new Vector4(0.82f, 0.74f, 0.76f, 0.70f);

        public static float Width(MapRibbonRole role)
        {
            if (role == MapRibbonRole.Turkey)
                return TurkeyWidthPixels;
            if (role == MapRibbonRole.Border)
                return BorderWidthPixels;
            return CoastWidthPixels;
        }

        public static Color32 Color(MapRibbonRole role)
        {
            if (role == MapRibbonRole.Turkey)
                return Turkey;
            if (role == MapRibbonRole.Border)
                return Border;
            return Coast;
        }

        /// <summary>
        /// Presentation-only fade for rings smaller than SmallRingWorld.
        /// Extent 0 is a border or the Türkiye outline and is never faded by this.
        /// </summary>
        public static float SmallRingAlpha(float ringExtentWorld, float orthographicSize)
        {
            if (ringExtentWorld <= 0f || ringExtentWorld >= SmallRingWorld)
                return 1f;
            if (orthographicSize <= 0f || DetailOrtho <= 0.001f)
                return 1f;

            var fade = (2f * DetailOrtho - orthographicSize) / DetailOrtho;
            if (fade < 0f)
                return 0f;
            if (fade > 1f)
                return 1f;
            return fade;
        }

        /// <summary>
        /// Multiplier for international borders from the camera size alone.
        /// Major coastlines and the Türkiye outline do not use this.
        /// </summary>
        public static float BorderFade(float orthographicSize)
        {
            if (orthographicSize <= BorderFadeNear)
                return 1f;
            var span = BorderFadeFar - BorderFadeNear;
            if (span <= 0.001f)
                return BorderFadeFloor;

            var t = (orthographicSize - BorderFadeNear) / span;
            if (t > 1f)
                t = 1f;
            return 1f + (BorderFadeFloor - 1f) * t;
        }

        public static Vector2 NdcOffsetFromPixels(Vector2 pixelOffset, float screenWidth, float screenHeight)
        {
            return new Vector2(pixelOffset.x / screenWidth * 2f, pixelOffset.y / screenHeight * 2f);
        }

        public static Vector2 PixelsFromNdc(Vector2 ndcOffset, float screenWidth, float screenHeight)
        {
            return new Vector2(ndcOffset.x * screenWidth * 0.5f, ndcOffset.y * screenHeight * 0.5f);
        }

        public static float ReachPixels(MapRibbonRole role)
        {
            return Width(role) * 0.5f + SoftnessPixels;
        }
    }
}
