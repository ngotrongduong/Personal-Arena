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
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.OldConfig(), 3); bool[] move = new bool[9], skill = new bool[5], pick = new bool[5]; SurvivorActionMask.WriteMask(sim, move, skill, pick);
            Assert.That(move, Is.All.True); Assert.That(pick[0], Is.True); Assert.That(pick[1], Is.False); Assert.That(skill[4], Is.False);
            sim.Step(new SurvivorInput(0, 1, 0)); SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[1], Is.False);
            sim.GiveXpForTests(5f); sim.Step(default); SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(move[0], Is.True); Assert.That(move[1], Is.False); Assert.That(skill[0], Is.True); Assert.That(skill[1], Is.False); Assert.That(pick[1], Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Performance_LateGame(bool fullKit)
        {
            SurvivorSim sim = PopulatedSim(fullKit); SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size]; sim.SetHeroInvulnerableForTests();
            sim.SetEnemiesInvulnerableForTests();
            sim.DisablePickupCollectionForTests();
            // Long warm-up so tiered JIT reaches optimized code; the best of several batches filters scheduler noise.
            // (tier-up needs ~30 calls plus a quiet background delay, hence a wall-clock minimum as well).
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 600 || watch.ElapsedMilliseconds < 600; i++) { sim.Step(default); observation.Write(sim, values); }
            const int batches = 5, samples = 200;
            double stepMs = double.MaxValue, writeMs = double.MaxValue;
            for (int batch = 0; batch < batches; batch++)
            {
                watch.Restart(); for (int i = 0; i < samples; i++) sim.Step(default); watch.Stop(); stepMs = Math.Min(stepMs, watch.Elapsed.TotalMilliseconds / samples);
                watch.Restart(); for (int i = 0; i < samples; i++) observation.Write(sim, values); watch.Stop(); writeMs = Math.Min(writeMs, watch.Elapsed.TotalMilliseconds / samples);
            }
            TestContext.Progress.WriteLine($"Late-game Step {stepMs:0.0000} ms; Write {writeMs:0.0000} ms; alive={sim.AliveEnemyCount}, t={sim.Time:0.0}, ended={sim.IsEnded}, pick={sim.IsAwaitingPick}");
            Assert.That(sim.IsEnded, Is.False); Assert.That(sim.AliveEnemyCount, Is.GreaterThanOrEqualTo(250));
#if DEBUG
            // Debug builds skip JIT optimizations; only catch gross regressions there. CI runs Release.
            const double stepBudgetMs = 1.0, writeBudgetMs = 2.0;
#else
            // Release target is 0.05 ms; the margin absorbs shared CI runners and a busy PC.
            const double stepBudgetMs = 0.2, writeBudgetMs = 1.0;
#endif
            Assert.That(stepMs, Is.LessThan(stepBudgetMs)); Assert.That(writeMs, Is.LessThan(writeBudgetMs));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Step_Write_WriteMask_DoNotAllocate(bool fullKit)
        {
            SurvivorSim sim = PopulatedSim(fullKit); sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];
            bool[] move = new bool[SurvivorInput.MoveBranchSize], skill = new bool[SurvivorInput.SkillBranchSize], pick = new bool[SurvivorInput.PickBranchSize];
            for (int i = 0; i < 300; i++) { sim.Step(new SurvivorInput(i % 9, i % 5, 0)); observation.Write(sim, values); SurvivorActionMask.WriteMask(sim, move, skill, pick); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 300; i++) { sim.Step(new SurvivorInput(i % 9, i % 5, 0)); observation.Write(sim, values); SurvivorActionMask.WriteMask(sim, move, skill, pick); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sim.IsEnded, Is.False); Assert.That(allocated, Is.EqualTo(0L));
        }

        [Test]
        public void Rays_WallDistanceAndOpenSpace_Exact()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 1); sim.SetHeroStateForTests(new Vec2(40f, 0f), Vec2.Zero, 0f);
            float[] values = new float[SurvivorObservation.Size]; new SurvivorObservation().Write(sim, values);
            int east = SurvivorObservation.RaysOffset, north = SurvivorObservation.RaysOffset + 18 * SurvivorObservation.RayStride;
            Assert.That(values[east + 1], Is.EqualTo(1f), "east ray hits the wall"); Assert.That(values[east + 12], Is.EqualTo(0.5f).Within(1e-5f), "10 m of 20 m");
            Assert.That(values[east + 0], Is.EqualTo(0f));
            Assert.That(values[north + 0], Is.EqualTo(1f), "north wall is beyond range"); Assert.That(values[north + 1], Is.EqualTo(0f)); Assert.That(values[north + 12], Is.EqualTo(1f));
        }

        [Test]
        public void Density_ExactCells()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 1); sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 0f);
            SurvivorEnemy near = sim.SpawnEnemyForTests(0, new Vec2(3f, 0f)); sim.SetEnemyVelocityForTests(near, new Vec2(-2f, 0f));
            sim.SpawnEnemyForTests(0, new Vec2(0f, 7f)); sim.SpawnGemForTests(new Vec2(0f, -3f), 10f);
            float[] values = new float[SurvivorObservation.Size]; new SurvivorObservation().Write(sim, values);
            int d = SurvivorObservation.DensityOffset;
            // cell = ring * 8 + sector; channels: count / 20, gem xp / 50, mean approach speed / 5.
            Assert.That(values[d + 0], Is.EqualTo(0.05f).Within(1e-6f), "ring 0, east: one enemy");
            Assert.That(values[d + 2], Is.EqualTo(0.4f).Within(1e-6f), "approaching at 2 m/s");
            Assert.That(values[d + (8 + 2) * 3], Is.EqualTo(0.05f).Within(1e-6f), "ring 1, north: one enemy");
            Assert.That(values[d + (8 + 2) * 3 + 2], Is.EqualTo(0f));
            Assert.That(values[d + 6 * 3 + 1], Is.EqualTo(0.2f).Within(1e-6f), "ring 0, south: 10 xp");
            float sum = 0f; for (int i = d; i < SurvivorObservation.Size; i++) sum += values[i];
            Assert.That(sum, Is.EqualTo(0.05f + 0.4f + 0.05f + 0.2f).Within(1e-5f), "no other density cell is set");
        }

        private static SurvivorSim PopulatedSim(bool fullKit = false)
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 55);
            // Full kit = worst case: all 6 weapons at level 5 plus spitters firing projectiles.
            if (fullKit) for (int item = 0; item <= 5; item++) sim.GiveItemForTests(item, 5);
            int types = fullKit ? 4 : 3;
            for (int i = 0; i < 250; i++) { float angle = i * 2.399963f; float radius = 6f + i % 24; sim.SpawnEnemyForTests(i % types, Vec2.FromAngle(angle) * radius); }
            for (int i = 0; i < 400; i++) { float angle = i * 1.7f; sim.SpawnGemForTests(Vec2.FromAngle(angle) * (3f + i % 27), 1 + i % 8); }
            return sim;
        }
        private static void AssertFinite(SurvivorObservation observation, SurvivorSim sim, float[] values) { observation.Write(sim, values); AssertFinite(values); }
        private static void AssertFinite(float[] values) { for (int i = 0; i < values.Length; i++) { Assert.That(float.IsFinite(values[i]), Is.True, "index " + i); Assert.That(values[i], Is.InRange(-1f, 1f), "index " + i); } }
    }
}
