using System;
using System.Diagnostics;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorObservationTests
    {
        [Test]
        public void Observation_SizeIs2264_AndOffsets()
        {
            Assert.That(SurvivorObservation.Size, Is.EqualTo(2264)); Assert.That(SurvivorObservation.InventoryOffset, Is.EqualTo(64)); Assert.That(SurvivorObservation.OffersOffset, Is.EqualTo(128)); Assert.That(SurvivorObservation.RaysOffset, Is.EqualTo(392)); Assert.That(SurvivorObservation.DensityOffset, Is.EqualTo(2192));
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 4); sim.SetHeroStateForTests(Vec2.Zero, new Vec2(2, 0), 0f); SurvivorEnemy enemy = sim.SpawnEnemyForTests(0, new Vec2(5, 0)); sim.SetEnemyVelocityForTests(enemy, new Vec2(-2, 0)); sim.SpawnPickupForTests(PickupKind.Gem, new Vec2(8, 0), 10f); sim.GiveXpForTests(5f); sim.Step(default);
            float[] values = new float[SurvivorObservation.Size]; new SurvivorObservation().Write(sim, values);
            Assert.That(values[48 + sim.LastMove], Is.EqualTo(1f)); Assert.That(values[57 + sim.LastSkill], Is.EqualTo(1f)); Assert.That(values[62], Is.EqualTo(MathF.Cos(sim.Hero.Facing)).Within(0.001f)); Assert.That(values[64], Is.EqualTo(0.2f));
            Assert.That(values[SurvivorObservation.OffersOffset + 65], Is.EqualTo(1f)); Assert.That(values[SurvivorObservation.RaysOffset + 2], Is.EqualTo(1f)); Assert.That(values[SurvivorObservation.RaysOffset + 18], Is.EqualTo(1f)); Assert.That(values[SurvivorObservation.DensityOffset], Is.GreaterThan(0f));
        }

        [Test]
        public void Observation_ApproachSpeed_Sign()
        {
            SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size]; SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 1); SurvivorEnemy enemy = sim.SpawnEnemyForTests(0, new Vec2(4, 0)); sim.SetEnemyVelocityForTests(enemy, new Vec2(-2, 0)); observation.Write(sim, values);
            Assert.That(values[SurvivorObservation.RaysOffset + 16], Is.Positive, "ray"); Assert.That(values[SurvivorObservation.DensityOffset + 2], Is.Positive, "density");
            sim.SetEnemyVelocityForTests(enemy, new Vec2(2, 0)); observation.Write(sim, values); Assert.That(values[SurvivorObservation.RaysOffset + 16], Is.Negative); Assert.That(values[SurvivorObservation.DensityOffset + 2], Is.Negative);
            SurvivorSim wall = new SurvivorSim(SurvivorTestHelpers.Config(), 2); wall.SetHeroStateForTests(new Vec2(40, 0), new Vec2(2, 0), 0); observation.Write(wall, values); Assert.That(values[SurvivorObservation.RaysOffset + 16], Is.Positive);
        }

        [Test]
        public void Observation_AllValuesFiniteAndInRange()
        {
            SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];
            for (int seed = 0; seed < 3; seed++)
            {
                SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), seed); sim.SetHeroInvulnerableForTests(); Rng rng = new Rng(seed + 100);
                for (int tick = 0; tick < 7200 && !sim.IsEnded; tick++) { SurvivorInput input = sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : new SurvivorInput(rng.NextInt(9), rng.NextInt(5), 0); sim.Step(input); if (tick % 60 == 0) AssertFinite(observation, sim, values); }
            }
            SurvivorSim late = PopulatedSim(); observation.Write(late, values); AssertFinite(values);
        }

        [Test]
        public void ActionMask_PickOnlyWhenOffer_SkillsMaskedOnCooldownAndEnergy()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 3); bool[] move = new bool[9], skill = new bool[5], pick = new bool[5]; SurvivorActionMask.WriteMask(sim, move, skill, pick);
            Assert.That(move, Is.All.True); Assert.That(pick[0], Is.True); Assert.That(pick[1], Is.False); Assert.That(skill[4], Is.False);
            sim.Step(new SurvivorInput(0, 1, 0)); SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[1], Is.False);
            sim.GiveXpForTests(5f); sim.Step(default); SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(move[0], Is.True); Assert.That(move[1], Is.False); Assert.That(skill[0], Is.True); Assert.That(skill[1], Is.False); Assert.That(pick[1], Is.True);
        }

        [Test]
        public void Performance_LateGame()
        {
            SurvivorSim sim = PopulatedSim(); SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size]; sim.SetHeroInvulnerableForTests();
            sim.SetEnemiesInvulnerableForTests();
            sim.DisablePickupCollectionForTests();
            for (int i = 0; i < 200; i++) { sim.Step(default); observation.Write(sim, values); }
            const int samples = 1000;
            Stopwatch watch = Stopwatch.StartNew(); for (int i = 0; i < samples; i++) sim.Step(default); watch.Stop(); double stepMs = watch.Elapsed.TotalMilliseconds / samples;
            watch.Restart(); for (int i = 0; i < samples; i++) observation.Write(sim, values); watch.Stop(); double writeMs = watch.Elapsed.TotalMilliseconds / samples;
            TestContext.Progress.WriteLine($"Late-game Step {stepMs:0.0000} ms; Write {writeMs:0.0000} ms; t={sim.Time:0.0}, ended={sim.IsEnded}, pick={sim.IsAwaitingPick}"); Assert.That(stepMs, Is.LessThan(0.4), "Debug budget; Release target is 0.05 ms"); Assert.That(writeMs, Is.LessThan(1.0));
        }

        private static SurvivorSim PopulatedSim()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 55);
            for (int i = 0; i < 250; i++) { float angle = i * 2.399963f; float radius = 6f + i % 24; sim.SpawnEnemyForTests(i % 3, Vec2.FromAngle(angle) * radius); }
            for (int i = 0; i < 400; i++) { float angle = i * 1.7f; sim.SpawnGemForTests(Vec2.FromAngle(angle) * (3f + i % 27), 1 + i % 8); }
            return sim;
        }
        private static void AssertFinite(SurvivorObservation observation, SurvivorSim sim, float[] values) { observation.Write(sim, values); AssertFinite(values); }
        private static void AssertFinite(float[] values) { for (int i = 0; i < values.Length; i++) { Assert.That(float.IsFinite(values[i]), Is.True, "index " + i); Assert.That(values[i], Is.InRange(-1f, 1f), "index " + i); } }
    }
}
