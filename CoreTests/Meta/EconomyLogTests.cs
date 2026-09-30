using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Meta
{
    public sealed class EconomyLogTests
    {
        [Test]
        public void Header_HasNineteenFields()
        {
            string[] fields = EconomyLog.Header.Split(',');
            Assert.That(fields, Has.Length.EqualTo(19));
            Assert.That(fields[0], Is.EqualTo("time")); Assert.That(fields[18], Is.EqualTo("stat_points"));
        }

        [Test]
        public void Line_MatchesHeader_ValuesInOrder()
        {
            CharacterBuild build = new CharacterBuild { Tier = 3 };
            build.Points[(int)StatId.MaxHp] = 4; build.Points[(int)StatId.Greed] = 2; build.Points[(int)StatId.Growth] = 1;
            string line = EconomyLog.Line(new DateTime(2026, 9, 30, 14, 5, 9, DateTimeKind.Utc), EconomyLog.FarmMode, "warrior", 3, "Bộ 1", build, Stats());
            string[] fields = line.Split(',');
            Assert.That(fields, Has.Length.EqualTo(EconomyLog.Header.Split(',').Length));
            Assert.That(fields, Is.EqualTo(new[]
            {
                "2026-09-30T14:05:09Z", "farm", "warrior", "3", "Bộ 1", "312.5", "Died", "Brute", "17", "420", "250.125",
                "200", "30", "0", "20.125", "0", "1234.5", "48.024", "4;0;0;0;0;0;0;0;0;0;0;2;1"
            }));
            Assert.That(fields[18].Split(';'), Has.Length.EqualTo(StatInfo.UsedCount));
        }

        [Test]
        public void Line_CleansTextFields()
        {
            string line = EconomyLog.Line(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), "watch", "war,rior", 1, "a,b;c\"d\r\ne", null, Stats());
            string[] fields = line.Split(',');
            Assert.That(fields, Has.Length.EqualTo(19));
            Assert.That(fields[2], Is.EqualTo("war rior")); Assert.That(fields[4], Is.EqualTo("a b c d  e"));
            Assert.That(line, Does.Not.Contain("\n")); Assert.That(line, Does.Not.Contain("\""));
            Assert.That(fields[18], Is.EqualTo("0;0;0;0;0;0;0;0;0;0;0;0;0"), "no build: zeros");
            Assert.That(EconomyLog.Line(DateTime.UtcNow, null, null, 1, null, null, Stats()).Split(','), Has.Length.EqualTo(19));
            Assert.Throws<ArgumentNullException>(() => EconomyLog.Line(DateTime.UtcNow, "watch", "warrior", 1, "x", null, null));
        }

        [Test]
        public void Line_UsesInvariantCulture()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("vi-VN");
                string line = EconomyLog.Line(new DateTime(2026, 9, 30, 14, 5, 9, DateTimeKind.Utc), "watch", "warrior", 1, "x", null, Stats());
                Assert.That(line.Split(','), Has.Length.EqualTo(19));
                Assert.That(line, Does.Contain(",312.5,")); Assert.That(line, Does.StartWith("2026-09-30T14:05:09Z,"));
            }
            finally { Thread.CurrentThread.CurrentCulture = previous; }
        }

        [Test]
        public void Line_ZeroSeconds_GivesZeroGoldPerMinute()
        {
            SurvivorRunStats stats = Stats(); stats.SurvivedSeconds = 0f;
            string[] fields = EconomyLog.Line(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "watch", "warrior", 1, "x", null, stats).Split(',');
            Assert.That(fields[5], Is.EqualTo("0")); Assert.That(fields[17], Is.EqualTo("0"));
        }

        private static SurvivorRunStats Stats()
        {
            SurvivorRunStats stats = new SurvivorRunStats
            {
                SurvivedSeconds = 312.5f, EndReason = EndReason.Died, DeathCause = DeathCause.Brute, Level = 17, Kills = 420,
                Gold = 250.125f, TotalXp = 1234.5f
            };
            stats.GoldBySource[(int)GoldSource.Normal] = 200f;
            stats.GoldBySource[(int)GoldSource.Elite] = 30f;
            stats.GoldBySource[(int)GoldSource.Chest] = 20.125f;
            return stats;
        }
    }
}
