using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>version.json of one saved brain (written last by Trainer/brain_lineage.py).</summary>
    [Serializable]
    public sealed class LineageVersionFile
    {
        public string id;
        public string run_id;
        public long step;
        public int schema_version;
        public string source;
        public string created_at;
        public bool evaluated;
        public float score;
        public bool champion;
        public bool passes_m4a;
        public ChampionSummary summary;
        public ChampionBehavior behavior;
        public string evaluated_at;
        public string brain_file;
        public bool has_checkpoint;
    }

    /// <summary>One entry of lineage/branches.json (written only by Python).</summary>
    [Serializable]
    public sealed class LineageBranchEntry
    {
        public string run_id;
        public string parent_run;
        public long parent_step;
        public string parent_version;
        public string created_at;
    }

    [Serializable]
    public sealed class LineageBranchesFile
    {
        public LineageBranchEntry[] branches;
    }

    [Serializable]
    public sealed class LineageVersionLabel
    {
        public string id;
        public string name;
        public bool pinned;
    }

    [Serializable]
    public sealed class LineageBranchLabel
    {
        public string run_id;
        public string name;
    }

    /// <summary>lineage/labels.json: the owner's names and pins (written only by the viewer).</summary>
    [Serializable]
    public sealed class LineageLabelsFile
    {
        public LineageVersionLabel[] versions = new LineageVersionLabel[0];
        public LineageBranchLabel[] branches = new LineageBranchLabel[0];
    }

    /// <summary>A saved brain version as the panel shows it.</summary>
    public sealed class LineageVersion
    {
        public string Id;
        public string RunId;
        public long Step;
        public int SchemaVersion;
        public string Source;
        public string CreatedAt;
        public bool Evaluated;
        public float Score;
        /// <summary>This brain became the champion when it was evaluated ("từng giỏi nhất").</summary>
        public bool WasChampion;
        public bool PassesM4A;
        /// <summary>Evaluation summary, or null when the version was never evaluated.</summary>
        public ChampionSummary Summary;
        public ChampionBehavior Behavior;
        public string EvaluatedAt;
        public string Directory;
        public string BrainPath;
        public string CheckpointPath;
        /// <summary>checkpoint.pt exists on disk now (needed to fork).</summary>
        public bool HasCheckpoint;
        /// <summary>It is the brain of the current champion.json ("GIỎI NHẤT").</summary>
        public bool IsCurrentChampion;
        public bool Pinned;
        /// <summary>The owner's name, or null.</summary>
        public string CustomName;
        public Dictionary<string, int> DeathCauses = new Dictionary<string, int>();

        public string Name => string.IsNullOrEmpty(CustomName) ? LineageStore.DefaultVersionName(Step) : CustomName;

        /// <summary>Has a usable evaluation (compare and the row numbers need it).</summary>
        public bool HasEvaluation => Evaluated && Summary != null && Summary.Runs > 0;
    }

    /// <summary>A training run the owner can watch, train or fork ("nhánh").</summary>
    public sealed class LineageBranch
    {
        public string RunId;
        public string Directory;
        /// <summary>False for a run that only survives through saved versions (folder gone or old schema).</summary>
        public bool OnDisk;
        public string ParentRun;
        public long ParentStep;
        public string ParentVersion;
        public string CreatedAt;
        public string CustomName;
        /// <summary>Newest numbered checkpoint step, else the latest.brain step, else 0.</summary>
        public long CurrentStep;
        /// <summary>&lt;run&gt;/&lt;Behavior&gt;/checkpoint.pt exists (TRAIN can continue it).</summary>
        public bool HasCheckpoint;
        /// <summary>&lt;run&gt;/&lt;Behavior&gt;/latest.brain, or null.</summary>
        public string LatestBrainPath;

        public string Name => string.IsNullOrEmpty(CustomName) ? LineageStore.DefaultBranchName(RunId) : CustomName;
        public bool HasParent => !string.IsNullOrEmpty(ParentRun);
    }

    /// <summary>Everything the lineage panel shows, read from disk in one go.</summary>
    public sealed class LineageIndex
    {
        public string RunsDirectory;
        public string Behavior;
        public string LineageDirectory;
        public readonly List<LineageBranch> Branches = new List<LineageBranch>();
        /// <summary>All versions, newest first.</summary>
        public readonly List<LineageVersion> Versions = new List<LineageVersion>();
        public LineageLabelsFile Labels = new LineageLabelsFile();

        public LineageBranch FindBranch(string runId)
        {
            if (string.IsNullOrEmpty(runId))
            {
                return null;
            }

            for (int i = 0; i < Branches.Count; i++)
            {
                if (string.Equals(Branches[i].RunId, runId, StringComparison.OrdinalIgnoreCase))
                {
                    return Branches[i];
                }
            }

            return null;
        }

        public LineageVersion FindVersion(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < Versions.Count; i++)
            {
                if (string.Equals(Versions[i].Id, id, StringComparison.Ordinal))
                {
                    return Versions[i];
                }
            }

            return null;
        }

        /// <summary>Versions of one run, newest first.</summary>
        public List<LineageVersion> VersionsOf(string runId)
        {
            List<LineageVersion> result = new List<LineageVersion>();
            for (int i = 0; i < Versions.Count; i++)
            {
                if (string.Equals(Versions[i].RunId, runId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(Versions[i]);
                }
            }

            return result;
        }

        /// <summary>The branch's display name (label or default), also for runs that are not branches.</summary>
        public string BranchName(string runId)
        {
            LineageBranch branch = FindBranch(runId);
            return branch != null ? branch.Name : LineageStore.DefaultBranchName(runId);
        }
    }

    /// <summary>
    /// Reads the brain lineage store (Trainer/runs/champions/&lt;Behavior&gt;/lineage, written by
    /// Trainer/brain_lineage.py) and the run folders, and writes the owner's labels (labels.json, atomic).
    /// Never throws on bad files: they are skipped.
    /// </summary>
    public static class LineageStore
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
                return "Nhánh ?";
            }

            Match match = SurvivorRunNumber.Match(runId);
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                return "Nhánh " + number.ToString(CultureInfo.InvariantCulture);
            }

            return runId;
        }

        /// <summary>96_999_889 → "Bước 97,0 tr"; below a million "Bước 950 nghìn".</summary>
        public static string DefaultVersionName(long step)
        {
            return "Bước " + FormatStep(step);
        }

        /// <summary>A step count in Vietnamese: "97,0 tr", "950 nghìn", "500".</summary>
        public static string FormatStep(long step)
        {
            if (step < 0)
            {
                step = 0;
            }

            if (step >= 1000000)
            {
                return (step / 1000000.0).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " tr";
            }

            if (step >= 1000)
            {
                return (step / 1000).ToString(CultureInfo.InvariantCulture) + " nghìn";
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

        // ------------------------------------------------------------------ labels (viewer writes)

        public static LineageLabelsFile ReadLabels(string lineageDirectory)
        {
            if (string.IsNullOrEmpty(lineageDirectory))
            {
                return new LineageLabelsFile();
            }

            return ParseLabels(ReadText(Path.Combine(lineageDirectory, LabelsFileName)));
        }

        /// <summary>
        /// Reads labels.json before an edit. False when the file exists but cannot be read or parsed
        /// (locked, half-written, damaged): saving then would drop every other name and pin.
        /// </summary>
        private static bool TryReadLabelsForEdit(string lineageDirectory, out LineageLabelsFile labels, out string error)
        {
            error = null;
            string path = Path.Combine(lineageDirectory, LabelsFileName);
            string text = ReadText(path);
            bool unreadable = text == null
                ? SafeFileExists(path)
                : !string.IsNullOrWhiteSpace(text) && ParseJson<LineageLabelsFile>(text) == null;
            if (unreadable)
            {
                labels = null;
                error = "Không đọc được tên/ghim đã lưu (labels.json) nên chưa đổi gì. Thử lại sau giây lát.";
                return false;
            }

            labels = ParseLabels(text);
            return true;
        }

        /// <summary>Renames a version (an empty name restores the default). Re-reads labels.json first, keeps other entries.</summary>
        public static bool RenameVersion(string runsDirectory, string behavior, string versionId, string name, out string error)
        {
            return UpdateVersionLabel(runsDirectory, behavior, versionId, label => label.name = CleanName(name) ?? string.Empty, out error);
        }

        /// <summary>Pins or unpins a version (a pinned version is never pruned by the trainer).</summary>
        public static bool SetPinned(string runsDirectory, string behavior, string versionId, bool pinned, out string error)
        {
            return UpdateVersionLabel(runsDirectory, behavior, versionId, label => label.pinned = pinned, out error);
        }

        /// <summary>Renames a branch (an empty name restores the default). Keeps other entries.</summary>
        public static bool RenameBranch(string runsDirectory, string behavior, string runId, string name, out string error)
        {
            error = null;
            string lineage = LineageDirectory(runsDirectory, behavior);
            if (lineage == null || string.IsNullOrEmpty(runId))
            {
                error = "Không tìm thấy thư mục lịch sử não.";
                return false;
            }

            if (!TryReadLabelsForEdit(lineage, out LineageLabelsFile labels, out error))
            {
                return false;
            }

            List<LineageBranchLabel> list = new List<LineageBranchLabel>();
            string clean = CleanName(name);
            for (int i = 0; i < labels.branches.Length; i++)
            {
                LineageBranchLabel label = labels.branches[i];
                if (label == null || string.IsNullOrEmpty(label.run_id) ||
                    string.Equals(label.run_id, runId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(label);
            }

            if (clean != null)
            {
                list.Add(new LineageBranchLabel { run_id = runId, name = clean });
            }

            labels.branches = list.ToArray();
            return SaveLabels(lineage, labels, out error);
        }

        private static bool UpdateVersionLabel(string runsDirectory, string behavior, string versionId,
            Action<LineageVersionLabel> change, out string error)
        {
            error = null;
            string lineage = LineageDirectory(runsDirectory, behavior);
            if (lineage == null || string.IsNullOrEmpty(versionId))
            {
                error = "Không tìm thấy thư mục lịch sử não.";
                return false;
            }

            if (!TryReadLabelsForEdit(lineage, out LineageLabelsFile labels, out error))
            {
                return false;
            }

            List<LineageVersionLabel> list = new List<LineageVersionLabel>();
            LineageVersionLabel target = null;
            for (int i = 0; i < labels.versions.Length; i++)
            {
                LineageVersionLabel label = labels.versions[i];
                if (label == null || string.IsNullOrEmpty(label.id))
                {
                    continue;
                }

                if (string.Equals(label.id, versionId, StringComparison.Ordinal))
                {
                    if (target != null)
                    {
                        continue; // drop duplicates
                    }

                    target = label;
                }

                list.Add(label);
            }

            if (target == null)
            {
                target = new LineageVersionLabel { id = versionId, name = string.Empty };
                list.Add(target);
            }

            change(target);
            target.name = target.name ?? string.Empty;
            if (string.IsNullOrEmpty(target.name) && !target.pinned)
            {
                list.Remove(target);
            }

            labels.versions = list.ToArray();
            return SaveLabels(lineage, labels, out error);
        }

        /// <summary>
        /// Atomic write of labels.json: a temp file of its own, then File.Replace (move when new). When
        /// File.Replace is refused, the old file becomes labels.json.bak until the new one is in place.
        /// </summary>
        public static bool SaveLabels(string lineageDirectory, LineageLabelsFile labels, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(lineageDirectory) || labels == null)
            {
                error = "Không tìm thấy thư mục lịch sử não.";
                return false;
            }

            string path = Path.Combine(lineageDirectory, LabelsFileName);
            string temp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            try
            {
                System.IO.Directory.CreateDirectory(lineageDirectory);
                labels.versions = labels.versions ?? new LineageVersionLabel[0];
                labels.branches = labels.branches ?? new LineageBranchLabel[0];
                byte[] bytes = Utf8.GetBytes(JsonUtility.ToJson(labels, true));
                using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temp, path, null, true);
                    }
                    catch (Exception exception) when (exception is IOException || exception is PlatformNotSupportedException ||
                        exception is UnauthorizedAccessException || exception is NotSupportedException)
                    {
                        ReplaceThroughBackup(temp, path);
                    }
                }
                else
                {
                    File.Move(temp, path);
                }

                return true;
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                TryDelete(temp);
                Debug.LogWarning("Could not save " + path + ": " + exception.Message);
                error = "Không lưu được tên/ghim: " + exception.Message;
                return false;
            }
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
