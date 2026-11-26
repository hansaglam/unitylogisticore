using System;
using LogistiCore.Backend;
using LogistiCore.Map;
using LogistiCore.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogistiCore.Depot
{
    /// <summary>
    /// Explicit, non-shipping navigation contract for an authenticated depot
    /// inspection. A valid map city alone does not prove company ownership.
    /// This does not authorize any delivery, storage mutation or payment.
    /// </summary>
    public static class DepotPrototypeNavigation
    {
        public const string SceneName = "DepotPrototype";
        public const string ScenePath = "Assets/Game/Scenes/DepotPrototype.unity";

        public static bool CanInspect(bool authenticatedLocalRead,
            bool verified, ClientStarterDepotProgressState progress,
            MapBusinessLocation requested, bool prototypeSceneInBuild)
        {
            if (!authenticatedLocalRead || !verified || !prototypeSceneInBuild ||
                !requested.IsValid || !MapGlobalCityCatalog.TryGet(requested, out _) ||
                !progress.HasCompany || !progress.HasHeadquarters ||
                !progress.HasStarterSite || progress.DispatchReady ||
                string.IsNullOrWhiteSpace(progress.CountryCode) ||
                string.IsNullOrWhiteSpace(progress.CityKey))
                return false;

            var headquarters = new MapBusinessLocation(
                progress.CountryCode, progress.CityKey);
            return headquarters.IsValid && headquarters.Equals(requested);
        }

        /// <summary>
        /// Must only be called after a fresh, signed-in emulator read of
        /// getMyStarterDepotProgress. No scene file or Build Profile is changed.
        /// SceneUtility is checked before invoking AppNavigation.Go.
        /// </summary>
        public static bool TryOpen(bool verified, ClientStarterDepotProgressState progress,
            MapBusinessLocation requested)
        {
            var allowed = ClientLocalEmulatorSecurity.AllowsLiveReads(
                ClientJobsRuntime.Settings, FirebaseClient.Current);
            var sceneIncluded = SceneUtility.GetBuildIndexByScenePath(ScenePath) >= 0;
            if (!CanInspect(allowed, verified, progress, requested, sceneIncluded))
                return false;

            AppNavigation.Go(new AppNavigation.Destination(
                SceneName, ScreenLayoutMode.Landscape));
            return true;
        }
    }
}
