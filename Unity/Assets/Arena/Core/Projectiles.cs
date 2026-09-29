namespace PersonalArena.Core
{
    /// <summary>Mutable state of one preallocated projectile pool slot.</summary>
    public sealed class ProjectileState
    {
        public bool Active;
        public int Id;
        public bool FromHero;
        public int SourceZombieId;
        public Vec2 Position;
        public Vec2 Velocity;
        public float Radius;
        public float Damage;
        public float RemainingSeconds;
        public bool Pierce;
        public ulong HitMask;
        public float Knockback;
        public float StunSeconds;
        public float SlowFactor;
        public float SlowSeconds;
        public float AreaRadius;
        public int SkillSlot;
    }
}
