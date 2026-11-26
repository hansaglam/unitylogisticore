using System;
using Firebase.Auth;
using LogistiCore.Backend;
using LogistiCore.Map;
using LogistiCore.UI;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace LogistiCore.Depot
{
    /// <summary>
    /// Non-shipping preview screen. Every owned facility visual comes from a
    /// verified local-emulator read. Inspection buttons show real prerequisites;
    /// they do not create jobs, pretend to load cargo or grant a warehouse.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DepotYardBlockoutView))]
    public sealed class DepotPrototypeController : MonoBehaviour
    {
        enum InspectTab : byte { Parcel, Storage, Dispatch }

        DepotYardBlockoutView _yard;
        ClientStarterDepotProgressState _company;
        DepotVisualStage _stage;
        Text _title;
        Text _subtitle;
        Text _status;
        Text _note;
        Button _return;
        Text _returnLabel;
        Button[] _tabs = new Button[3];
        Text[] _tabLabels = new Text[3];
        InspectTab _selected;
        bool _verified;
        bool _error;
        bool _requested;
        string _uid = "";
        int _generation;
        bool _built;
        Font _font;

        public DepotVisualStage CurrentStage => _stage;
        public bool HasVerifiedData => _verified;

        void Awake()
        {
            if (!Application.isPlaying) return;
            ScreenLayouts.Apply(ScreenLayoutMode.Landscape);
            _yard = GetComponent<DepotYardBlockoutView>();
            _yard.SetStage(DepotVisualStage.Unverified);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUi();
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        void OnDisable()
        {
            if (!Application.isPlaying) return;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            ResetReadState();
        }

        /// <summary>
        /// Invalidate authenticated data before a subsequent account/scene read.
        /// This must clear the failure latch too, otherwise a failed request
        /// permanently blocks the next signed-in player's depot read.
        /// </summary>
        void ResetReadState()
        {
            _generation++;
            _requested = false;
            _verified = false;
            _error = false;
            _company = default;
            _stage = DepotVisualStage.Unverified;
            _uid = "";
            if (_yard != null)
                _yard.SetStage(_stage);
            Refresh();
        }

        void Update()
        {
            if (!_built)
                return;
            // A sign-out or account change must also clear any previous
            // error latch, cached warehouse and late response generation.
            if (!string.IsNullOrEmpty(_uid) && CurrentUid() != _uid)
                ResetReadState();
            if (_requested || _verified || _error ||
                !FirebaseClient.SdkAppReady ||
                !ClientLocalEmulatorSecurity.AllowsLiveReads(
                    ClientJobsRuntime.Settings, FirebaseClient.Current))
                return;

            var uid = CurrentUid();
            if (string.IsNullOrEmpty(uid)) return;
            _uid = uid;
            _requested = true;
            var generation = ++_generation;
            Refresh();

            var reader = new ClientReadSession();
            reader.SetAsyncInvoker(ClientFunctionsReadInvoker.Invoke);
            reader.ReadAsync(ClientCallableCatalog.GetMyStarterDepotProgress,
                FirebaseClient.Current, true, ClientValue.ReadRequest(), result =>
                {
                    if (this == null || generation != _generation || CurrentUid() != uid)
                        return;
                    _requested = false;
                    if (result.Ok && ClientReadParser.TryStarterDepotProgress(
                        result.Envelope.Result, out var state))
                    {
                        _company = state;
                        _verified = true;
                        _error = false;
                        _stage = DepotVisualStagePolicy.Resolve(true, state);
                        _yard.SetStage(_stage);
                    }
                    else
                    {
                        _error = true;
                        _verified = false;
                        _company = default;
                        _stage = DepotVisualStage.Unverified;
                        _yard.SetStage(_stage);
                    }
                    Refresh();
                });
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

        void BuildUi()
        {
            if (_built) return;
            _built = true;

            var canvasObject = new GameObject("DepotPrototypeCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(canvasObject.transform, false);
            var safeRect = safe.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
            // Add after parenting: SafeAreaRect.OnEnable can now calculate
            // the correct anchor inset on its first pass.
            safe.AddComponent<SafeAreaRect>();

            var top = Surface(safe.transform, "DepotHeader", 0f, 1f, 1f, 1f, 146f);
            _title = Text(top, "DepotTitle", 44, FontStyle.Bold,
                new Vector2(0.02f, 0.46f), new Vector2(0.58f, 0.95f));
            _subtitle = Text(top, "DepotSubtitle", 26, FontStyle.Normal,
                new Vector2(0.02f, 0.06f), new Vector2(0.78f, 0.42f));
            _return = Button(top, "ReturnToMap",
                new Vector2(0.81f, 0.22f), new Vector2(0.98f, 0.80f),
                out _returnLabel);
            _return.onClick.AddListener(AppNavigation.ShowMap);

            var bottom = Surface(safe.transform, "DepotOperations", 0f, 0f, 1f, 0f, 208f);
            _status = Text(bottom, "AuthenticatedDepotState", 34, FontStyle.Bold,
                new Vector2(0.02f, 0.48f), new Vector2(0.53f, 0.91f));
            _note = Text(bottom, "AuthenticatedDepotDetail", 27, FontStyle.Normal,
                new Vector2(0.02f, 0.05f), new Vector2(0.53f, 0.48f));
            _note.horizontalOverflow = HorizontalWrapMode.Wrap;
            _note.verticalOverflow = VerticalWrapMode.Overflow;

            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var x0 = 0.56f + i * 0.14f;
                _tabs[i] = Button(bottom, "Inspect_" + (InspectTab)i,
                    new Vector2(x0, 0.20f), new Vector2(x0 + 0.13f, 0.80f),
                    out _tabLabels[i]);
                _tabs[i].onClick.AddListener(() => SelectTab((InspectTab)index));
            }
            _selected = InspectTab.Parcel;
            Refresh();
        }

        RectTransform Surface(Transform parent, string name,
            float minX, float minY, float maxX, float maxY, float height)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.pivot = new Vector2(0.5f, minY > 0.5f ? 1f : 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            var img = go.GetComponent<Image>();
            img.color = MapPremiumTheme.Panel;
            img.raycastTarget = false;
            return rect;
        }

        Text Text(Transform parent, string name, int fontSize, FontStyle fontStyle,
            Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = _font; text.fontStyle = fontStyle; text.fontSize = fontSize;
            text.color = MapPremiumTheme.TextPrimary;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        Button Button(Transform parent, string name, Vector2 min, Vector2 max,
            out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var face = go.GetComponent<Image>();
            face.color = MapPremiumTheme.Button;
            face.raycastTarget = true;
            var button = go.GetComponent<Button>();
            button.targetGraphic = face;
            label = Text(go.transform, "Label", 27, FontStyle.Bold,
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return button;
        }

        void SelectTab(InspectTab tab)
        {
            _selected = tab;
            Refresh();
        }

        string Language
        {
            get
            {
                var locale = LocalizationSettings.SelectedLocale;
                var code = locale != null ? locale.Identifier.Code : "en";
                if (code.StartsWith("tr", StringComparison.OrdinalIgnoreCase)) return "tr";
                if (code.StartsWith("de", StringComparison.OrdinalIgnoreCase)) return "de";
                return "en";
            }
        }
        string Local(string en, string tr, string de) =>
            Language == "tr" ? tr : Language == "de" ? de : en;

        void OnLocaleChanged(Locale _) => Refresh();

        void Refresh()
        {
            if (!_built) return;
            _title.text = Local("LOGISTICS YARD / PROTOTYPE",
                "LOJİSTİK TESİSİ / PROTOTİP", "LOGISTIKHOF / PROTOTYP");
            _subtitle.text = Local("Authored 2.5D blockout — not final artwork",
                "Tasarlanmış 2.5D bloklama — nihai görsel değil",
                "Gestalteter 2,5D-Blockout — keine endgültige Grafik");
            _returnLabel.text = Local("Map", "Harita", "Karte");
            _tabLabels[0].text = Local("Parcel", "Parsel", "Grundstück");
            _tabLabels[1].text = Local("Storage", "Depo", "Lager");
            _tabLabels[2].text = Local("Dispatch", "Sevkiyat", "Versand");

            var label = "";
            if (!_verified)
                label = _error ? Local("Data unavailable", "Veri alınamadı", "Daten fehlen")
                    : Local("Not verified", "Doğrulanmadı", "Nicht bestätigt");
            else if (_stage == DepotVisualStage.StorageOperational)
                label = Local("Storage operational", "Depo faal", "Lager in Betrieb");
            else if (_stage == DepotVisualStage.ActivationReady)
                label = Local("Activation ready", "Aktivasyon hazır", "Aktivierung bereit");
            else if (_stage == DepotVisualStage.ReservedParcel)
                label = Local("Site reserved", "Depo yeri ayrıldı", "Depotplatz reserviert");
            else label = Local("No assigned site", "Tesis atanmamış", "Kein Standort zugewiesen");
            _status.text = label;

            switch (_selected)
            {
                case InspectTab.Storage:
                    _note.text = _stage == DepotVisualStage.StorageOperational
                        ? Local("24 pallet slots verified; no inventory shown.",
                            "24 paletlik kapasite doğrulandı; stok gösterilmiyor.",
                            "24 Palettenplätze bestätigt; kein Bestand dargestellt.")
                        : Local("No operational building is authorized yet.",
                            "Henüz faal bir depo binası doğrulanmadı.",
                            "Noch kein betriebsfähiges Lager bestätigt.");
                    break;
                case InspectTab.Dispatch:
                    _note.text = Local(
                        "No authorized road freight or vehicle movement yet.",
                        "Henüz onaylı sevkiyat rotası veya araç hareketi yok.",
                        "Noch keine freigegebene Route oder Fahrzeugbewegung.");
                    break;
                default:
                    _note.text = _stage == DepotVisualStage.Unverified
                        ? Local("No verified company-owned land has been loaded.",
                            "Şirkete ait doğrulanmış parsel kaydı yüklenmedi.",
                            "Kein bestätigter firmeneigener Standort geladen.")
                        : Local("A verified headquarters site; no fake cargo or traffic.",
                            "Doğrulanmış merkez parseli; sahte yük veya trafik yok.",
                            "Bestätigter Firmensitz; keine erfundene Fracht.");
                    break;
            }

            for (var i = 0; i < _tabs.Length; i++)
            {
                var face = _tabs[i].GetComponent<Image>();
                if (face != null)
                    face.color = i == (int)_selected
                        ? MapPremiumTheme.Selected : MapPremiumTheme.Button;
            }
        }
    }
}
