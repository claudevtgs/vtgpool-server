using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VTG.Pool.Localization;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Small runtime uGUI builder shared by the main menu and settings screens (same look as the HUD).
    /// Layout uses a 1920x1080 reference resolution with ScaleWithScreenSize.
    /// Texts that are <see cref="Loc"/> keys are translated and follow live language changes; object names use the
    /// English text so they stay stable (tests, debugging).
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Panel = new Color(0.05f, 0.06f, 0.08f, 0.92f);
        public static readonly Color PanelLight = new Color(0.1f, 0.12f, 0.16f, 0.95f);
        public static readonly Color ButtonColor = new Color(0.16f, 0.19f, 0.25f, 0.96f);
        public static readonly Color Accent = new Color(1f, 0.78f, 0.25f, 1f);
        public static readonly Color Text = new Color(0.95f, 0.95f, 0.93f);
        public static readonly Color Muted = new Color(0.7f, 0.73f, 0.78f);

        private static Font font;
        private static Sprite whiteSprite;

        /// <summary>Plain white sprite: Image.Type.Filled only fills partially when the image has a sprite.</summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    Texture2D texture = Texture2D.whiteTexture;
                    whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    whiteSprite.name = "UiKitWhite";
                }

                return whiteSprite;
            }
        }

        /// <summary>
        /// UI font: Inter (SIL OFL, Resources/Fonts) — embedded so Vietnamese and symbols render everywhere, including
        /// WebGL where there are no system fallback fonts. Falls back to Unity's built-in font.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.Load<Font>("Fonts/Inter-Regular");
                    if (font == null)
                    {
                        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                }

                return font;
            }
        }

        /// <summary>Touch layout wanted: the player setting, or (Auto) a phone/tablet or a touch screen without a mouse.</summary>
        public static bool TouchMode
        {
            get
            {
                int setting = Save.SaveSystem.Settings.touchControls;
                if (setting >= 0)
                {
                    return setting == 1;
                }

                return Application.isMobilePlatform || (Touchscreen.current != null && Mouse.current == null);
            }
        }

        /// <summary>
        /// Desktop: 1920x1080 reference, balanced match. Touch: 1600x900 reference (about 20% larger controls),
        /// matched on height for wide phones and on width for squarer tablets so the layout always fits.
        /// </summary>
        public static void ConfigureScaler(CanvasScaler scaler, bool touch)
        {
            ConfigureScaler(scaler, touch, Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f);
        }

        public static void ConfigureScaler(CanvasScaler scaler, bool touch, float aspect)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if (touch)
            {
                scaler.referenceResolution = new Vector2(1600f, 900f);
                scaler.matchWidthOrHeight = aspect >= 1.76f ? 1f : 0f;
            }
            else
            {
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>Creates an overlay canvas and returns its safe-area content root (see <see cref="ResponsiveCanvas"/>).</summary>
        public static RectTransform CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            EnsureEventSystem();
            var canvasObject = new GameObject(name);
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvasObject.AddComponent<ResponsiveCanvas>().SafeRoot;
        }

        public static RectTransform Rect(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color? color = null)
        {
            var go = color.HasValue ? new GameObject(name, typeof(RectTransform), typeof(Image)) : new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            if (color.HasValue)
            {
                go.GetComponent<Image>().color = color.Value;
            }

            return rect;
        }

        /// <summary>Full-screen rect (optionally tinted) used for overlays and dimmers.</summary>
        public static RectTransform Fill(RectTransform parent, string name, Color? color = null)
        {
            RectTransform rect = Rect(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, color);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            // Overscan: parents are usually the safe area; dimmers should still cover notches and corners.
            rect.offsetMin = new Vector2(-600f, -600f);
            rect.offsetMax = new Vector2(600f, 600f);
            return rect;
        }

        public static Text Label(RectTransform parent, string text, int size, TextAnchor alignment, Vector2 anchor, Vector2 position, Vector2 boxSize,
            Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = boxSize;
            Text label = go.GetComponent<Text>();
            label.font = Font;
            if (Loc.Has(text))
            {
                LocalizedText.Bind(label, text);
            }
            else
            {
                label.text = text;
            }

            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color ?? Text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Button Button(RectTransform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, Action onClick, int fontSize = 24)
        {
            RectTransform rect = Rect(parent, "Button_" + Loc.InEnglish(text), anchor, position, size, ButtonColor);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            Text label = Label(rect, text, fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size, Text, FontStyle.Bold);
            label.name = "Text";
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return button;
        }

        /// <summary>Labelled horizontal slider (0..1 by default) with a live value read-out.</summary>
        public static Slider Slider(RectTransform parent, string title, Vector2 position, float width, float value, Action<float> onChanged,
            float min = 0f, float max = 1f, Func<float, string> format = null)
        {
            format ??= v => Mathf.RoundToInt(v * 100f) + "%";
            RectTransform row = Rect(parent, "Slider_" + Loc.InEnglish(title), new Vector2(0.5f, 1f), position, new Vector2(width, 56f));
            Label(row, title, 22, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(width * 0.38f, 40f));
            Text valueText = Label(row, format(value), 20, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(90f, 40f), Muted);

            float trackWidth = width * 0.62f - 110f;
            RectTransform track = Rect(row, "Track", new Vector2(0f, 0.5f), new Vector2(width * 0.38f, 0f), new Vector2(trackWidth, 10f), new Color(1f, 1f, 1f, 0.15f));
            RectTransform fillArea = Rect(track, "Fill Area", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            RectTransform fill = Rect(fillArea, "Fill", new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, Accent);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            RectTransform handleArea = Rect(track, "Handle Area", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            RectTransform handle = Rect(handleArea, "Handle", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(26f, 26f), Color.white);
            handle.GetComponent<Image>().sprite = BallIconFactory.Circle;

            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v =>
            {
                valueText.text = format(v);
                onChanged?.Invoke(v);
            });
            return slider;
        }

        /// <summary>Single-line text field (uses the on-screen keyboard on phones). The placeholder may be a Loc key.</summary>
        public static InputField Input(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, string text, string placeholder,
            int characterLimit = 64, int fontSize = 24)
        {
            RectTransform rect = Rect(parent, "Input_" + name, anchor, position, size, new Color(0.02f, 0.03f, 0.05f, 0.95f));
            Text hint = Label(rect, placeholder, fontSize, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(28f, 8f), new Color(1f, 1f, 1f, 0.3f));
            hint.name = "Placeholder";
            hint.fontStyle = FontStyle.Italic;
            Text value = Label(rect, string.Empty, fontSize, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(28f, 8f), Text);
            value.name = "Text";
            value.supportRichText = false;
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            InputField field = rect.gameObject.AddComponent<InputField>();
            field.textComponent = value;
            field.placeholder = hint;
            field.characterLimit = characterLimit;
            field.lineType = InputField.LineType.SingleLine;
            field.text = text ?? string.Empty;
            field.caretWidth = 3;
            field.selectionColor = new Color(1f, 0.78f, 0.25f, 0.4f);
            return field;
        }

        /// <summary>Row with a title and a button that cycles through named options (options may be Loc keys).</summary>
        public static Button Selector(RectTransform parent, string title, Vector2 position, float width, string[] options, int index, Action<int> onChanged)
        {
            RectTransform row = Rect(parent, "Selector_" + Loc.InEnglish(title), new Vector2(0.5f, 1f), position, new Vector2(width, 56f));
            Label(row, title, 22, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(width * 0.38f, 40f));
            int current = Mathf.Clamp(index, 0, options.Length - 1);
            Button button = Button(row, options[current], new Vector2(1f, 0.5f), Vector2.zero, new Vector2(width * 0.6f, 46f), null, 20);
            Text text = button.GetComponentInChildren<Text>();
            LocalizedText.Bind(text, () => Loc.T(options[current]));
            button.onClick.AddListener(() =>
            {
                current = (current + 1) % options.Length;
                text.text = Loc.T(options[current]);
                onChanged?.Invoke(current);
            });
            return button;
        }
    }
}
