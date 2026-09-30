using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorBehaviorTrackerTests
    {
        [Test]
        public void PositionMetrics_ReportAggressionRangeAndExploration()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(runSeconds: 60f), 123);
            sim.SpawnEnemyForTests(0, new Vec2(5f, 0f));
            SurvivorBehaviorStats stats = Track(sim, 5, new SurvivorInput(1, 0, 0));

            Assert.That(stats.Aggression, Is.EqualTo(1f));
            Assert.That(stats.Caution, Is.EqualTo(-1f));
            Assert.That(stats.KeepDistance, Is.GreaterThan(4f).And.LessThan(5f));
            Assert.That(stats.PreferredRange, Is.GreaterThan(4f).And.LessThan(5f));
            Assert.That(stats.Exploration, Is.EqualTo(1f / 400f));
        }

        [Test]
        public void HurtHeroMovingAway_IsCautious()
        {
            SurvivorSim sim = NewSim();
            sim.SetHeroHpForTests(sim.Hero.MaxHp * 0.2f);
            sim.SpawnEnemyForTests(0, new Vec2(-5f, 0f));

            SurvivorBehaviorStats stats = Track(sim, 5, new SurvivorInput(1, 0, 0));

            Assert.That(stats.Caution, Is.EqualTo(1f));
        }

        [Test]
        public void NoEnemies_UsesNoDataSentinels()
        {
            SurvivorBehaviorStats stats = Track(NewSim(), 5, new SurvivorInput(1, 0, 0));

            Assert.That(stats.Aggression, Is.EqualTo(-1f));
            Assert.That(stats.Caution, Is.EqualTo(-1f));
            Assert.That(stats.KeepDistance, Is.EqualTo(-1f));
            Assert.That(stats.PreferredRange, Is.EqualTo(-1f));
            Assert.That(stats.BossHunting, Is.EqualTo(-1f));
            Assert.That(stats.Greed, Is.EqualTo(-1f));
        }

        [Test]
        public void Greed_IsCollectedShareOfGoldAndMeatDrops()
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(runSeconds: 60f);
            config.Tuning.GoldChance = 1f; config.Tuning.MeatChance = 0f;
            SurvivorSim sim = new SurvivorSim(config, 12);
            SurvivorEnemy enemy = sim.SpawnEnemyForTests(0, new Vec2(2f, 0f));
            SurvivorBehaviorTracker tracker = new SurvivorBehaviorTracker(); tracker.Reset(sim);
            sim.DamageEnemyForTests(enemy, enemy.MaxHp + 1f);
            sim.SetHeroStateForTests(enemy.Position, Vec2.Zero, 0f);
            sim.Step(default); tracker.Observe(sim);

            SurvivorBehaviorStats stats = tracker.Finish(sim);

            Assert.That(sim.DropsSpawned, Is.EqualTo(1));
            Assert.That(sim.DropsCollected, Is.EqualTo(1));
            Assert.That(stats.Greed, Is.EqualTo(1f));
        }

        [Test]
        public void KickWithThreeHits_IsEffectiveCrowdControl()
        {
            SurvivorSim sim = NewSim();
            sim.SpawnEnemyForTests(0, new Vec2(1f, 0f));
            sim.SpawnEnemyForTests(0, new Vec2(1.1f, 0.2f));
            sim.SpawnEnemyForTests(0, new Vec2(1.1f, -0.2f));
            SurvivorBehaviorTracker tracker = new SurvivorBehaviorTracker(); tracker.Reset(sim);

            sim.Step(new SurvivorInput(0, 1, 0)); tracker.Observe(sim);
            SurvivorBehaviorStats stats = tracker.Finish(sim);

            Assert.That(stats.KickUses, Is.EqualTo(1));
            Assert.That(stats.EffectiveKicks, Is.EqualTo(1));
            Assert.That(stats.CrowdControl, Is.EqualTo(1f));
            Assert.That(stats.SkillDiscipline, Is.EqualTo(1f));
        }

        [Test]
        public void BlockedEvent_MakesBlockEffective()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(runSeconds: 60f), 123);
            SurvivorEnemy enemy = sim.SpawnEnemyForTests(2, new Vec2(1f, 0f));
            SurvivorBehaviorTracker tracker = new SurvivorBehaviorTracker(); tracker.Reset(sim);

            sim.Step(new SurvivorInput(0, 2, 0));
            sim.DamageHeroForTests(10f, enemy, true);
            tracker.Observe(sim);
            sim.Step(default); tracker.Observe(sim);
            SurvivorBehaviorStats stats = tracker.Finish(sim);

            Assert.That(stats.BlockUses, Is.EqualTo(1));
            Assert.That(stats.EffectiveBlocks, Is.EqualTo(1));
        }

        [Test]
        public void DashIsCountedAfterDelay_AndEffectiveWhenItEscapesEnemies()
        {
            SurvivorSim sim = NewSim();
            SurvivorEnemy enemy = sim.SpawnEnemyForTests(2, new Vec2(1f, 0f));
            SurvivorBehaviorTracker tracker = new SurvivorBehaviorTracker(); tracker.Reset(sim);
            sim.Step(new SurvivorInput(1, 3, 0)); enemy.Position = sim.Hero.Position; tracker.Observe(sim);
            enemy.Active = false;
            for (int i = 0; i < 30; i++) { sim.Step(new SurvivorInput(1, 0, 0)); tracker.Observe(sim); }
            SurvivorBehaviorStats stats = tracker.Finish(sim);

            Assert.That(stats.DashUses, Is.EqualTo(1));
            Assert.That(stats.EffectiveDashes, Is.EqualTo(1));
        }

        [Test]
        public void DashEndingBeforeDelay_IsIgnored()
        {
            SurvivorSim sim = NewSim(); SurvivorBehaviorTracker tracker = new SurvivorBehaviorTracker(); tracker.Reset(sim);
            sim.Step(new SurvivorInput(1, 3, 0)); tracker.Observe(sim);

            SurvivorBehaviorStats stats = tracker.Finish(sim);

            Assert.That(stats.DashUses, Is.EqualTo(0));
            Assert.That(stats.SkillDiscipline, Is.EqualTo(-1f));
        }

        [Test]
        public void BossHunting_UsesBossDirection()
        {
            SurvivorSim sim = NewSim();
            sim.SpawnEnemyForTests(4, new Vec2(10f, 0f));
            SurvivorBehaviorStats stats = Track(sim, 5, new SurvivorInput(1, 0, 0));

            Assert.That(stats.BossHunting, Is.EqualTo(1f));
        }

        private static SurvivorSim NewSim()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(runSeconds: 60f), 123);
            sim.SetHeroInvulnerableForTests();
            return sim;
        }

        private static SurvivorBehaviorStats Track(SurvivorSim sim, int ticks, SurvivorInput input)
        {
            SurvivorBehaviorTracker tracker = new SurvivorBehaviorTracker(); tracker.Reset(sim);
            for (int i = 0; i < ticks; i++) { sim.Step(input); tracker.Observe(sim); }
            return tracker.Finish(sim);
        }
    }
}
