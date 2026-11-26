using System;
using Firebase.Auth;
using LogistiCore.Backend;
using LogistiCore.Map;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace LogistiCore.UI
{
    /// <summary>
    /// A functional, restrained two-level operations panel. No pseudo-3D
    /// warehouse artwork or fictional progress: every state is derived from
    /// authenticated emulator data. Storage and dispatch are visually distinct.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanyStarterSiteStatus : MonoBehaviour
    {
        Text _heading;
        Text _details;
        Text _buttonLabel;
        Button _button;
        bool _built;
        bool _requested;
        bool _verified;
        bool _failed;
        bool _uncertain;
        bool _activating;
        int _generation;
        string _requestUid = "";
        string _activationKey = "";
        ClientReadSession _readSession;
        ClientStarterDepotProgressState _state;

        public bool IsVerified => _verified;
        public bool HasStarterSite => _verified && _state.HasStarterSite;
        public bool WarehouseReady => _verified && _state.WarehouseReady;

        public void InvalidateForCompanyCreation()
        {
            _generation++;
            _requested = false;
            _verified = false;
            _failed = false;
            _uncertain = false;
            _activating = false;
            _requestUid = "";
            _activationKey = "";
            _readSession = null;
            _state = default;
            Refresh();
        }

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            Refresh();
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            InvalidateForCompanyCreation();
        }

        public void Build(Font font)
        {
            if (_built)
                return;
            _built = true;

            var layoutElement = GetComponent<LayoutElement>() ??
                gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 236f;
            layoutElement.flexibleWidth = 1f;

            var face = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            face.color = ShellPalette.NoticeFill;
            face.raycastTarget = false;

            var stack = gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(22, 22, 16, 16);
            stack.spacing = 8f;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            _heading = MakeText("DepotStatusHeading", font, 36, 45, FontStyle.Bold);
            _details = MakeText("DepotStatusDetails", font, 27, 78, FontStyle.Normal);
            _details.color = ShellPalette.TextSecondary;
            _details.horizontalOverflow = HorizontalWrapMode.Wrap;
            _details.verticalOverflow = VerticalWrapMode.Wrap;

            _button = ShellWidgets.ActionBar(transform, "ActivateStarterDepot", font,
                out _buttonLabel);
            _button.GetComponent<LayoutElement>().preferredHeight = 73f;
            _button.onClick.AddListener(OnActivate);
            Refresh();
        }

        Text MakeText(string name, Font font, int size, float height, FontStyle weight)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(transform, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = weight;
            text.color = ShellPalette.Text;
            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        void Update()
        {
            if (!_built)
                return;

            if (!string.IsNullOrEmpty(_requestUid) &&
                CurrentUid() != _requestUid)
            {
                InvalidateForCompanyCreation();
                return;
            }

            if (_requested || _verified || _failed || _uncertain || _activating)
                return;

            var connection = FirebaseClient.Current;
            if (!FirebaseClient.SdkAppReady ||
                !ClientLocalEmulatorSecurity.AllowsLiveReads(ClientJobsRuntime.Settings, connection))
                return;
            var uid = CurrentUid();
            if (string.IsNullOrEmpty(uid))
                return;

            _requestUid = uid;
            _requested = true;
            var generation = ++_generation;
            _readSession = new ClientReadSession();
            _readSession.SetAsyncInvoker(ClientFunctionsReadInvoker.Invoke);
            _readSession.ReadAsync(ClientCallableCatalog.GetMyStarterDepotProgress,
                connection, true, ClientValue.ReadRequest(), outcome =>
                {
                    if (this == null || generation != _generation)
                        return;
                    if (uid != CurrentUid())
                    {
                        InvalidateForCompanyCreation();
                        return;
                    }
                    _requested = false;
                    if (outcome.Ok && ClientReadParser.TryStarterDepotProgress(
                        outcome.Envelope.Result, out var result))
                    {
                        _state = result;
                        _verified = true;
                        _failed = false;
                    }
                    else
                    {
                        _failed = true;
                        _verified = false;
                    }
                    Refresh();
                });
        }

        static string CurrentUid()
        {
            try
            {
                if (!FirebaseClient.SdkAppReady)
                    return "";
                var auth = FirebaseAuth.DefaultInstance;
                return auth != null && auth.CurrentUser != null
                    ? auth.CurrentUser.UserId : "";
            }
            catch { return ""; }
        }

        void OnActivate()
        {
            if (_failed || _uncertain)
            {
                InvalidateForCompanyCreation();
                return;
            }

            if (!_verified || !_state.CanRequestActivation ||
                _activating || _requested ||
                !ClientStarterDepotActivationGateway.IsAuthorized(
                    ClientJobsRuntime.Settings, FirebaseClient.Current, _requestUid) ||
                CurrentUid() != _requestUid)
                return;

            _activating = true;
            var generation = ++_generation;
            var uid = _requestUid;
            if (string.IsNullOrEmpty(_activationKey))
                _activationKey = ClientValue.NewIdempotencyKey();
            Refresh();

            ClientStarterDepotActivationGateway.Submit(_activationKey, uid,
                ClientJobsRuntime.Settings, FirebaseClient.Current,
                () =>
                {
                    if (this == null || generation != _generation ||
                        uid != CurrentUid())
                        return;
                    // Read authoritative stored state instead of trusting the
                    // outgoing command or creating a fake visual building.
                    InvalidateForCompanyCreation();
                },
                _ =>
                {
                    if (this == null || generation != _generation)
                        return;
                    _activating = false;
                    _verified = false;
                    _uncertain = true;
                    Refresh();
                });
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
            if (_heading == null || _details == null || _button == null)
                return;

            _heading.text = Local("STARTER DEPOT", "BAŞLANGIÇ DEPOSU", "STARTDEPOT");
            if (_activating)
            {
                _details.text = Local("Registering verified storage capacity...",
                    "Doğrulanmış depo kapasitesi kaydediliyor...",
                    "Bestätigte Lagerkapazität wird registriert...");
            }
            else if (_uncertain)
            {
                _details.text = Local("Result unknown. Check the server before trying again.",
                    "İşlem sonucu belirsiz. Tekrar denemeden önce sunucuyu kontrol et.",
                    "Ergebnis unklar. Vor erneutem Versuch Serverstatus prüfen.");
            }
            else if (_failed)
            {
                _details.text = Local("Depot state could not be verified.",
                    "Depo durumu doğrulanamadı.", "Depotstatus konnte nicht geprüft werden.");
            }
            else if (!_verified)
            {
                _details.text = Local("Awaiting verified company data. Nothing has been unlocked.",
                    "Şirket verisi doğrulanıyor. Henüz hiçbir şey açılmadı.",
                    "Firmendaten werden geprüft. Noch nichts freigeschaltet.");
            }
            else
            {
                switch (_state.Stage)
                {
                    case "no_company":
                        _details.text = Local("Create a company to reserve a depot location.",
                            "Depo yeri ayırmak için önce şirket kur.",
                            "Gründe eine Firma, um einen Depotstandort zu reservieren.");
                        break;
                    case "legacy_hq_missing":
                        _details.text = Local("Legacy company: headquarters not assigned.",
                            "Eski şirket kaydı: merkez şehir atanmadı.",
                            "Bestehende Firma: Hauptsitz nicht zugewiesen.");
                        break;
                    case "starter_site_missing":
                        _details.text = Local("Headquarters exists; initial depot site is unassigned.",
                            "Merkez kayıtlı; başlangıç depo yeri henüz ayrılmadı.",
                            "Hauptsitz vorhanden; Depotplatz noch nicht zugewiesen.");
                        break;
                    case "awaiting_economy":
                        _details.text = Local("Site reserved. Company economy must be initialized first.",
                            "Depo yeri ayrıldı. Önce şirket ekonomisi hazırlanmalı.",
                            "Depotplatz reserviert. Zuerst Firmenwirtschaft einrichten.");
                        break;
                    case "awaiting_starter_fleet":
                        _details.text = Local("Site reserved. A verified truck and driver are required.",
                            "Depo yeri ayrıldı. Doğrulanmış kamyon ve sürücü gerekli.",
                            "Depotplatz reserviert. Geprüfter Lkw und Fahrer erforderlich.");
                        break;
                    case "unsupported_starter_region":
                        _details.text = Local("Site reserved. This region awaits supported depot and road data.",
                            "Depo yeri ayrıldı. Bu bölgenin tesis ve yol verisi hazırlanıyor.",
                            "Depotplatz reserviert. Standort- und Straßendaten fehlen noch.");
                        break;
                    case "awaiting_warehouse_catalog":
                        _details.text = Local("Fleet ready. Storage catalog has not been approved.",
                            "Filo hazır. Depo kataloğu henüz onaylanmadı.",
                            "Fuhrpark bereit. Lagerkatalog noch nicht freigegeben.");
                        break;
                    case "ready_to_activate":
                        _details.text = Local(
                            "24 pallet slots ready. Activate your first storage depot.",
                            "24 paletlik kapasite hazır. İlk deponu aktifleştir.",
                            "24 Palettenplätze bereit. Aktiviere dein erstes Lager.");
                        break;
                    case "warehouse_operational_dispatch_pending_routes":
                        _details.text = Local(
                            "Storage active • 24 pallet slots. Dispatch awaits verified routes.",
                            "Depo aktif • 24 palet. Sevkiyat için doğrulanmış rota gerekli.",
                            "Lager aktiv • 24 Palettenplätze. Versand wartet auf geprüfte Routen.");
                        break;
                    default:
                        _details.text = Local("Unknown depot state; no operations allowed.",
                            "Depo durumu bilinmiyor; operasyon açılamaz.",
                            "Unbekannter Lagerstatus; kein Betrieb möglich.");
                        break;
                }
            }

            var needsRecheck = _failed || _uncertain;
            var mayActivate = _verified && _state.CanRequestActivation;
            _button.gameObject.SetActive(needsRecheck || mayActivate);
            _buttonLabel.text = needsRecheck
                ? Local("Check again", "Yeniden kontrol et", "Erneut prüfen")
                : Local("Activate storage depot", "Depoyu aktifleştir", "Lager aktivieren");
            _button.interactable = !_activating && !_requested &&
                (needsRecheck || (mayActivate &&
                ClientStarterDepotActivationGateway.IsAuthorized(
                    ClientJobsRuntime.Settings, FirebaseClient.Current, _requestUid)));
        }
    }
}
