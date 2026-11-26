using System;
using Firebase.Auth;
using LogistiCore.Backend;
using UnityEngine;

namespace LogistiCore.Map
{
    /// <summary>
    /// One read per map entry, only for a signed-in user in the approved local
    /// Firebase emulator. After an authoritative HQ read, focus the existing
    /// world-map camera. Manual preview, touch, mouse, and UI region changes
    /// always take priority over a late response.
    ///
    /// No Firestore writes, company creation, routes, facility ownership or
    /// production Firebase connection occur here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MapCameraRig))]
    public sealed class MapCompanyHeadquartersFocus : MonoBehaviour
    {
        MapTradeRegionMarkers _overlay;
        MapCameraRig _rig;
        Camera _camera;
        ClientReadSession _reader;
        bool _started;
        bool _requested;
        bool _completed;
        bool _suppressed;
        bool _subscribed;
        int _generation;
        string _requestedUid = "";
        MapHeadquartersFocusState _state = MapHeadquartersFocusState.AwaitingVerifiedAccount;

        public MapHeadquartersFocusState State => _state;
        public bool HasRequestedRead => _requested;
        public event Action<MapHeadquartersFocusState> StateChanged;

        public void Bind(MapTradeRegionMarkers overlay, Camera mapCamera)
        {
            _overlay = overlay;
            _camera = mapCamera;
            var rig = mapCamera != null ? mapCamera.GetComponent<MapCameraRig>() : null;
            if (_rig != rig)
                Unsubscribe();
            _rig = rig;
            Subscribe();
        }

        void Start()
        {
            _started = true;
        }

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            _generation++;
            Unsubscribe();
        }

        void OnDestroy()
        {
            _generation++;
            Unsubscribe();
            _reader = null;
            StateChanged = null;
        }

        void Subscribe()
        {
            if (_subscribed || _rig == null)
                return;
            _rig.ScopeChanged += OnScopeChanged;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || _rig == null)
                return;
            _rig.ScopeChanged -= OnScopeChanged;
            _subscribed = false;
        }

        void OnScopeChanged()
        {
            // Map rig, marker overlay and scene chrome may initialize in any
            // OnEnable order. Initialization is not a user navigation action.
            if (!_started || _completed || _suppressed)
                return;
            if (_overlay != null && _overlay.ManualPreviewApplied)
            {
                _suppressed = true;
                _completed = true;
                ChangeState(MapHeadquartersFocusState.ManualPreview);
                return;
            }
            SuppressNavigation();
        }

        public void SuppressNavigation()
        {
            if (_completed || _suppressed)
                return;
            _generation++;
            _suppressed = true;
            _requestedUid = "";
            ChangeState(MapHeadquartersFocusState.UserNavigated);
        }

        void Update()
        {
            if (!_started || _completed || _suppressed || _requested || _rig == null ||
                _camera == null || _overlay == null)
                return;

            // MapTradeRegionMarkers.Start() consumes the one-shot company-scene
            // preview before any Update() runs. Never override that explicit tap.
            if (_overlay.ManualPreviewApplied || MapHeadquartersPreviewSelection.HasPending)
            {
                _suppressed = true;
                _completed = true;
                ChangeState(MapHeadquartersFocusState.ManualPreview);
                return;
            }

            // Also catches on-screen zoom controls, which may bypass gestures.
            if (_rig.UserAdjusted)
            {
                SuppressNavigation();
                return;
            }

            var status = FirebaseClient.Current;
            if (!FirebaseClient.SdkAppReady ||
                !ClientLocalEmulatorSecurity.AllowsLiveReads(ClientJobsRuntime.Settings, status))
                return;

            var uid = CurrentUid();
            if (string.IsNullOrEmpty(uid))
                return;

            _requestedUid = uid;
            _requested = true;
            var generation = ++_generation;
            ChangeState(MapHeadquartersFocusState.Fetching);

            _reader = new ClientReadSession();
            _reader.SetAsyncInvoker(ClientFunctionsReadInvoker.Invoke);
            _reader.ReadAsync(ClientCallableCatalog.GetMyCompanyHeadquarters,
                status, true, ClientValue.ReadRequest(), outcome =>
                {
                    if (this == null || generation != _generation ||
                        _suppressed || _completed)
                        return;
                    _completed = true;

                    // An Auth switch while the network call was in flight
                    // must never position the camera for the previous account.
                    if (CurrentUid() != _requestedUid ||
                        _overlay == null || _overlay.ManualPreviewApplied ||
                        _rig == null || _rig.UserAdjusted)
                    {
                        ChangeState(MapHeadquartersFocusState.UserNavigated);
                        return;
                    }

                    if (!outcome.Ok ||
                        !ClientReadParser.TryCompanyHeadquarters(
                            outcome.Envelope.Result, out var account))
                    {
                        ChangeState(MapHeadquartersFocusState.Unavailable);
                        return;
                    }

                    var decision = MapCompanyHeadquartersFocusPolicy.Resolve(
                        account, out var city, out var world);
                    if (decision != MapHeadquartersFocusState.Focused)
                    {
                        // Existing companies without a sidecar remain on the
                        // unchanged initial Turkey-Europe map. No data mutation.
                        ChangeState(decision);
                        return;
                    }

                    // Mark complete before changing the camera scope: ScopeChanged
                    // is also raised by deliberate programmatic focus.
                    ChangeState(MapHeadquartersFocusState.Focused);
                    _rig.ApplyImmediate(city.Location.CountryCode == "TUR"
                        ? MapScope.Turkey : MapScope.Europe);
                    _rig.FocusWorldPoint(world);
                    _rig.ZoomAtScreen(
                        new Vector2(Screen.width * .5f, Screen.height * .5f),
                        city.Location.CountryCode == "TUR" ? 1.35f : 1.85f);

                    var legacy = _overlay.GetComponent<MapMarkerLayer>();
                    if (legacy != null)
                        legacy.ClearSelection();
                    var inspector = FindAnyObjectByType<MapCommandShell>();
                    if (inspector != null)
                    {
                        if (city.Location.CountryCode == "TUR" &&
                            MapCities.TryGet(city.Location.CityKey, out _))
                            inspector.ShowCity(city.Location.CityKey);
                        else
                            inspector.ShowGlobalCity(city.Location);
                    }
                });
        }

        static string CurrentUid()
        {
            if (!FirebaseClient.SdkAppReady)
                return "";
            try
            {
                var auth = FirebaseAuth.DefaultInstance;
                return auth != null && auth.CurrentUser != null
                    ? auth.CurrentUser.UserId : "";
            }
            catch
            {
                return "";
            }
        }

        void ChangeState(MapHeadquartersFocusState next)
        {
            _state = next;
            StateChanged?.Invoke(next);
        }
    }
}
