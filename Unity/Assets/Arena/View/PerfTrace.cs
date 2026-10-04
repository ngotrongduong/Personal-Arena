using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// -perfLog start-up timing: named marks (real milliseconds since launch) and spans (how long a step took).
    /// The watch controller writes them next to the perf log as *_startup.csv. Without -perfLog every call
    /// returns at once.
    /// </summary>
    public static class PerfTrace
    {
        private static readonly StringBuilder Lines = new StringBuilder(2048);
        private static int state;

        public static bool Enabled
        {
            get
            {
                if (state == 0)
                {
                    state = 2;
                    foreach (string argument in Environment.GetCommandLineArgs())
                    {
                        if (string.Equals(argument, "-perfLog", StringComparison.OrdinalIgnoreCase))
                        {
                            state = 1;
                        }
                    }
                }
                return state == 1;
            }
        }

        /// <summary>Real milliseconds since launch; pass it to <see cref="Span"/> when the step ends.</summary>
        public static double Now => Time.realtimeSinceStartupAsDouble * 1000.0;

        public static void Mark(string label)
        {
            if (Enabled)
            {
                Append(label, Now, -1.0);
            }
        }

        /// <summary>Records a step that began at <paramref name="startedAt"/> when it took at least <paramref name="minMs"/>.</summary>
        public static void Span(string label, double startedAt, double minMs = 0.0)
        {
            if (!Enabled)
            {
                return;
            }
            double now = Now;
            if (now - startedAt >= minMs)
            {
                Append(label, now, now - startedAt);
            }
        }

        /// <summary>Everything recorded since the last call (label,atMs,tookMs lines), then forgets it.</summary>
        public static string Drain()
        {
            string text = Lines.ToString();
            Lines.Length = 0;
            return text;
        }

        private static void Append(string label, double at, double took)
        {
            Lines.Append(label).Append(',').Append(at.ToString("0", CultureInfo.InvariantCulture)).Append(',');
            if (took >= 0.0)
            {
                Lines.Append(took.ToString("0.0", CultureInfo.InvariantCulture));
            }
            Lines.Append('\n');
        }
    }
}
