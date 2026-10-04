using System;
using System.Collections.Generic;
using System.Text;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>Refreshes the HUD from the sim each frame: top bar, vitals, buff chips, items, skills, the level-up offer and the end screen.</summary>
    public sealed partial class SurvivorHud
    {
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
                clockGoal.text = bossPhase ? "KILL THE BOSS BY " + SurvivorViewLogic.FormatClock(sim.Config.BossExpireSeconds) : "SURVIVE UNTIL " + SurvivorViewLogic.FormatClock(sim.Config.RunSeconds);
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
                    bossText.text = "BONE LORD   " + hp + " / " + Mathf.CeilToInt(boss.MaxHp);
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
                hpText.text = "HP  " + hp + " / " + maxHp;
            }

            float energyRatio = hero.MaxEnergy > 0f ? Mathf.Clamp01(hero.Energy / hero.MaxEnergy) : 0f;
            SetFill(energyFill, energyRatio);
            int energy = Mathf.FloorToInt(hero.Energy);
            if (energy != shownEnergy)
            {
                shownEnergy = energy;
                energyText.text = "ENERGY  " + energy + " / " + Mathf.CeilToInt(hero.MaxEnergy);
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
            RefreshBuffChips();
        }

        /// <summary>Effect demo (`-fxDemo`): show all three buff chips at fixed times so a screenshot can check them.</summary>
        public bool BuffDemo { get; set; }

        /// <summary>Shows a chip per running pickup buff (rage, shield, haste), stacked without gaps, with its time left.</summary>
        private void RefreshBuffChips()
        {
            int row = 0;
            for (int i = 0; i < BuffChipCount; i++)
            {
                BuffKind kind = (BuffKind)i;
                float remaining = kind == BuffKind.Rage ? sim.RageRemaining : kind == BuffKind.Shield ? sim.ShieldRemaining : sim.HasteRemaining;
                if (BuffDemo)
                {
                    remaining = SurvivorViewLogic.BuffSeconds(kind) * (0.9f - 0.3f * i);
                }
                bool active = remaining > 0f;
                if (buffChips[i].gameObject.activeSelf != active)
                {
                    buffChips[i].gameObject.SetActive(active);
                    shownBuffSeconds[i] = -1;
                }
                if (!active)
                {
                    continue;
                }
                buffChips[i].anchoredPosition = new Vector2(470f, -40f - row * 38f);
                row++;
                buffFills[i].anchorMax = new Vector2(Mathf.Clamp01(remaining / SurvivorViewLogic.BuffSeconds(kind)), 1f);
                int seconds = Mathf.CeilToInt(remaining);
                if (seconds != shownBuffSeconds[i])
                {
                    shownBuffSeconds[i] = seconds;
                    buffTexts[i].text = SurvivorViewLogic.BuffChipText(kind, remaining);
                }
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
                offerStatus.text = chosen >= 0 && def != null ? "AI picks:  " + def.Name : "The AI is choosing...";
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
            float spacing = OfferCardWidth + 22f;
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
                card.Kind.text = SurvivorViewLogic.ItemKindLabel(def);
                // A new item also says how it evolves (in gold), so the pick can be judged.
                string evolution = level <= 1 ? SurvivorItemDetails.Evolution(item) : string.Empty;
                card.Description.text = SurvivorViewLogic.ItemDescription(item, level) +
                    (evolution.Length > 0 ? "\n\n<color=#E6C36A>" + evolution + "</color>" : string.Empty);
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
                endCause.text = sim.EndReason == EndReason.Died ? "Cause: " + SurvivorViewLogic.DeathCauseText(sim.DeathCause) : string.Empty;
                endStats.text =
                    "Time  " + SurvivorViewLogic.FormatClock(sim.Time) +
                    "\nLevel  " + sim.Level +
                    "\nGold  " + Mathf.FloorToInt(sim.Gold) +
                    "\nKills  " + sim.Kills + (sim.EliteKills > 0 ? "   (elites " + sim.EliteKills + ")" : string.Empty);
                endItems.text = FinalItems(sim.Inventory);
            }

            int countdown = Mathf.CeilToInt(Mathf.Max(0f, endCountdown));
            if (countdown != shownEndCountdown)
            {
                shownEndCountdown = countdown;
                endFooter.text = countdown > 0 ? "Next run in " + countdown + "s" : "Starting a new run...";
            }
        }

        private static string FinalItems(SurvivorInventory inventory)
        {
            StringBuilder builder = new StringBuilder("Final build:\n");
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
    }
}
