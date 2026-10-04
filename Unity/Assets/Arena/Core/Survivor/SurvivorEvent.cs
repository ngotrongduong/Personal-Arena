using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public enum SurvivorEventType
    {
        DamageDealt, BossDamaged, Crit, EnemyKilled, EliteKilled, BossKilled,
        HeroDamaged, HeroDied, Blocked, Parry, SkillUsed, SkillFailed,
        XpCollected, GoldCollected, Healed, LevelUp, OfferShown, ItemPicked,
        WeaponFired, EnemySpawned, EliteSpawned, BossSpawned, RunWon,
        RunTimeUp, RunExpired, MagnetPicked, ChestOpened,
        /// <summary>Id = evolution catalog index, Extra = replaced weapon index.</summary>
        WeaponEvolved,
        /// <summary>An exploder blew up at Point; Value = blast radius.</summary>
        EnemyExploded,
        /// <summary>A summoner raised walkers at Point; Value = how many.</summary>
        EnemySummoned,
        /// <summary>A Strike weapon or skill blast hit the ground at Point; Value = radius, Id = source index.</summary>
        StrikeLanded,
        /// <summary>A power-up started a timed buff: Id = <see cref="BuffKind"/>, Value = seconds.</summary>
        BuffStarted,
        /// <summary>A bomb pickup went off around the hero at Point; Value = blast radius.</summary>
        BombExploded,
        /// <summary>A mana potion restored Value energy.</summary>
        ManaRestored,
        /// <summary>M11: a charger starts its dash (Id = enemy, Point = start).</summary>
        EnemyCharged,
        /// <summary>M11: a splitter died and left small copies (Value = count).</summary>
        EnemySplit,
        /// <summary>M11: a shaman healed the enemies around it (Value = radius).</summary>
        EnemyHealed,
        GoldenSpawned,
        /// <summary>M11: a golden enemy died (Value = gold dropped).</summary>
        GoldenKilled
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
    public enum DeathCause { None, Surrounded, Boss, Brute, Contact, Projectile, Explosion }
    public enum PickupKind { None, Gem, Gold, Meat, Chest, Magnet, Mana, Bomb, Rage, Shield, Haste }
}
