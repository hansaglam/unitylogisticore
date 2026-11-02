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
    /// Home and later main tabs request portrait. The world map requests landscape.
    /// PlayerSettings already allow portrait and both landscapes. This does not write them.
    /// </summary>
    public static class ScreenLayouts
    {
        public static ScreenLayoutMode Requested { get; private set; }

        public static void Apply(ScreenLayoutMode mode)
        {
            Requested = mode;
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
