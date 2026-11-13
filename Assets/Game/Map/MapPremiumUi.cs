using System;
using UnityEngine;
using UnityEngine.UI;

namespace LogistiCore.Map
{
    /// <summary>
    /// Reusable uGUI presentation primitives. No Firebase access, static game data
    /// or fake balances. Call factories at build time, not every Update.
    /// </summary>
    public static class MapPremiumUi
    {
        public static void StyleSurface(Image image, MapPremiumSurface role, bool blocksRaycasts = false)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));
            image.sprite = MapPremiumSprites.Rounded;
            image.type = Image.Type.Sliced;
            image.color = MapPremiumTheme.Surface(role);
            image.raycastTarget = blocksRaycasts;
        }

        public static Image CreateSurface(RectTransform parent, string name, MapPremiumSurface role)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.hideFlags = HideFlags.DontSave;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            StyleSurface(image, role);
            return image;
        }

        public static Button CreateButton(RectTransform parent, string name, bool primary)
        {
            var image = CreateSurface(parent, name, primary ? MapPremiumSurface.Selected : MapPremiumSurface.Button);
            var button = image.gameObject.AddComponent<Button>();
            StyleButton(button, primary);
            return button;
        }

        public static void StyleButton(Button button, bool primary)
        {
            if (button == null)
                throw new ArgumentNullException(nameof(button));
            var image = button.targetGraphic as Image;
            if (image == null)
                image = button.GetComponent<Image>();
            if (image == null)
                throw new InvalidOperationException("Premium buttons need a root Image.");

            StyleSurface(image, primary ? MapPremiumSurface.Selected : MapPremiumSurface.Button, true);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.97f, 1f, 1f);
            colors.pressedColor = new Color(0.74f, 0.88f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.58f, 0.65f, 0.56f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.10f;
            button.colors = colors;
        }

        public static void EnsureTouchTarget(Button button, float width, float height, float canvasScale)
        {
            if (button == null)
                throw new ArgumentNullException(nameof(button));
            float pixels = MapCommandLayout.TouchPixels(width, height);
            float canvas = pixels / Mathf.Max(0.01f, canvasScale);
            var layout = button.GetComponent<LayoutElement>();
            if (layout == null)
                layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = Mathf.Max(layout.minWidth, canvas);
            layout.minHeight = Mathf.Max(layout.minHeight, canvas);
        }

        public static Text CreateText(RectTransform parent, string name, Font font, MapPremiumTextRole role, string value)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.hideFlags = HideFlags.DontSave;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = role == MapPremiumTextRole.Title ? MapPremiumTheme.TitleFont
                : role == MapPremiumTextRole.Caption ? MapPremiumTheme.CaptionFont : MapPremiumTheme.BodyFont;
            text.fontStyle = role == MapPremiumTextRole.Title ? FontStyle.Bold : FontStyle.Normal;
            text.color = role == MapPremiumTextRole.Caption ? MapPremiumTheme.TextSecondary : MapPremiumTheme.TextPrimary;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.text = value ?? "";
            return text;
        }

        public static RectTransform CreateStatusChip(
            RectTransform parent, string name, Font font, string localizedText, MapPremiumStatus status)
        {
            var image = CreateSurface(parent, name, MapPremiumSurface.Chip);
            var label = CreateText((RectTransform)image.transform, "StatusText", font, MapPremiumTextRole.Caption, localizedText);
            label.rectTransform.offsetMin = new Vector2(10f, 2f);
            label.rectTransform.offsetMax = new Vector2(-10f, -2f);
            SetStatusChip(image, label, localizedText, status);
            return (RectTransform)image.transform;
        }

        public static void SetStatusChip(Image image, Text label, string localizedText, MapPremiumStatus status)
        {
            if (image == null || label == null)
                throw new ArgumentNullException(image == null ? nameof(image) : nameof(label));
            image.color = MapPremiumTheme.Chip;
            label.text = localizedText ?? "";
            label.color = MapPremiumTheme.Status(status);
        }
    }
}
