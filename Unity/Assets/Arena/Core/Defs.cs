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
    }

    /// <summary>Data-only hero class definition with exactly four skill slots.</summary>
    public sealed class HeroClassDef
    {
        public string Id = string.Empty;
        public float MaxHp;
        public float MoveSpeed;
        public float Acceleration = 30f;
        public float TurnSpeedDegPerSec;
        public float Radius;
        public float MaxEnergy;
        public float EnergyRegenPerSec;
        public SkillDef[] Skills = new SkillDef[4];
        public RewardConfig Rewards = new RewardConfig();
    }

    /// <summary>Data-only zombie type definition.</summary>
    public sealed class ZombieTypeDef
    {
        public string Id = string.Empty;
        public int TypeIndex;
        public float MaxHp;
        public float MoveSpeed;
        public float TurnSpeedDegPerSec;
        public float Radius;
        public float AttackDamage;
        public float AttackRange;
        public float AttackArcDegrees;
        public float AttackWindupSeconds;
        public float AttackCooldown;
    }

    /// <summary>Fresh default definitions for M1 combatants.</summary>
    public static class DefaultDefs
    {
        public static HeroClassDef Warrior()
        {
            return new HeroClassDef
            {
                Id = "warrior",
                MaxHp = 100f,
                MoveSpeed = 4.5f,
                Acceleration = 30f,
                TurnSpeedDegPerSec = 540f,
                Radius = 0.45f,
                MaxEnergy = 100f,
                EnergyRegenPerSec = 15f,
                Rewards = new RewardConfig(),
                Skills = new[]
                {
                    new SkillDef
                    {
                        Id = "spear-strike", Kind = SkillKind.MeleeStrike, Damage = 34f,
                        Range = 1.6f, ArcDegrees = 70f, Cooldown = 0.45f
                    },
                    new SkillDef
                    {
                        Id = "kick", Kind = SkillKind.Kick, Damage = 5f, Range = 1.3f,
                        ArcDegrees = 80f, StunSeconds = 1.2f, Knockback = 3f, Cooldown = 3f
                    },
                    new SkillDef
                    {
                        Id = "shield-block", Kind = SkillKind.Block, EnergyCost = 10f,
                        EnergyPerSecond = 20f, BlockMoveMultiplier = 0.4f,
                        BlockDamageMultiplier = 0.5f, ParryWindowSeconds = 0.2f,
                        StunSeconds = 1.5f, BlockStaggerSeconds = 0.6f, BlockPushback = 0.8f
                    },
                    new SkillDef
                    {
                        Id = "dash", Kind = SkillKind.Dash, Cooldown = 2.5f,
                        EnergyCost = 15f, DashDistance = 3f, DashSpeed = 18f
                    }
                }
            };
        }

        public static ZombieTypeDef Walker()
        {
            return new ZombieTypeDef
            {
                Id = "walker",
                TypeIndex = 0,
                MaxHp = 100f,
                MoveSpeed = 2.2f,
                TurnSpeedDegPerSec = 200f,
                Radius = 0.45f,
                AttackDamage = 20f,
                AttackRange = 1.3f,
                AttackArcDegrees = 60f,
                AttackWindupSeconds = 0.5f,
                AttackCooldown = 1.2f
            };
        }
    }
}
