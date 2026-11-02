using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace LogistiCore.UI
{
    /// <summary>
    /// Portrait company overview. Widgets are created at play and are not written into the scene.
    /// There is no company read contract yet, so the frame stays the unconnected state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanyShell : MonoBehaviour
    {
        Text _brand;
        Text _heading;
        Text _status;
        Text _body;
        Text _buttonLabel;
        Button _homeButton;
        Image _background;
        LayoutElement _overviewElement;
        LayoutElement _buttonElement;
        ShellWidgets.Column _column;
        bool _built;
        bool _subscribed;
        bool _listening;
        int _width = -1;
        int _height = -1;
        Rect _safe;
        bool _layoutApplied;
        LocalizedString _brandSource;
        LocalizedString _headingSource;
        LocalizedString _statusSource;
        LocalizedString _bodySource;
        LocalizedString _buttonSource;

        public Button HomeButton => _homeButton;
        public string Body => _body != null ? _body.text : string.Empty;

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
            if (_homeButton != null && !_listening)
            {
                _homeButton.onClick.AddListener(OpenHome);
                _listening = true;
            }

            ApplyCurrentScreen();
        }

        void OnDisable()
        {
            if (_homeButton != null && _listening)
            {
                _homeButton.onClick.RemoveListener(OpenHome);
                _listening = false;
            }

            Unsubscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
            DisposeSource(ref _brandSource);
            DisposeSource(ref _headingSource);
            DisposeSource(ref _statusSource);
            DisposeSource(ref _bodySource);
            DisposeSource(ref _buttonSource);
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

        public void OpenHome()
        {
            AppNavigation.ShowHome();
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
            ShellWidgets.Header(_column.Content, font, false, out _brand, out _heading, out _);
            var overview = ShellWidgets.Overview(_column.Content, font, out _status, out _body);
            _homeButton = ShellWidgets.ActionBar(_column.Content, "Home", font, out _buttonLabel);
            _overviewElement = overview.GetComponent<LayoutElement>();
            _buttonElement = _homeButton.GetComponent<LayoutElement>();

            _brandSource = Bind(_brand, ShellContent.Brand);
            _headingSource = Bind(_heading, ShellContent.CompanyHeading);
            _statusSource = Bind(_status, ShellContent.CompanyStatus);
            _bodySource = Bind(_body, ShellContent.CompanyUnconnected);
            _buttonSource = Bind(_buttonLabel, ShellContent.Home);
        }

        public void Apply(float screenWidth, float screenHeight, Rect safeArea)
        {
            BuildIfNeeded();
            var metrics = CompanyShellLayout.Evaluate(screenWidth, screenHeight, safeArea);
            if (_column.Layout != null)
            {
                var pad = Mathf.RoundToInt(metrics.Padding / metrics.CanvasScale);
                _column.Layout.padding = new RectOffset(pad, pad, pad, pad);
                _column.Layout.spacing = ShellTypography.BlockGap;
            }

            ShellWidgets.UseScroll(_column, metrics.Overflow);
            ShellWidgets.SetLine(_brand, metrics.BrandSize, 1);
            ShellWidgets.SetLine(_heading, metrics.HeadingSize, 1);
            ShellWidgets.SetLine(_status, metrics.StatusSize, 1);
            ShellWidgets.SetLine(_body, metrics.BodySize, 2);
            ShellWidgets.SetLine(_buttonLabel, metrics.ButtonLabelSize, 1);
            ShellWidgets.SetBox(_overviewElement, metrics.Notice.height / metrics.CanvasScale);
            ShellWidgets.SetBox(_buttonElement, metrics.ButtonHeight / metrics.CanvasScale);

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

            if (_brandSource != null)
                _brandSource.StringChanged += OnBrand;
            if (_headingSource != null)
                _headingSource.StringChanged += OnHeading;
            if (_statusSource != null)
                _statusSource.StringChanged += OnStatus;
            if (_bodySource != null)
                _bodySource.StringChanged += OnBody;
            if (_buttonSource != null)
                _buttonSource.StringChanged += OnButton;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed)
                return;

            if (_brandSource != null)
                _brandSource.StringChanged -= OnBrand;
            if (_headingSource != null)
                _headingSource.StringChanged -= OnHeading;
            if (_statusSource != null)
                _statusSource.StringChanged -= OnStatus;
            if (_bodySource != null)
                _bodySource.StringChanged -= OnBody;
            if (_buttonSource != null)
                _buttonSource.StringChanged -= OnButton;
            _subscribed = false;
        }

        void OnBrand(string value) { Set(_brand, value); }
        void OnHeading(string value) { Set(_heading, value); }
        void OnStatus(string value) { Set(_status, value); }
        void OnBody(string value) { Set(_body, value); }
        void OnButton(string value) { Set(_buttonLabel, value); }

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
