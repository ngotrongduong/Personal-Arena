using System;
using System.IO;
using PersonalArena.Core;

namespace PersonalArena.View
{
    /// <summary>Finds the newest exported training brain (Trainer/runs/&lt;run&gt;/&lt;Behavior&gt;/latest.brain).</summary>
    public static class BrainLocator
    {
        public const string LatestFileName = "latest.brain";
        public const string RulesFileName = "rules_version.txt";
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

        /// <summary>The most recently written latest.brain for <paramref name="behavior"/>, or null.</summary>
        public static string FindNewestBrain(string runsDirectory, string behavior)
        {
            return FindNewestBrain(runsDirectory, behavior, true);
        }

        /// <summary>
        /// The newest exported brain, preferring the current rules. Older brains are considered only
        /// when <paramref name="currentRulesOnly"/> is false and no current-rules brain exists.
        /// </summary>
        public static string FindNewestBrain(string runsDirectory, string behavior, bool currentRulesOnly)
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
                bool currentRules = RunRulesVersion(runs[i]) == ArenaSim.RulesVersion;
                if (currentRulesOnly && !currentRules)
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

                if (currentRules)
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

        /// <summary>Rules version recorded for a run, defaulting to the original rules.</summary>
        public static int RunRulesVersion(string runDirectory)
        {
            if (string.IsNullOrEmpty(runDirectory))
            {
                return 1;
            }

            try
            {
                string path = Path.Combine(runDirectory, RulesFileName);
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
        /// Newest current-rules run with a behavior folder, measured by the newest file inside it.
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
                if (RunRulesVersion(runs[i]) != ArenaSim.RulesVersion)
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
