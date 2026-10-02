using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorM4CContentTests
    {
        [Test]
        public void Catalog_NewRowsAndWarriorPools_AreComplete()
        {
            int[] indices = { 1, 2, 4, 5, 8, 10, 11, 13 };
            string[] ids = { "spear-thrust", "orbit-axe", "aura", "shockwave", "might-gauntlet", "hourglass", "area-charm", "magnet-charm" };
            for (int i = 0; i < indices.Length; i++)
            {
                ItemDef def = SurvivorCatalog.Get(indices[i]);
                Assert.That(def, Is.Not.Null); Assert.That(def.Id, Is.EqualTo(ids[i])); Assert.That(def.MaxLevel, Is.EqualTo(5));
                Assert.That(def.Kind, Is.EqualTo(i < 4 ? ItemKind.Weapon : ItemKind.Passive));
            }
            Assert.That(SurvivorCatalog.Get(1).BaseDamage, Is.EqualTo(30f)); Assert.That(SurvivorCatalog.Get(1).BaseCooldown, Is.EqualTo(1.8f));
            Assert.That(SurvivorCatalog.Get(2).Duration, Is.EqualTo(4f)); Assert.That(SurvivorCatalog.Get(4).HitInterval, Is.EqualTo(0.4f));
            Assert.That(SurvivorCatalog.Get(5).CooldownPerLevel, Is.EqualTo(-0.3f));
            SurvivorClassDef warrior = SurvivorDefaults.Warrior();
            Assert.That(warrior.WeaponPool, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 26, 27, 28, 29, 30 }));
            Assert.That(warrior.PassivePool, Is.EqualTo(new[] { 6, 7, 8, 9, 10, 11, 12, 13, 58, 59, 60, 61 }));
        }

        [Test]
        public void Offers_CanShowEveryNewItem_AndRespectSlotCaps()
        {
            bool[] seen = new bool[14];
            for (int seed = 0; seed < 300; seed++)
            {
                SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.OldConfig(), seed); sim.GiveXpForTests(5f); sim.Step(default);
                for (int i = 0; i < sim.OfferCount; i++) seen[sim.GetOffer(i).CatalogIndex] = true;
            }
            foreach (int index in new[] { 1, 2, 4, 5, 8, 10, 11, 13 }) Assert.That(seen[index], Is.True, "catalog " + index);

            SurvivorSim capped = new SurvivorSim(SurvivorTestHelpers.OldConfig(), 12);
            foreach (int index in new[] { 1, 2, 3, 6, 7, 8, 9 }) capped.GiveItemForTests(index, 1);
            Assert.That(capped.Inventory.WeaponCount, Is.EqualTo(4)); Assert.That(capped.Inventory.PassiveCount, Is.EqualTo(4));
            capped.GiveXpForTests(5f); capped.Step(default);
            for (int i = 0; i < capped.OfferCount; i++)
            {
                int index = capped.GetOffer(i).CatalogIndex;
                Assert.That(index, Is.Not.AnyOf(4, 5, 10, 11, 12, 13));
            }
        }

        [Test]
        public void Spear_PiercesCapsule_UsesFanAndCooldown()
        {
            SurvivorSim sim = WeaponSim(1, 5, 10);
            SurvivorEnemy a = sim.SpawnEnemyForTests(2, new Vec2(2f, 0f));
            SurvivorEnemy b = sim.SpawnEnemyForTests(2, new Vec2(3f, 0f));
            SurvivorEnemy c = sim.SpawnEnemyForTests(2, new Vec2(4f, 0f));
            SurvivorEnemy miss = sim.SpawnEnemyForTests(2, new Vec2(3f, 3f));
            sim.Step(default);
            Assert.That(a.Hp, Is.LessThan(a.MaxHp)); Assert.That(b.Hp, Is.LessThan(b.MaxHp)); Assert.That(c.Hp, Is.LessThan(c.MaxHp));
            Assert.That(miss.Hp, Is.EqualTo(miss.MaxHp));
            SurvivorEvent fired = Find(sim.Events, SurvivorEventType.WeaponFired, 1);
            Assert.That(fired.Extra, Is.EqualTo(3)); Assert.That(sim.WeaponCooldownForTests(1), Is.EqualTo(1.8f).Within(1e-5f));
            Assert.That(SurvivorCatalog.ThrustAngleOffset(0, 3), Is.EqualTo(-20f * MathF.PI / 180f).Within(1e-6f));
            Assert.That(SurvivorCatalog.ThrustAngleOffset(1, 2), Is.EqualTo(10f * MathF.PI / 180f).Within(1e-6f));
        }

        [Test]
        public void Spear_NoTargetDoesNotStartCooldown_AndAreaExtendsLength()
        {
            SurvivorSim empty = WeaponSim(1, 1, 11); empty.Step(default);
            Assert.That(empty.WeaponCooldownForTests(1), Is.Zero); Assert.That(empty.Events, Has.None.Matches<SurvivorEvent>(e => e.Type == SurvivorEventType.WeaponFired));

            SurvivorSim baseArea = WeaponSim(1, 1, 11); SurvivorEnemy outside = baseArea.SpawnEnemyForTests(3, new Vec2(5.5f, 0f)); baseArea.Step(default);
            Assert.That(outside.Hp, Is.EqualTo(outside.MaxHp)); Assert.That(baseArea.WeaponCooldownForTests(1), Is.Zero);
            SurvivorSim widened = WeaponSim(1, 1, 11); widened.GiveItemForTests(11, 1); SurvivorEnemy inside = widened.SpawnEnemyForTests(3, new Vec2(5.5f, 0f)); widened.Step(default);
            Assert.That(inside.Hp, Is.LessThan(inside.MaxHp));
        }

        [TestCase(1, 1)]
        [TestCase(3, 2)]
        [TestCase(5, 3)]
        public void Spear_CountByLevel_IsReportedInVolleyEvent(int level, int expected)
        {
            SurvivorSim sim = WeaponSim(1, level, 15 + level); sim.SpawnEnemyForTests(4, new Vec2(3f, 0f)); sim.Step(default);
            Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 1).Extra, Is.EqualTo(expected));
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(5, 3)]
        public void Orbit_CountByLevel_AndAreaScale(int level, int expected)
        {
            SurvivorSim sim = WeaponSim(2, level, 20 + level); sim.Step(default);
            Assert.That(sim.OrbitAxeCount, Is.EqualTo(expected)); Assert.That(sim.OrbitAxeRadius, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(Vec2.Distance(sim.Hero.Position, sim.GetOrbitAxePosition(0)), Is.EqualTo(2.2f).Within(1e-4f));
            if (level == 1)
            {
                sim.GiveItemForTests(11, 2);
                Assert.That(Vec2.Distance(sim.Hero.Position, sim.GetOrbitAxePosition(0)), Is.EqualTo(2.2f).Within(1e-4f), "active volley keeps emission size");
                SurvivorSim widened = WeaponSim(2, 1, 29); widened.GiveItemForTests(11, 1); widened.Step(default);
                Assert.That(Vec2.Distance(widened.Hero.Position, widened.GetOrbitAxePosition(0)), Is.EqualTo(2.2f * 1.08f).Within(1e-4f));
            }
        }

        [Test]
        public void Orbit_DamageUsesLevelFormula()
        {
            SurvivorSim sim = WeaponSim(2, 3, 30); SurvivorEnemy enemy = sim.SpawnEnemyForTests(4, new Vec2(2.2f, 0f)); float hp = enemy.Hp;
            sim.Step(default); Assert.That(hp - enemy.Hp, Is.EqualTo(20f).Within(1e-4f));
        }

        [Test]
        public void Orbit_FourSecondsUp_ThenCooldown_AndTouchRateLimited()
        {
            SurvivorSim sim = WeaponSim(2, 1, 25); SurvivorEnemy enemy = sim.SpawnEnemyForTests(4, Vec2.Zero); enemy.StunRemaining = 10f;
            int hits = 0, lastTick = -100;
            for (int tick = 0; tick < 245; tick++)
            {
                enemy.Position = sim.Hero.Position; sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.DamageDealt && sim.Events[i].Id == enemy.Id)
                { Assert.That(tick - lastTick, Is.GreaterThanOrEqualTo(30)); lastTick = tick; hits++; }
            }
            Assert.That(hits, Is.GreaterThanOrEqualTo(7)); Assert.That(sim.OrbitAxeCount, Is.Zero);
            Assert.That(sim.WeaponCooldownForTests(2), Is.InRange(2.9f, 3f));
        }

        [Test]
        public void Aura_TicksScalesRadiusAndPushes()
        {
            SurvivorSim sim = WeaponSim(4, 3, 31); sim.GiveItemForTests(11, 2);
            float expectedRadius = 1.8f * 1.2f * 1.16f;
            Assert.That(sim.AuraRadius, Is.EqualTo(expectedRadius).Within(1e-5f));
            SurvivorEnemy enemy = sim.SpawnEnemyForTests(2, new Vec2(1.5f, 0f)); enemy.StunRemaining = 10f; float start = enemy.Position.X;
            sim.Step(default); float firstHp = enemy.Hp; Assert.That(firstHp, Is.EqualTo(enemy.MaxHp - 9f).Within(1e-4f)); Assert.That(enemy.Position.X, Is.GreaterThan(start));
            SurvivorTestHelpers.Step(sim, 21); Assert.That(enemy.Hp, Is.EqualTo(firstHp));
            SurvivorTestHelpers.Step(sim, 4); Assert.That(enemy.Hp, Is.LessThan(firstHp));
        }

        [Test]
        public void Shockwave_ExpandsHitsOnceKnocksBackAndL5Cooldown()
        {
            SurvivorSim sim = WeaponSim(5, 5, 41); SurvivorEnemy enemy = sim.SpawnEnemyForTests(2, new Vec2(2f, 0f)); enemy.StunRemaining = 10f;
            sim.Step(default); Assert.That(sim.ShockwaveCenter, Is.EqualTo(Vec2.Zero)); Assert.That(sim.WeaponCooldownForTests(5), Is.EqualTo(2.8f).Within(1e-5f));
            int hits = 0; for (int tick = 0; tick < 40; tick++) { sim.Step(default); for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.DamageDealt && sim.Events[i].Id == enemy.Id) hits++; }
            Assert.That(hits, Is.EqualTo(1)); Assert.That(enemy.Position.X, Is.GreaterThan(2.7f)); Assert.That(sim.ShockwaveRadius, Is.Zero);

            SurvivorSim empty = WeaponSim(5, 1, 42); empty.Step(default); Assert.That(empty.WeaponCooldownForTests(5), Is.Zero);
        }

        [Test]
        public void NewPassives_ModifyDamageCooldownAreaAndMagnet()
        {
            SurvivorSim baseline = new SurvivorSim(NoCritConfig(), 51); SurvivorSim mighty = new SurvivorSim(NoCritConfig(), 51); mighty.GiveItemForTests(8, 1);
            SurvivorEnemy a = baseline.SpawnEnemyForTests(2, new Vec2(10f, 0f)); SurvivorEnemy b = mighty.SpawnEnemyForTests(2, new Vec2(10f, 0f));
            baseline.DamageEnemyForTests(a, 10f); mighty.DamageEnemyForTests(b, 10f);
            Assert.That(b.MaxHp - b.Hp, Is.EqualTo((a.MaxHp - a.Hp) * 1.08f).Within(1e-4f));

            SurvivorSim hourglass = WeaponSim(1, 1, 52); hourglass.GiveItemForTests(10, 1); hourglass.SpawnEnemyForTests(2, new Vec2(3f, 0f)); hourglass.Step(default);
            Assert.That(hourglass.WeaponCooldownForTests(1), Is.EqualTo(1.8f * 0.94f).Within(1e-5f));

            SurvivorSim sweep = new SurvivorSim(NoCritConfig(), 53); SurvivorEnemy narrowMiss = sweep.SpawnEnemyForTests(3, new Vec2(3f, 0f)); sweep.Step(default); Assert.That(narrowMiss.Hp, Is.EqualTo(narrowMiss.MaxHp));
            SurvivorSim area = new SurvivorSim(NoCritConfig(), 53); area.GiveItemForTests(11, 1); SurvivorEnemy wideHit = area.SpawnEnemyForTests(3, new Vec2(3f, 0f)); area.Step(default); Assert.That(wideHit.Hp, Is.LessThan(wideHit.MaxHp));

            SurvivorSim magnet = new SurvivorSim(NoCritConfig(), 54); float pickup = magnet.DerivedStats.PickupRadius; magnet.GiveItemForTests(13, 1);
            Assert.That(magnet.DerivedStats.PickupRadius, Is.EqualTo(pickup * 1.25f).Within(1e-5f));
        }

        [Test]
        public void Spitter_MovesToSixEightBand_WindsUpAndFires()
        {
            SurvivorSim farSim = new SurvivorSim(NoCritConfig(), 60); SurvivorEnemy far = farSim.SpawnEnemyForTests(3, new Vec2(10f, 0f)); far.AttackCooldown = 10f; farSim.Step(default); Assert.That(far.Position.X, Is.LessThan(10f));
            SurvivorSim closeSim = new SurvivorSim(NoCritConfig(), 61); SurvivorEnemy close = closeSim.SpawnEnemyForTests(3, new Vec2(5f, 0f)); close.AttackCooldown = 10f; closeSim.Step(default); Assert.That(close.Position.X, Is.GreaterThan(5f));
            SurvivorSim bandSim = new SurvivorSim(NoCritConfig(), 62); SurvivorEnemy band = bandSim.SpawnEnemyForTests(3, new Vec2(7f, 0f)); band.AttackCooldown = 10f; bandSim.Step(default); Assert.That(band.Position.X, Is.EqualTo(7f).Within(1e-5f));

            SurvivorSim attack = new SurvivorSim(NoCritConfig(), 63); SurvivorEnemy spitter = attack.SpawnEnemyForTests(3, new Vec2(7f, 0f)); attack.Step(default);
            Assert.That(spitter.WindingUp, Is.True); SurvivorTestHelpers.Step(attack, 31);
            Assert.That(ActiveEnemyProjectiles(attack), Is.EqualTo(1)); SurvivorEnemyProjectile projectile = FirstEnemyProjectile(attack);
            Assert.That(projectile.Position.X, Is.LessThan(spitter.Position.X)); Assert.That(projectile.SourceId, Is.EqualTo(spitter.Id));
        }

        [Test]
        public void EnemyProjectile_DamagesBlocksStopsAtObstacleAndSetsDeathCause()
        {
            SurvivorConfig armoredConfig = NoCritConfig(); armoredConfig.Build.Points[(int)StatId.Armor] = 2;
            SurvivorSim hit = new SurvivorSim(armoredConfig, 70); float hp = hit.Hero.Hp;
            hit.SpawnEnemyProjectileForTests(new Vec2(0.9f, 0f), new Vec2(-7f, 0f), 0.3f, 10f, 2f, 77); SurvivorTestHelpers.Step(hit, 2);
            Assert.That(Find(hit.Events, SurvivorEventType.HeroDamaged).Value, Is.EqualTo(9f).Within(1e-4f)); Assert.That(hit.Hero.Hp, Is.LessThan(hp));

            SurvivorSim front = new SurvivorSim(NoCritConfig(), 71); front.SpawnEnemyProjectileForTests(new Vec2(0.9f, 0f), new Vec2(-7f, 0f), 0.3f, 10f, 2f, 78); hp = front.Hero.Hp;
            SurvivorTestHelpers.Step(front, 2, new SurvivorInput(0, 2, 0)); Assert.That(front.Hero.Hp, Is.EqualTo(hp)); Assert.That(front.Events, Has.Some.Matches<SurvivorEvent>(e => e.Type == SurvivorEventType.Blocked && e.Id == 78));

            SurvivorSim rear = new SurvivorSim(NoCritConfig(), 72); rear.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, MathF.PI); rear.SpawnEnemyProjectileForTests(new Vec2(0.9f, 0f), new Vec2(-7f, 0f), 0.3f, 10f, 2f, 79); hp = rear.Hero.Hp;
            SurvivorTestHelpers.Step(rear, 2, new SurvivorInput(0, 2, 0)); Assert.That(rear.Hero.Hp, Is.LessThan(hp));

            SurvivorConfig obstacleConfig = NoCritConfig(); obstacleConfig.ObstacleCount = 1; obstacleConfig.ObstacleClearRadius = 0f;
            SurvivorSim stopped = new SurvivorSim(obstacleConfig, 73); SurvivorObstacle obstacle = FirstObstacle(stopped);
            stopped.SpawnEnemyProjectileForTests(obstacle.Position, Vec2.Zero, 0.3f, 10f, 2f); SurvivorTestHelpers.Step(stopped, 2);
            Assert.That(ActiveEnemyProjectiles(stopped), Is.Zero);

            SurvivorSim lethal = new SurvivorSim(NoCritConfig(), 74); lethal.SetHeroHpForTests(1f); lethal.SpawnEnemyProjectileForTests(Vec2.Zero, Vec2.Zero, 0.3f, 10f, 2f, 80); SurvivorTestHelpers.Step(lethal, 2);
            Assert.That(lethal.EndReason, Is.EqualTo(EndReason.Died)); Assert.That(lethal.DeathCause, Is.EqualTo(DeathCause.Projectile));
        }

        [Test]
        public void Spitter_DefinitionSpawnWeightsAndXp_AreCorrect()
        {
            SurvivorEnemyDef def = SurvivorDefaults.EnemyDef(3);
            Assert.That(def.Id, Is.EqualTo("spitter")); Assert.That(def.BaseHp, Is.EqualTo(20f)); Assert.That(def.MoveSpeed, Is.EqualTo(2.4f));
            Assert.That(def.AttackKind, Is.EqualTo(SurvivorAttackKind.Ranged)); Assert.That(def.Xp, Is.EqualTo(2));
            int[] expected = { 0, 0, 0, 1, 2, 2, 3 };
            for (int i = 0; i < expected.Length; i++) Assert.That(SurvivorDefaults.GetPhase(i).Weights[3], Is.EqualTo(expected[i]));
            SurvivorSim sim = new SurvivorSim(NoCritConfig(), 75); SurvivorEnemy spitter = sim.SpawnEnemyForTests(3, new Vec2(5f, 0f)); sim.DamageEnemyForTests(spitter, 1000f);
            Assert.That(FindPickup(sim, PickupKind.Gem).Value, Is.EqualTo(2f * sim.Config.Tuning.XpMul));
        }

        [Test]
        public void Magnet_AttractsAllGems_AndDropRateIsPointTwoPercent()
        {
            SurvivorSim sim = new SurvivorSim(NoCritConfig(), 80); sim.SpawnGemForTests(new Vec2(8f, 0f), 1f); sim.SpawnGemForTests(new Vec2(-9f, 0f), 2f); sim.SpawnGemForTests(new Vec2(0f, 10f), 3f);
            sim.SpawnPickupForTests(PickupKind.Magnet, Vec2.Zero, 0f); sim.Step(default);
            SurvivorEvent picked = Find(sim.Events, SurvivorEventType.MagnetPicked); Assert.That(picked.Value, Is.EqualTo(3f));
            for (int i = 0; i < sim.Pickups.Count; i++) if (sim.Pickups[i].Active && sim.Pickups[i].Kind == PickupKind.Gem) Assert.That(sim.Pickups[i].Attracted, Is.True);
            SurvivorTestHelpers.Step(sim, 60); Assert.That(ActivePickups(sim, PickupKind.Gem), Is.Zero); Assert.That(sim.TotalXp, Is.EqualTo(6f));

            SurvivorSim rolls = new SurvivorSim(NoCritConfig(), 81); int drops = 0;
            for (int i = 0; i < 100000; i++) if (rolls.RollMagnetDropForTests()) drops++;
            Assert.That(drops, Is.InRange(160, 240));
        }

        [Test]
        public void Chest_EliteDropsAndOpeningUpgradesOneItemAndPaysGold()
        {
            SurvivorSim drop = new SurvivorSim(NoCritConfig(), 90); SurvivorEnemy elite = drop.SpawnEnemyForTests(0, new Vec2(5f, 0f), true); drop.DamageEnemyForTests(elite, 10000f);
            Assert.That(FindPickup(drop, PickupKind.Chest), Is.Not.Null);

            SurvivorSim sim = new SurvivorSim(NoCritConfig(), 91); sim.SetWeaponCooldownForTests(0, 0.75f); sim.SpawnPickupForTests(PickupKind.Chest, Vec2.Zero, 0f); sim.Step(default);
            SurvivorEvent opened = Find(sim.Events, SurvivorEventType.ChestOpened); SurvivorEvent gold = Find(sim.Events, SurvivorEventType.GoldCollected);
            Assert.That(sim.Inventory.Level(0), Is.EqualTo(2)); Assert.That(opened.Id, Is.EqualTo(0)); Assert.That(opened.Extra, Is.EqualTo(2));
            Assert.That(opened.Value, Is.InRange(50f, 149f)); Assert.That(gold.Value, Is.EqualTo(opened.Value));
            Assert.That(sim.WeaponCooldownForTests(0), Is.LessThan(0.75f).And.GreaterThan(0.7f), "chest does not reset cooldown");
            Assert.That(sim.DropsSpawned, Is.Zero); Assert.That(sim.DropsCollected, Is.Zero);

            SurvivorSim maxed = new SurvivorSim(NoCritConfig(), 92); maxed.GiveItemForTests(0, 5); maxed.SpawnPickupForTests(PickupKind.Chest, Vec2.Zero, 0f); maxed.Step(default);
            SurvivorEvent maxedOpen = Find(maxed.Events, SurvivorEventType.ChestOpened); Assert.That(maxedOpen.Id, Is.EqualTo(-1)); Assert.That(maxedOpen.Extra, Is.Zero); Assert.That(maxed.Gold, Is.Positive);

            SurvivorConfig richConfig = NoCritConfig(); richConfig.Build.Tier = 3; richConfig.Build.Points[(int)StatId.Greed] = 10;
            SurvivorSim rich = new SurvivorSim(richConfig, 92); rich.GiveItemForTests(0, 5); rich.SpawnPickupForTests(PickupKind.Chest, Vec2.Zero, 0f); rich.Step(default);
            Assert.That(Find(rich.Events, SurvivorEventType.ChestOpened).Value, Is.EqualTo(maxedOpen.Value * 3f).Within(1e-4f));
        }

        [Test]
        public void Observation_ProjectileAndNewPickupsUseReservedOffsets()
        {
            SurvivorSim sim = new SurvivorSim(NoCritConfig(), 100); sim.SpawnEnemyProjectileForTests(new Vec2(4f, 0f), new Vec2(-2f, 0f), 0.3f, 10f, 10f);
            sim.SpawnPickupForTests(PickupKind.Chest, new Vec2(6f, 0f), 0f); sim.SpawnPickupForTests(PickupKind.Magnet, new Vec2(0f, 6f), 0f);
            float[] values = new float[SurvivorObservation.Size]; SurvivorObservation observation = new SurvivorObservation(); observation.Write(sim, values);
            int east = SurvivorObservation.RaysOffset, north = SurvivorObservation.RaysOffset + 18 * SurvivorObservation.RayStride;
            Assert.That(values[east + 10], Is.EqualTo(1f)); Assert.That(values[east + 16], Is.Positive); Assert.That(values[east + 21], Is.EqualTo(1f)); Assert.That(values[north + 22], Is.EqualTo(1f));
            SurvivorEnemyProjectile projectile = FirstEnemyProjectile(sim); projectile.Velocity = new Vec2(2f, 0f); observation.Write(sim, values); Assert.That(values[east + 16], Is.Negative);
            AssertFinite(values);

            SurvivorSim spitterSim = new SurvivorSim(NoCritConfig(), 101); spitterSim.SpawnEnemyForTests(3, new Vec2(4f, 0f)); observation.Write(spitterSim, values);
            Assert.That(values[east + 5], Is.EqualTo(1f));
        }

        [Test]
        public void AllContent_IsDeterministicAndAllocationFree()
        {
            SurvivorSim a = FullContentSim(110); SurvivorSim b = FullContentSim(110);
            SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];
            for (int tick = 0; tick < 180; tick++)
            {
                SurvivorInput input = new SurvivorInput((tick / 15) % 9, 0, 0); a.Step(input); b.Step(input);
                Assert.That(ContentHash(b), Is.EqualTo(ContentHash(a)), "tick " + tick); observation.Write(a, values); AssertFinite(values);
            }
            for (int i = 0; i < 200; i++) { a.Step(default); observation.Write(a, values); }
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 200; i++) a.Step(default); long stepAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 200; i++) observation.Write(a, values); long writeAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            // The .NET 10 allocation counter can charge one 24-byte sampling object to the final measured block.
            // A per-call allocation would be thousands of bytes here; the existing interleaved zero-allocation test remains exact.
            Assert.That(stepAllocated, Is.LessThanOrEqualTo(24L), "Step"); Assert.That(writeAllocated, Is.LessThanOrEqualTo(24L), "Write");
        }

        [Test]
        public void RealCombat_WithNewContent_IsDeterministic()
        {
            SurvivorSim a = CombatSim(130); SurvivorSim b = CombatSim(130);
            for (int tick = 0; tick < 900 && !a.IsEnded; tick++)
            {
                SurvivorInput input = new SurvivorInput((tick / 20) % 9, 0, a.IsAwaitingPick ? 1 : 0); a.Step(input); b.Step(input);
                Assert.That(b.Events.Count, Is.EqualTo(a.Events.Count), "tick " + tick);
                for (int i = 0; i < a.Events.Count; i++)
                {
                    Assert.That(b.Events[i].Type, Is.EqualTo(a.Events[i].Type)); Assert.That(b.Events[i].Value, Is.EqualTo(a.Events[i].Value)); Assert.That(b.Events[i].Id, Is.EqualTo(a.Events[i].Id));
                }
                Assert.That(ContentHash(b), Is.EqualTo(ContentHash(a)), "tick " + tick);
            }
            Assert.That(a.Kills, Is.Positive);
        }

        [Test]
        public void ChestRewardIsPositive_ProjectileDamageRewardIsNegative()
        {
            SurvivorRewardConfig config = new SurvivorRewardConfig(); SurvivorRewardCalculator calculator = new SurvivorRewardCalculator(config);
            SurvivorSim chest = new SurvivorSim(NoCritConfig(), 120); chest.GiveItemForTests(0, 5); chest.SpawnPickupForTests(PickupKind.Chest, Vec2.Zero, 0f); chest.Step(default);
            float survivalOnly = config.SurvivePerSecond * chest.LastStepSeconds;
            Assert.That(calculator.Compute(chest.Events, chest.LastStepSeconds), Is.GreaterThan(survivalOnly + config.PerGold * 49f), "chest gold adds reward beyond survival");
            SurvivorSim hurt = new SurvivorSim(NoCritConfig(), 121); hurt.SpawnEnemyProjectileForTests(Vec2.Zero, Vec2.Zero, 0.3f, 10f, 1f); SurvivorTestHelpers.Step(hurt, 2);
            Assert.That(calculator.Compute(hurt.Events, hurt.LastStepSeconds), Is.Negative);
        }

        private static SurvivorConfig NoCritConfig()
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef.CritChance = 0f; return config;
        }

        private static SurvivorSim WeaponSim(int item, int level, int seed)
        {
            SurvivorConfig config = NoCritConfig(); config.ClassDef.StartingWeapon = item;
            SurvivorSim sim = new SurvivorSim(config, seed); if (level != 1) sim.GiveItemForTests(item, level); return sim;
        }

        private static SurvivorSim FullContentSim(int seed)
        {
            SurvivorSim sim = new SurvivorSim(NoCritConfig(), seed); sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            for (int item = 1; item <= 5; item++) sim.GiveItemForTests(item, 5);
            for (int i = 0; i < 20; i++) { SurvivorEnemy enemy = sim.SpawnEnemyForTests(3, Vec2.FromAngle(i * 0.31f) * (6f + i % 4)); enemy.AttackCooldown = i * 0.03f; }
            for (int i = 0; i < 16; i++) sim.SpawnEnemyProjectileForTests(Vec2.FromAngle(i * 0.4f) * 12f, Vec2.Zero, 0.3f, 10f, 100f, i + 1);
            sim.SpawnPickupForTests(PickupKind.Chest, new Vec2(15f, 0f), 0f); sim.SpawnPickupForTests(PickupKind.Magnet, new Vec2(-15f, 0f), 0f);
            for (int i = 0; i < 20; i++) sim.SpawnGemForTests(Vec2.FromAngle(i) * 10f, 1f);
            return sim;
        }

        private static SurvivorSim CombatSim(int seed)
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), seed); sim.SetTimeForTests(400f);
            foreach (int item in new[] { 1, 2, 4, 5 }) sim.GiveItemForTests(item, 5);
            for (int i = 0; i < 12; i++) sim.SpawnEnemyForTests(i % 4, Vec2.FromAngle(i * 0.52f) * (5f + i % 5), i == 0);
            sim.SpawnPickupForTests(PickupKind.Chest, new Vec2(3f, 0f), 0f); sim.SpawnPickupForTests(PickupKind.Magnet, new Vec2(-3f, 0f), 0f);
            return sim;
        }

        private static long ContentHash(SurvivorSim sim)
        {
            long hash = 17; hash = hash * 31 + sim.Time.GetHashCode(); hash = hash * 31 + sim.Hero.Position.GetHashCode(); hash = hash * 31 + sim.Hero.Hp.GetHashCode();
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active) { hash = hash * 31 + sim.Enemies[i].Id; hash = hash * 31 + sim.Enemies[i].Position.GetHashCode(); hash = hash * 31 + sim.Enemies[i].Hp.GetHashCode(); }
            for (int i = 0; i < sim.EnemyProjectiles.Count; i++) if (sim.EnemyProjectiles[i].Active) { hash = hash * 31 + sim.EnemyProjectiles[i].Id; hash = hash * 31 + sim.EnemyProjectiles[i].Position.GetHashCode(); }
            for (int i = 0; i < sim.Pickups.Count; i++) if (sim.Pickups[i].Active) { hash = hash * 31 + (int)sim.Pickups[i].Kind; hash = hash * 31 + sim.Pickups[i].Position.GetHashCode(); }
            return hash;
        }

        private static SurvivorEvent Find(IReadOnlyList<SurvivorEvent> events, SurvivorEventType type, int id = int.MinValue)
        {
            for (int i = 0; i < events.Count; i++) if (events[i].Type == type && (id == int.MinValue || events[i].Id == id)) return events[i];
            Assert.Fail("Missing event " + type); return default;
        }

        private static SurvivorPickup FindPickup(SurvivorSim sim, PickupKind kind)
        {
            for (int i = 0; i < sim.Pickups.Count; i++) if (sim.Pickups[i].Active && sim.Pickups[i].Kind == kind) return sim.Pickups[i];
            return null;
        }

        private static int ActivePickups(SurvivorSim sim, PickupKind kind)
        {
            int count = 0; for (int i = 0; i < sim.Pickups.Count; i++) if (sim.Pickups[i].Active && sim.Pickups[i].Kind == kind) count++; return count;
        }

        private static int ActiveEnemyProjectiles(SurvivorSim sim)
        {
            int count = 0; for (int i = 0; i < sim.EnemyProjectiles.Count; i++) if (sim.EnemyProjectiles[i].Active) count++; return count;
        }

        private static SurvivorEnemyProjectile FirstEnemyProjectile(SurvivorSim sim)
        {
            for (int i = 0; i < sim.EnemyProjectiles.Count; i++) if (sim.EnemyProjectiles[i].Active) return sim.EnemyProjectiles[i];
            return null;
        }

        private static SurvivorObstacle FirstObstacle(SurvivorSim sim)
        {
            for (int i = 0; i < sim.Obstacles.Count; i++) if (sim.Obstacles[i].Active) return sim.Obstacles[i];
            return null;
        }

        private static void AssertFinite(float[] values)
        {
            Assert.That(values.Length, Is.EqualTo(2264));
            for (int i = 0; i < values.Length; i++) { Assert.That(float.IsFinite(values[i]), Is.True, "index " + i); Assert.That(values[i], Is.InRange(-1f, 1f), "index " + i); }
        }
    }
}
