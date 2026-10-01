using System;
using System.Collections.Generic;

namespace PersonalArena.View
{
    /// <summary>
    /// Voice limiter of the viewer's sound effects. A cue may start only when its own minimum interval has passed,
    /// fewer than its <see cref="SoundCueInfo.MaxVoices"/> copies are still sounding, and the whole mix is below
    /// <see cref="GlobalVoices"/> minus the voices kept for important cues. Important cues are never refused.
    /// Each accepted voice counts as sounding for the cue's <see cref="SoundCueInfo.HoldSeconds"/>.
    /// Time is real seconds from the caller (so a horde at view speed x8 is thinned to the same real rate as at x1).
    /// </summary>
    public sealed class SoundThrottle
    {
        public const int DefaultGlobalVoices = 24;
        public const int ReservedForImportant = 4;

        private readonly float[] lastStart;
        private readonly List<float>[] cueVoices;
        private readonly List<float> allVoices = new List<float>();
        private float prunedAt = float.NaN;

        public SoundThrottle(int globalVoices = DefaultGlobalVoices)
        {
            if (globalVoices <= ReservedForImportant)
            {
                throw new ArgumentOutOfRangeException(nameof(globalVoices));
            }

            GlobalVoices = globalVoices;
            lastStart = new float[SoundCueInfo.CueCount];
            cueVoices = new List<float>[SoundCueInfo.CueCount];
            for (int i = 0; i < cueVoices.Length; i++)
            {
                cueVoices[i] = new List<float>(4);
            }
            Reset();
        }

        /// <summary>Most voices the mix may hold; ordinary cues stop at <see cref="GlobalVoices"/> − <see cref="ReservedForImportant"/>.</summary>
        public int GlobalVoices { get; }

        /// <summary>
        /// True (and the voice is counted) when <paramref name="cue"/> may start at <paramref name="now"/>.
        /// <see cref="SoundCue.None"/> never plays; important cues always do.
        /// </summary>
        public bool TryStart(SoundCue cue, float now)
        {
            int index = (int)cue;
            if (cue == SoundCue.None || index < 0 || index >= lastStart.Length || float.IsNaN(now))
            {
                return false;
            }

            SoundCueInfo info = SoundCueInfo.For(cue);
            if (!info.Important && now - lastStart[index] < info.MinInterval && now >= lastStart[index])
            {
                // The cheap check first: most requests of a horde stop here.
                return false;
            }

            Prune(now);
            if (!info.Important)
            {
                if (cueVoices[index].Count >= info.MaxVoices)
                {
                    return false;
                }
                if (allVoices.Count >= GlobalVoices - ReservedForImportant)
                {
                    return false;
                }
            }

            lastStart[index] = now;
            float end = now + Math.Max(0f, info.HoldSeconds);
            cueVoices[index].Add(end);
            allVoices.Add(end);
            return true;
        }

        /// <summary>Voices still sounding at <paramref name="now"/>.</summary>
        public int ActiveVoices(float now)
        {
            Prune(now);
            return allVoices.Count;
        }

        /// <summary>Copies of <paramref name="cue"/> still sounding at <paramref name="now"/>.</summary>
        public int ActiveVoices(SoundCue cue, float now)
        {
            Prune(now);
            int index = (int)cue;
            return index >= 0 && index < cueVoices.Length ? cueVoices[index].Count : 0;
        }

        /// <summary>Forgets every voice and interval (new run, or time jumped).</summary>
        public void Reset()
        {
            for (int i = 0; i < lastStart.Length; i++)
            {
                lastStart[i] = float.NegativeInfinity;
                cueVoices[i].Clear();
            }
            allVoices.Clear();
            prunedAt = float.NaN;
        }

        private void Prune(float now)
        {
            if (now == prunedAt)
            {
                return;
            }

            prunedAt = now;
            RemoveEnded(allVoices, now);
            for (int i = 0; i < cueVoices.Length; i++)
            {
                RemoveEnded(cueVoices[i], now);
            }
        }

        /// <summary>Drops voices that ended by <paramref name="now"/>, without allocating.</summary>
        private static void RemoveEnded(List<float> voices, float now)
        {
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                if (voices[i] <= now)
                {
                    voices.RemoveAt(i);
                }
            }
        }
    }
}
