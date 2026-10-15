using UnityEngine;

namespace LogistiCore
{
    static class RuntimeFoundation
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            MobileFrameRate.Apply();
            GameLog.Info("Runtime", "Target frame rate set to " + MobileFrameRate.TargetFramesPerSecond + ".");
        }
    }
}
