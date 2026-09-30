using System;
using System.IO;
using NUnit.Framework;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View.Tests
{
    public sealed class ProfileStoreTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "pa-profile-tests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void MissingFileGivesANewProfileWithTheWarrior()
        {
            ProfileStore store = new ProfileStore(directory);

            Assert.That(store.Load(), Is.EqualTo(ProfileLoadOutcome.New));
            Assert.That(store.Warrior, Is.Not.Null);
            Assert.That(store.Profile.Gold, Is.EqualTo(0));
            Assert.That(File.Exists(store.ProfilePath), Is.False, "Loading alone must not create the file.");
        }

        [Test]
        public void SaveThenLoadRoundTripsTheProfile()
        {
            ProfileStore store = new ProfileStore(directory);
            store.Load();
            store.Profile.Gold = 12345;
            store.Profile.UnlockedTier = 3;
            store.Profile.SelectedTier = 2;
            store.Profile.TrainingFocus = "gold";
            CharacterProfile warrior = store.Warrior;
            warrior.Level = 3;
            Assert.That(ProfileRules.TryAddPoint(warrior, 1, StatId.Might), Is.True);
            Assert.That(ProfileRules.TryRename(warrior, 1, "Đánh mạnh"), Is.True);
            Assert.That(ProfileRules.TrySetActiveLoadout(warrior, 1), Is.True);
            Assert.That(store.Save(), Is.True);

            ProfileStore reloaded = new ProfileStore(directory);
            Assert.That(reloaded.Load(), Is.EqualTo(ProfileLoadOutcome.Loaded));
            Assert.That(reloaded.Profile.Gold, Is.EqualTo(12345));
            Assert.That(reloaded.Profile.UnlockedTier, Is.EqualTo(3));
            Assert.That(reloaded.Profile.SelectedTier, Is.EqualTo(2));
            Assert.That(reloaded.Profile.TrainingFocus, Is.EqualTo("gold"));
            Assert.That(reloaded.Warrior.Level, Is.EqualTo(3));
            Assert.That(reloaded.Warrior.ActiveLoadout, Is.EqualTo(1));
            Assert.That(reloaded.Warrior.Loadouts[1].Name, Is.EqualTo("Đánh mạnh"));
            Assert.That(reloaded.Warrior.Loadouts[1].Points[(int)StatId.Might], Is.EqualTo(1));
        }

        [Test]
        public void AtomicSaveLeavesAValidFileABackupAndNoTempFile()
        {
            ProfileStore store = new ProfileStore(directory);
            store.Load();
            store.Profile.Gold = 10;
            Assert.That(store.Save(), Is.True);
            store.Profile.Gold = 20;
            Assert.That(store.Save(), Is.True);

            Assert.That(File.Exists(store.TempPath), Is.False);
            Assert.That(File.Exists(store.ProfilePath), Is.True);
            Assert.That(File.Exists(store.BackupPath), Is.True, "The second save keeps the previous file as the backup.");

            ProfileStore reloaded = new ProfileStore(directory);
            reloaded.Load();
            Assert.That(reloaded.Profile.Gold, Is.EqualTo(20));

            ProfileStore backup = new ProfileStore(directory, Path.GetFileName(store.BackupPath));
            backup.Load();
            Assert.That(backup.Profile.Gold, Is.EqualTo(10));
        }

        [Test]
        public void CorruptFileIsKeptAsideAndTheBackupIsUsed()
        {
            ProfileStore store = new ProfileStore(directory);
            store.Load();
            store.Profile.Gold = 111;
            store.Save();
            store.Profile.Gold = 222;
            store.Save();
            File.WriteAllText(store.ProfilePath, "this is not json");

            ProfileStore recovered = new ProfileStore(directory);
            Assert.That(recovered.Load(), Is.EqualTo(ProfileLoadOutcome.RecoveredFromBackup));
            Assert.That(recovered.Profile.Gold, Is.EqualTo(111));
            Assert.That(recovered.CorruptCopyPath, Is.Not.Null);
            Assert.That(File.Exists(recovered.CorruptCopyPath), Is.True);
            Assert.That(File.ReadAllText(recovered.CorruptCopyPath), Is.EqualTo("this is not json"));
            Assert.That(recovered.SaveBlocked, Is.False);

            ProfileStore again = new ProfileStore(directory);
            Assert.That(again.Load(), Is.EqualTo(ProfileLoadOutcome.Loaded), "The recovered profile was saved back.");
            Assert.That(again.Profile.Gold, Is.EqualTo(111));
        }

        [Test]
        public void CorruptFileWithoutBackupGivesANewProfile()
        {
            Directory.CreateDirectory(directory);
            ProfileStore store = new ProfileStore(directory);
            File.WriteAllText(store.ProfilePath, "{ broken");

            Assert.That(store.Load(), Is.EqualTo(ProfileLoadOutcome.RecoveredNew));
            Assert.That(store.Warrior, Is.Not.Null);
            Assert.That(store.Profile.Gold, Is.EqualTo(0));
            Assert.That(store.CorruptCopyPath, Is.Not.Null);
        }

        [Test]
        public void EconomyHeaderIsWrittenOnce()
        {
            ProfileStore store = new ProfileStore(directory);
            store.Load();
            Assert.That(store.AppendEconomyLine("a,b"), Is.True);
            Assert.That(store.AppendEconomyLines(new[] { "c,d", "e,f" }), Is.True);

            string[] lines = File.ReadAllLines(store.EconomyPath);
            Assert.That(lines.Length, Is.EqualTo(4));
            Assert.That(lines[0], Is.EqualTo(EconomyLog.Header));
            Assert.That(lines[1], Is.EqualTo("a,b"));
            Assert.That(lines[3], Is.EqualTo("e,f"));
        }

        [Test]
        public void OverridePathPicksFolderOrFile()
        {
            string fallback = Path.Combine(directory, "fallback");
            ProfileStore folder = ProfileStore.Create(Path.Combine(directory, "scratch"), Path.Combine(directory, "real"), fallback);
            Assert.That(folder.ProfilePath, Is.EqualTo(Path.Combine(directory, "scratch", ProfileStore.DefaultFileName)));

            ProfileStore file = ProfileStore.Create(Path.Combine(directory, "shots", "owner.json"), Path.Combine(directory, "real"), fallback);
            Assert.That(file.ProfilePath, Is.EqualTo(Path.Combine(directory, "shots", "owner.json")));
            Assert.That(file.BackupPath, Is.EqualTo(Path.Combine(directory, "shots", "owner.bak.json")));

            ProfileStore real = ProfileStore.Create(null, Path.Combine(directory, "real"), fallback);
            Assert.That(real.ProfilePath, Is.EqualTo(Path.Combine(directory, "real", ProfileStore.DefaultFileName)));
        }
    }
}
