using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace LogistiCore.UI
{
    /// <summary>
    /// Portrait entry. Widgets are created when play starts and are not written back into the scene.
    /// The two actions are the World Map card and the Company card.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeShell : MonoBehaviour
    {
        Text _brand;
        Text _heading;
        Text _support;
        Text _mapTitle;
        Text _mapCaption;
        Text _companyTitle;
        Text _companyCaption;
        Text _noticeBody;
        Button _mapButton;
        Button _companyButton;
        Image _background;
        LayoutElement _mapElement;
        LayoutElement _companyElement;
        LayoutElement _noticeElement;
        ShellWidgets.Column _column;
        bool _built;
        bool _subscribed;
        bool _listening;
        bool _companyListening;
        int _width = -1;
        int _height = -1;
        Rect _safe;
        bool _layoutApplied;
        LocalizedString _brandSource;
        LocalizedString _headingSource;
        LocalizedString _supportSource;
        LocalizedString _mapSource;
        LocalizedString _mapCaptionSource;
        LocalizedString _companySource;
        LocalizedString _companyCaptionSource;
        LocalizedString _noticeSource;

        public Button MapButton => _mapButton;
        public Button CompanyButton => _companyButton;
        public int ButtonCount { get; private set; }

        void Awake()
        {
            BuildIfNeeded();
        }

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            ScreenLayouts.Apply(ScreenLayoutMode.Portrait);
            Subscribe();
            if (_mapButton != null && !_listening)
            {
                _mapButton.onClick.AddListener(OpenMap);
                _listening = true;
            }

            if (_companyButton != null && !_companyListening)
            {
                _companyButton.onClick.AddListener(OpenCompany);
                _companyListening = true;
            }

            ApplyCurrentScreen();
        }

        void OnDisable()
        {
            if (_mapButton != null && _listening)
            {
                _mapButton.onClick.RemoveListener(OpenMap);
                _listening = false;
            }

            if (_companyButton != null && _companyListening)
            {
                _companyButton.onClick.RemoveListener(OpenCompany);
                _companyListening = false;
            }

            Unsubscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
            DisposeSource(ref _brandSource);
            DisposeSource(ref _headingSource);
            DisposeSource(ref _supportSource);
            DisposeSource(ref _mapSource);
            DisposeSource(ref _mapCaptionSource);
            DisposeSource(ref _companySource);
            DisposeSource(ref _companyCaptionSource);
            DisposeSource(ref _noticeSource);
        }

        static void DisposeSource(ref LocalizedString source)
        {
            if (source == null)
                return;

            ((System.IDisposable)source).Dispose();
            source = null;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            ApplyCurrentScreen();
        }

        public void OpenMap()
        {
            AppNavigation.ShowMap();
        }

        public void OpenCompany()
        {
            AppNavigation.ShowCompany();
        }

        public void BuildIfNeeded()
        {
            if (_built)
                return;

            _built = true;
            EnsureEventSystem();
            var font = ShellWidgets.Builtin();
            var root = transform as RectTransform;
            if (root == null)
                return;

            _background = root.GetComponent<Image>();
            if (_background == null)
                _background = root.gameObject.AddComponent<Image>();
            _background.color = ShellPalette.Background;
            _background.raycastTarget = false;

            var safeObject = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaRect));
            safeObject.transform.SetParent(root, false);
            ShellWidgets.Mark(safeObject);
            ShellWidgets.Stretch((RectTransform)safeObject.transform);

            _column = ShellWidgets.ScrollColumn(safeObject.transform);
            ShellWidgets.Header(_column.Content, font, true, out _brand, out _heading, out _support);
            _mapButton = ShellWidgets.FeatureCard(
                _column.Content,
                "WorldMap",
                font,
                true,
                ShellPalette.CardRaised,
                ShellPalette.BorderStrong,
                out _mapTitle,
                out _mapCaption);
            _companyButton = ShellWidgets.FeatureCard(
                _column.Content,
                "Company",
                font,
                false,
                ShellPalette.Card,
                ShellPalette.Border,
                out _companyTitle,
                out _companyCaption);
            var notice = ShellWidgets.Notice(_column.Content, font, out _noticeBody);
            _mapElement = _mapButton.GetComponent<LayoutElement>();
            _companyElement = _companyButton.GetComponent<LayoutElement>();
            _noticeElement = notice.GetComponent<LayoutElement>();
            ButtonCount = 2;

            _brandSource = Bind(_brand, ShellContent.Brand);
            _headingSource = Bind(_heading, ShellContent.Heading);
            _supportSource = Bind(_support, ShellContent.Support);
            _mapSource = Bind(_mapTitle, ShellContent.WorldMap);
            _mapCaptionSource = Bind(_mapCaption, ShellContent.MapCaption);
            _companySource = Bind(_companyTitle, ShellContent.CompanyAction);
            _companyCaptionSource = Bind(_companyCaption, ShellContent.CompanyCaption);
            _noticeSource = Bind(_noticeBody, ShellContent.Offline);
        }

        public void Apply(float screenWidth, float screenHeight, Rect safeArea)
        {
            BuildIfNeeded();
            var metrics = HomeShellLayout.Evaluate(screenWidth, screenHeight, safeArea);
            if (_column.Layout != null)
            {
                var pad = Mathf.RoundToInt(metrics.Padding / metrics.CanvasScale);
                _column.Layout.padding = new RectOffset(pad, pad, pad, pad);
                _column.Layout.spacing = ShellTypography.BlockGap;
            }

            ShellWidgets.UseScroll(_column, metrics.Overflow);
            ShellWidgets.SetLine(_brand, metrics.BrandSize, 1);
            ShellWidgets.SetLine(_heading, metrics.HeadingSize, 1);
            ShellWidgets.SetLine(_support, metrics.BodySize, 1);
            ShellWidgets.SetLine(_mapTitle, metrics.ButtonLabelSize, 1);
            ShellWidgets.SetLine(_mapCaption, metrics.CaptionSize, 2);
            ShellWidgets.SetLine(_companyTitle, metrics.ButtonLabelSize, 1);
            ShellWidgets.SetLine(_companyCaption, metrics.CaptionSize, 2);
            ShellWidgets.SetLine(_noticeBody, metrics.BodySize, 2);
            ShellWidgets.SetBox(_mapElement, metrics.ButtonHeight / metrics.CanvasScale);
            ShellWidgets.SetBox(_companyElement, metrics.CompanyButton.height / metrics.CanvasScale);
            ShellWidgets.SetBox(_noticeElement, metrics.Notice.height / metrics.CanvasScale);

            _width = screenWidth < 1f ? 0 : Mathf.RoundToInt(screenWidth);
            _height = screenHeight < 1f ? 0 : Mathf.RoundToInt(screenHeight);
            _safe = safeArea;
            _layoutApplied = true;
        }

        void ApplyCurrentScreen()
        {
            var width = Screen.width;
            var height = Screen.height;
            var safe = Screen.safeArea;
            if (_layoutApplied && width == _width && height == _height && safe == _safe)
                return;

            Apply(width, height, safe);
        }

        void EnsureEventSystem()
        {
            var scene = gameObject.scene;
            if (!scene.IsValid())
                return;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<EventSystem>(true) != null)
                    return;
            }

            var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ShellWidgets.Mark(eventObject);
            if (eventObject.scene != scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(eventObject, scene);
        }

        void Subscribe()
        {
            if (_subscribed)
                return;

            Listen(_brandSource, OnBrand);
            Listen(_headingSource, OnHeading);
            Listen(_supportSource, OnSupport);
            Listen(_mapSource, OnMap);
            Listen(_mapCaptionSource, OnMapCaption);
            Listen(_companySource, OnCompany);
            Listen(_companyCaptionSource, OnCompanyCaption);
            Listen(_noticeSource, OnNotice);
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed)
                return;

            Unlisten(_brandSource, OnBrand);
            Unlisten(_headingSource, OnHeading);
            Unlisten(_supportSource, OnSupport);
            Unlisten(_mapSource, OnMap);
            Unlisten(_mapCaptionSource, OnMapCaption);
            Unlisten(_companySource, OnCompany);
            Unlisten(_companyCaptionSource, OnCompanyCaption);
            Unlisten(_noticeSource, OnNotice);
            _subscribed = false;
        }

        static void Listen(LocalizedString source, UnityEngine.Localization.LocalizedString.ChangeHandler handler)
        {
            if (source != null)
                source.StringChanged += handler;
        }

        static void Unlisten(LocalizedString source, UnityEngine.Localization.LocalizedString.ChangeHandler handler)
        {
            if (source != null)
                source.StringChanged -= handler;
        }

        void OnBrand(string value) { Set(_brand, value); }
        void OnHeading(string value) { Set(_heading, value); }
        void OnSupport(string value) { Set(_support, value); }
        void OnMap(string value) { Set(_mapTitle, value); }
        void OnMapCaption(string value) { Set(_mapCaption, value); }
        void OnCompany(string value) { Set(_companyTitle, value); }
        void OnCompanyCaption(string value) { Set(_companyCaption, value); }
        void OnNotice(string value) { Set(_noticeBody, value); }

        static void Set(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }

        static LocalizedString Bind(Text target, string key)
        {
            if (target != null)
                target.text = string.Empty;
            return new LocalizedString(ShellContent.Table, key);
        }
    }
}
