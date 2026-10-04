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
            EnsureBuilt();
        }

        private void LateUpdate()
        {
            Refresh(Time.unscaledDeltaTime);
            UpdateHeroLabel(Time.unscaledDeltaTime);
            UpdateToast(Time.unscaledDeltaTime);
            UpdateFps(Time.unscaledDeltaTime);
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

        private void Refresh(float delta)
        {
            if (!built || sim == null)
            {
                return;
            }

            RefreshTop(delta);
            RefreshVitals(delta);
            RefreshItems();
            RefreshSkills(delta);
            RefreshOffer(delta);
            RefreshEnd();
        }

        private void RefreshTop(float delta)
        {
            float xpRatio = sim.XpToNext > 0 ? Mathf.Clamp01(sim.Xp / sim.XpToNext) : 0f;
            if (sim.Level != shownLevel)
            {
                // A level up empties the bar at once instead of draining it backwards.
                if (shownLevel >= 0 && sim.Level > shownLevel)
                {
                    xpShown = 0f;
                }
                shownLevel = sim.Level;
                levelText.text = "Lv " + sim.Level;
            }
            xpShown = Mathf.MoveTowards(xpShown, xpRatio, delta * 1.5f + Mathf.Abs(xpRatio - xpShown) * delta * 8f);
            SetFill(xpFill, xpShown);

            int second = Mathf.FloorToInt(Mathf.Max(0f, sim.Time));
            if (second != shownSecond)
            {
                shownSecond = second;
                clockText.text = SurvivorViewLogic.FormatClock(sim.Time);
                bool bossPhase = sim.Time >= sim.Config.RunSeconds;
                clockGoal.text = bossPhase ? "HẠ TRÙM TRƯỚC " + SurvivorViewLogic.FormatClock(sim.Config.BossExpireSeconds) : "SỐNG SÓT TỚI " + SurvivorViewLogic.FormatClock(sim.Config.RunSeconds);
                clockText.color = bossPhase ? new Color(1f, 0.55f, 0.45f) : Color.white;
            }

            SurvivorEnemy boss = sim.BossEnemy;
            bool showBoss = boss != null && !sim.IsEnded;
            if (bossPanel.activeSelf != showBoss)
            {
                bossPanel.SetActive(showBoss);
                bossShown = 1f;
            }
            if (showBoss)
            {
                float ratio = boss.MaxHp > 0f ? Mathf.Clamp01(boss.Hp / boss.MaxHp) : 0f;
                bossShown = Mathf.MoveTowards(bossShown, ratio, delta * 0.5f + Mathf.Abs(ratio - bossShown) * delta * 6f);
                SetFill(bossFill, bossShown);
                int hp = Mathf.CeilToInt(boss.Hp);
                if (hp != shownBossHp)
                {
                    shownBossHp = hp;
                    bossText.text = "TRÙM XƯƠNG   " + hp + " / " + Mathf.CeilToInt(boss.MaxHp);
                }
            }
        }

        private void RefreshVitals(float delta)
        {
            SurvivorHero hero = sim.Hero;
            float hpRatio = hero.MaxHp > 0f ? Mathf.Clamp01(hero.Hp / hero.MaxHp) : 0f;
            hpShown = Mathf.MoveTowards(hpShown, hpRatio, delta * 2.5f + Mathf.Abs(hpRatio - hpShown) * delta * 10f);
            hpTrailShown = hpTrailShown < hpShown ? hpShown : Mathf.MoveTowards(hpTrailShown, hpShown, delta * 0.45f);
            SetFill(hpFill, hpShown);
            SetFill(hpTrail, hpTrailShown);
            bool lowHp = hpRatio < 0.3f && hero.Alive;
            float pulse = lowHp ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
            hpFillImage.color = Color.Lerp(HpColor, HpLowColor, pulse);
            int hp = Mathf.CeilToInt(hero.Hp);
            int maxHp = Mathf.CeilToInt(hero.MaxHp);
            if (hp != shownHp || maxHp != shownMaxHp)
            {
                shownHp = hp;
                shownMaxHp = maxHp;
                hpText.text = "MÁU  " + hp + " / " + maxHp;
            }

            float energyRatio = hero.MaxEnergy > 0f ? Mathf.Clamp01(hero.Energy / hero.MaxEnergy) : 0f;
            SetFill(energyFill, energyRatio);
            int energy = Mathf.FloorToInt(hero.Energy);
            if (energy != shownEnergy)
            {
                shownEnergy = energy;
                energyText.text = "NĂNG LƯỢNG  " + energy + " / " + Mathf.CeilToInt(hero.MaxEnergy);
            }

            int gold = Mathf.FloorToInt(sim.Gold);
            if (gold != shownGold)
            {
                shownGold = gold;
                goldText.text = gold.ToString();
            }
            if (sim.Kills != shownKills)
            {
                shownKills = sim.Kills;
                killsText.text = sim.Kills.ToString();
            }
        }

        private void RefreshItems()
        {
            SurvivorInventory inventory = sim.Inventory;
            for (int i = 0; i < WeaponSlots; i++)
            {
                int weapon = i < inventory.WeaponCount ? inventory.WeaponAt(i) : -1;
                ApplyItemSlot(i, weapon, weapon >= 0 ? inventory.Level(weapon) : 0);
            }
            for (int i = 0; i < PassiveSlots; i++)
            {
                int passive = i < inventory.PassiveCount ? inventory.PassiveAt(i) : -1;
                ApplyItemSlot(WeaponSlots + i, passive, passive >= 0 ? inventory.Level(passive) : 0);
            }
        }

        private void ApplyItemSlot(int slot, int item, int level)
        {
            if (shownItems[slot] == item && shownItemLevels[slot] == level)
            {
                return;
            }
            shownItems[slot] = item;
            shownItemLevels[slot] = level;
            ItemSlotView view = itemSlots[slot];
            ItemDef def = SurvivorCatalog.Get(item);
            if (def == null)
            {
                view.Icon.enabled = false;
                view.Level.text = string.Empty;
                view.Frame.color = new Color(1f, 1f, 1f, 0.06f);
                return;
            }
            view.Icon.enabled = true;
            view.Icon.sprite = SkillIconFactory.IconForItem(item);
            view.Level.text = level >= def.MaxLevel ? "MAX" : level.ToString();
            view.Level.color = level >= def.MaxLevel ? GoldText : Color.white;
            Color frame = SurvivorViewLogic.ItemColor(item);
            frame.a = 0.55f;
            view.Frame.color = frame;
        }

        private void RefreshSkills(float delta)
        {
            SkillDef[] skills = sim.Config.ClassDef.ActiveSkills;
            SurvivorHero hero = sim.Hero;
            for (int i = 0; i < SkillSlots; i++)
            {
                SkillDef skill = i < skills.Length ? skills[i] : null;
                SkillSlotView view = skillSlots[i];
                if (!SurvivorViewLogic.SkillVisible(skill))
                {
                    view.Skill = skill;
                    continue;
                }
                if (!view.Bound || view.Skill != skill)
                {
                    view.Bound = true;
                    view.Skill = skill;
                    view.Color = SkillIconFactory.ColorFor(skill, SkillIconIndex(skill, i));
                    view.Icon.sprite = SkillIconFactory.IconFor(skill, SkillIconIndex(skill, i));
                    view.Title.text = SurvivorViewLogic.SkillTitle(skill);
                    Color accent = view.Color;
                    accent.a = 0.75f;
                    view.Accent.color = accent;
                }

                float cooldown = i < hero.SkillCooldowns.Length ? hero.SkillCooldowns[i] : 0f;
                float maximum = skill != null ? skill.Cooldown : 0f;
                if (cooldown > view.LastCooldown + 0.01f)
                {
                    view.Flash = 1f;
                }
                view.LastCooldown = cooldown;
                bool active = skill != null && skill.Kind == SkillKind.Block && hero.Blocking;
                view.Flash = Mathf.Max(active ? 0.6f : 0f, view.Flash - delta * 3.5f);
                view.Shade.fillAmount = maximum > 0f ? Mathf.Clamp01(cooldown / maximum) : 0f;
                int tenths = cooldown > 0.05f ? Mathf.CeilToInt(cooldown * 10f) : 0;
                if (tenths != shownCooldownTenths[i])
                {
                    shownCooldownTenths[i] = tenths;
                    view.Cooldown.text = tenths > 0 ? (tenths / 10f).ToString("0.0") : string.Empty;
                }
                bool starved = skill != null && skill.EnergyCost > 0f && hero.Energy < skill.EnergyCost;
                Color glow = view.Color;
                glow.a = view.Flash * 0.55f;
                view.Glow.color = glow;
                view.Icon.color = cooldown > 0.05f || starved ? new Color(0.62f, 0.62f, 0.68f, 1f) : Color.white;
            }
        }

        private static int SkillIconIndex(SkillDef skill, int slot)
        {
            // SkillIconFactory's slot defaults are the old four-slot layout (strike, kick, block, dash).
            return skill != null ? slot : slot + 1;
        }

        private void RefreshOffer(float delta)
        {
            PickHighlight.Phase phase = highlight != null ? highlight.Current : PickHighlight.Phase.None;
            if (phase != shownPhase)
            {
                if (phase == PickHighlight.Phase.Offer || shownPhase == PickHighlight.Phase.None)
                {
                    phaseClock = 0f;
                }
                shownPhase = phase;
                shownChosen = -1;
                offerPanel.SetActive(phase != PickHighlight.Phase.None);
                if (phase == PickHighlight.Phase.Offer)
                {
                    FillCards();
                }
            }
            if (phase == PickHighlight.Phase.None)
            {
                return;
            }

            phaseClock += delta;
            int chosen = phase == PickHighlight.Phase.Highlight ? highlight.ChosenSlot : -1;
            if (chosen != shownChosen)
            {
                shownChosen = chosen;
                ItemDef def = SurvivorCatalog.Get(highlight.Item(chosen));
                offerStatus.text = chosen >= 0 && def != null ? "AI chọn:  " + def.Name : "AI đang chọn...";
                offerStatus.color = chosen >= 0 ? GoldText : new Color(0.8f, 0.85f, 0.95f);
            }

            float reveal = Mathf.Clamp01(phaseClock / 0.25f);
            float highlightEase = phase == PickHighlight.Phase.Highlight ? Mathf.Clamp01(highlight.Progress * 4f) : 0f;
            for (int i = 0; i < cards.Length; i++)
            {
                OfferCard card = cards[i];
                if (!card.Root.gameObject.activeSelf)
                {
                    continue;
                }
                bool isChosen = i == chosen;
                card.Group.alpha = reveal;
                card.Shade.color = new Color(0.02f, 0.02f, 0.04f, chosen >= 0 && !isChosen ? 0.72f * highlightEase : 0f);
                float lift = (1f - reveal) * -40f;
                float scale = isChosen ? 1f + 0.1f * highlightEase + 0.02f * Mathf.Sin(Time.unscaledTime * 10f) * highlightEase : 1f;
                card.Root.anchoredPosition = new Vector2(card.BaseX, lift + (isChosen ? 16f * highlightEase : 0f));
                card.Root.localScale = new Vector3(scale, scale, 1f);
                Color border = isChosen ? Color.Lerp(card.Accent, GoldText, highlightEase) : card.Accent;
                border.a = isChosen ? 0.6f + 0.4f * highlightEase : 0.45f;
                card.Border.color = border;
                card.Glow.color = new Color(GoldText.r, GoldText.g, GoldText.b, isChosen ? 0.45f * highlightEase : 0f);
            }
        }

        private void FillCards()
        {
            int count = highlight.Count;
            float spacing = 290f;
            float start = -(count - 1) * spacing * 0.5f;
            for (int i = 0; i < cards.Length; i++)
            {
                OfferCard card = cards[i];
                bool visible = i < count;
                card.Root.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }
                int item = highlight.Item(i);
                int level = highlight.ItemLevel(i);
                ItemDef def = SurvivorCatalog.Get(item);
                card.BaseX = start + i * spacing;
                card.Accent = SurvivorViewLogic.ItemColor(item);
                card.Icon.sprite = def != null ? SkillIconFactory.IconForItem(item) : SkillIconFactory.IconForKey(string.Empty, card.Accent);
                card.Name.text = def != null ? def.Name : "?";
                card.Level.text = SurvivorViewLogic.LevelLabel(item, level);
                card.Level.color = level <= 1 ? new Color(0.5f, 1f, 0.6f) : GoldText;
                card.Kind.text = def == null ? string.Empty : def.Kind == ItemKind.Weapon ? "VŨ KHÍ" : def.Kind == ItemKind.Passive ? "BỊ ĐỘNG" : "PHẦN THƯỞNG";
                card.Description.text = SurvivorViewLogic.ItemDescription(item, level);
                card.Group.alpha = 0f;
            }
        }

        private void RefreshEnd()
        {
            bool ended = sim.IsEnded;
            if (ended != endPanel.activeSelf)
            {
                endPanel.SetActive(ended);
            }
            if (!ended)
            {
                endShown = false;
                return;
            }

            if (!endShown)
            {
                endShown = true;
                shownEndCountdown = -1;
                endTitle.text = SurvivorViewLogic.EndTitle(sim.EndReason);
                endTitle.color = SurvivorViewLogic.EndColor(sim.EndReason);
                endCause.text = sim.EndReason == EndReason.Died ? "Nguyên nhân: " + SurvivorViewLogic.DeathCauseText(sim.DeathCause) : string.Empty;
                endStats.text =
                    "Thời gian  " + SurvivorViewLogic.FormatClock(sim.Time) +
                    "\nCấp  " + sim.Level +
                    "\nVàng  " + Mathf.FloorToInt(sim.Gold) +
                    "\nHạ gục  " + sim.Kills + (sim.EliteKills > 0 ? "   (tinh anh " + sim.EliteKills + ")" : string.Empty);
                endItems.text = FinalItems(sim.Inventory);
            }

            int countdown = Mathf.CeilToInt(Mathf.Max(0f, endCountdown));
            if (countdown != shownEndCountdown)
            {
                shownEndCountdown = countdown;
                endFooter.text = countdown > 0 ? "Trận mới sau " + countdown + " giây" : "Đang bắt đầu trận mới...";
            }
        }

        private static string FinalItems(SurvivorInventory inventory)
        {
            StringBuilder builder = new StringBuilder("Trang bị cuối:\n");
            int written = 0;
            for (int i = 0; i < inventory.WeaponCount; i++)
            {
                AppendItem(builder, inventory.WeaponAt(i), inventory, ref written);
            }
            for (int i = 0; i < inventory.PassiveCount; i++)
            {
                AppendItem(builder, inventory.PassiveAt(i), inventory, ref written);
            }
            return builder.ToString();
        }

        private static void AppendItem(StringBuilder builder, int index, SurvivorInventory inventory, ref int written)
        {
            ItemDef def = SurvivorCatalog.Get(index);
            if (def == null)
            {
                return;
            }
            if (written > 0)
            {
                builder.Append(written % 2 == 0 ? "\n" : "     ");
            }
            builder.Append(def.Name).Append(" Lv").Append(inventory.Level(index));
            written++;
        }

        private static void SetFill(RectTransform fill, float ratio)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        // ------------------------------------------------------------------ training panel API

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
