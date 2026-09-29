using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.View.Tests
{
    public sealed class ArenaStatsTests
    {
        [Test]
        public void RecordEvents_CountsCombatAndDoesNotDoubleCountBehindDamage()
        {
            ArenaStats stats = new ArenaStats();
            SimEvent[] events =
            {
                new SimEvent(SimEventType.HeroDealtDamage, 12f, 0),
                new SimEvent(SimEventType.ZombieKilled, 1f, 0),
                new SimEvent(SimEventType.Backstab, 1f, 0),
                new SimEvent(SimEventType.Parry, 1f, 1),
                new SimEvent(SimEventType.Kick, 1f, 1),
                new SimEvent(SimEventType.HeroDamaged, 8f, 1),
                new SimEvent(SimEventType.HeroDamagedFromBehind, 8f, 1)
            };

            stats.RecordEvents(events, 3.5f);

            Assert.That(stats.Kills, Is.EqualTo(1));
            Assert.That(stats.Backstabs, Is.EqualTo(1));
            Assert.That(stats.Parries, Is.EqualTo(1));
            Assert.That(stats.KicksLanded, Is.EqualTo(1));
            Assert.That(stats.DamageDealt, Is.EqualTo(12f));
            Assert.That(stats.DamageTaken, Is.EqualTo(8f));
            Assert.That(stats.TimeSurvived, Is.EqualTo(3.5f));
        }

        [Test]
        public void Reset_ClearsAllEpisodeValues()
        {
            ArenaStats stats = new ArenaStats();
            stats.RecordEvents(new[] { new SimEvent(SimEventType.ZombieKilled) }, 1f);

            stats.Reset();

            Assert.That(stats.Kills, Is.Zero);
            Assert.That(stats.TimeSurvived, Is.Zero);
        }
    }
}
