using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PersonalArena.ML.Editor
{
    public static class TrainingBuild
    {
        private const string ExecutableName = "PersonalArenaTraining.exe";

        [MenuItem("Personal Arena/Build Training Env (Windows)")]
        public static void BuildWindows()
        {
            try
            {
                BuildResult result = BuildWindowsPlayer();
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1);
                }
                else if (result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException($"Training build ended with result {result}.");
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

        private static BuildResult BuildWindowsPlayer()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TrainingSceneBuilder.TrainingScenePath) == null)
            {
                throw new FileNotFoundException(
                    "Build the training scene before building the environment.",
                    TrainingSceneBuilder.TrainingScenePath);
            }

            string outputDirectory = ResolveBuildDirectory();
            Directory.CreateDirectory(outputDirectory);

            // The training player needs a small background window, but these are project-wide
            // settings: restore them afterwards so the game build keeps its own defaults.
            bool previousRunInBackground = PlayerSettings.runInBackground;
            FullScreenMode previousFullScreenMode = PlayerSettings.fullScreenMode;
            int previousWidth = PlayerSettings.defaultScreenWidth;
            int previousHeight = PlayerSettings.defaultScreenHeight;
            try
            {
                PlayerSettings.runInBackground = true;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 640;
                PlayerSettings.defaultScreenHeight = 360;

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { TrainingSceneBuilder.TrainingScenePath },
                    locationPathName = Path.Combine(outputDirectory, ExecutableName),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log(
                    $"Training build {report.summary.result}: {report.summary.outputPath} " +
                    $"({report.summary.totalSize} bytes)." );
                return report.summary.result;
            }
            finally
            {
                PlayerSettings.runInBackground = previousRunInBackground;
                PlayerSettings.fullScreenMode = previousFullScreenMode;
                PlayerSettings.defaultScreenWidth = previousWidth;
                PlayerSettings.defaultScreenHeight = previousHeight;
                AssetDatabase.SaveAssets();
            }
        }

        private static string ResolveBuildDirectory()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string requested = CommandLineValue("-buildPath");
            if (string.IsNullOrWhiteSpace(requested))
            {
                return Path.Combine(repositoryRoot, "Build", "Training");
            }

            return Path.GetFullPath(Path.IsPathRooted(requested)
                ? requested
                : Path.Combine(repositoryRoot, requested));
        }

        private static string CommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
