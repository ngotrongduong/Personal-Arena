using System.Diagnostics;
using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public class PerformanceTests
    {
        [Test]
        public void SixteenZombiesForSixtySecondsRunsUnderTwoSeconds()
        {
            ArenaSim sim = TestHelpers.Sim(16, true, 42);
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int tick = 0; tick < 60 * 60; tick++)
            {
                sim.Step(new HeroInput((tick / 30) % 9, tick % 20 == 0 ? 1 : 0, tick % 90 == 0 ? 1 : 0));
                if (sim.Done)
                {
                    sim.Reset(42 + tick);
                }
            }

            stopwatch.Stop();
            Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(2d));
        }
    }
}
