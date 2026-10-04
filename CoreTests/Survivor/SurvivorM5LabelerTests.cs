using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorM5LabelerTests
    {
        private const float Tick = SurvivorSim.FixedDeltaTime;

        [Test]
        public void DisplayNames()
        {
            Assert.That(SpectatorLabels.DisplayName(SpectatorLabel.None), Is.EqualTo(""));
            Assert.That(SpectatorLabels.DisplayName(SpectatorLabel.Kiting), Is.EqualTo("KITING"));
            Assert.That(SpectatorLabels.DisplayName(SpectatorLabel.Looting), Is.EqualTo("LOOTING GOLD"));
            Assert.That(SpectatorLabels.DisplayName(SpectatorLabel.Charging), Is.EqualTo("CHARGING IN"));
            Assert.That(SpectatorLabels.DisplayName(SpectatorLabel.Escaping), Is.EqualTo("BREAKING OUT"));
        }

        [Test]
        public void StandingStill_IsNone()
        {
            SurvivorSim sim = NewSim();
            Group(sim, 6f);
            sim.SpawnPickupForTests(PickupKind.Gold, new Vec2(2f, 0f), 5f);
            Assert.That(new SpectatorLabeler().ComputeRaw(sim), Is.EqualTo(SpectatorLabel.None));
            sim.SetHeroStateForTests(Vec2.Zero, new Vec2(0.2f * sim.DerivedStats.MoveSpeed, 0f), 0f);
            Assert.That(new SpectatorLabeler().ComputeRaw(sim), Is.EqualTo(SpectatorLabel.None), "below 30% of max speed");
        }

        [Test]
        public void SurroundedAndMovingOut_IsEscaping()
        {
            SurvivorSim sim = NewSim();
            foreach (Vec2 p in new[] { new Vec2(2f, 0f), new Vec2(2f, 1f), new Vec2(2f, -1f), new Vec2(2.5f, 0.5f) }) sim.SpawnEnemyForTests(0, p);
            SpectatorLabeler labeler = new SpectatorLabeler();
            Move(sim, new Vec2(-1f, 0f)); Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.Escaping));
            Move(sim, new Vec2(0f, 1f)); Assert.That(labeler.ComputeRaw(sim), Is.Not.EqualTo(SpectatorLabel.Escaping), "sideways is not away");
            Move(sim, new Vec2(1f, 0f)); Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.Charging), "into the group");
        }

        [Test]
        public void LowHpWithEnemyClose_MovingAway_IsEscaping()
        {
            SurvivorSim sim = NewSim();
            sim.SpawnEnemyForTests(0, new Vec2(3f, 0f));
            SpectatorLabeler labeler = new SpectatorLabeler();
            Move(sim, new Vec2(-1f, 0f));
            Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.None), "full HP, one enemy");
            sim.SetHeroHpForTests(sim.Hero.MaxHp * 0.3f);
            Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.Escaping));
        }

        [Test]
        public void GoldAheadWithinSixMetres_IsLooting()
        {
            SurvivorSim sim = NewSim();
            SurvivorPickup gold = sim.SpawnPickupForTests(PickupKind.Gold, new Vec2(4f, 1f), 5f);
            SpectatorLabeler labeler = new SpectatorLabeler();
            Move(sim, new Vec2(1f, 0f)); Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.Looting));
            Move(sim, new Vec2(0f, 1f)); Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.None), "outside the 45° cone");
            gold.Position = new Vec2(7f, 0f);
            Move(sim, new Vec2(1f, 0f)); Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.None), "beyond 6 m");
            gold.Position = new Vec2(0f, -5f); gold.Kind = PickupKind.Gem;
            Move(sim, new Vec2(0f, -1f)); Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.None), "XP gems are not loot");
            gold.Kind = PickupKind.Chest; Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.Looting), "chest");
        }

        [Test]
        public void MovingIntoAGroup_IsCharging()
        {
            SurvivorSim sim = NewSim();
            Group(sim, 6f);
            SpectatorLabeler labeler = new SpectatorLabeler();
            Move(sim, new Vec2(1f, 0f)); Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.Charging));
            Move(sim, new Vec2(1f, 1.5f)); Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.Charging), "dot 0.55 is above 0.5");
            Move(sim, new Vec2(1f, 2f)); Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.None), "dot 0.45 is below 0.5");
        }

        [Test]
        public void TwoEnemies_AreNotAGroup()
        {
            SurvivorSim sim = NewSim();
            sim.SpawnEnemyForTests(0, new Vec2(6f, 0.5f)); sim.SpawnEnemyForTests(0, new Vec2(6f, -0.5f));
            sim.SpawnEnemyForTests(0, new Vec2(11f, 0f));
            Move(sim, new Vec2(1f, 0f));
            Assert.That(new SpectatorLabeler().ComputeRaw(sim), Is.EqualTo(SpectatorLabel.None));
        }

        [Test]
        public void MovingAwayWhileHitting_IsKiting()
        {
            SurvivorSim sim = NewSim();
            SurvivorEnemy[] group = Group(sim, 6f);
            SpectatorLabeler labeler = new SpectatorLabeler();
            Move(sim, new Vec2(-1f, 0f));
            Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.None), "no recent damage");
            sim.SetTimeForTests(10f);
            sim.DamageEnemyForTests(group[0], 1f);
            labeler.Observe(sim, 0f);
            Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.Kiting));
            sim.SetTimeForTests(10.9f); Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.Kiting), "within 1 s");
            sim.SetTimeForTests(11.2f); Assert.That(labeler.ComputeRaw(sim), Is.EqualTo(SpectatorLabel.None), "damage too old");
        }

        [Test]
        public void ShortRawFlip_DoesNotChangeCurrent_AndSecondsInCountsShownTime()
        {
            SpectatorLabeler labeler = new SpectatorLabeler();
            Feed(labeler, SpectatorLabel.Charging, 29); Assert.That(labeler.Current, Is.EqualTo(SpectatorLabel.None));
            Feed(labeler, SpectatorLabel.Charging, 1); Assert.That(labeler.Current, Is.EqualTo(SpectatorLabel.Charging), "after 0.5 s");
            Feed(labeler, SpectatorLabel.Charging, 60);
            Feed(labeler, SpectatorLabel.Looting, 29); Assert.That(labeler.Current, Is.EqualTo(SpectatorLabel.Charging), "flip shorter than 0.5 s");
            Assert.That(labeler.Raw, Is.EqualTo(SpectatorLabel.Looting));
            Feed(labeler, SpectatorLabel.Charging, 10); Assert.That(labeler.Current, Is.EqualTo(SpectatorLabel.Charging));
            Feed(labeler, SpectatorLabel.Looting, 30); Assert.That(labeler.Current, Is.EqualTo(SpectatorLabel.Looting));

            Assert.That(labeler.SecondsIn(SpectatorLabel.None), Is.EqualTo(29 * Tick).Within(1e-4f));
            Assert.That(labeler.SecondsIn(SpectatorLabel.Charging), Is.EqualTo(129 * Tick).Within(1e-4f));
            Assert.That(labeler.SecondsIn(SpectatorLabel.Looting), Is.EqualTo(1 * Tick).Within(1e-4f));
            Assert.That(labeler.TotalSeconds, Is.EqualTo(159 * Tick).Within(1e-4f));
            labeler.Reset();
            Assert.That(labeler.Current, Is.EqualTo(SpectatorLabel.None)); Assert.That(labeler.TotalSeconds, Is.EqualTo(0f));
            Assert.That(labeler.SecondsIn(SpectatorLabel.Charging), Is.EqualTo(0f));
        }

        [Test]
        public void ShownLabel_StaysAtLeastOneSecond()
        {
            SpectatorLabeler labeler = new SpectatorLabeler();
            Feed(labeler, SpectatorLabel.Charging, 30); Assert.That(labeler.Current, Is.EqualTo(SpectatorLabel.Charging));
            Feed(labeler, SpectatorLabel.None, 55); Assert.That(labeler.Current, Is.EqualTo(SpectatorLabel.Charging), "held 0.5 s but shown < 1 s");
            Feed(labeler, SpectatorLabel.None, 5); Assert.That(labeler.Current, Is.EqualTo(SpectatorLabel.None));
        }

        [Test]
        public void ZeroDt_ChangesNothing()
        {
            SurvivorSim sim = NewSim(); Group(sim, 6f); Move(sim, new Vec2(1f, 0f));
            SpectatorLabeler labeler = new SpectatorLabeler();
            for (int i = 0; i < 100; i++) labeler.Observe(sim, 0f);
            Assert.That(labeler.Current, Is.EqualTo(SpectatorLabel.None)); Assert.That(labeler.TotalSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void ObserveAfterSteps_ShowsCharging()
        {
            SurvivorSim sim = NewSim(); sim.SetEnemiesInvulnerableForTests();
            foreach (Vec2 p in new[] { new Vec2(8f, 0f), new Vec2(8f, 1f), new Vec2(8f, -1f) }) sim.SpawnEnemyForTests(2, p);
            SpectatorLabeler labeler = new SpectatorLabeler();
            for (int i = 0; i < 45; i++) { sim.Step(new SurvivorInput(1, 0, 0)); labeler.Observe(sim, sim.LastStepSeconds); }
            Assert.That(labeler.Current, Is.EqualTo(SpectatorLabel.Charging));
            Assert.That(labeler.SecondsIn(SpectatorLabel.Charging), Is.GreaterThan(0f));
            Assert.That(labeler.TotalSeconds, Is.EqualTo(45 * Tick).Within(1e-4f));
        }

        private static SurvivorSim NewSim()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(runSeconds: 60f), 11);
            sim.SetHeroInvulnerableForTests();
            return sim;
        }

        private static SurvivorEnemy[] Group(SurvivorSim sim, float x) => new[]
        {
            sim.SpawnEnemyForTests(0, new Vec2(x, 0f)), sim.SpawnEnemyForTests(0, new Vec2(x, 1f)), sim.SpawnEnemyForTests(0, new Vec2(x, -1f))
        };

        /// <summary>Hero at the origin moving at full speed along <paramref name="direction"/>.</summary>
        private static void Move(SurvivorSim sim, Vec2 direction) =>
            sim.SetHeroStateForTests(Vec2.Zero, direction.Normalized() * sim.DerivedStats.MoveSpeed, 0f);

        private static void Feed(SpectatorLabeler labeler, SpectatorLabel raw, int ticks)
        {
            for (int i = 0; i < ticks; i++) labeler.ObserveRaw(raw, Tick);
        }
    }
}
