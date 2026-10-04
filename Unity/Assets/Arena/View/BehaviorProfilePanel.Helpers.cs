using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>Trait lookups, text formatting and the UI building blocks of the profile panel.</summary>
    public sealed partial class BehaviorProfilePanel
    {
        /// <summary>Number of play-style traits (shared with the brain lineage compare view).</summary>
        public static int TraitCount => Traits.Length;

        /// <summary>Vietnamese name of trait <paramref name="index"/>.</summary>
        public static string TraitName(int index)
        {
            return Traits[index].Name;
        }

        /// <summary>Bar colour of trait <paramref name="index"/>.</summary>
        public static Color TraitColor(int index)
        {
            return Traits[index].Color;
        }

        /// <summary>Value 0..1 of trait <paramref name="index"/>, or -1 when there is no data.</summary>
        public static float TraitValue(int index, ChampionBehavior behavior)
        {
            return behavior == null ? -1f : Traits[index].Read(behavior);
        }

        /// <summary>Vietnamese name for a DeathCause enum name.</summary>
        public static string DeathCauseName(string cause)
        {
            switch (cause)
            {
                case "Surrounded": return "Bị bao vây";
                case "Boss": return "Boss";
                case "Brute": return "Quái khổng lồ";
                case "Contact": return "Va chạm quái";
                case "Projectile": return "Trúng đạn";
                default: return cause;
            }
        }

        /// <summary>m:ss, e.g. 552 → "9:12".</summary>
        public static string FormatSeconds(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                return "—";
            }

            int total = Mathf.FloorToInt(seconds);
            return (total / 60) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>Training steps in Vietnamese units, e.g. 16999928 → "17.0 triệu".</summary>
        public static string FormatSteps(long steps)
        {
            if (steps >= 1000000L)
            {
                return (steps / 1000000f).ToString("0.0", CultureInfo.InvariantCulture) + " triệu";
            }

            if (steps >= 1000L)
            {
                return (steps / 1000f).ToString("0", CultureInfo.InvariantCulture) + " nghìn";
            }

            return steps.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatTime(string iso)
        {
            if (!string.IsNullOrEmpty(iso) && DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime utc))
            {
                return utc.ToLocalTime().ToString("HH:mm dd/MM", CultureInfo.InvariantCulture);
            }

            return "không rõ";
        }

        private static string Percent(float fraction)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(fraction) * 100f) + "%";
        }

        private static string Metres(float value)
        {
            return value < 0f ? "chưa có" : value.ToString("0.0", CultureInfo.InvariantCulture) + " m";
        }

        private static string SkillLine(string name, int uses, int effective, int runs)
        {
            string perRun = (uses / (float)runs).ToString("0.#", CultureInfo.InvariantCulture);
            string useful = uses > 0 ? Mathf.RoundToInt(effective * 100f / uses) + "% có tác dụng" : "không dùng";
            return name + " " + perRun + " lần (" + useful + ")";
        }

        private Text Header(Transform card, string text)
        {
            Text header = CreateText("Header", card, 20, TextAnchor.UpperLeft, Color.white);
            header.text = text;
            header.fontStyle = FontStyle.Bold;
            Place(header.rectTransform, 18f, -16f, 380f, 28f);
            return header;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
        }

        private RectTransform CreateImage(string objectName, Transform parent, Color color,
            Sprite sprite, Image.Type type)
        {
            GameObject imageObject = CreateUiObject(objectName, parent);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = type;
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

        /// <summary>Top-left anchored rect at (x, y) from the parent's top-left corner.</summary>
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y),
                new Vector2(width, height), new Vector2(0f, 1f));
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, Vector2 pivot)
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
