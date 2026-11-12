using LogistiCore.Backend;
using LogistiCore.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// Premium command-header clusters. Uses trusted economy only. No invented
    /// reputation, XP requirement, clock, weather, or notification badges.
    /// </summary>
    public sealed class MapTopHud
    {
        public const string LevelRootName = "HudLevel";
        public const string BalanceRootName = "HudBalance";
        public const string ReputationRootName = "HudReputation";
        public const string RuleName = "HudRule";

        RectTransform _levelRoot;
        RectTransform _balanceRoot;
        RectTransform _reputationRoot;
        RectTransform _rule;
        RectTransform _xpTrack;
        RectTransform _xpFill;
        Image _xpFillImage;
        Text _levelValue;
        Text _levelLabel;
        Text _xpLabel;
        Text _balanceText;
        Text _reputationLabel;
        Text _reputationValue;
        string _levelCopy = "";
        string _xpCopy = "";
        string _loadingCopy = "";
        string _unavailableCopy = "";
        string _reputationCopy = "";

        public Text BalanceText => _balanceText;
        public Text ProgressText => _xpLabel;
        public bool Intact =>
            _levelRoot != null && _balanceRoot != null && _reputationRoot != null
            && _balanceText != null && _xpLabel != null && _levelValue != null;

        public static MapTopHud Build(RectTransform header, Font font)
        {
            var hud = new MapTopHud();
            hud._rule = Hairline(header, RuleName);
            hud._levelRoot = Cluster(header, LevelRootName);
            hud._balanceRoot = Cluster(header, BalanceRootName);
            hud._reputationRoot = Cluster(header, ReputationRootName);

            Icon(hud._levelRoot, "LevelBadge", MapHudIcons.Get(MapHudIcons.Badge), MapHudPalette.Accent);
            hud._levelValue = Label(hud._levelRoot, "LevelValue", font, FontStyle.Bold, MapHudPalette.Text);
            hud._levelValue.alignment = TextAnchor.MiddleCenter;
            hud._levelLabel = Label(hud._levelRoot, "LevelLabel", font, FontStyle.Normal, MapHudPalette.TextMuted);
            hud._xpLabel = Label(hud._levelRoot, "Progress", font, FontStyle.Normal, MapHudPalette.TextMuted);
            hud._xpTrack = Bar(hud._levelRoot, "XpTrack", new Color(0.04f, 0.08f, 0.12f, 1f));
            hud._xpFill = Bar(hud._levelRoot, "XpFill", MapHudPalette.Accent);
            hud._xpFillImage = hud._xpFill.GetComponent<Image>();
            hud._xpFillImage.type = Image.Type.Filled;
            hud._xpFillImage.fillMethod = Image.FillMethod.Horizontal;
            hud._xpFillImage.fillAmount = 0f;
            hud._xpTrack.gameObject.SetActive(false);
            hud._xpFill.gameObject.SetActive(false);

            Icon(hud._balanceRoot, "BalanceIcon", MapHudIcons.Get(MapHudIcons.Wallet), MapHudPalette.Accent);
            hud._balanceText = Label(hud._balanceRoot, "Economy", font, FontStyle.Bold, MapHudPalette.Text);
            hud._balanceText.horizontalOverflow = HorizontalWrapMode.Overflow;

            hud._reputationLabel = Label(hud._reputationRoot, "ReputationLabel", font, FontStyle.Normal, MapHudPalette.TextMuted);
            hud._reputationValue = Label(hud._reputationRoot, "ReputationValue", font, FontStyle.Bold, MapHudPalette.Text);
            hud._reputationRoot.gameObject.SetActive(false);
            return hud;
        }

        public bool Owns(Text text)
        {
            if (text == null)
                return false;
            return text == _balanceText || text == _xpLabel || text == _levelLabel
                || text == _levelValue || text == _reputationLabel || text == _reputationValue;
        }

        public void BindCopy(string level, string xp, string loading, string unavailable, string reputation)
        {
            _levelCopy = level ?? "";
            _xpCopy = xp ?? "";
            _loadingCopy = loading ?? "";
            _unavailableCopy = unavailable ?? "";
            _reputationCopy = reputation ?? "";
        }

        public void Present()
        {
            if (!Intact)
                return;

            if (!MapCompanyStore.HasTrusted)
            {
                var hold = MapCompanyEconomyRead.InFlight
                    ? FirstNonEmpty(_loadingCopy, "—")
                    : FirstNonEmpty(_unavailableCopy, "—");
                _balanceText.text = hold;
                _levelValue.text = "—";
                _levelLabel.text = FirstNonEmpty(_levelCopy, "");
                _xpLabel.text = "";
                _xpTrack.gameObject.SetActive(false);
                _xpFill.gameObject.SetActive(false);
                _reputationRoot.gameObject.SetActive(false);
                return;
            }

            var economy = MapCompanyStore.Economy;
            _balanceText.text = FormatBalance(economy);
            if (economy.Level > 0)
            {
                _levelValue.text = economy.Level.ToString();
                _levelLabel.text = Join(_levelCopy, economy.Level.ToString());
            }
            else
            {
                _levelValue.text = "—";
                _levelLabel.text = FirstNonEmpty(_unavailableCopy, _levelCopy);
            }

            _xpLabel.text = Join(_xpCopy, economy.Xp.ToString());
            _xpTrack.gameObject.SetActive(false);
            _xpFill.gameObject.SetActive(false);
            _xpFillImage.fillAmount = 0f;
            _reputationRoot.gameObject.SetActive(false);
            if (_reputationLabel != null)
                _reputationLabel.text = _reputationCopy;
            if (_reputationValue != null)
                _reputationValue.text = FirstNonEmpty(_unavailableCopy, "—");
        }

        public const int MinReadableFont = 12;

        public void Layout(
            RectTransform header,
            RectTransform regionChip,
            float leftPad,
            float rightPad,
            float topPad,
            MapCommandLayout.Metrics metrics,
            float scale,
            float trailingReserve)
        {
            if (!Intact || header == null)
                return;

            Present();
            var headerW = header.rect.width;
            if (headerW < 8f)
                headerW = metrics.Header.width / Mathf.Max(0.01f, scale);
            var safeLeft = Mathf.Max(10f, leftPad);
            var trailing = Mathf.Max(trailingReserve, 8f);
            var safeRight = headerW - Mathf.Max(10f, rightPad) - trailing;
            var start = safeLeft;
            if (regionChip != null && regionChip.gameObject.activeSelf)
                start = regionChip.anchoredPosition.x + regionChip.sizeDelta.x + 16f;
            if (start < safeLeft)
                start = safeLeft;
            if (safeRight < start + 48f)
                safeRight = Mathf.Min(headerW - 8f, start + 48f);

            var inner = Mathf.Max(22f, header.rect.height - topPad - 4f);
            var icon = Mathf.Clamp(inner * 0.5f, 16f, 22f);
            var badge = Mathf.Clamp(inner * 0.72f, 20f, 28f);
            var title = Mathf.Max(MinReadableFont, metrics.CanvasFont);
            var body = Mathf.Max(MinReadableFont, Mathf.RoundToInt(metrics.CanvasFont * 0.92f));
            SetFont(_balanceText, title);
            SetFont(_levelValue, body);
            SetFont(_levelLabel, body);
            SetFont(_xpLabel, Mathf.Max(MinReadableFont, body - 1));
            SetFont(_reputationLabel, body);
            SetFont(_reputationValue, body);
            _balanceText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _xpLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _levelLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            var room = Mathf.Max(0f, safeRight - start);
            var stacked = inner >= 30f && room >= 420f;
            _reputationRoot.gameObject.SetActive(false);

            var pad = 8f;
            var balanceW = MeasureBalance(icon, pad);
            balanceW = FitBalanceFont(icon, pad, room, title, balanceW);

            var showCopy = stacked
                ? room >= balanceW + 180f
                : room >= balanceW + 220f;
            var showXp = room >= balanceW + 160f;
            var showLevel = room >= balanceW + badge + 48f && (showCopy || showXp);
            _levelLabel.gameObject.SetActive(showLevel && showCopy);
            _xpLabel.gameObject.SetActive(showLevel && showXp);
            if (stacked && showLevel)
            {
                _levelLabel.gameObject.SetActive(true);
                _xpLabel.gameObject.SetActive(true);
            }

            var levelW = 0f;
            if (showLevel)
            {
                levelW = badge + pad;
                if (stacked)
                {
                    var copyW = Mathf.Max(
                        _levelLabel.gameObject.activeSelf ? _levelLabel.preferredWidth : 0f,
                        _xpLabel.gameObject.activeSelf ? _xpLabel.preferredWidth : 0f);
                    levelW += Mathf.Max(36f, copyW + 8f);
                }
                else
                {
                    if (_levelLabel.gameObject.activeSelf)
                        levelW += Mathf.Max(28f, _levelLabel.preferredWidth + 8f);
                    if (_xpLabel.gameObject.activeSelf)
                        levelW += Mathf.Max(24f, _xpLabel.preferredWidth + 6f);
                }

                if (balanceW + 14f + levelW > room
                    || (!_levelLabel.gameObject.activeSelf && !_xpLabel.gameObject.activeSelf))
                {
                    showLevel = false;
                    levelW = 0f;
                }
            }

            HideLevelGroup(showLevel);

            var balanceX = safeRight - balanceW;
            if (balanceX < start)
                balanceX = start;
            Place(_balanceRoot, balanceX, balanceW, inner, topPad);
            LayoutBalance(icon, inner, pad);

            if (showLevel)
            {
                Place(_levelRoot, start, levelW, inner, topPad);
                LayoutLevel(badge, inner, pad, stacked);
                var ruleX = start + levelW + 6f;
                Place(_rule, ruleX, 1f, inner * 0.56f, topPad + inner * 0.22f);
                _rule.gameObject.SetActive(ruleX + 8f < balanceX);
            }
            else
                HideRule();
        }

        public static float MeasureTrailing(
            RectTransform header,
            MapCommandLayout.Metrics metrics,
            float scale,
            Canvas canvas)
        {
            if (header == null)
                return 8f;
            if (scale < 0.0001f)
                scale = 1f;

            var headerW = header.rect.width;
            if (headerW < 1f)
                headerW = metrics.Header.width / scale;
            var fromHome = 0f;
            if (metrics.Home.width > 1f)
            {
                var homeLocal = (metrics.Home.xMin - metrics.Header.xMin) / scale;
                fromHome = Mathf.Max(0f, headerW - homeLocal);
            }

            var fromLocale = 0f;
            var root = canvas != null ? canvas.transform : header.root;
            if (root != null)
            {
                var headerWorld = World(header);
                var texts = root.GetComponentsInChildren<Text>(true);
                for (var i = 0; i < texts.Length; i++)
                {
                    var text = texts[i];
                    if (text == null || !text.gameObject.activeInHierarchy || !IsLocaleChrome(text))
                        continue;
                    var target = text.rectTransform;
                    var button = text.GetComponentInParent<Button>();
                    if (button != null)
                        target = (RectTransform)button.transform;
                    var box = World(target);
                    if (box.yMax < headerWorld.yMin + 0.5f || box.yMin > headerWorld.yMax - 0.5f)
                        continue;
                    if (box.xMax < headerWorld.xMin + 8f)
                        continue;
                    var local = header.InverseTransformPoint(new Vector3(box.xMin, box.center.y, 0f)).x;
                    fromLocale = Mathf.Max(fromLocale, headerW - local + 8f);
                }
            }

#if UNITY_EDITOR
            var editorMenu = MapCommandLayout.TextWidth("Turkish", Mathf.Max(12f, metrics.FontPixels)) / scale + 16f;
            fromLocale = Mathf.Max(fromLocale, editorMenu);
#endif
            return Mathf.Max(fromHome, fromLocale, 8f);
        }

        static bool IsLocaleChrome(Text text)
        {
            if (text == null)
                return false;
            var name = text.gameObject.name ?? "";
            var parent = text.transform.parent != null ? text.transform.parent.name : "";
            if (ContainsLocaleName(name) || ContainsLocaleName(parent))
                return true;
            var value = text.text ?? "";
            return value == "Turkish" || value == "English" || value == "German"
                || value == "Türkçe" || value == "Deutsch";
        }

        static bool ContainsLocaleName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            return name.IndexOf("Locale", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Language", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool Overlaps(RectTransform a, RectTransform b)
        {
            if (a == null || b == null || !a.gameObject.activeInHierarchy || !b.gameObject.activeInHierarchy)
                return false;
            var left = World(a);
            var right = World(b);
            return left.xMin < right.xMax - 0.5f && left.xMax > right.xMin + 0.5f
                && left.yMin < right.yMax - 0.5f && left.yMax > right.yMin + 0.5f;
        }

        float MeasureBalance(float icon, float pad)
        {
            return pad + icon + 8f + Mathf.Max(48f, _balanceText.preferredWidth + 6f) + pad;
        }

        float FitBalanceFont(float icon, float pad, float room, int startSize, float currentWidth)
        {
            var size = startSize;
            var width = currentWidth;
            while (size > MinReadableFont && width > room)
            {
                size--;
                SetFont(_balanceText, size);
                width = MeasureBalance(icon, pad);
            }

            return width;
        }

        void LayoutLevel(float badge, float inner, float pad, bool stacked)
        {
            var badgeRect = _levelRoot.Find("LevelBadge") as RectTransform;
            if (badgeRect != null)
            {
                badgeRect.anchorMin = new Vector2(0f, 0.5f);
                badgeRect.anchorMax = new Vector2(0f, 0.5f);
                badgeRect.pivot = new Vector2(0f, 0.5f);
                badgeRect.sizeDelta = new Vector2(badge, badge);
                badgeRect.anchoredPosition = new Vector2(pad, 0f);
            }

            _levelValue.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _levelValue.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _levelValue.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _levelValue.rectTransform.sizeDelta = new Vector2(badge, badge);
            _levelValue.rectTransform.anchoredPosition = new Vector2(pad + badge * 0.5f, 0f);

            var x = pad + badge + 8f;
            if (stacked)
            {
                var w = Mathf.Max(
                    _levelLabel.gameObject.activeSelf ? _levelLabel.preferredWidth + 2f : 24f,
                    _xpLabel.gameObject.activeSelf ? _xpLabel.preferredWidth + 2f : 24f);
                if (_levelLabel.gameObject.activeSelf)
                    Band(_levelLabel, x, w, inner * 0.46f, inner * 0.18f);
                if (_xpLabel.gameObject.activeSelf)
                    Band(_xpLabel, x, w, inner * 0.42f, -inner * 0.16f);
                return;
            }

            if (_levelLabel.gameObject.activeSelf)
            {
                var w = Mathf.Max(24f, _levelLabel.preferredWidth + 2f);
                Band(_levelLabel, x, w, inner, 0f);
                x += w + 10f;
            }

            if (_xpLabel.gameObject.activeSelf)
            {
                var w = Mathf.Max(20f, _xpLabel.preferredWidth + 2f);
                Band(_xpLabel, x, w, inner, 0f);
            }
        }

        void LayoutBalance(float icon, float inner, float pad)
        {
            var iconRect = _balanceRoot.Find("BalanceIcon") as RectTransform;
            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.sizeDelta = new Vector2(icon, icon);
                iconRect.anchoredPosition = new Vector2(pad, 0f);
            }

            Band(_balanceText, pad + icon + 8f, Mathf.Max(48f, _balanceText.preferredWidth + 6f), inner, 0f);
        }

        static void Band(Text text, float x, float width, float height, float y)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        void HideLevelGroup(bool show)
        {
            if (_levelRoot != null)
            {
                _levelRoot.gameObject.SetActive(show);
                if (!show)
                {
                    _levelRoot.sizeDelta = new Vector2(0f, _levelRoot.sizeDelta.y);
                    _levelRoot.anchoredPosition = new Vector2(-1000f, _levelRoot.anchoredPosition.y);
                }
            }

            if (_levelLabel != null)
                _levelLabel.gameObject.SetActive(show && _levelLabel.gameObject.activeSelf);
            if (_xpLabel != null)
                _xpLabel.gameObject.SetActive(show && _xpLabel.gameObject.activeSelf);
            var badge = _levelRoot != null ? _levelRoot.Find("LevelBadge") : null;
            if (badge != null)
                badge.gameObject.SetActive(show);
            if (_levelValue != null)
                _levelValue.gameObject.SetActive(show);
            if (!show)
                HideRule();
        }

        void HideRule()
        {
            if (_rule == null)
                return;
            _rule.gameObject.SetActive(false);
            _rule.sizeDelta = new Vector2(0f, 0f);
        }

        static void Place(RectTransform rect, float x, float width, float height, float topPad)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(1f, width), -(topPad + 4f));
            rect.anchoredPosition = new Vector2(x, -topPad * 0.15f);
        }

        static string FormatBalance(ClientEconomyState economy)
        {
            if (!economy.HasBalance)
                return "—";
            var sep = LocaleService.CurrentCode == LocaleCodes.English ? "." : ",";
            string display;
            if (!MapJobsMoney.TryFormat(economy.Balance, economy.Currency, economy.Scale, sep, out display))
                return "—";
            return display;
        }

        static string Join(string label, string value)
        {
            if (string.IsNullOrEmpty(label))
                return value ?? "";
            if (string.IsNullOrEmpty(value))
                return label;
            return label + " " + value;
        }

        static string FirstNonEmpty(string a, string b)
        {
            return string.IsNullOrEmpty(a) ? b ?? "" : a;
        }

        static void SetFont(Text text, int size)
        {
            if (text != null)
                text.fontSize = size;
        }

        static RectTransform Cluster(RectTransform parent, string name)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            image.color = new Color(0.035f, 0.07f, 0.12f, 1f);
            image.raycastTarget = false;
            return (RectTransform)item.transform;
        }

        static RectTransform Hairline(RectTransform parent, string name)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            image.color = MapHudPalette.Rule;
            image.raycastTarget = false;
            return (RectTransform)item.transform;
        }

        static RectTransform Bar(RectTransform parent, string name, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return (RectTransform)item.transform;
        }

        static void Icon(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
        }

        static Text Label(RectTransform parent, string name, Font font, FontStyle style, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>();
            text.font = font;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = string.Empty;
            return text;
        }

        static Rect World(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var minX = corners[0].x;
            var minY = corners[0].y;
            var maxX = corners[0].x;
            var maxY = corners[0].y;
            for (var i = 1; i < 4; i++)
            {
                minX = Mathf.Min(minX, corners[i].x);
                minY = Mathf.Min(minY, corners[i].y);
                maxX = Mathf.Max(maxX, corners[i].x);
                maxY = Mathf.Max(maxY, corners[i].y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
    }
}
