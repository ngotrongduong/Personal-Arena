using System;
using System.IO;
using UnityEngine;

namespace PersonalArena.View
{
    public enum TrainingState
    {
        /// <summary>Python, the trainer script, or the training build is missing.</summary>
        Unavailable,
        Idle,
        Starting,
        Training,
        Stopping,
        Stopped,
        Error,
        /// <summary>Training that was not started by this viewer (e.g. from a terminal).</summary>
        External
    }

    /// <summary>Progress written by Trainer/train_service.py to Trainer/runs/training_service.json.</summary>
    [Serializable]
    public sealed class TrainingStatus
    {
        public string state;
        public string message;
        public string run_id;
        public string behavior;
        public int pid;
        public int trainer_pid;
        public long step;
        public long max_steps;
        public bool has_reward;
        public float mean_reward;
        public float zombies;
        public ZombieMix zombie_mix;
        public float session_seconds;
        public int num_envs;
        public int arena_agents;
        public bool cpu;
        public double updated_unix;
        public long[] steps;
        public float[] rewards;
        // M5: what the service passed to Unity (older status files leave these empty / false / 0).
        public string training_focus;
        public bool owner_build;
        public int owner_tier;

        public static TrainingStatus Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                TrainingStatus status = JsonUtility.FromJson<TrainingStatus>(json);
                return status != null && !string.IsNullOrEmpty(status.state) ? status : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    /// <summary>Curriculum spawn weights the trainer is currently using (walker is always 1).</summary>
    [Serializable]
    public sealed class ZombieMix
    {
        public float walker;
        public float runner;
        public float brute;
        public float spitter;

        /// <summary>Short label such as "walkers" or "walker+runner+brute".</summary>
        public string Describe()
        {
            string text = "walker";
            if (runner > 0f)
            {
                text += "+runner";
            }
            if (brute > 0f)
            {
                text += "+brute";
            }
            if (spitter > 0f)
            {
                text += "+spitter";
            }
            return text == "walker" ? "walkers only" : text;
        }
    }

    /// <summary>How hard training runs: hidden Unity games, fighters per game and simulation speed.</summary>
    public readonly struct TrainingPower
    {
        public readonly string Name;
        public readonly int Games;
        public readonly int FightersPerGame;
        public readonly float TimeScale;

        public TrainingPower(string name, int games, int fightersPerGame, float timeScale)
        {
            Name = name;
            Games = games;
            FightersPerGame = fightersPerGame;
            TimeScale = timeScale;
        }

        public int Fighters => Games * FightersPerGame;

        public string Arguments()
        {
            return " --num-envs " + Games + " --arena-agents " + FightersPerGame + " --time-scale " +
                TimeScale.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The owner's choices for a TRAIN session (M5): stat points and tier, and the reward focus.</summary>
    public sealed class OwnerTraining
    {
        /// <summary>16 stat point counts, or null to train on random builds only.</summary>
        public int[] Points;
        public int Tier = 1;
        /// <summary>Training Focus id ("balanced", "gold", ...), or null to leave the default.</summary>
        public string FocusId;

        /// <summary>Command-line arguments for Trainer/train_service.py (leading space, or empty).</summary>
        public static string Arguments(OwnerTraining owner)
        {
            if (owner == null)
            {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            if (owner.Points != null)
            {
                builder.Append(" --owner-build ").Append(string.Join(",", owner.Points))
                    .Append(" --owner-tier ").Append(Math.Max(1, Math.Min(10, owner.Tier)));
            }
            if (!string.IsNullOrWhiteSpace(owner.FocusId))
            {
                builder.Append(" --training-focus ").Append(owner.FocusId.Trim().ToLowerInvariant());
            }
            return builder.ToString();
        }
    }

    public readonly struct TrainingSnapshot
    {
        public readonly TrainingState State;
        public readonly TrainingStatus Status;

        public TrainingSnapshot(TrainingState state, TrainingStatus status)
        {
            State = state;
            Status = status;
        }

        public bool IsActive => State == TrainingState.Starting || State == TrainingState.Training ||
            State == TrainingState.Stopping;
    }

    /// <summary>
    /// Starts and stops Trainer/train_service.py for the viewer's "Train the AI" button and reads its
    /// status file. The service runs ML-Agents in a hidden console and saves a checkpoint on stop.
    /// </summary>
    public sealed class TrainingServiceClient
    {
        public const string StatusFileName = "training_service.json";
        public const string StopFileName = "training_service.stop";
        public const string ErrorLogFileName = "training_service.err.log";
        public const double FreshSeconds = 15.0;
        public const double LaunchGraceSeconds = 30.0;
        public const double ExternalLogSeconds = 120.0;

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private System.Diagnostics.Process process;
        private DateTime launchedUtc = DateTime.MinValue;
        private string launchError;
        private double launchErrorUnix;

        public TrainingServiceClient(string runsDirectory)
        {
            RunsDirectory = Path.GetFullPath(runsDirectory);
            RepositoryRoot = Path.GetFullPath(Path.Combine(RunsDirectory, "..", ".."));
            PythonPath = Path.Combine(RepositoryRoot, ".venv-ml", "Scripts", "python.exe");
            ScriptPath = Path.Combine(RepositoryRoot, "Trainer", "train_service.py");
            TrainingBuildPath = Path.Combine(RepositoryRoot, "Build", "Training", "PersonalArenaTraining.exe");
        }

        public string RunsDirectory { get; }
        public string RepositoryRoot { get; }
        public string PythonPath { get; }
        public string ScriptPath { get; }
        public string TrainingBuildPath { get; }
        public string StatusPath => Path.Combine(RunsDirectory, StatusFileName);
        public string StopPath => Path.Combine(RunsDirectory, StopFileName);

        /// <summary>Null when training can be started, otherwise what is missing.</summary>
        public string MissingPiece()
        {
            if (!File.Exists(PythonPath))
            {
                return "Python for training (.venv-ml) is not installed.";
            }
            if (!File.Exists(ScriptPath))
            {
                return "Trainer/train_service.py is missing.";
            }
            if (!File.Exists(TrainingBuildPath))
            {
                return "The training build (Build/Training) is missing.";
            }
            return null;
        }

        /// <summary>Launches the service in a hidden console; returns an error message or null.</summary>
        public string Start(int parentProcessId, TrainingPower power, string behavior = "Warrior", OwnerTraining owner = null)
        {
            string missing = MissingPiece();
            if (missing != null)
            {
                return missing;
            }

            try
            {
                if (File.Exists(StopPath))
                {
                    File.Delete(StopPath);
                }

                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo(
                    PythonPath, Quote(ScriptPath) + " --parent-pid " + parentProcessId + power.Arguments() +
                    " --behavior " + (string.IsNullOrEmpty(behavior) ? "Warrior" : behavior) + OwnerTraining.Arguments(owner))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = RepositoryRoot
                };
                info.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
                process = System.Diagnostics.Process.Start(info);
                launchedUtc = DateTime.UtcNow;
                launchError = null;
                return null;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidOperationException ||
                exception is System.ComponentModel.Win32Exception || exception is UnauthorizedAccessException)
            {
                return "Could not start training: " + exception.Message;
            }
        }

        /// <summary>Asks the service to save a checkpoint and quit.</summary>
        public void RequestStop()
        {
            try
            {
                File.WriteAllText(StopPath, DateTime.Now.ToString("O"));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public TrainingSnapshot Read()
        {
            DateTime utcNow = DateTime.UtcNow;
            TrainingStatus status = TrainingStatus.Parse(ReadShared(StatusPath));
            bool launching = (utcNow - launchedUtc).TotalSeconds < LaunchGraceSeconds;
            if (process != null && HasExited(process, out int exitCode))
            {
                string failure = LaunchFailure(exitCode, status, launchedUtc);
                if (failure != null)
                {
                    launchError = failure;
                    launchErrorUnix = ToUnix(utcNow);
                }
                process = null;
                launchedUtc = DateTime.MinValue;
                launching = false;
            }
            if (launchError != null && status != null && status.updated_unix > launchErrorUnix)
            {
                launchError = null; // A newer status (e.g. from another start) replaces it.
            }

            TrainingState state = Classify(status, ToUnix(utcNow), NewestLogWriteUtc(), utcNow,
                launching ? launchedUtc : (DateTime?)null);
            if (launchError != null && (state == TrainingState.Idle || state == TrainingState.Stopped ||
                state == TrainingState.Error))
            {
                status = new TrainingStatus { state = "error", message = launchError };
                state = TrainingState.Error;
            }
            else if (state == TrainingState.Idle && MissingPiece() != null)
            {
                state = TrainingState.Unavailable;
            }

            return new TrainingSnapshot(state, status);
        }

        /// <summary>
        /// Why a launched service exited, or null when it wrote its own status for this launch (a newer
        /// "stopped" or "error" already explains itself). An older status must not hide the failure.
        /// </summary>
        public static string LaunchFailure(int exitCode, TrainingStatus status, DateTime launchedUtc)
        {
            if (status != null && status.updated_unix >= ToUnix(launchedUtc))
            {
                return null;
            }
            return exitCode == 3
                ? "Training is already running in another window."
                : "The training service closed right away (code " + exitCode + "). See Trainer/runs/" +
                  ErrorLogFileName + ".";
        }

        /// <summary>Decides what the button shows from the status file and the run logs.</summary>
        public static TrainingState Classify(TrainingStatus status, double nowUnix, DateTime? newestLogUtc,
            DateTime utcNow, DateTime? launchedUtc)
        {
            bool fresh = status != null && nowUnix - status.updated_unix <= FreshSeconds;
            if (fresh)
            {
                switch (status.state)
                {
                    case "starting": return TrainingState.Starting;
                    case "training": return TrainingState.Training;
                    case "stopping": return TrainingState.Stopping;
                }
            }

            if (launchedUtc.HasValue && (status == null || status.updated_unix < ToUnix(launchedUtc.Value)))
            {
                return TrainingState.Starting;
            }

            if (newestLogUtc.HasValue && (utcNow - newestLogUtc.Value).TotalSeconds <= ExternalLogSeconds &&
                (status == null || ToUnix(newestLogUtc.Value) > status.updated_unix + 10.0))
            {
                return TrainingState.External;
            }

            if (status != null && status.state == "stopped")
            {
                return TrainingState.Stopped;
            }
            if (status != null && status.state == "error")
            {
                return TrainingState.Error;
            }
            return TrainingState.Idle;
        }

        public static double ToUnix(DateTime utc)
        {
            return (utc - UnixEpoch).TotalSeconds;
        }

        private DateTime? NewestLogWriteUtc()
        {
            try
            {
                DateTime? newest = null;
                foreach (string path in Directory.GetFiles(RunsDirectory, "*.log"))
                {
                    DateTime written = File.GetLastWriteTimeUtc(path);
                    if (!newest.HasValue || written > newest.Value)
                    {
                        newest = written;
                    }
                }
                return newest;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string ReadShared(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static bool HasExited(System.Diagnostics.Process target, out int exitCode)
        {
            exitCode = 0;
            try
            {
                if (!target.HasExited)
                {
                    return false;
                }
                exitCode = target.ExitCode;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }
    }
}
