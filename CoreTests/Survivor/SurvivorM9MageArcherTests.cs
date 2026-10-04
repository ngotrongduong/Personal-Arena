using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>M9 groups A/B for Mage and Archer (T-044): pool additions (26, 31, 33 / 29, 32, 33) and new weapons 69 fireball-nova, 70 bracelet-trio, 71 quad-shot, 72 magi-stone.</summary>
    public sealed class SurvivorM9MageArcherTests
    {
        private static readonly int[] NewWeapons = { 69, 70, 71, 72 };
        private static readonly int[] MagePool = { 14, 15, 16, 17, 18, 19, 64, 66, 67, 68, 26, 31, 33, 69, 70, 72 };
        private static readonly int[] ArcherPool = { 20, 21, 22, 23, 24, 25, 64, 65, 29, 32, 33, 70, 71 };
        private static readonly int[] MageKit = { 69, 70, 72, 31, 33, 26 };
        private static readonly int[] ArcherKit = { 70, 71, 29, 32, 33, 20 };

        // ---- catalog and pools ----

        [Test]
        public void Catalog_RowsMatchTheTaskTable()
        {
            ItemDef nova = SurvivorCatalog.Get(69), trio = SurvivorCatalog.Get(70), quad = SurvivorCatalog.Get(71), stone = SurvivorCatalog.Get(72);
            Assert.That(nova.Id, Is.EqualTo("fireball-nova")); Assert.That(nova.Name, Is.EqualTo("Hỏa cầu nổ")); Assert.That(nova.Pattern, Is.EqualTo(WeaponPattern.Strike)); Assert.That(nova.BaseDamage, Is.EqualTo(45f)); Assert.That(nova.DamagePerLevel, Is.EqualTo(14f)); Assert.That(nova.BaseRange, Is.EqualTo(9f)); Assert.That(nova.Width, Is.EqualTo(1.8f)); Assert.That(nova.BaseCooldown, Is.EqualTo(3f)); Assert.That(nova.Knockback, Is.GreaterThan(0f)); Assert.That(nova.CountByLevel, Is.EqualTo(new[] { 1, 1, 1, 2, 2 }));
            Assert.That(trio.Id, Is.EqualTo("bracelet-trio")); Assert.That(trio.Name, Is.EqualTo("Vòng tay ba mũi")); Assert.That(trio.Pattern, Is.EqualTo(WeaponPattern.Trio)); Assert.That(trio.BaseDamage, Is.EqualTo(12f)); Assert.That(trio.DamagePerLevel, Is.EqualTo(4f)); Assert.That(trio.ProjectileSpeed, Is.EqualTo(14f)); Assert.That(trio.BaseRange, Is.EqualTo(11f)); Assert.That(trio.ProjectileRange, Is.EqualTo(11f)); Assert.That(trio.BaseCooldown, Is.EqualTo(1.4f)); Assert.That(trio.ArcDegrees, Is.EqualTo(8f)); Assert.That(trio.CountByLevel, Is.EqualTo(new[] { 3, 3, 3, 3, 3 }));
            Assert.That(quad.Id, Is.EqualTo("quad-shot")); Assert.That(quad.Name, Is.EqualTo("Bắn bốn hướng")); Assert.That(quad.Pattern, Is.EqualTo(WeaponPattern.Quad)); Assert.That(quad.BaseDamage, Is.EqualTo(10f)); Assert.That(quad.DamagePerLevel, Is.EqualTo(4f)); Assert.That(quad.ProjectileSpeed, Is.EqualTo(13f)); Assert.That(quad.ProjectileRange, Is.EqualTo(9f)); Assert.That(quad.BaseCooldown, Is.EqualTo(1.2f)); Assert.That(quad.Pierce, Is.EqualTo(1)); Assert.That(quad.CountByLevel, Is.EqualTo(new[] { 1, 1, 2, 2, 3 })); Assert.That(SurvivorCatalog.QuadStaggerDegrees, Is.EqualTo(8f));
            Assert.That(stone.Id, Is.EqualTo("magi-stone")); Assert.That(stone.Name, Is.EqualTo("Đá tụ lực")); Assert.That(stone.Pattern, Is.EqualTo(WeaponPattern.Stone)); Assert.That(stone.BaseDamage, Is.EqualTo(20f)); Assert.That(stone.DamagePerLevel, Is.EqualTo(20f)); Assert.That(stone.BaseRange, Is.EqualTo(10f)); Assert.That(stone.BaseCooldown, Is.EqualTo(0.9f)); Assert.That(stone.CountByLevel, Is.EqualTo(new[] { 1, 1, 2, 2, 3 }));
            foreach (int index in NewWeapons) { ItemDef def = SurvivorCatalog.Get(index); Assert.That(def.Kind, Is.EqualTo(ItemKind.Weapon)); Assert.That(def.MaxLevel, Is.EqualTo(5)); Assert.That(def.CatalogIndex, Is.EqualTo(index)); }
            Assert.That(SurvivorCatalog.Get(73), Is.Null); Assert.That(SurvivorObservation.Size, Is.EqualTo(2592)); Assert.That(SurvivorObservation.SchemaVersion, Is.EqualTo(5));
        }

        [Test]
        public void Pools_AreExactlyTheFinalLists_AndNothingElseChanged()
        {
            Assert.That(SurvivorDefaults.Mage().WeaponPool, Is.EqualTo(MagePool)); Assert.That(SurvivorDefaults.Archer().WeaponPool, Is.EqualTo(ArcherPool));
            Assert.That(SurvivorDefaults.Warrior().WeaponPool, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 26, 27, 28, 29, 30, 31, 32, 33, 64 }));
            int[] passives = { 6, 7, 8, 9, 10, 11, 12, 13, 58, 59, 60, 61, 34, 35, 36, 37 };
            foreach (SurvivorClassDef kit in new[] { SurvivorDefaults.Warrior(), SurvivorDefaults.Mage(), SurvivorDefaults.Archer() }) Assert.That(kit.PassivePool, Is.EqualTo(passives), kit.Id);
            Assert.That(SurvivorDefaults.Mage().StartingWeapon, Is.EqualTo(14)); Assert.That(SurvivorDefaults.Archer().StartingWeapon, Is.EqualTo(20));
            foreach (int weapon in NewWeapons) Assert.That(Array.IndexOf(SurvivorDefaults.Warrior().WeaponPool, weapon), Is.LessThan(0), "warrior " + weapon);
            foreach (SurvivorClassDef kit in new[] { SurvivorDefaults.Mage(), SurvivorDefaults.Archer() })
            {
                int[] counts = new int[Enum.GetValues(typeof(WeaponPattern)).Length];
                foreach (int index in kit.WeaponPool) counts[(int)SurvivorCatalog.Get(index).Pattern]++;
                Assert.That(counts[(int)WeaponPattern.Barrier], Is.LessThanOrEqualTo(1), kit.Id); Assert.That(counts[(int)WeaponPattern.Aura], Is.LessThanOrEqualTo(1)); Assert.That(counts[(int)WeaponPattern.Shockwave], Is.LessThanOrEqualTo(1)); Assert.That(counts[(int)WeaponPattern.Combo], Is.Zero); Assert.That(counts[(int)WeaponPattern.Retaliate], Is.Zero);
                Assert.That(new HashSet<int>(kit.WeaponPool).Count, Is.EqualTo(kit.WeaponPool.Length), kit.Id + " has no duplicate");
            }
        }

        [Test]
        public void OldRules_ExcludeTheNewWeaponsAndTheAddedEntries()
        {
            int[] added = { 26, 31, 33, 69, 70, 71, 72, 29, 32 };
            foreach (SurvivorClassDef kit in new[] { SurvivorDefaults.Warrior(), SurvivorDefaults.Mage(), SurvivorDefaults.Archer() })
            {
                SurvivorConfig old = SurvivorTestHelpers.Config(); old.ClassDef = kit; SurvivorTestHelpers.OldRules(old);
                foreach (int index in added) Assert.That(Array.IndexOf(old.ClassDef.WeaponPool, index), Is.LessThan(0), kit.Id + " " + index);
            }
            Assert.That(SurvivorTestHelpers.OldConfig().ClassDef.WeaponPool, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void Offers_EachClassSeesItsWeaponsOnly()
        {
            foreach (string id in new[] { "mage", "archer" })
            {
                HashSet<int> seen = new HashSet<int>(); int[] pool = id == "mage" ? MagePool : ArcherPool;
                for (int seed = 0; seed < 500; seed++)
                {
                    SurvivorConfig config = Cfg(id); SurvivorSim sim = new SurvivorSim(config, seed); sim.GiveXpForTests(5f); sim.Step(default);
                    for (int i = 0; i < sim.OfferCount; i++) seen.Add(sim.GetOffer(i).CatalogIndex);
                }
                foreach (int weapon in new[] { 26, 29, 31, 32, 33, 64, 65, 66, 67, 68, 69, 70, 71, 72 }) Assert.That(seen.Contains(weapon), Is.EqualTo(Array.IndexOf(pool, weapon) >= 0), id + " " + weapon);
            }
        }

        [Test]
        public void SixPlusSixSlotsStillHold_WithTheNewWeapons()
        {
            foreach (string id in new[] { "mage", "archer" })
                for (int seed = 0; seed < 20; seed++)
                {
                    SurvivorSim sim = ClassSim(id, id == "mage" ? 14 : 20, 1, seed);
                    foreach (int index in id == "mage" ? new[] { 69, 70, 72, 31, 26 } : new[] { 70, 71, 29, 32, 33 }) sim.GiveItemForTests(index, 1);
                    foreach (int index in new[] { 6, 7, 8, 9, 10, 11 }) sim.GiveItemForTests(index, 1);
                    Assert.That(sim.Inventory.WeaponCount, Is.EqualTo(6)); Assert.That(sim.Inventory.PassiveCount, Is.EqualTo(6));
                    for (int n = 0; n < 5; n++)
                    {
                        sim.GiveXpForTests(500f); sim.Step(default);
                        for (int i = 0; i < sim.OfferCount; i++) { int index = sim.GetOffer(i).CatalogIndex; Assert.That(sim.Inventory.Level(index), Is.GreaterThan(0).Or.EqualTo(62).Or.EqualTo(63), id + " offer " + index + " would exceed the slots"); }
                        Step(sim, 3);
                        Assert.That(sim.Inventory.WeaponCount, Is.LessThanOrEqualTo(6)); Assert.That(sim.Inventory.PassiveCount, Is.LessThanOrEqualTo(6));
                    }
                }
        }

        // ---- fireball nova (69) ----

        [Test]
        public void Nova_BlastRadiusIsOnePointEight_BlastsOneRandomEnemyInRange()
        {
            SurvivorSim sim = ClassSim("mage", 69, 1, 1);
            SurvivorEnemy target = Tank(sim, 8.5f, 0f), inside = Tank(sim, 9.8f, 0f), outside = Tank(sim, 11.1f, 0f);
            sim.Step(default);
            SurvivorEvent landed = Find(sim.Events, SurvivorEventType.StrikeLanded, 69);
            Assert.That(landed.Value, Is.EqualTo(1.8f).Within(1e-5f)); Assert.That(landed.Point.X, Is.EqualTo(8.5f).Within(0.01f));
            Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 69).Extra, Is.EqualTo(1)); Assert.That(sim.WeaponCooldownForTests(69), Is.EqualTo(3f).Within(1e-5f));
            Assert.That(target.MaxHp - target.Hp, Is.EqualTo(45f).Within(1e-3f)); Assert.That(inside.MaxHp - inside.Hp, Is.EqualTo(45f).Within(1e-3f), "reach = radius 1.8 + enemy radius 0.45 (walker)"); Assert.That(outside.Hp, Is.EqualTo(outside.MaxHp));
        }

        [Test]
        public void Nova_KnocksTheTargetBack()
        {
            SurvivorSim sim = ClassSim("mage", 69, 1, 2); SurvivorEnemy target = Tank(sim, 6f, 0f); sim.Step(default);
            Assert.That(target.Position.X, Is.GreaterThan(6.01f));
        }

        [Test]
        public void Nova_NothingInRange_DoesNotFireOrSpendTheCooldown()
        {
            SurvivorSim sim = ClassSim("mage", 69, 1, 3); Tank(sim, 9.5f, 0f); sim.Step(default);
            Assert.That(sim.WeaponCooldownForTests(69), Is.Zero); for (int i = 0; i < sim.Events.Count; i++) Assert.That(sim.Events[i].Type, Is.Not.EqualTo(SurvivorEventType.StrikeLanded));
        }

        [TestCase(1, 1, 45f)]
        [TestCase(2, 1, 59f)]
        [TestCase(3, 1, 73f)]
        [TestCase(4, 2, 87f)]
        [TestCase(5, 2, 101f)]
        public void Nova_DamageAndBlastCountScaleWithTheLevel(int level, int blasts, float damage)
        {
            SurvivorSim sim = ClassSim("mage", 69, level, 4); SurvivorEnemy a = Tank(sim, 6f, 0f), b = Tank(sim, -6f, 0f); sim.Step(default);
            Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 69).Extra, Is.EqualTo(blasts)); Assert.That(CountEvents(sim, SurvivorEventType.StrikeLanded, 69), Is.EqualTo(blasts));
            int hit = 0; foreach (SurvivorEnemy e in new[] { a, b }) if (e.Hp < e.MaxHp) { hit++; Assert.That(e.MaxHp - e.Hp, Is.EqualTo(damage).Within(1e-3f)); }
            Assert.That(hit, Is.EqualTo(blasts), "the second blast goes to a different enemy");
        }

        [Test]
        public void Nova_ChoosesDifferentEnemiesAcrossSeeds()
        {
            int leftHits = 0, rightHits = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                SurvivorSim sim = ClassSim("mage", 69, 1, 100 + seed); SurvivorEnemy a = Tank(sim, 6f, 0f), b = Tank(sim, -6f, 0f); sim.Step(default);
                if (a.Hp < a.MaxHp) rightHits++; if (b.Hp < b.MaxHp) leftHits++;
            }
            Assert.That(leftHits, Is.GreaterThan(5)); Assert.That(rightHits, Is.GreaterThan(5)); Assert.That(leftHits + rightHits, Is.EqualTo(40));
        }

        // ---- bracelet trio (70) ----

        [TestCase("mage")]
        [TestCase("archer")]
        public void Trio_FiresThreeProjectilesInAnEightDegreeSpreadAtOneEnemy(string cls)
        {
            SurvivorSim sim = ClassSim(cls, 70, 1, 10); Tank(sim, 7f, 3f); sim.Step(default);
            List<SurvivorProjectile> shots = Shots(sim, 70); Assert.That(shots.Count, Is.EqualTo(3));
            float aim = MathF.Atan2(3f, 7f), min = float.MaxValue, max = float.MinValue, mean = 0f;
            foreach (SurvivorProjectile s in shots) { float angle = s.Velocity.Angle(); min = MathF.Min(min, angle); max = MathF.Max(max, angle); mean += angle / 3f; Assert.That(s.Velocity.Length, Is.EqualTo(14f).Within(1e-3f)); Assert.That(s.Damage, Is.EqualTo(12f)); Assert.That(s.PierceRemaining, Is.Zero); }
            Assert.That((max - min) * 180f / MathF.PI, Is.EqualTo(8f).Within(0.05f)); Assert.That(mean, Is.EqualTo(aim).Within(0.002f));
            Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 70).Extra, Is.EqualTo(3)); Assert.That(sim.WeaponCooldownForTests(70), Is.EqualTo(1.4f).Within(1e-5f));
        }

        [Test]
        public void Trio_AllThreeAimAtTheSameRandomEnemy_AndBothEnemiesGetPickedAcrossSeeds()
        {
            int picked0 = 0, picked1 = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                SurvivorSim sim = ClassSim("mage", 70, 1, 200 + seed); Tank(sim, 6f, 0f); Tank(sim, -6f, 0f); sim.Step(default);
                List<SurvivorProjectile> shots = Shots(sim, 70); Assert.That(shots.Count, Is.EqualTo(3));
                float x = MathF.Sign(shots[0].Velocity.X); foreach (SurvivorProjectile s in shots) Assert.That(MathF.Sign(s.Velocity.X), Is.EqualTo(x), "one target for the whole trio");
                if (x > 0f) picked0++; else picked1++;
            }
            Assert.That(picked0, Is.GreaterThan(5)); Assert.That(picked1, Is.GreaterThan(5));
        }

        [Test]
        public void Trio_RangeIsElevenMetres_NoTargetKeepsTheCooldown()
        {
            SurvivorSim sim = ClassSim("archer", 70, 1, 11); Tank(sim, 11.5f, 0f); sim.Step(default);
            Assert.That(Shots(sim, 70), Is.Empty); Assert.That(sim.WeaponCooldownForTests(70), Is.Zero);
            Tank(sim, 10.5f, 0f); sim.Step(default); Assert.That(Shots(sim, 70).Count, Is.EqualTo(3));
        }

        [Test]
        public void Trio_DamageGrowsByFourPerLevel_ThreeShotsEveryLevel_AndAllThreeCanHit()
        {
            for (int level = 1; level <= 5; level++)
            {
                SurvivorSim sim = ClassSim("mage", 70, level, 12); SurvivorEnemy target = Tank(sim, 5f, 0f); sim.Step(default);
                List<SurvivorProjectile> shots = Shots(sim, 70); Assert.That(shots.Count, Is.EqualTo(3), "level " + level);
                foreach (SurvivorProjectile s in shots) Assert.That(s.Damage, Is.EqualTo(12f + 4f * (level - 1)));
                Step(sim, 20); Assert.That(target.MaxHp - target.Hp, Is.EqualTo(3f * (12f + 4f * (level - 1))).Within(1e-3f), "all three hit a close target, level " + level);
            }
        }

        // ---- quad shot (71) ----

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(-2.3f)]
        public void Quad_FiresInFourDirectionsRotatingWithTheFacing_WithNoEnemy(float facing)
        {
            SurvivorSim sim = ClassSim("archer", 71, 1, 20); sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, facing); sim.Step(default);
            List<SurvivorProjectile> shots = Shots(sim, 71); Assert.That(shots.Count, Is.EqualTo(4));
            bool[] seen = new bool[4];
            foreach (SurvivorProjectile s in shots)
            {
                Assert.That(s.Velocity.Length, Is.EqualTo(13f).Within(1e-3f)); Assert.That(s.PierceRemaining, Is.EqualTo(1)); Assert.That(s.Damage, Is.EqualTo(10f));
                float turn = s.Velocity.Angle() - facing; turn = MathF.Atan2(MathF.Sin(turn), MathF.Cos(turn)); int quarter = (int)MathF.Round(turn / (MathF.PI / 2f)); quarter = (quarter + 4) % 4;
                Assert.That(MathF.Abs(turn - MathF.Round(turn / (MathF.PI / 2f)) * MathF.PI / 2f), Is.LessThan(1e-3f)); seen[quarter] = true;
            }
            Assert.That(seen, Is.All.True); Assert.That(sim.WeaponCooldownForTests(71), Is.EqualTo(1.2f).Within(1e-5f)); Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 71).Extra, Is.EqualTo(1));
        }

        [TestCase(1, 1, 10f)]
        [TestCase(2, 1, 14f)]
        [TestCase(3, 2, 18f)]
        [TestCase(4, 2, 22f)]
        [TestCase(5, 3, 26f)]
        public void Quad_CountPerDirectionAndDamageScaleWithTheLevel_StaggeredEightDegrees(int level, int perDirection, float damage)
        {
            SurvivorSim sim = ClassSim("archer", 71, level, 21); sim.Step(default);
            List<SurvivorProjectile> shots = Shots(sim, 71); Assert.That(shots.Count, Is.EqualTo(4 * perDirection)); Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 71).Extra, Is.EqualTo(perDirection));
            foreach (SurvivorProjectile s in shots) Assert.That(s.Damage, Is.EqualTo(damage));
            if (perDirection > 1)
            {
                int right = 0; float lo = float.MaxValue, hi = float.MinValue;
                foreach (SurvivorProjectile s in shots) if (MathF.Abs(s.Velocity.Angle()) < 0.3f) { right++; lo = MathF.Min(lo, s.Velocity.Angle()); hi = MathF.Max(hi, s.Velocity.Angle()); }
                Assert.That(right, Is.EqualTo(perDirection)); Assert.That((hi - lo) * 180f / MathF.PI, Is.EqualTo(8f * (perDirection - 1)).Within(0.05f), "adjacent shots are 8 degrees apart");
            }
        }

        [Test]
        public void Quad_EachShotPiercesOneExtraEnemy()
        {
            SurvivorSim sim = ClassSim("archer", 71, 1, 22); sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 0f);
            SurvivorEnemy first = Tank(sim, 3f, 0f), second = Tank(sim, 5f, 0f), third = Tank(sim, 7f, 0f); sim.Step(default); Step(sim, 40);
            Assert.That(first.MaxHp - first.Hp, Is.EqualTo(10f).Within(1e-3f)); Assert.That(second.MaxHp - second.Hp, Is.EqualTo(10f).Within(1e-3f)); Assert.That(third.Hp, Is.EqualTo(third.MaxHp), "pierce 1 = two enemies per shot");
        }

        [Test]
        public void Quad_FullProjectilePool_IsSafe_AndCooldownKeptWhenNothingFires()
        {
            SurvivorSim sim = ClassSim("archer", 71, 5, 23); sim.GiveItemForTests(35, 2);
            for (int tick = 0; tick < 400; tick++) { sim.SetWeaponCooldownForTests(71, 0f); sim.Step(default); }
            Assert.That(sim.Projectiles.Count, Is.LessThanOrEqualTo(128)); Assert.That(sim.IsEnded, Is.False);
        }

        // ---- magi stone (72) ----

        [TestCase(1, 1, 20f)]
        [TestCase(2, 1, 40f)]
        [TestCase(3, 2, 60f)]
        [TestCase(4, 2, 80f)]
        [TestCase(5, 3, 100f)]
        public void Stone_FixedDamageAndCountScaleWithTheLevel_HittingTheNearestDistinctEnemies(int level, int count, float damage)
        {
            SurvivorSim sim = ClassSim("mage", 72, level, 30);
            SurvivorEnemy[] enemies = { Tank(sim, 3f, 0f), Tank(sim, 0f, 4f), Tank(sim, -5f, 0f), Tank(sim, 0f, -6f) }; sim.Step(default);
            Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 72).Extra, Is.EqualTo(count)); Assert.That(CountEvents(sim, SurvivorEventType.StrikeLanded, 72), Is.EqualTo(count)); Assert.That(sim.WeaponCooldownForTests(72), Is.EqualTo(0.9f).Within(1e-5f));
            for (int i = 0; i < 4; i++) Assert.That(enemies[i].MaxHp - enemies[i].Hp, Is.EqualTo(i < count ? damage : 0f).Within(1e-3f), "enemy " + i + " level " + level);
        }

        [Test]
        public void Stone_IgnoresMightAndCrit()
        {
            SurvivorConfig config = Cfg("mage"); config.ClassDef.StartingWeapon = 72; config.ClassDef.CritChance = 1f; config.ClassDef.CritDamage = 3f;
            SurvivorSim sim = new SurvivorSim(config, 31); sim.GiveItemForTests(72, 3); sim.GiveItemForTests(8, 5); sim.GiveItemForTests(9, 5); sim.SetHeroInvulnerableForTests();
            SurvivorEnemy a = Tank(sim, 3f, 0f), b = Tank(sim, -4f, 0f); sim.Step(default);
            Assert.That(a.MaxHp - a.Hp, Is.EqualTo(60f).Within(1e-3f)); Assert.That(b.MaxHp - b.Hp, Is.EqualTo(60f).Within(1e-3f));
            for (int i = 0; i < sim.Events.Count; i++) Assert.That(sim.Events[i].Type, Is.Not.EqualTo(SurvivorEventType.Crit));
            // control: an ordinary weapon with the same hero does crit and gain Might.
            SurvivorConfig control = Cfg("mage"); control.ClassDef.CritChance = 1f; control.ClassDef.CritDamage = 3f; control.ClassDef.StartingWeapon = 18;
            SurvivorSim other = new SurvivorSim(control, 31); other.GiveItemForTests(8, 5); other.SetHeroInvulnerableForTests(); SurvivorEnemy t = Tank(other, 3f, 0f); other.Step(default);
            Assert.That(t.MaxHp - t.Hp, Is.GreaterThan(28f * 1.4f)); Assert.That(Find(other.Events, SurvivorEventType.Crit).Type, Is.EqualTo(SurvivorEventType.Crit));
        }

        [Test]
        public void Stone_RangeIsTenMetres_AndNoEnemyKeepsTheCooldown()
        {
            SurvivorSim sim = ClassSim("mage", 72, 1, 32); SurvivorEnemy far = Tank(sim, 11f, 0f); sim.Step(default);
            Assert.That(sim.WeaponCooldownForTests(72), Is.Zero); Assert.That(far.Hp, Is.EqualTo(far.MaxHp));
            SurvivorEnemy edge = Tank(sim, 10.3f, 0f); sim.Step(default); Assert.That(edge.MaxHp - edge.Hp, Is.EqualTo(20f).Within(1e-3f));
        }

        [Test]
        public void Stone_FewerEnemiesThanCount_HitsOnlyThose_AndDoesNotRepeatOne()
        {
            SurvivorSim sim = ClassSim("mage", 72, 5, 33); SurvivorEnemy only = Tank(sim, 4f, 0f); sim.Step(default);
            Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 72).Extra, Is.EqualTo(1)); Assert.That(only.MaxHp - only.Hp, Is.EqualTo(100f).Within(1e-3f), "one enemy is hit once, not three times");
        }

        [Test]
        public void Stone_KillsAnEnemyAndTheBossTakesTheSameFixedDamage()
        {
            SurvivorSim sim = ClassSim("mage", 72, 5, 34); SurvivorEnemy weak = sim.SpawnEnemyForTests(0, new Vec2(3f, 0f)); sim.Step(default);
            Assert.That(weak.Active, Is.False); Assert.That(sim.Kills, Is.EqualTo(1));
            SurvivorSim bossSim = ClassSim("mage", 72, 2, 35); SurvivorEnemy boss = bossSim.SpawnBossForTests(new Vec2(4f, 0f)); float before = boss.Hp; bossSim.Step(default);
            Assert.That(before - boss.Hp, Is.EqualTo(40f).Within(1e-3f));
        }

        // ---- duplicator on the new weapons ----

        [Test]
        public void Duplicator_AddsOneToEachNewWeaponsCount()
        {
            Assert.That(Count(ClassSim("mage", 70, 1, 40), 70, 0), Is.EqualTo(3)); Assert.That(Count(ClassSim("mage", 70, 1, 40), 70, 1), Is.EqualTo(4));
            Assert.That(Count(ClassSim("archer", 71, 1, 41), 71, 0), Is.EqualTo(4)); Assert.That(Count(ClassSim("archer", 71, 1, 41), 71, 2), Is.EqualTo(12));
            Assert.That(Count(ClassSim("mage", 72, 1, 42), 72, 0), Is.EqualTo(1)); Assert.That(Count(ClassSim("mage", 72, 1, 42), 72, 2), Is.EqualTo(3));
            Assert.That(Count(ClassSim("mage", 69, 1, 43), 69, 0), Is.EqualTo(1)); Assert.That(Count(ClassSim("mage", 69, 1, 43), 69, 1), Is.EqualTo(2));
        }

        // ---- pool-added weapons for the new class ----

        [Test]
        public void Mage_FlameCone_SweepsTheFacingArc_AtItsOwnFacing()
        {
            SurvivorSim sim = ClassSim("mage", 26, 1, 50); sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, MathF.PI / 2f);
            SurvivorEnemy ahead = Tank(sim, 0f, 2.5f), behind = Tank(sim, 0f, -2f); sim.Step(default);
            Assert.That(ahead.MaxHp - ahead.Hp, Is.EqualTo(6f).Within(1e-3f)); Assert.That(behind.Hp, Is.EqualTo(behind.MaxHp)); Assert.That(sim.WeaponCooldownForTests(26), Is.EqualTo(0.35f).Within(1e-5f));
        }

        [Test]
        public void Mage_Barrier_AbsorbsAHit_WithTheMagesStats()
        {
            SurvivorConfig config = Cfg("mage"); config.ClassDef.StartingWeapon = 31; SurvivorSim sim = new SurvivorSim(config, 51); SurvivorEnemy source = Tank(sim, 30f, 0f);
            sim.Step(default); Assert.That(sim.BarrierCharges, Is.EqualTo(1)); float hp = sim.Hero.Hp; Assert.That(hp, Is.EqualTo(110f).Within(0.5f));
            sim.DamageHeroForTests(20f, source); Assert.That(sim.Hero.Hp, Is.EqualTo(hp)); Assert.That(sim.BarrierCharges, Is.Zero);
            Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 31).Extra, Is.Zero, "break event");
            sim.DamageHeroForTests(20f, source); Assert.That(sim.Hero.Hp, Is.LessThan(hp), "the next hit lands while the shield recharges");
            Assert.That(sim.WeaponCooldownForTests(31), Is.EqualTo(12f).Within(0.05f));
        }

        [Test]
        public void Mage_PoisonPool_DropsAZoneOnTheGroup()
        {
            SurvivorSim sim = ClassSim("mage", 33, 1, 52); SurvivorEnemy a = Tank(sim, 5f, 0f); Tank(sim, 5.5f, 0.5f); sim.Step(default);
            SurvivorZone zone = sim.Zones[0]; Assert.That(zone.Active, Is.True); Assert.That(zone.Radius, Is.EqualTo(1.8f).Within(1e-4f)); Assert.That(zone.Position.X, Is.EqualTo(5f).Within(0.8f));
            Step(sim, 60); Assert.That(a.MaxHp - a.Hp, Is.GreaterThan(0f));
        }

        [Test]
        public void Archer_Bomb_ThrownAtTheNearestEnemy_ExplodesForFullDamage()
        {
            SurvivorSim sim = ClassSim("archer", 29, 1, 53); SurvivorEnemy target = Tank(sim, 6f, 0f); sim.Step(default);
            SurvivorEvent blast = default; bool found = false;
            for (int tick = 0; tick < 90 && !found; tick++) { sim.Step(default); for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.StrikeLanded && sim.Events[i].Id == 29) { blast = sim.Events[i]; found = true; } }
            Assert.That(found, Is.True); Assert.That(blast.Value, Is.EqualTo(2f).Within(1e-5f)); Assert.That(target.MaxHp - target.Hp, Is.EqualTo(30f).Within(1e-3f));
        }

        [Test]
        public void Archer_Boomerang_FliesOutAndHitsTheTarget()
        {
            SurvivorSim sim = ClassSim("archer", 32, 1, 54); SurvivorEnemy target = Tank(sim, 5f, 0f); sim.Step(default);
            Assert.That(sim.Boomerangs[0].Active, Is.True); Assert.That(sim.Boomerangs[0].Damage, Is.EqualTo(18f).Within(1e-3f)); Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 32).Extra, Is.EqualTo(1));
            Step(sim, 60); Assert.That(target.MaxHp - target.Hp, Is.GreaterThanOrEqualTo(18f - 1e-3f));
        }

        [Test]
        public void Archer_PoisonPool_DropsAZone()
        {
            SurvivorSim sim = ClassSim("archer", 33, 1, 55); Tank(sim, 5f, 0f); sim.Step(default);
            Assert.That(sim.Zones[0].Active, Is.True); Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 33).Value, Is.EqualTo(3.5f).Within(1e-4f));
        }

        // ---- whole kits: determinism, allocation, old rules ----

        [Test]
        public void MageAndArcherKits_RunDeterministically()
        {
            foreach (string id in new[] { "mage", "archer" })
            {
                Assert.That(RunHash(id, 601), Is.EqualTo(RunHash(id, 601)), id); Assert.That(RunHash(id, 601), Is.Not.EqualTo(RunHash(id, 602)), id);
            }
        }

        [Test]
        public void MageAndArcherKits_EveryWeaponActs()
        {
            foreach (string id in new[] { "mage", "archer" })
            {
                SurvivorSim sim = FullKitSim(id, 603); HashSet<int> fired = new HashSet<int>();
                for (int i = 0; i < 1800; i++)
                {
                    sim.Step(new SurvivorInput(i / 20 % 9, 0, 0));
                    for (int e = 0; e < sim.Events.Count; e++) if (sim.Events[e].Type == SurvivorEventType.WeaponFired) fired.Add(sim.Events[e].Id);
                }
                foreach (int index in id == "mage" ? MageKit : ArcherKit) if (index != 31) Assert.That(fired.Contains(index), Is.True, id + " weapon " + index);
            }
        }

        [Test]
        public void MageAndArcherKits_StepAndObservationAllocateNothing()
        {
            foreach (string id in new[] { "mage", "archer" })
            {
                SurvivorSim sim = FullKitSim(id, 604); SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];
                for (int i = 0; i < 900; i++) { sim.Step(new SurvivorInput(i / 20 % 9, 0, 0)); observation.Write(sim, values); }
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 900; i < 2400; i++) sim.Step(new SurvivorInput(i / 20 % 9, 0, 0));
                long stepAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
                before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 100; i++) observation.Write(sim, values); long writeAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(sim.IsEnded, Is.False); Assert.That(stepAllocated, Is.EqualTo(0L), id + " Step"); Assert.That(writeAllocated, Is.EqualTo(0L), id + " Write");
            }
        }

        [Test]
        public void NotOwned_TheRunIsBitIdenticalToTheOldRules()
        {
            foreach (string id in new[] { "mage", "archer" })
            {
                SurvivorConfig now = SurvivorTestHelpers.PreM11Drops(SurvivorTestHelpers.Config()); now.ClassDef = SurvivorDefaults.ForClass(id);
                SurvivorConfig old = SurvivorTestHelpers.Config(); old.ClassDef = SurvivorDefaults.ForClass(id); SurvivorTestHelpers.OldRules(old);
                SurvivorSim a = new SurvivorSim(now, 605), b = new SurvivorSim(old, 605); a.DisablePickupCollectionForTests(); b.DisablePickupCollectionForTests();
                for (int tick = 0; tick < 1500; tick++)
                {
                    SurvivorInput input = new SurvivorInput(tick / 30 % 9, 0, 0); a.Step(input); b.Step(input);
                    Assert.That(SurvivorM5GoldenTests.StateHash(a), Is.EqualTo(SurvivorM5GoldenTests.StateHash(b)), id + " tick " + tick);
                }
            }
        }

        // ---- helpers ----

        private static SurvivorConfig Cfg(string cls)
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef = SurvivorDefaults.ForClass(cls); config.ClassDef.CritChance = 0f; config.OpeningRing = 0; config.MapHalfSize = 50f; return config;
        }

        /// <summary>A hero of class <paramref name="cls"/> whose only weapon is <paramref name="item"/> at <paramref name="level"/>; no opening ring, no crits, invulnerable.</summary>
        private static SurvivorSim ClassSim(string cls, int item, int level, int seed)
        {
            SurvivorConfig config = Cfg(cls); config.ClassDef.StartingWeapon = item;
            SurvivorSim sim = new SurvivorSim(config, seed); if (level != 1) sim.GiveItemForTests(item, level);
            sim.SetHeroInvulnerableForTests(); return sim;
        }

        /// <summary>A stunned walker with a huge HP pool so damage can be read exactly.</summary>
        private static SurvivorEnemy Tank(SurvivorSim sim, float x, float y) { SurvivorEnemy e = sim.SpawnEnemyForTests(0, new Vec2(x, y)); e.MaxHp = 1e6f; e.Hp = 1e6f; e.StunRemaining = 1000f; return e; }

        private static List<SurvivorProjectile> Shots(SurvivorSim sim, int source)
        {
            List<SurvivorProjectile> list = new List<SurvivorProjectile>();
            for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == source) list.Add(sim.Projectiles[i]);
            return list;
        }

        private static void Step(SurvivorSim sim, int ticks) { for (int i = 0; i < ticks; i++) sim.Step(default); }

        private static int CountEvents(SurvivorSim sim, SurvivorEventType type, int id) { int n = 0; for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == type && sim.Events[i].Id == id) n++; return n; }

        /// <summary>First-volley size of <paramref name="weapon"/> with a duplicator of <paramref name="amountLevel"/> and plenty of tanks around (projectile count, or blasts / hits for Strike and Stone).</summary>
        private static int Count(SurvivorSim sim, int weapon, int amountLevel)
        {
            if (amountLevel > 0) sim.GiveItemForTests(35, amountLevel);
            for (int i = 0; i < 6; i++) Tank(sim, 3f + i * 0.4f, (i - 3) * 1.6f);
            sim.Step(default);
            return SurvivorCatalog.Get(weapon).Pattern == WeaponPattern.Strike || SurvivorCatalog.Get(weapon).Pattern == WeaponPattern.Stone ? CountEvents(sim, SurvivorEventType.StrikeLanded, weapon) : Shots(sim, weapon).Count;
        }

        private static SurvivorSim FullKitSim(string cls, int seed)
        {
            SurvivorConfig config = Cfg(cls); config.MapHalfSize = 15f; config.ClassDef.MaxHp = 1e7f; config.ClassDef.StartingWeapon = cls == "mage" ? 69 : 70; SurvivorSim sim = new SurvivorSim(config, seed);
            sim.SetEnemiesInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            foreach (int index in cls == "mage" ? MageKit : ArcherKit) sim.GiveItemForTests(index, 5);
            foreach (int index in new[] { 34, 35, 36, 37 }) sim.GiveItemForTests(index, index == 35 ? 2 : 3);
            for (int i = 0; i < 40; i++) sim.SpawnEnemyForTests(i % 4, Vec2.FromAngle(i * 0.7f) * (2f + i % 7));
            return sim;
        }

        private static long RunHash(string cls, int seed)
        {
            SurvivorConfig config = Cfg(cls); config.OpeningRing = 6; SurvivorSim sim = new SurvivorSim(config, seed); long hash = 17;
            foreach (int index in cls == "mage" ? MageKit : ArcherKit) sim.GiveItemForTests(index, 4);
            for (int tick = 0; tick < 3600 && !sim.IsEnded; tick++)
            {
                SurvivorInput input = sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1 + tick % 3) : new SurvivorInput((tick / 40) % 9, (tick / 90) % 4, 0);
                sim.Step(input); hash = hash * 31 + SurvivorM5GoldenTests.StateHash(sim);
                for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active) hash = hash * 31 + BitConverter.SingleToInt32Bits(sim.Projectiles[i].Position.X);
            }
            Assert.That(sim.Kills, Is.GreaterThan(0)); return hash;
        }

        private static SurvivorEvent Find(IReadOnlyList<SurvivorEvent> events, SurvivorEventType type, int id = int.MinValue)
        {
            for (int i = 0; i < events.Count; i++) if (events[i].Type == type && (id == int.MinValue || events[i].Id == id)) return events[i];
            Assert.Fail("Missing event " + type); return default;
        }
    }
}
