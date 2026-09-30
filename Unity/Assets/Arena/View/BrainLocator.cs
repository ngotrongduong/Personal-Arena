using System;
using System.IO;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View
{
    /// <summary>Finds the newest exported training brain (Trainer/runs/&lt;run&gt;/&lt;Behavior&gt;/latest.brain).</summary>
    public static class BrainLocator
    {
        public const string LatestFileName = "latest.brain";

        /// <summary>Marker written by the trainer with the observation schema of a run.</summary>
        public const string SchemaFileName = "schema_version.txt";

        /// <summary>Folder under Trainer/runs where the trainer keeps the best brains (not a training run).</summary>
        public const string ChampionsDirectoryName = "champions";

        /// <summary>File name of the current best brain inside champions/&lt;Behavior&gt;.</summary>
        public const string ChampionFileName = "champion.brain";

        /// <summary>The only schema this viewer can drive (the survivor observation layout).</summary>
        public const int CurrentSchemaVersion = SurvivorObservation.SchemaVersion;

        private const int MaximumParentLevels = 6;

        /// <summary>Walks up from <paramref name="startDirectory"/> looking for Trainer/runs.</summary>
        public static string FindRunsDirectory(string startDirectory)
        {
            if (string.IsNullOrEmpty(startDirectory))
            {
                return null;
            }

            DirectoryInfo directory = new DirectoryInfo(startDirectory);
            for (int level = 0; directory != null && level <= MaximumParentLevels; level++)
            {
                string candidate = Path.Combine(directory.FullName, "Trainer", "runs");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }

        /// <summary>The most recently written current-schema latest.brain for <paramref name="behavior"/>, or null.</summary>
        public static string FindNewestBrain(string runsDirectory, string behavior)
        {
            return FindNewestBrain(runsDirectory, behavior, true);
        }

        /// <summary>
        /// The newest exported brain, preferring the current schema. Older brains are considered only
        /// when <paramref name="currentSchemaOnly"/> is false and no current-schema brain exists.
        /// </summary>
        public static string FindNewestBrain(string runsDirectory, string behavior, bool currentSchemaOnly)
        {
            if (string.IsNullOrEmpty(runsDirectory) || string.IsNullOrEmpty(behavior) ||
                !Directory.Exists(runsDirectory))
            {
                return null;
            }

            string newestCurrent = null;
            DateTime newestCurrentTime = DateTime.MinValue;
            string newestFallback = null;
            DateTime newestFallbackTime = DateTime.MinValue;
            string[] runs;
            try
            {
                runs = Directory.GetDirectories(runsDirectory);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            for (int i = 0; i < runs.Length; i++)
            {
                if (IsChampionsDirectory(runs[i]))
                {
                    continue;
                }

                bool currentSchema = RunSchemaVersion(runs[i]) == CurrentSchemaVersion;
                if (currentSchemaOnly && !currentSchema)
                {
                    continue;
                }

                string candidate = Path.Combine(runs[i], behavior, LatestFileName);
                if (!File.Exists(candidate))
                {
                    continue;
                }

                DateTime written;
                try
                {
                    written = File.GetLastWriteTimeUtc(candidate);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                if (currentSchema)
                {
                    if (newestCurrent == null || written > newestCurrentTime)
                    {
                        newestCurrent = candidate;
                        newestCurrentTime = written;
                    }
                }
                else if (newestFallback == null || written > newestFallbackTime)
                {
                    newestFallback = candidate;
                    newestFallbackTime = written;
                }
            }

            return newestCurrent ?? newestFallback;
        }

        /// <summary>
        /// Observation schema recorded for a run in schema_version.txt, else 1
        /// (mirrors run_schema_version in Trainer/arena_trainer.py).
        /// </summary>
        public static int RunSchemaVersion(string runDirectory)
        {
            if (string.IsNullOrEmpty(runDirectory))
            {
                return 1;
            }

            try
            {
                string path = Path.Combine(runDirectory, SchemaFileName);
                if (!File.Exists(path))
                {
                    return 1;
                }

                return int.TryParse(File.ReadAllText(path).Trim(), out int version) && version > 0
                    ? version
                    : 1;
            }
            catch (IOException)
            {
                return 1;
            }
            catch (UnauthorizedAccessException)
            {
                return 1;
            }
            catch (ArgumentException)
            {
                return 1;
            }
        }

        /// <summary>Run directory for a brain path (.../runs/&lt;run&gt;/&lt;Behavior&gt;/latest.brain).</summary>
        public static string RunDirectory(string brainPath)
        {
            if (string.IsNullOrEmpty(brainPath))
            {
                return null;
            }

            try
            {
                return new FileInfo(brainPath).Directory?.Parent?.FullName;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>
        /// Newest current-schema run with a behavior folder, measured by the newest file inside it.
        /// </summary>
        public static string FindNewestRunDirectory(string runsDirectory, string behavior)
        {
            if (string.IsNullOrEmpty(runsDirectory) || string.IsNullOrEmpty(behavior) ||
                !Directory.Exists(runsDirectory))
            {
                return null;
            }

            string[] runs;
            try
            {
                runs = Directory.GetDirectories(runsDirectory);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            string newest = null;
            DateTime newestTime = DateTime.MinValue;
            for (int i = 0; i < runs.Length; i++)
            {
                if (IsChampionsDirectory(runs[i]) || RunSchemaVersion(runs[i]) != CurrentSchemaVersion)
                {
                    continue;
                }

                string behaviorDirectory = Path.Combine(runs[i], behavior);
                if (!Directory.Exists(behaviorDirectory) ||
                    !TryNewestFileTime(behaviorDirectory, out DateTime written))
                {
                    continue;
                }

                if (newest == null || written > newestTime)
                {
                    newest = runs[i];
                    newestTime = written;
                }
            }

            return newest;
        }

        /// <summary>champions/&lt;Behavior&gt; folder of the trainer's best brains, or null without a runs folder.</summary>
        public static string ChampionDirectory(string runsDirectory, string behavior)
        {
            return string.IsNullOrEmpty(runsDirectory) || string.IsNullOrEmpty(behavior)
                ? null
                : Path.Combine(runsDirectory, ChampionsDirectoryName, behavior);
        }

        /// <summary>The current best brain for <paramref name="behavior"/> if the trainer has chosen one, else null.</summary>
        public static string FindChampionBrain(string runsDirectory, string behavior)
        {
            string directory = ChampionDirectory(runsDirectory, behavior);
            if (directory == null)
            {
                return null;
            }

            string path = Path.Combine(directory, ChampionFileName);
            return File.Exists(path) ? path : null;
        }

        private static bool IsChampionsDirectory(string runDirectory)
        {
            return string.Equals(Path.GetFileName(runDirectory), ChampionsDirectoryName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Run folder name for a brain path (…/runs/&lt;run&gt;/&lt;Behavior&gt;/latest.brain).</summary>
        public static string RunName(string brainPath)
        {
            string runDirectory = RunDirectory(brainPath);
            return string.IsNullOrEmpty(runDirectory) ? string.Empty : new DirectoryInfo(runDirectory).Name;
        }

        private static bool TryNewestFileTime(string directory, out DateTime newest)
        {
            newest = DateTime.MinValue;
            bool found = false;
            try
            {
                string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    DateTime written = File.GetLastWriteTimeUtc(files[i]);
                    if (!found || written > newest)
                    {
                        newest = written;
                        found = true;
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return found;
        }
    }
}
