using System;
using Firebase.Auth;
using LogistiCore.Backend;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace LogistiCore.UI
{
    /// <summary>
    /// Status only: this panel never provisions trucks/warehouses or unlocks a
    /// route. It displays the server-verified, non-operational HQ depot site
    /// when a signed-in local-emulator company has one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanyStarterSiteStatus : MonoBehaviour
    {
        Text _label;
        bool _built;
        bool _requested;
        int _generation;
        string _requestUid = "";
        ClientStarterDepotSiteState _state;
        bool _verified;
        bool _failed;

        public bool IsVerified => _verified;
        public bool HasStarterSite => _verified && _state.HasStarterSite;

        /// <summary>Re-read after explicit company registration, never after an arbitrary locale change.</summary>
        public void InvalidateForCompanyCreation()
        {
            _generation++;
            _requested = false;
            _verified = false;
            _failed = false;
            _requestUid = "";
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
            _generation++;
            _requested = false;
            _requestUid = "";
        }

        public void Build(Font font)
        {
            if (_built)
                return;
            _built = true;
            var element = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 102f;
            element.flexibleWidth = 1f;

            var box = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            box.color = ShellPalette.Well;
            box.raycastTarget = false;

            var layout = GetComponent<HorizontalLayoutGroup>() ??
                gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 14, 14);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var child = new GameObject("StarterDepotNotice",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            child.transform.SetParent(transform, false);
            _label = child.GetComponent<Text>();
            _label.font = font;
            _label.fontSize = 28;
            _label.color = ShellPalette.Text;
            _label.supportRichText = false;
            _label.alignment = TextAnchor.MiddleLeft;
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
            Refresh();
        }

        void Update()
        {
            if (!_built || _requested || _verified || _failed)
                return;

            var status = FirebaseClient.Current;
            if (!FirebaseClient.SdkAppReady ||
                !ClientLocalEmulatorSecurity.AllowsLiveReads(ClientJobsRuntime.Settings, status))
                return;

            string uid;
            try
            {
                var auth = FirebaseAuth.DefaultInstance;
                uid = auth != null && auth.CurrentUser != null
                    ? auth.CurrentUser.UserId : "";
            }
            catch { return; }
            if (string.IsNullOrEmpty(uid))
                return;

            _requested = true;
            _requestUid = uid;
            var generation = ++_generation;
            var session = new ClientReadSession();
            session.SetAsyncInvoker(ClientFunctionsReadInvoker.Invoke);
            session.ReadAsync(ClientCallableCatalog.GetMyStarterDepotSite,
                status, true, ClientValue.ReadRequest(), response =>
                {
                    if (this == null || _generation != generation)
                        return;

                    var current = "";
                    try
                    {
                        var auth = FirebaseAuth.DefaultInstance;
                        current = auth != null && auth.CurrentUser != null
                            ? auth.CurrentUser.UserId : "";
                    }
                    catch { }
                    if (_requestUid != current || string.IsNullOrEmpty(current))
                    {
                        _failed = true;
                        Refresh();
                        return;
                    }

                    if (response.Ok &&
                        ClientReadParser.TryStarterDepotSite(
                            response.Envelope.Result, out var parsed))
                    {
                        _state = parsed;
                        _verified = true;
                    }
                    else
                        _failed = true;
                    Refresh();
                });
        }

        string Language
        {
            get
            {
                var locale = LocalizationSettings.SelectedLocale;
                var name = locale != null ? locale.Identifier.Code : "en";
                if (name.StartsWith("tr", StringComparison.OrdinalIgnoreCase))
                    return "tr";
                return name.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
            }
        }

        string Local(string en, string tr, string de) =>
            Language == "tr" ? tr : Language == "de" ? de : en;

        void OnLocaleChanged(Locale _) => Refresh();

        void Refresh()
        {
            if (_label == null)
                return;
            if (_failed)
            {
                _label.text = Local("Depot status unavailable; no changes made.",
                    "Depo durumu alınamadı; hiçbir kayıt değiştirilmedi.",
                    "Depotstatus nicht verfügbar; keine Daten geändert.");
            }
            else if (!_verified)
            {
                _label.text = Local("Starter depot: awaiting verified account data.",
                    "Başlangıç deposu: doğrulanmış şirket bilgisi bekleniyor.",
                    "Startdepot: verifizierte Firmendaten werden erwartet.");
            }
            else if (!_state.HasCompany)
            {
                _label.text = Local("Create a company to reserve a starter depot site.",
                    "Başlangıç depo yeri için önce şirket kur.",
                    "Gründe eine Firma, um einen Startdepotplatz zu reservieren.");
            }
            else if (!_state.HasHeadquarters)
            {
                _label.text = Local("Existing company: no HQ has been assigned.",
                    "Mevcut şirkette merkez şehir kayıtlı değil.",
                    "Bestehende Firma: noch kein Hauptsitz zugewiesen.");
            }
            else if (!_state.HasStarterSite)
            {
                _label.text = Local("Headquarters confirmed; starter site not reserved.",
                    "Merkez doğrulandı; başlangıç depo yeri ayrılmamış.",
                    "Hauptsitz bestätigt; Startdepotplatz nicht reserviert.");
            }
            else
            {
                _label.text = Local(
                    "Depot site reserved at HQ. Dispatch awaits verified routes and fleet.",
                    "Merkezde depo yeri ayrıldı. Teslimat için doğrulanmış rota ve filo gerekli.",
                    "Depotplatz am Hauptsitz reserviert. Versand wartet auf geprüfte Routen und Fuhrpark.");
            }
        }
    }
}
