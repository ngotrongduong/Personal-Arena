using System;
using System.Collections.Generic;
using PersonalArena.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>Runtime-built uGUI HUD for manual arena play.</summary>
    public sealed class ArenaHud : MonoBehaviour
    {
        private ArenaSim sim;
        private ArenaStats stats;
        private Font font;
        private Image hpFill;
        private Image energyFill;
        private Text hpText;
        private Text energyText;
        private readonly Image[] cooldownFills = new Image[4];
        private readonly Text[] cooldownTexts = new Text[4];
        private Text timerText;
        private Text countersText;
        private Text pauseText;
        private GameObject resultPanel;
        private Text resultText;
        private Text helpText;
        private Text infoText;
        private string resultFooter = "Press R to restart";
        private bool built;
        private Transform canvasRoot;
        private GameObject trainingPanel;
        private Button trainingButton;
        private Image trainingButtonImage;
        private Text trainingButtonLabel;
        private Text trainingText;
        private Text trainingGraphCaption;
        private readonly Image[] trainingBars = new Image[TrainingBarCount];
        private const int TrainingBarCount = 64;
        private const float TrainingGraphHeight = 64f;

        /// <summary>Raised when the owner clicks the Train the AI / Stop button.</summary>
        public event Action TrainingButtonClicked;

        public void Bind(ArenaSim arenaSim, ArenaStats arenaStats)
        {
            EnsureBuilt();
            sim = arenaSim;
            stats = arenaStats;
            Refresh();
        }

        public void SetPaused(bool paused)
        {
            EnsureBuilt();
            pauseText.gameObject.SetActive(paused);
        }

        /// <summary>Replaces the controls hint in the bottom-left corner.</summary>
        public void SetHelpText(string text)
        {
            EnsureBuilt();
            helpText.text = text ?? string.Empty;
        }

        /// <summary>Shows a multi-line panel in the top-right corner (hidden when empty).</summary>
        public void SetInfoText(string text)
        {
            EnsureBuilt();
            infoText.text = text ?? string.Empty;
            infoText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>Last line of the end-of-episode panel.</summary>
        public void SetResultFooter(string text)
        {
            resultFooter = text ?? string.Empty;
        }

        /// <summary>Shows the training panel (button, progress, reward graph) under the info panel.</summary>
        public void ShowTrainingPanel(bool show)
        {
            EnsureBuilt();
            if (show && trainingPanel == null)
            {
                BuildTrainingPanel();
            }
            if (trainingPanel != null)
            {
                trainingPanel.SetActive(show);
            }
        }

        public void SetTrainingButton(string label, bool interactable, Color color)
        {
            ShowTrainingPanel(true);
            trainingButtonLabel.text = label ?? string.Empty;
            trainingButton.interactable = interactable;
            trainingButtonImage.color = interactable ? color : new Color(0.24f, 0.26f, 0.3f, 1f);
        }

        public void SetTrainingText(string text)
        {
            ShowTrainingPanel(true);
            trainingText.text = text ?? string.Empty;
        }

        /// <summary>Draws the mean-reward history as bars (oldest left); empty hides the graph.</summary>
        public void SetTrainingGraph(IReadOnlyList<float> values, string caption)
        {
            ShowTrainingPanel(true);
            trainingGraphCaption.text = caption ?? string.Empty;
            float[] buckets = Bucket(values, TrainingBarCount);
            float minimum = 0f;
            float maximum = 0f;
            foreach (float value in buckets)
            {
                minimum = Mathf.Min(minimum, value);
                maximum = Mathf.Max(maximum, value);
            }

            float range = Mathf.Max(maximum - minimum, 1e-3f);
            float zero = -minimum / range * TrainingGraphHeight;
            for (int i = 0; i < TrainingBarCount; i++)
            {
                Image bar = trainingBars[i];
                bool visible = i < buckets.Length;
                bar.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                float value = buckets[i];
                float height = Mathf.Max(Mathf.Abs(value) / range * TrainingGraphHeight, 1.5f);
                RectTransform rect = bar.rectTransform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, value >= 0f ? zero : zero - height);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
                bar.color = value >= 0f ? new Color(0.3f, 0.78f, 0.42f, 1f) : new Color(0.85f, 0.3f, 0.28f, 1f);
            }
        }

        /// <summary>Averages <paramref name="values"/> into at most <paramref name="count"/> buckets.</summary>
        public static float[] Bucket(IReadOnlyList<float> values, int count)
        {
            if (values == null || values.Count == 0 || count <= 0)
            {
                return new float[0];
            }

            int buckets = Mathf.Min(count, values.Count);
            float[] result = new float[buckets];
            for (int bucket = 0; bucket < buckets; bucket++)
            {
                int start = bucket * values.Count / buckets;
                int end = Mathf.Max(start + 1, (bucket + 1) * values.Count / buckets);
                float sum = 0f;
                for (int i = start; i < end; i++)
                {
                    sum += values[i];
                }
                result[bucket] = sum / (end - start);
            }
            return result;
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (!built || sim == null)
            {
                return;
            }

            float hpRatio = sim.HeroDef.MaxHp > 0f ? sim.Hero.Hp / sim.HeroDef.MaxHp : 0f;
            float energyRatio = sim.HeroDef.MaxEnergy > 0f ? sim.Hero.Energy / sim.HeroDef.MaxEnergy : 0f;
            hpFill.fillAmount = Mathf.Clamp01(hpRatio);
            energyFill.fillAmount = Mathf.Clamp01(energyRatio);
            hpText.text = "HP  " + Mathf.CeilToInt(sim.Hero.Hp) + " / " + Mathf.CeilToInt(sim.HeroDef.MaxHp);
            energyText.text = "ENERGY  " + Mathf.CeilToInt(sim.Hero.Energy) + " / " + Mathf.CeilToInt(sim.HeroDef.MaxEnergy);

            for (int i = 0; i < 4; i++)
            {
                float cooldown = sim.Hero.CooldownRemaining[i];
                float maximum = sim.HeroDef.Skills[i].Cooldown;
                cooldownFills[i].fillAmount = maximum > 0f ? Mathf.Clamp01(cooldown / maximum) : 0f;
                cooldownTexts[i].text = cooldown > 0.05f ? cooldown.ToString("0.0") : string.Empty;
            }

            int alive = 0;
            for (int i = 0; i < sim.Zombies.Count; i++)
            {
                if (sim.Zombies[i].Alive)
                {
                    alive++;
                }
            }

            timerText.text = FormatTime(sim.Time) + " / " + FormatTime(sim.Config.EpisodeSeconds);
            countersText.text = "Kills  " + (stats != null ? stats.Kills : 0) + "     Zombies  " + alive;
            resultPanel.SetActive(sim.Done);
            if (sim.Done && stats != null)
            {
                string heading = sim.Hero.Alive ? "EPISODE COMPLETE" : "WARRIOR FALLEN";
                resultText.text = heading + "\n\nSurvived  " + FormatTime(stats.TimeSurvived) +
                    "\nKills  " + stats.Kills +
                    "\nBackstabs  " + stats.Backstabs +
                    "\nParries  " + stats.Parries +
                    "\nDamage taken  " + stats.DamageTaken.ToString("0") +
                    "\n\n" + resultFooter;
            }
        }

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }
            built = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasObject = CreateUiObject("Arena HUD Canvas", transform);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasRoot = canvasObject.transform;

            RectTransform vitals = CreatePanel("Vitals", canvasObject.transform, new Color(0.04f, 0.05f, 0.07f, 0.84f));
            SetRect(vitals, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(420f, 126f), new Vector2(0f, 1f));
            CreateBar(vitals, "HP Bar", new Vector2(18f, -18f), new Color(0.72f, 0.12f, 0.12f), out hpFill, out hpText);
            CreateBar(vitals, "Energy Bar", new Vector2(18f, -70f), new Color(0.12f, 0.48f, 0.92f), out energyFill, out energyText);

            timerText = CreateText("Timer", canvasObject.transform, 28, TextAnchor.UpperCenter, Color.white);
            SetRect(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(360f, 42f), new Vector2(0.5f, 1f));
            countersText = CreateText("Counters", canvasObject.transform, 23, TextAnchor.UpperCenter, new Color(0.85f, 0.9f, 0.95f));
            SetRect(countersText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(420f, 36f), new Vector2(0.5f, 1f));

            RectTransform skills = CreatePanel("Skills", canvasObject.transform, new Color(0.04f, 0.05f, 0.07f, 0.86f));
            SetRect(skills, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(760f, 142f), new Vector2(0.5f, 0f));
            for (int i = 0; i < 4; i++)
            {
                CreateSkillSlot(skills, i, -276f + i * 184f);
            }

            helpText = CreateText("Help", canvasObject.transform, 18, TextAnchor.LowerLeft, new Color(0.82f, 0.84f, 0.88f));
            helpText.text = "WASD move   Mouse aim   Q/E turn   Esc pause   R restart   1-6 zombies: 1/2/4/8/16/32";
            SetRect(helpText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 12f), new Vector2(1040f, 32f), new Vector2(0f, 0f));

            RectTransform info = CreatePanel("Info", canvasObject.transform, new Color(0.04f, 0.05f, 0.07f, 0.84f));
            SetRect(info, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(430f, 180f), new Vector2(1f, 1f));
            infoText = CreateText("Info Text", info, 18, TextAnchor.UpperLeft, new Color(0.9f, 0.93f, 0.97f));
            SetStretch(infoText.rectTransform, 18f, 14f, 14f, 12f);
            info.gameObject.SetActive(false);

            pauseText = CreateText("Paused", canvasObject.transform, 52, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.3f));
            pauseText.text = "PAUSED";
            SetRect(pauseText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 100f), new Vector2(0.5f, 0.5f));
            pauseText.gameObject.SetActive(false);

            RectTransform result = CreatePanel("Result Panel", canvasObject.transform, new Color(0.025f, 0.03f, 0.045f, 0.94f));
            SetRect(result, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(510f, 480f), new Vector2(0.5f, 0.5f));
            resultPanel = result.gameObject;
            resultText = CreateText("Result", result, 30, TextAnchor.MiddleCenter, Color.white);
            SetStretch(resultText.rectTransform, 30f, 30f, 30f, 30f);
            resultPanel.SetActive(false);
        }

        private void BuildTrainingPanel()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            RectTransform panel = CreatePanel("Training", canvasRoot, new Color(0.04f, 0.05f, 0.07f, 0.86f));
            SetRect(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -212f), new Vector2(430f, 306f), new Vector2(1f, 1f));
            trainingPanel = panel.gameObject;

            Text title = CreateText("Title", panel, 20, TextAnchor.UpperLeft, new Color(1f, 0.86f, 0.45f));
            title.text = "TRAINING";
            title.fontStyle = FontStyle.Bold;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(394f, 26f), new Vector2(0f, 1f));

            RectTransform buttonRect = CreatePanel("Train Button", panel, new Color(0.2f, 0.6f, 0.32f, 1f));
            SetRect(buttonRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -44f), new Vector2(394f, 54f), new Vector2(0f, 1f));
            trainingButtonImage = buttonRect.GetComponent<Image>();
            trainingButton = buttonRect.gameObject.AddComponent<Button>();
            trainingButton.targetGraphic = trainingButtonImage;
            trainingButton.onClick.AddListener(() => TrainingButtonClicked?.Invoke());
            trainingButtonLabel = CreateText("Label", buttonRect, 24, TextAnchor.MiddleCenter, Color.white);
            trainingButtonLabel.fontStyle = FontStyle.Bold;
            SetStretch(trainingButtonLabel.rectTransform, 0f, 0f, 0f, 0f);

            trainingText = CreateText("Status", panel, 17, TextAnchor.UpperLeft, new Color(0.9f, 0.93f, 0.97f));
            SetRect(trainingText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -110f), new Vector2(394f, 94f), new Vector2(0f, 1f));

            trainingGraphCaption = CreateText("Graph Caption", panel, 15, TextAnchor.UpperLeft, new Color(0.7f, 0.76f, 0.84f));
            SetRect(trainingGraphCaption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -206f), new Vector2(394f, 20f), new Vector2(0f, 1f));

            RectTransform graph = CreatePanel("Reward Graph", panel, new Color(0.1f, 0.11f, 0.14f, 1f));
            SetRect(graph, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -228f), new Vector2(394f, TrainingGraphHeight), new Vector2(0f, 1f));
            float slot = 394f / TrainingBarCount;
            for (int i = 0; i < TrainingBarCount; i++)
            {
                RectTransform bar = CreatePanel("Bar " + i, graph, Color.clear);
                SetRect(bar, Vector2.zero, Vector2.zero, new Vector2(i * slot + 0.5f, 0f), new Vector2(slot - 1f, 0f), Vector2.zero);
                trainingBars[i] = bar.GetComponent<Image>();
                trainingBars[i].raycastTarget = false;
                bar.gameObject.SetActive(false);
            }

            trainingPanel.SetActive(false);
        }

        private void CreateBar(Transform parent, string barName, Vector2 position, Color fillColor, out Image fill, out Text label)
        {
            RectTransform background = CreatePanel(barName, parent, new Color(0.13f, 0.14f, 0.17f, 1f));
            SetRect(background, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(384f, 38f), new Vector2(0f, 1f));
            GameObject fillObject = CreateUiObject("Fill", background);
            fill = fillObject.AddComponent<Image>();
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            SetStretch(fill.rectTransform, 3f, 3f, 3f, 3f);
            label = CreateText("Label", background, 20, TextAnchor.MiddleCenter, Color.white);
            SetStretch(label.rectTransform, 0f, 0f, 0f, 0f);
        }

        private void CreateSkillSlot(Transform parent, int index, float x)
        {
            RectTransform slot = CreatePanel("Skill " + (index + 1), parent, new Color(0.13f, 0.15f, 0.19f, 1f));
            SetRect(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(164f, 112f), new Vector2(0.5f, 0.5f));
            Text title = CreateText("Title", slot, 21, TextAnchor.UpperCenter, Color.white);
            title.text = SkillName(index);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -9f), new Vector2(0f, 30f), new Vector2(0.5f, 1f));
            Text hint = CreateText("Key Hint", slot, 16, TextAnchor.LowerCenter, new Color(0.7f, 0.76f, 0.84f));
            hint.text = SkillKey(index);
            SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 7f), new Vector2(0f, 28f), new Vector2(0.5f, 0f));

            GameObject shadeObject = CreateUiObject("Cooldown", slot);
            Image shade = shadeObject.AddComponent<Image>();
            shade.color = new Color(0.03f, 0.04f, 0.06f, 0.76f);
            shade.type = Image.Type.Filled;
            shade.fillMethod = Image.FillMethod.Radial360;
            shade.fillOrigin = 2;
            shade.fillClockwise = false;
            SetStretch(shade.rectTransform, 2f, 2f, 2f, 2f);
            cooldownFills[index] = shade;

            Text cooldown = CreateText("Cooldown Time", slot, 28, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.35f));
            SetStretch(cooldown.rectTransform, 0f, 0f, 0f, 0f);
            cooldownTexts[index] = cooldown;
        }

        private RectTransform CreatePanel(string objectName, Transform parent, Color color)
        {
            GameObject panelObject = CreateUiObject(objectName, parent);
            Image image = panelObject.AddComponent<Image>();
            image.color = color;
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

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }

        private static string SkillName(int index)
        {
            switch (index)
            {
                case 0: return "Strike";
                case 1: return "Kick";
                case 2: return "Block";
                default: return "Dash";
            }
        }

        private static string SkillKey(int index)
        {
            switch (index)
            {
                case 0: return "LMB / J";
                case 1: return "F / K";
                case 2: return "RMB / L";
                default: return "Space / Shift";
            }
        }
    }
}
