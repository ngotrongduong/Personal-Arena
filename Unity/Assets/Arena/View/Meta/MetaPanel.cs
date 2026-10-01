using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>
    /// Base of the full-screen M5 panels (character, compare, Auto Farm): a dimmed overlay with one card, a title
    /// and a close button, built at runtime under the HUD canvas (same look as <see cref="BehaviorProfilePanel"/>).
    /// Escape closes the panel, except while an input field has focus (then Escape only leaves the field).
    /// </summary>
    public abstract class MetaPanel : MonoBehaviour
    {
        protected static readonly Color Good = new Color(0.36f, 0.88f, 0.5f, 1f);
        protected static readonly Color Warn = new Color(1f, 0.72f, 0.28f, 1f);
        protected static readonly Color Bad = new Color(1f, 0.42f, 0.42f, 1f);
        protected static readonly Color Muted = new Color(0.66f, 0.72f, 0.81f, 1f);
        protected static readonly Color Panel = new Color(0.085f, 0.1f, 0.135f, 1f);
        protected static readonly Color Track = new Color(0.15f, 0.17f, 0.22f, 1f);
        protected static readonly Color Gold = new Color(1f, 0.83f, 0.28f, 1f);
        protected static readonly Color ButtonColor = new Color(0.16f, 0.2f, 0.28f, 1f);
        protected static readonly Color ButtonActive = new Color(0.22f, 0.46f, 0.8f, 1f);
        protected static readonly Color ButtonGo = new Color(0.2f, 0.55f, 0.32f, 1f);
        protected static readonly Color ButtonStop = new Color(0.62f, 0.22f, 0.22f, 1f);
        protected static readonly Color ButtonDisabled = new Color(0.12f, 0.13f, 0.16f, 1f);
        protected static readonly Color TextDisabled = new Color(0.45f, 0.48f, 0.54f, 1f);

        private GameObject overlay;
        private bool built;
        private bool isOpen;
        private int consumedEscapeFrame = -1;

        protected Font UiFont { get; private set; }

        /// <summary>Raised after the panel opens.</summary>
        public event Action Opened;

        /// <summary>Raised after an open panel closes.</summary>
        public event Action Closed;

        public bool IsOpen => isOpen;
        public bool IsBuilt => built;

        /// <summary>Whether this panel handled Escape during the current frame.</summary>
        public bool ConsumedEscapeThisFrame => consumedEscapeFrame == Time.frameCount;

        protected abstract string Title { get; }
        protected virtual Vector2 CardSize => new Vector2(1600f, 900f);

        /// <summary>Builds the overlay under an existing HUD canvas (once).</summary>
        public void Build(Transform canvasRoot, Font font)
        {
            if (built || canvasRoot == null)
            {
                return;
            }

            built = true;
            UiFont = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem(transform);

            overlay = CreateUiObject(GetType().Name + " Overlay", canvasRoot);
            Image backdrop = overlay.AddComponent<Image>();
            backdrop.color = new Color(0.02f, 0.025f, 0.04f, 0.92f);
            backdrop.raycastTarget = true;
            SetStretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);

            RectTransform card = CreateImage("Card", overlay.transform, new Color(0.055f, 0.065f, 0.09f, 1f),
                UiSprites.RoundedSprite(), Image.Type.Sliced);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, CardSize, new Vector2(0.5f, 0.5f));

            Text title = CreateText("Title", card, 34, TextAnchor.UpperLeft, Color.white);
            title.text = Title;
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, 34f, -22f, CardSize.x - 140f, 44f);

            RectTransform closeRect = CreateImage("Close", card, new Color(0.14f, 0.16f, 0.21f, 1f), UiSprites.Circle(), Image.Type.Simple);
            SetRect(closeRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(48f, 48f), new Vector2(1f, 1f));
            Image closeImage = closeRect.GetComponent<Image>();
            closeImage.raycastTarget = true;
            Button closeButton = closeRect.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(UiSounds.Click);
            closeButton.onClick.AddListener(() => SetOpen(false));
            Text closeLabel = CreateText("Label", closeRect, 25, TextAnchor.MiddleCenter, Color.white);
            closeLabel.text = "X";
            closeLabel.fontStyle = FontStyle.Bold;
            SetStretch(closeLabel.rectTransform, 0f, 0f, 0f, 0f);

            BuildContent(card);
            overlay.SetActive(isOpen);
            if (isOpen)
            {
                OnOpened();
            }
        }

        public void SetOpen(bool open)
        {
            if (isOpen == open)
            {
                return;
            }

            isOpen = open;
            if (overlay != null)
            {
                overlay.SetActive(open);
                if (open)
                {
                    overlay.transform.SetAsLastSibling();
                }
            }

            if (open)
            {
                if (built)
                {
                    OnOpened();
                }
                Opened?.Invoke();
            }
            else
            {
                DeselectInput();
                Closed?.Invoke();
            }
        }

        public void Toggle()
        {
            SetOpen(!isOpen);
        }

        /// <summary>True while the player types into a UI input field; hotkeys must not act then.</summary>
        public static bool IsTypingInInputField()
        {
            EventSystem events = EventSystem.current;
            if (events == null || events.currentSelectedGameObject == null)
            {
                return false;
            }

            InputField field = events.currentSelectedGameObject.GetComponent<InputField>();
            return field != null && field.isFocused;
        }

        /// <summary>Creates an EventSystem (Input System module) when the scene has none, so panel buttons work.</summary>
        public static void EnsureEventSystem(Transform parent)
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (parent != null)
            {
                events.transform.SetParent(parent, false);
            }
        }

        protected abstract void BuildContent(RectTransform card);

        /// <summary>Called when the panel opens (after it is built): refresh from the model.</summary>
        protected virtual void OnOpened()
        {
        }

        protected virtual void Update()
        {
            if (!isOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            consumedEscapeFrame = Time.frameCount;
            if (IsTypingInInputField())
            {
                DeselectInput();
                return;
            }

            SetOpen(false);
        }

        private static void DeselectInput()
        {
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null &&
                events.currentSelectedGameObject.GetComponent<InputField>() != null)
            {
                events.SetSelectedGameObject(null);
            }
        }

        // ------------------------------------------------------------------ UI helpers

        protected RectTransform CreateImage(string objectName, Transform parent, Color color, Sprite sprite, Image.Type type)
        {
            GameObject imageObject = CreateUiObject(objectName, parent);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = type;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        protected RectTransform CreateCard(string objectName, Transform parent, float x, float y, float width, float height)
        {
            RectTransform card = CreateImage(objectName, parent, Panel, UiSprites.RoundedSprite(), Image.Type.Sliced);
            Place(card, x, y, width, height);
            return card;
        }

        protected Text CreateText(string objectName, Transform parent, int size, TextAnchor alignment, Color color)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            Text text = textObject.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        protected Text PlaceText(string objectName, Transform parent, int size, TextAnchor alignment, Color color,
            float x, float y, float width, float height, bool bold = false, bool wrap = false)
        {
            Text text = CreateText(objectName, parent, size, alignment, color);
            if (bold)
            {
                text.fontStyle = FontStyle.Bold;
            }
            if (wrap)
            {
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            Place(text.rectTransform, x, y, width, height);
            return text;
        }

        /// <summary>Header text of a section card.</summary>
        protected Text Header(Transform card, string text, float width = 520f)
        {
            Text header = PlaceText("Header", card, 20, TextAnchor.UpperLeft, Color.white, 18f, -14f, width, 28f, true);
            header.text = text;
            return header;
        }

        /// <summary>A rounded button (top-left anchored) with a centred label.</summary>
        protected UiButton CreateButton(string objectName, Transform parent, string label, int fontSize,
            float x, float y, float width, float height, UnityAction onClick)
        {
            RectTransform rect = CreateImage(objectName, parent, ButtonColor, UiSprites.RoundedSprite(), Image.Type.Sliced);
            Place(rect, x, y, width, height);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.onClick.AddListener(UiSounds.Click);
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            Text text = CreateText("Label", rect, fontSize, TextAnchor.MiddleCenter, Color.white);
            text.text = label;
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetStretch(text.rectTransform, 6f, 6f, 2f, 2f);
            return new UiButton(button, image, text);
        }

        protected RectTransform CreateBar(string objectName, Transform parent, float x, float y, float width, float height, Color fill, out Image fillImage)
        {
            RectTransform track = CreateImage(objectName, parent, Track, UiSprites.RoundedSprite(), Image.Type.Sliced);
            Place(track, x, y, width, height);
            fillImage = CreateImage("Fill", track, fill, UiSprites.RoundedSprite(), Image.Type.Sliced).GetComponent<Image>();
            Place(fillImage.rectTransform, 0f, 0f, 0f, height);
            return track;
        }

        protected static void SetBar(Image fill, float trackWidth, float fraction)
        {
            if (fill == null)
            {
                return;
            }

            float height = fill.rectTransform.sizeDelta.y;
            float clamped = Mathf.Clamp01(fraction);
            fill.enabled = clamped > 0f;
            fill.rectTransform.sizeDelta = new Vector2(trackWidth * clamped, height);
        }

        protected static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        /// <summary>Top-left anchored rect at (x, y) from the parent's top-left corner.</summary>
        protected static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height), new Vector2(0f, 1f));
        }

        protected static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        protected static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>A built button with its background and label.</summary>
        protected sealed class UiButton
        {
            public readonly Button Button;
            public readonly Image Background;
            public readonly Text Label;

            public UiButton(Button button, Image background, Text label)
            {
                Button = button;
                Background = background;
                Label = label;
            }

            /// <summary>Sets the label, colour and whether the button can be clicked.</summary>
            public void Set(string label, Color color, bool interactable)
            {
                if (label != null && Label.text != label)
                {
                    Label.text = label;
                }
                Background.color = interactable ? color : ButtonDisabled;
                Label.color = interactable ? Color.white : TextDisabled;
                Button.interactable = interactable;
            }

            public void SetVisible(bool visible)
            {
                if (Button.gameObject.activeSelf != visible)
                {
                    Button.gameObject.SetActive(visible);
                }
            }
        }
    }
}
