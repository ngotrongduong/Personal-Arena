using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorM5TierTests
    {
        [Test]
        public void Has_For_DisplayName()
        {
            Assert.That(TierModifiers.For(1), Is.Empty);
            Assert.That(TierModifiers.For(5), Is.EqualTo(new[] { TierModifier.DenserSpawns, TierModifier.EarlyElite, TierModifier.FastRunners, TierModifier.LessMeat }));
            Assert.That(TierModifiers.For(10).Count(), Is.EqualTo(9));
            Assert.That(TierModifiers.For(10), Is.Ordered);
            foreach (TierModifier m in Enum.GetValues(typeof(TierModifier)))
            {
                Assert.That(TierModifiers.Has((int)m, m), Is.True); Assert.That(TierModifiers.Has((int)m - 1, m), Is.False);
                Assert.That(TierModifiers.DisplayName(m), Is.Not.Empty, m.ToString());
            }
            Assert.That(TierModifiers.DisplayName(TierModifier.Nightmare), Is.EqualTo("Nightmare: all modifiers above, enemies 10% faster, elites +50% HP"));
        }

        [Test]
        public void DenserSpawns_Tier2_SpawnsMore()
        {
            int plain = CountSpawns(Sim(2, t => t.DenserSpawnsMul = 1f), 1200);
            int denser = CountSpawns(Sim(2), 1200);
            int tier1 = CountSpawns(Sim(1), 1200);
            Assert.That(tier1, Is.EqualTo(20).Within(1));
            Assert.That(plain, Is.EqualTo(23).Within(1)); // 20 s · 1/s · 1.15 (SpawnPerTier only)
            Assert.That(denser, Is.EqualTo(25).Within(1)); // · 1.10 more
            Assert.That(denser, Is.GreaterThan(plain));
        }

        [TestCase(1, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 1)]
        [TestCase(6, 1)]
        [TestCase(7, 2)]
        [TestCase(10, 2)]
        public void EarlyElite_At90s_FromTier3_DoubledFromTier7(int tier, int expected)
        {
            SurvivorSim sim = Sim(tier); sim.SetTimeForTests(89.9f);
            Assert.That(CountEvents(sim, SurvivorEventType.EliteSpawned, 30), Is.EqualTo(expected));
        }

        [TestCase(1, 1)]
        [TestCase(3, 2)]
        [TestCase(6, 2)]
        [TestCase(7, 4)]
        public void ScheduledElites_DoubleFromTier7(int tier, int expected)
        {
            // Jumping to 179.9 s also triggers the tier-3 early elite (90 s) on the first tick.
            SurvivorSim sim = Sim(tier); sim.SetTimeForTests(179.9f);
            Assert.That(CountEvents(sim, SurvivorEventType.EliteSpawned, 30), Is.EqualTo(expected));
        }

        [Test]
        public void FastRunners_Tier4()
        {
            SurvivorEnemyDef runner = SurvivorDefaults.EnemyDef(1), walker = SurvivorDefaults.EnemyDef(0);
            SurvivorSim t3 = Sim(3), t4 = Sim(4);
            Assert.That(t3.EnemyMoveSpeed(t3.SpawnEnemyForTests(1, new Vec2(15f, 0f)), runner), Is.EqualTo(4f));
            Assert.That(t4.EnemyMoveSpeed(t4.SpawnEnemyForTests(1, new Vec2(15f, 0f)), runner), Is.EqualTo(4f * 1.15f).Within(1e-5f));
            Assert.That(t4.EnemyMoveSpeed(t4.SpawnEnemyForTests(0, new Vec2(-15f, 0f)), walker), Is.EqualTo(2f));
            Assert.That(OneTickDistance(3, 1), Is.EqualTo(4f / 60f).Within(1e-4f));
            Assert.That(OneTickDistance(4, 1), Is.EqualTo(4f * 1.15f / 60f).Within(1e-4f));
        }

        [Test]
        public void LessMeat_Tier5()
        {
            float chance = new SurvivorTuning().MeatChance;
            Assert.That(Sim(4).EffectiveMeatChance, Is.EqualTo(chance));
            Assert.That(Sim(5).EffectiveMeatChance, Is.EqualTo(chance * 0.5f));
        }

        [Test]
        public void EarlyBrutes_Tier6_OnlyFrom60sBeforeTheBrutePhase()
        {
            SurvivorSim t5 = Sim(5), t6 = Sim(6);
            Assert.That(t5.SpawnWeight(SurvivorDefaults.GetPhase(1), 2), Is.EqualTo(0));
            Assert.That(t6.SpawnWeight(SurvivorDefaults.GetPhase(1), 2), Is.EqualTo(1));
            Assert.That(t6.SpawnWeight(SurvivorDefaults.GetPhase(0), 2), Is.EqualTo(0), "first minute unchanged");
            Assert.That(t6.SpawnWeight(SurvivorDefaults.GetPhase(2), 2), Is.EqualTo(SurvivorDefaults.GetPhase(2).Weights[2]));
            Assert.That(t6.SpawnWeight(SurvivorDefaults.GetPhase(1), 0), Is.EqualTo(SurvivorDefaults.GetPhase(1).Weights[0]));
            Assert.That(BrutesAfter60s(5), Is.EqualTo(0));
            Assert.That(BrutesAfter60s(6), Is.GreaterThan(0));
        }

        [TestCase(7, false)]
        [TestCase(8, true)]
        public void EnemyRegen_After3sIdle_FromTier8(int tier, bool regenerates)
        {
            SurvivorSim sim = Sim(tier); sim.SetHeroInvulnerableForTests();
            SurvivorEnemy e = sim.SpawnEnemyForTests(0, new Vec2(35f, 0f));
            sim.DamageEnemyForTests(e, 5f);
            float damaged = e.Hp;
            Assert.That(damaged, Is.LessThan(e.MaxHp));
            SurvivorTestHelpers.Step(sim, 170); // 2.83 s: still inside the 3 s delay
            Assert.That(e.Hp, Is.EqualTo(damaged));
            SurvivorTestHelpers.Step(sim, 70); // 4 s after the hit: ~1 s of regen
            if (regenerates) Assert.That(e.Hp, Is.EqualTo(Math.Min(e.MaxHp, damaged + e.MaxHp * 0.02f)).Within(e.MaxHp * 0.02f * 0.1f));
            else Assert.That(e.Hp, Is.EqualTo(damaged));
            SurvivorEnemy boss = sim.SpawnBossForTests(new Vec2(-35f, 0f));
            sim.DamageEnemyForTests(boss, 50f); float bossHp = boss.Hp;
            SurvivorTestHelpers.Step(sim, 300);
            Assert.That(boss.Hp, Is.EqualTo(bossHp), "the boss never regenerates");
        }

        [Test]
        public void BossSummonsFaster_Tier9()
        {
            SurvivorSim t8 = Sim(8), t9 = Sim(9);
            float interval = new SurvivorTuning().BossSummonIntervalSeconds;
            Assert.That(t8.SummonCooldownForTests(t8.SpawnBossForTests(new Vec2(20f, 0f))), Is.EqualTo(interval));
            Assert.That(t9.SummonCooldownForTests(t9.SpawnBossForTests(new Vec2(20f, 0f))), Is.EqualTo(interval * 0.5f));
            Assert.That(t9.EffectiveBossSummonInterval, Is.EqualTo(interval * 0.5f));
            // Over 10.1 s the tier-9 boss summons twice (every 5 s), the tier-8 boss once.
            Assert.That(CountSummons(9), Is.EqualTo(2));
            Assert.That(CountSummons(8), Is.EqualTo(1));
        }

        private static int CountSummons(int tier)
        {
            SurvivorSim sim = Sim(tier); sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests();
            SurvivorEnemy boss = sim.SpawnBossForTests(new Vec2(25f, 0f));
            int summons = 0; float previous = sim.SummonCooldownForTests(boss);
            for (int t = 0; t < 606; t++)
            {
                sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : default);
                float cooldown = sim.SummonCooldownForTests(boss);
                if (cooldown > previous) summons++;
                previous = cooldown;
            }
            return summons;
        }

        [Test]
        public void Nightmare_Tier10_SpeedAndEliteHp()
        {
            SurvivorSim t9 = Sim(9), t10 = Sim(10);
            SurvivorEnemyDef walker = SurvivorDefaults.EnemyDef(0), runner = SurvivorDefaults.EnemyDef(1), boss = SurvivorDefaults.EnemyDef(4);
            Assert.That(t9.EnemyMoveSpeed(t9.SpawnEnemyForTests(0, new Vec2(15f, 0f)), walker), Is.EqualTo(2f));
            Assert.That(t10.EnemyMoveSpeed(t10.SpawnEnemyForTests(0, new Vec2(15f, 0f)), walker), Is.EqualTo(2f * 1.1f).Within(1e-5f));
            Assert.That(t10.EnemyMoveSpeed(t10.SpawnEnemyForTests(1, new Vec2(-15f, 0f)), runner), Is.EqualTo(4f * 1.15f * 1.1f).Within(1e-5f));
            Assert.That(t10.EnemyMoveSpeed(t10.SpawnBossForTests(new Vec2(0f, 20f)), boss), Is.EqualTo(1.4f * 1.1f).Within(1e-5f));
            SurvivorEnemy elite9 = t9.SpawnEnemyForTests(0, new Vec2(0f, -15f), true), elite10 = t10.SpawnEnemyForTests(0, new Vec2(0f, -15f), true);
            SurvivorEnemy normal10 = t10.SpawnEnemyForTests(0, new Vec2(0f, 15f));
            Assert.That(elite9.MaxHp, Is.EqualTo(15f * (1f + 0.35f * 8f) * 10f).Within(1e-3f));
            Assert.That(elite10.MaxHp, Is.EqualTo(15f * (1f + 0.35f * 9f) * 10f * 1.5f).Within(1e-3f));
            Assert.That(normal10.MaxHp, Is.EqualTo(15f * (1f + 0.35f * 9f)).Within(1e-3f));
            Assert.That(OneTickDistance(10, 0), Is.EqualTo(2f * 1.1f / 60f).Within(1e-4f));
        }

        [TestCase(1)]
        [TestCase(5)]
        [TestCase(10)]
        public void SameSeed_SameRun_AtTier(int tier)
        {
            SurvivorSim a = new SurvivorSim(SurvivorTestHelpers.Config(tier), 77), b = new SurvivorSim(SurvivorTestHelpers.Config(tier), 77);
            for (int tick = 0; tick < 3600 && !a.IsEnded; tick++)
            {
                SurvivorInput input = a.IsAwaitingPick ? new SurvivorInput(0, 0, 1 + tick % 3) : new SurvivorInput((tick / 20) % 9, tick % 97 == 0 ? 3 : 0, 0);
                a.Step(input); b.Step(input);
                if (tick % 60 == 0) Assert.That(SurvivorM5GoldenTests.StateHash(b), Is.EqualTo(SurvivorM5GoldenTests.StateHash(a)), "tick " + tick);
            }
            Assert.That(SurvivorM5GoldenTests.StateHash(b), Is.EqualTo(SurvivorM5GoldenTests.StateHash(a)));
            Assert.That(b.Gold, Is.EqualTo(a.Gold)); Assert.That(b.Kills, Is.EqualTo(a.Kills));
        }

        [Test]
        public void Tier10_AllModifiers_StepAndWrite_DoNotAllocate()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(10), 55);
            for (int item = 0; item <= 5; item++) sim.GiveItemForTests(item, 5);
            for (int i = 0; i < 250; i++) { float angle = i * 2.399963f; float radius = 6f + i % 24; sim.SpawnEnemyForTests(i % 4, Vec2.FromAngle(angle) * radius, i % 50 == 0); }
            SurvivorEnemy boss = sim.SpawnBossForTests(new Vec2(12f, 0f));
            for (int i = 0; i < 400; i++) { float angle = i * 1.7f; sim.SpawnGemForTests(Vec2.FromAngle(angle) * (3f + i % 27), 1 + i % 8); }
            sim.SetTimeForTests(89f); // crosses the early-elite time during the warm-up
            SurvivorEnemy[] pool = sim.EnemyPool;
            for (int i = 0; i < sim.EnemyLimit; i++) if (pool[i].Active && !pool[i].IsBoss) sim.DamageEnemyForTests(pool[i], 1f); // regen path active
            sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];
            for (int i = 0; i < 300; i++) { sim.Step(new SurvivorInput(i % 9, i % 5, 0)); observation.Write(sim, values); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 300; i++) { sim.Step(new SurvivorInput(i % 9, i % 5, 0)); observation.Write(sim, values); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sim.IsEnded, Is.False); Assert.That(boss.Active, Is.True); Assert.That(allocated, Is.EqualTo(0L));
        }

        private static SurvivorSim Sim(int tier, Action<SurvivorTuning> tune = null, int seed = 11)
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(tier);
            tune?.Invoke(config.Tuning);
            return new SurvivorSim(config, seed);
        }

        private static int CountSpawns(SurvivorSim sim, int ticks)
        {
            sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests();
            return CountEvents(sim, SurvivorEventType.EnemySpawned, ticks);
        }

        private static int CountEvents(SurvivorSim sim, SurvivorEventType type, int ticks)
        {
            sim.SetHeroInvulnerableForTests();
            int count = 0;
            for (int t = 0; t < ticks && !sim.IsEnded; t++)
            {
                sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : default);
                IReadOnlyList<SurvivorEvent> events = sim.Events;
                for (int i = 0; i < events.Count; i++) if (events[i].Type == type) count++;
            }
            return count;
        }

        /// <summary>Distance a lone enemy of <paramref name="type"/> 15 m away walks toward the hero in one tick.</summary>
        private static float OneTickDistance(int tier, int type)
        {
            SurvivorSim sim = Sim(tier); sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests();
            SurvivorEnemy e = sim.SpawnEnemyForTests(type, new Vec2(15f, 0f));
            Vec2 start = e.Position; sim.Step(default);
            return (e.Position - start).Length;
        }

        private static int BrutesAfter60s(int tier)
        {
            SurvivorSim sim = Sim(tier); sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests(); sim.SetTimeForTests(60f);
            int brutes = 0;
            for (int t = 0; t < 60 * 60; t++)
            {
                sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : default);
                IReadOnlyList<SurvivorEvent> events = sim.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i].Type != SurvivorEventType.EnemySpawned) continue;
                    SurvivorEnemy[] pool = sim.EnemyPool;
                    for (int k = 0; k < sim.EnemyLimit; k++) if (pool[k].Active && pool[k].Id == events[i].Id && pool[k].TypeIndex == 2) brutes++;
                }
            }
            return brutes;
        }
    }
}
