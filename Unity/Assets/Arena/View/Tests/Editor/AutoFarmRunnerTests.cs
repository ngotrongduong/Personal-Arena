using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View.Tests
{
    public sealed class AutoFarmRunnerTests
    {
        private const int WaitMilliseconds = 120000;

        /// <summary>A valid schema-v4 brain with no hidden layers and all-zero weights (it stands still).</summary>
        private static byte[] ZeroBrain()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(new[] { (byte)'P', (byte)'A', (byte)'B', (byte)'R' });
                    writer.Write(PolicyBrain.FormatVersion);
                    byte[] name = Encoding.UTF8.GetBytes("Warrior");
                    writer.Write(name.Length);
                    writer.Write(name);
                    writer.Write(0L);
                    writer.Write(SurvivorObservation.Size);
                    writer.Write(0); // body layers
                    int[] branches = { SurvivorInput.MoveBranchSize, SurvivorInput.SkillBranchSize, SurvivorInput.PickBranchSize };
                    writer.Write(branches.Length);
                    foreach (int outputs in branches)
                    {
                        writer.Write(SurvivorObservation.Size);
                        writer.Write(outputs);
                        writer.Write(new byte[SurvivorObservation.Size * outputs * sizeof(float)]);
                        writer.Write(new byte[outputs * sizeof(float)]);
                    }
                    writer.Write(0); // no self test
                }

                return stream.ToArray();
            }
        }

        [Test]
        public void TheTestBrainIsAValidWarriorBrain()
        {
            PolicyBrain brain = PolicyBrain.Load(ZeroBrain());

            Assert.That(SurvivorPilot.Validate(brain), Is.Null);
            Assert.That(brain.ObservationSize, Is.EqualTo(SurvivorObservation.Size));
        }

        [Test]
        public void BadBrainBytesAreRejected()
        {
            CharacterBuild build = new CharacterBuild();
            Assert.Throws<ArgumentException>(() => new AutoFarmRunner(null, build, 1, 1, 60f));
            Assert.Throws<ArgumentException>(() => new AutoFarmRunner(new byte[0], build, 1, 1, 60f));
        }

        [Test]
        public void TwoShortRunsFinishAndAreBooked()
        {
            PlayerProfile profile = ProfileRules.NewProfile();
            CharacterProfile warrior = ProfileRules.FindCharacter(profile, ProfileRules.WarriorId);
            CharacterBuild build = ProfileRules.ToBuild(warrior, profile.SelectedTier);
            AutoFarmRunner runner = new AutoFarmRunner(ZeroBrain(), build, 2, 1000, 60f);

            runner.Start();
            Assert.That(runner.Wait(WaitMilliseconds), Is.True, "Two 60 s matches should finish quickly.");
            Assert.That(runner.IsDone, Is.True);
            Assert.That(runner.Completed, Is.EqualTo(2));
            Assert.That(runner.Error, Is.Null);

            List<string> lines = new List<string>();
            FarmSummary summary = runner.Record(profile, warrior, 0, "Bộ 1", lines);

            Assert.That(runner.IsRecorded, Is.True);
            Assert.That(summary.Runs, Is.EqualTo(2));
            Assert.That(summary.Cancelled, Is.False);
            Assert.That(profile.Stats.FarmSessions, Is.EqualTo(1));
            Assert.That(profile.Stats.FarmRuns, Is.EqualTo(2));
            Assert.That(profile.Stats.Runs, Is.EqualTo(2));
            Assert.That(profile.Gold, Is.EqualTo(summary.Gold));
            Assert.That(warrior.Loadouts[0].Record.Runs, Is.EqualTo(2));
            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0], Does.Contain(",farm,"));

            FarmSummary again = runner.Record(profile, warrior, 0, "Bộ 1", lines);
            Assert.That(again, Is.SameAs(summary), "Booking twice must not pay twice.");
            Assert.That(profile.Stats.FarmSessions, Is.EqualTo(1));
        }

        [Test]
        public void CancelStopsAfterTheCurrentMatch()
        {
            PlayerProfile profile = ProfileRules.NewProfile();
            CharacterProfile warrior = ProfileRules.FindCharacter(profile, ProfileRules.WarriorId);
            AutoFarmRunner runner = new AutoFarmRunner(ZeroBrain(), ProfileRules.ToBuild(warrior, 1), 100, 5, 60f);

            runner.Start();
            runner.Cancel();
            Assert.That(runner.Wait(WaitMilliseconds), Is.True);
            Assert.That(runner.IsDone, Is.True);
            Assert.That(runner.IsCancelled, Is.True);
            Assert.That(runner.Completed, Is.LessThanOrEqualTo(1));

            FarmSummary summary = runner.Record(profile, warrior, 0, "Bộ 1", null);
            Assert.That(summary.Cancelled, Is.True);
            Assert.That(summary.Runs, Is.EqualTo(runner.Completed));
            Assert.That(profile.Stats.FarmSessions, Is.EqualTo(1));
        }

        [Test]
        public void SummaryTextIsVietnamese()
        {
            FarmSummary summary = new FarmSummary { Runs = 3, Wins = 1, Gold = 1500, AverageSeconds = 400f, Cancelled = true };

            string text = MetaViewLogic.FarmSummaryText(summary);

            Assert.That(text, Does.StartWith("Xong 3 trận: thắng 1 · +1.500 vàng vào ví · sống TB 06:40"));
            Assert.That(text, Does.Contain("Đã dừng giữa chừng"));
        }
    }
}
