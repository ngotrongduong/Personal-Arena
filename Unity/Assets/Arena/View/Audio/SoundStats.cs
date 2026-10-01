using System;
using System.Globalization;
using System.Text;

namespace PersonalArena.View
{
    /// <summary>
    /// How many times each cue was asked for and actually played. Written by <c>-audioLog</c> so the wiring can be
    /// checked in a muted automated run.
    /// </summary>
    public sealed class SoundStats
    {
        private readonly int[] requested = new int[SoundCueInfo.CueCount];
        private readonly int[] played = new int[SoundCueInfo.CueCount];

        public int MissingClipCues { get; private set; }

        public int Requested(SoundCue cue) => InRange(cue) ? requested[(int)cue] : 0;
        public int Played(SoundCue cue) => InRange(cue) ? played[(int)cue] : 0;

        public void CountRequested(SoundCue cue)
        {
            if (InRange(cue))
            {
                requested[(int)cue]++;
            }
        }

        public void CountPlayed(SoundCue cue)
        {
            if (InRange(cue))
            {
                played[(int)cue]++;
            }
        }

        /// <summary>Called once per cue that has no clip in the sound set.</summary>
        public void CountMissingClip()
        {
            MissingClipCues++;
        }

        /// <summary>
        /// Text report: a header, then one "cue requested played" line per cue that was asked for at least once,
        /// then the totals.
        /// </summary>
        public string Format(string header)
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            StringBuilder text = new StringBuilder();
            text.AppendLine("# Personal Arena audio log");
            if (!string.IsNullOrEmpty(header))
            {
                text.AppendLine(header);
            }

            text.AppendLine("cue requested played");
            long totalRequested = 0;
            long totalPlayed = 0;
            foreach (SoundCue cue in (SoundCue[])Enum.GetValues(typeof(SoundCue)))
            {
                int index = (int)cue;
                if (cue == SoundCue.None || requested[index] == 0)
                {
                    continue;
                }

                totalRequested += requested[index];
                totalPlayed += played[index];
                text.Append(cue.ToString()).Append(' ')
                    .Append(requested[index].ToString(culture)).Append(' ')
                    .Append(played[index].ToString(culture)).AppendLine();
            }

            text.Append("total ").Append(totalRequested.ToString(culture)).Append(' ')
                .Append(totalPlayed.ToString(culture)).AppendLine();
            text.Append("missingClipCues ").Append(MissingClipCues.ToString(culture)).AppendLine();
            return text.ToString();
        }

        private bool InRange(SoundCue cue)
        {
            int index = (int)cue;
            return index >= 0 && index < requested.Length;
        }
    }
}
