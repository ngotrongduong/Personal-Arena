using System;
using System.Collections.Generic;
using PersonalArena.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>Runtime-built uGUI HUD shared by manual play and the watch-AI viewer.</summary>
    public sealed class ArenaHud : MonoBehaviour
    {
        private const int TrainingBarCount = 64;
        private const float TrainingGraphHeight = 64f;
        private const float BarWidth = 384f;

        private static readonly Color PanelColor = new Color(0.05f, 0.055f, 0.085f, 0.86f);
        private static readonly Color PanelEdge = new Color(0.55f, 0.6f, 0.85f, 0.18f);
        private static readonly Color TrackColor = new Color(0.1f, 0.1f, 0.14f, 1f);
        private static readonly Color HpColor = new Color(0.86f, 0.18f, 0.2f);
        private static readonly Color HpLowColor = new Color(1f, 0.35f, 0.2f);
        private static readonly Color EnergyColor = new Color(0.2f, 0.58f, 1f);
        private static readonly Color GoldText = new Color(1f, 0.86f, 0.45f);

        private ArenaSim sim;
        private ArenaStats stats;
        private Font font;
        private RectTransform hpFill;
        private RectTransform hpTrail;
        private Image hpFillImage;
        private RectTransform energyFill;
        private RectTransform energyTrail;
        private Text hpText;
        private Text energyText;
        private float hpShown = 1f;
        private float hpTrailShown = 1f;
        private float energyShown = 1f;
        private float energyTrailShown = 1f;
        private readonly Image[] cooldownFills = new Image[4];
        private readonly Text[] cooldownTexts = new Text[4];
        private readonly Text[] skillHints = new Text[4];
        private readonly Image[] skillGlows = new Image[4];
        private readonly Image[] skillFrames = new Image[4];
        private readonly float[] skillFlash = new float[4];
        private readonly float[] lastCooldown = new float[4];
        private bool showSkillKeys;
        private Text timerText;
        private Text countersText;
        private Text pauseText;
        private GameObject resultPanel;
        private Text resultTitle;
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
        private Button powerButton;
        private Text powerLabel;
        private Text trainingText;
        private Text trainingGraphCaption;
        private readonly Image[] trainingBars = new Image[TrainingBarCount];
        private TrainingHistoryPanel historyPanel;
        private readonly Text[] skillTitles = new Text[4];
        private readonly Image[] skillIcons = new Image[4];
        private readonly Color[] skillColors = new Color[4];
        private Text heroNameText;
        private string boundClassId;
        private GameObject matchPanel;
        private Text heroChoiceLabel;
        private Text mixChoiceLabel;

        /// <summary>Raised when the owner clicks the Train the AI / Stop button.</summary>
        public event Action TrainingButtonClicked;

        /// <summary>Raised when the owner clicks the training power (speed) selector.</summary>
        public event Action TrainingPowerClicked;

        /// <summary>Raised when the owner clicks the hero class selector.</summary>
        public event Action HeroClassClicked;

        /// <summary>Raised when the owner clicks the zombie mix selector.</summary>
        public event Action ZombieMixClicked;

        /// <summary>The full-screen training data panel (created with the training panel).</summary>
        public TrainingHistoryPanel HistoryPanel => historyPanel;

        public void Bind(ArenaSim arenaSim, ArenaStats arenaStats)
        {
            EnsureBuilt();
            sim = arenaSim;
            stats = arenaStats;
            if (sim != null)
            {
                if (sim.HeroDef.Id != boundClassId)
                {
                    boundClassId = sim.HeroDef.Id;
                    RefreshSkillLabels();
                }
                hpShown = hpTrailShown = HpRatio();
                energyShown = energyTrailShown = EnergyRatio();
                for (int i = 0; i < 4; i++)
                {
                    lastCooldown[i] = sim.Hero.CooldownRemaining[i];
                    skillFlash[i] = 0f;
                }
            }
            Refresh();
        }

        public void SetPaused(bool paused)
        {
            EnsureBuilt();
            pauseText.transform.parent.gameObject.SetActive(paused);
        }

        /// <summary>Replaces the controls hint in the bottom-left corner.</summary>
        public void SetHelpText(string text)
        {
            EnsureBuilt();
            helpText.text = text ?? string.Empty;
        }

        /// <summary>Skill slots show their keys (manual play) or what each skill does (watching the AI).</summary>
        public void ShowSkillKeys(bool show)
        {
            EnsureBuilt();
            showSkillKeys = show;
            RefreshSkillLabels();
        }

        /// <summary>Shows the hero class and zombie mix selectors (watch-AI viewer).</summary>
        public void SetMatchChoices(string heroLabel, string mixLabel)
        {
            EnsureBuilt();
            if (matchPanel == null)
            {
                BuildMatchPanel();
            }
            heroChoiceLabel.text = heroLabel ?? string.Empty;
            mixChoiceLabel.text = mixLabel ?? string.Empty;
        }

        private void RefreshSkillLabels()
        {
            SkillDef[] skills = sim != null ? sim.HeroDef.Skills : null;
            for (int i = 0; i < 4; i++)
            {
                SkillDef skill = skills != null && i < skills.Length ? skills[i] : null;
                ApplySkill(i, skill);
            }
            heroNameText.text = HeroName();
        }

        private void ApplySkill(int index, SkillDef skill)
        {
            skillTitles[index].text = SkillName(skill, index).ToUpperInvariant();
            skillHints[index].text = showSkillKeys ? SkillKey(index) : SkillEffect(skill, index);
            skillIcons[index].sprite = SkillIconFactory.IconFor(skill, index);
            skillColors[index] = SkillIconFactory.ColorFor(skill, index);
        }

        private string HeroName()
        {
            return sim != null && !string.IsNullOrEmpty(sim.HeroDef.Id) ? sim.HeroDef.Id.ToUpperInvariant() : "WARRIOR";
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

        public void SetTrainingPower(string label, bool interactable)
        {
            ShowTrainingPanel(true);
            powerLabel.text = label ?? string.Empty;
            powerButton.interactable = interactable;
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

        private float HpRatio()
        {
            return sim.HeroDef.MaxHp > 0f ? Mathf.Clamp01(sim.Hero.Hp / sim.HeroDef.MaxHp) : 0f;
        }

        private float EnergyRatio()
        {
            return sim.HeroDef.MaxEnergy > 0f ? Mathf.Clamp01(sim.Hero.Energy / sim.HeroDef.MaxEnergy) : 0f;
        }

        private void Refresh()
        {
            if (!built || sim == null)
            {
                return;
            }

            // Bars glide to their value; a pale trail shows the chunk just lost.
            float delta = Time.unscaledDeltaTime;
            float hpRatio = HpRatio();
            float energyRatio = EnergyRatio();
            hpShown = Mathf.MoveTowards(hpShown, hpRatio, delta * 2.5f + Mathf.Abs(hpRatio - hpShown) * delta * 10f);
            energyShown = Mathf.MoveTowards(energyShown, energyRatio, delta * 3f + Mathf.Abs(energyRatio - energyShown) * delta * 10f);
            hpTrailShown = hpTrailShown < hpShown ? hpShown : Mathf.MoveTowards(hpTrailShown, hpShown, delta * 0.45f);
            energyTrailShown = energyTrailShown < energyShown ? energyShown : Mathf.MoveTowards(energyTrailShown, energyShown, delta * 0.6f);
            SetFill(hpFill, hpShown);
            SetFill(hpTrail, hpTrailShown);
            SetFill(energyFill, energyShown);
            SetFill(energyTrail, energyTrailShown);
            bool lowHp = hpRatio < 0.3f && sim.Hero.Alive;
            float pulse = lowHp ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
            hpFillImage.color = Color.Lerp(HpColor, HpLowColor, pulse);
            hpText.text = "HP  " + Mathf.CeilToInt(sim.Hero.Hp) + " / " + Mathf.CeilToInt(sim.HeroDef.MaxHp);
            energyText.text = "ENERGY  " + Mathf.CeilToInt(sim.Hero.Energy) + " / " + Mathf.CeilToInt(sim.HeroDef.MaxEnergy);

            for (int i = 0; i < 4; i++)
            {
                float cooldown = sim.Hero.CooldownRemaining[i];
                SkillDef skill = sim.HeroDef.Skills[i];
                float maximum = skill != null ? skill.Cooldown : 0f;
                if (cooldown > lastCooldown[i] + 0.01f)
                {
                    skillFlash[i] = 1f;
                }
                lastCooldown[i] = cooldown;
                bool active = skill != null && skill.Kind == SkillKind.Block && sim.Hero.IsBlocking;
                skillFlash[i] = Mathf.Max(active ? 0.6f : 0f, skillFlash[i] - delta * 3.5f);
                cooldownFills[i].fillAmount = maximum > 0f ? Mathf.Clamp01(cooldown / maximum) : 0f;
                cooldownTexts[i].text = cooldown > 0.05f ? cooldown.ToString("0.0") : string.Empty;
                Color glow = skillColors[i];
                glow.a = skillFlash[i] * 0.55f;
                skillGlows[i].color = glow;
                Color frame = skillColors[i];
                frame.a = cooldown > 0.05f ? 0.25f : 0.75f;
                skillFrames[i].color = frame;
                skillIcons[i].color = cooldown > 0.05f ? new Color(0.72f, 0.72f, 0.76f, 1f) : Color.white;
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
            countersText.text = "KILLS  " + (stats != null ? stats.Kills : 0) + "      ZOMBIES  " + alive;
            resultPanel.SetActive(sim.Done);
            if (sim.Done && stats != null)
            {
                bool fell = !sim.Hero.Alive && sim.Hero.FellOff;
                resultTitle.text = sim.Hero.Alive ? "ROUND COMPLETE" : fell ? "FELL INTO THE ABYSS" : HeroName() + " FALLEN";
                resultTitle.color = sim.Hero.Alive ? GoldText : new Color(1f, 0.4f, 0.35f);
                resultText.text = "Survived  " + FormatTime(stats.TimeSurvived) +
                    "\nKills  " + stats.Kills +
                    "\nBackstabs  " + stats.Backstabs +
                    "\nParries  " + stats.Parries +
                    "\nDamage taken  " + stats.DamageTaken.ToString("0") +
                    "\n\n" + resultFooter;
            }
        }

        private static void SetFill(RectTransform fill, float ratio)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
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

            RectTransform vitals = CreatePanel("Vitals", canvasRoot, PanelColor);
            SetRect(vitals, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(420f, 128f), new Vector2(0f, 1f));
            Text heroName = CreateText("Name", vitals, 17, TextAnchor.UpperLeft, GoldText);
            heroName.text = "WARRIOR";
            heroName.fontStyle = FontStyle.Bold;
            heroNameText = heroName;
            SetRect(heroName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(300f, 22f), new Vector2(0f, 1f));
            CreateBar(vitals, "HP Bar", new Vector2(18f, -36f), 40f, HpColor, out hpFill, out hpTrail, out hpText);
            hpFillImage = hpFill.GetComponent<Image>();
            CreateBar(vitals, "Energy Bar", new Vector2(18f, -84f), 28f, EnergyColor, out energyFill, out energyTrail, out energyText);
            energyText.fontSize = 16;

            RectTransform clock = CreatePanel("Clock", canvasRoot, PanelColor);
            SetRect(clock, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(360f, 84f), new Vector2(0.5f, 1f));
            timerText = CreateText("Timer", clock, 30, TextAnchor.UpperCenter, Color.white);
            timerText.fontStyle = FontStyle.Bold;
            SetRect(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(340f, 40f), new Vector2(0.5f, 1f));
            countersText = CreateText("Counters", clock, 18, TextAnchor.UpperCenter, new Color(0.8f, 0.85f, 0.95f));
            SetRect(countersText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(340f, 26f), new Vector2(0.5f, 1f));

            RectTransform skills = CreatePanel("Skills", canvasRoot, PanelColor);
            SetRect(skills, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(720f, 184f), new Vector2(0.5f, 0f));
            for (int i = 0; i < 4; i++)
            {
                CreateSkillSlot(skills, i, -261f + i * 174f);
            }

            // Controls live under the vitals panel and grow with their text, clear of the skill bar.
            RectTransform help = CreatePanel("Help", canvasRoot, new Color(0.03f, 0.035f, 0.055f, 0.62f));
            SetRect(help, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -164f), new Vector2(420f, 60f), new Vector2(0f, 1f));
            VerticalLayoutGroup helpLayout = help.gameObject.AddComponent<VerticalLayoutGroup>();
            helpLayout.padding = new RectOffset(16, 14, 10, 12);
            helpLayout.childControlWidth = true;
            helpLayout.childControlHeight = true;
            helpLayout.childForceExpandWidth = true;
            helpLayout.childForceExpandHeight = false;
            ContentSizeFitter helpFitter = help.gameObject.AddComponent<ContentSizeFitter>();
            helpFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            helpText = CreateText("Help Text", help, 15, TextAnchor.UpperLeft, new Color(0.8f, 0.83f, 0.9f));
            helpText.horizontalOverflow = HorizontalWrapMode.Wrap;
            helpText.lineSpacing = 1.1f;
            helpText.text = "WASD move   Mouse aim   Q/E turn\nEsc pause   R restart\n1-6 zombies: 1/2/4/8/16/32\n" +
                "Camera: middle-drag rotate   Scroll zoom\nC camera mode   Home reset view";

            RectTransform info = CreatePanel("Info", canvasRoot, PanelColor);
            SetRect(info, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(430f, 180f), new Vector2(1f, 1f));
            infoText = CreateText("Info Text", info, 17, TextAnchor.UpperLeft, new Color(0.9f, 0.93f, 0.97f));
            infoText.lineSpacing = 1.1f;
            SetStretch(infoText.rectTransform, 18f, 14f, 14f, 12f);
            info.gameObject.SetActive(false);

            RectTransform pause = CreatePanel("Paused", canvasRoot, new Color(0.03f, 0.03f, 0.05f, 0.8f));
            SetRect(pause, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(360f, 96f), new Vector2(0.5f, 0.5f));
            pauseText = CreateText("Label", pause, 46, TextAnchor.MiddleCenter, GoldText);
            pauseText.text = "PAUSED";
            pauseText.fontStyle = FontStyle.Bold;
            SetStretch(pauseText.rectTransform, 0f, 0f, 0f, 0f);
            pause.gameObject.SetActive(false);

            RectTransform result = CreatePanel("Result Panel", canvasRoot, new Color(0.035f, 0.035f, 0.06f, 0.94f));
            SetRect(result, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 440f), new Vector2(0.5f, 0.5f));
            resultPanel = result.gameObject;
            RectTransform accent = CreatePanel("Accent", result, new Color(1f, 0.8f, 0.4f, 0.8f));
            SetRect(accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(300f, 3f), new Vector2(0.5f, 1f));
            resultTitle = CreateText("Title", result, 36, TextAnchor.UpperCenter, GoldText);
            resultTitle.fontStyle = FontStyle.Bold;
            SetRect(resultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(500f, 48f), new Vector2(0.5f, 1f));
            resultText = CreateText("Result", result, 26, TextAnchor.UpperCenter, Color.white);
            resultText.lineSpacing = 1.15f;
            SetStretch(resultText.rectTransform, 30f, 30f, 110f, 26f);
            resultPanel.SetActive(false);
        }

        private void BuildTrainingPanel()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            RectTransform panel = CreatePanel("Training", canvasRoot, PanelColor);
            SetRect(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -212f), new Vector2(430f, 420f), new Vector2(1f, 1f));
            trainingPanel = panel.gameObject;

            Text title = CreateText("Title", panel, 20, TextAnchor.UpperLeft, GoldText);
            title.text = "TRAINING";
            title.fontStyle = FontStyle.Bold;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(394f, 26f), new Vector2(0f, 1f));

            trainingButton = CreateButton("Train Button", panel, new Color(0.2f, 0.6f, 0.32f, 1f), new Vector2(18f, -44f), new Vector2(394f, 54f),
                24, out trainingButtonImage, out trainingButtonLabel);
            trainingButton.onClick.AddListener(() => TrainingButtonClicked?.Invoke());

            powerButton = CreateButton("Power Button", panel, new Color(0.17f, 0.21f, 0.32f, 1f), new Vector2(18f, -106f), new Vector2(394f, 36f),
                16, out _, out powerLabel);
            powerLabel.fontStyle = FontStyle.Normal;
            powerButton.onClick.AddListener(() => TrainingPowerClicked?.Invoke());

            trainingText = CreateText("Status", panel, 16, TextAnchor.UpperLeft, new Color(0.9f, 0.93f, 0.97f));
            SetRect(trainingText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -152f), new Vector2(394f, 116f), new Vector2(0f, 1f));

            trainingGraphCaption = CreateText("Graph Caption", panel, 14, TextAnchor.UpperLeft, new Color(0.7f, 0.76f, 0.84f));
            SetRect(trainingGraphCaption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -270f), new Vector2(394f, 20f), new Vector2(0f, 1f));

            RectTransform graph = CreatePanel("Reward Graph", panel, new Color(0.08f, 0.09f, 0.13f, 1f));
            SetRect(graph, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -292f), new Vector2(394f, TrainingGraphHeight), new Vector2(0f, 1f));
            float slot = 394f / TrainingBarCount;
            for (int i = 0; i < TrainingBarCount; i++)
            {
                GameObject barObject = CreateUiObject("Bar " + i, graph);
                Image bar = barObject.AddComponent<Image>();
                bar.raycastTarget = false;
                SetRect(bar.rectTransform, Vector2.zero, Vector2.zero, new Vector2(i * slot + 0.5f, 0f), new Vector2(slot - 1f, 0f), Vector2.zero);
                trainingBars[i] = bar;
                barObject.SetActive(false);
            }

            Button dataButton = CreateButton("Training Data Button", panel, new Color(0.36f, 0.25f, 0.62f, 1f), new Vector2(18f, -366f), new Vector2(394f, 40f),
                18, out _, out Text dataLabel);
            dataLabel.text = "TRAINING DATA  (learning graphs)  G";
            dataButton.onClick.AddListener(() => historyPanel?.Toggle());

            historyPanel = GetComponent<TrainingHistoryPanel>();
            if (historyPanel == null)
            {
                historyPanel = gameObject.AddComponent<TrainingHistoryPanel>();
            }
            historyPanel.Build(canvasRoot, font);

            trainingPanel.SetActive(false);
        }

        private Button CreateButton(string objectName, Transform parent, Color color, Vector2 position, Vector2 size, int fontSize,
            out Image image, out Text label)
        {
            RectTransform rect = CreatePanel(objectName, parent, color);
            SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
            image = rect.GetComponent<Image>();
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
            label = CreateText("Label", rect, fontSize, TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            SetStretch(label.rectTransform, 6f, 6f, 0f, 0f);
            return button;
        }

        private void CreateBar(Transform parent, string barName, Vector2 position, float height, Color fillColor,
            out RectTransform fill, out RectTransform trail, out Text label)
        {
            RectTransform background = CreatePanel(barName, parent, TrackColor);
            SetRect(background, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(BarWidth, height), new Vector2(0f, 1f));

            RectTransform inner = CreateUiObject("Inner", background).GetComponent<RectTransform>();
            SetStretch(inner, 3f, 3f, 3f, 3f);

            trail = CreateSliced("Trail", inner, new Color(1f, 0.92f, 0.75f, 0.55f));
            trail.anchorMin = Vector2.zero;
            trail.anchorMax = Vector2.one;
            trail.offsetMin = Vector2.zero;
            trail.offsetMax = Vector2.zero;

            fill = CreateSliced("Fill", inner, fillColor);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            RectTransform shine = CreateSliced("Shine", fill, new Color(1f, 1f, 1f, 0.16f));
            shine.anchorMin = new Vector2(0f, 0.55f);
            shine.anchorMax = Vector2.one;
            shine.offsetMin = new Vector2(2f, 0f);
            shine.offsetMax = new Vector2(-2f, -2f);

            label = CreateText("Label", background, 19, TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            Shadow shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(1f, -1f);
            SetStretch(label.rectTransform, 0f, 0f, 0f, 0f);
        }

        private void CreateSkillSlot(Transform parent, int index, float x)
        {
            RectTransform slot = CreatePanel("Skill " + (index + 1), parent, new Color(0.1f, 0.11f, 0.16f, 1f));
            SetRect(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(160f, 160f), new Vector2(0.5f, 0.5f));

            RectTransform glow = CreateSliced("Glow", slot, Color.clear);
            SetStretch(glow, 0f, 0f, 0f, 0f);
            skillGlows[index] = glow.GetComponent<Image>();

            skillColors[index] = SkillIconFactory.ColorFor(null, index);
            RectTransform frame = CreateSliced("Accent", slot, skillColors[index]);
            frame.anchorMin = new Vector2(0f, 1f);
            frame.anchorMax = new Vector2(1f, 1f);
            frame.pivot = new Vector2(0.5f, 1f);
            frame.anchoredPosition = new Vector2(0f, -4f);
            frame.sizeDelta = new Vector2(-24f, 4f);
            skillFrames[index] = frame.GetComponent<Image>();

            GameObject iconObject = CreateUiObject("Icon", slot);
            Image icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetRect(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(76f, 76f), new Vector2(0.5f, 1f));
            skillIcons[index] = icon;

            Text title = CreateText("Title", slot, 18, TextAnchor.UpperCenter, Color.white);
            title.fontStyle = FontStyle.Bold;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -94f), new Vector2(0f, 24f), new Vector2(0.5f, 1f));
            skillTitles[index] = title;
            Text hint = CreateText("Key Hint", slot, 13, TextAnchor.LowerCenter, new Color(0.68f, 0.74f, 0.84f));
            SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 10f), new Vector2(-8f, 34f), new Vector2(0.5f, 0f));
            skillHints[index] = hint;
            ApplySkill(index, null);

            GameObject shadeObject = CreateUiObject("Cooldown", slot);
            Image shade = shadeObject.AddComponent<Image>();
            // A clock sweep over the icon only, so the icon and its name stay readable on cooldown.
            // A Filled Image needs a sprite: without one Unity ignores fillAmount and covers the rect.
            shade.sprite = UiSprites.RoundedSprite();
            shade.color = new Color(0.02f, 0.02f, 0.05f, 0.62f);
            shade.type = Image.Type.Filled;
            shade.fillMethod = Image.FillMethod.Radial360;
            shade.fillOrigin = (int)Image.Origin360.Top;
            shade.fillClockwise = false;
            shade.raycastTarget = false;
            SetRect(shade.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(76f, 76f), new Vector2(0.5f, 1f));
            cooldownFills[index] = shade;

            Text cooldown = CreateText("Cooldown Time", slot, 30, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.35f));
            cooldown.fontStyle = FontStyle.Bold;
            Outline cooldownOutline = cooldown.gameObject.AddComponent<Outline>();
            cooldownOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            cooldownOutline.effectDistance = new Vector2(2f, -2f);
            SetRect(cooldown.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(150f, 76f), new Vector2(0.5f, 1f));
            cooldownTexts[index] = cooldown;
        }

        private RectTransform CreatePanel(string objectName, Transform parent, Color color)
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

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }

        /// <summary>Short display name for a skill; slot defaults are the warrior's.</summary>
        public static string SkillName(SkillDef skill, int index)
        {
            switch (skill != null ? skill.Id : null)
            {
                case "spear-strike": return "Strike";
                case "kick": return "Kick";
                case "shield-block": return "Block";
                case "dash": return "Dash";
                case "fireball": return "Fireball";
                case "frost-nova": return "Frost Nova";
                case "mana-shield": return "Mana Shield";
                case "blink": return "Blink";
                case "arrow": return "Arrow";
                case "piercing-arrow": return "Pierce";
                case "leap-back": return "Leap Back";
                case "concussive-arrow": return "Concuss";
            }

            if (skill != null && !string.IsNullOrEmpty(skill.Id))
            {
                return skill.Id.Replace('-', ' ');
            }

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

        /// <summary>What a skill does, shown while watching the AI.</summary>
        public static string SkillEffect(SkillDef skill, int index)
        {
            switch (skill != null ? skill.Kind : (SkillKind)(index + 1))
            {
                case SkillKind.MeleeStrike: return "spear hit";
                case SkillKind.Kick: return "knockback + stun";
                case SkillKind.Block:
                    return skill != null && skill.BlockAllDirections ? "blocks all sides" : "stagger / parry";
                case SkillKind.Dash: return skill != null && skill.DashBackward ? "jump away" : "burst of speed";
                case SkillKind.Projectile:
                    if (skill.AreaRadius > 0f) return "explodes on hit";
                    if (skill.Pierce) return "goes through all";
                    if (skill.Knockback > 0f) return "knocks back + stun";
                    return "fast shot";
                case SkillKind.AreaBurst: return "slows all nearby";
                case SkillKind.Teleport: return "teleport forward";
                default: return string.Empty;
            }
        }

        private void BuildMatchPanel()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            // Sits under the training panel when there is one, otherwise under the info panel.
            float top = trainingPanel != null ? -644f : -212f;
            RectTransform panel = CreatePanel("Match", canvasRoot, PanelColor);
            SetRect(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, top), new Vector2(430f, 112f), new Vector2(1f, 1f));
            matchPanel = panel.gameObject;

            Button heroButton = CreateButton("Hero Button", panel, new Color(0.3f, 0.22f, 0.12f, 1f), new Vector2(18f, -14f), new Vector2(394f, 38f),
                17, out _, out heroChoiceLabel);
            heroButton.onClick.AddListener(() => HeroClassClicked?.Invoke());

            Button mixButton = CreateButton("Zombie Mix Button", panel, new Color(0.16f, 0.28f, 0.18f, 1f), new Vector2(18f, -60f), new Vector2(394f, 38f),
                17, out _, out mixChoiceLabel);
            mixButton.onClick.AddListener(() => ZombieMixClicked?.Invoke());
        }
    }
}
