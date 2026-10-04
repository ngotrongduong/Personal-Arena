using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>Ring weapons: bodies that circle the hero all the time at a fixed distance, several rings at once.</summary>
    public sealed class SurvivorRingTests
    {
        private static readonly int[] Rings =
        {
            SurvivorCatalog.SpiritOrbsIndex, SurvivorCatalog.SawRingIndex, SurvivorCatalog.FrostHaloIndex, SurvivorCatalog.CometIndex
        };

        [Test]
        public void Rings_AreInEveryClassPool_EachAtItsOwnDistance()
        {
            HashSet<float> distances = new HashSet<float>();
            foreach (int index in Rings)
            {
                ItemDef def = SurvivorCatalog.Get(index);
                Assert.That(def.Pattern, Is.EqualTo(WeaponPattern.Ring), def.Id); Assert.That(def.BaseCooldown, Is.Zero, def.Id + " never rests");
                Assert.That(def.MaxLevel, Is.EqualTo(5)); Assert.That(def.CountByLevel.Count, Is.EqualTo(5));
                Assert.That(distances.Add(def.BaseRange), Is.True, def.Id + " shares a distance with another ring");
                foreach (string cls in new[] { "warrior", "mage", "archer" })
                    Assert.That(Array.IndexOf(SurvivorDefaults.ForClass(cls).WeaponPool, index), Is.GreaterThanOrEqualTo(0), cls + " " + def.Id);
            }
            Assert.That(SurvivorObservation.Size, Is.EqualTo(2592), "schema v5 is untouched");
        }

        [Test]
        public void Ring_BodiesStayAtTheFixedDistance_AndNeverStop()
        {
            SurvivorSim sim = RingSim(SurvivorCatalog.SpiritOrbsIndex, 1, 301); sim.Step(default);
            Assert.That(sim.RingBodyCount(0), Is.EqualTo(2)); Assert.That(sim.RingRadius(0), Is.EqualTo(3.2f).Within(1e-5f));
            Assert.That(sim.RingBodyRadius(0), Is.EqualTo(0.45f).Within(1e-5f));
            Vec2 first = sim.GetRingBodyPosition(0, 0);
            Assert.That(Vec2.Distance(first, sim.GetRingBodyPosition(0, 1)), Is.EqualTo(6.4f).Within(1e-3f), "two bodies sit opposite each other");
            for (int tick = 0; tick < 900; tick++)
            {
                sim.Step(default);
                Assert.That(sim.RingBodyCount(0), Is.EqualTo(2), "tick " + tick);
                Assert.That(Vec2.Distance(sim.Hero.Position, sim.GetRingBodyPosition(0, 0)), Is.EqualTo(3.2f).Within(1e-3f));
            }
            Assert.That(Vec2.Distance(first, sim.GetRingBodyPosition(0, 0)), Is.GreaterThan(0.1f), "it turned");
            Assert.That(sim.RingBodyCount(1), Is.Zero, "an empty slot holds no ring"); Assert.That(sim.RingRadius(1), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => sim.GetRingBodyPosition(0, 2));
        }

        [Test]
        public void Ring_DamageUsesLevelFormula()
        {
            SurvivorSim sim = RingSim(SurvivorCatalog.CometIndex, 3, 302);
            SurvivorEnemy enemy = sim.SpawnEnemyForTests(4, sim.Hero.Position + new Vec2(5f, 0f)); float hp = enemy.Hp;
            sim.Step(default); Assert.That(hp - enemy.Hp, Is.EqualTo(50f).Within(1e-3f));
        }

        [Test]
        public void Ring_HitsAnEnemyOnItsPath_AtMostOncePerHitInterval()
        {
            // Four saws at 420 degrees a second pass a point of the ring every 0.21 s; the enemy is still hit only every 0.3 s.
            SurvivorSim sim = RingSim(SurvivorCatalog.SawRingIndex, 5, 303);
            SurvivorEnemy enemy = sim.SpawnEnemyForTests(4, sim.Hero.Position + new Vec2(1.6f, 0f)); enemy.StunRemaining = 1000f;
            int hits = 0; float lastHit = -10f;
            for (int tick = 0; tick < 240; tick++)
            {
                enemy.Position = sim.Hero.Position + new Vec2(1.6f, 0f); sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++)
                {
                    if (sim.Events[i].Type != SurvivorEventType.DamageDealt || sim.Events[i].Id != enemy.Id) continue;
                    Assert.That(sim.Time - lastHit, Is.GreaterThanOrEqualTo(0.3f - 1e-3f)); lastHit = sim.Time; hits++;
                }
            }
            Assert.That(hits, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void TwoRings_TurnAtOnce_EachWithItsOwnHitTimer()
        {
            SurvivorSim sim = RingSim(SurvivorCatalog.SpiritOrbsIndex, 5, 304); sim.GiveItemForTests(SurvivorCatalog.CometIndex, 5);
            sim.Step(default);
            Assert.That(sim.RingBodyCount(0), Is.EqualTo(4)); Assert.That(sim.RingBodyCount(1), Is.EqualTo(2));
            Assert.That(sim.RingRadius(0), Is.EqualTo(3.2f).Within(1e-5f)); Assert.That(sim.RingRadius(1), Is.EqualTo(5f).Within(1e-5f));
            // One enemy between the two rings is in reach of both: each ring hits it on its own timer.
            SurvivorEnemy enemy = sim.SpawnEnemyForTests(4, sim.Hero.Position + new Vec2(4.1f, 0f)); enemy.StunRemaining = 1000f;
            bool orbHit = false, cometHit = false;
            for (int tick = 0; tick < 240; tick++)
            {
                enemy.Position = sim.Hero.Position + new Vec2(4.1f, 0f); float before = enemy.Hp; sim.Step(default);
                float lost = before - enemy.Hp;
                if (Math.Abs(lost - 21f) < 1e-2f || lost > 90f) orbHit = true;
                if (Math.Abs(lost - 70f) < 1e-2f || lost > 90f) cometHit = true;
            }
            Assert.That(orbHit, Is.True, "the orbs (21 damage) reach it"); Assert.That(cometHit, Is.True, "the comet (70 damage) reaches it");
        }

        [Test]
        public void Ring_GrowsWithAreaAndAmountBonuses()
        {
            SurvivorSim sim = RingSim(SurvivorCatalog.FrostHaloIndex, 1, 305);
            sim.GiveItemForTests(SurvivorCatalog.AreaCharmIndex, 1); sim.GiveItemForTests(SurvivorCatalog.DuplicatorIndex, 1); sim.Step(default);
            Assert.That(sim.RingRadius(0), Is.EqualTo(2.4f * 1.08f).Within(1e-4f)); Assert.That(sim.RingBodyCount(0), Is.EqualTo(4), "3 shards + 1 from the duplicator");
        }

        private static SurvivorSim RingSim(int item, int level, int seed)
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef.CritChance = 0f; config.ClassDef.StartingWeapon = item;
            SurvivorSim sim = new SurvivorSim(config, seed); sim.SetHeroInvulnerableForTests();
            if (level != 1) sim.GiveItemForTests(item, level);
            return sim;
        }
    }
}
