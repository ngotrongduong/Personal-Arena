using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>Where run gold came from: normal-kill drops, elite drops, the boss, chests, level-up fillers.</summary>
    public enum GoldSource { Normal, Elite, Boss, Chest, Filler }

    public sealed class SurvivorHero
    {
        public Vec2 Position { get; internal set; }
        public Vec2 Velocity { get; internal set; }
        public float Facing { get; internal set; }
        public float Hp { get; internal set; }
        public float MaxHp { get; internal set; }
        public float Energy { get; internal set; }
        public float MaxEnergy { get; internal set; }
        public float Radius { get; internal set; }
        public float[] SkillCooldowns { get; } = new float[SurvivorInput.SkillSlotCount];
        public bool Blocking { get; internal set; }
        public bool Dashing { get; internal set; }
        public bool Alive { get; internal set; }
        public float StunRemaining { get; internal set; }
        internal float BlockStarted;
        internal float DashRemaining;
        internal Vec2 DashDirection;
        /// <summary>The block skill being held and the dash skill in progress (their slot's def).</summary>
        internal SkillDef BlockSkill;
        internal SkillDef DashSkill;
    }

    public sealed class SurvivorDerivedStats
    {
        public float MaxHp { get; internal set; }
        public float Armor { get; internal set; }
        public float Regen { get; internal set; }
        public float Might { get; internal set; }
        public float CritChance { get; internal set; }
        public float CritDamage { get; internal set; }
        public float CooldownMul { get; internal set; }
        public float AreaMul { get; internal set; }
        public float MoveSpeed { get; internal set; }
        public float PickupRadius { get; internal set; }
        public float Luck { get; internal set; }
        public float GreedMul { get; internal set; }
        public float GrowthMul { get; internal set; }
        public float TierGold { get; internal set; }
        /// <summary>Multiplier on volley durations (orbit, poison zone); 1 + duration-charm + omni-box.</summary>
        public float DurationMul { get; internal set; }
        /// <summary>Extra projectiles per volley from the duplicator (0 without it).</summary>
        public int Amount { get; internal set; }
        /// <summary>Share of each contact hit taken that is reflected to touching enemies (spiked armor).</summary>
        public float ReflectFraction { get; internal set; }
    }

    public sealed class SurvivorEnemy
    {
        public bool Active { get; internal set; }
        public int Id { get; internal set; }
        public int TypeIndex { get; internal set; }
        public Vec2 Position { get; internal set; }
        public Vec2 Velocity { get; internal set; }
        public float Facing { get; internal set; }
        public float Hp { get; internal set; }
        public float MaxHp { get; internal set; }
        public float Damage { get; internal set; }
        public float Radius { get; internal set; }
        public float Mass { get; internal set; }
        public float KnockbackResist { get; internal set; }
        public bool Elite { get; internal set; }
        public bool IsBoss { get; internal set; }
        public bool WindingUp => WindupRemaining > 0f;
        public float WindupRemaining { get; internal set; }
        public float StunRemaining { get; internal set; }
        internal float AttackCooldown;
        internal float ContactCooldown;
        internal float SummonCooldown;
        internal float OrbitNextHitTime;
        internal int LastShockwaveId;
        /// <summary>Run time of the last hit taken (spawn time until first hit); drives the tier-8 regen.</summary>
        internal float LastHitTime;
        internal bool RelocatedThisTick;
        internal bool Separated;
    }

    public sealed class SurvivorProjectile
    {
        public bool Active { get; internal set; }
        public int Id { get; internal set; }
        public Vec2 Position { get; internal set; }
        public Vec2 Velocity { get; internal set; }
        public float Radius { get; internal set; }
        public float Damage { get; internal set; }
        public float Knockback { get; internal set; }
        public float Lifetime { get; internal set; }
        public int PierceRemaining { get; internal set; }
        /// <summary>Catalog index of the weapon that fired it, or −1 − slot for an active skill.</summary>
        public int SourceIndex { get; internal set; }
        /// <summary>When above 0 the first hit blows up: every enemy within this radius takes the damage.</summary>
        public float ExplodeRadius { get; internal set; }
        internal float StunSeconds;
        internal readonly int[] HitIds = new int[4];
        /// <summary>A weapon bomb also blows up when its range runs out (a skill fireball just vanishes).</summary>
        internal bool ExplodeOnExpire;
        internal int HitCount;
    }

    /// <summary>A flying boomerang: out toward its target, then back to the hero. Each enemy is hit at most once per direction.</summary>
    public sealed class SurvivorBoomerang
    {
        public const int HitCapacity = 48;
        public bool Active { get; internal set; }
        public Vec2 Position { get; internal set; }
        public Vec2 Direction { get; internal set; }
        public float Radius { get; internal set; }
        public float Damage { get; internal set; }
        /// <summary>True once it turned around and flies back to the hero.</summary>
        public bool Returning { get; internal set; }
        public int SourceIndex { get; internal set; }
        internal float Travelled;
        internal float MaxRange;
        internal float Age;
        internal int OutCount;
        internal int BackCount;
        internal readonly int[] OutIds = new int[HitCapacity];
        internal readonly int[] BackIds = new int[HitCapacity];
    }

    /// <summary>A poison pool on the ground: hurts every enemy inside every tick interval until it runs out.</summary>
    public sealed class SurvivorZone
    {
        public bool Active { get; internal set; }
        public Vec2 Position { get; internal set; }
        public float Radius { get; internal set; }
        public float Remaining { get; internal set; }
        public float Duration { get; internal set; }
        public float Damage { get; internal set; }
        public int SourceIndex { get; internal set; }
        internal float TickTimer;
    }

    public sealed class SurvivorEnemyProjectile
    {
        public bool Active { get; internal set; }
        public int Id { get; internal set; }
        public Vec2 Position { get; internal set; }
        public Vec2 Velocity { get; internal set; }
        public float Radius { get; internal set; }
        public float Damage { get; internal set; }
        public float Lifetime { get; internal set; }
        public int SourceId { get; internal set; }
        internal bool JustSpawned;
    }

    public sealed class SurvivorPickup
    {
        public bool Active { get; internal set; }
        public PickupKind Kind { get; internal set; }
        public Vec2 Position { get; internal set; }
        public float Value { get; internal set; }
        public bool Attracted { get; internal set; }
        /// <summary>Where a gold pickup came from (Normal or Elite); ignored for other kinds.</summary>
        public GoldSource Source { get; internal set; }
        public float Radius => Kind == PickupKind.Gem ? GemRadius(Value) : 0.3f;
        public static int GemSize(float value) => value < 10f ? 0 : value < 100f ? 1 : 2;
        public static float GemRadius(float value) => GemSize(value) == 0 ? 0.18f : GemSize(value) == 1 ? 0.25f : 0.35f;
    }

    public sealed class SurvivorObstacle
    {
        public bool Active { get; internal set; }
        public Vec2 Position { get; internal set; }
        public float Radius { get; internal set; }
    }

    public sealed class SurvivorInventory
    {
        private readonly int[] levels = new int[SurvivorCatalog.CatalogSize];
        // Storage covers test/debug grants beyond the gameplay slot caps; offers enforce the tuning slot caps (default 6 + 6).
        private readonly int[] weapons = new int[SurvivorCatalog.CatalogSize];
        private readonly int[] passives = new int[SurvivorCatalog.CatalogSize];
        public int WeaponCount { get; internal set; }
        public int PassiveCount { get; internal set; }
        public System.Collections.Generic.IReadOnlyList<int> ItemLevels => levels;
        public System.Collections.Generic.IReadOnlyList<int> Weapons => weapons;
        public System.Collections.Generic.IReadOnlyList<int> Passives => passives;
        public int Level(int catalogIndex) => catalogIndex >= 0 && catalogIndex < levels.Length ? levels[catalogIndex] : 0;
        public int WeaponAt(int index) => weapons[index];
        public int PassiveAt(int index) => passives[index];
        internal int[] Levels => levels;
        internal void Clear() { System.Array.Clear(levels, 0, levels.Length); WeaponCount = 0; PassiveCount = 0; }
        internal void Set(int index, int level)
        {
            if (levels[index] == 0 && level > 0)
            {
                ItemDef def = SurvivorCatalog.Get(index);
                if (def.Kind == ItemKind.Weapon) weapons[WeaponCount++] = index;
                else if (def.Kind == ItemKind.Passive) passives[PassiveCount++] = index;
            }
            levels[index] = level;
        }

        /// <summary>Swaps an owned weapon for <paramref name="replacement"/> at level 1, keeping its slot.</summary>
        internal void Replace(int owned, int replacement)
        {
            for (int i = 0; i < WeaponCount; i++)
            {
                if (weapons[i] != owned) continue;
                weapons[i] = replacement; levels[owned] = 0; levels[replacement] = 1;
                return;
            }
        }
    }
}
