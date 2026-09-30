using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
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
        public float[] SkillCooldowns { get; } = new float[4];
        public bool Blocking { get; internal set; }
        public bool Dashing { get; internal set; }
        public bool Alive { get; internal set; }
        public float StunRemaining { get; internal set; }
        internal float BlockStarted;
        internal float DashRemaining;
        internal Vec2 DashDirection;
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
        internal readonly int[] HitIds = new int[3];
        internal int HitCount;
    }

    public sealed class SurvivorPickup
    {
        public bool Active { get; internal set; }
        public PickupKind Kind { get; internal set; }
        public Vec2 Position { get; internal set; }
        public float Value { get; internal set; }
        public bool Attracted { get; internal set; }
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
        private readonly int[] weapons = new int[SurvivorCatalog.MaxWeapons];
        private readonly int[] passives = new int[SurvivorCatalog.MaxPassives];
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
    }
}
