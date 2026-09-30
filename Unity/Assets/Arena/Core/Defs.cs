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
        Teleport
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
    }
}
