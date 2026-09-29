using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public enum SurvivorEventType
    {
        DamageDealt, BossDamaged, Crit, EnemyKilled, EliteKilled, BossKilled,
        HeroDamaged, HeroDied, Blocked, Parry, SkillUsed, SkillFailed,
        XpCollected, GoldCollected, Healed, LevelUp, OfferShown, ItemPicked,
        WeaponFired, EnemySpawned, EliteSpawned, BossSpawned, RunWon,
        RunTimeUp, RunExpired, MagnetPicked, ChestOpened
    }

    public readonly struct SurvivorEvent
    {
        public readonly SurvivorEventType Type;
        public readonly float Value;
        public readonly float Extra;
        public readonly int Id;
        public readonly Vec2 Point;
        public SurvivorEvent(SurvivorEventType type, float value = 0f, float extra = 0f, int id = -1, Vec2 point = default)
        { Type = type; Value = value; Extra = extra; Id = id; Point = point; }
    }

    public enum EndReason { None, Died, Won, TimeUp, Expired }
    public enum DeathCause { None, Surrounded, Boss, Brute, Contact, Projectile }
    public enum PickupKind { None, Gem, Gold, Meat, Chest, Magnet }
}
