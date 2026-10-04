using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>The JSON line Trainer/brain_lineage.py prints last: {"ok": true, ...} or {"ok": false, "error": "..."}.</summary>
    [Serializable]
    public sealed class LineageResult
    {
        public bool ok;
        public string error;
        public string warning;
        public string run_id;
        public string id;
    }

    /// <summary>
    /// Runs Trainer/brain_lineage.py (sync, snapshot, fork) in a hidden process, one command at a time, without
    /// blocking the game. Call <see cref="Poll"/> from the main thread every frame; it reports a finished command
    /// once. Paths are the same as <see cref="TrainingServiceClient"/> (repo = runs/../..).
    /// </summary>
    public sealed class LineageCommand
    {
        public const string ScriptFileName = "brain_lineage.py";
        public const double DefaultTimeoutSeconds = 120.0;
        public const double EvaluateTimeoutSeconds = 15.0 * 60.0;
        private const int MaximumCapturedChars = 64 * 1024;

        private readonly object gate = new object();
        private readonly StringBuilder output = new StringBuilder();
        private readonly StringBuilder errors = new StringBuilder();
        private System.Diagnostics.Process process;
        private DateTime startedUtc;
        private double timeoutSeconds;

        public LineageCommand(string runsDirectory, string behavior)
        {
            RunsDirectory = Path.GetFullPath(runsDirectory);
            RepositoryRoot = Path.GetFullPath(Path.Combine(RunsDirectory, "..", ".."));
            PythonPath = Path.Combine(RepositoryRoot, ".venv-ml", "Scripts", "python.exe");
            ScriptPath = Path.Combine(RepositoryRoot, "Trainer", ScriptFileName);
            Behavior = string.IsNullOrEmpty(behavior) ? "Warrior" : behavior;
        }

        public string RunsDirectory { get; }
        public string RepositoryRoot { get; }
        public string PythonPath { get; }
        public string ScriptPath { get; }
        public string Behavior { get; }

        /// <summary>A command is running.</summary>
        public bool IsRunning => process != null;

        /// <summary>What is running ("sync", "snapshot", "fork"), or null.</summary>
        public string RunningKind { get; private set; }

        /// <summary>Caller data of the running command (e.g. the name to give a new branch).</summary>
        public object RunningTag { get; private set; }

        /// <summary>Text for the panel while the command runs.</summary>
        public string BusyText { get; private set; }

        public double ElapsedSeconds => IsRunning ? (DateTime.UtcNow - startedUtc).TotalSeconds : 0.0;

        /// <summary>Null when the command can run, otherwise what is missing.</summary>
        public string MissingPiece()
        {
            if (!File.Exists(PythonPath))
            {
                return "Python for training is not installed (.venv-ml).";
            }
            if (!File.Exists(ScriptPath))
            {
                return "Missing Trainer/" + ScriptFileName + ".";
            }
            return null;
        }

        /// <summary>The command line after the python executable.</summary>
        public static string Arguments(string scriptPath, string runsDirectory, string behavior, string commandArguments)
        {
            return "\"" + scriptPath + "\" --results-dir \"" + runsDirectory + "\" --behavior " +
                (string.IsNullOrEmpty(behavior) ? "Warrior" : behavior) + " " + (commandArguments ?? string.Empty).Trim();
        }

        /// <summary>
        /// Starts a command (e.g. "sync", "snapshot --run-id warrior-s001", "fork --version ID"). Returns an error
        /// or null. Only one command runs at a time.
        /// </summary>
        public string Start(string kind, string commandArguments, string busyText, double timeout, object tag = null)
        {
            if (IsRunning)
            {
                return "Another command is running, please wait.";
            }

            string missing = MissingPiece();
            if (missing != null)
            {
                return missing;
            }

            try
            {
                lock (gate)
                {
                    output.Length = 0;
                    errors.Length = 0;
                }

                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo(
                    PythonPath, Arguments(ScriptPath, RunsDirectory, Behavior, commandArguments))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = RepositoryRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                info.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
                info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                System.Diagnostics.Process started = new System.Diagnostics.Process { StartInfo = info };
                started.OutputDataReceived += (sender, data) => Append(output, data.Data);
                started.ErrorDataReceived += (sender, data) => Append(errors, data.Data);
                if (!started.Start())
                {
                    started.Dispose();
                    return "Could not run the brain history tool.";
                }

                started.BeginOutputReadLine();
                started.BeginErrorReadLine();
                process = started;
                startedUtc = DateTime.UtcNow;
                timeoutSeconds = timeout > 0.0 ? timeout : DefaultTimeoutSeconds;
                RunningKind = kind;
                RunningTag = tag;
                BusyText = busyText;
                return null;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidOperationException ||
                exception is System.ComponentModel.Win32Exception || exception is UnauthorizedAccessException)
            {
                return "Could not run the brain history tool: " + exception.Message;
            }
        }

        /// <summary>
        /// Main thread, every frame: true once when the running command has finished (or timed out), with its
        /// result, kind and tag. False while it runs or when nothing runs.
        /// </summary>
        public bool Poll(out LineageResult result, out string kind, out object tag)
        {
            result = null;
            kind = null;
            tag = null;
            if (process == null)
            {
                return false;
            }

            bool exited;
            int exitCode = -1;
            try
            {
                exited = process.HasExited;
            }
            catch (InvalidOperationException)
            {
                exited = true;
            }

            if (!exited)
            {
                if ((DateTime.UtcNow - startedUtc).TotalSeconds <= timeoutSeconds)
                {
                    return false;
                }

                TryKill(process);
                result = new LineageResult
                {
                    ok = false,
                    error = "The command took too long and was stopped (" + Math.Round(timeoutSeconds / 60.0) + " min)."
                };
            }
            else
            {
                try
                {
                    // Waits for the asynchronous output readers to deliver the last lines.
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is SystemException)
                {
                    exitCode = -1;
                }

                string stdout;
                string stderr;
                lock (gate)
                {
                    stdout = output.ToString();
                    stderr = errors.ToString();
                }

                result = ParseResult(stdout, exitCode);
                if (!result.ok && !string.IsNullOrWhiteSpace(stderr))
                {
                    Debug.LogWarning("brain_lineage.py " + RunningKind + " failed:\n" + stderr);
                }
            }

            kind = RunningKind;
            tag = RunningTag;
            try
            {
                process.Dispose();
            }
            catch (Exception)
            {
                // Nothing left to release.
            }

            process = null;
            RunningKind = null;
            RunningTag = null;
            BusyText = null;
            return true;
        }

        /// <summary>
        /// The result from the command's stdout: the last non-empty line that is a JSON object with "ok"; when there
        /// is none, an error that mentions the exit code.
        /// </summary>
        public static LineageResult ParseResult(string stdout, int exitCode)
        {
            if (!string.IsNullOrEmpty(stdout))
            {
                string[] lines = stdout.Replace("\r", string.Empty).Split('\n');
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    if (!line.StartsWith("{", StringComparison.Ordinal) || line.IndexOf("\"ok\"", StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    LineageResult parsed;
                    try
                    {
                        parsed = JsonUtility.FromJson<LineageResult>(line);
                    }
                    catch (Exception)
                    {
                        parsed = null;
                    }

                    if (parsed == null)
                    {
                        continue;
                    }

                    if (!parsed.ok && string.IsNullOrWhiteSpace(parsed.error))
                    {
                        parsed.error = "The brain history tool failed (code " + exitCode + ").";
                    }

                    return parsed;
                }
            }

            return new LineageResult
            {
                ok = false,
                error = "The brain history tool gave a bad answer (code " + exitCode + ")."
            };
        }

        private void Append(StringBuilder target, string line)
        {
            if (line == null)
            {
                return;
            }

            lock (gate)
            {
                if (target.Length > MaximumCapturedChars)
                {
                    target.Remove(0, target.Length - MaximumCapturedChars / 2);
                }

                target.Append(line).Append('\n');
            }
        }

        private static void TryKill(System.Diagnostics.Process target)
        {
            try
            {
                if (target.HasExited)
                {
                    return;
                }

                // A snapshot runs the evaluator as a child of python: stop the whole tree (Windows).
                if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                {
                    System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = System.IO.Path.Combine(Environment.SystemDirectory, "taskkill.exe"),
                        Arguments = "/T /F /PID " + target.Id,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    using (System.Diagnostics.Process killer = System.Diagnostics.Process.Start(info))
                    {
                        killer?.WaitForExit(5000);
                    }
                }

                if (!target.HasExited)
                {
                    target.Kill();
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException ||
                exception is System.ComponentModel.Win32Exception || exception is NotSupportedException)
            {
                // Already gone.
            }
        }
    }
}
