using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PersonalArena.View.Editor
{
    /// <summary>Builds the standalone "Watch AI" viewer (Build/Watch/PersonalArenaWatch.exe).</summary>
    public static class WatchBuild
    {
        private const string ExecutableName = "PersonalArenaWatch.exe";

        [MenuItem("Personal Arena/Build Watch AI Viewer (Windows)")]
        public static void BuildWindows()
        {
            try
            {
                PlaySceneBuilder.BuildScene(PlaySceneBuilder.WatchScenePath, typeof(AiArenaController));
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

        private static BuildResult BuildPlayer()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string outputDirectory = Path.Combine(repositoryRoot, "Build", "Watch");
            Directory.CreateDirectory(outputDirectory);

            // Project-wide settings: a resizable window that keeps playing in the background, restored afterwards.
            bool previousRunInBackground = PlayerSettings.runInBackground;
            FullScreenMode previousFullScreenMode = PlayerSettings.fullScreenMode;
            int previousWidth = PlayerSettings.defaultScreenWidth;
            int previousHeight = PlayerSettings.defaultScreenHeight;
            bool previousResizable = PlayerSettings.resizableWindow;
            try
            {
                PlayerSettings.runInBackground = true;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 1600;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.resizableWindow = true;

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
                AssetDatabase.SaveAssets();
            }
        }
    }
}
