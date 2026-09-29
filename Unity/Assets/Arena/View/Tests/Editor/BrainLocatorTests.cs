using System;
using System.IO;
using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class BrainLocatorTests
    {
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

        private string WriteBrain(string run, string behavior, DateTime writtenUtc)
        {
            string folder = Directory.CreateDirectory(Path.Combine(root, run, behavior)).FullName;
            string path = Path.Combine(folder, BrainLocator.LatestFileName);
            File.WriteAllBytes(path, new byte[] { 1 });
            File.SetLastWriteTimeUtc(path, writtenUtc);
            return path;
        }
    }
}
