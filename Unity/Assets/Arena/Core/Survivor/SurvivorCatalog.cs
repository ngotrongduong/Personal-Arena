using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>Catalog constants and lookups. The rows and evolution tables live in <c>SurvivorCatalog.Items.cs</c> (static fields stay there, in dependency order).</summary>
    public static partial class SurvivorCatalog
    {
        /// <summary>
        /// Schema v5: 128 slots. Indices never move. Reserved ranges: 64-95 new weapons, 96-111 new passives,
        /// 112-127 evolutions of the new weapons. Free old ids: 38-39; 62/63 stay the bonus fillers.
        /// </summary>
        public const int CatalogSize = 128;
        /// <summary>Gameplay slot caps (M9: 6 + 6). <see cref="SurvivorTuning.MaxWeaponSlots"/> may lower them for old-rule tests.</summary>
        public const int MaxWeapons = 6;
        public const int MaxPassives = 6;

        public const int SweepIndex = 0;
        public const int SpearThrustIndex = 1;
        public const int OrbitAxeIndex = 2;
        public const int ThrownHammerIndex = 3;
        public const int AuraIndex = 4;
        public const int ShockwaveIndex = 5;

        public const int IronHeartIndex = 6;
        public const int BoneArmorIndex = 7;
        public const int MightGauntletIndex = 8;
        public const int CritEyeIndex = 9;
        public const int HourglassIndex = 10;
        public const int AreaCharmIndex = 11;
        public const int WindBootsIndex = 12;
        public const int MagnetCharmIndex = 13;
        // M9 wave 1a: Warrior weapons 26..30 and passives for every class 58..61.
        public const int FlameConeIndex = 26;
        public const int ComboBladeIndex = 27;
        public const int HeavyHammerIndex = 28;
        public const int BombIndex = 29;
        public const int RetaliateIndex = 30;
        public const int RecoveryIndex = 58;
        public const int CloverIndex = 59;
        public const int GreedIndex = 60;
        public const int CrownIndex = 61;
        // M9 wave 1b: Warrior weapons 31..33 and passives for every class 34..37.
        public const int BarrierIndex = 31;
        public const int BoomerangIndex = 32;
        public const int PoisonPoolIndex = 33;
        public const int DurationCharmIndex = 34;
        public const int DuplicatorIndex = 35;
        public const int SpikedArmorIndex = 36;
        public const int OmniBoxIndex = 37;
        // M9 group C: weapons 64..68 (Warrior gets bounce-shot; Archer bounce-shot and momentum-spirit; Mage bounce-shot, time-clock, purge, bomb-ring).
        public const int BounceShotIndex = 64;
        public const int MomentumSpiritIndex = 65;
        public const int TimeClockIndex = 66;
        public const int PurgeIndex = 67;
        public const int BombRingIndex = 68;
        // M9 groups A/B for Mage and Archer: weapons 69..72 (fireball-nova and magi-stone Mage; bracelet-trio Mage and Archer; quad-shot Archer).
        public const int FireballNovaIndex = 69;
        public const int BraceletTrioIndex = 70;
        public const int QuadShotIndex = 71;
        public const int MagiStoneIndex = 72;
        public const int BonusGoldIndex = 62;
        public const int BonusHealIndex = 63;

        public const float MagnetChance = 0.002f;
        public const float MagnetDropAngle = 3.6f;
        public const float ChestDropAngle = 4.8f;
        public const float ChestGoldMin = 50f;
        public const float ChestGoldMax = 150f;

        // Mage weapons (14..19).
        public const int MagicBoltIndex = 14;
        public const int FireOrbIndex = 15;
        public const int FrostNovaIndex = 16;
        public const int HolyFieldIndex = 17;
        public const int LightningIndex = 18;
        public const int ArcaneBeamIndex = 19;
        // Archer weapons (20..25).
        public const int ArrowIndex = 20;
        public const int MultiShotIndex = 21;
        public const int ArrowRainIndex = 22;
        public const int OrbitKnifeIndex = 23;
        public const int DaggerIndex = 24;
        public const int CrossbowIndex = 25;
        /// <summary>Original evolutions occupy 40..57 (see also <see cref="FirstNewEvolutionIndex"/>): evolution of weapon w is <see cref="EvolutionOf"/>(w).</summary>
        public const int FirstEvolutionIndex = 40;
        public const int EvolutionCount = 18;
        /// <summary>M9: evolutions of the new weapons (26-33, 64-66, 68-72) occupy 112..127; purge (67) has none.</summary>
        public const int FirstNewEvolutionIndex = 112;
        public const int NewEvolutionCount = 16;

        /// <summary>Combo blade: strikes per swing and seconds between them (the last strike deals <see cref="ComboFinisherMul"/>×).</summary>
        public const int ComboHits = 3;
        /// <summary>Spiked armor: share of each contact hit taken that is reflected, per level.</summary>
        public const float SpikedReflectPerLevel = 0.1f;
        /// <summary>Most projectiles/axes/strikes a volley may have after the duplicator (buffers are sized for it).</summary>
        public const int MaxVolleyCount = 8;
        public const float ComboHitInterval = 0.25f;
        public const float ComboFinisherMul = 2f;
        /// <summary>Bounce shot: an enemy is hit at most once per this many seconds by one projectile.</summary>
        public const float BounceRehitSeconds = 0.5f;
        /// <summary>Bounce shot: after hitting an enemy it turns toward another enemy within this distance.</summary>
        public const float BounceRicochetRange = 8f;
        /// <summary>Momentum spirit: the hero counts as moving above this speed (m/s); the factor is an EMA with this time constant (s); damage × (Min + Span × factor).</summary>
        public const float MomentumSpeedThreshold = 0.5f;
        public const float MomentumTimeConstant = 1.5f;
        public const float MomentumMinMul = 0.4f;
        public const float MomentumSpanMul = 1.2f;
        /// <summary>Time clock: elites are stunned for this share of the time. Purge: share of max HP elites and the boss lose.</summary>
        public const float FreezeEliteShare = 0.5f;
        public const float PurgeBossFraction = 0.05f;
        /// <summary>Bomb ring: the ring turns this many radians every salvo; at most this many blasts wait at once.</summary>
        public const float BombRingRotationStep = 0.5235988f;
        public const int MaxPendingBlasts = 8;
        /// <summary>Quad shot: angle between the shots of one direction (degrees).</summary>
        public const float QuadStaggerDegrees = 8f;

        public static ItemDef Get(int index)
        {
            if (index >= FirstEvolutionIndex && index < FirstEvolutionIndex + EvolutionCount) return Evolutions[index - FirstEvolutionIndex];
            if (index >= FirstNewEvolutionIndex && index < FirstNewEvolutionIndex + NewEvolutionCount) return NewEvolutions[index - FirstNewEvolutionIndex];
            return index switch
            {
                0 => Sweep,
                1 => SpearThrust,
                2 => OrbitAxe,
                3 => Hammer,
                4 => Aura,
                5 => Shockwave,
                6 => IronHeart,
                7 => BoneArmor,
                8 => MightGauntlet,
                9 => CritEye,
                10 => Hourglass,
                11 => AreaCharm,
                12 => WindBoots,
                13 => MagnetCharm,
                14 => MagicBolt,
                15 => FireOrb,
                16 => FrostNova,
                17 => HolyField,
                18 => Lightning,
                19 => ArcaneBeam,
                20 => Arrow,
                21 => MultiShot,
                22 => ArrowRain,
                23 => OrbitKnife,
                24 => Dagger,
                25 => Crossbow,
                26 => FlameCone,
                27 => ComboBlade,
                28 => HeavyHammer,
                29 => Bomb,
                30 => Retaliate,
                31 => Barrier,
                32 => Boomerang,
                33 => PoisonPool,
                64 => BounceShot,
                65 => MomentumSpirit,
                66 => TimeClock,
                67 => Purge,
                68 => BombRing,
                69 => FireballNova,
                70 => BraceletTrio,
                71 => QuadShot,
                72 => MagiStone,
                34 => DurationCharm,
                35 => Duplicator,
                36 => SpikedArmor,
                37 => OmniBox,
                58 => Recovery,
                59 => Clover,
                60 => Greed,
                61 => Crown,
                62 => BonusGold,
                63 => BonusHeal,
                _ => null
            };
        }

        /// <summary>Catalog index of the evolution of base weapon <paramref name="weapon"/>, or −1.</summary>
        public static int EvolutionOf(int weapon)
        {
            for (int i = 0; i < Evolutions.Length; i++) if (Evolutions[i].EvolvesFrom == weapon) return Evolutions[i].CatalogIndex;
            for (int i = 0; i < NewEvolutions.Length; i++) if (NewEvolutions[i].EvolvesFrom == weapon) return NewEvolutions[i].CatalogIndex;
            return -1;
        }

        /// <summary>
        /// An evolution keeps the base weapon's pattern with its level-5 numbers improved: damage ×1.5,
        /// range ×1.2, cooldown ×0.8, one more projectile, +1 pierce, longer orbit, faster hits.
        /// It has a single level, so the level-scaling fields are folded in and zeroed.
        /// </summary>
        /// <remarks>
        /// Pattern-specific folding (all other numbers use the generic rule): <paramref name="rangeMul"/> replaces the ×1.2 range (retaliate radius ×1.5),
        /// <paramref name="widthMul"/>/<paramref name="durationMul"/> replace the ×1.2 width and ×1.25 duration (poison pool ×1.3 both),
        /// <paramref name="extraCount"/> replaces the +1 volley count (bomb ring +2), <paramref name="extraBounces"/> adds to the level-5 bounces,
        /// <paramref name="stunBonus"/>/<paramref name="chanceBonus"/> add to the level-5 stun and chance of a Freeze weapon,
        /// <paramref name="momentumFloor"/> sets the lowest movement factor of a Momentum weapon.
        /// </remarks>
        private static ItemDef Evolve(ItemDef b, int index, string id, string name, int passive, float rangeMul = 1.2f, float widthMul = 1.2f,
            float durationMul = 1.25f, int extraCount = 1, int extraBounces = 0, float stunBonus = 0f, float chanceBonus = 0f, float momentumFloor = 0f)
        {
            int top = b.MaxLevel - 1;
            int topCount = b.CountByLevel.Count == 0 ? 0 : b.CountByLevel[Math.Min(top, b.CountByLevel.Count - 1)];
            int topBounces = b.BouncesByLevel.Count == 0 ? 0 : b.BouncesByLevel[Math.Min(top, b.BouncesByLevel.Count - 1)];
            return new ItemDef
            {
                CatalogIndex = index, Id = id, Name = name, Kind = ItemKind.Weapon, Pattern = b.Pattern, MaxLevel = 1,
                BaseDamage = (b.BaseDamage + b.DamagePerLevel * top) * 1.5f,
                BaseRange = b.BaseRange * (1f + b.RangePerLevel * top) * rangeMul,
                BaseCooldown = (b.BaseCooldown + b.CooldownPerLevel * top) * 0.8f,
                Knockback = b.Knockback * 1.2f,
                ArcDegrees = b.ArcDegrees, BackArcLevel = b.BackArcLevel > 0 ? 1 : 0,
                ProjectileSpeed = b.ProjectileSpeed, ProjectileRadius = b.ProjectileRadius * 1.2f,
                ProjectileRange = b.ProjectileRange * 1.2f, Pierce = b.ProjectileSpeed > 0f ? b.Pierce + 1 : b.Pierce,
                CountByLevel = topCount == 0 ? Array.Empty<int>() : Array.AsReadOnly(new[] { topCount + extraCount }),
                Duration = b.Duration * durationMul, HitInterval = b.HitInterval * 0.8f,
                AngularSpeedDegrees = b.AngularSpeedDegrees, Width = b.Width * widthMul, Clustered = b.Clustered,
                StunSeconds = b.Pattern == WeaponPattern.Freeze ? b.StunSeconds + b.StunPerLevel * top + stunBonus : b.StunSeconds * 1.5f,
                BouncesByLevel = topBounces == 0 ? Array.Empty<int>() : Array.AsReadOnly(new[] { topBounces + extraBounces }),
                Chance = b.Chance > 0f ? MathF.Min(1f, b.Chance + b.ChancePerLevel * top + chanceBonus) : 0f, MomentumFloor = momentumFloor,
                EvolvesFrom = b.CatalogIndex, EvolutionPassive = passive
            };
        }

        /// <summary>
        /// Effect per level of a passive row (reserved rows included, so the derived-stat formulas
        /// already cover them): iron-heart +10% max HP, bone-armor +1 armor, might-gauntlet +8% damage,
        /// crit-eye +4% crit chance, hourglass −6% cooldown (returned positive), area-charm +8% area,
        /// wind-boots +8% move speed, magnet-charm +25% pickup radius; M9: recovery +0.2 HP/s, clover +5 Luck
        /// (one Luck stat point), greed +10% gold, crown +8% EXP; M9 1b: duration-charm +10% duration, duplicator +1 projectile,
        /// spiked-armor +1 armor (the reflect share is <see cref="SpikedReflectPerLevel"/>), omni-box +4% damage, move speed, duration and area. 0 for other rows.
        /// </summary>
        public static float PassivePerLevel(int index)
        {
            return index switch
            {
                IronHeartIndex => 0.1f,
                BoneArmorIndex => 1f,
                MightGauntletIndex => 0.08f,
                CritEyeIndex => 0.04f,
                HourglassIndex => 0.06f,
                AreaCharmIndex => 0.08f,
                WindBootsIndex => 0.08f,
                MagnetCharmIndex => 0.25f,
                RecoveryIndex => 0.2f,
                CloverIndex => 5f,
                GreedIndex => 0.1f,
                CrownIndex => 0.08f,
                DurationCharmIndex => 0.1f,
                DuplicatorIndex => 1f,
                SpikedArmorIndex => 1f,
                OmniBoxIndex => 0.04f,
                _ => 0f
            };
        }

        private static ItemDef Passive(int index, string id, string name, int maxLevel = 5) =>
            new ItemDef { CatalogIndex = index, Id = id, Name = name, Kind = ItemKind.Passive, PerLevel = PassivePerLevel(index), MaxLevel = maxLevel };

        private static ItemDef Filler(int index, string id, string name) =>
            new ItemDef { CatalogIndex = index, Id = id, Name = name, Kind = ItemKind.Filler, MaxLevel = 0 };

        /// <summary>Angular offset of spear k in an n-spear fan, in radians.</summary>
        public static float ThrustAngleOffset(int k, int n) => ThrustAngleOffset(k, n, true);

        /// <summary>
        /// Angle of volley member <paramref name="k"/> of <paramref name="n"/>, 20 degrees apart. Odd volleys are symmetric around
        /// the aim (-20, 0, +20). Even volleys keep the first member on the aim and alternate sides (0, +20, -20, +40) when
        /// <paramref name="centered"/>; the old rule (pre-2026-10-03, <see cref="SurvivorTuning.CenteredEvenVolleys"/> off) spread
        /// them symmetrically (-10, +10), so no member flew at the target and anything behind it was missed.
        /// </summary>
        public static float ThrustAngleOffset(int k, int n, bool centered)
        {
            if (!centered || n % 2 == 1) return (k - (n - 1) * 0.5f) * 20f * MathF.PI / 180f;
            int step = (k + 1) / 2;
            return (k % 2 == 1 ? step : -step) * 20f * MathF.PI / 180f;
        }
    }
}
