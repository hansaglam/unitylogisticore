using UnityEngine;

namespace LogistiCore
{
    /// <summary>
    /// Applies the mobile frame-time target once. This is a target, not a measured result.
    /// vSync is cleared because a non-zero vSync count ignores Application.targetFrameRate.
    /// Phones ignore vSync and follow the target frame rate.
    /// </summary>
    public static class MobileFrameRate
    {
        public const int TargetFramesPerSecond = 60;

        public static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFramesPerSecond;
        }
    }
}
