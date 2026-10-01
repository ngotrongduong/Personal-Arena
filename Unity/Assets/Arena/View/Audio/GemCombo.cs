using System;

namespace PersonalArena.View
{
    /// <summary>
    /// Pitch of the gem pickup blip: each gem that plays soon after the previous one climbs one note of a major
    /// pentatonic scale (up to <see cref="TopSemitones"/>), and a pause longer than <see cref="ResetSeconds"/>
    /// starts again from the bottom. Time is real seconds from the caller.
    /// </summary>
    public sealed class GemCombo
    {
        public const float ResetSeconds = 0.7f;

        private static readonly int[] Scale = { 0, 2, 4, 7, 9, 12, 14 };

        private int step;
        private float lastTime = float.NegativeInfinity;

        /// <summary>The highest note, in semitones above the base pitch.</summary>
        public static int TopSemitones => Scale[Scale.Length - 1];

        /// <summary>How many gems in a row are in the current combo (0 after a reset).</summary>
        public int Step => step;

        /// <summary>The pitch multiplier for a gem played at <paramref name="now"/>, and advances the combo.</summary>
        public float Next(float now)
        {
            if (float.IsNaN(now) || now - lastTime > ResetSeconds || now < lastTime)
            {
                step = 0;
            }

            lastTime = float.IsNaN(now) ? lastTime : now;
            int semitones = Scale[Math.Min(step, Scale.Length - 1)];
            step++;
            return (float)Math.Pow(2.0, semitones / 12.0);
        }

        public void Reset()
        {
            step = 0;
            lastTime = float.NegativeInfinity;
        }
    }
}
