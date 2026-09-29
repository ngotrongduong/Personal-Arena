using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorEvaluatorTests
    {
        [Test]
        public void Pilot_RejectsWrongSizes()
        {
            Assert.That(SurvivorPilot.Validate(SurvivorTestHelpers.Brain(100)), Is.Not.Null); Assert.That(SurvivorPilot.Validate(SurvivorTestHelpers.Brain(move: 8)), Is.Not.Null); Assert.That(SurvivorPilot.Validate(SurvivorTestHelpers.Brain()), Is.Null);
        }

        [Test]
        public void Evaluator_RunsShortRuns_AndSummarizes()
        {
            SurvivorEvaluator evaluator = new SurvivorEvaluator(); List<SurvivorRunStats> runs = new List<SurvivorRunStats>();
            for (int seed = 0; seed < 3; seed++) runs.Add(evaluator.RunOne(SurvivorTestHelpers.Brain(), SurvivorTestHelpers.Config(runSeconds: 60f), seed, true));
            SurvivorEvalSummary summary = evaluator.Summarize(runs); Assert.That(summary.Runs, Is.EqualTo(3)); Assert.That(summary.P10SurvivedSeconds, Is.EqualTo(Min(runs))); Assert.That(summary.MedianSurvivedSeconds, Is.GreaterThanOrEqualTo(summary.P10SurvivedSeconds));
            Assert.That(evaluator.PassesM4A(new SurvivorEvalSummary { MedianSurvivedSeconds = 600, P10SurvivedSeconds = 420, CatastrophicCount = 0 }), Is.True); Assert.That(evaluator.PassesM4A(new SurvivorEvalSummary { MedianSurvivedSeconds = 599, P10SurvivedSeconds = 420 }), Is.False);
        }

        [Test]
        public void Determinism_TwoSimsSameSeed()
        {
            SurvivorSim a = new SurvivorSim(SurvivorTestHelpers.Config(), 99); SurvivorSim b = new SurvivorSim(SurvivorTestHelpers.Config(), 99);
            for (int tick = 0; tick < 10800 && !a.IsEnded && !b.IsEnded; tick++)
            {
                SurvivorInput input = a.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : new SurvivorInput((tick / 30) % 9, (tick / 120) % 4, 0); a.Step(input); b.Step(input);
                Assert.That(Hash(a), Is.EqualTo(Hash(b)), "tick " + tick);
            }
        }

        private static float Min(List<SurvivorRunStats> runs) { float min = float.MaxValue; for (int i = 0; i < runs.Count; i++) if (runs[i].SurvivedSeconds < min) min = runs[i].SurvivedSeconds; return min; }
        private static long Hash(SurvivorSim sim)
        {
            long hash = 17; hash = hash * 31 + sim.Time.GetHashCode(); hash = hash * 31 + sim.Hero.Position.GetHashCode(); hash = hash * 31 + sim.Hero.Hp.GetHashCode(); hash = hash * 31 + sim.Level; hash = hash * 31 + sim.Kills;
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active) { hash = hash * 31 + sim.Enemies[i].Id; hash = hash * 31 + sim.Enemies[i].Position.GetHashCode(); hash = hash * 31 + sim.Enemies[i].Hp.GetHashCode(); }
            return hash;
        }
    }
}
