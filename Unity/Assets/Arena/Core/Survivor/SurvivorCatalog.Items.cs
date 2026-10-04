using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>
    /// Every catalog row and both evolution tables. STATIC INIT ORDER: initialisers of a partial class run in textual order
    /// only within one file, so every row an evolution reads must stay in THIS file ABOVE the evolution tables. Keep all
    /// static fields of <see cref="SurvivorCatalog"/> here (the other partial holds only constants and methods).
    /// </summary>
    public static partial class SurvivorCatalog
    {
        // ---- Warrior weapons ----
        private static readonly ItemDef Sweep = new ItemDef
        {
            CatalogIndex = 0, Id = "sword-sweep", Name = "Kiếm quét", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Sweep,
            BaseDamage = 20f, DamagePerLevel = 8f, BaseRange = 2.5f, BaseCooldown = 1.2f, Knockback = 0.5f,
            RangePerLevel = 0.1f, ArcDegrees = 120f, BackArcLevel = 5,
            // A sweep with a projectile speed throws a sword wave: it flies ProjectileRange and cuts every enemy on its path.
            ProjectileSpeed = 12f, ProjectileRadius = 1.1f, ProjectileRange = 6f
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
        private static readonly ItemDef Retaliate = new ItemDef
        {
            CatalogIndex = RetaliateIndex, Id = "retaliate", Name = "Phản đòn", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Retaliate,
            BaseDamage = 20f, DamagePerLevel = 8f, BaseRange = 2.5f, BaseCooldown = 1f, Knockback = 1f
        };

        // ---- Mage weapons ----
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
        private static readonly ItemDef FireballNova = new ItemDef
        {
            CatalogIndex = FireballNovaIndex, Id = "fireball-nova", Name = "Hỏa cầu nổ", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Strike,
            BaseDamage = 45f, DamagePerLevel = 14f, BaseRange = 9f, BaseCooldown = 3f, Knockback = 1.5f, Width = 1.8f,
            CountByLevel = Array.AsReadOnly(new[] { 1, 1, 1, 2, 2 })
        };
        private static readonly ItemDef MagiStone = new ItemDef
        {
            CatalogIndex = MagiStoneIndex, Id = "magi-stone", Name = "Đá tụ lực", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Stone,
            BaseDamage = 20f, DamagePerLevel = 20f, BaseRange = 10f, BaseCooldown = 0.9f,
            CountByLevel = Array.AsReadOnly(new[] { 1, 1, 2, 2, 3 })
        };

        // ---- Archer weapons ----
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
            Width = 2.6f, Clustered = true, CountByLevel = Array.AsReadOnly(new[] { 1, 1, 2, 2, 3 })
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
        private static readonly ItemDef MomentumSpirit = new ItemDef
        {
            CatalogIndex = MomentumSpiritIndex, Id = "momentum-spirit", Name = "Bóng tốc", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Momentum,
            BaseDamage = 10f, DamagePerLevel = 4f, BaseCooldown = 1f, Knockback = 0.2f, ArcDegrees = 20f,
            ProjectileSpeed = 12f, ProjectileRadius = 0.3f, ProjectileRange = 14f, CountByLevel = Array.AsReadOnly(new[] { 1, 2, 2, 3, 3 })
        };
        private static readonly ItemDef QuadShot = new ItemDef
        {
            CatalogIndex = QuadShotIndex, Id = "quad-shot", Name = "Bắn bốn hướng", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Quad,
            BaseDamage = 10f, DamagePerLevel = 4f, BaseCooldown = 1.2f, Knockback = 0.2f, Pierce = 1,
            ProjectileSpeed = 13f, ProjectileRadius = 0.25f, ProjectileRange = 9f, CountByLevel = Array.AsReadOnly(new[] { 1, 1, 2, 2, 3 })
        };

        // ---- Shared weapons (in more than one class pool) ----
        private static readonly ItemDef FlameCone = new ItemDef
        {
            CatalogIndex = FlameConeIndex, Id = "flame-cone", Name = "Phun lửa", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Sweep,
            BaseDamage = 6f, DamagePerLevel = 2f, BaseRange = 3.2f, BaseCooldown = 0.35f, Knockback = 0f, ArcDegrees = 60f
        };
        private static readonly ItemDef Bomb = new ItemDef
        {
            CatalogIndex = BombIndex, Id = "bomb", Name = "Bom", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Bomb,
            BaseDamage = 30f, DamagePerLevel = 9f, BaseRange = 9f, BaseCooldown = 2f, Knockback = 1f,
            ProjectileSpeed = 9f, ProjectileRadius = 0.3f, ProjectileRange = 9f, Width = 2f
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
        private static readonly ItemDef BraceletTrio = new ItemDef
        {
            CatalogIndex = BraceletTrioIndex, Id = "bracelet-trio", Name = "Vòng tay ba mũi", Kind = ItemKind.Weapon, Pattern = WeaponPattern.Trio,
            BaseDamage = 12f, DamagePerLevel = 4f, BaseRange = 11f, BaseCooldown = 1.4f, Knockback = 0.2f, ArcDegrees = 8f,
            ProjectileSpeed = 14f, ProjectileRadius = 0.25f, ProjectileRange = 11f, CountByLevel = Array.AsReadOnly(new[] { 3, 3, 3, 3, 3 })
        };

        // ---- Passives ----
        private static readonly ItemDef IronHeart = Passive(IronHeartIndex, "iron-heart", "Tim sắt");
        private static readonly ItemDef BoneArmor = Passive(BoneArmorIndex, "bone-armor", "Giáp xương");
        private static readonly ItemDef MightGauntlet = Passive(MightGauntletIndex, "might-gauntlet", "Găng sức mạnh");
        private static readonly ItemDef CritEye = Passive(CritEyeIndex, "crit-eye", "Mắt chí mạng");
        private static readonly ItemDef Hourglass = Passive(HourglassIndex, "hourglass", "Đồng hồ cát");
        private static readonly ItemDef AreaCharm = Passive(AreaCharmIndex, "area-charm", "Bùa vùng");
        private static readonly ItemDef WindBoots = Passive(WindBootsIndex, "wind-boots", "Ủng gió");
        private static readonly ItemDef MagnetCharm = Passive(MagnetCharmIndex, "magnet-charm", "Nam châm");
        private static readonly ItemDef DurationCharm = Passive(DurationCharmIndex, "duration-charm", "Bùa thời gian");
        private static readonly ItemDef Duplicator = Passive(DuplicatorIndex, "duplicator", "Bộ nhân đôi", 2);
        private static readonly ItemDef SpikedArmor = Passive(SpikedArmorIndex, "spiked-armor", "Giáp phản");
        private static readonly ItemDef OmniBox = Passive(OmniBoxIndex, "omni-box", "Hộp tổng hợp");
        private static readonly ItemDef Recovery = Passive(RecoveryIndex, "recovery", "Hồi phục");
        private static readonly ItemDef Clover = Passive(CloverIndex, "clover", "Cỏ may mắn");
        private static readonly ItemDef Greed = Passive(GreedIndex, "greed", "Tham lam");
        private static readonly ItemDef Crown = Passive(CrownIndex, "crown", "Vương miện");

        // ---- Fillers ----
        private static readonly ItemDef BonusGold = Filler(62, "bonus-gold", "+25 vàng");
        private static readonly ItemDef BonusHeal = Filler(63, "bonus-heal", "Hồi 30 máu");

        // ---- Evolutions (must stay below every base row) ----
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
            Evolve(ArrowRain, 54, "sky-arrows", "Thiên tiễn", MagnetCharmIndex, extraCount: 0),
            Evolve(OrbitKnife, 55, "blade-dance", "Vũ điệu dao", MightGauntletIndex),
            Evolve(Dagger, 56, "twin-assassin", "Song đao ám sát", CritEyeIndex),
            Evolve(Crossbow, 57, "siege-crossbow", "Nỏ công thành", BoneArmorIndex)
        };

        private static readonly ItemDef[] NewEvolutions =
        {
            Evolve(FlameCone, 112, "inferno", "Hỏa ngục", RecoveryIndex),
            Evolve(ComboBlade, 113, "phantom-blade", "Kiếm vô ảnh", WindBootsIndex),
            Evolve(HeavyHammer, 114, "mountain-hammer", "Búa núi", BoneArmorIndex),
            Evolve(Bomb, 115, "carpet-bomb", "Mưa bom", DuplicatorIndex),
            Evolve(Retaliate, 116, "fury", "Cơn thịnh nộ", SpikedArmorIndex, rangeMul: 1.5f),
            Evolve(Barrier, 117, "aegis", "Kết giới bất diệt", IronHeartIndex),
            Evolve(Boomerang, 118, "storm-boomerang", "Boomerang bão", HourglassIndex),
            Evolve(PoisonPool, 119, "miasma", "Đầm độc", DurationCharmIndex, widthMul: 1.3f, durationMul: 1.3f),
            Evolve(BounceShot, 120, "chaos-shot", "Đạn hỗn loạn", CloverIndex, extraBounces: 2),
            Evolve(MomentumSpirit, 121, "wraith", "Bóng ma tốc độ", OmniBoxIndex, momentumFloor: 0.4f),
            Evolve(TimeClock, 122, "eternal-corridor", "Hành lang vĩnh cửu", HourglassIndex, stunBonus: 0.5f, chanceBonus: 0.1f),
            Evolve(BombRing, 123, "nebula", "Tinh vân", AreaCharmIndex, extraCount: 2),
            Evolve(FireballNova, 124, "meteor", "Thiên thạch", MightGauntletIndex),
            Evolve(BraceletTrio, 125, "twin-bracelet", "Vòng tay song", CritEyeIndex),
            Evolve(QuadShot, 126, "four-winds", "Tứ phương", MagnetCharmIndex),
            Evolve(MagiStone, 127, "sage-stone", "Đá hiền triết", GreedIndex)
        };
    }
}
