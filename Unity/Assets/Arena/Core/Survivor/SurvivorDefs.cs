using System;
using System.Collections.Generic;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public enum ItemKind { None, Weapon, Passive, Filler }
    public enum WeaponPattern { None, Sweep, Thrust, Orbit, Thrown, Aura, Shockwave }
    public enum StatId
    {
        MaxHp, Armor, Regen, Might, Crit, CritDamage, Cooldown, Area,
        MoveSpeed, Magnet, Luck, Greed, Growth, Reserved13, Reserved14, Reserved15
    }

    public sealed class ItemDef
    {
        public int CatalogIndex { get; init; }
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public ItemKind Kind { get; init; }
        public WeaponPattern Pattern { get; init; }
        public int MaxLevel { get; init; } = 5;
        public float BaseDamage { get; init; }
        public float DamagePerLevel { get; init; }
        /// <summary>Sweep: arc radius at level 1. Thrown: how far a target may be (m).</summary>
        public float BaseRange { get; init; }
        public float BaseCooldown { get; init; }
        public float Knockback { get; init; }
        /// <summary>Sweep range growth per level above 1 (fraction of <see cref="BaseRange"/>).</summary>
        public float RangePerLevel { get; init; }
        /// <summary>Total arc width of an arc weapon (degrees).</summary>
        public float ArcDegrees { get; init; }
        /// <summary>Level from which the sweep also hits behind the hero (0 = never).</summary>
        public int BackArcLevel { get; init; }
        public float ProjectileSpeed { get; init; }
        /// <summary>Projectile radius before the hero's area multiplier.</summary>
        public float ProjectileRadius { get; init; }
        /// <summary>Distance a projectile flies before it expires (lifetime = range / speed).</summary>
        public float ProjectileRange { get; init; }
        /// <summary>Extra enemies a projectile may hit after the first.</summary>
        public int Pierce { get; init; }
        /// <summary>Projectiles per volley, indexed by level − 1.</summary>
        public IReadOnlyList<int> CountByLevel { get; init; } = Array.Empty<int>();
        /// <summary>Passive: effect per level (see <see cref="SurvivorCatalog.PassivePerLevel"/>).</summary>
        public float PerLevel { get; init; }
    }

    public static class SurvivorCatalog
    {
        public const int CatalogSize = 64;
        public const int MaxWeapons = 4;
        public const int MaxPassives = 4;

        public const int IronHeartIndex = 6;
        public const int BoneArmorIndex = 7;
        public const int MightGauntletIndex = 8;
        public const int CritEyeIndex = 9;
        public const int HourglassIndex = 10;
        public const int AreaCharmIndex = 11;
        public const int WindBootsIndex = 12;
        public const int MagnetCharmIndex = 13;
        public const int BonusGoldIndex = 62;
        public const int BonusHealIndex = 63;

        private static readonly ItemDef Sweep = new ItemDef
        {
            CatalogIndex = 0, Id = "sword-sweep", Name = "Kiếm quét", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Sweep,
            BaseDamage = 20f, DamagePerLevel = 8f, BaseRange = 2.5f, BaseCooldown = 1.2f, Knockback = 0.5f,
            RangePerLevel = 0.1f, ArcDegrees = 120f, BackArcLevel = 5
        };
        private static readonly ItemDef Hammer = new ItemDef
        {
            CatalogIndex = 3, Id = "thrown-hammer", Name = "Búa ném", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Thrown,
            BaseDamage = 25f, DamagePerLevel = 7f, BaseRange = 10f, BaseCooldown = 1.5f, Knockback = 0.5f,
            ProjectileSpeed = 12f, ProjectileRadius = 0.4f, ProjectileRange = 12f, Pierce = 1,
            CountByLevel = Array.AsReadOnly(new[] { 1, 2, 2, 3, 3 })
        };
        private static readonly ItemDef IronHeart = Passive(IronHeartIndex, "iron-heart", "Tim sắt");
        private static readonly ItemDef BoneArmor = Passive(BoneArmorIndex, "bone-armor", "Giáp xương");
        private static readonly ItemDef CritEye = Passive(CritEyeIndex, "crit-eye", "Mắt chí mạng");
        private static readonly ItemDef WindBoots = Passive(WindBootsIndex, "wind-boots", "Ủng gió");
        private static readonly ItemDef BonusGold = Filler(62, "bonus-gold", "+25 vàng");
        private static readonly ItemDef BonusHeal = Filler(63, "bonus-heal", "Hồi 30 máu");

        public static ItemDef Get(int index)
        {
            return index switch
            {
                0 => Sweep,
                3 => Hammer,
                6 => IronHeart,
                7 => BoneArmor,
                9 => CritEye,
                12 => WindBoots,
                62 => BonusGold,
                63 => BonusHeal,
                _ => null
            };
        }

        /// <summary>
        /// Effect per level of a passive row (reserved rows included, so the derived-stat formulas
        /// already cover them): iron-heart +10% max HP, bone-armor +1 armor, might-gauntlet +8% damage,
        /// crit-eye +4% crit chance, hourglass −6% cooldown (returned positive), area-charm +8% area,
        /// wind-boots +8% move speed, magnet-charm +25% pickup radius. 0 for other rows.
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
                _ => 0f
            };
        }

        private static ItemDef Passive(int index, string id, string name) =>
            new ItemDef { CatalogIndex = index, Id = id, Name = name, Kind = ItemKind.Passive, PerLevel = PassivePerLevel(index) };

        private static ItemDef Filler(int index, string id, string name) =>
            new ItemDef { CatalogIndex = index, Id = id, Name = name, Kind = ItemKind.Filler, MaxLevel = 0 };
    }

    public static class StatInfo
    {
        public const int UsedCount = 13;
        public const int SlotCount = 16;

        public static float PerPoint(StatId stat)
        {
            return stat switch
            {
                StatId.MaxHp => 0.05f, StatId.Armor => 0.5f, StatId.Regen => 0.1f,
                StatId.Might => 0.03f, StatId.Crit => 0.02f, StatId.CritDamage => 0.1f,
                StatId.Cooldown => -0.02f, StatId.Area => 0.03f, StatId.MoveSpeed => 0.02f,
                StatId.Magnet => 0.1f, StatId.Luck => 5f, StatId.Greed => 0.05f,
                StatId.Growth => 0.03f, _ => 0f
            };
        }

        public static int Cap(StatId stat)
        {
            return stat switch
            {
                StatId.MaxHp or StatId.Might or StatId.Crit or StatId.CritDamage or StatId.Greed => 20,
                StatId.Cooldown or StatId.Area => 15,
                StatId.Armor or StatId.Regen or StatId.MoveSpeed or StatId.Magnet or StatId.Luck or StatId.Growth => 10,
                _ => 0
            };
        }
    }

    public enum SurvivorAttackKind { Contact, Melee }

    public sealed class SurvivorEnemyDef
    {
        public int TypeIndex { get; init; }
        public string Id { get; init; } = string.Empty;
        public float BaseHp { get; init; }
        public float MoveSpeed { get; init; }
        public float Radius { get; init; }
        public float Mass { get; init; }
        public SurvivorAttackKind AttackKind { get; init; }
        public float AttackDamage { get; init; }
        public float AttackRange { get; init; }
        public float AttackArcDegrees { get; init; }
        public float WindupSeconds { get; init; }
        public float RecoverSeconds { get; init; }
        public int Xp { get; init; }
        public float KnockbackResist { get; init; }
        public float TurnSpeedDegPerSec { get; init; }
        public bool IsBoss { get; init; }
    }

    public sealed class SpawnPhase
    {
        public float From { get; init; }
        public float To { get; init; }
        public IReadOnlyList<int> Weights { get; init; }
        public int MaxAlive { get; init; }
        public float SpawnsPerSecond { get; init; }
    }

    public sealed class SurvivorClassDef
    {
        public string Id = string.Empty;
        public float MaxHp;
        public float Regen;
        public float Armor;
        public float MoveSpeed;
        public float Acceleration;
        public float Radius;
        public float PickupRadius;
        public float CritChance;
        public float CritDamage;
        public float MaxEnergy;
        public float EnergyRegen;
        public float Mass;
        public int StartingWeapon;
        public int[] WeaponPool = Array.Empty<int>();
        public int[] PassivePool = Array.Empty<int>();
        public SkillDef[] ActiveSkills = new SkillDef[4];
    }

    public static class SurvivorDefaults
    {
        private static readonly SurvivorEnemyDef[] EnemyDefs =
        {
            Enemy(0, "walker", 15f, 2f, 0.45f, 1f, SurvivorAttackKind.Contact, 8f, 0f, 0f, 0f, 0.5f, 1, 0f, 360f),
            Enemy(1, "runner", 10f, 4f, 0.4f, 1f, SurvivorAttackKind.Contact, 5f, 0f, 0f, 0f, 0.5f, 1, 0f, 360f),
            Enemy(2, "brute", 80f, 1.6f, 0.7f, 3f, SurvivorAttackKind.Melee, 25f, 1.8f, 90f, 0.9f, 1.8f, 5, 0.6f, 180f),
            null,
            Enemy(4, "bone-lord", 6000f, 1.4f, 1.75f, 1000f, SurvivorAttackKind.Melee, 40f, 3.5f, 180f, 1.2f, 4.8f, 0, 1f, 180f, true),
            null, null, null
        };

        private static readonly SpawnPhase[] Phases =
        {
            Phase(0, 60, 1, 0, 0, 0, 30, 1), Phase(60, 180, 3, 1, 0, 0, 60, 2),
            Phase(180, 300, 3, 2, 1, 0, 90, 3), Phase(300, 420, 2, 2, 1, 0, 120, 4),
            Phase(420, 600, 2, 3, 1, 0, 160, 5), Phase(600, 720, 1, 3, 2, 0, 200, 6),
            Phase(720, 900, 1, 3, 3, 0, 250, 8)
        };

        public static SurvivorClassDef Warrior()
        {
            return new SurvivorClassDef
            {
                Id = "warrior", MaxHp = 150f, Regen = 0.2f, Armor = 0f, MoveSpeed = 4.5f,
                Acceleration = 30f, Radius = 0.5f, PickupRadius = 1.5f, CritChance = 0.05f,
                CritDamage = 1.5f, MaxEnergy = 100f, EnergyRegen = 15f, Mass = 1f,
                StartingWeapon = 0, WeaponPool = new[] { 0, 3 }, PassivePool = new[] { 6, 7, 9, 12 },
                ActiveSkills = new[]
                {
                    new SkillDef { Id = "kick", Kind = SkillKind.Kick, Damage = 10f, Range = 1.8f, ArcDegrees = 100f, StunSeconds = 1.2f, Knockback = 3f, Cooldown = 3f },
                    new SkillDef { Id = "shield-block", Kind = SkillKind.Block, EnergyCost = 10f, EnergyPerSecond = 20f, BlockMoveMultiplier = 0.4f, BlockDamageMultiplier = 0.5f, ParryWindowSeconds = 0.2f, StunSeconds = 1.5f, BlockStaggerSeconds = 0.6f, BlockPushback = 0.8f },
                    new SkillDef { Id = "dash", Kind = SkillKind.Dash, Cooldown = 2.5f, EnergyCost = 15f, DashDistance = 3f, DashSpeed = 18f },
                    new SkillDef { Id = "none", Kind = SkillKind.None }
                }
            };
        }

        public static SurvivorEnemyDef EnemyDef(int typeIndex) =>
            typeIndex >= 0 && typeIndex < EnemyDefs.Length ? EnemyDefs[typeIndex] : null;

        public static SpawnPhase PhaseAt(float seconds)
        {
            for (int i = 0; i < Phases.Length; i++)
            {
                if (seconds >= Phases[i].From && seconds < Phases[i].To) return Phases[i];
            }
            return null;
        }

        public static int PhaseCount => Phases.Length;
        public static SpawnPhase GetPhase(int index) => Phases[index];

        private static SurvivorEnemyDef Enemy(int type, string id, float hp, float speed, float radius,
            float mass, SurvivorAttackKind attack, float damage, float range, float arc, float windup,
            float recover, int xp, float resist, float turn, bool boss = false)
        {
            return new SurvivorEnemyDef { TypeIndex = type, Id = id, BaseHp = hp, MoveSpeed = speed,
                Radius = radius, Mass = mass, AttackKind = attack, AttackDamage = damage,
                AttackRange = range, AttackArcDegrees = arc, WindupSeconds = windup,
                RecoverSeconds = recover, Xp = xp, KnockbackResist = resist,
                TurnSpeedDegPerSec = turn, IsBoss = boss };
        }

        private static SpawnPhase Phase(float from, float to, int w, int r, int b, int s, int max, float rate) =>
            new SpawnPhase { From = from, To = to, Weights = Array.AsReadOnly(new[] { w, r, b, s }), MaxAlive = max, SpawnsPerSecond = rate };
    }
}
