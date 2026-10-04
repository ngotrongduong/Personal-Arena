using System;
using System.Diagnostics;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>
    /// Late-game cost of <see cref="SurvivorSim.Step"/> with the M9 content: per class, the heaviest six weapons (base or
    /// evolved), the volley passives, and every skill tried each tick, against 250 enemies and 400 gems. Same method as
    /// <c>SurvivorObservationTests.Performance_LateGame</c> (JIT warm-up, best of several batches).
    /// </summary>
    public sealed class SurvivorM9PerformanceTests
    {
        private static readonly int[] Passives = { 35, 34, 11, 37, 10, 8 }; // duplicator, duration, area, omni, hourglass, might

        public static readonly object[] Kits =
        {
            new object[] { "warrior-base", "warrior", new[] { 27, 29, 32, 33, 64, 30 } },
            new object[] { "warrior-evolved", "warrior", new[] { 113, 115, 118, 119, 120, 116 } },
            new object[] { "mage-base", "mage", new[] { 68, 66, 69, 72, 33, 26 } },
            new object[] { "mage-evolved", "mage", new[] { 123, 122, 124, 127, 119, 112 } },
            new object[] { "archer-base", "archer", new[] { 65, 71, 70, 32, 33, 64 } },
            new object[] { "archer-evolved", "archer", new[] { 121, 126, 125, 118, 119, 120 } },
        };

        [TestCaseSource(nameof(Kits))]
        public void Performance_LateGame_M9Kit(string name, string hero, int[] weapons)
        {
            SurvivorConfig config = hero == "mage" ? new SurvivorConfig { ClassDef = SurvivorDefaults.Mage() }
                : hero == "archer" ? new SurvivorConfig { ClassDef = SurvivorDefaults.Archer() } : new SurvivorConfig();
            SurvivorSim sim = new SurvivorSim(config, 55);
            for (int i = 0; i < weapons.Length; i++) sim.GiveItemForTests(weapons[i], SurvivorCatalog.Get(weapons[i]).EvolvesFrom >= 0 ? 1 : 5);
            for (int i = 0; i < Passives.Length; i++) sim.GiveItemForTests(Passives[i], Math.Min(5, SurvivorCatalog.Get(Passives[i]).MaxLevel));
            for (int i = 0; i < 250; i++) { float angle = i * 2.399963f; float radius = 6f + i % 24; sim.SpawnEnemyForTests(i % 4, Vec2.FromAngle(angle) * radius); }
            for (int i = 0; i < 400; i++) { float angle = i * 1.7f; sim.SpawnGemForTests(Vec2.FromAngle(angle) * (3f + i % 27), 1 + i % 8); }
            sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];

            int tick = 0;
            // Moves in a circle (feeds momentum) and tries a skill every tick; masked or cooling skills are simply refused.
            SurvivorInput Next() { tick++; return new SurvivorInput(1 + (tick / 20) % 8, 1 + tick % 6, 0); }
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 600 || watch.ElapsedMilliseconds < 600; i++) { sim.Step(Next()); observation.Write(sim, values); }
            const int batches = 5, samples = 200;
            double stepMs = double.MaxValue;
            for (int batch = 0; batch < batches; batch++)
            {
                watch.Restart(); for (int i = 0; i < samples; i++) sim.Step(Next()); watch.Stop();
                stepMs = Math.Min(stepMs, watch.Elapsed.TotalMilliseconds / samples);
            }
            TestContext.Progress.WriteLine($"M9 {name}: Step {stepMs:0.0000} ms; alive={sim.AliveEnemyCount}, projectiles active={ActiveProjectiles(sim)}, t={sim.Time:0.0}");
            Assert.That(sim.IsEnded, Is.False); Assert.That(sim.AliveEnemyCount, Is.GreaterThanOrEqualTo(250));
#if DEBUG
            const double stepBudgetMs = 1.0;
#else
            // Same margin as Performance_LateGame: the target is 0.05 ms, 0.2 absorbs shared CI runners.
            const double stepBudgetMs = 0.2;
#endif
            Assert.That(stepMs, Is.LessThan(stepBudgetMs));
        }

        private static int ActiveProjectiles(SurvivorSim sim)
        {
            int count = 0; for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active) count++;
            return count;
        }
    }
}
