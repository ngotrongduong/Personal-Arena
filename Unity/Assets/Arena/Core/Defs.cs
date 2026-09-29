using System;

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

    /// <summary>How a zombie moves and attacks.</summary>
    public enum ZombieBehavior
    {
        Melee,
        Ranged
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
        public ZombieBehavior Behavior = ZombieBehavior.Melee;
        public float KnockbackResist;
        public float PreferredDistance;
        public float ProjectileSpeed;
        public float ProjectileRadius;
        public float ProjectileRange;
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

        public static ZombieTypeDef Runner()
        {
            return new ZombieTypeDef
            {
                Id = "runner",
                TypeIndex = 1,
                Behavior = ZombieBehavior.Melee,
                MaxHp = 50f,
                MoveSpeed = 4.2f,
                TurnSpeedDegPerSec = 360f,
                Radius = 0.4f,
                AttackDamage = 12f,
                AttackRange = 1.2f,
                AttackArcDegrees = 60f,
                AttackWindupSeconds = 0.3f,
                AttackCooldown = 0.8f
            };
        }

        public static ZombieTypeDef Brute()
        {
            return new ZombieTypeDef
            {
                Id = "brute",
                TypeIndex = 2,
                Behavior = ZombieBehavior.Melee,
                MaxHp = 300f,
                MoveSpeed = 1.6f,
                TurnSpeedDegPerSec = 120f,
                Radius = 0.7f,
                AttackDamage = 45f,
                AttackRange = 1.8f,
                AttackArcDegrees = 90f,
                AttackWindupSeconds = 0.9f,
                AttackCooldown = 1.8f,
                KnockbackResist = 0.6f
            };
        }

        public static ZombieTypeDef Spitter()
        {
            return new ZombieTypeDef
            {
                Id = "spitter",
                TypeIndex = 3,
                Behavior = ZombieBehavior.Ranged,
                MaxHp = 70f,
                MoveSpeed = 2.4f,
                TurnSpeedDegPerSec = 240f,
                Radius = 0.45f,
                AttackDamage = 15f,
                AttackRange = 10f,
                AttackArcDegrees = 20f,
                AttackWindupSeconds = 0.6f,
                AttackCooldown = 2.5f,
                PreferredDistance = 7f,
                ProjectileSpeed = 9f,
                ProjectileRadius = 0.3f,
                ProjectileRange = 12f
            };
        }

        public static ZombieTypeDef[] ZombieTypes() =>
            new[] { Walker(), Runner(), Brute(), Spitter() };

        public static HeroClassDef Mage()
        {
            return new HeroClassDef
            {
                Id = "mage",
                MaxHp = 80f,
                MoveSpeed = 4.3f,
                Acceleration = 30f,
                TurnSpeedDegPerSec = 540f,
                Radius = 0.45f,
                MaxEnergy = 120f,
                EnergyRegenPerSec = 18f,
                Rewards = new RewardConfig(),
                Skills = new[]
                {
                    new SkillDef
                    {
                        Id = "fireball", Kind = SkillKind.Projectile, Damage = 40f,
                        ProjectileSpeed = 14f, ProjectileRadius = 0.35f, Range = 16f,
                        Cooldown = 0.8f, EnergyCost = 12f, AreaRadius = 1.5f
                    },
                    new SkillDef
                    {
                        Id = "frost-nova", Kind = SkillKind.AreaBurst, AreaRadius = 4f,
                        Damage = 10f, SlowFactor = 0.4f, SlowSeconds = 3f,
                        Knockback = 1.5f, Cooldown = 6f, EnergyCost = 30f
                    },
                    new SkillDef
                    {
                        Id = "mana-shield", Kind = SkillKind.Block, EnergyCost = 10f,
                        EnergyPerSecond = 25f, BlockMoveMultiplier = 0.6f,
                        BlockDamageMultiplier = 0.25f, BlockAllDirections = true
                    },
                    new SkillDef
                    {
                        Id = "blink", Kind = SkillKind.Teleport, DashDistance = 6f,
                        Cooldown = 5f, EnergyCost = 25f
                    }
                }
            };
        }

        public static HeroClassDef Archer()
        {
            return new HeroClassDef
            {
                Id = "archer",
                MaxHp = 85f,
                MoveSpeed = 5f,
                Acceleration = 30f,
                TurnSpeedDegPerSec = 600f,
                Radius = 0.42f,
                MaxEnergy = 100f,
                EnergyRegenPerSec = 16f,
                Rewards = new RewardConfig(),
                Skills = new[]
                {
                    new SkillDef
                    {
                        Id = "arrow", Kind = SkillKind.Projectile, Damage = 22f,
                        ProjectileSpeed = 24f, ProjectileRadius = 0.15f, Range = 20f,
                        Cooldown = 0.4f
                    },
                    new SkillDef
                    {
                        Id = "piercing-arrow", Kind = SkillKind.Projectile, Pierce = true,
                        Damage = 45f, ProjectileSpeed = 30f, ProjectileRadius = 0.15f,
                        Range = 22f, Cooldown = 3f, EnergyCost = 20f
                    },
                    new SkillDef
                    {
                        Id = "leap-back", Kind = SkillKind.Dash, DashBackward = true,
                        DashDistance = 4f, DashSpeed = 16f, Cooldown = 3f, EnergyCost = 15f
                    },
                    new SkillDef
                    {
                        Id = "concussive-arrow", Kind = SkillKind.Projectile, Damage = 10f,
                        ProjectileSpeed = 20f, ProjectileRadius = 0.2f, Range = 18f,
                        Knockback = 4f, StunSeconds = 1f, Cooldown = 5f, EnergyCost = 20f
                    }
                }
            };
        }

        public static HeroClassDef HeroClass(string id)
        {
            if (string.Equals(id, "warrior", StringComparison.OrdinalIgnoreCase))
            {
                return Warrior();
            }

            if (string.Equals(id, "mage", StringComparison.OrdinalIgnoreCase))
            {
                return Mage();
            }

            if (string.Equals(id, "archer", StringComparison.OrdinalIgnoreCase))
            {
                return Archer();
            }

            throw new ArgumentException("Unknown hero class id.", nameof(id));
        }
    }
}
