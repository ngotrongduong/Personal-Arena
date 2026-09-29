using System;
using System.IO;

namespace PersonalArena.View
{
    /// <summary>Finds the newest exported training brain (Trainer/runs/&lt;run&gt;/&lt;Behavior&gt;/latest.brain).</summary>
    public static class BrainLocator
    {
        public const string LatestFileName = "latest.brain";
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
            if (string.IsNullOrEmpty(runsDirectory) || !Directory.Exists(runsDirectory))
            {
                return null;
            }

            string newest = null;
            DateTime newestTime = DateTime.MinValue;
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
                string candidate = Path.Combine(runs[i], behavior, LatestFileName);
                if (!File.Exists(candidate))
                {
                    continue;
                }

                DateTime written = File.GetLastWriteTimeUtc(candidate);
                if (newest == null || written > newestTime)
                {
                    newest = candidate;
                    newestTime = written;
                }
            }

            return newest;
        }

        /// <summary>Run folder name for a brain path (…/runs/&lt;run&gt;/&lt;Behavior&gt;/latest.brain).</summary>
        public static string RunName(string brainPath)
        {
            if (string.IsNullOrEmpty(brainPath))
            {
                return string.Empty;
            }

            DirectoryInfo behaviorDirectory = new FileInfo(brainPath).Directory;
            return behaviorDirectory?.Parent?.Name ?? string.Empty;
        }
    }
}
