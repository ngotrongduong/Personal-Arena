namespace PersonalArena.Core
{
    /// <summary>Kind of action performed by a skill slot.</summary>
    public enum SkillKind
    {
        None,
        MeleeStrike,
        Kick,
        Block,
        Dash,
        Projectile,
        AreaBurst,
        Teleport,
        Leap,
        Whirlwind,
        Trap,
        Barrage,
        Wall,
        Chain
    }

    /// <summary>Data-only skill definition.</summary>
    public sealed class SkillDef
    {
        public string Id = string.Empty;
        public SkillKind Kind;
        public float Cooldown;
        public float EnergyCost;
        public float EnergyPerSecond;
        public float Damage;
        public float Range;
        public float ArcDegrees;
        public float StunSeconds;
        public float Knockback;
        public float WindupSeconds;
        public float DashDistance;
        public float DashSpeed;
        public float ProjectileSpeed;
        public float ProjectileRadius;
        public float AreaRadius;
        public float SlowFactor;
        public float SlowSeconds;
        public float BlockMoveMultiplier = 1f;
        public float BlockDamageMultiplier = 1f;
        public float ParryWindowSeconds;
        public float BlockStaggerSeconds;
        public float BlockPushback;
        public bool Pierce;
        public bool DashBackward;
        public bool BlockAllDirections;
        // M9 skills 5-6 (unused fields stay 0).
        /// <summary>Arrows (Barrage) or targets (Chain).</summary>
        public int Count;
        /// <summary>Most traps or walls alive at once; a new one replaces the oldest.</summary>
        public int MaxAlive;
        /// <summary>Seconds a spin, trap or wall lasts.</summary>
        public float Duration;
        /// <summary>Seconds between damage ticks of a spin, trap or wall.</summary>
        public float TickSeconds;
        /// <summary>Wall width (perpendicular to the facing) and depth (along it, m).</summary>
        public float Width;
        public float Depth;
        /// <summary>Chain: longest jump between two targets (m).</summary>
        public float JumpRange;
        /// <summary>Chain: damage multiplier per jump.</summary>
        public float Falloff;
    }
}
