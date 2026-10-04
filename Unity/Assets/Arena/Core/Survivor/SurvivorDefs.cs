using System;
using System.Collections.Generic;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public enum ItemKind { None, Weapon, Passive, Filler }
    /// <summary>
    /// How a weapon attacks. Strike: hits random enemies in range with a small instant blast at each.
    /// Fan: a spread of projectiles toward the nearest enemy. A class owns at most one Orbit, one Aura
    /// and one Shockwave weapon (the sim keeps one state for each); likewise at most one Combo and one Retaliate.
    /// Combo: a sweep that strikes three times in a row. Bomb: a thrown projectile that explodes on its first hit
    /// or at max range. Retaliate: no attack of its own; it blasts a ring around the hero when the hero is hit.
    /// Barrier: no attack; a shield that absorbs hits and returns after a recharge (at most one per hero).
    /// Boomerang: flies out and back, hitting each enemy once per direction. Zone: drops a damage pool on the ground.
    /// Bounce: a projectile that bounces off walls and obstacles. Momentum: a volley along the hero's movement, stronger the more the hero moves.
    /// Freeze: a chance to stun every non-boss enemy nearby. Purge: kills every normal enemy nearby. BombRing: a salvo of blasts placed on a rotating ring.
    /// Trio: a very tight fan at ONE random enemy in range. Quad: projectiles in four fixed directions around the hero's body. Stone: instant fixed-damage hits (no Might, no crit) on the nearest enemies.
    /// </summary>
    public enum WeaponPattern { None, Sweep, Thrust, Orbit, Thrown, Aura, Shockwave, Strike, Fan, Combo, Bomb, Retaliate, Barrier, Boomerang, Zone, Bounce, Momentum, Freeze, Purge, BombRing, Trio, Quad, Stone }
    public enum StatId
    {
        MaxHp, Armor, Regen, Might, Crit, CritDamage, Cooldown, Area,
        MoveSpeed, Magnet, Luck, Greed, Growth, Duration, Amount, Reserved15
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
        /// <summary>Duration of a persistent weapon volley (seconds).</summary>
        public float Duration { get; init; }
        /// <summary>Minimum interval between repeated hits or aura ticks (seconds).</summary>
        public float HitInterval { get; init; }
        /// <summary>Counter-clockwise angular speed (degrees per second).</summary>
        public float AngularSpeedDegrees { get; init; }
        /// <summary>Cooldown change for each level above one (seconds).</summary>
        public float CooldownPerLevel { get; init; }
        /// <summary>Half-width of an instant capsule weapon, or a Strike's blast radius, before the area multiplier.</summary>
        public float Width { get; init; }
        /// <summary>Strike only: each blast lands on the densest group of enemies in range instead of a random enemy (arrow rain).</summary>
        public bool Clustered { get; init; }
        /// <summary>Stun applied to each enemy hit by a Shockwave or Strike weapon (0 = none).</summary>
        public float StunSeconds { get; init; }
        /// <summary>Evolution: the weapon it replaces (−1 for a normal item).</summary>
        public int EvolvesFrom { get; init; } = -1;
        /// <summary>Evolution: the passive the hero must own (any level) next to the max-level base weapon.</summary>
        public int EvolutionPassive { get; init; } = -1;
        /// <summary>Passive: effect per level (see <see cref="SurvivorCatalog.PassivePerLevel"/>).</summary>
        public float PerLevel { get; init; }
        /// <summary>Bounce: wall/obstacle bounces per projectile, indexed by level − 1.</summary>
        public IReadOnlyList<int> BouncesByLevel { get; init; } = Array.Empty<int>();
        /// <summary>Freeze: trigger chance at level 1 and its growth per level above 1.</summary>
        public float Chance { get; init; }
        public float ChancePerLevel { get; init; }
        /// <summary>Freeze: stun growth per level above 1 (seconds).</summary>
        public float StunPerLevel { get; init; }
        /// <summary>Momentum: the movement factor never reads below this (an evolution keeps some damage while idle).</summary>
        public float MomentumFloor { get; init; }
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

    /// <summary>Explode: winds up next to the hero, then blows up (area damage) and dies without drops.</summary>
    public enum SurvivorAttackKind { Contact, Melee, Ranged, Explode }

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
        public float PreferredDistance { get; init; }
        public float ProjectileSpeed { get; init; }
        public float ProjectileRadius { get; init; }
        public float ProjectileRange { get; init; }
        /// <summary>Explode: blast radius around the enemy.</summary>
        public float ExplodeRadius { get; init; }
        /// <summary>Moves through obstacles (still kept inside the map).</summary>
        public bool IgnoresObstacles { get; init; }
        /// <summary>Seconds between walker summons (0 = never; the boss uses the tuning instead).</summary>
        public float SummonInterval { get; init; }
        public int SummonCount { get; init; }
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
        public SkillDef[] ActiveSkills = SurvivorDefaults.Skills();
    }
}
