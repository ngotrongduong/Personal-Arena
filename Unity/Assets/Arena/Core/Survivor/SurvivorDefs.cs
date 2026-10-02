using System;
using System.Collections.Generic;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public enum ItemKind { None, Weapon, Passive, Filler }
    /// <summary>
    /// How a weapon attacks. Strike: hits random enemies in range with a small instant blast at each.
    /// Fan: a spread of projectiles toward the nearest enemy. A class owns at most one Orbit, one Aura
    /// and one Shockwave weapon (the sim keeps one state for each); likewise at most one Combo and one Retaliate.
    /// Combo: a sweep that strikes three times in a row. Bomb: a thrown projectile that explodes on its first hit
    /// or at max range. Retaliate: no attack of its own; it blasts a ring around the hero when the hero is hit.
    /// Barrier: no attack; a shield that absorbs hits and returns after a recharge (at most one per hero).
    /// Boomerang: flies out and back, hitting each enemy once per direction. Zone: drops a damage pool on the ground.
    /// Bounce: a projectile that bounces off walls and obstacles. Momentum: a volley along the hero's movement, stronger the more the hero moves.
    /// Freeze: a chance to stun every non-boss enemy nearby. Purge: kills every normal enemy nearby. BombRing: a salvo of blasts placed on a rotating ring.
    /// </summary>
    public enum WeaponPattern { None, Sweep, Thrust, Orbit, Thrown, Aura, Shockwave, Strike, Fan, Combo, Bomb, Retaliate, Barrier, Boomerang, Zone, Bounce, Momentum, Freeze, Purge, BombRing }
    public enum StatId
    {
        MaxHp, Armor, Regen, Might, Crit, CritDamage, Cooldown, Area,
        MoveSpeed, Magnet, Luck, Greed, Growth, Duration, Amount, Reserved15
    }

    public sealed class ItemDef
    {
        public int CatalogIndex { get; init; }
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public ItemKind Kind { get; init; }
        public WeaponPattern Pattern { get; init; }
        public int MaxLevel { get; init; } = 5;
        public float BaseDamage { get; init; }
        public float DamagePerLevel { get; init; }
        /// <summary>Sweep: arc radius at level 1. Thrown: how far a target may be (m).</summary>
        public float BaseRange { get; init; }
        public float BaseCooldown { get; init; }
        public float Knockback { get; init; }
        /// <summary>Sweep range growth per level above 1 (fraction of <see cref="BaseRange"/>).</summary>
        public float RangePerLevel { get; init; }
        /// <summary>Total arc width of an arc weapon (degrees).</summary>
        public float ArcDegrees { get; init; }
        /// <summary>Level from which the sweep also hits behind the hero (0 = never).</summary>
        public int BackArcLevel { get; init; }
        public float ProjectileSpeed { get; init; }
        /// <summary>Projectile radius before the hero's area multiplier.</summary>
        public float ProjectileRadius { get; init; }
        /// <summary>Distance a projectile flies before it expires (lifetime = range / speed).</summary>
        public float ProjectileRange { get; init; }
        /// <summary>Extra enemies a projectile may hit after the first.</summary>
        public int Pierce { get; init; }
        /// <summary>Projectiles per volley, indexed by level − 1.</summary>
        public IReadOnlyList<int> CountByLevel { get; init; } = Array.Empty<int>();
        /// <summary>Duration of a persistent weapon volley (seconds).</summary>
        public float Duration { get; init; }
        /// <summary>Minimum interval between repeated hits or aura ticks (seconds).</summary>
        public float HitInterval { get; init; }
        /// <summary>Counter-clockwise angular speed (degrees per second).</summary>
        public float AngularSpeedDegrees { get; init; }
        /// <summary>Cooldown change for each level above one (seconds).</summary>
        public float CooldownPerLevel { get; init; }
        /// <summary>Half-width of an instant capsule weapon, or a Strike's blast radius, before the area multiplier.</summary>
        public float Width { get; init; }
        /// <summary>Stun applied to each enemy hit by a Shockwave or Strike weapon (0 = none).</summary>
        public float StunSeconds { get; init; }
        /// <summary>Evolution: the weapon it replaces (−1 for a normal item).</summary>
        public int EvolvesFrom { get; init; } = -1;
        /// <summary>Evolution: the passive the hero must own (any level) next to the max-level base weapon.</summary>
        public int EvolutionPassive { get; init; } = -1;
        /// <summary>Passive: effect per level (see <see cref="SurvivorCatalog.PassivePerLevel"/>).</summary>
        public float PerLevel { get; init; }
        /// <summary>Bounce: wall/obstacle bounces per projectile, indexed by level − 1.</summary>
        public IReadOnlyList<int> BouncesByLevel { get; init; } = Array.Empty<int>();
        /// <summary>Freeze: trigger chance at level 1 and its growth per level above 1.</summary>
        public float Chance { get; init; }
        public float ChancePerLevel { get; init; }
        /// <summary>Freeze: stun growth per level above 1 (seconds).</summary>
        public float StunPerLevel { get; init; }
    }

    public static class SurvivorCatalog
    {
        /// <summary>
        /// Schema v5: 128 slots. Indices never move. Reserved ranges: 64-95 new weapons, 96-111 new passives,
        /// 112-127 evolutions of the new weapons. Free old ids: 38-39; 62/63 stay the bonus fillers.
        /// </summary>
        public const int CatalogSize = 128;
        /// <summary>Gameplay slot caps (M9: 6 + 6). <see cref="SurvivorTuning.MaxWeaponSlots"/> may lower them for old-rule tests.</summary>
        public const int MaxWeapons = 6;
        public const int MaxPassives = 6;

        public const int SweepIndex = 0;
        public const int SpearThrustIndex = 1;
        public const int OrbitAxeIndex = 2;
        public const int ThrownHammerIndex = 3;
        public const int AuraIndex = 4;
        public const int ShockwaveIndex = 5;

        public const int IronHeartIndex = 6;
        public const int BoneArmorIndex = 7;
        public const int MightGauntletIndex = 8;
        public const int CritEyeIndex = 9;
        public const int HourglassIndex = 10;
        public const int AreaCharmIndex = 11;
        public const int WindBootsIndex = 12;
        public const int MagnetCharmIndex = 13;
        // M9 wave 1a: Warrior weapons 26..30 and passives for every class 58..61.
        public const int FlameConeIndex = 26;
        public const int ComboBladeIndex = 27;
        public const int HeavyHammerIndex = 28;
        public const int BombIndex = 29;
        public const int RetaliateIndex = 30;
        public const int RecoveryIndex = 58;
        public const int CloverIndex = 59;
        public const int GreedIndex = 60;
        public const int CrownIndex = 61;
        // M9 wave 1b: Warrior weapons 31..33 and passives for every class 34..37.
        public const int BarrierIndex = 31;
        public const int BoomerangIndex = 32;
        public const int PoisonPoolIndex = 33;
        public const int DurationCharmIndex = 34;
        public const int DuplicatorIndex = 35;
        public const int SpikedArmorIndex = 36;
        public const int OmniBoxIndex = 37;
        // M9 group C: weapons 64..68 (Warrior gets bounce-shot; Archer bounce-shot and momentum-spirit; Mage bounce-shot, time-clock, purge, bomb-ring).
        public const int BounceShotIndex = 64;
        public const int MomentumSpiritIndex = 65;
        public const int TimeClockIndex = 66;
        public const int PurgeIndex = 67;
        public const int BombRingIndex = 68;
        public const int BonusGoldIndex = 62;
        public const int BonusHealIndex = 63;

        public const float MagnetChance = 0.002f;
        public const float MagnetDropAngle = 3.6f;
        public const float ChestDropAngle = 4.8f;
        public const float ChestGoldMin = 50f;
        public const float ChestGoldMax = 150f;

        private static readonly ItemDef Sweep = new ItemDef
        {
            CatalogIndex = 0, Id = "sword-sweep", Name = "Kiếm quét", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Sweep,
            BaseDamage = 20f, DamagePerLevel = 8f, BaseRange = 2.5f, BaseCooldown = 1.2f, Knockback = 0.5f,
            RangePerLevel = 0.1f, ArcDegrees = 120f, BackArcLevel = 5
        };
        private static readonly ItemDef SpearThrust = new ItemDef
        {
            CatalogIndex = SpearThrustIndex, Id = "spear-thrust", Name = "Giáo đâm", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Thrust,
            BaseDamage = 30f, DamagePerLevel = 10f, BaseRange = 5f, BaseCooldown = 1.8f, Knockback = 0.3f,
            Width = 0.3f, CountByLevel = Array.AsReadOnly(new[] { 1, 1, 2, 2, 3 })
        };
        private static readonly ItemDef OrbitAxe = new ItemDef
        {
            CatalogIndex = OrbitAxeIndex, Id = "orbit-axe", Name = "Rìu xoay", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Orbit,
            BaseDamage = 12f, DamagePerLevel = 4f, BaseRange = 2.2f, BaseCooldown = 3f, Knockback = 0.4f,
            ProjectileRadius = 0.5f, Duration = 4f, HitInterval = 0.5f, AngularSpeedDegrees = 300f,
            CountByLevel = Array.AsReadOnly(new[] { 1, 2, 2, 3, 3 })
        };
        private static readonly ItemDef Hammer = new ItemDef
        {
            CatalogIndex = 3, Id = "thrown-hammer", Name = "Búa ném", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Thrown,
            BaseDamage = 25f, DamagePerLevel = 7f, BaseRange = 10f, BaseCooldown = 1.5f, Knockback = 0.5f,
            ProjectileSpeed = 12f, ProjectileRadius = 0.4f, ProjectileRange = 12f, Pierce = 1,
            CountByLevel = Array.AsReadOnly(new[] { 1, 2, 2, 3, 3 })
        };
        private static readonly ItemDef Aura = new ItemDef
        {
            CatalogIndex = AuraIndex, Id = "aura", Name = "Hào quang", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Aura,
            BaseDamage = 5f, DamagePerLevel = 2f, BaseRange = 1.8f, Knockback = 0.15f,
            RangePerLevel = 0.1f, HitInterval = 0.4f
        };
        private static readonly ItemDef Shockwave = new ItemDef
        {
            CatalogIndex = ShockwaveIndex, Id = "shockwave", Name = "Sóng chấn động", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Shockwave,
            BaseDamage = 18f, DamagePerLevel = 6f, BaseRange = 5f, BaseCooldown = 4f, CooldownPerLevel = -0.3f,
            Knockback = 2f, ProjectileSpeed = 10f
        };
        private static readonly ItemDef IronHeart = Passive(IronHeartIndex, "iron-heart", "Tim sắt");
        private static readonly ItemDef BoneArmor = Passive(BoneArmorIndex, "bone-armor", "Giáp xương");
        private static readonly ItemDef CritEye = Passive(CritEyeIndex, "crit-eye", "Mắt chí mạng");
        private static readonly ItemDef WindBoots = Passive(WindBootsIndex, "wind-boots", "Ủng gió");
        private static readonly ItemDef BonusGold = Filler(62, "bonus-gold", "+25 vàng");
        private static readonly ItemDef BonusHeal = Filler(63, "bonus-heal", "Hồi 30 máu");

        private static readonly ItemDef MightGauntlet = Passive(MightGauntletIndex, "might-gauntlet", "Găng sức mạnh");
        private static readonly ItemDef Hourglass = Passive(HourglassIndex, "hourglass", "Đồng hồ cát");
        private static readonly ItemDef AreaCharm = Passive(AreaCharmIndex, "area-charm", "Bùa vùng");
        private static readonly ItemDef MagnetCharm = Passive(MagnetCharmIndex, "magnet-charm", "Nam châm");

        // Mage weapons (14..19).
        public const int MagicBoltIndex = 14;
        public const int FireOrbIndex = 15;
        public const int FrostNovaIndex = 16;
        public const int HolyFieldIndex = 17;
        public const int LightningIndex = 18;
        public const int ArcaneBeamIndex = 19;
        // Archer weapons (20..25).
        public const int ArrowIndex = 20;
        public const int MultiShotIndex = 21;
        public const int ArrowRainIndex = 22;
        public const int OrbitKnifeIndex = 23;
        public const int DaggerIndex = 24;
        public const int CrossbowIndex = 25;
        /// <summary>Evolutions occupy 40..57: evolution of weapon w is <see cref="EvolutionOf"/>(w).</summary>
        public const int FirstEvolutionIndex = 40;
        public const int EvolutionCount = 18;

        private static readonly ItemDef MagicBolt = new ItemDef
        {
            CatalogIndex = MagicBoltIndex, Id = "magic-bolt", Name = "Tia phép", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Thrown,
            BaseDamage = 15f, DamagePerLevel = 5f, BaseRange = 12f, BaseCooldown = 1f, Knockback = 0.2f,
            ProjectileSpeed = 14f, ProjectileRadius = 0.3f, ProjectileRange = 14f,
            CountByLevel = Array.AsReadOnly(new[] { 1, 1, 2, 2, 3 })
        };
        private static readonly ItemDef FireOrb = new ItemDef
        {
            CatalogIndex = FireOrbIndex, Id = "fire-orb", Name = "Cầu lửa xoay", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Orbit,
            BaseDamage = 10f, DamagePerLevel = 4f, BaseRange = 2.6f, BaseCooldown = 3.5f, Knockback = 0.3f,
            ProjectileRadius = 0.45f, Duration = 4f, HitInterval = 0.5f, AngularSpeedDegrees = 240f,
            CountByLevel = Array.AsReadOnly(new[] { 1, 2, 2, 3, 3 })
        };
        private static readonly ItemDef FrostNova = new ItemDef
        {
            CatalogIndex = FrostNovaIndex, Id = "frost-nova", Name = "Vòng băng", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Shockwave,
            BaseDamage = 14f, DamagePerLevel = 5f, BaseRange = 5.5f, BaseCooldown = 4.5f, CooldownPerLevel = -0.3f,
            Knockback = 1f, ProjectileSpeed = 9f, StunSeconds = 0.6f
        };
        private static readonly ItemDef HolyField = new ItemDef
        {
            CatalogIndex = HolyFieldIndex, Id = "holy-field", Name = "Vùng thánh", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Aura,
            BaseDamage = 6f, DamagePerLevel = 2f, BaseRange = 2.2f, Knockback = 0.1f,
            RangePerLevel = 0.1f, HitInterval = 0.5f
        };
        private static readonly ItemDef Lightning = new ItemDef
        {
            CatalogIndex = LightningIndex, Id = "lightning", Name = "Sét", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Strike,
            BaseDamage = 28f, DamagePerLevel = 9f, BaseRange = 9f, BaseCooldown = 2f, Knockback = 0.2f,
            Width = 1f, StunSeconds = 0.2f, CountByLevel = Array.AsReadOnly(new[] { 1, 1, 2, 2, 3 })
        };
        private static readonly ItemDef ArcaneBeam = new ItemDef
        {
            CatalogIndex = ArcaneBeamIndex, Id = "arcane-beam", Name = "Tia ma thuật", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Thrust,
            BaseDamage = 22f, DamagePerLevel = 8f, BaseRange = 7f, BaseCooldown = 2.2f, Knockback = 0.2f,
            Width = 0.4f, CountByLevel = Array.AsReadOnly(new[] { 1, 1, 1, 2, 2 })
        };
        private static readonly ItemDef Arrow = new ItemDef
        {
            CatalogIndex = ArrowIndex, Id = "arrow", Name = "Mũi tên", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Thrown,
            BaseDamage = 18f, DamagePerLevel = 6f, BaseRange = 14f, BaseCooldown = 0.9f, Knockback = 0.3f,
            ProjectileSpeed = 18f, ProjectileRadius = 0.25f, ProjectileRange = 16f, Pierce = 1,
            CountByLevel = Array.AsReadOnly(new[] { 1, 1, 2, 2, 3 })
        };
        private static readonly ItemDef MultiShot = new ItemDef
        {
            CatalogIndex = MultiShotIndex, Id = "multi-shot", Name = "Tên chùm", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Fan,
            BaseDamage = 10f, DamagePerLevel = 4f, BaseRange = 9f, BaseCooldown = 1.6f, Knockback = 0.2f,
            ProjectileSpeed = 16f, ProjectileRadius = 0.25f, ProjectileRange = 11f, ArcDegrees = 40f,
            CountByLevel = Array.AsReadOnly(new[] { 3, 3, 4, 5, 5 })
        };
        private static readonly ItemDef ArrowRain = new ItemDef
        {
            CatalogIndex = ArrowRainIndex, Id = "arrow-rain", Name = "Mưa tên", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Strike,
            BaseDamage = 16f, DamagePerLevel = 6f, BaseRange = 10f, BaseCooldown = 2.5f, Knockback = 0.1f,
            Width = 1.4f, CountByLevel = Array.AsReadOnly(new[] { 2, 2, 3, 3, 4 })
        };
        private static readonly ItemDef OrbitKnife = new ItemDef
        {
            CatalogIndex = OrbitKnifeIndex, Id = "orbit-knife", Name = "Dao xoay", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Orbit,
            BaseDamage = 9f, DamagePerLevel = 3f, BaseRange = 1.9f, BaseCooldown = 2.5f, Knockback = 0.2f,
            ProjectileRadius = 0.35f, Duration = 3.5f, HitInterval = 0.4f, AngularSpeedDegrees = 420f,
            CountByLevel = Array.AsReadOnly(new[] { 1, 2, 3, 3, 4 })
        };
        private static readonly ItemDef Dagger = new ItemDef
        {
            CatalogIndex = DaggerIndex, Id = "dagger", Name = "Dao găm", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Sweep,
            BaseDamage = 14f, DamagePerLevel = 5f, BaseRange = 1.8f, BaseCooldown = 0.7f, Knockback = 0.2f,
            RangePerLevel = 0.1f, ArcDegrees = 180f, BackArcLevel = 5
        };
        private static readonly ItemDef Crossbow = new ItemDef
        {
            CatalogIndex = CrossbowIndex, Id = "crossbow", Name = "Nỏ", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Thrust,
            BaseDamage = 45f, DamagePerLevel = 15f, BaseRange = 9f, BaseCooldown = 2.6f, Knockback = 1f,
            Width = 0.25f, CountByLevel = Array.AsReadOnly(new[] { 1, 1, 1, 2, 2 })
        };

        private static readonly ItemDef FlameCone = new ItemDef
        {
            CatalogIndex = FlameConeIndex, Id = "flame-cone", Name = "Phun lửa", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Sweep,
            BaseDamage = 6f, DamagePerLevel = 2f, BaseRange = 3.2f, BaseCooldown = 0.35f, Knockback = 0f, ArcDegrees = 60f
        };
        private static readonly ItemDef ComboBlade = new ItemDef
        {
            CatalogIndex = ComboBladeIndex, Id = "combo-blade", Name = "Kiếm liên hoàn", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Combo,
            BaseDamage = 14f, DamagePerLevel = 5f, BaseRange = 2.6f, BaseCooldown = 1.6f, Knockback = 0.3f, ArcDegrees = 90f,
            HitInterval = ComboHitInterval, CountByLevel = Array.AsReadOnly(new[] { ComboHits })
        };
        private static readonly ItemDef HeavyHammer = new ItemDef
        {
            CatalogIndex = HeavyHammerIndex, Id = "heavy-hammer", Name = "Búa nặng", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Thrown,
            BaseDamage = 60f, DamagePerLevel = 15f, BaseRange = 8f, BaseCooldown = 2.6f, Knockback = 1.2f,
            ProjectileSpeed = 8f, ProjectileRadius = 0.8f, ProjectileRange = 8f, Pierce = 3,
            CountByLevel = Array.AsReadOnly(new[] { 1, 1, 1, 2, 2 })
        };
        private static readonly ItemDef Bomb = new ItemDef
        {
            CatalogIndex = BombIndex, Id = "bomb", Name = "Bom", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Bomb,
            BaseDamage = 30f, DamagePerLevel = 9f, BaseRange = 9f, BaseCooldown = 2f, Knockback = 1f,
            ProjectileSpeed = 9f, ProjectileRadius = 0.3f, ProjectileRange = 9f, Width = 2f
        };
        private static readonly ItemDef Retaliate = new ItemDef
        {
            CatalogIndex = RetaliateIndex, Id = "retaliate", Name = "Phản đòn", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Retaliate,
            BaseDamage = 20f, DamagePerLevel = 8f, BaseRange = 2.5f, BaseCooldown = 1f, Knockback = 1f
        };
        private static readonly ItemDef Barrier = new ItemDef
        {
            CatalogIndex = BarrierIndex, Id = "barrier", Name = "Vòng bảo hộ", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Barrier,
            BaseCooldown = 12f, CooldownPerLevel = -1f, CountByLevel = Array.AsReadOnly(new[] { 1, 1, 2, 2, 3 })
        };
        private static readonly ItemDef Boomerang = new ItemDef
        {
            CatalogIndex = BoomerangIndex, Id = "boomerang", Name = "Boomerang", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Boomerang,
            BaseDamage = 18f, DamagePerLevel = 6f, BaseRange = 10f, BaseCooldown = 1.8f, Knockback = 0.3f,
            ProjectileSpeed = 11f, ProjectileRadius = 0.45f, ProjectileRange = 8f, CountByLevel = Array.AsReadOnly(new[] { 1, 1, 2, 2, 3 })
        };
        private static readonly ItemDef PoisonPool = new ItemDef
        {
            CatalogIndex = PoisonPoolIndex, Id = "poison-pool", Name = "Bình độc", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Zone,
            BaseDamage = 5f, DamagePerLevel = 2f, BaseRange = 8f, BaseCooldown = 4f, Width = 1.8f, Duration = 3.5f, HitInterval = 0.5f
        };
        private static readonly ItemDef BounceShot = new ItemDef
        {
            CatalogIndex = BounceShotIndex, Id = "bounce-shot", Name = "Đạn nảy", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Bounce,
            BaseDamage = 12f, DamagePerLevel = 4f, BaseRange = 12f, BaseCooldown = 2f, Knockback = 0.2f,
            ProjectileSpeed = 10f, ProjectileRadius = 0.35f, ProjectileRange = 40f, // 4 s of flight
            CountByLevel = Array.AsReadOnly(new[] { 1, 1, 2, 2, 3 }), BouncesByLevel = Array.AsReadOnly(new[] { 3, 3, 4, 4, 5 })
        };
        private static readonly ItemDef MomentumSpirit = new ItemDef
        {
            CatalogIndex = MomentumSpiritIndex, Id = "momentum-spirit", Name = "Bóng tốc", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Momentum,
            BaseDamage = 10f, DamagePerLevel = 4f, BaseCooldown = 1f, Knockback = 0.2f, ArcDegrees = 20f,
            ProjectileSpeed = 12f, ProjectileRadius = 0.3f, ProjectileRange = 14f, CountByLevel = Array.AsReadOnly(new[] { 1, 2, 2, 3, 3 })
        };
        private static readonly ItemDef TimeClock = new ItemDef
        {
            CatalogIndex = TimeClockIndex, Id = "time-clock", Name = "Đồng hồ băng", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Freeze,
            BaseRange = 14f, BaseCooldown = 6f, CooldownPerLevel = -0.5f, Chance = 0.3f, ChancePerLevel = 0.1f, StunSeconds = 1.2f, StunPerLevel = 0.2f
        };
        private static readonly ItemDef Purge = new ItemDef
        {
            CatalogIndex = PurgeIndex, Id = "purge", Name = "Thanh tẩy", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Purge,
            BaseRange = 14f, BaseCooldown = 30f, CooldownPerLevel = -2f
        };
        private static readonly ItemDef BombRing = new ItemDef
        {
            CatalogIndex = BombRingIndex, Id = "bomb-ring", Name = "Mưa bom vòng", Kind = ItemKind.Weapon, Pattern = WeaponPattern.BombRing,
            BaseDamage = 22f, DamagePerLevel = 7f, BaseRange = 4f, BaseCooldown = 3.5f, Knockback = 0.5f, Width = 1.3f, HitInterval = 0.15f,
            CountByLevel = Array.AsReadOnly(new[] { 4, 4, 5, 5, 6 })
        };
        private static readonly ItemDef DurationCharm = Passive(DurationCharmIndex, "duration-charm", "Bùa thời gian");
        private static readonly ItemDef Duplicator = Passive(DuplicatorIndex, "duplicator", "Bộ nhân đôi", 2);
        private static readonly ItemDef SpikedArmor = Passive(SpikedArmorIndex, "spiked-armor", "Giáp phản");
        private static readonly ItemDef OmniBox = Passive(OmniBoxIndex, "omni-box", "Hộp tổng hợp");
        private static readonly ItemDef Recovery = Passive(RecoveryIndex, "recovery", "Hồi phục");
        private static readonly ItemDef Clover = Passive(CloverIndex, "clover", "Cỏ may mắn");
        private static readonly ItemDef Greed = Passive(GreedIndex, "greed", "Tham lam");
        private static readonly ItemDef Crown = Passive(CrownIndex, "crown", "Vương miện");

        /// <summary>Combo blade: strikes per swing and seconds between them (the last strike deals <see cref="ComboFinisherMul"/>×).</summary>
        public const int ComboHits = 3;
        /// <summary>Spiked armor: share of each contact hit taken that is reflected, per level.</summary>
        public const float SpikedReflectPerLevel = 0.1f;
        /// <summary>Most projectiles/axes/strikes a volley may have after the duplicator (buffers are sized for it).</summary>
        public const int MaxVolleyCount = 8;
        public const float ComboHitInterval = 0.25f;
        public const float ComboFinisherMul = 2f;
        /// <summary>Bounce shot: an enemy is hit at most once per this many seconds by one projectile.</summary>
        public const float BounceRehitSeconds = 0.5f;
        /// <summary>Momentum spirit: the hero counts as moving above this speed (m/s); the factor is an EMA with this time constant (s); damage × (Min + Span × factor).</summary>
        public const float MomentumSpeedThreshold = 0.5f;
        public const float MomentumTimeConstant = 1.5f;
        public const float MomentumMinMul = 0.4f;
        public const float MomentumSpanMul = 1.2f;
        /// <summary>Time clock: elites are stunned for this share of the time. Purge: share of max HP elites and the boss lose.</summary>
        public const float FreezeEliteShare = 0.5f;
        public const float PurgeBossFraction = 0.05f;
        /// <summary>Bomb ring: the ring turns this many radians every salvo; at most this many blasts wait at once.</summary>
        public const float BombRingRotationStep = 0.5235988f;
        public const int MaxPendingBlasts = 8;

        private static readonly ItemDef[] Evolutions =
        {
            Evolve(Sweep, 40, "storm-blade", "Kiếm bão", MightGauntletIndex),
            Evolve(SpearThrust, 41, "dragon-lance", "Thương rồng", CritEyeIndex),
            Evolve(OrbitAxe, 42, "axe-cyclone", "Lốc rìu", AreaCharmIndex),
            Evolve(Hammer, 43, "thunder-hammer", "Búa sấm", HourglassIndex),
            Evolve(Aura, 44, "holy-aura", "Hào quang thánh", IronHeartIndex),
            Evolve(Shockwave, 45, "earthquake", "Động đất", BoneArmorIndex),
            Evolve(MagicBolt, 46, "spell-storm", "Bão phép", HourglassIndex),
            Evolve(FireOrb, 47, "solar-ring", "Vành mặt trời", AreaCharmIndex),
            Evolve(FrostNova, 48, "ice-age", "Kỷ băng hà", BoneArmorIndex),
            Evolve(HolyField, 49, "sanctuary", "Thánh địa", IronHeartIndex),
            Evolve(Lightning, 50, "thunder-god", "Lôi thần", CritEyeIndex),
            Evolve(ArcaneBeam, 51, "doom-ray", "Tia hủy diệt", MightGauntletIndex),
            Evolve(Arrow, 52, "wind-arrow", "Tên gió", WindBootsIndex),
            Evolve(MultiShot, 53, "arrow-fan", "Quạt tên", AreaCharmIndex),
            Evolve(ArrowRain, 54, "sky-arrows", "Thiên tiễn", MagnetCharmIndex),
            Evolve(OrbitKnife, 55, "blade-dance", "Vũ điệu dao", MightGauntletIndex),
            Evolve(Dagger, 56, "twin-assassin", "Song đao ám sát", CritEyeIndex),
            Evolve(Crossbow, 57, "siege-crossbow", "Nỏ công thành", BoneArmorIndex)
        };

        public static ItemDef Get(int index)
        {
            if (index >= FirstEvolutionIndex && index < FirstEvolutionIndex + EvolutionCount) return Evolutions[index - FirstEvolutionIndex];
            return index switch
            {
                0 => Sweep,
                1 => SpearThrust,
                2 => OrbitAxe,
                3 => Hammer,
                4 => Aura,
                5 => Shockwave,
                6 => IronHeart,
                7 => BoneArmor,
                8 => MightGauntlet,
                9 => CritEye,
                10 => Hourglass,
                11 => AreaCharm,
                12 => WindBoots,
                13 => MagnetCharm,
                14 => MagicBolt,
                15 => FireOrb,
                16 => FrostNova,
                17 => HolyField,
                18 => Lightning,
                19 => ArcaneBeam,
                20 => Arrow,
                21 => MultiShot,
                22 => ArrowRain,
                23 => OrbitKnife,
                24 => Dagger,
                25 => Crossbow,
                26 => FlameCone,
                27 => ComboBlade,
                28 => HeavyHammer,
                29 => Bomb,
                30 => Retaliate,
                31 => Barrier,
                32 => Boomerang,
                33 => PoisonPool,
                64 => BounceShot,
                65 => MomentumSpirit,
                66 => TimeClock,
                67 => Purge,
                68 => BombRing,
                34 => DurationCharm,
                35 => Duplicator,
                36 => SpikedArmor,
                37 => OmniBox,
                58 => Recovery,
                59 => Clover,
                60 => Greed,
                61 => Crown,
                62 => BonusGold,
                63 => BonusHeal,
                _ => null
            };
        }

        /// <summary>Catalog index of the evolution of base weapon <paramref name="weapon"/>, or −1.</summary>
        public static int EvolutionOf(int weapon)
        {
            for (int i = 0; i < Evolutions.Length; i++) if (Evolutions[i].EvolvesFrom == weapon) return Evolutions[i].CatalogIndex;
            return -1;
        }

        /// <summary>
        /// An evolution keeps the base weapon's pattern with its level-5 numbers improved: damage ×1.5,
        /// range ×1.2, cooldown ×0.8, one more projectile, +1 pierce, longer orbit, faster hits.
        /// It has a single level, so the level-scaling fields are folded in and zeroed.
        /// </summary>
        private static ItemDef Evolve(ItemDef b, int index, string id, string name, int passive)
        {
            int top = b.MaxLevel - 1;
            int topCount = b.CountByLevel.Count == 0 ? 0 : b.CountByLevel[Math.Min(top, b.CountByLevel.Count - 1)];
            return new ItemDef
            {
                CatalogIndex = index, Id = id, Name = name, Kind = ItemKind.Weapon, Pattern = b.Pattern, MaxLevel = 1,
                BaseDamage = (b.BaseDamage + b.DamagePerLevel * top) * 1.5f,
                BaseRange = b.BaseRange * (1f + b.RangePerLevel * top) * 1.2f,
                BaseCooldown = (b.BaseCooldown + b.CooldownPerLevel * top) * 0.8f,
                Knockback = b.Knockback * 1.2f,
                ArcDegrees = b.ArcDegrees, BackArcLevel = b.BackArcLevel > 0 ? 1 : 0,
                ProjectileSpeed = b.ProjectileSpeed, ProjectileRadius = b.ProjectileRadius * 1.2f,
                ProjectileRange = b.ProjectileRange * 1.2f, Pierce = b.ProjectileSpeed > 0f ? b.Pierce + 1 : b.Pierce,
                CountByLevel = topCount == 0 ? Array.Empty<int>() : Array.AsReadOnly(new[] { topCount + 1 }),
                Duration = b.Duration * 1.25f, HitInterval = b.HitInterval * 0.8f,
                AngularSpeedDegrees = b.AngularSpeedDegrees, Width = b.Width * 1.2f, StunSeconds = b.StunSeconds * 1.5f,
                EvolvesFrom = b.CatalogIndex, EvolutionPassive = passive
            };
        }

        /// <summary>
        /// Effect per level of a passive row (reserved rows included, so the derived-stat formulas
        /// already cover them): iron-heart +10% max HP, bone-armor +1 armor, might-gauntlet +8% damage,
        /// crit-eye +4% crit chance, hourglass −6% cooldown (returned positive), area-charm +8% area,
        /// wind-boots +8% move speed, magnet-charm +25% pickup radius; M9: recovery +0.2 HP/s, clover +5 Luck
        /// (one Luck stat point), greed +10% gold, crown +8% EXP; M9 1b: duration-charm +10% duration, duplicator +1 projectile,
        /// spiked-armor +1 armor (the reflect share is <see cref="SpikedReflectPerLevel"/>), omni-box +4% damage, move speed, duration and area. 0 for other rows.
        /// </summary>
        public static float PassivePerLevel(int index)
        {
            return index switch
            {
                IronHeartIndex => 0.1f,
                BoneArmorIndex => 1f,
                MightGauntletIndex => 0.08f,
                CritEyeIndex => 0.04f,
                HourglassIndex => 0.06f,
                AreaCharmIndex => 0.08f,
                WindBootsIndex => 0.08f,
                MagnetCharmIndex => 0.25f,
                RecoveryIndex => 0.2f,
                CloverIndex => 5f,
                GreedIndex => 0.1f,
                CrownIndex => 0.08f,
                DurationCharmIndex => 0.1f,
                DuplicatorIndex => 1f,
                SpikedArmorIndex => 1f,
                OmniBoxIndex => 0.04f,
                _ => 0f
            };
        }

        private static ItemDef Passive(int index, string id, string name, int maxLevel = 5) =>
            new ItemDef { CatalogIndex = index, Id = id, Name = name, Kind = ItemKind.Passive, PerLevel = PassivePerLevel(index), MaxLevel = maxLevel };

        private static ItemDef Filler(int index, string id, string name) =>
            new ItemDef { CatalogIndex = index, Id = id, Name = name, Kind = ItemKind.Filler, MaxLevel = 0 };

        /// <summary>Angular offset of spear k in an n-spear fan, in radians.</summary>
        public static float ThrustAngleOffset(int k, int n) =>
            (k - (n - 1) * 0.5f) * 20f * MathF.PI / 180f;
    }

    public static class StatInfo
    {
        public const int UsedCount = 13;
        public const int SlotCount = 16;

        public static float PerPoint(StatId stat)
        {
            return stat switch
            {
                StatId.MaxHp => 0.05f, StatId.Armor => 0.5f, StatId.Regen => 0.1f,
                StatId.Might => 0.03f, StatId.Crit => 0.02f, StatId.CritDamage => 0.1f,
                StatId.Cooldown => -0.02f, StatId.Area => 0.03f, StatId.MoveSpeed => 0.02f,
                StatId.Magnet => 0.1f, StatId.Luck => 5f, StatId.Greed => 0.05f,
                StatId.Growth => 0.03f, _ => 0f
            };
        }

        public static int Cap(StatId stat)
        {
            return stat switch
            {
                StatId.MaxHp or StatId.Might or StatId.Crit or StatId.CritDamage or StatId.Greed => 20,
                StatId.Cooldown or StatId.Area => 15,
                StatId.Armor or StatId.Regen or StatId.MoveSpeed or StatId.Magnet or StatId.Luck or StatId.Growth => 10,
                _ => 0
            };
        }
    }

    /// <summary>Explode: winds up next to the hero, then blows up (area damage) and dies without drops.</summary>
    public enum SurvivorAttackKind { Contact, Melee, Ranged, Explode }

    public sealed class SurvivorEnemyDef
    {
        public int TypeIndex { get; init; }
        public string Id { get; init; } = string.Empty;
        public float BaseHp { get; init; }
        public float MoveSpeed { get; init; }
        public float Radius { get; init; }
        public float Mass { get; init; }
        public SurvivorAttackKind AttackKind { get; init; }
        public float AttackDamage { get; init; }
        public float AttackRange { get; init; }
        public float AttackArcDegrees { get; init; }
        public float WindupSeconds { get; init; }
        public float RecoverSeconds { get; init; }
        public int Xp { get; init; }
        public float KnockbackResist { get; init; }
        public float TurnSpeedDegPerSec { get; init; }
        public bool IsBoss { get; init; }
        public float PreferredDistance { get; init; }
        public float ProjectileSpeed { get; init; }
        public float ProjectileRadius { get; init; }
        public float ProjectileRange { get; init; }
        /// <summary>Explode: blast radius around the enemy.</summary>
        public float ExplodeRadius { get; init; }
        /// <summary>Moves through obstacles (still kept inside the map).</summary>
        public bool IgnoresObstacles { get; init; }
        /// <summary>Seconds between walker summons (0 = never; the boss uses the tuning instead).</summary>
        public float SummonInterval { get; init; }
        public int SummonCount { get; init; }
    }

    public sealed class SpawnPhase
    {
        public float From { get; init; }
        public float To { get; init; }
        public IReadOnlyList<int> Weights { get; init; }
        public int MaxAlive { get; init; }
        public float SpawnsPerSecond { get; init; }
    }

    public sealed class SurvivorClassDef
    {
        public string Id = string.Empty;
        public float MaxHp;
        public float Regen;
        public float Armor;
        public float MoveSpeed;
        public float Acceleration;
        public float Radius;
        public float PickupRadius;
        public float CritChance;
        public float CritDamage;
        public float MaxEnergy;
        public float EnergyRegen;
        public float Mass;
        public int StartingWeapon;
        public int[] WeaponPool = Array.Empty<int>();
        public int[] PassivePool = Array.Empty<int>();
        public SkillDef[] ActiveSkills = SurvivorDefaults.Skills();
    }

    public static class SurvivorDefaults
    {
        private static readonly SurvivorEnemyDef[] EnemyDefs =
        {
            Enemy(0, "walker", 15f, 2f, 0.45f, 1f, SurvivorAttackKind.Contact, 8f, 0f, 0f, 0f, 0.5f, 1, 0f, 360f),
            Enemy(1, "runner", 10f, 4f, 0.4f, 1f, SurvivorAttackKind.Contact, 5f, 0f, 0f, 0f, 0.5f, 1, 0f, 360f),
            Enemy(2, "brute", 80f, 1.6f, 0.7f, 3f, SurvivorAttackKind.Melee, 25f, 1.8f, 90f, 0.9f, 1.8f, 5, 0.6f, 180f),
            new SurvivorEnemyDef
            {
                TypeIndex = 3, Id = "spitter", BaseHp = 20f, MoveSpeed = 2.4f, Radius = 0.4f, Mass = 1f,
                AttackKind = SurvivorAttackKind.Ranged, AttackDamage = 10f, PreferredDistance = 7f,
                AttackRange = 9f, AttackArcDegrees = 60f, WindupSeconds = 0.5f, RecoverSeconds = 2.5f,
                ProjectileSpeed = 7f, ProjectileRadius = 0.3f, ProjectileRange = 12f,
                Xp = 2, KnockbackResist = 0f, TurnSpeedDegPerSec = 360f
            },
            Enemy(4, "bone-lord", 6000f, 1.4f, 1.75f, 1000f, SurvivorAttackKind.Melee, 40f, 3.5f, 180f, 1.2f, 4.8f, 0, 1f, 180f, true),
            new SurvivorEnemyDef
            {
                TypeIndex = ExploderTypeIndex, Id = "exploder", BaseHp = 25f, MoveSpeed = 2.8f, Radius = 0.45f, Mass = 1f,
                AttackKind = SurvivorAttackKind.Explode, AttackDamage = 30f, AttackRange = 1f, AttackArcDegrees = 360f,
                WindupSeconds = 0.6f, ExplodeRadius = 2.2f, Xp = 2, KnockbackResist = 0f, TurnSpeedDegPerSec = 360f
            },
            new SurvivorEnemyDef
            {
                TypeIndex = GhostTypeIndex, Id = "ghost", BaseHp = 12f, MoveSpeed = 3.6f, Radius = 0.35f, Mass = 0.5f,
                AttackKind = SurvivorAttackKind.Contact, AttackDamage = 6f, Xp = 2, KnockbackResist = 0f,
                TurnSpeedDegPerSec = 360f, IgnoresObstacles = true
            },
            new SurvivorEnemyDef
            {
                TypeIndex = NecromancerTypeIndex, Id = "necromancer", BaseHp = 60f, MoveSpeed = 2f, Radius = 0.5f, Mass = 1.5f,
                AttackKind = SurvivorAttackKind.Ranged, AttackDamage = 8f, PreferredDistance = 9f,
                AttackRange = 11f, AttackArcDegrees = 60f, WindupSeconds = 0.6f, RecoverSeconds = 3.5f,
                ProjectileSpeed = 6f, ProjectileRadius = 0.3f, ProjectileRange = 13f,
                Xp = 6, KnockbackResist = 0.3f, TurnSpeedDegPerSec = 240f, SummonInterval = 6f, SummonCount = 3
            }
        };

        public const int EnemyTypeCount = 8;
        /// <summary>Elites are only drawn from the four basic types.</summary>
        public const int EliteTypeCount = 4;
        public const int ExploderTypeIndex = 5;
        public const int GhostTypeIndex = 6;
        public const int NecromancerTypeIndex = 7;

        // New enemies only appear from 300 s, so the spawn sequence before that is unchanged.
        private static readonly SpawnPhase[] Phases =
        {
            Phase(0, 60, 1, 0, 0, 0, 30, 1), Phase(60, 180, 3, 1, 0, 0, 60, 2),
            Phase(180, 300, 3, 2, 1, 0, 90, 3), Phase(300, 420, 2, 2, 1, 1, 120, 4, exploder: 1),
            Phase(420, 600, 2, 3, 1, 2, 160, 5, exploder: 1, ghost: 1),
            Phase(600, 720, 1, 3, 2, 2, 200, 6, exploder: 2, ghost: 1, necromancer: 1),
            Phase(720, 900, 1, 3, 3, 3, 250, 8, exploder: 2, ghost: 2, necromancer: 1)
        };

        public static SurvivorClassDef Warrior()
        {
            return new SurvivorClassDef
            {
                Id = "warrior", MaxHp = 150f, Regen = 0.2f, Armor = 0f, MoveSpeed = 4.5f,
                Acceleration = 30f, Radius = 0.5f, PickupRadius = 1.5f, CritChance = 0.05f,
                CritDamage = 1.5f, MaxEnergy = 100f, EnergyRegen = 15f, Mass = 1f,
                StartingWeapon = 0, WeaponPool = new[] { 0, 1, 2, 3, 4, 5, 26, 27, 28, 29, 30, 31, 32, 33, 64 }, PassivePool = new[] { 6, 7, 8, 9, 10, 11, 12, 13, 58, 59, 60, 61, 34, 35, 36, 37 },
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
                StartingWeapon = SurvivorCatalog.MagicBoltIndex, WeaponPool = new[] { 14, 15, 16, 17, 18, 19, 64, 66, 67, 68 },
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
                StartingWeapon = SurvivorCatalog.ArrowIndex, WeaponPool = new[] { 20, 21, 22, 23, 24, 25, 64, 65 },
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

        public static SurvivorEnemyDef EnemyDef(int typeIndex) =>
            typeIndex >= 0 && typeIndex < EnemyDefs.Length ? EnemyDefs[typeIndex] : null;

        public static SpawnPhase PhaseAt(float seconds)
        {
            for (int i = 0; i < Phases.Length; i++)
            {
                if (seconds >= Phases[i].From && seconds < Phases[i].To) return Phases[i];
            }
            return null;
        }

        public static int PhaseCount => Phases.Length;
        public static SpawnPhase GetPhase(int index) => Phases[index];

        private static SurvivorEnemyDef Enemy(int type, string id, float hp, float speed, float radius,
            float mass, SurvivorAttackKind attack, float damage, float range, float arc, float windup,
            float recover, int xp, float resist, float turn, bool boss = false)
        {
            return new SurvivorEnemyDef { TypeIndex = type, Id = id, BaseHp = hp, MoveSpeed = speed,
                Radius = radius, Mass = mass, AttackKind = attack, AttackDamage = damage,
                AttackRange = range, AttackArcDegrees = arc, WindupSeconds = windup,
                RecoverSeconds = recover, Xp = xp, KnockbackResist = resist,
                TurnSpeedDegPerSec = turn, IsBoss = boss };
        }

        private static SpawnPhase Phase(float from, float to, int w, int r, int b, int s, int max, float rate,
            int exploder = 0, int ghost = 0, int necromancer = 0) =>
            new SpawnPhase
            {
                From = from, To = to, MaxAlive = max, SpawnsPerSecond = rate,
                Weights = Array.AsReadOnly(new[] { w, r, b, s, 0, exploder, ghost, necromancer })
            };
    }
}
