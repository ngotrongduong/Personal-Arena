namespace PersonalArena.Core
{
    /// <summary>Observable result emitted by a simulation step.</summary>
    public enum SimEventType
    {
        HeroDealtDamage,
        ZombieKilled,
        Backstab,
        Kick,
        Parry,
        BlockedHit,
        HeroDamaged,
        HeroDamagedFromBehind,
        HeroDied,
        SkillUsed,
        SkillFailedCooldown,
        SkillFailedEnergy,
        EpisodeTimeout,
        HeroFell,
        ZombieFell,
        Stagger,
        PotionDropped,
        PotionPicked,
        ProjectileFired,
        ProjectileHit,
        ProjectileBlocked,
        HeroTeleported
    }

    /// <summary>Allocation-free event payload produced by the simulation.</summary>
    public readonly struct SimEvent
    {
        public readonly SimEventType Type;
        public readonly float Value;
        public readonly int ZombieId;

        public SimEvent(SimEventType type, float value = 0f, int zombieId = -1)
        {
            Type = type;
            Value = value;
            ZombieId = zombieId;
        }
    }
}
