using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PersonalArena.View
{
    /// <summary>What the -smokeTest run saw for one class (buy, select, one watched run, booking, next run).</summary>
    public sealed class SmokeClassResult
    {
        public string ClassId;
        public bool Selected;
        public bool RunStarted;
        public int OffersShown;
        public int Picks;
        public string EndReason;
        public long GoldAdded;
        public bool GoldBooked;
        public bool Saved;
        public bool NextRunStarted;

        public bool Passed => Problem == null;

        /// <summary>The first failed check in plain English, or null when the class passed.</summary>
        public string Problem
        {
            get
            {
                if (!Selected) return "class not bought or selected";
                if (!RunStarted) return "run did not start";
                if (OffersShown < 1) return "no level-up offer shown";
                if (Picks < 1) return "no level-up pick";
                if (string.IsNullOrEmpty(EndReason)) return "run did not end";
                if (!GoldBooked) return "gold not booked into the profile";
                if (!Saved) return "profile not saved to disk";
                if (!NextRunStarted) return "next run did not start";
                return null;
            }
        }
    }

    /// <summary>
    /// Result of the viewer's -smokeTest: every class plays one short run end to end, the panels open and close,
    /// a 2-run Auto Farm is booked, and no error is logged. Pure logic so the pass rule, the refusal rule and the
    /// JSON are tested in EditMode.
    /// </summary>
    public sealed class SmokeTestReport
    {
        public const int MaxErrors = 50;
        public const int ExpectedClassCount = 3;

        public readonly List<SmokeClassResult> Classes = new List<SmokeClassResult>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> PanelsChecked = new List<string>();
        public bool PanelsPassed;
        public bool FarmStarted;
        public bool FarmBooked;
        public bool TimedOut;
        public double Seconds;
        public string Refused;

        private int droppedErrors;

        public bool Passed
        {
            get
            {
                if (Refused != null || TimedOut || Errors.Count > 0 || Classes.Count != ExpectedClassCount)
                {
                    return false;
                }

                foreach (SmokeClassResult result in Classes)
                {
                    if (result == null || !result.Passed)
                    {
                        return false;
                    }
                }

                return PanelsPassed && FarmStarted && FarmBooked;
            }
        }

        /// <summary>Records an error (Error/Exception log line or a failed step); keeps the first <see cref="MaxErrors"/>.</summary>
        public void AddError(string message)
        {
            if (Errors.Count >= MaxErrors)
            {
                droppedErrors++;
                return;
            }

            Errors.Add(string.IsNullOrEmpty(message) ? "(empty error)" : message);
        }

        public int DroppedErrors => droppedErrors;

        /// <summary>
        /// Why the smoke test must not run, or null. It needs a scratch -profile (never the owner's real profile
        /// folder or anything inside it) and an existing -brain file.
        /// </summary>
        public static string RefusalReason(string profileArgument, string brainArgument, string realProfileDirectory)
        {
            if (string.IsNullOrWhiteSpace(profileArgument))
            {
                return "-smokeTest needs -profile <scratch folder>; it never runs on the real profile.";
            }

            if (string.IsNullOrWhiteSpace(brainArgument))
            {
                return "-smokeTest needs -brain <file>.";
            }

            if (!File.Exists(brainArgument))
            {
                return "-smokeTest brain file not found: " + brainArgument;
            }

            if (!string.IsNullOrWhiteSpace(realProfileDirectory) && IsSameOrInside(profileArgument, realProfileDirectory))
            {
                return "-smokeTest refuses the real profile folder: " + profileArgument;
            }

            return null;
        }

        /// <summary>True when <paramref name="path"/> is <paramref name="directory"/> or lies inside it (case-insensitive).</summary>
        public static bool IsSameOrInside(string path, string directory)
        {
            string full = Normalize(path);
            string root = Normalize(directory);
            if (full == null || root == null)
            {
                return false;
            }

            return string.Equals(full, root, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(path.Trim()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                    .TrimEnd(Path.DirectorySeparatorChar);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The report file: { "passed", "classes", "panels", "farm", "errors", "seconds", ... }.</summary>
        public string ToJson()
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            StringBuilder json = new StringBuilder(1024);
            json.Append("{\n");
            json.Append("  \"passed\": ").Append(Passed ? "true" : "false").Append(",\n");
            json.Append("  \"classes\": [");
            for (int i = 0; i < Classes.Count; i++)
            {
                SmokeClassResult c = Classes[i];
                json.Append(i == 0 ? "\n" : ",\n");
                json.Append("    { \"class\": ").Append(Quote(c.ClassId))
                    .Append(", \"passed\": ").Append(c.Passed ? "true" : "false")
                    .Append(", \"selected\": ").Append(c.Selected ? "true" : "false")
                    .Append(", \"runStarted\": ").Append(c.RunStarted ? "true" : "false")
                    .Append(", \"offersShown\": ").Append(c.OffersShown.ToString(culture))
                    .Append(", \"picks\": ").Append(c.Picks.ToString(culture))
                    .Append(", \"endReason\": ").Append(Quote(c.EndReason))
                    .Append(", \"goldAdded\": ").Append(c.GoldAdded.ToString(culture))
                    .Append(", \"goldBooked\": ").Append(c.GoldBooked ? "true" : "false")
                    .Append(", \"saved\": ").Append(c.Saved ? "true" : "false")
                    .Append(", \"nextRunStarted\": ").Append(c.NextRunStarted ? "true" : "false")
                    .Append(", \"problem\": ").Append(Quote(c.Problem))
                    .Append(" }");
            }
            json.Append(Classes.Count > 0 ? "\n  ],\n" : "],\n");

            json.Append("  \"panels\": { \"passed\": ").Append(PanelsPassed ? "true" : "false").Append(", \"checked\": [");
            for (int i = 0; i < PanelsChecked.Count; i++)
            {
                json.Append(i == 0 ? string.Empty : ", ").Append(Quote(PanelsChecked[i]));
            }
            json.Append("] },\n");
            json.Append("  \"farm\": { \"started\": ").Append(FarmStarted ? "true" : "false")
                .Append(", \"booked\": ").Append(FarmBooked ? "true" : "false").Append(" },\n");
            json.Append("  \"errors\": [");
            for (int i = 0; i < Errors.Count; i++)
            {
                json.Append(i == 0 ? "\n    " : ",\n    ").Append(Quote(Errors[i]));
            }
            json.Append(Errors.Count > 0 ? "\n  ],\n" : "],\n");
            json.Append("  \"droppedErrors\": ").Append(droppedErrors.ToString(culture)).Append(",\n");
            json.Append("  \"timedOut\": ").Append(TimedOut ? "true" : "false").Append(",\n");
            json.Append("  \"refused\": ").Append(Quote(Refused)).Append(",\n");
            json.Append("  \"seconds\": ").Append(Math.Round(Seconds, 1).ToString("0.0", culture)).Append("\n");
            json.Append("}\n");
            return json.ToString();
        }

        /// <summary>A JSON string literal (null becomes null).</summary>
        public static string Quote(string value)
        {
            if (value == null)
            {
                return "null";
            }

            StringBuilder quoted = new StringBuilder(value.Length + 2);
            quoted.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': quoted.Append("\\\""); break;
                    case '\\': quoted.Append("\\\\"); break;
                    case '\n': quoted.Append("\\n"); break;
                    case '\r': quoted.Append("\\r"); break;
                    case '\t': quoted.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            quoted.Append(c);
                        }
                        break;
                }
            }
            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
