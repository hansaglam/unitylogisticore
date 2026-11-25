using System;
using System.Collections.Generic;
using LogistiCore.Map;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace LogistiCore.UI
{
    /// <summary>
    /// Client-side country and city browser for the proposed headquarters.
    /// Explicitly a map-only preview until a server-authorized company HQ
    /// command, existing-player migration and persistence contract are built.
    /// Country labels use MapCountryCatalog TR/EN/DE, city names use published
    /// map place labels (some localized city translations are still pending).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanyHeadquartersPreview : MonoBehaviour
    {
        const float PreferredHeight = 650f;

        struct CountryOption
        {
            public string Code;
            public string Label;
        }

        struct CityOption
        {
            public MapBusinessLocation Location;
            public string Label;
        }

        readonly List<CountryOption> _countries = new List<CountryOption>(50);
        readonly List<CityOption> _cities = new List<CityOption>(100);
        Dropdown _country;
        Dropdown _city;
        Button _preview;
        Text _heading;
        Text _notice;
        Text _action;
        Text _countryTitle;
        Text _cityTitle;
        Font _font;
        bool _built;

        public bool Built => _built;
        public int CountryCount => _countries.Count;
        public int CityCount => _cities.Count;
        public MapBusinessLocation SelectedLocation =>
            _city != null && _city.value >= 0 && _city.value < _cities.Count
                ? _cities[_city.value].Location
                : default;

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            if (_built)
                RefreshLabels();
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        public void Build(Font font)
        {
            if (_built)
                return;

            _built = true;
            _font = font;
            var root = GetComponent<RectTransform>();
            if (root == null)
                return;

            var face = gameObject.AddComponent<Image>();
            face.color = ShellPalette.CardRaised;
            face.raycastTarget = false;
            var element = GetComponent<LayoutElement>();
            if (element == null)
                element = gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = PreferredHeight;
            element.flexibleWidth = 1f;

            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 22, 22);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            _heading = AddText("PreviewHeading", 52f, FontStyle.Bold, 76f);
            _countryTitle = AddText("CountryLabel", 35f, FontStyle.Bold, 50f);
            _country = AddDropdown("Country", 106f);
            _cityTitle = AddText("CityLabel", 35f, FontStyle.Bold, 50f);
            _city = AddDropdown("City", 106f);
            _notice = AddText("PreviewNotice", 31f, FontStyle.Normal, 90f);
            _notice.horizontalOverflow = HorizontalWrapMode.Wrap;
            _notice.verticalOverflow = VerticalWrapMode.Overflow;

            var action = ShellWidgets.ActionBar(transform, "PreviewMap", _font, out _action);
            var actionElement = action.GetComponent<LayoutElement>();
            actionElement.preferredHeight = 104f;
            _preview = action;
            _preview.onClick.AddListener(PreviewOnMap);
            _country.onValueChanged.AddListener(OnCountryChanged);
            _city.onValueChanged.AddListener(OnCityChanged);

            RefreshLabels();
        }

        Text AddText(string name, float fontSize, FontStyle style, float height)
        {
            var go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(transform, false);
            var label = go.GetComponent<Text>();
            label.font = _font;
            label.fontSize = Mathf.RoundToInt(fontSize);
            label.fontStyle = style;
            label.color = ShellPalette.Text;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        Dropdown AddDropdown(string name, float height)
        {
            // Unity uGUI's standard dropdown creates its own item template
            // and scroll view, instead of an unscrollable list of all cities.
            var go = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(transform, false);
            var element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleWidth = 1f;
            var dropdown = go.GetComponent<Dropdown>();
            var image = go.GetComponent<Image>();
            if (image != null)
                image.color = ShellPalette.Well;
            if (dropdown.captionText != null)
            {
                dropdown.captionText.font = _font;
                dropdown.captionText.fontSize = 38;
                dropdown.captionText.color = ShellPalette.Text;
            }
            if (dropdown.itemText != null)
            {
                dropdown.itemText.font = _font;
                dropdown.itemText.fontSize = 32;
                dropdown.itemText.color = Color.black; // Standard white expanded menu.
            }
            return dropdown;
        }

        string Language
        {
            get
            {
                var locale = LocalizationSettings.SelectedLocale;
                if (locale == null)
                    return "en";
                var code = locale.Identifier.Code;
                if (code != null && code.StartsWith("tr", StringComparison.OrdinalIgnoreCase))
                    return "tr";
                if (code != null && code.StartsWith("de", StringComparison.OrdinalIgnoreCase))
                    return "de";
                return "en";
            }
        }

        string Copy(string english, string turkish, string german)
        {
            switch (Language)
            {
                case "tr": return turkish;
                case "de": return german;
                default: return english;
            }
        }

        void OnLocaleChanged(Locale _)
        {
            if (_built)
                RefreshLabels();
        }

        string CountryName(string code)
        {
            var index = MapCountryCatalog.IndexOf(code);
            if (index < 0)
                return code;
            var country = MapCountryCatalog.All[index];
            switch (Language)
            {
                case "tr": return country.Turkish;
                case "de": return country.German;
                default: return country.English;
            }
        }

        void RefreshLabels()
        {
            if (_country == null || _city == null)
                return;
            _heading.text = Copy("Choose a starting region", "Başlangıç bölgeni seç", "Startregion auswählen");
            _countryTitle.text = Copy("Country", "Ülke", "Land");
            _cityTitle.text = Copy("City", "Şehir", "Stadt");
            _notice.text = Copy(
                "Map preview only. This does not create a company or change your headquarters.",
                "Yalnızca harita önizlemesi. Şirket kurmaz veya mevcut merkezini değiştirmez.",
                "Nur Kartenvorschau. Dies gründet keine Firma und ändert deinen Firmensitz nicht.");
            _action.text = Copy("Preview on map", "Haritada incele", "Auf Karte ansehen");
            var previousCountry = _countries.Count > 0 && _country.value < _countries.Count
                ? _countries[_country.value].Code : "TUR";
            RebuildCountries(previousCountry);
        }

        void RebuildCountries(string requestedCode)
        {
            _countries.Clear();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var city in MapGlobalCityCatalog.All)
            {
                if (!MapTradeRegionCatalog.Includes(city))
                    continue;
                var code = city.Location.CountryCode;
                if (seen.Add(code))
                    _countries.Add(new CountryOption { Code = code, Label = CountryName(code) });
            }
            _countries.Sort((a, b) =>
            {
                var result = StringComparer.OrdinalIgnoreCase.Compare(a.Label, b.Label);
                return result == 0 ? StringComparer.Ordinal.Compare(a.Code, b.Code) : result;
            });

            var options = new List<Dropdown.OptionData>(_countries.Count);
            var selected = 0;
            for (var i = 0; i < _countries.Count; i++)
            {
                options.Add(new Dropdown.OptionData(_countries[i].Label));
                if (_countries[i].Code == requestedCode)
                    selected = i;
            }

            _country.ClearOptions();
            _country.AddOptions(options);
            _country.SetValueWithoutNotify(selected);
            _country.RefreshShownValue();
            RebuildCities();
        }

        void OnCountryChanged(int index)
        {
            RebuildCities();
        }

        void RebuildCities()
        {
            _cities.Clear();
            if (_countries.Count == 0)
            {
                _preview.interactable = false;
                return;
            }

            var countryCode = _countries[_country.value].Code;
            foreach (var city in MapGlobalCityCatalog.All)
            {
                if (city.Location.CountryCode != countryCode || !MapTradeRegionCatalog.Includes(city))
                    continue;
                _cities.Add(new CityOption
                {
                    Location = city.Location,
                    Label = city.DisplayName
                });
            }

            _cities.Sort((a, b) =>
            {
                var result = StringComparer.OrdinalIgnoreCase.Compare(a.Label, b.Label);
                return result == 0
                    ? StringComparer.Ordinal.Compare(a.Location.CityKey, b.Location.CityKey)
                    : result;
            });

            var options = new List<Dropdown.OptionData>(_cities.Count);
            foreach (var city in _cities)
                options.Add(new Dropdown.OptionData(city.Label));
            _city.ClearOptions();
            _city.AddOptions(options);
            _city.SetValueWithoutNotify(0);
            _city.RefreshShownValue();
            OnCityChanged(0);
        }

        void OnCityChanged(int index)
        {
            if (_preview != null)
                _preview.interactable = SelectedLocation.IsValid;
        }

        public bool PreviewOnMap()
        {
            var location = SelectedLocation;
            if (!MapHeadquartersPreviewSelection.TryQueue(location))
                return false;

            AppNavigation.ShowMap();
            return true;
        }
    }
}
