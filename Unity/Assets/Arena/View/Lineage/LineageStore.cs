using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Reads the brain lineage store (Trainer/runs/champions/&lt;Behavior&gt;/lineage, written by
    /// Trainer/brain_lineage.py) and the run folders, and writes the owner's labels (labels.json, atomic).
    /// Never throws on bad files: they are skipped.
    /// </summary>
    public static partial class LineageStore
    {
        public const string LineageDirectoryName = "lineage";
        public const string VersionsDirectoryName = "versions";
        public const string VersionFileName = "version.json";
        public const string BrainFileName = "brain.brain";
        public const string CheckpointFileName = "checkpoint.pt";
        public const string BranchesFileName = "branches.json";
        public const string LabelsFileName = "labels.json";
        public const string ChampionJsonFileName = "champion.json";
        public const int MaxNameLength = 24;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private static readonly Regex SurvivorRunNumber = new Regex("-s(\\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NonFiniteNumber = new Regex("(?<=[:\\[,]\\s*)-?(NaN|Infinity)", RegexOptions.Compiled);
        private static readonly byte[] BrainMagic = { (byte)'P', (byte)'A', (byte)'B', (byte)'R' };

        /// <summary>champions/&lt;Behavior&gt;/lineage, or null without a runs folder.</summary>
        public static string LineageDirectory(string runsDirectory, string behavior)
        {
            string champions = BrainLocator.ChampionDirectory(runsDirectory, behavior);
            return champions == null ? null : Path.Combine(champions, LineageDirectoryName);
        }

        public static string LabelsPath(string runsDirectory, string behavior)
        {
            string lineage = LineageDirectory(runsDirectory, behavior);
            return lineage == null ? null : Path.Combine(lineage, LabelsFileName);
        }

        // ------------------------------------------------------------------ names

        /// <summary>"warrior-s001" → "Nhánh 1"; any other run id is shown as is.</summary>
        public static string DefaultBranchName(string runId)
        {
            if (string.IsNullOrEmpty(runId))
            {
                return "Branch ?";
            }

            Match match = SurvivorRunNumber.Match(runId);
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                return "Branch " + number.ToString(CultureInfo.InvariantCulture);
            }

            return runId;
        }

        /// <summary>96_999_889 → "Step 97.0M"; below a million "Step 950K".</summary>
        public static string DefaultVersionName(long step)
        {
            return "Step " + FormatStep(step);
        }

        /// <summary>A short step count: "97.0M", "950K", "500".</summary>
        public static string FormatStep(long step)
        {
            if (step < 0)
            {
                step = 0;
            }

            if (step >= 1000000)
            {
                return (step / 1000000.0).ToString("0.0", CultureInfo.InvariantCulture) + "M";
            }

            if (step >= 1000)
            {
                return (step / 1000).ToString(CultureInfo.InvariantCulture) + "K";
            }

            return step.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>An ISO time from the trainer as local "dd/MM HH:mm", or "" when it cannot be read.</summary>
        public static string FormatDate(string iso)
        {
            if (string.IsNullOrEmpty(iso))
            {
                return string.Empty;
            }

            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime utc))
            {
                return utc.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        /// <summary>Trims a name the owner typed and clips it to <see cref="MaxNameLength"/>; null when empty.</summary>
        public static string CleanName(string name)
        {
            if (name == null)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                builder.Append(char.IsControl(c) ? ' ' : c);
            }

            string clean = builder.ToString().Trim();
            if (clean.Length > MaxNameLength)
            {
                clean = clean.Substring(0, MaxNameLength).TrimEnd();
            }

            return clean.Length == 0 ? null : clean;
        }

        // ------------------------------------------------------------------ load

        /// <summary>Loads branches, versions, parents and labels. Never throws; bad folders and files are skipped.</summary>
        public static LineageIndex Load(string runsDirectory, string behavior)
        {
            LineageIndex index = new LineageIndex
            {
                RunsDirectory = runsDirectory,
                Behavior = behavior,
                LineageDirectory = LineageDirectory(runsDirectory, behavior)
            };

            if (string.IsNullOrEmpty(runsDirectory) || string.IsNullOrEmpty(behavior) || !SafeDirectoryExists(runsDirectory))
            {
                return index;
            }

            try
            {
                index.Labels = ReadLabels(index.LineageDirectory);
                ChampionRecord champion = ReadChampion(runsDirectory, behavior);
                LoadVersions(index, champion);
                LoadBranches(index, ReadBranches(index.LineageDirectory));
                ApplyLabels(index);
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                Debug.LogWarning("Could not read the brain lineage in " + runsDirectory + ": " + exception.Message);
            }

            return index;
        }

        /// <summary>Parses version.json text; null when it is not a version (no id, run or brain step).</summary>
        public static LineageVersionFile ParseVersion(string json)
        {
            LineageVersionFile file = ParseJson<LineageVersionFile>(json);
            return file == null || string.IsNullOrEmpty(file.id) || string.IsNullOrEmpty(file.run_id) ? null : file;
        }

        public static LineageLabelsFile ParseLabels(string json)
        {
            LineageLabelsFile labels = ParseJson<LineageLabelsFile>(json) ?? new LineageLabelsFile();
            labels.versions = labels.versions ?? new LineageVersionLabel[0];
            labels.branches = labels.branches ?? new LineageBranchLabel[0];
            return labels;
        }

        public static LineageBranchesFile ParseBranches(string json)
        {
            LineageBranchesFile branches = ParseJson<LineageBranchesFile>(json) ?? new LineageBranchesFile();
            branches.branches = branches.branches ?? new LineageBranchEntry[0];
            return branches;
        }

        private static T ParseJson<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                // Python's json writes NaN / Infinity, which JsonUtility rejects.
                return JsonUtility.FromJson<T>(NonFiniteNumber.Replace(json, "0"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void LoadVersions(LineageIndex index, ChampionRecord champion)
        {
            string versionsDirectory = index.LineageDirectory == null ? null : Path.Combine(index.LineageDirectory, VersionsDirectoryName);
            if (versionsDirectory == null || !SafeDirectoryExists(versionsDirectory))
            {
                return;
            }

            string[] folders;
            try
            {
                folders = System.IO.Directory.GetDirectories(versionsDirectory);
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                return;
            }

            for (int i = 0; i < folders.Length; i++)
            {
                if (Path.GetFileName(folders[i]).StartsWith(".", StringComparison.Ordinal))
                {
                    continue;
                }

                LineageVersion version = ReadVersion(folders[i]);
                if (version == null)
                {
                    continue;
                }

                version.IsCurrentChampion = champion != null && champion.step == version.Step &&
                    string.Equals(champion.run_id, version.RunId, StringComparison.OrdinalIgnoreCase);
                index.Versions.Add(version);
            }

            index.Versions.Sort(CompareNewestFirst);
        }

        /// <summary>Newest first: higher step, then later creation, then id.</summary>
        public static int CompareNewestFirst(LineageVersion a, LineageVersion b)
        {
            int byStep = b.Step.CompareTo(a.Step);
            if (byStep != 0)
            {
                return byStep;
            }

            int byTime = string.CompareOrdinal(b.CreatedAt ?? string.Empty, a.CreatedAt ?? string.Empty);
            return byTime != 0 ? byTime : string.CompareOrdinal(a.Id, b.Id);
        }

        private static LineageVersion ReadVersion(string folder)
        {
            string jsonPath = Path.Combine(folder, VersionFileName);
            string text = ReadText(jsonPath);
            LineageVersionFile file = ParseVersion(text);
            if (file == null)
            {
                return null;
            }

            string brainName = string.IsNullOrEmpty(file.brain_file) || file.brain_file.IndexOfAny(new[] { '/', '\\' }) >= 0
                ? BrainFileName
                : file.brain_file;
            string brainPath = Path.Combine(folder, brainName);
            if (!SafeFileExists(brainPath))
            {
                brainPath = Path.Combine(folder, BrainFileName);
                if (!SafeFileExists(brainPath))
                {
                    return null;
                }
            }

            string checkpoint = Path.Combine(folder, CheckpointFileName);
            bool evaluated = file.evaluated && file.summary != null && file.summary.Runs > 0;
            return new LineageVersion
            {
                Id = file.id,
                RunId = file.run_id,
                Step = file.step,
                SchemaVersion = file.schema_version,
                Source = file.source,
                CreatedAt = file.created_at,
                Evaluated = evaluated,
                Score = file.score,
                WasChampion = file.champion,
                PassesM4A = file.passes_m4a,
                Summary = evaluated ? file.summary : null,
                Behavior = evaluated ? (file.behavior ?? new ChampionBehavior()) : null,
                EvaluatedAt = file.evaluated_at,
                Directory = folder,
                BrainPath = brainPath,
                CheckpointPath = checkpoint,
                HasCheckpoint = SafeFileExists(checkpoint),
                DeathCauses = evaluated ? ChampionInfo.ParseDeathCauses(text) : new Dictionary<string, int>()
            };
        }

        private static void LoadBranches(LineageIndex index, LineageBranchesFile parents)
        {
            string[] runs;
            try
            {
                runs = System.IO.Directory.GetDirectories(index.RunsDirectory);
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                runs = new string[0];
            }

            for (int i = 0; i < runs.Length; i++)
            {
                string name = Path.GetFileName(runs[i]);
                if (!IsBranchFolderName(name) ||
                    BrainLocator.RunSchemaVersion(runs[i]) != BrainLocator.CurrentSchemaVersion ||
                    !SafeDirectoryExists(Path.Combine(runs[i], index.Behavior)))
                {
                    continue;
                }

                string behaviorDirectory = Path.Combine(runs[i], index.Behavior);
                string latest = Path.Combine(behaviorDirectory, BrainLocator.LatestFileName);
                LineageBranch branch = new LineageBranch
                {
                    RunId = name,
                    Directory = runs[i],
                    OnDisk = true,
                    HasCheckpoint = SafeFileExists(Path.Combine(behaviorDirectory, CheckpointFileName)),
                    LatestBrainPath = SafeFileExists(latest) ? latest : null
                };
                branch.CurrentStep = NewestCheckpointStep(behaviorDirectory, index.Behavior);
                if (branch.CurrentStep <= 0 && branch.LatestBrainPath != null)
                {
                    branch.CurrentStep = ReadBrainStep(branch.LatestBrainPath);
                }

                index.Branches.Add(branch);
            }

            // Runs that only live on through saved versions: still listed so their versions can be watched.
            for (int i = 0; i < index.Versions.Count; i++)
            {
                LineageVersion version = index.Versions[i];
                if (index.FindBranch(version.RunId) != null)
                {
                    continue;
                }

                index.Branches.Add(new LineageBranch
                {
                    RunId = version.RunId,
                    OnDisk = false,
                    CurrentStep = version.Step
                });
            }

            for (int i = 0; i < parents.branches.Length; i++)
            {
                LineageBranchEntry entry = parents.branches[i];
                LineageBranch branch = entry == null ? null : index.FindBranch(entry.run_id);
                if (branch == null)
                {
                    continue;
                }

                branch.ParentRun = entry.parent_run;
                branch.ParentStep = entry.parent_step;
                branch.ParentVersion = entry.parent_version;
                branch.CreatedAt = entry.created_at;
            }

            index.Branches.Sort((a, b) => string.Compare(a.RunId, b.RunId, StringComparison.OrdinalIgnoreCase));
        }

        private static void ApplyLabels(LineageIndex index)
        {
            LineageLabelsFile labels = index.Labels;
            for (int i = 0; i < labels.versions.Length; i++)
            {
                LineageVersionLabel label = labels.versions[i];
                LineageVersion version = label == null ? null : index.FindVersion(label.id);
                if (version == null)
                {
                    continue;
                }

                version.CustomName = CleanName(label.name);
                version.Pinned = label.pinned;
            }

            for (int i = 0; i < labels.branches.Length; i++)
            {
                LineageBranchLabel label = labels.branches[i];
                LineageBranch branch = label == null ? null : index.FindBranch(label.run_id);
                if (branch != null)
                {
                    branch.CustomName = CleanName(label.name);
                }
            }
        }

        /// <summary>A run folder name that can be a branch ("champions" and hidden/partial folders cannot).</summary>
        public static bool IsBranchFolderName(string name)
        {
            return !string.IsNullOrEmpty(name) && !name.StartsWith(".", StringComparison.Ordinal) &&
                !string.Equals(name, BrainLocator.ChampionsDirectoryName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Highest step of &lt;Behavior&gt;-&lt;step&gt;.pt / .onnx in a run's behavior folder, else 0.</summary>
        public static long NewestCheckpointStep(string behaviorDirectory, string behavior)
        {
            long newest = 0;
            string[] files;
            try
            {
                files = System.IO.Directory.GetFiles(behaviorDirectory, behavior + "-*");
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                return 0;
            }

            string prefix = behavior + "-";
            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]);
                if (!string.Equals(extension, ".pt", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".onnx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string stem = Path.GetFileNameWithoutExtension(files[i]);
                if (stem.Length <= prefix.Length || !stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (long.TryParse(stem.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out long step) &&
                    step > newest)
                {
                    newest = step;
                }
            }

            return newest;
        }

        /// <summary>The training step stored in a .brain header (magic, version, name, step), or 0.</summary>
        public static long ReadBrainStep(string brainPath)
        {
            try
            {
                using (FileStream stream = new FileStream(brainPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    byte[] magic = reader.ReadBytes(4);
                    if (magic.Length != 4 || magic[0] != BrainMagic[0] || magic[1] != BrainMagic[1] ||
                        magic[2] != BrainMagic[2] || magic[3] != BrainMagic[3])
                    {
                        return 0;
                    }

                    reader.ReadInt32(); // format version
                    int nameLength = reader.ReadInt32();
                    if (nameLength < 0 || nameLength > 256)
                    {
                        return 0;
                    }

                    reader.ReadBytes(nameLength);
                    long step = reader.ReadInt64();
                    return step > 0 ? step : 0;
                }
            }
            catch (Exception exception) when (IsIoProblem(exception) || exception is EndOfStreamException)
            {
                return 0;
            }
        }

        // ------------------------------------------------------------------ runs used by the viewer

        /// <summary>
        /// The run TRAIN should continue: <paramref name="profileRunId"/> when that run exists with the current
        /// schema and a &lt;Behavior&gt;/checkpoint.pt, else null (the service then picks the newest run).
        /// </summary>
        public static string TrainRunId(string runsDirectory, string behavior, string profileRunId)
        {
            string runDirectory = BranchDirectory(runsDirectory, behavior, profileRunId);
            return runDirectory != null && SafeFileExists(Path.Combine(runDirectory, behavior, CheckpointFileName))
                ? profileRunId
                : null;
        }

        /// <summary>
        /// &lt;runs&gt;/&lt;runId&gt;/&lt;Behavior&gt;/latest.brain when that run is a current-schema branch and the
        /// file exists, else null ("não mới nhất" of the active branch).
        /// </summary>
        public static string BranchLatestBrain(string runsDirectory, string behavior, string runId)
        {
            string runDirectory = BranchDirectory(runsDirectory, behavior, runId);
            if (runDirectory == null)
            {
                return null;
            }

            string path = Path.Combine(runDirectory, behavior, BrainLocator.LatestFileName);
            return SafeFileExists(path) ? path : null;
        }

        /// <summary>The run folder of a branch when it exists with the current schema and a behavior folder, else null.</summary>
        private static string BranchDirectory(string runsDirectory, string behavior, string runId)
        {
            if (string.IsNullOrEmpty(runsDirectory) || string.IsNullOrEmpty(behavior) ||
                !TrainingServiceClient.IsSafeRunId(runId) || !IsBranchFolderName(runId))
            {
                return null;
            }

            string runDirectory = Path.Combine(runsDirectory, runId);
            if (!SafeDirectoryExists(Path.Combine(runDirectory, behavior)) ||
                BrainLocator.RunSchemaVersion(runDirectory) != BrainLocator.CurrentSchemaVersion)
            {
                return null;
            }

            return runDirectory;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Old file to .bak, new file in, then drop .bak; puts the old file back if the move fails.</summary>
        private static void ReplaceThroughBackup(string temp, string path)
        {
            string backup = path + ".bak";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }

            File.Move(path, backup);
            try
            {
                File.Move(temp, path);
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                if (!File.Exists(path))
                {
                    File.Move(backup, path);
                }

                throw;
            }

            TryDelete(backup);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                // A leftover temp or backup file is harmless; the next save tries again.
            }
        }

        private static ChampionRecord ReadChampion(string runsDirectory, string behavior)
        {
            string directory = BrainLocator.ChampionDirectory(runsDirectory, behavior);
            return directory == null ? null : ChampionInfo.ParseRecord(ReadText(Path.Combine(directory, ChampionJsonFileName)));
        }

        private static LineageBranchesFile ReadBranches(string lineageDirectory)
        {
            return string.IsNullOrEmpty(lineageDirectory)
                ? new LineageBranchesFile { branches = new LineageBranchEntry[0] }
                : ParseBranches(ReadText(Path.Combine(lineageDirectory, BranchesFileName)));
        }

        private static string ReadText(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                return null;
            }
        }

        private static bool SafeFileExists(string path)
        {
            try
            {
                return !string.IsNullOrEmpty(path) && File.Exists(path);
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                return false;
            }
        }

        private static bool SafeDirectoryExists(string path)
        {
            try
            {
                return !string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path);
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                return false;
            }
        }

        private static bool IsIoProblem(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                exception is System.Security.SecurityException || exception is NotSupportedException ||
                exception is ArgumentException;
        }
    }
}
