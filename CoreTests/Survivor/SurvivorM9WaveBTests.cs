using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>M9 wave 1b (T-038): Warrior weapons 31..33 (barrier, boomerang, poison pool) and passives 34..37 (duration, duplicator, spiked armor, omni box).</summary>
    public sealed class SurvivorM9WaveBTests
    {
        private static readonly int[] NewWeapons = { 31, 32, 33 };
        private static readonly int[] NewPassives = { 34, 35, 36, 37 };

        // ---- catalog and pools ----

        [Test]
        public void Catalog_NewRowsMatchTheTaskTable()
        {
            ItemDef barrier = SurvivorCatalog.Get(31), boomerang = SurvivorCatalog.Get(32), pool = SurvivorCatalog.Get(33);
            Assert.That(barrier.Id, Is.EqualTo("barrier")); Assert.That(barrier.Pattern, Is.EqualTo(WeaponPattern.Barrier)); Assert.That(barrier.BaseCooldown, Is.EqualTo(12f)); Assert.That(barrier.CooldownPerLevel, Is.EqualTo(-1f)); Assert.That(barrier.CountByLevel, Is.EqualTo(new[] { 1, 1, 2, 2, 3 }));
            Assert.That(boomerang.Id, Is.EqualTo("boomerang")); Assert.That(boomerang.Pattern, Is.EqualTo(WeaponPattern.Boomerang)); Assert.That(boomerang.BaseDamage, Is.EqualTo(18f)); Assert.That(boomerang.DamagePerLevel, Is.EqualTo(6f)); Assert.That(boomerang.ProjectileSpeed, Is.EqualTo(11f)); Assert.That(boomerang.ProjectileRadius, Is.EqualTo(0.45f)); Assert.That(boomerang.ProjectileRange, Is.EqualTo(8f)); Assert.That(boomerang.BaseRange, Is.EqualTo(10f)); Assert.That(boomerang.BaseCooldown, Is.EqualTo(1.8f)); Assert.That(boomerang.CountByLevel, Is.EqualTo(new[] { 1, 1, 2, 2, 3 }));
            Assert.That(pool.Id, Is.EqualTo("poison-pool")); Assert.That(pool.Pattern, Is.EqualTo(WeaponPattern.Zone)); Assert.That(pool.BaseDamage, Is.EqualTo(5f)); Assert.That(pool.DamagePerLevel, Is.EqualTo(2f)); Assert.That(pool.BaseCooldown, Is.EqualTo(4f)); Assert.That(pool.Width, Is.EqualTo(1.8f)); Assert.That(pool.Duration, Is.EqualTo(3.5f)); Assert.That(pool.HitInterval, Is.EqualTo(0.5f)); Assert.That(pool.BaseRange, Is.EqualTo(8f));
            foreach (int index in NewWeapons) { ItemDef def = SurvivorCatalog.Get(index); Assert.That(def.Kind, Is.EqualTo(ItemKind.Weapon)); Assert.That(def.MaxLevel, Is.EqualTo(5)); Assert.That(def.CatalogIndex, Is.EqualTo(index)); Assert.That(def.Name, Is.Not.Empty); }
            string[] ids = { "duration-charm", "duplicator", "spiked-armor", "omni-box" };
            for (int i = 0; i < 4; i++) { ItemDef def = SurvivorCatalog.Get(34 + i); Assert.That(def.Id, Is.EqualTo(ids[i])); Assert.That(def.Kind, Is.EqualTo(ItemKind.Passive)); Assert.That(def.PerLevel, Is.GreaterThan(0f)); Assert.That(def.MaxLevel, Is.EqualTo(i == 1 ? 2 : 5)); }
            Assert.That(SurvivorCatalog.Get(38), Is.Null); Assert.That(SurvivorCatalog.Get(62).Kind, Is.EqualTo(ItemKind.Filler));
            Assert.That(SurvivorObservation.Size, Is.EqualTo(2592)); Assert.That(SurvivorObservation.SchemaVersion, Is.EqualTo(5)); Assert.That(SurvivorInput.SkillBranchSize, Is.EqualTo(7));
        }

        [Test]
        public void Pools_WarriorGetsTheWeapons_EveryClassGetsThePassives_AndOnlyOneBarrierExists()
        {
            foreach (int weapon in NewWeapons) Assert.That(Array.IndexOf(SurvivorDefaults.Warrior().WeaponPool, weapon) >= 0, Is.True, "warrior " + weapon);
            // T-044 added barrier + poison pool to the Mage and boomerang + poison pool to the Archer (see SurvivorM9MageArcherTests).
            Assert.That(Array.IndexOf(SurvivorDefaults.Mage().WeaponPool, 32), Is.LessThan(0)); Assert.That(Array.IndexOf(SurvivorDefaults.Archer().WeaponPool, 31), Is.LessThan(0));
            foreach (SurvivorClassDef kit in new[] { SurvivorDefaults.Warrior(), SurvivorDefaults.Mage(), SurvivorDefaults.Archer() })
                foreach (int passive in NewPassives) Assert.That(Array.IndexOf(kit.PassivePool, passive) >= 0, Is.True, kit.Id + " " + passive);
            int barriers = 0; foreach (int index in SurvivorDefaults.Warrior().WeaponPool) if (SurvivorCatalog.Get(index).Pattern == WeaponPattern.Barrier) barriers++;
            Assert.That(barriers, Is.EqualTo(1));
        }

        [Test]
        public void OldRules_ExcludeEverythingNew()
        {
            SurvivorConfig old = SurvivorTestHelpers.OldConfig();
            foreach (int index in NewWeapons) Assert.That(Array.IndexOf(old.ClassDef.WeaponPool, index), Is.LessThan(0));
            foreach (int index in NewPassives) Assert.That(Array.IndexOf(old.ClassDef.PassivePool, index), Is.LessThan(0));
        }

        [Test]
        public void Offers_EveryNewItemCanAppear_AndDuplicatorStopsAtLevelTwo()
        {
            HashSet<int> seen = new HashSet<int>();
            for (int seed = 0; seed < 500; seed++)
            {
                SurvivorSim sim = NewSim(seed); sim.GiveXpForTests(5f); sim.Step(default);
                for (int i = 0; i < sim.OfferCount; i++) seen.Add(sim.GetOffer(i).CatalogIndex);
            }
            foreach (int index in NewWeapons) Assert.That(seen.Contains(index), Is.True, "weapon " + index);
            foreach (int index in NewPassives) Assert.That(seen.Contains(index), Is.True, "passive " + index);
            for (int seed = 0; seed < 100; seed++)
            {
                SurvivorSim sim = NewSim(seed); sim.GiveItemForTests(35, 2); sim.GiveXpForTests(5f); sim.Step(default);
                for (int i = 0; i < sim.OfferCount; i++) Assert.That(sim.GetOffer(i).CatalogIndex, Is.Not.EqualTo(35), "a maxed duplicator is not offered again");
            }
            SurvivorSim one = NewSim(1); one.GiveItemForTests(35, 1); one.GiveXpForTests(5f);
            for (int tick = 0; tick < 3 && !one.IsAwaitingPick; tick++) one.Step(default);
            for (int i = 0; i < one.OfferCount; i++) if (one.GetOffer(i).CatalogIndex == 35) Assert.That(one.GetOffer(i).NextLevel, Is.EqualTo(2));
        }

        [Test]
        public void SixPlusSixSlotsStillHold_WithTheNewItems()
        {
            SurvivorSim sim = NewSim(3); sim.SetHeroInvulnerableForTests(); Rng picks = new Rng(9); int full = 0;
            for (int round = 0; round < 300; round++)
            {
                sim.GiveXpForTests(XpCurve.Required(sim.Level)); sim.Step(default);
                if (!sim.IsAwaitingPick) continue;
                for (int i = 0; i < sim.OfferCount; i++)
                {
                    int index = sim.GetOffer(i).CatalogIndex; ItemDef def = SurvivorCatalog.Get(index);
                    if (def.Kind == ItemKind.Filler || sim.Inventory.Level(index) > 0) continue;
                    if (def.Kind == ItemKind.Weapon) Assert.That(sim.Inventory.WeaponCount, Is.LessThan(6));
                    if (def.Kind == ItemKind.Passive) Assert.That(sim.Inventory.PassiveCount, Is.LessThan(6));
                }
                sim.Step(new SurvivorInput(0, 0, 1 + picks.NextInt(sim.OfferCount)));
                Assert.That(sim.Inventory.WeaponCount, Is.LessThanOrEqualTo(6)); Assert.That(sim.Inventory.PassiveCount, Is.LessThanOrEqualTo(6));
                if (sim.Inventory.WeaponCount == 6 && sim.Inventory.PassiveCount == 6) full++;
            }
            Assert.That(full, Is.GreaterThan(0));
        }

        // ---- barrier ----

        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        public void Barrier_AbsorbsTheNextNHits_ThenBreaksWithAnEvent(int level, int hits)
        {
            SurvivorSim sim = WeaponSim(31, level, 100); SurvivorEnemy source = Brute(sim, 30f, 0f); float hp = sim.Hero.Hp;
            for (int hit = 0; hit < hits; hit++)
            {
                sim.DamageHeroForTests(20f, source);
                Assert.That(sim.Hero.Hp, Is.EqualTo(hp), "hit " + hit + " is absorbed"); Assert.That(sim.DamageTaken, Is.Zero);
                for (int i = 0; i < sim.Events.Count; i++) Assert.That(sim.Events[i].Type, Is.Not.EqualTo(SurvivorEventType.HeroDamaged));
                if (hit < hits - 1) Assert.That(sim.BarrierCharges, Is.EqualTo(hits - 1 - hit));
            }
            SurvivorEvent broke = Find(sim.Events, SurvivorEventType.WeaponFired, 31); Assert.That(broke.Extra, Is.Zero, "break event: no charges left");
            Assert.That(sim.BarrierCharges, Is.Zero);
            sim.DamageHeroForTests(20f, source); Assert.That(sim.Hero.Hp, Is.EqualTo(hp - 20f), "the next hit goes through");
        }

        [TestCase(1, 12f)]
        [TestCase(3, 10f)]
        [TestCase(5, 8f)]
        public void Barrier_ReturnsAfterItsRecharge_WithAnEvent(int level, float seconds)
        {
            SurvivorSim sim = WeaponSim(31, level, 101); SurvivorEnemy source = Brute(sim, 30f, 0f);
            for (int i = 0; i < 3; i++) sim.DamageHeroForTests(1f, source);
            Assert.That(sim.BarrierCharges, Is.Zero); Assert.That(sim.WeaponCooldownForTests(31), Is.EqualTo(seconds).Within(1e-4f));
            sim.SetHeroInvulnerableForTests(); int returned = -1; int expectedCharges = level <= 2 ? 1 : level <= 4 ? 2 : 3;
            for (int tick = 1; tick <= 900 && returned < 0; tick++)
            {
                sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 31) { returned = tick; Assert.That(sim.Events[i].Extra, Is.EqualTo(expectedCharges)); }
            }
            Assert.That(returned, Is.InRange((int)(seconds * 60) - 1, (int)(seconds * 60) + 2)); Assert.That(sim.BarrierCharges, Is.EqualTo(expectedCharges));
        }

        [Test]
        public void Barrier_AbsorbedHitDoesNotTriggerRetaliateOrReflect()
        {
            SurvivorConfig config = NoCrit(); config.ClassDef.StartingWeapon = 31; SurvivorSim sim = new SurvivorSim(config, 102);
            sim.GiveItemForTests(30, 1); sim.GiveItemForTests(36, 5); sim.SetWeaponCooldownForTests(0, 1000f);
            SurvivorEnemy touching = sim.SpawnEnemyForTests(0, new Vec2(0.9f, 0f)); touching.StunRemaining = 1000f; SurvivorEnemy near = Brute(sim, 0f, 2f);
            sim.DamageHeroForTests(30f, touching); sim.Step(default);
            Assert.That(near.Hp, Is.EqualTo(near.MaxHp), "no retaliate ring"); Assert.That(touching.Hp, Is.EqualTo(touching.MaxHp), "no reflect"); Assert.That(sim.DamageTaken, Is.Zero);
            sim.DamageHeroForTests(30f, touching); sim.Step(default);
            Assert.That(near.Hp, Is.LessThan(near.MaxHp), "the unshielded hit does retaliate"); Assert.That(touching.Hp, Is.LessThan(touching.MaxHp));
        }

        [Test]
        public void Barrier_AbsorbsEnemyProjectiles_AndExplosions()
        {
            SurvivorSim sim = WeaponSim(31, 3, 103); float hp = sim.Hero.Hp;
            sim.SpawnEnemyProjectileForTests(sim.Hero.Position, Vec2.Zero, 0.3f, 10f, 5f); SurvivorTestHelpers.Step(sim, 2);
            Assert.That(sim.Hero.Hp, Is.EqualTo(hp)); Assert.That(sim.BarrierCharges, Is.EqualTo(1));
            SurvivorEnemy exploder = Brute(sim, 30f, 0f); sim.DamageHeroForTests(30f, exploder, false);
            Assert.That(sim.Hero.Hp, Is.EqualTo(hp)); Assert.That(sim.BarrierCharges, Is.Zero);
            sim.SpawnEnemyProjectileForTests(sim.Hero.Position, Vec2.Zero, 0.3f, 10f, 5f); SurvivorTestHelpers.Step(sim, 2);
            Assert.That(sim.Hero.Hp, Is.LessThan(hp));
        }

        [Test]
        public void Barrier_NeedsTheWeapon_AndRaisingItsLevelAddsCharges()
        {
            SurvivorSim plain = NewSim(104); SurvivorEnemy source = Brute(plain, 30f, 0f); plain.DamageHeroForTests(20f, source); Assert.That(plain.Hero.Hp, Is.EqualTo(plain.Hero.MaxHp - 20f));
            Assert.That(plain.BarrierCharges, Is.Zero);
            SurvivorSim sim = WeaponSim(31, 1, 105); sim.Step(default); Assert.That(sim.BarrierCharges, Is.EqualTo(1));
            sim.GiveItemForTests(31, 3); sim.Step(default); Assert.That(sim.BarrierCharges, Is.EqualTo(2), "level 3 holds two hits");
            sim.GiveItemForTests(31, 5); sim.Step(default); Assert.That(sim.BarrierCharges, Is.EqualTo(3));
            SurvivorEnemy far = Brute(sim, 30f, 0f); for (int i = 0; i < 3; i++) sim.DamageHeroForTests(1f, far);
            sim.GiveItemForTests(31, 4); sim.Step(default); Assert.That(sim.BarrierCharges, Is.Zero, "a broken shield is not refilled by a level-up");
        }

        [Test]
        public void Barrier_ParryComesFirst_AndAParriedSwingKeepsTheCharge()
        {
            SurvivorSim sim = WeaponSim(31, 1, 106); sim.Step(default); Assert.That(sim.BarrierCharges, Is.EqualTo(1));
            SurvivorEnemy brute = sim.SpawnEnemyForTests(2, new Vec2(1.2f, 0f));
            sim.Step(new SurvivorInput(0, 2, 0)); // start blocking
            sim.DamageHeroForTests(25f, brute, false);
            Assert.That(Find(sim.Events, SurvivorEventType.Parry).Type, Is.EqualTo(SurvivorEventType.Parry)); Assert.That(sim.BarrierCharges, Is.EqualTo(1));
        }

        // ---- boomerang ----

        [Test]
        public void Boomerang_HitsOnTheWayOutAndOnTheWayBack_EachEnemyOncePerDirection_ThenComesHome()
        {
            SurvivorSim sim = WeaponSim(32, 1, 110); SurvivorEnemy target = Brute(sim, 6f, 0f);
            int outTick = -1, backTick = -1, homeTick = -1; float last = target.Hp; bool sawReturning = false;
            for (int tick = 1; tick <= 100; tick++)
            {
                sim.Step(default); SurvivorBoomerang b = sim.Boomerangs[0];
                if (b.Returning) sawReturning = true;
                if (target.Hp < last) { Assert.That(last - target.Hp, Is.EqualTo(18f).Within(1e-3f)); last = target.Hp; if (!sawReturning) outTick = tick; else backTick = tick; }
                if (homeTick < 0 && sawReturning && !b.Active) homeTick = tick;
            }
            Assert.That(target.MaxHp - target.Hp, Is.EqualTo(36f).Within(1e-3f), "exactly two hits: once out, once back"); Assert.That(outTick, Is.GreaterThan(0)); Assert.That(backTick, Is.GreaterThan(outTick));
            Assert.That(homeTick, Is.InRange(60, 100), "caught by the hero after the round trip"); Assert.That(sim.WeaponCooldownForTests(32), Is.GreaterThan(0f));
        }

        [Test]
        public void Boomerang_TurnsAtMaxRange_AndFliesBackToTheHero()
        {
            SurvivorSim sim = WeaponSim(32, 1, 111); Brute(sim, 9.5f, 0f); sim.SetEnemiesInvulnerableForTests();
            float farthest = 0f; bool turned = false; int home = -1;
            for (int tick = 1; tick <= 140; tick++)
            {
                sim.Step(default); SurvivorBoomerang b = sim.Boomerangs[0];
                if (b.Active) farthest = MathF.Max(farthest, b.Position.X);
                if (b.Returning) turned = true;
                if (turned && !b.Active && home < 0) home = tick;
            }
            Assert.That(turned, Is.True); Assert.That(farthest, Is.EqualTo(8f).Within(0.3f), "max range 8 m"); Assert.That(home, Is.GreaterThan(0));
        }

        [Test]
        public void Boomerang_TurnsAtTheMapEdge()
        {
            SurvivorConfig config = NoCrit(); config.ClassDef.StartingWeapon = 32; config.MapHalfSize = 11f; SurvivorSim sim = new SurvivorSim(config, 112);
            sim.SetHeroStateForTests(new Vec2(8f, 0f), Vec2.Zero, 0f); Brute(sim, 9.5f, 0f); sim.SetEnemiesInvulnerableForTests();
            float farthest = 0f; bool turned = false;
            for (int tick = 0; tick < 30; tick++) { sim.Step(default); SurvivorBoomerang b = sim.Boomerangs[0]; if (b.Active) farthest = MathF.Max(farthest, b.Position.X); if (b.Returning) turned = true; }
            Assert.That(turned, Is.True); Assert.That(farthest, Is.LessThan(11.5f), "turned before leaving the map by more than a step");
        }

        [Test]
        public void Boomerang_TurnsAtTheFirstObstacle()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                SurvivorConfig config = NoCrit(); config.ClassDef.StartingWeapon = 32; config.ObstacleCount = 1; config.ObstacleClearRadius = 0f;
                SurvivorSim sim = new SurvivorSim(config, 113 + seed); SurvivorObstacle obstacle = null;
                for (int i = 0; i < sim.Obstacles.Count; i++) if (sim.Obstacles[i].Active) obstacle = sim.Obstacles[i];
                if (obstacle == null || MathF.Abs(obstacle.Position.X) > 30f || MathF.Abs(obstacle.Position.Y) > 30f) continue;
                float side = obstacle.Position.X > 0f ? -1f : 1f;
                sim.SetHeroStateForTests(obstacle.Position + new Vec2(side * 5f, 0f), Vec2.Zero, side > 0f ? 0f : MathF.PI);
                sim.SpawnEnemyForTests(2, obstacle.Position - new Vec2(side * (obstacle.Radius + 1.2f), 0f)).StunRemaining = 1000f;
                sim.SetEnemiesInvulnerableForTests(); bool turned = false; float reach = 0f;
                for (int tick = 0; tick < 40; tick++)
                {
                    sim.Step(default); SurvivorBoomerang b = sim.Boomerangs[0]; if (!b.Active) continue;
                    reach = MathF.Max(reach, MathF.Abs(b.Position.X - sim.Hero.Position.X)); if (b.Returning) turned = true;
                }
                Assert.That(turned, Is.True); Assert.That(reach, Is.LessThan(5f - obstacle.Radius + 1f), "it never got past the obstacle");
                return;
            }
            Assert.Inconclusive("no seed placed a usable obstacle");
        }

        [Test]
        public void Boomerang_DoesNotFlyWithoutATargetWithinTenMetres()
        {
            SurvivorSim sim = WeaponSim(32, 1, 114); Brute(sim, 12f, 0f); sim.Step(default);
            Assert.That(sim.WeaponCooldownForTests(32), Is.Zero); for (int i = 0; i < sim.Boomerangs.Count; i++) Assert.That(sim.Boomerangs[i].Active, Is.False);
        }

        [TestCase(1, 1, 18f)]
        [TestCase(2, 1, 24f)]
        [TestCase(3, 2, 30f)]
        [TestCase(4, 2, 36f)]
        [TestCase(5, 3, 42f)]
        public void Boomerang_CountAndDamageByLevel(int level, int count, float damage)
        {
            SurvivorSim sim = WeaponSim(32, level, 115); Brute(sim, 5f, 0f); sim.Step(default);
            int active = 0; for (int i = 0; i < sim.Boomerangs.Count; i++) if (sim.Boomerangs[i].Active) { active++; Assert.That(sim.Boomerangs[i].Damage, Is.EqualTo(damage).Within(1e-3f)); Assert.That(sim.Boomerangs[i].Radius, Is.EqualTo(0.45f).Within(1e-5f)); }
            Assert.That(active, Is.EqualTo(count)); Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 32).Extra, Is.EqualTo(count)); Assert.That(sim.WeaponCooldownForTests(32), Is.EqualTo(1.8f).Within(1e-5f));
        }

        [Test]
        public void Boomerang_PoolFull_SkipsTheVolleySafely()
        {
            SurvivorSim sim = WeaponSim(32, 5, 116); sim.GiveItemForTests(35, 2); sim.SetHeroInvulnerableForTests(); Brute(sim, 5f, 0f); sim.SetEnemiesInvulnerableForTests(); int maxActive = 0;
            for (int tick = 0; tick < 120; tick++)
            {
                sim.SetWeaponCooldownForTests(32, 0f); sim.Step(default); int active = 0;
                for (int i = 0; i < sim.Boomerangs.Count; i++) if (sim.Boomerangs[i].Active) active++;
                maxActive = Math.Max(maxActive, active); Assert.That(active, Is.LessThanOrEqualTo(SurvivorSim.BoomerangCapacity));
            }
            Assert.That(maxActive, Is.EqualTo(6), "the pool filled and stayed within its size"); Assert.That(sim.Boomerangs.Count, Is.EqualTo(6));
        }

        // ---- poison pool ----

        [Test]
        public void PoisonPool_DropsOnTheDensestGroupWithinEightMetres()
        {
            SurvivorSim sim = WeaponSim(33, 1, 120);
            Vec2[] cluster = { new Vec2(-6f, 2f), new Vec2(-6.8f, 2.5f), new Vec2(-5.4f, 1.4f), new Vec2(-6.2f, 3.1f) };
            Brute(sim, 2f, -5f); Brute(sim, 7f, 4f); Brute(sim, 20f, 0f);
            foreach (Vec2 p in cluster) Brute(sim, p.X, p.Y);
            sim.Step(default); SurvivorZone zone = sim.Zones[0];
            Assert.That(zone.Active, Is.True); Assert.That(zone.Radius, Is.EqualTo(1.8f).Within(1e-5f)); Assert.That(zone.Remaining, Is.InRange(3.4f, 3.5f));
            Assert.That(Vec2.Distance(zone.Position, new Vec2(-6.1f, 2.3f)), Is.LessThan(1.5f), "on the cluster, not on the loners");
            Assert.That(sim.WeaponCooldownForTests(33), Is.EqualTo(4f).Within(1e-5f));
            SurvivorEvent fired = Find(sim.Events, SurvivorEventType.WeaponFired, 33); Assert.That(Vec2.Distance(fired.Point, zone.Position), Is.Zero);
        }

        [Test]
        public void PoisonPool_WithNobodyInRange_FallsBackToARandomPointThreeMetresAway()
        {
            HashSet<int> angles = new HashSet<int>();
            for (int seed = 0; seed < 8; seed++)
            {
                SurvivorSim sim = WeaponSim(33, 1, 121 + seed); Brute(sim, 25f, 0f); sim.Step(default); SurvivorZone zone = sim.Zones[0];
                Assert.That(zone.Active, Is.True); Assert.That(Vec2.Distance(zone.Position, sim.Hero.Position), Is.EqualTo(3f).Within(1e-3f));
                angles.Add((int)(MathF.Atan2(zone.Position.Y, zone.Position.X) * 100f));
            }
            Assert.That(angles.Count, Is.GreaterThan(4), "the angle comes from the seeded rng");
        }

        [Test]
        public void PoisonPool_HurtsEveryoneInsideEveryHalfSecond_ForThreeAndAHalfSeconds()
        {
            SurvivorSim sim = WeaponSim(33, 1, 130); SurvivorEnemy a = Brute(sim, 5f, 0f), b = Brute(sim, 5.6f, 0.5f), outside = Brute(sim, 5f, 4f);
            a.MaxHp = a.Hp = 1000f; b.MaxHp = b.Hp = 1000f; int ticks = 0, activeTicks = 0; float lastA = a.Hp;
            for (int tick = 1; tick <= 300; tick++)
            {
                sim.Step(default); sim.SetWeaponCooldownForTests(33, 1000f);
                if (sim.Zones[0].Active) activeTicks++;
                if (a.Hp < lastA) { ticks++; Assert.That(lastA - a.Hp, Is.EqualTo(5f).Within(1e-3f)); lastA = a.Hp; }
            }
            Assert.That(ticks, Is.EqualTo(7), "7 ticks over 3.5 s"); Assert.That(a.MaxHp - a.Hp, Is.EqualTo(35f).Within(1e-3f)); Assert.That(b.MaxHp - b.Hp, Is.EqualTo(35f).Within(1e-3f));
            Assert.That(outside.Hp, Is.EqualTo(outside.MaxHp)); Assert.That(activeTicks, Is.InRange(208, 212), "gone after 3.5 s");
        }

        [Test]
        public void PoisonPool_CooldownRedropsAFreshZone()
        {
            SurvivorSim sim = WeaponSim(33, 1, 131); Brute(sim, 4f, 0f).MaxHp = 5000f; int drops = 0;
            for (int tick = 0; tick < 600; tick++) { sim.Step(default); for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 33) drops++; }
            Assert.That(drops, Is.EqualTo(3), "one drop every 4 s over 10 s (0, 4, 8)");
        }

        [TestCase(1, 5f)]
        [TestCase(3, 9f)]
        [TestCase(5, 13f)]
        public void PoisonPool_DamagePerTickGrowsByTwoPerLevel(int level, float damage)
        {
            SurvivorSim sim = WeaponSim(33, level, 132); SurvivorEnemy target = Brute(sim, 4f, 0f); target.MaxHp = target.Hp = 1000f;
            for (int tick = 0; tick < 40 && target.Hp >= 1000f; tick++) sim.Step(default);
            Assert.That(1000f - target.Hp, Is.EqualTo(damage).Within(1e-3f));
        }

        [Test]
        public void PoisonPool_AtMostThreeZonesAtOnce_AndAFullPoolSkipsTheDrop()
        {
            SurvivorSim sim = WeaponSim(33, 1, 133); Brute(sim, 4f, 0f).MaxHp = 5000f; int drops = 0;
            for (int tick = 0; tick < 12; tick++)
            {
                sim.SetWeaponCooldownForTests(33, 0f); sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 33) drops++;
            }
            int alive = 0; for (int i = 0; i < sim.Zones.Count; i++) if (sim.Zones[i].Active) alive++;
            Assert.That(alive, Is.EqualTo(3)); Assert.That(drops, Is.EqualTo(3)); Assert.That(sim.Zones.Count, Is.EqualTo(SurvivorSim.ZoneCapacity));
            Assert.That(sim.WeaponCooldownForTests(33), Is.Zero, "a skipped drop does not spend the cooldown");
        }

        [Test]
        public void PoisonPool_RadiusFollowsArea_AndDurationFollowsTheDurationStat()
        {
            SurvivorSim sim = WeaponSim(33, 1, 134); sim.GiveItemForTests(11, 5); sim.GiveItemForTests(34, 5); Brute(sim, 4f, 0f); sim.Step(default); SurvivorZone zone = sim.Zones[0];
            Assert.That(zone.Radius, Is.EqualTo(1.8f * 1.4f).Within(1e-4f)); Assert.That(zone.Duration, Is.EqualTo(3.5f * 1.5f).Within(1e-4f));
        }

        // ---- duration charm ----

        [TestCase(1)]
        [TestCase(5)]
        public void DurationCharm_RaisesTheDurationStat(int level)
        {
            SurvivorSim plain = NewSim(140), charmed = NewSim(140); charmed.GiveItemForTests(34, level);
            Assert.That(plain.DerivedStats.DurationMul, Is.EqualTo(1f)); Assert.That(charmed.DerivedStats.DurationMul, Is.EqualTo(1f + 0.1f * level).Within(1e-5f));
            Assert.That(charmed.DerivedStats.AreaMul, Is.EqualTo(plain.DerivedStats.AreaMul)); Assert.That(charmed.DerivedStats.Might, Is.EqualTo(plain.DerivedStats.Might));
        }

        [Test]
        public void DurationCharm_LengthensTheOrbit_ButNotBarrierRecharge_OrBoomerangFlight()
        {
            Assert.That(OrbitTicks(0), Is.InRange(238, 243)); Assert.That(OrbitTicks(5), Is.InRange(358, 363), "4 s becomes 6 s at +50%");
            foreach (int level in new[] { 0, 5 })
            {
                SurvivorSim sim = WeaponSim(31, 1, 141); if (level > 0) sim.GiveItemForTests(34, level); if (level > 0) sim.GiveItemForTests(37, level);
                sim.DamageHeroForTests(1f, Brute(sim, 30f, 0f)); Assert.That(sim.WeaponCooldownForTests(31), Is.EqualTo(12f).Within(1e-4f), "recharge ignores duration");
            }
            Assert.That(BoomerangHomeTick(0), Is.EqualTo(BoomerangHomeTick(5)), "flight time ignores duration");
        }

        // ---- duplicator ----

        [Test]
        public void Duplicator_AddsOneProjectilePerLevel_ToCountBasedWeapons()
        {
            Assert.That(ShotsOf(3, 0), Is.EqualTo(1)); Assert.That(ShotsOf(3, 1), Is.EqualTo(2)); Assert.That(ShotsOf(3, 2), Is.EqualTo(3));
            Assert.That(SpearsOf(0), Is.EqualTo(1)); Assert.That(SpearsOf(2), Is.EqualTo(3));
            Assert.That(ShotsOf(21, 0), Is.EqualTo(3)); Assert.That(ShotsOf(21, 2), Is.EqualTo(5), "multi-shot fan");
            Assert.That(ShotsOf(20, 2), Is.EqualTo(3), "arrow");
            Assert.That(StrikesOf(0), Is.EqualTo(1)); Assert.That(StrikesOf(2), Is.EqualTo(3));
            Assert.That(OrbitOf(0), Is.EqualTo(1)); Assert.That(OrbitOf(2), Is.EqualTo(3));
            Assert.That(BoomerangsOf(0), Is.EqualTo(1)); Assert.That(BoomerangsOf(2), Is.EqualTo(3));
            Assert.That(ShotsOf(29, 2), Is.EqualTo(1), "bomb is not count based"); Assert.That(ShotsOf(28, 2), Is.EqualTo(3), "heavy hammer is Thrown: count based");
        }

        [Test]
        public void Duplicator_NeverOverflowsAnyBuffer_WithAllSixWeaponsAtLevelFive()
        {
            foreach (int amountLevel in new[] { 2, 8 })
            {
                SurvivorConfig config = NoCrit(); config.ClassDef.StartingWeapon = 1; config.ClassDef.MaxHp = 1e7f; SurvivorSim sim = new SurvivorSim(config, 150);
                foreach (int index in new[] { 1, 2, 3, 21, 18, 32 }) sim.GiveItemForTests(index, 5);
                sim.GiveItemForTests(35, amountLevel); sim.SetEnemiesInvulnerableForTests(); sim.DisablePickupCollectionForTests();
                Assert.That(sim.DerivedStats.Amount, Is.EqualTo(amountLevel));
                for (int i = 0; i < 40; i++) sim.SpawnEnemyForTests(i % 4, Vec2.FromAngle(i * 0.5f) * (2f + i % 6));
                int mostAxes = 0, mostStrikes = 0, mostBoomerangs = 0;
                for (int tick = 0; tick < 1500; tick++)
                {
                    Assert.DoesNotThrow(() => sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : new SurvivorInput(tick / 30 % 9, 0, 0)));
                    mostAxes = Math.Max(mostAxes, sim.OrbitAxeCount);
                    int boomerangs = 0; for (int i = 0; i < sim.Boomerangs.Count; i++) if (sim.Boomerangs[i].Active) boomerangs++;
                    mostBoomerangs = Math.Max(mostBoomerangs, boomerangs);
                    for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 18) mostStrikes = Math.Max(mostStrikes, (int)sim.Events[i].Extra);
                }
                Assert.That(mostAxes, Is.InRange(1, 8)); Assert.That(mostStrikes, Is.InRange(1, 8)); Assert.That(mostBoomerangs, Is.InRange(1, 6));
                if (amountLevel == 8) Assert.That(mostStrikes, Is.EqualTo(8), "lightning 3 + 8 is capped at the 8-slot target buffer");
            }
        }

        [Test]
        public void Duplicator_FanAndOrbitCapAtEight_WhenTheAmountIsHuge()
        {
            SurvivorSim fan = WeaponSim(21, 5, 151); fan.GiveItemForTests(35, 8); for (int i = 0; i < 4; i++) Brute(fan, 3f + i, 0f); fan.Step(default);
            int shots = 0; for (int i = 0; i < fan.Projectiles.Count; i++) if (fan.Projectiles[i].Active && fan.Projectiles[i].SourceIndex == 21) shots++;
            Assert.That(shots, Is.EqualTo(8));
            SurvivorSim orbit = WeaponSim(23, 5, 152); orbit.GiveItemForTests(35, 8); orbit.Step(default); Assert.That(orbit.OrbitAxeCount, Is.EqualTo(8));
            Assert.DoesNotThrow(() => { for (int i = 0; i < 120; i++) orbit.Step(default); Assert.That(orbit.GetOrbitAxePosition(7), Is.Not.EqualTo(Vec2.Zero)); });
        }

        // ---- spiked armor ----

        [TestCase(1)]
        [TestCase(5)]
        public void SpikedArmor_AddsArmor(int level)
        {
            SurvivorSim plain = NewSim(160), armored = NewSim(160); armored.GiveItemForTests(36, level);
            Assert.That(armored.DerivedStats.Armor, Is.EqualTo(plain.DerivedStats.Armor + level).Within(1e-5f)); Assert.That(armored.DerivedStats.ReflectFraction, Is.EqualTo(0.1f * level).Within(1e-5f)); Assert.That(plain.DerivedStats.ReflectFraction, Is.Zero);
            SurvivorEnemy source = Brute(armored, 30f, 0f); float hp = armored.Hero.Hp; armored.DamageHeroForTests(30f, source); Assert.That(hp - armored.Hero.Hp, Is.EqualTo(30f - level).Within(1e-4f));
        }

        [TestCase(1, 2.9f)]
        [TestCase(3, 8.1f)]
        [TestCase(5, 12.5f)]
        public void SpikedArmor_ReflectsAShareOfContactDamage_ToEveryEnemyTouchingTheHero(int level, float reflected)
        {
            SurvivorSim sim = NewSim(161); sim.GiveItemForTests(36, level); sim.GiveItemForTests(8, 5); sim.SetWeaponCooldownForTests(0, 1000f);
            SurvivorEnemy touching = Walker(sim, 0.9f, 0f), second = Walker(sim, -0.9f, 0f), far = Brute(sim, 3f, 0f);
            sim.DamageHeroForTests(30f, touching, true); Assert.That(touching.Hp, Is.EqualTo(touching.MaxHp), "reflection resolves after the enemy phase");
            sim.Step(default);
            Assert.That(touching.MaxHp - touching.Hp, Is.EqualTo(reflected).Within(1e-3f), "10% per level of the damage taken, flat (no Might)"); Assert.That(second.MaxHp - second.Hp, Is.EqualTo(reflected).Within(1e-3f)); Assert.That(far.Hp, Is.EqualTo(far.MaxHp));
        }

        [Test]
        public void SpikedArmor_IgnoresSwingsProjectilesAndTheKillingBlow()
        {
            SurvivorSim sim = NewSim(162); sim.GiveItemForTests(36, 5); sim.SetWeaponCooldownForTests(0, 1000f); SurvivorEnemy touching = Walker(sim, 0.9f, 0f);
            sim.DamageHeroForTests(30f, touching, false); sim.Step(default); Assert.That(touching.Hp, Is.EqualTo(touching.MaxHp), "a melee swing is not a contact attack");
            sim.SpawnEnemyProjectileForTests(sim.Hero.Position, Vec2.Zero, 0.3f, 10f, 5f); SurvivorTestHelpers.Step(sim, 2); Assert.That(touching.Hp, Is.EqualTo(touching.MaxHp));
            sim.SetHeroHpForTests(5f); sim.DamageHeroForTests(50f, touching, true); sim.Step(default);
            Assert.That(sim.EndReason, Is.EqualTo(EndReason.Died)); Assert.That(touching.Hp, Is.EqualTo(touching.MaxHp));
        }

        // ---- omni box ----

        [TestCase(1)]
        [TestCase(5)]
        public void OmniBox_RaisesDamageSpeedDurationAndArea_ByFourPercentPerLevel(int level)
        {
            SurvivorSim plain = NewSim(170), box = NewSim(170); box.GiveItemForTests(37, level); SurvivorDerivedStats b = plain.DerivedStats, o = box.DerivedStats; float gain = 0.04f * level;
            Assert.That(o.Might, Is.EqualTo(b.Might + gain).Within(1e-5f)); Assert.That(o.MoveSpeed, Is.EqualTo(b.MoveSpeed * (1f + gain)).Within(1e-4f));
            Assert.That(o.DurationMul, Is.EqualTo(b.DurationMul + gain).Within(1e-5f)); Assert.That(o.AreaMul, Is.EqualTo(b.AreaMul + gain).Within(1e-5f));
            Assert.That(o.Armor, Is.EqualTo(b.Armor)); Assert.That(o.Regen, Is.EqualTo(b.Regen)); Assert.That(o.MaxHp, Is.EqualTo(b.MaxHp)); Assert.That(o.CooldownMul, Is.EqualTo(b.CooldownMul)); Assert.That(o.Amount, Is.Zero);
        }

        [Test]
        public void OmniBox_StacksWithTheOtherPassivesAndOwnerPoints()
        {
            SurvivorConfig config = NoCrit(); config.Build.Points[(int)StatId.Might] = 5; config.Build.Points[(int)StatId.Area] = 2; SurvivorSim sim = new SurvivorSim(config, 171);
            sim.GiveItemForTests(37, 2); sim.GiveItemForTests(8, 1); sim.GiveItemForTests(11, 3); sim.GiveItemForTests(34, 2);
            Assert.That(sim.DerivedStats.Might, Is.EqualTo(1f + 0.08f + 0.03f * 5 + 0.08f).Within(1e-5f)); Assert.That(sim.DerivedStats.AreaMul, Is.EqualTo(1f + 0.24f + 0.03f * 2 + 0.08f).Within(1e-5f));
            Assert.That(sim.DerivedStats.DurationMul, Is.EqualTo(1f + 0.2f + 0.08f).Within(1e-5f));
        }

        [Test]
        public void NewStats_AddExactlyZeroWithoutTheNewPassives()
        {
            SurvivorSim old = new SurvivorSim(SurvivorTestHelpers.OldConfig(), 172), now = NewSim(172);
            foreach (SurvivorSim sim in new[] { old, now }) { sim.GiveItemForTests(6, 3); sim.GiveItemForTests(7, 2); sim.GiveItemForTests(8, 4); sim.GiveItemForTests(11, 3); sim.GiveItemForTests(12, 2); }
            SurvivorDerivedStats a = old.DerivedStats, b = now.DerivedStats;
            Assert.That(b.Armor, Is.EqualTo(a.Armor)); Assert.That(b.Might, Is.EqualTo(a.Might)); Assert.That(b.AreaMul, Is.EqualTo(a.AreaMul)); Assert.That(b.MoveSpeed, Is.EqualTo(a.MoveSpeed));
            Assert.That(b.DurationMul, Is.EqualTo(1f)); Assert.That(b.Amount, Is.Zero); Assert.That(b.ReflectFraction, Is.Zero);
        }

        // ---- determinism and allocation ----

        [Test]
        public void WaveB_RunsDeterministically()
        {
            Assert.That(RunHash(181), Is.EqualTo(RunHash(181))); Assert.That(RunHash(181), Is.Not.EqualTo(RunHash(182)));
        }

        [Test]
        public void WaveB_StepAndObservationAllocateNothing()
        {
            SurvivorSim sim = FullKitSim(183); SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];
            for (int i = 0; i < 900; i++) { Poke(sim, i); sim.Step(new SurvivorInput(i / 20 % 9, 0, 0)); observation.Write(sim, values); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 900; i < 1800; i++) { Poke(sim, i); sim.Step(new SurvivorInput(i / 20 % 9, 0, 0)); }
            long stepAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 100; i++) observation.Write(sim, values); long writeAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sim.IsEnded, Is.False); Assert.That(stepAllocated, Is.EqualTo(0L), "Step"); Assert.That(writeAllocated, Is.EqualTo(0L), "Write");
            for (int i = 0; i < values.Length; i++) { Assert.That(float.IsFinite(values[i]), Is.True); Assert.That(values[i], Is.InRange(-1f, 1f)); }
        }

        [Test]
        public void WaveB_EveryNewWeaponActuallyActsInTheFullKitRun()
        {
            SurvivorSim sim = FullKitSim(184); HashSet<int> fired = new HashSet<int>(); int breaks = 0, returns = 0; int zones = 0;
            for (int i = 0; i < 1800; i++)
            {
                Poke(sim, i); sim.Step(new SurvivorInput(i / 20 % 9, 0, 0));
                for (int e = 0; e < sim.Events.Count; e++)
                {
                    if (sim.Events[e].Type != SurvivorEventType.WeaponFired) continue;
                    fired.Add(sim.Events[e].Id);
                    if (sim.Events[e].Id == 31) { if (sim.Events[e].Extra == 0f) breaks++; else returns++; }
                }
                for (int z = 0; z < sim.Zones.Count; z++) if (sim.Zones[z].Active) zones++;
            }
            foreach (int index in NewWeapons) Assert.That(fired.Contains(index), Is.True, "weapon " + index);
            Assert.That(breaks, Is.GreaterThan(0)); Assert.That(returns, Is.GreaterThan(0)); Assert.That(zones, Is.GreaterThan(0));
        }

        // ---- helpers ----

        private static SurvivorConfig NoCrit() { SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef.CritChance = 0f; return config; }
        private static SurvivorSim NewSim(int seed) => new SurvivorSim(NoCrit(), seed);

        private static SurvivorSim WeaponSim(int item, int level, int seed)
        {
            SurvivorConfig config = NoCrit(); config.ClassDef.StartingWeapon = item;
            SurvivorSim sim = new SurvivorSim(config, seed); if (level != 1) sim.GiveItemForTests(item, level); return sim;
        }

        private static SurvivorEnemy Brute(SurvivorSim sim, float x, float y) { SurvivorEnemy enemy = sim.SpawnEnemyForTests(2, new Vec2(x, y)); enemy.StunRemaining = 1000f; return enemy; }
        private static SurvivorEnemy Walker(SurvivorSim sim, float x, float y) { SurvivorEnemy enemy = sim.SpawnEnemyForTests(0, new Vec2(x, y)); enemy.StunRemaining = 1000f; return enemy; }

        private static int OrbitTicks(int charmLevel)
        {
            SurvivorSim sim = WeaponSim(2, 1, 190); if (charmLevel > 0) sim.GiveItemForTests(34, charmLevel); sim.SetHeroInvulnerableForTests();
            sim.Step(default); Assert.That(sim.OrbitAxeCount, Is.GreaterThan(0)); int ticks = 1;
            while (sim.OrbitAxeCount > 0 && ticks < 1000) { sim.Step(default); ticks++; }
            return ticks;
        }

        private static int BoomerangHomeTick(int charmLevel)
        {
            SurvivorSim sim = WeaponSim(32, 1, 191); if (charmLevel > 0) { sim.GiveItemForTests(34, charmLevel); sim.GiveItemForTests(37, charmLevel); }
            Brute(sim, 5f, 0f); sim.SetEnemiesInvulnerableForTests(); bool turned = false;
            for (int tick = 1; tick <= 150; tick++) { sim.Step(default); if (sim.Boomerangs[0].Returning) turned = true; if (turned && !sim.Boomerangs[0].Active) return tick; }
            return -1;
        }

        private static int ShotsOf(int weapon, int amountLevel)
        {
            SurvivorSim sim = WeaponSim(weapon, 1, 200 + weapon); if (amountLevel > 0) sim.GiveItemForTests(35, amountLevel);
            for (int i = 0; i < 6; i++) Brute(sim, 2.5f + i * 0.9f, (i % 3 - 1) * 1.2f);
            sim.Step(default); int shots = 0;
            for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == weapon) shots++;
            return shots;
        }

        private static int SpearsOf(int amountLevel)
        {
            SurvivorSim sim = WeaponSim(1, 1, 210); if (amountLevel > 0) sim.GiveItemForTests(35, amountLevel); Brute(sim, 3f, 0f); sim.Step(default);
            return (int)Find(sim.Events, SurvivorEventType.WeaponFired, 1).Extra;
        }

        private static int StrikesOf(int amountLevel)
        {
            SurvivorSim sim = WeaponSim(18, 1, 211); if (amountLevel > 0) sim.GiveItemForTests(35, amountLevel); for (int i = 0; i < 6; i++) Brute(sim, 3f + i * 0.7f, i - 3f); sim.Step(default);
            return (int)Find(sim.Events, SurvivorEventType.WeaponFired, 18).Extra;
        }

        private static int OrbitOf(int amountLevel)
        {
            SurvivorSim sim = WeaponSim(2, 1, 212); if (amountLevel > 0) sim.GiveItemForTests(35, amountLevel); sim.Step(default); return sim.OrbitAxeCount;
        }

        private static int BoomerangsOf(int amountLevel)
        {
            SurvivorSim sim = WeaponSim(32, 1, 213); if (amountLevel > 0) sim.GiveItemForTests(35, amountLevel); Brute(sim, 5f, 0f); sim.Step(default);
            int count = 0; for (int i = 0; i < sim.Boomerangs.Count; i++) if (sim.Boomerangs[i].Active) count++; return count;
        }

        private static SurvivorSim FullKitSim(int seed)
        {
            SurvivorConfig config = NoCrit(); config.ClassDef.MaxHp = 1e7f; SurvivorSim sim = new SurvivorSim(config, seed);
            sim.SetEnemiesInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            foreach (int index in new[] { 31, 32, 33, 28, 3, 2 }) sim.GiveItemForTests(index, 5);
            foreach (int index in NewPassives) sim.GiveItemForTests(index, index == 35 ? 2 : 3);
            for (int i = 0; i < 40; i++) sim.SpawnEnemyForTests(i % 4, Vec2.FromAngle(i * 0.7f) * (2f + i % 7));
            return sim;
        }

        /// <summary>Hits the hero now and then so the barrier breaks and returns during the run.</summary>
        private static void Poke(SurvivorSim sim, int tick)
        {
            if (tick % 40 != 0) return;
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active) { sim.DamageHeroForTests(5f, sim.Enemies[i]); return; }
        }

        private static long RunHash(int seed)
        {
            SurvivorSim sim = new SurvivorSim(NoCrit(), seed); long hash = 17;
            foreach (int index in new[] { 31, 32, 33, 28, 3, 2 }) sim.GiveItemForTests(index, 4);
            foreach (int index in NewPassives) sim.GiveItemForTests(index, 2);
            for (int tick = 0; tick < 3600 && !sim.IsEnded; tick++)
            {
                SurvivorInput input = sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1 + tick % 3) : new SurvivorInput((tick / 40) % 9, (tick / 90) % 4, 0);
                sim.Step(input); hash = hash * 31 + SurvivorM5GoldenTests.StateHash(sim);
                for (int i = 0; i < sim.Boomerangs.Count; i++) if (sim.Boomerangs[i].Active) hash = hash * 31 + BitConverter.SingleToInt32Bits(sim.Boomerangs[i].Position.X);
                for (int i = 0; i < sim.Zones.Count; i++) if (sim.Zones[i].Active) hash = hash * 31 + BitConverter.SingleToInt32Bits(sim.Zones[i].Position.Y);
                hash = hash * 31 + sim.BarrierCharges;
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
