using UnityEngine;
using UnityEngine.UI;

namespace LogistiCore.UI
{
    /// <summary>
    /// Shared uGUI pieces for the portrait shells. Decorative graphics do not take touches.
    /// </summary>
    public static class ShellWidgets
    {
        public readonly struct Column
        {
            public readonly RectTransform Viewport;
            public readonly RectTransform Content;
            public readonly VerticalLayoutGroup Layout;
            public readonly ScrollRect Scroll;
            public readonly RectMask2D Mask;
            public readonly ContentSizeFitter Fitter;

            public Column(
                RectTransform viewport,
                RectTransform content,
                VerticalLayoutGroup layout,
                ScrollRect scroll,
                RectMask2D mask,
                ContentSizeFitter fitter)
            {
                Viewport = viewport;
                Content = content;
                Layout = layout;
                Scroll = scroll;
                Mask = mask;
                Fitter = fitter;
            }
        }

        public static Font Builtin()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        public static Column ScrollColumn(Transform safe)
        {
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
            viewportObject.transform.SetParent(safe, false);
            Mark(viewportObject);
            var viewport = (RectTransform)viewportObject.transform;
            Stretch(viewport);
            var scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 24f;
            var mask = viewportObject.GetComponent<RectMask2D>();
            mask.enabled = false;

            var contentObject = new GameObject("Column", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            Mark(contentObject);
            var content = (RectTransform)contentObject.transform;
            Stretch(content);
            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.enabled = false;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return new Column(viewport, content, layout, scroll, mask, fitter);
        }

        public static void UseScroll(Column column, bool overflow)
        {
            column.Scroll.vertical = overflow;
            column.Scroll.horizontal = false;
            column.Mask.enabled = overflow;
            column.Fitter.enabled = overflow;
            if (overflow)
            {
                column.Content.anchorMin = new Vector2(0f, 1f);
                column.Content.anchorMax = new Vector2(1f, 1f);
                column.Content.pivot = new Vector2(0.5f, 1f);
                column.Content.anchoredPosition = Vector2.zero;
                column.Content.sizeDelta = new Vector2(0f, 0f);
            }
            else
            {
                Stretch(column.Content);
            }
        }

        public static RectTransform Header(
            Transform parent,
            Font font,
            bool withSupport,
            out Text brand,
            out Text heading,
            out Text support)
        {
            var block = new GameObject("Header", typeof(RectTransform), typeof(VerticalLayoutGroup));
            block.transform.SetParent(parent, false);
            Mark(block);
            var layout = block.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = ShellTypography.HeaderGap;
            var brandRow = new GameObject("BrandRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            brandRow.transform.SetParent(block.transform, false);
            Mark(brandRow);
            var brandLayout = brandRow.GetComponent<HorizontalLayoutGroup>();
            brandLayout.childAlignment = TextAnchor.MiddleLeft;
            brandLayout.childControlWidth = true;
            brandLayout.childControlHeight = true;
            brandLayout.childForceExpandWidth = false;
            brandLayout.childForceExpandHeight = false;
            brandLayout.spacing = 12f;
            brandRow.GetComponent<LayoutElement>().flexibleWidth = 1f;
            var mark = new GameObject("BrandMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            mark.transform.SetParent(brandRow.transform, false);
            Mark(mark);
            var markImage = mark.GetComponent<Image>();
            markImage.color = ShellPalette.Accent;
            markImage.raycastTarget = false;
            var markElement = mark.GetComponent<LayoutElement>();
            markElement.preferredWidth = 14f;
            markElement.preferredHeight = 14f;
            markElement.minWidth = 14f;
            markElement.minHeight = 14f;
            markElement.flexibleWidth = 0f;
            markElement.flexibleHeight = 0f;
            brand = Text(brandRow.transform, "Brand", font, FontStyle.Bold, ShellPalette.Accent, TextAnchor.MiddleLeft);
            heading = Text(block.transform, "Heading", font, FontStyle.Bold, ShellPalette.Text, TextAnchor.UpperLeft);
            var rule = Rule(block.transform, 210f);
            rule.GetComponent<Image>().color = new Color(ShellPalette.Accent.r, ShellPalette.Accent.g, ShellPalette.Accent.b, 0.72f);
            support = null;
            if (withSupport)
            {
                support = Text(block.transform, "Support", font, FontStyle.Normal, ShellPalette.TextMuted, TextAnchor.UpperLeft);
                support.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            return (RectTransform)block.transform;
        }

        public static Button FeatureCard(
            Transform parent,
            string name,
            Font font,
            bool mapMotif,
            Color face,
            Color border,
            out Text title,
            out Text caption)
        {
            var button = Face(parent, name, face, out var root);
            Surface(root, mapMotif, border);
            var row = Row(root);
            Motif(row, mapMotif, ShellTypography.Motif);
            var copy = new GameObject("Copy", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            copy.transform.SetParent(row, false);
            Mark(copy);
            var copyLayout = copy.GetComponent<VerticalLayoutGroup>();
            copyLayout.childAlignment = TextAnchor.MiddleLeft;
            copyLayout.childControlWidth = true;
            copyLayout.childControlHeight = true;
            copyLayout.childForceExpandWidth = true;
            copyLayout.childForceExpandHeight = false;
            copyLayout.spacing = 8f;
            var copyElement = copy.GetComponent<LayoutElement>();
            copyElement.flexibleWidth = 1f;
            title = Text(copy.transform, "Title", font, FontStyle.Bold, ShellPalette.Text, TextAnchor.MiddleLeft);
            caption = Text(copy.transform, "Caption", font, FontStyle.Normal, ShellPalette.TextSecondary, TextAnchor.MiddleLeft);
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            return button;
        }

        public static RectTransform Overview(Transform parent, Font font, out Text status, out Text body)
        {
            var frameObject = new GameObject(
                "Overview",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement));
            frameObject.transform.SetParent(parent, false);
            Mark(frameObject);
            var face = frameObject.GetComponent<Image>();
            face.color = ShellPalette.Card;
            face.raycastTarget = false;
            var element = frameObject.GetComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            Surface((RectTransform)frameObject.transform, false, ShellPalette.Border);
            var inner = new GameObject("Inner", typeof(RectTransform), typeof(VerticalLayoutGroup));
            inner.transform.SetParent(frameObject.transform, false);
            Mark(inner);
            Stretch((RectTransform)inner.transform);
            var layout = inner.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 14f;
            layout.padding = new RectOffset(28, 28, 28, 28);
            Motif((RectTransform)inner.transform, false, 112f);
            status = Text(inner.transform, "Status", font, FontStyle.Bold, ShellPalette.Text, TextAnchor.MiddleCenter);
            var rule = Rule(inner.transform, 128f);
            rule.GetComponent<Image>().color = new Color(ShellPalette.Accent.r, ShellPalette.Accent.g, ShellPalette.Accent.b, 0.78f);
            body = Text(inner.transform, "Body", font, FontStyle.Normal, ShellPalette.TextMuted, TextAnchor.MiddleCenter);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.alignment = TextAnchor.MiddleCenter;
            return (RectTransform)frameObject.transform;
        }

        public static Button ActionBar(Transform parent, string name, Font font, out Text label)
        {
            var button = Face(parent, name, ShellPalette.Action, out var root);
            var colors = button.colors;
            colors.normalColor = ShellPalette.Action;
            colors.highlightedColor = ShellPalette.Highlighted;
            colors.pressedColor = ShellPalette.Pressed;
            colors.selectedColor = ShellPalette.Highlighted;
            button.colors = colors;
            Surface(root, true, ShellPalette.BorderStrong);
            var cluster = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            cluster.childAlignment = TextAnchor.MiddleCenter;
            cluster.childControlWidth = true;
            cluster.childControlHeight = true;
            cluster.childForceExpandWidth = false;
            cluster.childForceExpandHeight = false;
            cluster.spacing = 14f;
            var glyph = new GameObject("ReturnMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            glyph.transform.SetParent(root, false);
            Mark(glyph);
            var glyphImage = glyph.GetComponent<Image>();
            glyphImage.color = ShellPalette.Accent;
            glyphImage.raycastTarget = false;
            var glyphElement = glyph.GetComponent<LayoutElement>();
            glyphElement.preferredWidth = 18f;
            glyphElement.preferredHeight = 18f;
            glyphElement.minWidth = 18f;
            glyphElement.minHeight = 18f;
            glyphElement.flexibleWidth = 0f;
            glyphElement.flexibleHeight = 0f;
            glyphElement.ignoreLayout = false;
            label = Text(root, "Label", font, FontStyle.Bold, ShellPalette.Text, TextAnchor.MiddleCenter);
            var labelElement = label.GetComponent<LayoutElement>();
            labelElement.flexibleWidth = 0f;
            labelElement.ignoreLayout = false;
            return button;
        }

        public static void SetLine(Text text, int size, int lines)
        {
            if (text == null)
                return;

            text.fontSize = size;
            text.resizeTextForBestFit = false;
            var element = text.GetComponent<LayoutElement>();
            if (element == null)
                return;

            var height = size * Mathf.Max(1, lines) + 4f;
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
        }

        public static void SetBox(LayoutElement element, float canvasHeight)
        {
            if (element == null)
                return;

            element.minHeight = canvasHeight;
            element.preferredHeight = canvasHeight;
            element.flexibleHeight = 0f;
            element.flexibleWidth = 1f;
        }

        static Button Face(Transform parent, string name, Color face, out RectTransform root)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            Mark(buttonObject);
            root = (RectTransform)buttonObject.transform;
            var image = buttonObject.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = face;
            colors.highlightedColor = ShellPalette.Highlighted;
            colors.pressedColor = ShellPalette.Pressed;
            colors.selectedColor = ShellPalette.Highlighted;
            colors.disabledColor = face;
            colors.fadeDuration = 0.08f;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            var element = buttonObject.GetComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            return button;
        }

        static RectTransform Row(RectTransform root)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(root, false);
            Mark(row);
            Stretch((RectTransform)row.transform);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 16f;
            layout.padding = new RectOffset(
                Mathf.RoundToInt(ShellTypography.CardPad),
                Mathf.RoundToInt(ShellTypography.CardPad),
                Mathf.RoundToInt(ShellTypography.CardPad),
                Mathf.RoundToInt(ShellTypography.CardPad));
            return (RectTransform)row.transform;
        }

        static void Motif(RectTransform parent, bool map, float preferredHeight)
        {
            var host = new GameObject("Motif", typeof(RectTransform), typeof(LayoutElement));
            host.transform.SetParent(parent, false);
            Mark(host);
            var element = host.GetComponent<LayoutElement>();
            element.preferredWidth = ShellTypography.Motif;
            element.minWidth = ShellTypography.Motif;
            element.flexibleWidth = 0f;
            if (preferredHeight > 0f)
            {
                element.preferredHeight = preferredHeight;
                element.minHeight = preferredHeight;
                element.flexibleHeight = 0f;
            }

            var rect = (RectTransform)host.transform;
            var well = new GameObject("Well", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            well.transform.SetParent(rect, false);
            Mark(well);
            Stretch((RectTransform)well.transform);
            var wellImage = well.GetComponent<Image>();
            wellImage.color = ShellPalette.Well;
            wellImage.raycastTarget = false;
            if (map)
            {
                Bar(rect, 0.12f, 0.78f, 0.34f, 0.88f, ShellPalette.Accent);
                Bar(rect, 0.12f, 0.62f, 0.22f, 0.88f, ShellPalette.Accent);
                Bar(rect, 0.66f, 0.12f, 0.88f, 0.22f, ShellPalette.Accent);
                Bar(rect, 0.78f, 0.12f, 0.88f, 0.34f, ShellPalette.Accent);
                Bar(rect, 0.22f, 0.28f, 0.40f, 0.46f, ShellPalette.Accent);
                Bar(rect, 0.34f, 0.32f, 0.62f, 0.42f, ShellPalette.Accent);
                Bar(rect, 0.52f, 0.36f, 0.62f, 0.64f, ShellPalette.Accent);
                Bar(rect, 0.48f, 0.58f, 0.70f, 0.80f, ShellPalette.Text);
            }
            else
            {
                Bar(rect, 0.16f, 0.16f, 0.84f, 0.34f, ShellPalette.AccentQuiet);
                Bar(rect, 0.34f, 0.34f, 0.66f, 0.66f, ShellPalette.Accent);
                Bar(rect, 0.24f, 0.66f, 0.76f, 0.80f, ShellPalette.Accent);
                Bar(rect, 0.42f, 0.80f, 0.58f, 0.90f, ShellPalette.Accent);
                Bar(rect, 0.44f, 0.44f, 0.56f, 0.56f, ShellPalette.Well);
            }
        }

        static void Surface(RectTransform root, bool primary, Color side)
        {
            Plate(root, primary ? Lift(ShellPalette.CardRaised, 0.035f) : Lift(ShellPalette.Card, 0.03f), 8f);
            Edge(root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f), primary ? ShellPalette.BorderStrong : new Color(1f, 1f, 1f, 0.10f));
            Edge(root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), ShellPalette.EdgeShadow);
            Edge(root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(primary ? 5f : 2f, 0f), primary ? ShellPalette.Accent : side);
            Edge(root, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f), side);
        }

        static void Plate(RectTransform root, Color color, float inset)
        {
            var item = new GameObject("Plate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            item.transform.SetParent(root, false);
            Mark(item);
            var rect = (RectTransform)item.transform;
            Stretch(rect);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            var image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            item.GetComponent<LayoutElement>().ignoreLayout = true;
        }

        static Color Lift(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r + amount),
                Mathf.Clamp01(color.g + amount),
                Mathf.Clamp01(color.b + amount),
                1f);
        }

        static void Bar(RectTransform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var item = new GameObject("Mark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            Mark(item);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static void Edge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Color color)
        {
            var item = new GameObject("Edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            Mark(item);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            var image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var element = item.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }

        public static Text Text(Transform parent, string name, Font font, FontStyle style, Color color, TextAnchor anchor)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            item.transform.SetParent(parent, false);
            Mark(item);
            var text = item.GetComponent<Text>();
            text.font = font;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
            text.text = string.Empty;
            var element = item.GetComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            return text;
        }

        public static RectTransform Rule(Transform parent, float width = 0f)
        {
            var item = new GameObject("Rule", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            item.transform.SetParent(parent, false);
            Mark(item);
            var image = item.GetComponent<Image>();
            image.color = ShellPalette.Rule;
            image.raycastTarget = false;
            var element = item.GetComponent<LayoutElement>();
            element.minHeight = ShellTypography.Rule;
            element.preferredHeight = ShellTypography.Rule;
            element.flexibleHeight = 0f;
            if (width > 0f)
            {
                element.preferredWidth = width;
                element.minWidth = width;
                element.flexibleWidth = 0f;
            }
            else
            {
                element.flexibleWidth = 1f;
            }

            return (RectTransform)item.transform;
        }

        public static RectTransform Notice(Transform parent, Font font, out Text body)
        {
            var frame = new GameObject(
                "Notice",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            frame.transform.SetParent(parent, false);
            Mark(frame);
            var image = frame.GetComponent<Image>();
            image.color = ShellPalette.NoticeFill;
            image.raycastTarget = false;
            var layout = frame.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 14f;
            layout.padding = new RectOffset(18, 18, 16, 16);
            var element = frame.GetComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            var pip = new GameObject("Pip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            pip.transform.SetParent(frame.transform, false);
            Mark(pip);
            var pipImage = pip.GetComponent<Image>();
            pipImage.color = ShellPalette.AccentQuiet;
            pipImage.raycastTarget = false;
            var pipElement = pip.GetComponent<LayoutElement>();
            pipElement.preferredWidth = 10f;
            pipElement.preferredHeight = 10f;
            pipElement.minWidth = 10f;
            pipElement.minHeight = 10f;
            pipElement.flexibleWidth = 0f;
            pipElement.flexibleHeight = 0f;
            body = Text(frame.transform, "Body", font, FontStyle.Normal, ShellPalette.TextMuted, TextAnchor.MiddleLeft);
            body.GetComponent<LayoutElement>().flexibleWidth = 1f;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            return (RectTransform)frame.transform;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        public static void Mark(GameObject item)
        {
            item.hideFlags = HideFlags.DontSave;
        }
    }
}
