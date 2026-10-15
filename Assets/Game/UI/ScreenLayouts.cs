using System;
using UnityEngine;

namespace LogistiCore.UI
{
    public enum ScreenLayoutMode
    {
        Portrait = 0,
        Landscape = 1
    }

    /// <summary>
    /// Requests a layout without locking the project to one orientation.
    /// The world map should request landscape. Other main screens should request portrait.
    /// </summary>
    public static class ScreenLayouts
    {
        public static void Apply(ScreenLayoutMode mode)
        {
            Screen.autorotateToPortraitUpsideDown = false;

            switch (mode)
            {
                case ScreenLayoutMode.Portrait:
                    Screen.autorotateToPortrait = true;
                    Screen.autorotateToLandscapeLeft = false;
                    Screen.autorotateToLandscapeRight = false;
                    Screen.orientation = ScreenOrientation.Portrait;
                    break;

                case ScreenLayoutMode.Landscape:
                    Screen.autorotateToPortrait = false;
                    Screen.autorotateToLandscapeLeft = true;
                    Screen.autorotateToLandscapeRight = true;
                    Screen.orientation = ScreenOrientation.AutoRotation;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
    }
}
