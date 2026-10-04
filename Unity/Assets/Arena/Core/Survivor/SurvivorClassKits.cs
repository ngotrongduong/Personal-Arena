using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>Class kits: base stats, weapon/passive pools and active skills of each class.</summary>
    public static partial class SurvivorDefaults
    {
        public static SurvivorClassDef Warrior()
        {
            return new SurvivorClassDef
            {
                Id = "warrior", MaxHp = 150f, Regen = 0.2f, Armor = 0f, MoveSpeed = 4.5f,
                Acceleration = 30f, Radius = 0.5f, PickupRadius = 1.5f, CritChance = 0.05f,
                CritDamage = 1.5f, MaxEnergy = 100f, EnergyRegen = 15f, Mass = 1f,
                StartingWeapon = 0, WeaponPool = new[] { 0, 1, 2, 3, 4, 5, 26, 27, 28, 29, 30, 31, 32, 33, 64, 73, 74, 75, 76 }, PassivePool = new[] { 6, 7, 8, 9, 10, 11, 12, 13, 58, 59, 60, 61, 34, 35, 36, 37 },
                ActiveSkills = Skills(
                    new SkillDef { Id = "kick", Kind = SkillKind.Kick, Damage = 10f, Range = 1.8f, ArcDegrees = 100f, StunSeconds = 1.2f, Knockback = 3f, Cooldown = 3f },
                    new SkillDef { Id = "shield-block", Kind = SkillKind.Block, EnergyCost = 10f, EnergyPerSecond = 20f, BlockMoveMultiplier = 0.4f, BlockDamageMultiplier = 0.5f, ParryWindowSeconds = 0.2f, StunSeconds = 1.5f, BlockStaggerSeconds = 0.6f, BlockPushback = 0.8f },
                    new SkillDef { Id = "dash", Kind = SkillKind.Dash, Cooldown = 2.5f, EnergyCost = 15f, DashDistance = 3f, DashSpeed = 18f },
                    new SkillDef { Id = "war-cry", Kind = SkillKind.AreaBurst, Damage = 15f, AreaRadius = 3.5f, StunSeconds = 0.8f, Knockback = 2f, Cooldown = 10f, EnergyCost = 20f },
                    new SkillDef { Id = "leap-slam", Kind = SkillKind.Leap, Damage = 40f, Range = 7f, DashDistance = 5f, DashSpeed = 20f, AreaRadius = 2.8f, StunSeconds = 0.6f, Knockback = 2.5f, Cooldown = 8f, EnergyCost = 30f },
                    new SkillDef { Id = "whirlwind", Kind = SkillKind.Whirlwind, Damage = 12f, AreaRadius = 2.4f, Knockback = 0.5f, Duration = 1.5f, TickSeconds = 0.15f, SlowFactor = 0.7f, Cooldown = 9f, EnergyCost = 30f }
                )
            };
        }

        /// <summary>Fragile caster: less HP, more energy, a ranged weapon kit and four skills.</summary>
        public static SurvivorClassDef Mage()
        {
            return new SurvivorClassDef
            {
                Id = "mage", MaxHp = 110f, Regen = 0.15f, Armor = 0f, MoveSpeed = 4.3f,
                Acceleration = 30f, Radius = 0.45f, PickupRadius = 1.8f, CritChance = 0.05f,
                CritDamage = 1.5f, MaxEnergy = 150f, EnergyRegen = 20f, Mass = 0.9f,
                StartingWeapon = SurvivorCatalog.MagicBoltIndex, WeaponPool = new[] { 14, 15, 16, 17, 18, 19, 64, 66, 67, 68, 26, 31, 33, 69, 70, 72, 73, 74, 75, 76 },
                PassivePool = new[] { 6, 7, 8, 9, 10, 11, 12, 13, 58, 59, 60, 61, 34, 35, 36, 37 },
                ActiveSkills = Skills(
                    new SkillDef { Id = "fireball", Kind = SkillKind.Projectile, Damage = 40f, Range = 12f, ProjectileSpeed = 14f, ProjectileRadius = 0.4f, AreaRadius = 2f, Knockback = 1f, Cooldown = 3f, EnergyCost = 25f },
                    new SkillDef { Id = "mana-shield", Kind = SkillKind.Block, EnergyCost = 15f, EnergyPerSecond = 25f, BlockMoveMultiplier = 0.7f, BlockDamageMultiplier = 0.4f, ParryWindowSeconds = 0.15f, StunSeconds = 1f, BlockStaggerSeconds = 0.4f, BlockPushback = 0.5f, BlockAllDirections = true },
                    new SkillDef { Id = "blink", Kind = SkillKind.Teleport, Cooldown = 4f, EnergyCost = 20f, DashDistance = 4f },
                    new SkillDef { Id = "frost-burst", Kind = SkillKind.AreaBurst, Damage = 15f, AreaRadius = 3f, StunSeconds = 1.5f, Knockback = 1.5f, Cooldown = 8f, EnergyCost = 40f },
                    new SkillDef { Id = "fire-wall", Kind = SkillKind.Wall, Damage = 8f, Range = 3f, Width = 6f, Depth = 1.2f, Duration = 4f, TickSeconds = 0.4f, MaxAlive = 2, Cooldown = 12f, EnergyCost = 35f },
                    new SkillDef { Id = "chain-lightning", Kind = SkillKind.Chain, Damage = 35f, Range = 10f, JumpRange = 5f, Count = 6, Falloff = 0.85f, StunSeconds = 0.3f, Cooldown = 6f, EnergyCost = 30f }
                )
            };
        }

        /// <summary>Fast skirmisher: medium HP, higher crit, long-range weapons, a piercing shot and a back roll.</summary>
        public static SurvivorClassDef Archer()
        {
            return new SurvivorClassDef
            {
                Id = "archer", MaxHp = 130f, Regen = 0.2f, Armor = 0f, MoveSpeed = 5f,
                Acceleration = 34f, Radius = 0.45f, PickupRadius = 1.6f, CritChance = 0.08f,
                CritDamage = 1.6f, MaxEnergy = 100f, EnergyRegen = 15f, Mass = 0.9f,
                StartingWeapon = SurvivorCatalog.ArrowIndex, WeaponPool = new[] { 20, 21, 22, 23, 24, 25, 64, 65, 29, 32, 33, 70, 71, 73, 74, 75, 76 },
                PassivePool = new[] { 6, 7, 8, 9, 10, 11, 12, 13, 58, 59, 60, 61, 34, 35, 36, 37 },
                ActiveSkills = Skills(
                    new SkillDef { Id = "power-shot", Kind = SkillKind.Projectile, Damage = 50f, Range = 14f, ProjectileSpeed = 22f, ProjectileRadius = 0.35f, Knockback = 2.5f, Pierce = true, Cooldown = 3.5f, EnergyCost = 20f },
                    new SkillDef { Id = "roll-back", Kind = SkillKind.Dash, DashBackward = true, Cooldown = 2.5f, EnergyCost = 15f, DashDistance = 3.5f, DashSpeed = 18f },
                    new SkillDef { Id = "kick", Kind = SkillKind.Kick, Damage = 10f, Range = 1.8f, ArcDegrees = 100f, StunSeconds = 1.2f, Knockback = 3f, Cooldown = 3f },
                    new SkillDef { Id = "none", Kind = SkillKind.None },
                    new SkillDef { Id = "caltrop-trap", Kind = SkillKind.Trap, Damage = 6f, AreaRadius = 2f, Duration = 6f, TickSeconds = 0.5f, SlowFactor = 0.6f, SlowSeconds = 0.1f, MaxAlive = 2, Cooldown = 10f, EnergyCost = 20f },
                    new SkillDef { Id = "arrow-barrage", Kind = SkillKind.Barrage, Damage = 18f, Range = 14f, ArcDegrees = 50f, Count = 9, ProjectileSpeed = 16f, ProjectileRadius = 0.25f, Knockback = 0.3f, Pierce = true, Cooldown = 7f, EnergyCost = 25f }
                )
            };
        }

        /// <summary>Class skill list padded with <c>none</c> to <see cref="SurvivorInput.SkillSlotCount"/> slots (slots 4-5 are free for later content).</summary>
        public static SkillDef[] Skills(params SkillDef[] first)
        {
            SkillDef[] all = new SkillDef[SurvivorInput.SkillSlotCount];
            for (int i = 0; i < all.Length; i++) all[i] = i < first.Length ? first[i] : new SkillDef { Id = "none", Kind = SkillKind.None };
            return all;
        }

        /// <summary>Survivor kit for a class id (warrior, mage, archer), or null.</summary>
        public static SurvivorClassDef ForClass(string classId)
        {
            return classId switch
            {
                "warrior" => Warrior(),
                "mage" => Mage(),
                "archer" => Archer(),
                _ => null
            };
        }
    }
}
