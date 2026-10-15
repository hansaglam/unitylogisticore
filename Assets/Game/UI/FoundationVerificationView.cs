using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using LogistiCore;
using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace LogistiCore.UI
{
    /// <summary>
    /// Foundation check for locale switching, smart strings, formatting, safe area, and orientation.
    /// Text stays empty until localization is ready. There is no per-frame refresh.
    /// </summary>
    public sealed class FoundationVerificationView : MonoBehaviour
    {
        const string SamplePlayer = "Ada";
        const double SampleNumber = 1234567.89d;
        const decimal SampleMoney = 19990.50m;
        const string SampleCurrency = "EUR";
        const double SampleRatio = 0.125d;
        const double SampleMeters = 12850d;
        const double SampleKilograms = 2400d;

        static readonly TimeSpan SampleDuration = new TimeSpan(1, 2, 5, 0);
        static readonly DateTimeOffset SampleTime = new DateTimeOffset(2026, 10, 16, 15, 30, 0, TimeSpan.Zero);

        [SerializeField] Text titleText;
        [SerializeField] Text bodyText;
        [SerializeField] Text englishLabel;
        [SerializeField] Text turkishLabel;
        [SerializeField] Text germanLabel;
        [SerializeField] Text portraitLabel;
        [SerializeField] Text landscapeLabel;
        [SerializeField] Button englishButton;
        [SerializeField] Button turkishButton;
        [SerializeField] Button germanButton;
        [SerializeField] Button portraitButton;
        [SerializeField] Button landscapeButton;

        readonly List<Binding> _bindings = new List<Binding>(18);
        readonly StringBuilder _body = new StringBuilder(512);
        readonly Dictionary<string, object> _localeArguments = new Dictionary<string, object> { { "code", LocaleCodes.English } };

        Binding _copy;
        Binding _greeting;
        Binding _deliveryOne;
        Binding _deliveryMany;
        Binding _locale;
        Binding _labelNumber;
        Binding _labelCurrency;
        Binding _labelPercent;
        Binding _labelDistance;
        Binding _labelWeight;
        Binding _labelDuration;
        Binding _labelDateTime;
        bool _subscribed;

        void Awake()
        {
            _copy = Track(LocalizationContent.Body);
            _greeting = Track(LocalizationContent.Greeting, Named("player", SamplePlayer));
            _deliveryOne = Track(LocalizationContent.Deliveries, Named("count", 1));
            _deliveryMany = Track(LocalizationContent.Deliveries, Named("count", 5));
            _locale = Track(LocalizationContent.ActiveLocale, _localeArguments);
            _labelNumber = Track(LocalizationContent.LabelNumber);
            _labelCurrency = Track(LocalizationContent.LabelCurrency);
            _labelPercent = Track(LocalizationContent.LabelPercent);
            _labelDistance = Track(LocalizationContent.LabelDistance);
            _labelWeight = Track(LocalizationContent.LabelWeight);
            _labelDuration = Track(LocalizationContent.LabelDuration);
            _labelDateTime = Track(LocalizationContent.LabelDateTime);

            TrackLabel(titleText, LocalizationContent.Title);
            TrackLabel(englishLabel, LocalizationContent.LanguageEnglish);
            TrackLabel(turkishLabel, LocalizationContent.LanguageTurkish);
            TrackLabel(germanLabel, LocalizationContent.LanguageGerman);
            TrackLabel(portraitLabel, LocalizationContent.Portrait);
            TrackLabel(landscapeLabel, LocalizationContent.Landscape);

            if (titleText != null)
                titleText.text = string.Empty;
            if (bodyText != null)
                bodyText.text = string.Empty;
        }

        IEnumerator Start()
        {
            var operation = LocalizationSettings.InitializationOperation;
            yield return operation;
            if (LocalizationSettings.SelectedLocale == null)
                GameLog.Error("Locale", "Localization did not finish initializing.");
        }

        void OnEnable()
        {
            Subscribe();
            Listen(englishButton, SelectEnglish, true);
            Listen(turkishButton, SelectTurkish, true);
            Listen(germanButton, SelectGerman, true);
            Listen(portraitButton, SelectPortrait, true);
            Listen(landscapeButton, SelectLandscape, true);

            if (LocaleService.IsReady)
                OnLocaleChanged(LocalizationSettings.SelectedLocale);
        }

        void OnDisable()
        {
            Listen(englishButton, SelectEnglish, false);
            Listen(turkishButton, SelectTurkish, false);
            Listen(germanButton, SelectGerman, false);
            Listen(portraitButton, SelectPortrait, false);
            Listen(landscapeButton, SelectLandscape, false);
            Unsubscribe();
        }

        Binding Track(string key, object argumentSource = null)
        {
            var binding = new Binding
            {
                Source = new LocalizedString(LocalizationContent.Table, key)
            };
            if (argumentSource != null)
                binding.Source.Arguments = new object[] { argumentSource };

            binding.Handler = value =>
            {
                binding.Value = value ?? string.Empty;
                Refresh();
            };
            _bindings.Add(binding);
            return binding;
        }

        void TrackLabel(Text label, string key)
        {
            var binding = new Binding
            {
                Source = new LocalizedString(LocalizationContent.Table, key),
                Target = label
            };
            binding.Handler = value =>
            {
                if (binding.Target != null)
                    binding.Target.text = value ?? string.Empty;
            };
            _bindings.Add(binding);
        }

        void Subscribe()
        {
            if (_subscribed)
                return;

            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Source.StringChanged += _bindings[i].Handler;

            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed)
                return;

            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            for (var i = 0; i < _bindings.Count; i++)
                _bindings[i].Source.StringChanged -= _bindings[i].Handler;

            _subscribed = false;
        }

        void OnLocaleChanged(Locale locale)
        {
            _localeArguments["code"] = locale == null
                ? LocaleCodes.English
                : LocaleCodes.NormalizeSelection(locale.Identifier.Code);
            _locale.Source.RefreshString();
            Refresh();
        }

        void Refresh()
        {
            if (bodyText == null)
                return;

            _body.Clear();
            AppendLine(_copy.Value);
            AppendLine(_greeting.Value);
            AppendLine(_deliveryOne.Value);
            AppendLine(_deliveryMany.Value);
            AppendLine(_locale.Value);

            if (LocaleService.IsReady)
            {
                var culture = LocaleService.CurrentCulture;
                _body.AppendLine();
                AppendSample(_labelNumber.Value, LocalizedFormatting.Number(SampleNumber, culture));
                AppendSample(_labelCurrency.Value, LocalizedFormatting.Currency(SampleMoney, SampleCurrency, culture));
                AppendSample(_labelPercent.Value, LocalizedFormatting.Percent(SampleRatio, culture));
                AppendSample(_labelDistance.Value, LocalizedFormatting.Distance(SampleMeters, culture));
                AppendSample(_labelWeight.Value, LocalizedFormatting.Weight(SampleKilograms, culture));
                AppendSample(_labelDuration.Value, LocalizedFormatting.Duration(SampleDuration, culture));
                AppendSample(_labelDateTime.Value, LocalizedFormatting.DateAndTime(SampleTime, culture));
            }

            bodyText.text = _body.ToString();
        }

        void AppendLine(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            _body.AppendLine(value);
        }

        void AppendSample(string label, string formatted)
        {
            if (string.IsNullOrEmpty(label))
                _body.AppendLine(formatted);
            else
                _body.AppendLine(label + "  " + formatted);
        }

        static Dictionary<string, object> Named(string key, object value)
        {
            return new Dictionary<string, object> { { key, value } };
        }

        static void Listen(Button button, UnityEngine.Events.UnityAction action, bool add)
        {
            if (button == null)
                return;

            if (add)
                button.onClick.AddListener(action);
            else
                button.onClick.RemoveListener(action);
        }

        void SelectEnglish() => LocaleService.TrySelect(LocaleCodes.English);
        void SelectTurkish() => LocaleService.TrySelect(LocaleCodes.Turkish);
        void SelectGerman() => LocaleService.TrySelect(LocaleCodes.German);
        void SelectPortrait() => ScreenLayouts.Apply(ScreenLayoutMode.Portrait);
        void SelectLandscape() => ScreenLayouts.Apply(ScreenLayoutMode.Landscape);

        sealed class Binding
        {
            public LocalizedString Source;
            public Text Target;
            public string Value = string.Empty;
            public LocalizedString.ChangeHandler Handler;
        }
    }
}
