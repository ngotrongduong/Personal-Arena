using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>
    /// Tier-1 goldens recorded on the code before T-023 (M5). They prove the tier modifiers, gold
    /// sources and other M5 additions leave tier-1 behaviour bit-identical.
    /// </summary>
    public sealed class SurvivorM5GoldenTests
    {
        // Re-recorded for the sword wave (the warrior's starting sword now throws a flying wave instead of an instant arc).
        private const long ScriptedGolden = 8083976984068779899L;
        private const long InvulnerableFirst300Golden = 3316066975718045987L;
        private const long InvulnerableGolden = 1550448328795120658L;
        private const long EvaluatorGolden = -1410093660554504573L;

        [Test]
        public void Tier1_ScriptedRun_MatchesPreM5Golden()
        {
            RequireWindowsGolden();
            SurvivorSim sim = new SurvivorSim(PreM8(SurvivorTestHelpers.Config()), 99);
            long hash = 17;
            for (int tick = 0; tick < 10800 && !sim.IsEnded; tick++)
            {
                SurvivorInput input = sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : new SurvivorInput((tick / 30) % 9, (tick / 120) % 4, 0);
                sim.Step(input); hash = hash * 31 + StateHash(sim);
            }
            Assert.That(hash, Is.EqualTo(ScriptedGolden), "end " + sim.EndReason + " at " + sim.Time);
        }

        /// <summary>
        /// M7 adds enemies from minute 5 on, so the pre-M5 behaviour is pinned up to 300 s (checked tick by tick
        /// against the pre-M7 code); the full run is pinned by a golden recorded on M7 (T-030).
        /// </summary>
        [Test]
        public void Tier1_InvulnerableRun_First300Seconds_MatchesPreM5Golden()
        {
            RequireWindowsGolden();
            Assert.That(InvulnerableHash(300f, out SurvivorSim sim), Is.EqualTo(InvulnerableFirst300Golden), "at " + sim.Time);
        }

        [Test]
        public void Tier1_InvulnerableFullRun_MatchesM7Golden()
        {
            RequireWindowsGolden();
            long hash = InvulnerableHash(float.PositiveInfinity, out SurvivorSim sim);
            Assert.That(hash, Is.EqualTo(InvulnerableGolden), "end " + sim.EndReason + " at " + sim.Time + " gold " + sim.Gold);
        }

        /// <summary>
        /// These goldens hash every float bit and were recorded on Windows. MathF.Sin/Cos/Atan2 come from the
        /// platform libm, so the chaotic runs drift apart on Linux (the short evaluator golden agrees on both;
        /// the scripted run stopped agreeing once the sword became a flying wave).
        /// CI runs them on a Windows job; on other platforms they are skipped rather than failing.
        /// </summary>
        private static void RequireWindowsGolden()
        {
            Assume.That(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "bit-exact golden recorded on Windows");
        }

        private static long InvulnerableHash(float untilSeconds, out SurvivorSim sim)
        {
            sim = new SurvivorSim(PreM8(new SurvivorConfig()), 2024); sim.SetHeroInvulnerableForTests();
            long hash = 17;
            for (int tick = 0; tick < 62000 && !sim.IsEnded && sim.Time < untilSeconds; tick++)
            {
                SurvivorInput input = sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1 + tick % 3) : new SurvivorInput((tick / 45) % 9, (tick / 200) % 4, 0);
                sim.Step(input); hash = hash * 31 + StateHash(sim);
            }
            return hash;
        }

        [Test]
        public void Tier1_EvaluatorRun_MatchesPreM5Golden()
        {
            SurvivorRunStats stats = new SurvivorEvaluator().RunOne(SurvivorTestHelpers.Brain(favouredMove: 1), PreM8(new SurvivorConfig { RunSeconds = 120f }), 555, true);
            long hash = 17;
            hash = hash * 31 + Bits(stats.SurvivedSeconds); hash = hash * 31 + (int)stats.EndReason; hash = hash * 31 + stats.Level;
            hash = hash * 31 + stats.Kills; hash = hash * 31 + Bits(stats.Gold); hash = hash * 31 + Bits(stats.TotalXp);
            hash = hash * 31 + Bits(stats.DamageTaken); hash = hash * 31 + Bits(stats.DamageDealt); hash = hash * 31 + Bits(stats.MinHpRatio);
            Assert.That(hash, Is.EqualTo(EvaluatorGolden), "end " + stats.EndReason + " at " + stats.SurvivedSeconds);
        }

        /// <summary>
        /// The goldens were recorded before the T-035 (M8) balance pass; with its three numbers set back they
        /// still pin every other rule bit for bit. T-036 (M9) also restores the old content: 4 + 4 slots, the old pools and
        /// no fourth Warrior skill.
        /// </summary>
        internal static SurvivorConfig PreM8(SurvivorConfig config)
        {
            config.Tuning.XpMul = 1f; config.Tuning.BossHpMul = 1f; config.Tuning.GoldChance = 0.03f;
            return SurvivorTestHelpers.OldRules(config);
        }

        internal static long StateHash(SurvivorSim sim)
        {
            long hash = 17;
            hash = hash * 31 + Bits(sim.Time); hash = hash * 31 + Bits(sim.Hero.Position.X); hash = hash * 31 + Bits(sim.Hero.Position.Y);
            hash = hash * 31 + Bits(sim.Hero.Hp); hash = hash * 31 + Bits(sim.Hero.Energy); hash = hash * 31 + sim.Level; hash = hash * 31 + sim.Kills;
            hash = hash * 31 + Bits(sim.Gold); hash = hash * 31 + Bits(sim.Xp); hash = hash * 31 + (int)sim.EndReason; hash = hash * 31 + sim.Events.Count;
            for (int i = 0; i < sim.Enemies.Count; i++)
            {
                SurvivorEnemy e = sim.Enemies[i]; if (!e.Active) continue;
                hash = hash * 31 + e.Id; hash = hash * 31 + e.TypeIndex; hash = hash * 31 + Bits(e.Position.X); hash = hash * 31 + Bits(e.Position.Y); hash = hash * 31 + Bits(e.Hp);
            }
            for (int i = 0; i < sim.Pickups.Count; i++)
            {
                SurvivorPickup p = sim.Pickups[i]; if (!p.Active) continue;
                hash = hash * 31 + (int)p.Kind; hash = hash * 31 + Bits(p.Position.X); hash = hash * 31 + Bits(p.Position.Y); hash = hash * 31 + Bits(p.Value);
            }
            return hash;
        }

        private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    }
}
