using System;
using System.Collections.Generic;
using System.Text;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>
    /// Runtime-built uGUI HUD of the survivor viewer: EXP bar, clock, boss bar, vitals, items, active skills,
    /// the level-up cards with the AI's highlighted choice, the end screen, and the AI / training panels.
    /// It only reads the simulation; the controller owns stepping and the level-up flow.
    /// </summary>
    [DefaultExecutionOrder(1000)] // After SurvivorRenderer and SurvivorCamera, so the hero tag sits on this frame's hero.
    public sealed partial class SurvivorHud : MonoBehaviour
    {
        private const int SkillSlots = SurvivorInput.SkillSlotCount;
        private const int WeaponSlots = SurvivorCatalog.MaxWeapons;
        private const int PassiveSlots = SurvivorCatalog.MaxPassives;

        private SurvivorSim sim;
        private PickHighlight highlight;
        private bool built;

        // Cached values so text is rebuilt only when what it shows changes.
        private int shownSecond = -1;
        private int shownLevel = -1;
        private int shownHp = -1;
        private int shownMaxHp = -1;
        private int shownEnergy = -1;
        private int shownGold = -1;
        private int shownKills = -1;
        private int shownBossHp = -1;
        private int shownEndCountdown = -1;
        private bool endShown;
        private readonly int[] shownItems = new int[WeaponSlots + PassiveSlots];
        private readonly int[] shownItemLevels = new int[WeaponSlots + PassiveSlots];
        private readonly int[] shownCooldownTenths = new int[SkillSlots];
        private PickHighlight.Phase shownPhase = PickHighlight.Phase.None;
        private int shownChosen = -1;
        private float phaseClock;
        private float hpShown = 1f;
        private float hpTrailShown = 1f;
        private float xpShown;
        private float bossShown = 1f;
        private float endCountdown;
        private bool fpsVisible;
        private int fpsFrames;
        private float fpsClock;

        public event Action TrainingButtonClicked;
        public event Action TrainingPowerClicked;

        public TrainingHistoryPanel HistoryPanel => historyPanel;
        public BehaviorProfilePanel ProfilePanel => profilePanel;

        public CharacterPanel CharacterPanel
        {
            get
            {
                EnsureBuilt();
                return characterPanel;
            }
        }

        public AutoFarmPanel FarmPanel
        {
            get
            {
                EnsureBuilt();
                return farmPanel;
            }
        }

        public LoadoutComparePanel ComparePanel
        {
            get
            {
                EnsureBuilt();
                return comparePanel;
            }
        }

        public BrainLineagePanel LineagePanel
        {
            get
            {
                EnsureBuilt();
                return lineagePanel;
            }
        }

        public SettingsPanel SettingsPanel
        {
            get
            {
                EnsureBuilt();
                return settingsPanel;
            }
        }

        /// <summary>Opens or closes the settings (O or the gear button); only one full-screen panel is open at a time.</summary>
        public void ToggleSettingsPanel()
        {
            EnsureBuilt();
            CloseOtherPanels(settingsPanel);
            settingsPanel.Toggle();
        }

        /// <summary>Shows or hides the small FPS counter in the bottom-left corner.</summary>
        public void SetFpsVisible(bool visible)
        {
            EnsureBuilt();
            fpsVisible = visible;
            fpsFrames = 0;
            fpsClock = 0f;
            fpsText.text = "FPS ...";
            fpsText.gameObject.SetActive(visible);
        }

        private void UpdateFps(float delta)
        {
            if (!fpsVisible || fpsText == null)
            {
                return;
            }

            fpsFrames++;
            fpsClock += delta;
            if (fpsClock >= 0.5f)
            {
                fpsText.text = "FPS " + Mathf.RoundToInt(fpsFrames / fpsClock);
                fpsFrames = 0;
                fpsClock = 0f;
            }
        }

        /// <summary>Opens or closes the brain lineage (L); only one full-screen panel is open at a time.</summary>
        public void ToggleLineagePanel()
        {
            EnsureBuilt();
            if (lineagePanel == null || !lineagePanel.IsBound)
            {
                return;
            }

            CloseOtherPanels(lineagePanel);
            lineagePanel.Toggle();
        }

        /// <summary>Opens or closes the training charts; only one full-screen panel is open at a time.</summary>
        public void ToggleHistoryPanel()
        {
            EnsureBuilt();
            CloseOtherPanels(historyPanel);
            historyPanel?.Toggle();
        }

        /// <summary>Opens or closes the AI profile; only one full-screen panel is open at a time.</summary>
        public void ToggleProfilePanel()
        {
            EnsureBuilt();
            CloseOtherPanels(profilePanel);
            profilePanel?.Toggle();
        }

        /// <summary>Opens or closes the character panel (C); only one full-screen panel is open at a time.</summary>
        public void ToggleCharacterPanel()
        {
            EnsureBuilt();
            CloseOtherPanels(characterPanel);
            characterPanel.Toggle();
        }

        /// <summary>Opens or closes the Auto Farm panel (F); only one full-screen panel is open at a time.</summary>
        public void ToggleFarmPanel()
        {
            EnsureBuilt();
            CloseOtherPanels(farmPanel);
            farmPanel.Toggle();
        }

        /// <summary>Opens or closes the build comparison (V); only one full-screen panel is open at a time.</summary>
        public void ToggleComparePanel()
        {
            EnsureBuilt();
            CloseOtherPanels(comparePanel);
            comparePanel.Toggle();
        }

        /// <summary>True when a full-screen panel is open or closed itself with Escape this frame (Escape must not pause then).</summary>
        public bool PanelHandlesEscape()
        {
            return Handles(characterPanel) || Handles(farmPanel) || Handles(comparePanel) || Handles(lineagePanel) || Handles(settingsPanel) ||
                (historyPanel != null && (historyPanel.IsOpen || historyPanel.ConsumedEscapeThisFrame)) ||
                (profilePanel != null && (profilePanel.IsOpen || profilePanel.ConsumedEscapeThisFrame));
        }

        private static bool Handles(MetaPanel panel)
        {
            return panel != null && (panel.IsOpen || panel.ConsumedEscapeThisFrame);
        }

        private void CloseOtherPanels(MonoBehaviour keep)
        {
            if (historyPanel != null && !ReferenceEquals(historyPanel, keep))
            {
                historyPanel.SetOpen(false);
            }
            if (profilePanel != null && !ReferenceEquals(profilePanel, keep))
            {
                profilePanel.SetOpen(false);
            }
            if (characterPanel != null && !ReferenceEquals(characterPanel, keep))
            {
                characterPanel.SetOpen(false);
            }
            if (farmPanel != null && !ReferenceEquals(farmPanel, keep))
            {
                farmPanel.SetOpen(false);
            }
            if (comparePanel != null && !ReferenceEquals(comparePanel, keep))
            {
                comparePanel.SetOpen(false);
            }
            if (lineagePanel != null && !ReferenceEquals(lineagePanel, keep))
            {
                lineagePanel.SetOpen(false);
            }
            if (settingsPanel != null && !ReferenceEquals(settingsPanel, keep))
            {
                settingsPanel.SetOpen(false);
            }
        }

        /// <summary>Where the floating label above the hero is anchored (the renderer's display position, seen by this camera).</summary>
        public void BindHeroLabel(SurvivorRenderer heroSource, Camera viewCamera)
        {
            EnsureBuilt();
            labelSource = heroSource;
            labelCamera = viewCamera;
        }

        /// <summary>The spectator label to show above the hero (None hides it, with a short fade).</summary>
        public void SetHeroLabel(SpectatorLabel label)
        {
            wantedLabel = label;
        }

        /// <summary>End-screen reward text ("+N vàng vào ví", "Mở khóa bậc N!"); cleared on every Bind.</summary>
        public void SetEndReward(string text, bool earned)
        {
            EnsureBuilt();
            endReward.text = text ?? string.Empty;
            endReward.color = earned ? GoldText : new Color(0.7f, 0.75f, 0.85f);
        }

        /// <summary>End-screen story column ("Câu chuyện trận đấu"); cleared on every Bind.</summary>
        public void SetEndStory(string text)
        {
            EnsureBuilt();
            endStory.text = text ?? string.Empty;
        }

        /// <summary>Shows a run; call again after every reset (the HUD re-reads everything).</summary>
        public void Bind(SurvivorSim survivorSim, PickHighlight pickHighlight)
        {
            EnsureBuilt();
            sim = survivorSim;
            highlight = pickHighlight;
            ResetCaches();
            heroName.text = SurvivorViewLogic.HeroNameLine(sim?.Config.ClassDef?.Id);
            Refresh(0f);
        }

        /// <summary>Big gold banner over the arena that fades out after a few seconds (e.g. "TIẾN HÓA: Bão Sét").</summary>
        public void ShowToast(string text, Color color)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            toastText.text = text;
            toastText.color = color;
            toastClock = ToastSeconds;
            toastText.gameObject.SetActive(true);
        }

        private void UpdateToast(float delta)
        {
            if (!built || toastClock <= 0f)
            {
                return;
            }
            toastClock -= delta;
            if (toastClock <= 0f)
            {
                toastText.gameObject.SetActive(false);
                return;
            }
            // Pops in over 0.15 s, holds, then fades over the last 0.8 s.
            float age = ToastSeconds - toastClock;
            float scale = age < 0.15f ? Mathf.Lerp(1.4f, 1f, age / 0.15f) : 1f;
            toastText.rectTransform.localScale = new Vector3(scale, scale, 1f);
            Color color = toastText.color;
            color.a = Mathf.Clamp01(toastClock / 0.8f);
            toastText.color = color;
        }

        public void SetInfoText(string text)
        {
            EnsureBuilt();
            infoText.text = text ?? string.Empty;
        }

        public void SetHelpText(string text)
        {
            EnsureBuilt();
            helpText.text = text ?? string.Empty;
        }

        public void SetPaused(bool paused)
        {
            EnsureBuilt();
            pausePanel.SetActive(paused);
        }

        /// <summary>Big centered message (e.g. "no brain yet"); null or empty hides it.</summary>
        public void SetNotice(string text)
        {
            EnsureBuilt();
            bool show = !string.IsNullOrEmpty(text);
            noticePanel.SetActive(show);
            if (show)
            {
                noticeText.text = text;
            }
        }

        /// <summary>Seconds until the next run starts, shown on the end screen.</summary>
        public void SetEndCountdown(float seconds)
        {
            endCountdown = seconds;
        }

        private void ResetCaches()
        {
            shownSecond = -1;
            shownLevel = -1;
            shownHp = -1;
            shownMaxHp = -1;
            shownEnergy = -1;
            shownGold = -1;
            shownKills = -1;
            shownBossHp = -1;
            shownEndCountdown = -1;
            endShown = false;
            for (int i = 0; i < shownItems.Length; i++)
            {
                shownItems[i] = int.MinValue;
                shownItemLevels[i] = int.MinValue;
            }
            for (int i = 0; i < SkillSlots; i++)
            {
                shownCooldownTenths[i] = -1;
                skillSlots[i].Bound = false;
            }
            toastClock = 0f;
            toastText.gameObject.SetActive(false);
            if (sim != null)
            {
                LayoutSkillSlots(sim.Config.ClassDef?.ActiveSkills);
                for (int i = 0; i < WeaponSlots; i++)
                {
                    itemSlots[i].Frame.gameObject.SetActive(i < sim.Config.Tuning.MaxWeaponSlots);
                }
                for (int i = 0; i < PassiveSlots; i++)
                {
                    itemSlots[WeaponSlots + i].Frame.gameObject.SetActive(i < sim.Config.Tuning.MaxPassiveSlots);
                }
            }
            shownPhase = PickHighlight.Phase.None;
            shownChosen = -1;
            hpShown = 1f;
            hpTrailShown = 1f;
            xpShown = 0f;
            bossShown = 1f;
            offerPanel.SetActive(false);
            endPanel.SetActive(false);
            bossPanel.SetActive(false);
            endReward.text = string.Empty;
            endStory.text = ChronicleText.EmptyText;
            wantedLabel = SpectatorLabel.None;
            shownLabel = SpectatorLabel.None;
            labelAlpha = 0f;
            heroLabelGroup.alpha = 0f;
        }

        private void Awake()
        {
            double at = PerfTrace.Now;
            EnsureBuilt();
            PerfTrace.Span("hud built", at);
        }

        private void LateUpdate()
        {
            double at = PerfTrace.Now;
            Refresh(Time.unscaledDeltaTime);
            UpdateHeroLabel(Time.unscaledDeltaTime);
            UpdateToast(Time.unscaledDeltaTime);
            UpdateFps(Time.unscaledDeltaTime);
            UpdateTooltip();
            PerfTrace.Span("hud late update", at, 20.0);
        }

        /// <summary>Fades the spectator tag in/out (~0.2 s) and keeps it above the hero on screen.</summary>
        private void UpdateHeroLabel(float delta)
        {
            if (!built)
            {
                return;
            }

            bool canShow = sim != null && !sim.IsEnded && labelSource != null && labelCamera != null;
            SpectatorLabel wanted = canShow ? wantedLabel : SpectatorLabel.None;
            // A new label waits until the old one has faded out, then fades in.
            float target = wanted != SpectatorLabel.None && wanted == shownLabel ? 1f : 0f;
            labelAlpha = Mathf.MoveTowards(labelAlpha, target, delta / HeroLabelFadeSeconds);
            if (labelAlpha <= 0f && wanted != shownLabel)
            {
                shownLabel = wanted;
                if (shownLabel != SpectatorLabel.None)
                {
                    Color color = HeroLabelColor(shownLabel);
                    heroLabelText.text = SpectatorLabels.DisplayName(shownLabel);
                    heroLabelText.color = color;
                    heroLabelAccent.color = color;
                }
            }

            bool visible = labelAlpha > 0.001f && shownLabel != SpectatorLabel.None;
            if (heroLabel.gameObject.activeSelf != visible)
            {
                heroLabel.gameObject.SetActive(visible);
            }
            if (!visible)
            {
                return;
            }

            Vector3 screen = labelCamera.WorldToScreenPoint(labelSource.HeroWorldPosition + Vector3.up * HeroLabelHeight);
            if (screen.z <= 0f ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvasRoot, screen, null, out Vector2 local))
            {
                heroLabel.gameObject.SetActive(false);
                return;
            }

            heroLabelGroup.alpha = labelAlpha;
            heroLabel.anchoredPosition = local;
        }

        private static Color HeroLabelColor(SpectatorLabel label)
        {
            switch (label)
            {
                case SpectatorLabel.Kiting: return new Color(0.4f, 0.78f, 1f, 1f);
                case SpectatorLabel.Looting: return new Color(1f, 0.84f, 0.3f, 1f);
                case SpectatorLabel.Charging: return new Color(1f, 0.45f, 0.32f, 1f);
                case SpectatorLabel.Escaping: return new Color(0.48f, 0.95f, 0.58f, 1f);
                default: return Color.white;
            }
        }

        // ------------------------------------------------------------------ training panel API

        public void ShowTrainingPanel(bool show)
        {
            EnsureBuilt();
            if (show && trainingPanel == null)
            {
                BuildTrainingPanel();
            }
            if (trainingShown != show)
            {
                trainingShown = show;
                LayoutRightColumn();
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

        /// <summary>Draws the mean-reward history as bars (oldest left).</summary>
        public void SetTrainingGraph(IReadOnlyList<float> values, string caption)
        {
            ShowTrainingPanel(true);
            trainingGraphCaption.text = caption ?? string.Empty;
            float[] buckets = TrainingHistory.Bucket(values, TrainingBarCount);
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
    }
}
