using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View
{
    /// <summary>
    /// Detailed text about items and skills, read from the catalog numbers: what an item does, its numbers at a
    /// level, what the next level changes and how it evolves. Used by the HUD tooltips and the level-up cards.
    /// The numbers are the item's own (before the hero's Might, Area and Cooldown bonuses).
    /// </summary>
    public static class SurvivorItemDetails
    {
        private readonly struct Stat
        {
            public readonly string Label;
            public readonly string Value;

            public Stat(string label, string value)
            {
                Label = label;
                Value = value;
            }
        }

        /// <summary>One sentence on what the item does; empty for unknown rows.</summary>
        public static string Summary(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def == null)
            {
                return string.Empty;
            }
            if (def.EvolvesFrom >= 0)
            {
                ItemDef baseDef = SurvivorCatalog.Get(def.EvolvesFrom);
                return baseDef == null ? string.Empty : "The evolved " + baseDef.Name + ": far stronger, with longer reach and a shorter cooldown.";
            }
            if (def.Kind == ItemKind.Passive)
            {
                return PassiveEffect(def, 1) + " per level.";
            }
            switch (def.Id)
            {
                case "sword-sweep": return "Throws a sword wave forward that cuts every enemy in its path.";
                case "spear-thrust": return "Thrusts a spear through the enemies in a line.";
                case "orbit-axe": return "Axes circle the hero and hit whatever they touch.";
                case "thrown-hammer": return "Throws hammers at the nearest enemies.";
                case "aura": return "A burning aura damages enemies standing close.";
                case "shockwave": return "A shockwave spreads out and knocks enemies back.";
                case "combo-blade": return "Three quick slashes in a row; the last one hits twice as hard.";
                case "heavy-hammer": return "A slow, heavy hammer that ploughs through several enemies.";
                case "retaliate": return "No attack of its own: when the hero is hit, it blasts everything around.";
                case "magic-bolt": return "Fires magic bolts at the nearest enemies.";
                case "fire-orb": return "Fireballs circle the hero and burn what they touch.";
                case "frost-nova": return "A ring of frost spreads out and stuns enemies.";
                case "holy-field": return "Holy light damages enemies standing close.";
                case "lightning": return "Lightning strikes random enemies in range with a small blast.";
                case "arcane-beam": return "A ray that cuts through every enemy in a line.";
                case "time-clock": return "Has a chance to freeze every enemy nearby except the boss.";
                case "purge": return "Kills every normal enemy nearby.";
                case "bomb-ring": return "A salvo of blasts on a ring that turns around the hero.";
                case "fireball-nova": return "A big fireball explodes on a random enemy in range.";
                case "magi-stone": return "Instant hits of fixed damage on the nearest enemies (ignores Might and crits).";
                case "arrow": return "Fires arrows at the nearest enemies.";
                case "multi-shot": return "Fires a fan of arrows toward the nearest enemy.";
                case "arrow-rain": return "Arrows rain down on the densest group of enemies.";
                case "orbit-knife": return "Daggers circle the hero quickly.";
                case "dagger": return "A fast half-circle slash in front of the hero.";
                case "crossbow": return "A heavy bolt that pierces every enemy in a line.";
                case "momentum-spirit": return "Fires along the hero's movement; hits harder the more the hero moves.";
                case "quad-shot": return "Shoots in four fixed directions around the hero.";
                case "flame-cone": return "A short cone of fire in front, firing very fast.";
                case "bomb": return "Throws a bomb that explodes on its first hit or at the end of its flight.";
                case "barrier": return "A shield that absorbs hits and comes back after a recharge.";
                case "boomerang": return "Flies out and back, hitting each enemy once each way.";
                case "poison-pool": return "Drops a pool of poison that damages enemies standing in it.";
                case "bounce-shot": return "A shot that bounces off walls and ricochets from enemy to enemy.";
                case "bracelet-trio": return "Three tight shots at one random enemy in range.";
                case "spirit-orbs": return "Orbs that never stop circling the hero at mid range.";
                case "saw-ring": return "Saws spin close around the hero and grind whatever touches them.";
                case "frost-halo": return "Ice shards circle the other way and shove enemies back.";
                case "comet": return "A heavy comet sweeps a wide circle and knocks enemies away.";
                case "bonus-gold": return "Gain 25 gold now.";
                case "bonus-heal": return "Heal 30 HP now.";
                default: return string.Empty;
            }
        }

        /// <summary>The item's numbers at <paramref name="level"/>, one "Label value" per line.</summary>
        public static string Stats(int catalogIndex, int level)
        {
            return Join(Collect(SurvivorCatalog.Get(catalogIndex), level), "\n", 99);
        }

        /// <summary>The first few numbers at <paramref name="level"/> on one line, for a level-up card.</summary>
        public static string KeyStats(int catalogIndex, int level)
        {
            return Join(Collect(SurvivorCatalog.Get(catalogIndex), level), "  ·  ", 3);
        }

        /// <summary>What reaching <paramref name="nextLevel"/> changes, one per line, e.g. "Damage 20 → 28" and "Projectiles 1 → 2".</summary>
        public static string Upgrade(int catalogIndex, int nextLevel)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def == null || nextLevel <= 1)
            {
                return string.Empty;
            }
            if (def.Kind == ItemKind.Passive)
            {
                return "Total " + PassiveEffect(def, nextLevel) + "\n(" + PassiveEffect(def, 1) + " per level)";
            }

            List<Stat> before = Collect(def, nextLevel - 1);
            List<Stat> after = Collect(def, nextLevel);
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < after.Count; i++)
            {
                string old = null;
                for (int k = 0; k < before.Count; k++)
                {
                    if (before[k].Label == after[i].Label)
                    {
                        old = before[k].Value;
                    }
                }
                if (old == after[i].Value)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }
                builder.Append(after[i].Label).Append(' ');
                builder.Append(old == null ? after[i].Value : old + " → " + after[i].Value);
            }
            return builder.ToString();
        }

        /// <summary>A passive's whole bonus at <paramref name="level"/>, e.g. "+50% max HP"; empty for other rows.</summary>
        public static string PassiveTotal(int catalogIndex, int level)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            return def != null && def.Kind == ItemKind.Passive ? PassiveEffect(def, level) : string.Empty;
        }

        /// <summary>How the item evolves or what it evolved from; for a passive, the weapons it evolves. Empty when none.</summary>
        public static string Evolution(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def == null)
            {
                return string.Empty;
            }
            if (def.EvolvesFrom >= 0)
            {
                ItemDef baseDef = SurvivorCatalog.Get(def.EvolvesFrom);
                ItemDef passive = SurvivorCatalog.Get(def.EvolutionPassive);
                return baseDef == null ? string.Empty : "Evolved from " + baseDef.Name + (passive != null ? " with " + passive.Name : string.Empty) + ".";
            }
            if (def.Kind == ItemKind.Weapon)
            {
                ItemDef evolved = SurvivorCatalog.Get(SurvivorCatalog.EvolutionOf(catalogIndex));
                if (evolved == null)
                {
                    return string.Empty;
                }
                ItemDef passive = SurvivorCatalog.Get(evolved.EvolutionPassive);
                return "Evolves into " + evolved.Name + ": reach Lv " + def.MaxLevel +
                    (passive != null ? ", own " + passive.Name : string.Empty) + ", then open a chest.";
            }
            if (def.Kind != ItemKind.Passive)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < SurvivorCatalog.CatalogSize; index++)
            {
                ItemDef other = SurvivorCatalog.Get(index);
                if (other == null || other.EvolvesFrom < 0 || other.EvolutionPassive != catalogIndex)
                {
                    continue;
                }
                ItemDef baseDef = SurvivorCatalog.Get(other.EvolvesFrom);
                if (baseDef == null)
                {
                    continue;
                }
                builder.Append(builder.Length == 0 ? "Evolves: " : ", ").Append(baseDef.Name).Append(" → ").Append(other.Name);
            }
            return builder.Length == 0 ? string.Empty : builder.Append('.').ToString();
        }

        /// <summary>Tooltip body of an owned item: what it does, its numbers at its level and its evolution.</summary>
        public static string Tooltip(int catalogIndex, int level)
        {
            StringBuilder builder = new StringBuilder(Summary(catalogIndex));
            AppendBlock(builder, Stats(catalogIndex, level));
            AppendBlock(builder, Evolution(catalogIndex));
            return builder.ToString();
        }

        /// <summary>Level-up card text: a new item's summary and key numbers, or what the upgrade changes.</summary>
        public static string CardText(int catalogIndex, int nextLevel)
        {
            if (nextLevel > 1)
            {
                return Upgrade(catalogIndex, nextLevel);
            }
            StringBuilder builder = new StringBuilder(Summary(catalogIndex));
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def != null && def.Kind == ItemKind.Weapon)
            {
                AppendBlock(builder, KeyStats(catalogIndex, 1));
            }
            return builder.ToString();
        }

        /// <summary>A skill's numbers, one "Label value" per line.</summary>
        public static string SkillStats(SkillDef skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }
            List<Stat> stats = new List<Stat>();
            Add(stats, "Cooldown", skill.Cooldown, " s");
            Add(stats, "Energy", skill.EnergyCost, string.Empty);
            Add(stats, "Energy", skill.EnergyPerSecond, " per second");
            Add(stats, "Damage", skill.Damage, skill.TickSeconds > 0f ? " every " + Number(skill.TickSeconds) + " s" : string.Empty);
            Add(stats, "Range", skill.Range, " m");
            Add(stats, "Radius", skill.AreaRadius, " m");
            Add(stats, "Distance", skill.DashDistance, " m");
            if (skill.Count > 0)
            {
                stats.Add(new Stat(skill.Kind == SkillKind.Chain ? "Targets" : "Count", skill.Count.ToString(CultureInfo.InvariantCulture)));
            }
            Add(stats, "Lasts", skill.Duration, " s");
            Add(stats, "Stun", skill.StunSeconds, " s");
            Add(stats, "Slow", skill.SlowSeconds, " s");
            if (skill.Kind == SkillKind.Block && skill.BlockDamageMultiplier < 1f)
            {
                stats.Add(new Stat("Blocks", Percent(1f - skill.BlockDamageMultiplier) + " of the damage"));
            }
            Add(stats, "Counter window", skill.ParryWindowSeconds, " s");
            if (skill.Pierce)
            {
                stats.Add(new Stat("Pierces", "every enemy"));
            }
            return Join(stats, "\n", 99);
        }

        /// <summary>Tooltip body of a skill: its description and numbers.</summary>
        public static string SkillTooltip(SkillDef skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }
            StringBuilder builder = new StringBuilder(SurvivorViewLogic.SkillDescription(skill.Id));
            if (builder.Length > 0)
            {
                builder.Append('.');
            }
            AppendBlock(builder, SkillStats(skill));
            return builder.ToString();
        }

        private static List<Stat> Collect(ItemDef def, int level)
        {
            List<Stat> stats = new List<Stat>();
            if (def == null)
            {
                return stats;
            }
            int capped = level < 1 ? 1 : def.MaxLevel > 0 && level > def.MaxLevel ? def.MaxLevel : level;
            if (def.Kind == ItemKind.Passive)
            {
                stats.Add(new Stat("Now", PassiveEffect(def, capped)));
                return stats;
            }
            if (def.Kind != ItemKind.Weapon)
            {
                return stats;
            }

            int step = capped - 1;
            WeaponPattern pattern = def.Pattern;
            bool ticks = pattern == WeaponPattern.Aura || pattern == WeaponPattern.Zone;
            Add(stats, "Damage", def.BaseDamage + def.DamagePerLevel * step,
                ticks && def.HitInterval > 0f ? " every " + Number(def.HitInterval) + " s" : string.Empty);
            Add(stats, "Cooldown", def.BaseCooldown + def.CooldownPerLevel * step, " s");
            int count = At(def.CountByLevel, step);
            if (count > 0)
            {
                stats.Add(new Stat(CountLabel(pattern), count.ToString(CultureInfo.InvariantCulture)));
            }
            float reach = def.BaseRange * (1f + def.RangePerLevel * step);
            Add(stats, RangeLabel(pattern), reach > 0f ? reach : def.ProjectileRange, " m");
            if (pattern == WeaponPattern.Sweep && def.ProjectileSpeed > 0f)
            {
                Add(stats, "Wave flies", def.ProjectileRange, " m");
            }
            if (pattern == WeaponPattern.Sweep || pattern == WeaponPattern.Combo || pattern == WeaponPattern.Fan)
            {
                Add(stats, "Arc", def.ArcDegrees, "°");
            }
            if (pattern == WeaponPattern.Strike || pattern == WeaponPattern.Bomb || pattern == WeaponPattern.BombRing || pattern == WeaponPattern.Zone)
            {
                Add(stats, pattern == WeaponPattern.Zone ? "Pool radius" : "Blast radius", def.Width, " m");
            }
            if (def.ProjectileSpeed > 0f && def.Pierce > 0)
            {
                stats.Add(new Stat("Pierces", "+" + def.Pierce.ToString(CultureInfo.InvariantCulture) + (def.Pierce == 1 ? " enemy" : " enemies")));
            }
            int bounces = At(def.BouncesByLevel, step);
            if (bounces > 0)
            {
                stats.Add(new Stat("Bounces", bounces.ToString(CultureInfo.InvariantCulture)));
            }
            if (pattern == WeaponPattern.Orbit || pattern == WeaponPattern.Zone)
            {
                Add(stats, "Lasts", def.Duration, " s");
            }
            if (pattern == WeaponPattern.Orbit || pattern == WeaponPattern.Ring)
            {
                Add(stats, "Hits an enemy again after", def.HitInterval, " s");
            }
            if (def.Chance > 0f)
            {
                stats.Add(new Stat("Chance", Percent(System.Math.Min(1f, def.Chance + def.ChancePerLevel * step))));
            }
            Add(stats, "Stun", pattern == WeaponPattern.Freeze ? def.StunSeconds + def.StunPerLevel * step : def.StunSeconds, " s");
            if (def.BackArcLevel > 0 && capped >= def.BackArcLevel)
            {
                stats.Add(new Stat("Also hits", "behind the hero"));
            }
            return stats;
        }

        private static string PassiveEffect(ItemDef def, int level)
        {
            float total = def.PerLevel * level;
            switch (def.CatalogIndex)
            {
                case SurvivorCatalog.IronHeartIndex: return "+" + Percent(total) + " max HP";
                case SurvivorCatalog.BoneArmorIndex: return "+" + Number(total) + " armor";
                case SurvivorCatalog.MightGauntletIndex: return "+" + Percent(total) + " damage";
                case SurvivorCatalog.CritEyeIndex: return "+" + Percent(total) + " crit chance";
                case SurvivorCatalog.HourglassIndex: return "-" + Percent(total) + " weapon cooldown";
                case SurvivorCatalog.AreaCharmIndex: return "+" + Percent(total) + " area";
                case SurvivorCatalog.WindBootsIndex: return "+" + Percent(total) + " move speed";
                case SurvivorCatalog.MagnetCharmIndex: return "+" + Percent(total) + " pickup range";
                case SurvivorCatalog.RecoveryIndex: return "+" + Number(total) + " HP per second";
                case SurvivorCatalog.CloverIndex: return "+" + Number(total) + " Luck";
                case SurvivorCatalog.GreedIndex: return "+" + Percent(total) + " gold";
                case SurvivorCatalog.CrownIndex: return "+" + Percent(total) + " EXP";
                case SurvivorCatalog.DurationCharmIndex: return "+" + Percent(total) + " effect duration";
                case SurvivorCatalog.DuplicatorIndex: return "+" + Number(total) + (total > 1.5f ? " projectiles" : " projectile") + " per volley";
                case SurvivorCatalog.SpikedArmorIndex:
                    return "+" + Number(total) + " armor, reflects " + Percent(SurvivorCatalog.SpikedReflectPerLevel * level) + " of the damage taken";
                case SurvivorCatalog.OmniBoxIndex: return "+" + Percent(total) + " damage, move speed, duration and area";
                default: return string.Empty;
            }
        }

        private static string CountLabel(WeaponPattern pattern)
        {
            switch (pattern)
            {
                case WeaponPattern.Orbit:
                case WeaponPattern.Ring: return "Orbiting";
                case WeaponPattern.Strike:
                case WeaponPattern.BombRing: return "Blasts";
                case WeaponPattern.Barrier: return "Charges";
                case WeaponPattern.Combo: return "Hits";
                case WeaponPattern.Stone: return "Targets";
                case WeaponPattern.Thrust: return "Lines";
                default: return "Projectiles";
            }
        }

        private static string RangeLabel(WeaponPattern pattern)
        {
            switch (pattern)
            {
                case WeaponPattern.Sweep:
                case WeaponPattern.Combo: return "Reach";
                case WeaponPattern.Thrust: return "Length";
                case WeaponPattern.Orbit:
                case WeaponPattern.Ring: return "Orbit radius";
                case WeaponPattern.BombRing: return "Ring radius";
                case WeaponPattern.Aura:
                case WeaponPattern.Shockwave:
                case WeaponPattern.Retaliate:
                case WeaponPattern.Freeze:
                case WeaponPattern.Purge: return "Radius";
                default: return "Range";
            }
        }

        private static int At(IReadOnlyList<int> values, int step)
        {
            return values == null || values.Count == 0 ? 0 : values[step < values.Count ? step : values.Count - 1];
        }

        private static void Add(List<Stat> stats, string label, float value, string unit)
        {
            if (value > 0.0001f)
            {
                stats.Add(new Stat(label, Number(value) + unit));
            }
        }

        private static string Join(List<Stat> stats, string separator, int limit)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < stats.Count && i < limit; i++)
            {
                if (i > 0)
                {
                    builder.Append(separator);
                }
                builder.Append(stats[i].Label).Append(' ').Append(stats[i].Value);
            }
            return builder.ToString();
        }

        private static void AppendBlock(StringBuilder builder, string block)
        {
            if (string.IsNullOrEmpty(block))
            {
                return;
            }
            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }
            builder.Append(block);
        }

        private static string Number(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Percent(float fraction)
        {
            return (fraction * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }
    }
}
