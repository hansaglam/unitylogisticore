using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
    /// An explicit company-name -> review -> check-existing-company -> confirm
    /// flow built into the existing city selection card. Offline/default build
    /// supports reviewing proposals only; no company is created unless the
    /// user opts in to the *local emulator* company authorization window.
    /// No login form, production connection or automatic Firebase mutation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanyHeadquartersSetupFlow : MonoBehaviour
    {
        public enum Stage : byte
        {
            Edit,
            Checking,
            ExistingCompany,
            RequiresConnection,
            Eligible,
            Saving,
            Saved,
            Uncertain
        }

        const int MaxNameCodePoints = 64;
        Func<MapBusinessLocation> _selectedLocation;
        Font _font;
        InputField _companyName;
        Button _review;
        Button _confirm;
        Text _reviewLabel;
        Text _confirmLabel;
        Text _heading;
        Text _nameLabel;
        Text _summary;
        Text _status;
        MapBusinessLocation _reviewCity;
        string _reviewName = "";
        string _verifiedUid = "";
        string _requestKey = "";
        Stage _stage;
        int _generation;
        bool _built;
        bool _awaitingResponse;
        ClientReadSession _reader;

        public Stage CurrentStage => _stage;
        public bool IsBusy => _stage == Stage.Checking || _stage == Stage.Saving;
        public bool HasReviewedSelection => _reviewCity.IsValid && !string.IsNullOrEmpty(_reviewName);
        public bool CanConfirm => _stage == Stage.Eligible &&
            !string.IsNullOrEmpty(_verifiedUid) && HasReviewedSelection &&
            HasSameSelection() && CanConnectForCommand(_verifiedUid);

        public string CompanyName => _companyName == null ? "" : _companyName.text;
        public MapBusinessLocation ReviewedLocation => _reviewCity;

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            if (_built)
                Refresh();
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            _generation++; // Ignore late reads and writes after scene transitions.
            _verifiedUid = "";
            _awaitingResponse = false;
            _stage = Stage.Edit;
        }

        public void Build(Transform parent, Font font, Func<MapBusinessLocation> selectedLocation)
        {
            if (_built || parent == null || selectedLocation == null)
                return;
            _selectedLocation = selectedLocation;
            _font = font;
            _built = true;
            _heading = MakeText(parent, "CompanySetupHeading", 43, 68, FontStyle.Bold);
            _nameLabel = MakeText(parent, "CompanyNameLabel", 34, 46, FontStyle.Bold);
            _companyName = MakeInput(parent);
            _companyName.characterLimit = 128; // Server validates NFC 2–64 code points.
            _companyName.onValueChanged.AddListener(_ => SelectionChanged());

            _review = ShellWidgets.ActionBar(parent, "ReviewCompany", font, out _reviewLabel);
            _review.GetComponent<LayoutElement>().preferredHeight = 95f;
            _review.onClick.AddListener(ReviewSelection);
            _summary = MakeText(parent, "CompanyReviewSummary", 31, 114, FontStyle.Normal);
            _summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            _summary.verticalOverflow = VerticalWrapMode.Overflow;
            _confirm = ShellWidgets.ActionBar(parent, "ConfirmCompany", font, out _confirmLabel);
            _confirm.GetComponent<LayoutElement>().preferredHeight = 95f;
            _confirm.onClick.AddListener(ConfirmCompany);
            _status = MakeText(parent, "CompanySetupStatus", 30, 110, FontStyle.Normal);
            _status.color = ShellPalette.TextSecondary;
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            _status.verticalOverflow = VerticalWrapMode.Overflow;
            Refresh();
        }

        Text MakeText(Transform parent, string name, int size, float height, FontStyle style)
        {
            var go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = ShellPalette.Text;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        InputField MakeInput(Transform parent)
        {
            var go = DefaultControls.CreateInputField(new DefaultControls.Resources());
            go.name = "CompanyNameInput";
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredHeight = 92f;
            var image = go.GetComponent<Image>();
            if (image != null)
                image.color = ShellPalette.Well;
            var input = go.GetComponent<InputField>();
            if (input.textComponent != null)
            {
                input.textComponent.font = _font;
                input.textComponent.fontSize = 35;
                input.textComponent.color = ShellPalette.Text;
            }
            if (input.placeholder is Text placeholder)
            {
                placeholder.font = _font;
                placeholder.fontSize = 34;
                placeholder.color = ShellPalette.TextMuted;
            }
            return input;
        }

        public void SelectionChanged()
        {
            // A company/name/city change is NEVER allowed to reuse an earlier
            // server eligibility decision or command idempotency key.
            if (!_built || _awaitingResponse)
            {
                // A selection altered mid-flight invalidates callback result.
                _generation++;
            }
            _reviewCity = default;
            _reviewName = "";
            _verifiedUid = "";
            _requestKey = "";
            _stage = Stage.Edit;
            _awaitingResponse = false;
            Refresh();
        }

        static string NormalizedDisplayName(string value)
        {
            if (value == null)
                return "";
            try
            {
                // Match the server's NFC / Unicode SpaceSeparator collapsing.
                // Never assume text normalization of malformed UTF-16 succeeds.
                var nfc = value.Normalize(NormalizationForm.FormC);
                var output = new StringBuilder(nfc.Length);
                var lastSpace = false;
                foreach (var c in nfc)
                {
                    var category = CharUnicodeInfo.GetUnicodeCategory(c);
                    if (category == UnicodeCategory.SpaceSeparator)
                    {
                        if (!lastSpace)
                            output.Append(' ');
                        lastSpace = true;
                    }
                    else
                    {
                        output.Append(c);
                        lastSpace = false;
                    }
                }
                return output.ToString().Trim();
            }
            catch (ArgumentException)
            {
                return "";
            }
        }

        static bool IsPlausibleCompanyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            var codePoints = 0;
            var hasAlphanumeric = false;
            for (var i = 0; i < name.Length; i++)
            {
                if (char.IsSurrogate(name[i]))
                {
                    if (!char.IsHighSurrogate(name[i]) || i + 1 >= name.Length ||
                        !char.IsLowSurrogate(name[i + 1]))
                        return false;
                    i++;
                }
                var c = name[i];
                if (char.IsLetterOrDigit(c))
                    hasAlphanumeric = true;
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.Control ||
                    category == UnicodeCategory.Format ||
                    category == UnicodeCategory.LineSeparator ||
                    category == UnicodeCategory.ParagraphSeparator ||
                    category == UnicodeCategory.Surrogate)
                    return false;
                codePoints++;
            }
            return codePoints >= 2 && codePoints <= MaxNameCodePoints && hasAlphanumeric;
        }

        bool HasSameSelection()
        {
            return _selectedLocation != null &&
                _selectedLocation().Equals(_reviewCity) &&
                NormalizedDisplayName(CompanyName) == _reviewName;
        }

        string CurrentUid()
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

        bool CanRead()
        {
            return ClientLocalEmulatorSecurity.AllowsLiveReads(
                ClientJobsRuntime.Settings, FirebaseClient.Current) &&
                !string.IsNullOrEmpty(CurrentUid());
        }

        bool CanConnectForCommand(string uid)
        {
            return uid == CurrentUid() &&
                ClientHeadquartersCreationGateway.IsAuthorized(
                    ClientJobsRuntime.Settings, FirebaseClient.Current, true);
        }

        public void ReviewSelection()
        {
            if (!_built || _awaitingResponse || _stage == Stage.Saved)
                return;
            var city = _selectedLocation();
            var name = NormalizedDisplayName(CompanyName);
            if (!MapTradeRegionCatalog.Includes(city) || !IsPlausibleCompanyName(name))
            {
                _stage = Stage.Edit;
                Refresh();
                return;
            }

            _reviewCity = city;
            _reviewName = name;
            _requestKey = ""; // New review => a fresh command fingerprint.
            _verifiedUid = "";
            if (!CanRead())
            {
                _stage = Stage.RequiresConnection;
                Refresh();
                return;
            }

            var uid = CurrentUid();
            var generation = ++_generation;
            _awaitingResponse = true;
            _stage = Stage.Checking;
            Refresh();

            _reader = new ClientReadSession();
            _reader.SetAsyncInvoker(ClientFunctionsReadInvoker.Invoke);
            _reader.ReadAsync(ClientCallableCatalog.GetMyCompanyState,
                FirebaseClient.Current, true, ClientValue.ReadRequest(), outcome =>
                {
                    if (this == null || generation != _generation)
                        return;
                    _awaitingResponse = false;
                    if (!HasSameSelection() || uid != CurrentUid())
                    {
                        _stage = Stage.Edit;
                        _verifiedUid = "";
                    }
                    else if (!outcome.Ok ||
                        !ClientReadParser.TryCompany(outcome.Envelope.Result, out var company))
                    {
                        _stage = Stage.RequiresConnection;
                    }
                    else if (company.HasCompany)
                    {
                        _stage = Stage.ExistingCompany;
                    }
                    else
                    {
                        _verifiedUid = uid;
                        _stage = CanConnectForCommand(uid) ? Stage.Eligible :
                            Stage.RequiresConnection;
                    }

                    Refresh();
                });
        }

        public void ConfirmCompany()
        {
            if (!CanConfirm || _awaitingResponse)
            {
                Refresh();
                return;
            }
            _awaitingResponse = true;
            _stage = Stage.Saving;
            var generation = ++_generation;
            var name = _reviewName;
            var city = _reviewCity;
            var uid = _verifiedUid;
            if (string.IsNullOrEmpty(_requestKey))
                _requestKey = ClientValue.NewIdempotencyKey();

            Refresh();
            ClientHeadquartersCreationGateway.Submit(
                name, city, _requestKey,
                ClientJobsRuntime.Settings, FirebaseClient.Current, uid,
                raw =>
                {
                    if (this == null || generation != _generation)
                        return;
                    _awaitingResponse = false;
                    if (uid != CurrentUid() || !HasSameSelection())
                    {
                        _stage = Stage.RequiresConnection;
                    }
                    else if (CreatedResultMatches(raw, name, city))
                    {
                        _stage = Stage.Saved;
                    }
                    else
                    {
                        _stage = Stage.Uncertain;
                    }
                    Refresh();
                },
                _ =>
                {
                    if (this == null || generation != _generation)
                        return;
                    _awaitingResponse = false;
                    _stage = Stage.Uncertain;
                    // Do not silently retry: same key is preserved until explicit
                    // user re-checks the server company state.
                    Refresh();
                });
        }

        static bool CreatedResultMatches(object raw, string name, MapBusinessLocation city)
        {
            if (!ClientEnvelope.TryParse(raw, out var envelope, out _) ||
                !ClientValue.TryMap(envelope.Result, out var result) ||
                !ClientValue.TryString(ClientValue.Get(result, "companyId"), out var companyId) ||
                string.IsNullOrEmpty(companyId) ||
                !ClientValue.TryString(ClientValue.Get(result, "name"), out var storedName) ||
                storedName != name ||
                !ClientValue.TryMap(ClientValue.Get(result, "headquarters"), out var hq) ||
                !ClientValue.TryString(ClientValue.Get(hq, "countryCode"), out var country) ||
                !ClientValue.TryString(ClientValue.Get(hq, "cityKey"), out var cityKey) ||
                !ClientValue.TryString(ClientValue.Get(hq, "catalogVersion"), out var version))
                return false;
            return country == city.CountryCode && cityKey == city.CityKey &&
                version == "lc_tur_europe_2026_11_v1";
        }

        string Language
        {
            get
            {
                var selected = LocalizationSettings.SelectedLocale;
                var code = selected != null ? selected.Identifier.Code : "en";
                if (code.StartsWith("tr", StringComparison.OrdinalIgnoreCase))
                    return "tr";
                return code.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
            }
        }

        string Local(string english, string turkish, string german)
        {
            return Language == "tr" ? turkish : Language == "de" ? german : english;
        }

        void OnLocaleChanged(Locale _) => Refresh();

        void Refresh()
        {
            if (!_built || _heading == null)
                return;
            _heading.text = Local("Found your company", "Şirketini kur", "Unternehmen gründen");
            _nameLabel.text = Local("Company name", "Şirket adı", "Firmenname");
            if (_companyName.placeholder is Text hint)
                hint.text = Local("e.g. Horizon Logistics", "Örn. Ufuk Lojistik", "z. B. Horizont Logistik");
            _reviewLabel.text = Local("Review and check", "İncele ve kontrol et", "Prüfen und bestätigen");
            _confirmLabel.text = Local("Confirm company creation", "Şirket kuruluşunu onayla",
                "Firmengründung bestätigen");

            var cityLabel = "";
            if (_reviewCity.IsValid && MapGlobalCityCatalog.TryGet(_reviewCity, out var city))
                cityLabel = city.DisplayName + " (" + city.Location.CountryCode + ")";
            _summary.text = HasReviewedSelection
                ? _reviewName + "  •  " + cityLabel
                : Local("Select a city and enter a company name.",
                    "Şehir seç ve şirket adını yaz.", "Stadt wählen und Firmennamen eingeben.");
            switch (_stage)
            {
                case Stage.Checking:
                    _status.text = Local("Checking your existing company...", "Mevcut şirketin kontrol ediliyor...",
                        "Bestehende Firma wird geprüft...");
                    break;
                case Stage.ExistingCompany:
                    _status.text = Local("You already have a company. Existing progress is protected.",
                        "Zaten bir şirketin var. Mevcut ilerlemen korunuyor.",
                        "Du hast bereits eine Firma. Dein Fortschritt bleibt erhalten.");
                    break;
                case Stage.Eligible:
                    _status.text = Local("No existing company found. Confirm to create in the local emulator.",
                        "Mevcut şirket bulunmadı. Yerel emülatörde kurmak için onayla.",
                        "Keine Firma gefunden. Für den lokalen Emulator bestätigen.");
                    break;
                case Stage.Saving:
                    _status.text = Local("Saving company and headquarters...", "Şirket ve merkez kaydediliyor...",
                        "Firma und Sitz werden gespeichert...");
                    break;
                case Stage.Saved:
                    _status.text = Local("Company and headquarters were confirmed by the emulator.",
                        "Şirket ve merkez emülatör tarafından doğrulandı.",
                        "Firma und Sitz wurden vom Emulator bestätigt.");
                    break;
                case Stage.Uncertain:
                    _status.text = Local("Server result is uncertain. Recheck company state before retrying.",
                        "Sunucu sonucu belirsiz. Tekrar denemeden önce şirket durumunu kontrol et.",
                        "Serverantwort ungewiss. Vor erneutem Versuch Firmenstatus prüfen.");
                    break;
                case Stage.RequiresConnection:
                    _status.text = Local("Preview only: a signed-in local emulator and approved company test are required.",
                        "Yalnızca önizleme: oturum açılmış yerel emülatör ve şirket testi izni gerekli.",
                        "Nur Vorschau: Anmeldung am lokalen Emulator und Firmentest-Freigabe nötig.");
                    break;
                default:
                    _status.text = Local("No account or company changes happen until you explicitly confirm.",
                        "Açıkça onaylamadan hesap veya şirket verileri değişmez.",
                        "Ohne ausdrückliche Bestätigung werden keine Kontodaten verändert.");
                    break;
            }

            var locked = _awaitingResponse || _stage == Stage.Saved;
            _companyName.interactable = !locked;
            var preview = GetComponent<CompanyHeadquartersPreview>();
            if (preview != null)
                preview.SetSetupLocked(locked);
            _review.interactable = !_awaitingResponse && _stage != Stage.Saved;
            _confirm.interactable = CanConfirm && !_awaitingResponse;
        }
    }
}
