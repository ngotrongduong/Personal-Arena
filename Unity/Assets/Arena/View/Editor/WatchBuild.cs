using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PersonalArena.View.Editor
{
    /// <summary>Builds the standalone "Watch AI" viewer (Build/Watch/PersonalArenaWatch.exe).</summary>
    public static class WatchBuild
    {
        private const string ExecutableName = "PersonalArenaWatch.exe";
        private const string VersionPrefix = "0.8.";
        private const string IconPath = "Assets/Arena/View/Art/AppIcon.png";

        [MenuItem("Personal Arena/Build Watch AI Viewer (Windows)")]
        public static void BuildWindows()
        {
            try
            {
                PlaySceneBuilder.BuildSurvivorScene(PlaySceneBuilder.WatchScenePath);
                BuildResult result = BuildPlayer();
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1);
                }
                else if (result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException($"Watch build ended with result {result}.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw;
            }
        }

        private static string OutputOverride()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-watchBuildOutput", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    return Path.GetFullPath(args[i + 1]);
                }
            }

            return null;
        }

        private static BuildResult BuildPlayer()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            // -watchBuildOutput <dir> builds a test copy elsewhere, e.g. while the owner has the viewer open.
            string outputDirectory = OutputOverride() ?? Path.Combine(repositoryRoot, "Build", "Watch");
            Directory.CreateDirectory(outputDirectory);

            // Project-wide settings: a resizable window that keeps playing in the background, restored afterwards.
            // M8 identity (also temporary): the app icon and version 0.8.<commit count>. productName and companyName
            // stay as they are, so Application.persistentDataPath (the owner's profile folder) never moves.
            bool previousRunInBackground = PlayerSettings.runInBackground;
            FullScreenMode previousFullScreenMode = PlayerSettings.fullScreenMode;
            int previousWidth = PlayerSettings.defaultScreenWidth;
            int previousHeight = PlayerSettings.defaultScreenHeight;
            bool previousResizable = PlayerSettings.resizableWindow;
            string previousVersion = PlayerSettings.bundleVersion;
            Texture2D[] previousDefaultIcons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            Texture2D[] previousStandaloneIcons = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Any);
            try
            {
                PlayerSettings.runInBackground = true;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 1600;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.resizableWindow = true;
                // Restricted build sessions can preserve the current version without invoking git.
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-skipGitVersion") < 0)
                {
                    PlayerSettings.bundleVersion = VersionFromCommitCount(GitCommitCount(repositoryRoot));
                }
                ApplyIcon();
                Debug.Log($"Watch build identity: {PlayerSettings.productName} v{PlayerSettings.bundleVersion} ({PlayerSettings.companyName}).");

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { PlaySceneBuilder.WatchScenePath },
                    locationPathName = Path.Combine(outputDirectory, ExecutableName),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log($"Watch build {report.summary.result}: {report.summary.outputPath} ({report.summary.totalSize} bytes).");
                return report.summary.result;
            }
            finally
            {
                PlayerSettings.runInBackground = previousRunInBackground;
                PlayerSettings.fullScreenMode = previousFullScreenMode;
                PlayerSettings.defaultScreenWidth = previousWidth;
                PlayerSettings.defaultScreenHeight = previousHeight;
                PlayerSettings.resizableWindow = previousResizable;
                PlayerSettings.bundleVersion = previousVersion;
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, previousDefaultIcons ?? Array.Empty<Texture2D>(), IconKind.Any);
                PlayerSettings.SetIcons(NamedBuildTarget.Standalone, previousStandaloneIcons ?? Array.Empty<Texture2D>(), IconKind.Any);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>The viewer's version from the output of "git rev-list --count HEAD": "0.8.N", or "0.8.0" when unknown.</summary>
        public static string VersionFromCommitCount(string gitOutput)
        {
            string text = gitOutput != null ? gitOutput.Trim() : string.Empty;
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int count) && count > 0
                ? VersionPrefix + count.ToString(CultureInfo.InvariantCulture)
                : VersionPrefix + "0";
        }

        /// <summary>"git rev-list --count HEAD" in the repository, or null when git is missing or fails.</summary>
        private static string GitCommitCount(string repositoryRoot)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo("git", "rev-list --count HEAD")
                {
                    WorkingDirectory = repositoryRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (Process git = Process.Start(start))
                {
                    if (git == null)
                    {
                        return null;
                    }

                    string output = git.StandardOutput.ReadToEnd();
                    if (!git.WaitForExit(10000) || git.ExitCode != 0)
                    {
                        return null;
                    }

                    return output;
                }
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception || exception is InvalidOperationException ||
                exception is IOException)
            {
                Debug.LogWarning("git rev-list failed (" + exception.Message + "); using version " + VersionPrefix + "0.");
                return null;
            }
        }

        /// <summary>The app icon (composed from game-icons.net CC BY 3.0 glyphs) for every Standalone icon size.</summary>
        private static void ApplyIcon()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogWarning("App icon not found at " + IconPath + "; building with the default icon.");
                return;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            Texture2D[] icons = new Texture2D[Math.Max(1, sizes != null ? sizes.Length : 0)];
            for (int i = 0; i < icons.Length; i++)
            {
                icons[i] = icon;
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Any);
        }
    }
}
