using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Pure presentation rules of the survivor viewer (no scene objects), kept here so they can be unit tested:
    /// clock text, stable obstacle model choice, item descriptions and end-of-run texts.
    /// </summary>
    public static class SurvivorViewLogic
    {
        /// <summary>HUD chip of a running pickup buff: its name and the whole seconds left (rounded up); null once it ran out.</summary>
        public static string BuffChipText(BuffKind kind, float remaining)
        {
            if (float.IsNaN(remaining) || remaining <= 0f)
            {
                return null;
            }
            string name = kind == BuffKind.Rage ? "RAGE" : kind == BuffKind.Shield ? "SHIELD" : "HASTE";
            return name + "  " + Mathf.CeilToInt(remaining) + "s";
        }

        /// <summary>Full length in seconds of a pickup buff (for the HUD timer bar).</summary>
        public static float BuffSeconds(BuffKind kind)
        {
            return kind == BuffKind.Rage ? SurvivorSim.RageSeconds : kind == BuffKind.Shield ? SurvivorSim.ShieldSeconds : SurvivorSim.HasteSeconds;
        }

        /// <summary>Formats seconds as "mm:ss" (whole seconds, rounded down); invalid or negative values show "00:00".</summary>
        public static string FormatClock(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
            {
                return "00:00";
            }

            int whole = Mathf.FloorToInt(seconds);
            int minutes = whole / 60;
            int rest = whole % 60;
            return minutes.ToString("00") + ":" + rest.ToString("00");
        }

        /// <summary>Stable integer hash (same result on every run and platform) for model and yaw choices.</summary>
        public static uint Hash(int value, int salt = 0)
        {
            unchecked
            {
                uint x = (uint)value * 0x9E3779B1u ^ (uint)salt * 0x85EBCA77u;
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>Model index in [0, count) for an obstacle; the same obstacle always gets the same model. -1 when there are no models.</summary>
        public static int ObstacleModelIndex(int obstacleIndex, int count, int salt = 0)
        {
            if (count <= 0)
            {
                return -1;
            }

            return (int)(Hash(obstacleIndex, 7919 + salt) % (uint)count);
        }

        /// <summary>Grave, tree or rock for an obstacle: small ones are mostly graves, big ones mostly trees.</summary>
        public static ObstacleKind ObstacleKindFor(int obstacleIndex, float radius)
        {
            int roll = (int)(Hash(obstacleIndex, 104729) % 100u);
            if (radius < 0.8f)
            {
                return roll < 60 ? ObstacleKind.Grave : ObstacleKind.Rock;
            }
            if (radius < 1.2f)
            {
                return roll < 40 ? ObstacleKind.Grave : roll < 80 ? ObstacleKind.Tree : ObstacleKind.Rock;
            }
            return roll < 60 ? ObstacleKind.Tree : ObstacleKind.Rock;
        }

        /// <summary>Stable yaw in degrees for an obstacle model.</summary>
        public static float ObstacleYaw(int obstacleIndex)
        {
            return Hash(obstacleIndex, 15485863) % 360u;
        }

        /// <summary>Reach of the sword sweep at a level, matching the simulation's formula.</summary>
        public static float SweepRange(int level, float areaMultiplier)
        {
            int clamped = Mathf.Max(1, level);
            ItemDef sweep = SurvivorCatalog.Get(0);
            float baseRange = sweep != null ? sweep.BaseRange : 2.5f;
            float perLevel = sweep != null ? sweep.RangePerLevel : 0.1f;
            return baseRange * (1f + perLevel * (clamped - 1)) * (areaMultiplier > 0f ? areaMultiplier : 1f);
        }

        /// <summary>True when the sword sweep also hits behind the hero at this level.</summary>
        public static bool SweepHitsBehind(int level)
        {
            ItemDef sweep = SurvivorCatalog.Get(0);
            int from = sweep != null ? sweep.BackArcLevel : 5;
            return from > 0 && level >= from;
        }

        /// <summary>Reach of any sweep weapon (sword, dagger and their evolutions) at a level, matching the simulation.</summary>
        public static float SweepRange(int catalogIndex, int level, float areaMultiplier)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def == null)
            {
                return SweepRange(level, areaMultiplier);
            }
            int clamped = Mathf.Max(1, level);
            return def.BaseRange * (1f + def.RangePerLevel * (clamped - 1)) * (areaMultiplier > 0f ? areaMultiplier : 1f);
        }

        /// <summary>True when a sweep weapon also hits behind the hero at this level (evolutions from level 1).</summary>
        public static bool SweepHitsBehind(int catalogIndex, int level)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            return def != null && def.BackArcLevel > 0 && level >= def.BackArcLevel;
        }

        /// <summary>Length of a thrust weapon's streak (spear, arcane beam, crossbow, evolutions), matching the simulation.</summary>
        public static float ThrustLength(int catalogIndex, float areaMultiplier)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            float range = def != null && def.BaseRange > 0f ? def.BaseRange : 5f;
            return range * (areaMultiplier > 0f ? areaMultiplier : 1f);
        }

        /// <summary>Level-up card text: what a new item does, or what reaching <paramref name="nextLevel"/> changes.</summary>
        public static string ItemDescription(int catalogIndex, int nextLevel)
        {
            return SurvivorItemDetails.CardText(catalogIndex, nextLevel);
        }

        /// <summary>Kind line of a tooltip or level-up card: weapon, evolved weapon, passive or one-off reward.</summary>
        public static string ItemKindLabel(ItemDef def)
        {
            if (def == null)
            {
                return string.Empty;
            }
            if (def.EvolvesFrom >= 0)
            {
                return "EVOLVED WEAPON";
            }
            return def.Kind == ItemKind.Weapon ? "WEAPON" : def.Kind == ItemKind.Passive ? "PASSIVE" : "REWARD";
        }

        /// <summary>Card level line: "MỚI" for a new item, "Lv N" for an upgrade, empty for one-off rewards.</summary>
        public static string LevelLabel(int catalogIndex, int nextLevel)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def != null && def.Kind == ItemKind.Filler)
            {
                return string.Empty;
            }
            return nextLevel <= 1 ? "NEW" : "Lv " + nextLevel;
        }

        /// <summary>Theme color of a survivor item, used for icon badges and card accents.</summary>
        public static Color ItemColor(int catalogIndex)
        {
            if (IsEvolution(catalogIndex))
            {
                return Color.Lerp(ItemColor(BaseWeapon(catalogIndex)), EvolutionGold, 0.45f);
            }
            switch (catalogIndex)
            {
                case 0: return new Color(0.78f, 0.86f, 1f);
                case 1: return new Color(0.55f, 0.8f, 1f);
                case 2: return new Color(0.95f, 0.5f, 0.35f);
                case 3: return new Color(1f, 0.66f, 0.3f);
                case 4: return new Color(1f, 0.92f, 0.45f);
                case 5: return new Color(0.6f, 0.65f, 1f);
                case 6: return new Color(1f, 0.36f, 0.4f);
                case 7: return new Color(0.84f, 0.8f, 0.68f);
                case 8: return new Color(1f, 0.5f, 0.25f);
                case 9: return new Color(1f, 0.85f, 0.3f);
                case 10: return new Color(0.75f, 0.6f, 1f);
                case 11: return new Color(0.45f, 0.85f, 1f);
                case 12: return new Color(0.4f, 0.95f, 0.75f);
                case 13: return new Color(1f, 0.45f, 0.6f);
                // Mage: blue, fire, ice, holy gold, electric, arcane purple.
                case 14: return new Color(0.45f, 0.62f, 1f);
                case 15: return new Color(1f, 0.52f, 0.2f);
                case 16: return new Color(0.62f, 0.92f, 1f);
                case 17: return new Color(1f, 0.9f, 0.52f);
                case 18: return new Color(0.72f, 0.82f, 1f);
                case 19: return new Color(0.78f, 0.45f, 1f);
                // Archer: greens and woods.
                case 20: return new Color(0.55f, 0.86f, 0.4f);
                case 21: return new Color(0.42f, 0.75f, 0.36f);
                case 22: return new Color(0.68f, 0.92f, 0.5f);
                case 23: return new Color(0.72f, 0.82f, 0.66f);
                case 24: return new Color(0.82f, 0.62f, 0.4f);
                case 25: return new Color(0.7f, 0.5f, 0.3f);
                case 26: return new Color(1f, 0.38f, 0.12f);
                case 27: return new Color(1f, 0.72f, 0.28f);
                case 28: return new Color(0.76f, 0.8f, 0.9f);
                case 29: return new Color(1f, 0.3f, 0.2f);
                case 30: return new Color(1f, 0.62f, 0.28f);
                case 31: return new Color(0.45f, 0.82f, 1f);
                case 32: return new Color(0.42f, 0.95f, 0.82f);
                case 33: return new Color(0.48f, 0.9f, 0.28f);
                case 34: return new Color(0.7f, 0.6f, 1f);
                case 35: return new Color(0.48f, 0.86f, 1f);
                case 36: return new Color(0.9f, 0.7f, 0.5f);
                case 37: return new Color(0.85f, 0.82f, 1f);
                case 58: return new Color(0.45f, 1f, 0.58f);
                case 59: return new Color(0.55f, 0.95f, 0.42f);
                case 60: return new Color(1f, 0.78f, 0.25f);
                case 61: return new Color(1f, 0.72f, 0.3f);
                case 62: return new Color(1f, 0.82f, 0.3f);
                case 63: return new Color(0.45f, 1f, 0.5f);
                case 64: return new Color(0.45f, 0.9f, 1f);
                case 65: return new Color(0.42f, 1f, 0.7f);
                case 66: return new Color(0.65f, 0.92f, 1f);
                case 67: return new Color(1f, 0.96f, 0.72f);
                case 68: return new Color(1f, 0.42f, 0.2f);
                case 69: return new Color(1f, 0.3f, 0.12f);
                case 70: return new Color(0.78f, 0.48f, 1f);
                case 71: return new Color(0.55f, 0.95f, 0.42f);
                case 72: return new Color(0.78f, 0.7f, 1f);
                case SurvivorCatalog.SpiritOrbsIndex:
                case SurvivorCatalog.SawRingIndex:
                case SurvivorCatalog.FrostHaloIndex:
                case SurvivorCatalog.CometIndex:
                    return SurvivorRenderer.RingColor(catalogIndex);
                default: return new Color(0.8f, 0.82f, 0.9f);
            }
        }

        /// <summary>End-of-run title.</summary>
        public static string EndTitle(EndReason reason)
        {
            switch (reason)
            {
                case EndReason.Won: return "VICTORY!";
                case EndReason.TimeUp: return "TIME UP";
                case EndReason.Expired: return "BOSS STILL ALIVE - TIME UP";
                case EndReason.Died: return "DEFEATED";
                default: return string.Empty;
            }
        }

        /// <summary>Cause of death for the end screen.</summary>
        public static string DeathCauseText(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.Surrounded: return "surrounded";
                case DeathCause.Boss: return "killed by the Boss";
                case DeathCause.Brute: return "hit by a Brute";
                case DeathCause.Contact: return "clawed by enemies";
                case DeathCause.Projectile: return "hit by a projectile";
                case DeathCause.Explosion: return "blown up by an Exploder";
                default: return "unknown";
            }
        }

        /// <summary>Title color: gold for a win, red for a death, pale blue otherwise.</summary>
        public static Color EndColor(EndReason reason)
        {
            switch (reason)
            {
                case EndReason.Won: return new Color(1f, 0.86f, 0.4f);
                case EndReason.Died: return new Color(1f, 0.4f, 0.35f);
                default: return new Color(0.7f, 0.85f, 1f);
            }
        }

        // ------------------------------------------------------------------ M7: class weapons and evolutions

        /// <summary>Gold used for evolution frames, tints and the evolution toast.</summary>
        public static readonly Color EvolutionGold = new Color(1f, 0.82f, 0.3f);

        /// <summary>How much larger an evolved weapon is drawn than its base weapon.</summary>
        public const float EvolutionScale = 1.3f;

        /// <summary>True for an evolution row (40..57) of the catalog.</summary>
        public static bool IsEvolution(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            return def != null && def.EvolvesFrom >= 0;
        }

        /// <summary>The base weapon of an evolution, or the index itself for a normal item.</summary>
        public static int BaseWeapon(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            return def != null && def.EvolvesFrom >= 0 ? def.EvolvesFrom : catalogIndex;
        }

        /// <summary>Display name of an item ("Kiếm bão"); empty for an unknown index.</summary>
        public static string ItemName(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            return def != null ? def.Name : string.Empty;
        }

        /// <summary>HUD toast when a weapon evolves: "TIẾN HÓA: Kiếm bão".</summary>
        public static string EvolvedToast(int catalogIndex)
        {
            string name = ItemName(catalogIndex);
            return string.IsNullOrEmpty(name) ? "EVOLVED!" : "EVOLVED: " + name;
        }

        /// <summary>Visual of a weapon; an evolution draws like its base weapon.</summary>
        public static WeaponVisual WeaponVisualOf(int catalogIndex)
        {
            switch (BaseWeapon(catalogIndex))
            {
                case 0: return WeaponVisual.Sweep;
                case 1: return WeaponVisual.Spear;
                case 2: return WeaponVisual.OrbitAxe;
                case 3: return WeaponVisual.Hammer;
                case 4: return WeaponVisual.Aura;
                case 5: return WeaponVisual.Shockwave;
                case SurvivorCatalog.MagicBoltIndex: return WeaponVisual.MagicBolt;
                case SurvivorCatalog.FireOrbIndex: return WeaponVisual.FireOrb;
                case SurvivorCatalog.FrostNovaIndex: return WeaponVisual.FrostNova;
                case SurvivorCatalog.HolyFieldIndex: return WeaponVisual.HolyField;
                case SurvivorCatalog.LightningIndex: return WeaponVisual.Lightning;
                case SurvivorCatalog.ArcaneBeamIndex: return WeaponVisual.ArcaneBeam;
                case SurvivorCatalog.ArrowIndex: return WeaponVisual.Arrow;
                case SurvivorCatalog.MultiShotIndex: return WeaponVisual.MultiShot;
                case SurvivorCatalog.ArrowRainIndex: return WeaponVisual.ArrowRain;
                case SurvivorCatalog.OrbitKnifeIndex: return WeaponVisual.OrbitKnife;
                case SurvivorCatalog.DaggerIndex: return WeaponVisual.Dagger;
                case SurvivorCatalog.CrossbowIndex: return WeaponVisual.Crossbow;
                case SurvivorCatalog.FlameConeIndex: return WeaponVisual.FlameCone;
                case SurvivorCatalog.ComboBladeIndex: return WeaponVisual.Combo;
                case SurvivorCatalog.HeavyHammerIndex: return WeaponVisual.Hammer;
                case SurvivorCatalog.BombIndex: return WeaponVisual.Bomb;
                case SurvivorCatalog.RetaliateIndex: return WeaponVisual.Retaliate;
                case SurvivorCatalog.BarrierIndex: return WeaponVisual.Barrier;
                case SurvivorCatalog.BoomerangIndex: return WeaponVisual.Boomerang;
                case SurvivorCatalog.PoisonPoolIndex: return WeaponVisual.Zone;
                case SurvivorCatalog.BounceShotIndex: return WeaponVisual.Bounce;
                case SurvivorCatalog.MomentumSpiritIndex: return WeaponVisual.Momentum;
                case SurvivorCatalog.TimeClockIndex: return WeaponVisual.Freeze;
                case SurvivorCatalog.PurgeIndex: return WeaponVisual.Purge;
                case SurvivorCatalog.BombRingIndex: return WeaponVisual.BombRing;
                case SurvivorCatalog.FireballNovaIndex: return WeaponVisual.FireballNova;
                case SurvivorCatalog.BraceletTrioIndex: return WeaponVisual.Trio;
                case SurvivorCatalog.QuadShotIndex: return WeaponVisual.Quad;
                case SurvivorCatalog.MagiStoneIndex: return WeaponVisual.Stone;
                default: return WeaponVisual.None;
            }
        }

        /// <summary>
        /// Model of a hero projectile from its source: a weapon catalog index, or −1 − slot for a skill projectile
        /// (the skill id of that slot in <paramref name="classDef"/> picks fireball or power shot).
        /// </summary>
        public static ProjectileLook ProjectileLookOf(int sourceIndex, SurvivorClassDef classDef)
        {
            if (sourceIndex < 0)
            {
                int slot = -1 - sourceIndex;
                SkillDef[] skills = classDef != null ? classDef.ActiveSkills : null;
                string id = skills != null && slot < skills.Length && skills[slot] != null ? skills[slot].Id : null;
                switch (id)
                {
                    case "fireball": return ProjectileLook.Fireball;
                    case "power-shot": return ProjectileLook.PowerShot;
                    default: return ProjectileLook.MagicBolt;
                }
            }

            switch (WeaponVisualOf(sourceIndex))
            {
                case WeaponVisual.MagicBolt: return ProjectileLook.MagicBolt;
                case WeaponVisual.Arrow:
                case WeaponVisual.MultiShot:
                case WeaponVisual.Trio:
                case WeaponVisual.Quad: return ProjectileLook.Arrow;
                case WeaponVisual.Bomb: return ProjectileLook.Bomb;
                case WeaponVisual.Bounce: return ProjectileLook.Bounce;
                case WeaponVisual.Momentum: return ProjectileLook.Momentum;
                case WeaponVisual.Sweep: return ProjectileLook.SwordWave;
                default: return ProjectileLook.Hammer;
            }
        }

        /// <summary>Art walker model of an enemy type: exploder = minion, ghost = rogue, necromancer = skeleton mage.</summary>
        public static int EnemyWalkerIndex(int typeIndex)
        {
            switch (typeIndex)
            {
                case 1: return 1;
                case 2: return 2;
                case 3: return 3;
                case SurvivorDefaults.ExploderTypeIndex: return 0;
                case SurvivorDefaults.GhostTypeIndex: return 1;
                case SurvivorDefaults.NecromancerTypeIndex: return 3;
                default: return 0;
            }
        }

        /// <summary>True when the class's block covers every direction (the mage's mana shield bubble).</summary>
        public static bool BlockIsBubble(SurvivorClassDef classDef)
        {
            if (classDef == null || classDef.ActiveSkills == null)
            {
                return false;
            }
            foreach (SkillDef skill in classDef.ActiveSkills)
            {
                if (skill != null && skill.Kind == SkillKind.Block && skill.BlockAllDirections)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Skill button title; unknown ids show the id.</summary>
        public static string SkillTitle(SkillDef skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }
            switch (skill.Id)
            {
                case "kick": return "Kick";
                case "shield-block": return "Shield Block";
                case "dash": return "Dash";
                case "fireball": return "Fireball";
                case "mana-shield": return "Magic Shield";
                case "blink": return "Blink";
                case "frost-burst": return "Frost Nova";
                case "power-shot": return "Power Shot";
                case "roll-back": return "Backflip";
                case "war-cry": return "War Cry";
                case "leap-slam": return "Leap Slam";
                case "whirlwind": return "Whirlwind";
                case "caltrop-trap": return "Spike Trap";
                case "arrow-barrage": return "Arrow Barrage";
                case "fire-wall": return "Fire Wall";
                case "chain-lightning": return "Chain Lightning";
                default: return skill.Id;
            }
        }

        /// <summary>One-line description of a skill; empty for unknown ids.</summary>
        public static string SkillDescription(string skillId)
        {
            switch (skillId)
            {
                case "kick": return "Kicks enemies in front away and stuns them";
                case "shield-block": return "Raises the shield to block hits from the front; a well-timed block counters";
                case "dash": return "Dashes quickly in the move direction";
                case "fireball": return "Throws a fireball that explodes on several enemies";
                case "mana-shield": return "A magic shield blocks hits from every side";
                case "blink": return "Teleports a short distance instantly";
                case "frost-burst": return "A frost blast around the hero stuns enemies";
                case "power-shot": return "Fires a strong arrow that pierces several enemies";
                case "roll-back": return "Rolls backward to keep distance";
                case "war-cry": return "A shout knocks back and stuns enemies around the hero";
                case "leap-slam": return "Leaps at enemies and slams the ground, stunning them";
                case "whirlwind": return "Spins the sword, damaging everything around continuously";
                case "caltrop-trap": return "Places a spike trap that damages and slows enemies";
                case "arrow-barrage": return "Fires a volley of arrows that pierces enemies ahead";
                case "fire-wall": return "Raises a wall of fire that burns enemies passing through";
                case "chain-lightning": return "Casts lightning that jumps between enemies";
                default: return string.Empty;
            }
        }

        /// <summary>True when a skill slot is shown on the HUD (a real skill, not the empty "none" slot).</summary>
        public static bool SkillVisible(SkillDef skill) => skill != null && skill.Kind != SkillKind.None;

        /// <summary>Distance between two skill slots in the bottom HUD bar.</summary>
        public const float SkillSlotSpacing = 112f;

        /// <summary>Width of the skill bar holding <paramref name="shown"/> slots.</summary>
        public static float SkillPanelWidth(int shown) => Mathf.Max(1, shown) * SkillSlotSpacing + 16f;

        /// <summary>Centre x of the <paramref name="column"/>-th of <paramref name="shown"/> centred skill slots.</summary>
        public static float SkillSlotX(int column, int shown) => (column - (Mathf.Max(1, shown) - 1) * 0.5f) * SkillSlotSpacing;

        /// <summary>Name line of the vitals panel, e.g. "PHÁP SƯ  (AI điều khiển)"; unknown classes read as the Warrior.</summary>
        public static string HeroNameLine(string classId) => ClassViewLogic.UpperName(classId) + "  (AI controlled)";
    }
}
