using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class LineageStoreTests
    {
        private const string Behavior = "Warrior";
        private const string ChampionVersionId = "warrior-s001-96999889";
        private const string PlainVersionId = "warrior-s002-1500000";
        private const string OldSchemaVersionId = "warrior-s003-5000";

        private string root;
        private string runs;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "LineageStoreTests-" + Guid.NewGuid().ToString("N"));
            runs = Path.Combine(root, "Trainer", "runs");
            Directory.CreateDirectory(runs);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        // ------------------------------------------------------------------ names

        [Test]
        public void DefaultNames_AreReadable()
        {
            Assert.That(LineageStore.DefaultBranchName("warrior-s001"), Is.EqualTo("Branch 1"));
            Assert.That(LineageStore.DefaultBranchName("warrior-s012"), Is.EqualTo("Branch 12"));
            Assert.That(LineageStore.DefaultBranchName("my-run"), Is.EqualTo("my-run"));
            Assert.That(LineageStore.DefaultBranchName(null), Is.EqualTo("Branch ?"));

            Assert.That(LineageStore.DefaultVersionName(96999889), Is.EqualTo("Step 97.0M"));
            Assert.That(LineageStore.FormatStep(950000), Is.EqualTo("950K"));
            Assert.That(LineageStore.FormatStep(500), Is.EqualTo("500"));
            Assert.That(LineageStore.FormatStep(-5), Is.EqualTo("0"));
        }

        [Test]
        public void CleanName_TrimsClipsAndRejectsEmpty()
        {
            Assert.That(LineageStore.CleanName("  Vô địch  "), Is.EqualTo("Vô địch"));
            Assert.That(LineageStore.CleanName("a\tb\nc"), Is.EqualTo("a b c"));
            Assert.That(LineageStore.CleanName(new string('x', 40)), Has.Length.EqualTo(LineageStore.MaxNameLength));
            Assert.That(LineageStore.CleanName("   "), Is.Null);
            Assert.That(LineageStore.CleanName(null), Is.Null);
        }

        [Test]
        public void IsBranchFolderName_SkipsChampionsAndHiddenFolders()
        {
            Assert.That(LineageStore.IsBranchFolderName("warrior-s001"), Is.True);
            Assert.That(LineageStore.IsBranchFolderName("champions"), Is.False);
            Assert.That(LineageStore.IsBranchFolderName(".warrior-s004.partial"), Is.False);
            Assert.That(LineageStore.IsBranchFolderName(""), Is.False);
        }

        // ------------------------------------------------------------------ load

        [Test]
        public void Load_MissingRunsFolderGivesEmptyIndex()
        {
            LineageIndex index = LineageStore.Load(Path.Combine(root, "missing"), Behavior);

            Assert.That(index.Branches, Is.Empty);
            Assert.That(index.Versions, Is.Empty);
            Assert.That(LineageStore.Load(null, Behavior).Versions, Is.Empty);
        }

        [Test]
        public void Load_ReadsBranchesWithAndWithoutParents()
        {
            BuildTree();

            LineageIndex index = LineageStore.Load(runs, Behavior);

            Assert.That(index.Branches.Count, Is.EqualTo(3));
            Assert.That(index.Branches[0].RunId, Is.EqualTo("warrior-s001"));
            Assert.That(index.Branches[1].RunId, Is.EqualTo("warrior-s002"));
            Assert.That(index.Branches[2].RunId, Is.EqualTo("warrior-s003"));

            LineageBranch first = index.Branches[0];
            Assert.That(first.OnDisk, Is.True);
            Assert.That(first.HasParent, Is.False);
            Assert.That(first.HasCheckpoint, Is.True);
            Assert.That(first.CurrentStep, Is.EqualTo(96999889L), "newest numbered checkpoint wins");
            Assert.That(first.LatestBrainPath, Is.Not.Null);

            LineageBranch second = index.Branches[1];
            Assert.That(second.OnDisk, Is.True);
            Assert.That(second.HasParent, Is.True);
            Assert.That(second.ParentRun, Is.EqualTo("warrior-s001"));
            Assert.That(second.ParentStep, Is.EqualTo(96999889L));
            Assert.That(second.ParentVersion, Is.EqualTo(ChampionVersionId));
            Assert.That(second.HasCheckpoint, Is.False);
            Assert.That(second.CurrentStep, Is.EqualTo(1500000L), "falls back to the latest.brain header step");

            // warrior-s003 has an old schema: it only survives through its saved version.
            LineageBranch old = index.Branches[2];
            Assert.That(old.OnDisk, Is.False);
            Assert.That(old.CurrentStep, Is.EqualTo(5000L));

            Assert.That(index.FindBranch("champions"), Is.Null);
            Assert.That(index.FindBranch(".hidden"), Is.Null);
        }

        [Test]
        public void Load_ReadsVersionsNewestFirstAndSkipsBadFolders()
        {
            BuildTree();

            LineageIndex index = LineageStore.Load(runs, Behavior);

            Assert.That(index.Versions.Count, Is.EqualTo(3), "folders without version.json or brain are ignored");
            Assert.That(index.Versions[0].Id, Is.EqualTo(ChampionVersionId));
            Assert.That(index.Versions[1].Id, Is.EqualTo(PlainVersionId));
            Assert.That(index.Versions[2].Id, Is.EqualTo(OldSchemaVersionId));

            LineageVersion champion = index.Versions[0];
            Assert.That(champion.HasCheckpoint, Is.True);
            Assert.That(champion.IsCurrentChampion, Is.True);
            Assert.That(champion.WasChampion, Is.True);
            Assert.That(champion.HasEvaluation, Is.True);
            Assert.That(champion.Summary.MedianSurvivedSeconds, Is.EqualTo(420.5f).Within(0.001f));
            Assert.That(champion.Behavior, Is.Not.Null);
            Assert.That(champion.SchemaVersion, Is.EqualTo(BrainLocator.CurrentSchemaVersion));
            Assert.That(File.Exists(champion.BrainPath), Is.True);

            LineageVersion plain = index.Versions[1];
            Assert.That(plain.HasCheckpoint, Is.False, "a version without .pt can only be watched");
            Assert.That(plain.IsCurrentChampion, Is.False);
            Assert.That(plain.HasEvaluation, Is.False);
            Assert.That(plain.Summary, Is.Null);
            Assert.That(plain.Score, Is.EqualTo(0f), "NaN from Python is read as 0");

            Assert.That(index.Versions[2].SchemaVersion, Is.EqualTo(3));
            Assert.That(index.VersionsOf("warrior-s001").Count, Is.EqualTo(1));
            Assert.That(index.FindVersion(PlainVersionId), Is.SameAs(plain));
        }

        [Test]
        public void Load_AppliesLabelsAndDefaultNames()
        {
            BuildTree();

            LineageIndex index = LineageStore.Load(runs, Behavior);

            LineageVersion plain = index.FindVersion(PlainVersionId);
            Assert.That(plain.CustomName, Is.EqualTo("Thử nghiệm"));
            Assert.That(plain.Name, Is.EqualTo("Thử nghiệm"));
            Assert.That(plain.Pinned, Is.True);

            LineageVersion champion = index.FindVersion(ChampionVersionId);
            Assert.That(champion.CustomName, Is.Null);
            Assert.That(champion.Name, Is.EqualTo("Step 97.0M"));
            Assert.That(champion.Pinned, Is.False);

            Assert.That(index.FindBranch("warrior-s001").Name, Is.EqualTo("Chính"));
            Assert.That(index.FindBranch("warrior-s002").Name, Is.EqualTo("Branch 2"));
            Assert.That(index.BranchName("warrior-s009"), Is.EqualTo("Branch 9"));
        }

        [Test]
        public void Load_WithoutChampionJsonMarksNoCurrentChampion()
        {
            BuildTree();
            File.Delete(Path.Combine(ChampionDirectory(), LineageStore.ChampionJsonFileName));

            LineageIndex index = LineageStore.Load(runs, Behavior);

            Assert.That(index.FindVersion(ChampionVersionId).IsCurrentChampion, Is.False);
            Assert.That(index.FindVersion(ChampionVersionId).WasChampion, Is.True);
        }

        // ------------------------------------------------------------------ labels

        [Test]
        public void Labels_RoundTripKeepsOtherEntries()
        {
            BuildTree();

            Assert.That(LineageStore.RenameVersion(runs, Behavior, ChampionVersionId, "  Vô địch  ", out string error), Is.True, error);
            LineageLabelsFile labels = ReadLabelsFile();
            Assert.That(FindVersionLabel(labels, ChampionVersionId).name, Is.EqualTo("Vô địch"));
            Assert.That(FindVersionLabel(labels, PlainVersionId).name, Is.EqualTo("Thử nghiệm"), "other version kept");
            Assert.That(FindVersionLabel(labels, PlainVersionId).pinned, Is.True);
            Assert.That(FindVersionLabel(labels, "gone-version"), Is.Not.Null, "unknown entries are kept too");
            Assert.That(FindBranchLabel(labels, "warrior-s001").name, Is.EqualTo("Chính"), "branch labels kept");

            Assert.That(LineageStore.SetPinned(runs, Behavior, ChampionVersionId, true, out error), Is.True, error);
            labels = ReadLabelsFile();
            Assert.That(FindVersionLabel(labels, ChampionVersionId).pinned, Is.True);
            Assert.That(FindVersionLabel(labels, ChampionVersionId).name, Is.EqualTo("Vô địch"));

            // An empty name keeps a pinned entry; unpinning a nameless entry removes it.
            Assert.That(LineageStore.RenameVersion(runs, Behavior, PlainVersionId, "", out error), Is.True, error);
            Assert.That(FindVersionLabel(ReadLabelsFile(), PlainVersionId), Is.Not.Null);
            Assert.That(LineageStore.SetPinned(runs, Behavior, PlainVersionId, false, out error), Is.True, error);
            Assert.That(FindVersionLabel(ReadLabelsFile(), PlainVersionId), Is.Null);

            Assert.That(LineageStore.RenameBranch(runs, Behavior, "warrior-s002", "Nhánh thử", out error), Is.True, error);
            labels = ReadLabelsFile();
            Assert.That(FindBranchLabel(labels, "warrior-s002").name, Is.EqualTo("Nhánh thử"));
            Assert.That(FindBranchLabel(labels, "warrior-s001").name, Is.EqualTo("Chính"));
            Assert.That(FindVersionLabel(labels, ChampionVersionId).name, Is.EqualTo("Vô địch"), "version labels kept");

            Assert.That(LineageStore.RenameBranch(runs, Behavior, "warrior-s001", "   ", out error), Is.True, error);
            Assert.That(FindBranchLabel(ReadLabelsFile(), "warrior-s001"), Is.Null, "empty name restores the default");

            LineageIndex index = LineageStore.Load(runs, Behavior);
            Assert.That(index.FindVersion(ChampionVersionId).Name, Is.EqualTo("Vô địch"));
            Assert.That(index.FindVersion(ChampionVersionId).Pinned, Is.True);
            Assert.That(index.FindVersion(PlainVersionId).Pinned, Is.False);
            Assert.That(index.FindBranch("warrior-s001").Name, Is.EqualTo("Branch 1"));
            Assert.That(index.FindBranch("warrior-s002").Name, Is.EqualTo("Nhánh thử"));
            string lineageDirectory = Path.GetDirectoryName(LineageStore.LabelsPath(runs, Behavior));
            Assert.That(Directory.GetFiles(lineageDirectory, "*.tmp"), Is.Empty);
            Assert.That(Directory.GetFiles(lineageDirectory, "*.bak"), Is.Empty);
        }

        [Test]
        public void Labels_AreNotOverwrittenWhileTheFileCannotBeRead()
        {
            BuildTree();
            string path = LineageStore.LabelsPath(runs, Behavior);
            const string damaged = "{\"versions\":[{\"id\":\"x\",\"pinned\":tr";
            File.WriteAllText(path, damaged);

            Assert.That(LineageStore.SetPinned(runs, Behavior, ChampionVersionId, true, out string error), Is.False);
            Assert.That(error, Does.Contain("labels.json"));
            Assert.That(LineageStore.RenameVersion(runs, Behavior, ChampionVersionId, "Mới", out error), Is.False);
            Assert.That(LineageStore.RenameBranch(runs, Behavior, "warrior-s001", "Mới", out error), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(damaged), "the owner's other pins are not dropped");

            // An empty file holds nothing to lose, so edits go ahead.
            File.WriteAllText(path, "  ");
            Assert.That(LineageStore.SetPinned(runs, Behavior, ChampionVersionId, true, out error), Is.True, error);
            Assert.That(FindVersionLabel(ReadLabelsFile(), ChampionVersionId).pinned, Is.True);
        }

        [Test]
        public void Labels_AreCreatedWhenTheFileIsMissing()
        {
            Assert.That(LineageStore.SetPinned(runs, Behavior, "warrior-s001-100", true, out string error), Is.True, error);

            string path = LineageStore.LabelsPath(runs, Behavior);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(FindVersionLabel(ReadLabelsFile(), "warrior-s001-100").pinned, Is.True);
            Assert.That(LineageStore.ParseLabels("not json").versions, Is.Empty);
            Assert.That(LineageStore.ParseLabels(null).branches, Is.Empty);
        }

        [Test]
        public void Labels_RejectMissingRunsFolder()
        {
            Assert.That(LineageStore.RenameVersion(null, Behavior, "x", "y", out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        // ------------------------------------------------------------------ runs used by the viewer

        [Test]
        public void TrainRunId_NeedsACurrentSchemaRunWithCheckpoint()
        {
            BuildTree();

            Assert.That(LineageStore.TrainRunId(runs, Behavior, "warrior-s001"), Is.EqualTo("warrior-s001"));
            Assert.That(LineageStore.TrainRunId(runs, Behavior, "warrior-s002"), Is.Null, "no checkpoint.pt");
            Assert.That(LineageStore.TrainRunId(runs, Behavior, "warrior-s003"), Is.Null, "old schema");
            Assert.That(LineageStore.TrainRunId(runs, Behavior, "warrior-s999"), Is.Null, "missing run");
            Assert.That(LineageStore.TrainRunId(runs, Behavior, null), Is.Null);
            Assert.That(LineageStore.TrainRunId(runs, Behavior, "../warrior-s001"), Is.Null);
            Assert.That(LineageStore.TrainRunId(runs, Behavior, "champions"), Is.Null);
        }

        [Test]
        public void BranchLatestBrain_FollowsTheActiveBranch()
        {
            BuildTree();

            string first = LineageStore.BranchLatestBrain(runs, Behavior, "warrior-s001");
            Assert.That(first, Is.EqualTo(Path.Combine(runs, "warrior-s001", Behavior, "latest.brain")));
            Assert.That(LineageStore.BranchLatestBrain(runs, Behavior, "warrior-s002"), Is.Not.Null);
            Assert.That(LineageStore.BranchLatestBrain(runs, Behavior, "warrior-s003"), Is.Null, "old schema");
            Assert.That(LineageStore.BranchLatestBrain(runs, Behavior, "warrior-s999"), Is.Null);
            Assert.That(LineageStore.BranchLatestBrain(runs, Behavior, null), Is.Null);
        }

        [Test]
        public void ReadBrainStep_ReadsTheHeader()
        {
            string path = Path.Combine(root, "test.brain");
            WriteBrain(path, 123456789L);
            Assert.That(LineageStore.ReadBrainStep(path), Is.EqualTo(123456789L));

            File.WriteAllText(Path.Combine(root, "bad.brain"), "nope");
            Assert.That(LineageStore.ReadBrainStep(Path.Combine(root, "bad.brain")), Is.EqualTo(0L));
            Assert.That(LineageStore.ReadBrainStep(Path.Combine(root, "missing.brain")), Is.EqualTo(0L));
        }

        [Test]
        public void NewestCheckpointStep_IgnoresOtherFiles()
        {
            string folder = Directory.CreateDirectory(Path.Combine(root, "b")).FullName;
            File.WriteAllText(Path.Combine(folder, "Warrior-500.pt"), "x");
            File.WriteAllText(Path.Combine(folder, "Warrior-9000.onnx"), "x");
            File.WriteAllText(Path.Combine(folder, "Warrior-99999.txt"), "x");
            File.WriteAllText(Path.Combine(folder, "Warrior-abc.pt"), "x");

            Assert.That(LineageStore.NewestCheckpointStep(folder, "Warrior"), Is.EqualTo(9000L));
            Assert.That(LineageStore.NewestCheckpointStep(Path.Combine(root, "missing"), "Warrior"), Is.EqualTo(0L));
        }

        // ------------------------------------------------------------------ helpers

        private void BuildTree()
        {
            // warrior-s001: current schema, checkpoint.pt, numbered checkpoints, latest.brain.
            string s001 = MakeRun("warrior-s001", BrainLocator.CurrentSchemaVersion.ToString());
            File.WriteAllText(Path.Combine(s001, "checkpoint.pt"), "pt");
            File.WriteAllText(Path.Combine(s001, "Warrior-500.pt"), "pt");
            File.WriteAllText(Path.Combine(s001, "Warrior-96999889.pt"), "pt");
            WriteBrain(Path.Combine(s001, "latest.brain"), 97000000L);

            // warrior-s002: current schema, only latest.brain (cannot be trained on).
            string s002 = MakeRun("warrior-s002", BrainLocator.CurrentSchemaVersion.ToString());
            WriteBrain(Path.Combine(s002, "latest.brain"), 1500000L);

            // warrior-s003: old schema, not a branch even with a checkpoint.
            string s003 = MakeRun("warrior-s003", "3");
            File.WriteAllText(Path.Combine(s003, "checkpoint.pt"), "pt");
            WriteBrain(Path.Combine(s003, "latest.brain"), 5000L);

            // Folders that are never branches.
            MakeRun(".hidden", BrainLocator.CurrentSchemaVersion.ToString());
            string lineage = Path.Combine(ChampionDirectory(), "lineage");
            string versions = Directory.CreateDirectory(Path.Combine(lineage, "versions")).FullName;

            WriteVersion(versions, ChampionVersionId,
                "{\"id\":\"" + ChampionVersionId + "\",\"run_id\":\"warrior-s001\",\"step\":96999889,\"schema_version\":" + PersonalArena.Core.Survivor.SurvivorObservation.SchemaVersion + "," +
                "\"source\":\"champion\",\"created_at\":\"2026-09-30T08:00:00Z\",\"evaluated\":true,\"score\":512.5," +
                "\"champion\":true,\"passes_m4a\":true,\"summary\":{\"Runs\":100,\"MedianSurvivedSeconds\":420.5," +
                "\"P10SurvivedSeconds\":300.0,\"WinRate\":0.25,\"DamageTakenPerMinute\":40.0," +
                "\"DeathCauseCounts\":{\"Surrounded\":3,\"Boss\":1}},\"behavior\":{\"Aggression\":0.6}," +
                "\"brain_file\":\"brain.brain\",\"has_checkpoint\":true}",
                96999889L, true);
            WriteVersion(versions, PlainVersionId,
                "{\"id\":\"" + PlainVersionId + "\",\"run_id\":\"warrior-s002\",\"step\":1500000,\"schema_version\":" + PersonalArena.Core.Survivor.SurvivorObservation.SchemaVersion + "," +
                "\"source\":\"snapshot\",\"created_at\":\"2026-09-30T09:00:00Z\",\"evaluated\":false,\"score\":NaN," +
                "\"summary\":null,\"has_checkpoint\":false}",
                1500000L, false);
            WriteVersion(versions, OldSchemaVersionId,
                "{\"id\":\"" + OldSchemaVersionId + "\",\"run_id\":\"warrior-s003\",\"step\":5000,\"schema_version\":3," +
                "\"source\":\"import\",\"created_at\":\"2026-09-01T09:00:00Z\",\"evaluated\":false}",
                5000L, false);

            // A partial folder (brain but no version.json) and a version.json without brain: both ignored.
            string partial = Directory.CreateDirectory(Path.Combine(versions, "warrior-s001-777")).FullName;
            WriteBrain(Path.Combine(partial, "brain.brain"), 777L);
            string noBrain = Directory.CreateDirectory(Path.Combine(versions, "warrior-s001-888")).FullName;
            File.WriteAllText(Path.Combine(noBrain, "version.json"),
                "{\"id\":\"warrior-s001-888\",\"run_id\":\"warrior-s001\",\"step\":888}");

            File.WriteAllText(Path.Combine(lineage, "branches.json"),
                "{\"branches\":[{\"run_id\":\"warrior-s002\",\"parent_run\":\"warrior-s001\",\"parent_step\":96999889," +
                "\"parent_version\":\"" + ChampionVersionId + "\",\"created_at\":\"2026-09-30T09:00:00Z\"}," +
                "{\"run_id\":\"warrior-s404\",\"parent_run\":\"warrior-s001\",\"parent_step\":1}]}",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(lineage, "labels.json"),
                "{\"versions\":[{\"id\":\"" + PlainVersionId + "\",\"name\":\"Thử nghiệm\",\"pinned\":true}," +
                "{\"id\":\"gone-version\",\"name\":\"Cũ\",\"pinned\":false}]," +
                "\"branches\":[{\"run_id\":\"warrior-s001\",\"name\":\"Chính\"}]}",
                new UTF8Encoding(false));

            File.WriteAllText(Path.Combine(ChampionDirectory(), "champion.json"),
                "{\"run_id\":\"warrior-s001\",\"step\":96999889,\"score\":512.5,\"passes_m4a\":true," +
                "\"summary\":{\"Runs\":100,\"MedianSurvivedSeconds\":420.5}}",
                new UTF8Encoding(false));
        }

        private string ChampionDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(runs, "champions", Behavior)).FullName;
        }

        /// <summary>Creates runs/&lt;name&gt; with a schema file and a behavior folder; returns the behavior folder.</summary>
        private string MakeRun(string name, string schema)
        {
            string run = Directory.CreateDirectory(Path.Combine(runs, name)).FullName;
            File.WriteAllText(Path.Combine(run, BrainLocator.SchemaFileName), schema + "\n");
            return Directory.CreateDirectory(Path.Combine(run, Behavior)).FullName;
        }

        private static void WriteVersion(string versions, string id, string json, long step, bool withCheckpoint)
        {
            string folder = Directory.CreateDirectory(Path.Combine(versions, id)).FullName;
            WriteBrain(Path.Combine(folder, "brain.brain"), step);
            if (withCheckpoint)
            {
                File.WriteAllText(Path.Combine(folder, "checkpoint.pt"), "pt");
            }

            File.WriteAllText(Path.Combine(folder, "version.json"), json, new UTF8Encoding(false));
        }

        private static void WriteBrain(string path, long step)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                byte[] name = Encoding.UTF8.GetBytes("Warrior");
                writer.Write(new[] { (byte)'P', (byte)'A', (byte)'B', (byte)'R' });
                writer.Write(1);
                writer.Write(name.Length);
                writer.Write(name);
                writer.Write(step);
                writer.Write(new byte[16]);
            }
        }

        private LineageLabelsFile ReadLabelsFile()
        {
            return LineageStore.ReadLabels(LineageStore.LineageDirectory(runs, Behavior));
        }

        private static LineageVersionLabel FindVersionLabel(LineageLabelsFile labels, string id)
        {
            for (int i = 0; i < labels.versions.Length; i++)
            {
                if (labels.versions[i] != null && labels.versions[i].id == id)
                {
                    return labels.versions[i];
                }
            }

            return null;
        }

        private static LineageBranchLabel FindBranchLabel(LineageLabelsFile labels, string runId)
        {
            for (int i = 0; i < labels.branches.Length; i++)
            {
                if (labels.branches[i] != null && labels.branches[i].run_id == runId)
                {
                    return labels.branches[i];
                }
            }

            return null;
        }
    }
}
