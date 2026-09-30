using System;
using System.IO;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View.Tests
{
    public sealed class BrainLocatorTests
    {
        private const int Current = SurvivorObservation.SchemaVersion;

        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "BrainLocatorTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void CurrentSchema_IsTheSurvivorObservationSchema()
        {
            Assert.That(BrainLocator.CurrentSchemaVersion, Is.EqualTo(4));
            Assert.That(BrainLocator.SchemaFileName, Is.EqualTo("schema_version.txt"));
        }

        [Test]
        public void FindRunsDirectory_WalksUpFromBuildDataFolder()
        {
            string runs = Directory.CreateDirectory(Path.Combine(root, "Trainer", "runs")).FullName;
            string data = Directory.CreateDirectory(Path.Combine(root, "Build", "Watch", "PersonalArenaWatch_Data")).FullName;

            Assert.That(BrainLocator.FindRunsDirectory(data), Is.EqualTo(runs));
            Assert.That(BrainLocator.FindRunsDirectory(null), Is.Null);
        }

        [Test]
        public void FindNewestBrain_PicksMostRecentlyWrittenRunForBehavior()
        {
            string older = WriteBrain("warrior-001", "Warrior", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            string newer = WriteBrain("warrior-002", "Warrior", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            WriteBrain("mage-001", "Mage", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(BrainLocator.FindNewestBrain(root, "Warrior"), Is.EqualTo(newer));
            Assert.That(older, Is.Not.EqualTo(newer));
            Assert.That(BrainLocator.RunName(newer), Is.EqualTo("warrior-002"));
            Assert.That(BrainLocator.FindNewestBrain(root, "Archer"), Is.Null);
            Assert.That(BrainLocator.FindNewestBrain(Path.Combine(root, "missing"), "Warrior"), Is.Null);
        }

        [Test]
        public void RunSchemaVersion_DefaultsToOneForMissingOrBadFiles()
        {
            string run = Directory.CreateDirectory(Path.Combine(root, "warrior-001")).FullName;

            Assert.That(BrainLocator.RunSchemaVersion(run), Is.EqualTo(1));
            Assert.That(BrainLocator.RunSchemaVersion(null), Is.EqualTo(1));
            File.WriteAllText(Path.Combine(run, BrainLocator.SchemaFileName), "not a number");
            Assert.That(BrainLocator.RunSchemaVersion(run), Is.EqualTo(1));
            File.WriteAllText(Path.Combine(run, BrainLocator.SchemaFileName), Current + "\n");
            Assert.That(BrainLocator.RunSchemaVersion(run), Is.EqualTo(Current));
        }

        [Test]
        public void FindNewestBrain_SkipsNewerBrainFromOtherSchema()
        {
            string current = WriteBrain("survivor-001", "Warrior",
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            string old = WriteBrain("warrior-002", "Warrior",
                new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), 3);

            Assert.That(BrainLocator.FindNewestBrain(root, "Warrior"), Is.EqualTo(current));
            Assert.That(BrainLocator.FindNewestBrain(root, "Warrior", false), Is.EqualTo(current));
            Assert.That(BrainLocator.RunDirectory(current),
                Is.EqualTo(Directory.GetParent(Directory.GetParent(current).FullName).FullName));

            File.Delete(current);
            Assert.That(BrainLocator.FindNewestBrain(root, "Warrior"), Is.Null);
            Assert.That(BrainLocator.FindNewestBrain(root, "Warrior", false), Is.EqualTo(old));
        }

        [Test]
        public void FindNewestBrain_SkipsFutureSchema()
        {
            WriteBrain("survivor-next", "Warrior", new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), Current + 1);
            string current = WriteBrain("survivor-001", "Warrior", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(BrainLocator.FindNewestBrain(root, "Warrior"), Is.EqualTo(current));
        }

        [Test]
        public void FindNewestRunDirectory_UsesNewestFileFromCurrentSchemaRun()
        {
            string older = WriteRunFile("warrior-001", "Warrior", "events.old",
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Current);
            string newer = WriteRunFile("warrior-002", "Warrior", "events.new",
                new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), Current);
            WriteRunFile("warrior-legacy", "Warrior", "events.legacy",
                new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 3);

            Assert.That(BrainLocator.FindNewestRunDirectory(root, "Warrior"),
                Is.EqualTo(Directory.GetParent(Directory.GetParent(newer).FullName).FullName));
            Assert.That(older, Is.Not.EqualTo(newer));
            Assert.That(BrainLocator.FindNewestRunDirectory(root, "Mage"), Is.Null);
        }

        [Test]
        public void ChampionsFolder_IsNotATrainingRun()
        {
            string run = WriteBrain("warrior-s001", "Warrior", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            // Even a champions folder that looks like a newer current-schema run must be ignored.
            WriteBrain(BrainLocator.ChampionsDirectoryName, "Warrior", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            WriteRunFile(BrainLocator.ChampionsDirectoryName, "Warrior", "evaluations.jsonl",
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), Current);

            Assert.That(BrainLocator.FindNewestBrain(root, "Warrior"), Is.EqualTo(run));
            Assert.That(BrainLocator.FindNewestBrain(root, "Warrior", false), Is.EqualTo(run));
            Assert.That(BrainLocator.FindNewestRunDirectory(root, "Warrior"),
                Is.EqualTo(Path.Combine(root, "warrior-s001")));
        }

        [Test]
        public void FindChampionBrain_ReturnsChampionFileOnlyWhenPresent()
        {
            Assert.That(BrainLocator.FindChampionBrain(root, "Warrior"), Is.Null);
            Assert.That(BrainLocator.FindChampionBrain(null, "Warrior"), Is.Null);

            string folder = Directory.CreateDirectory(BrainLocator.ChampionDirectory(root, "Warrior")).FullName;
            string champion = Path.Combine(folder, BrainLocator.ChampionFileName);
            File.WriteAllBytes(champion, new byte[] { 1 });

            Assert.That(BrainLocator.FindChampionBrain(root, "Warrior"), Is.EqualTo(champion));
            Assert.That(BrainLocator.FindChampionBrain(root, "Mage"), Is.Null);
        }

        private string WriteBrain(string run, string behavior, DateTime writtenUtc,
            int schemaVersion = Current)
        {
            string folder = Directory.CreateDirectory(Path.Combine(root, run, behavior)).FullName;
            File.WriteAllText(Path.Combine(Directory.GetParent(folder).FullName, BrainLocator.SchemaFileName),
                schemaVersion.ToString());
            string path = Path.Combine(folder, BrainLocator.LatestFileName);
            File.WriteAllBytes(path, new byte[] { 1 });
            File.SetLastWriteTimeUtc(path, writtenUtc);
            return path;
        }

        private string WriteRunFile(string run, string behavior, string fileName, DateTime writtenUtc,
            int schemaVersion)
        {
            string folder = Directory.CreateDirectory(Path.Combine(root, run, behavior)).FullName;
            File.WriteAllText(Path.Combine(Directory.GetParent(folder).FullName, BrainLocator.SchemaFileName),
                schemaVersion.ToString());
            string path = Path.Combine(folder, fileName);
            File.WriteAllText(path, "event");
            File.SetLastWriteTimeUtc(path, writtenUtc);
            return path;
        }
    }
}
