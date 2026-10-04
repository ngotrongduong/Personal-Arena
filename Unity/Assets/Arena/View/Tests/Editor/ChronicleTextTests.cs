using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View.Tests
{
    public sealed class ChronicleTextTests
    {
        private static ChronicleEntry Entry(float time, ChronicleKind kind)
        {
            return new ChronicleEntry { Time = time, Kind = kind, ItemIndex = -1 };
        }

        [Test]
        public void EmptyChronicleShowsTheEmptyText()
        {
            Assert.That(ChronicleText.Format(null), Is.EqualTo(ChronicleText.EmptyText));
            Assert.That(ChronicleText.Format(new RunChronicle()), Is.EqualTo(ChronicleText.EmptyText));
        }

        [Test]
        public void LinesHaveTheClockAndTheText()
        {
            RunChronicle chronicle = new RunChronicle { EndReason = EndReason.Won };
            chronicle.Entries.Add(Entry(65f, ChronicleKind.EliteKilled));
            chronicle.Entries.Add(Entry(900f, ChronicleKind.BossSpawned));
            chronicle.Entries.Add(Entry(960f, ChronicleKind.BossKilled));
            chronicle.Entries.Add(Entry(960f, ChronicleKind.End));

            string[] lines = ChronicleText.Format(chronicle).Split('\n');

            Assert.That(lines.Length, Is.EqualTo(4));
            Assert.That(lines[0], Is.EqualTo("01:05  Killed an elite"));
            Assert.That(lines[1], Does.EndWith("The boss appeared"));
            Assert.That(lines[2], Does.EndWith("Boss killed!"));
            Assert.That(lines[3], Does.EndWith("Victory!"));
        }

        [Test]
        public void NearDeathShowsThePercentAndCause()
        {
            ChronicleEntry entry = Entry(200f, ChronicleKind.NearDeath);
            entry.Value = 0.12f;
            entry.Cause = DeathCause.Surrounded;

            Assert.That(ChronicleText.Describe(entry, EndReason.None), Is.EqualTo("Near death (12% HP, surrounded)"));
        }

        [Test]
        public void StyleChangeUsesTheSpectatorLabel()
        {
            ChronicleEntry entry = Entry(120f, ChronicleKind.StyleChange);
            entry.Label = SpectatorLabel.Kiting;

            Assert.That(ChronicleText.Describe(entry, EndReason.None), Is.EqualTo("Switched to " + SpectatorLabels.DisplayName(SpectatorLabel.Kiting)));
        }

        [Test]
        public void TooManyEntriesKeepTheImportantOnesInTimeOrder()
        {
            List<ChronicleEntry> entries = new List<ChronicleEntry>();
            for (int i = 0; i < 20; i++)
            {
                entries.Add(Entry(10f + i, ChronicleKind.EliteKilled));
            }
            entries.Add(Entry(300f, ChronicleKind.NearDeath));
            entries.Add(Entry(500f, ChronicleKind.ItemMaxed));
            entries.Add(Entry(600f, ChronicleKind.End));

            List<ChronicleEntry> picked = ChronicleText.Select(entries, 4);

            Assert.That(picked.Count, Is.EqualTo(4));
            Assert.That(picked[0].Kind, Is.EqualTo(ChronicleKind.EliteKilled));
            Assert.That(picked[0].Time, Is.EqualTo(10f), "Ties keep the earliest entry.");
            Assert.That(picked[1].Kind, Is.EqualTo(ChronicleKind.NearDeath));
            Assert.That(picked[2].Kind, Is.EqualTo(ChronicleKind.ItemMaxed));
            Assert.That(picked[3].Kind, Is.EqualTo(ChronicleKind.End));
        }

        [Test]
        public void EndAlwaysHasTheTopPriority()
        {
            foreach (ChronicleKind kind in System.Enum.GetValues(typeof(ChronicleKind)))
            {
                if (kind != ChronicleKind.End)
                {
                    Assert.That(ChronicleText.Priority(ChronicleKind.End), Is.LessThan(ChronicleText.Priority(kind)));
                }
            }
        }

        [Test]
        public void RecorderOutputFormatsAfterARealRun()
        {
            SurvivorSim sim = new SurvivorSim(new SurvivorConfig { RunSeconds = 60f }, 7);
            SpectatorLabeler labeler = new SpectatorLabeler();
            RunChronicleRecorder recorder = new RunChronicleRecorder();
            int guard = 0;
            while (!sim.IsEnded && guard++ < 200000)
            {
                sim.Step(new SurvivorInput(0, 0, sim.IsAwaitingPick ? 1 : 0));
                labeler.Observe(sim, sim.LastStepSeconds);
                recorder.Observe(sim, labeler.Current);
            }
            recorder.Finish(sim);

            string text = ChronicleText.Format(recorder.Result);
            Assert.That(sim.IsEnded, Is.True);
            Assert.That(text, Is.Not.EqualTo(ChronicleText.EmptyText));
            Assert.That(text.Split('\n').Length, Is.LessThanOrEqualTo(ChronicleText.DefaultMaxLines));
        }
    }
}
