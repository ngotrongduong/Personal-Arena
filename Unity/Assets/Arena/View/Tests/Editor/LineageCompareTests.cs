using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class LineageCompareTests
    {
        [Test]
        public void Better_HigherWinsForMostMetrics()
        {
            Assert.That(LineageCompare.Better(LineageMetric.MedianSurvival, 500f, 400f), Is.EqualTo(-1));
            Assert.That(LineageCompare.Better(LineageMetric.MedianSurvival, 400f, 500f), Is.EqualTo(1));
            Assert.That(LineageCompare.Better(LineageMetric.WinRate, 0.2f, 0.3f), Is.EqualTo(1));
            Assert.That(LineageCompare.Better(LineageMetric.GoldPerMinute, 30f, 20f), Is.EqualTo(-1));
            Assert.That(LineageCompare.Better(LineageMetric.Score, 900f, 100f), Is.EqualTo(-1));
        }

        [Test]
        public void Better_LowerDamageTakenWins()
        {
            Assert.That(LineageCompare.LowerIsBetter(LineageMetric.DamageTakenPerMinute), Is.True);
            Assert.That(LineageCompare.LowerIsBetter(LineageMetric.MedianSurvival), Is.False);
            Assert.That(LineageCompare.Better(LineageMetric.DamageTakenPerMinute, 30f, 50f), Is.EqualTo(-1));
            Assert.That(LineageCompare.Better(LineageMetric.DamageTakenPerMinute, 50f, 30f), Is.EqualTo(1));
        }

        [Test]
        public void Better_EqualValuesTie()
        {
            Assert.That(LineageCompare.Better(LineageMetric.MedianSurvival, 420f, 420f), Is.EqualTo(0));
            Assert.That(LineageCompare.Better(LineageMetric.DamageTakenPerMinute, 12.5f, 12.5f), Is.EqualTo(0));
            Assert.That(LineageCompare.Better(LineageMetric.WinRate, 0f, 0f), Is.EqualTo(0));
        }

        [Test]
        public void Better_UnevaluatedVersionIsNeverBetter()
        {
            LineageVersion evaluated = Version(500f, 20f);
            LineageVersion plain = new LineageVersion { Id = "b", RunId = "warrior-s001", Step = 10 };

            Assert.That(LineageCompare.Better(LineageMetric.MedianSurvival, evaluated, plain), Is.EqualTo(0));
            Assert.That(LineageCompare.Better(LineageMetric.MedianSurvival, plain, evaluated), Is.EqualTo(0));
            Assert.That(LineageCompare.TryValue(LineageMetric.MedianSurvival, plain, out float value), Is.False);
            Assert.That(value, Is.EqualTo(0f));
            Assert.That(LineageCompare.TryValue(LineageMetric.MedianSurvival, null, out value), Is.False);
        }

        [Test]
        public void Better_ComparesEvaluatedVersions()
        {
            LineageVersion a = Version(500f, 20f);
            LineageVersion b = Version(400f, 10f);

            Assert.That(LineageCompare.Better(LineageMetric.MedianSurvival, a, b), Is.EqualTo(-1));
            Assert.That(LineageCompare.Better(LineageMetric.DamageTakenPerMinute, a, b), Is.EqualTo(1));
            Assert.That(LineageCompare.TryValue(LineageMetric.MedianSurvival, a, out float value), Is.True);
            Assert.That(value, Is.EqualTo(500f));
            Assert.That(LineageCompare.TryValue(LineageMetric.Score, a, out value), Is.True);
            Assert.That(value, Is.EqualTo(123f));
        }

        [Test]
        public void FormatAndLabels_AreReadable()
        {
            Assert.That(LineageCompare.Format(LineageMetric.MedianSurvival, 125f), Is.EqualTo("2:05"));
            Assert.That(LineageCompare.Format(LineageMetric.WinRate, 0.456f), Is.EqualTo("46%"));
            Assert.That(LineageCompare.Format(LineageMetric.GoldPerMinute, 12.34f), Is.EqualTo("12,3"));
            Assert.That(LineageCompare.Format(LineageMetric.Score, 511.6f), Is.EqualTo("512"));
            Assert.That(LineageCompare.Metrics.Length, Is.EqualTo(9));

            for (int i = 0; i < LineageCompare.Metrics.Length; i++)
            {
                Assert.That(LineageCompare.Label(LineageCompare.Metrics[i]), Is.Not.Empty);
            }
        }

        private static LineageVersion Version(float medianSeconds, float damagePerMinute)
        {
            return new LineageVersion
            {
                Id = "a",
                RunId = "warrior-s001",
                Step = 100,
                Evaluated = true,
                Score = 123f,
                Summary = new ChampionSummary
                {
                    Runs = 100,
                    MedianSurvivedSeconds = medianSeconds,
                    DamageTakenPerMinute = damagePerMinute
                },
                Behavior = new ChampionBehavior()
            };
        }
    }
}
