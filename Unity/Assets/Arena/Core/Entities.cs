namespace PersonalArena.Core
{
    /// <summary>Current phase of a zombie attack.</summary>
    public enum ZombieAttackPhase
    {
        Idle,
        Windup,
        Recover
    }

    /// <summary>Mutable state of the single hero.</summary>
    public sealed class HeroState
    {
        public Vec2 Position;
        public float Facing;
        public Vec2 Velocity;
        public float Hp;
        public float Energy;
        public readonly float[] CooldownRemaining = new float[4];
        public bool IsBlocking;
        public float BlockStartTime;
        public float StunRemaining;
        public float SlowFactor = 1f;
        public float SlowRemaining;
        public float DashRemaining;
        public Vec2 DashDirection;
        public float DashSpeed;
        public int BlockSkillSlot = -1;
        public bool Alive;
        public bool FellOff;
        public int Id;
    }

    /// <summary>Mutable state of one zombie.</summary>
    public sealed class ZombieState
    {
        public Vec2 Position;
        public float Facing;
        public Vec2 Velocity;
        public Vec2 KnockbackVelocity;
        public float Hp;
        public float Energy;
        public bool IsBlocking;
        public float BlockStartTime;
        public float StunRemaining;
        public float SlowFactor = 1f;
        public float SlowRemaining;
        public ZombieAttackPhase AttackPhase;
        public float AttackTimer;
        public bool Alive;
        public bool FellOff;
        public int Id;
        public ZombieTypeDef Def;
        public float RespawnRemaining;
    }

    /// <summary>A health potion dropped by a slain zombie.</summary>
    public sealed class PotionState
    {
        public int Id;
        public Vec2 Position;
        public float Remaining;
        public bool Active;
    }
}
