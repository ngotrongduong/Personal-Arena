using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>Small UI building blocks of the HUD: buttons, bars, panels and text.</summary>
    public sealed partial class SurvivorHud
    {
        private Button CreateButton(string objectName, Transform parent, Color color, Vector2 position, Vector2 size, int fontSize,
            out Image image, out Text label)
        {
            RectTransform rect = CreatePanel(objectName, parent, color);
            SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
            image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -3f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            colors.colorMultiplier = 1.2f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(UiSounds.Click);
            label = CreateText("Label", rect, fontSize, TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            SetStretch(label.rectTransform, 6f, 6f, 0f, 0f);
            return button;
        }

        private void CreateBar(Transform parent, string barName, Vector2 position, float width, float height, Color fillColor,
            out RectTransform fill, out RectTransform trail, out Text label)
        {
            RectTransform background = CreatePanel(barName, parent, TrackColor);
            SetRect(background, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(width, height), new Vector2(0f, 1f));

            RectTransform inner = CreateUiObject("Inner", background).GetComponent<RectTransform>();
            SetStretch(inner, 3f, 3f, 3f, 3f);

            trail = CreateSliced("Trail", inner, new Color(1f, 0.92f, 0.75f, 0.55f));
            SetStretch(trail, 0f, 0f, 0f, 0f);

            fill = CreateSliced("Fill", inner, fillColor);
            SetStretch(fill, 0f, 0f, 0f, 0f);

            RectTransform shine = CreateSliced("Shine", fill, new Color(1f, 1f, 1f, 0.16f));
            shine.anchorMin = new Vector2(0f, 0.55f);
            shine.anchorMax = Vector2.one;
            shine.offsetMin = new Vector2(2f, 0f);
            shine.offsetMax = new Vector2(-2f, -2f);

            label = CreateText("Label", background, Mathf.RoundToInt(Mathf.Clamp(height * 0.52f, 14f, 19f)), TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            AddShadow(label);
            SetStretch(label.rectTransform, 0f, 0f, 0f, 0f);
        }

        private static void AddShadow(Text text)
        {
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private static RectTransform CreatePanel(string objectName, Transform parent, Color color)
        {
            RectTransform rect = CreateSliced(objectName, parent, color);
            if (color.a > 0.5f && color.r + color.g + color.b < 0.6f)
            {
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = PanelEdge;
                outline.effectDistance = new Vector2(1f, -1f);
            }
            return rect;
        }

        private static RectTransform CreateSliced(string objectName, Transform parent, Color color)
        {
            GameObject panelObject = CreateUiObject(objectName, parent);
            Image image = panelObject.AddComponent<Image>();
            image.sprite = UiSprites.RoundedSprite();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.6f;
            image.color = color;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        private Text CreateText(string objectName, Transform parent, int size, TextAnchor alignment, Color color)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
