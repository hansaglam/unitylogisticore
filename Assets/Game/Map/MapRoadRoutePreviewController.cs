using System;
using Firebase.Auth;
using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Map
{
    public enum MapRoadPreviewState : byte
    {
        Idle,
        CheckingHeadquarters,
        LoadingRoad,
        Visible,
        NoRoad,
        NoHeadquarters,
        Unavailable
    }

    /// <summary>
    /// Explicit map UI action only. First reads trusted company HQ, then asks
    /// the server for a provider route from that HQ to a selected city. Neither
    /// request is payable or changes game state. No fake fallback geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapRoadRoutePreviewController : MonoBehaviour
    {
        Camera _camera;
        MapRoadRoutePreviewLine _line;
        ClientReadSession _reads;
        int _generation;
        string _uid;
        MapBusinessLocation _destination;
        MapRoadPreviewState _state;

        public MapRoadPreviewState State => _state;
        public bool IsBusy => _state == MapRoadPreviewState.CheckingHeadquarters ||
            _state == MapRoadPreviewState.LoadingRoad;
        public event Action StateChanged;

        public static MapRoadRoutePreviewController Ensure(MapMarkerLayer markers, Camera camera)
        {
            if (!Application.isPlaying || markers == null || camera == null)
                return null;
            var controller = markers.GetComponent<MapRoadRoutePreviewController>();
            if (controller == null)
                controller = markers.gameObject.AddComponent<MapRoadRoutePreviewController>();
            controller.Bind(camera, MapRoadRoutePreviewLine.Ensure(markers, camera));
            return controller;
        }

        public void Bind(Camera camera, MapRoadRoutePreviewLine line)
        {
            _camera = camera;
            _line = line;
        }

        public void Clear()
        {
            _generation++;
            _destination = default;
            _uid = "";
            _line?.Clear();
            Change(MapRoadPreviewState.Idle);
        }

        void OnDisable() => Clear();

        public bool Request(MapBusinessLocation destination)
        {
            Clear();
            if (!MapTradeRegionCatalog.Includes(destination) ||
                _camera == null || _line == null ||
                !FirebaseClient.SdkAppReady ||
                !ClientLocalEmulatorSecurity.AllowsLiveReads(
                    ClientJobsRuntime.Settings, FirebaseClient.Current))
            {
                Change(MapRoadPreviewState.Unavailable);
                return false;
            }

            var uid = CurrentUid();
            if (string.IsNullOrEmpty(uid))
            {
                Change(MapRoadPreviewState.Unavailable);
                return false;
            }

            _destination = destination;
            _uid = uid;
            var generation = ++_generation;
            Change(MapRoadPreviewState.CheckingHeadquarters);
            _reads = new ClientReadSession();
            _reads.SetAsyncInvoker(ClientFunctionsReadInvoker.Invoke);
            _reads.ReadAsync(ClientCallableCatalog.GetMyCompanyHeadquarters,
                FirebaseClient.Current, true, ClientValue.ReadRequest(), response =>
                {
                    if (!Valid(generation)) return;
                    if (!response.Ok ||
                        !ClientReadParser.TryCompanyHeadquarters(response.Envelope.Result, out var hq))
                    {
                        Change(MapRoadPreviewState.Unavailable);
                        return;
                    }
                    if (!hq.HasCompany || !hq.HasHeadquarters)
                    {
                        Change(MapRoadPreviewState.NoHeadquarters);
                        return;
                    }
                    var origin = new MapBusinessLocation(hq.CountryCode, hq.CityKey);
                    var payload = MapRoadRoutePreviewParser.Request(origin, _destination);
                    if (payload == null)
                    {
                        Change(MapRoadPreviewState.NoRoad);
                        return;
                    }

                    Change(MapRoadPreviewState.LoadingRoad);
                    _reads.ReadAsync(ClientCallableCatalog.GetMyRoadRoutePreview,
                        FirebaseClient.Current, true, payload, routeResponse =>
                        {
                            if (!Valid(generation)) return;
                            if (!routeResponse.Ok ||
                                !MapRoadRoutePreviewParser.TryParse(
                                    routeResponse.Envelope.Result, out var route, out var noRoad))
                            {
                                Change(MapRoadPreviewState.Unavailable);
                                return;
                            }
                            if (noRoad)
                            {
                                Change(MapRoadPreviewState.NoRoad);
                                return;
                            }
                            if (!origin.Equals(route.Origin) ||
                                !_destination.Equals(route.Destination) ||
                                !_line.Show(route))
                            {
                                Change(MapRoadPreviewState.Unavailable);
                                return;
                            }
                            Change(MapRoadPreviewState.Visible);
                        });
                });
            return true;
        }

        bool Valid(int generation)
        {
            return this != null && isActiveAndEnabled &&
                _generation == generation && !string.IsNullOrEmpty(_uid) &&
                _uid == CurrentUid();
        }

        static string CurrentUid()
        {
            try
            {
                if (!FirebaseClient.SdkAppReady) return "";
                var auth = FirebaseAuth.DefaultInstance;
                return auth?.CurrentUser != null ? auth.CurrentUser.UserId : "";
            }
            catch { return ""; }
        }

        void Change(MapRoadPreviewState state)
        {
            _state = state;
            StateChanged?.Invoke();
        }
    }
}
