using System;

namespace PersonalArena.View
{
    /// <summary>How to play one accepted sound: loudness 0..1 (before the volume sliders), pitch and stereo pan.</summary>
    public readonly struct SoundPlay
    {
        public readonly SoundCue Cue;
        public readonly float Volume;
        public readonly float Pitch;
        public readonly float Pan;
        public readonly SoundBus Bus;
        public readonly bool Important;

        public SoundPlay(SoundCue cue, float volume, float pitch, float pan, SoundBus bus, bool important)
        {
            Cue = cue;
            Volume = volume;
            Pitch = pitch;
            Pan = pan;
            Bus = bus;
            Important = important;
        }
    }

    /// <summary>
    /// Decides whether and how a requested cue plays: the <see cref="SoundThrottle"/> voice limits, the gem combo
    /// pitch, a small random pitch and volume variation, a quieter sound far from the hero and a little stereo pan.
    /// Counts every request and every accepted sound in <see cref="Stats"/>. Pure; time is real seconds from the caller.
    /// </summary>
    public sealed class SoundMixer
    {
        /// <summary>Full loudness up to this distance from the hero (world units, about the inner screen).</summary>
        public const float NearDistance = 8f;
        /// <summary>From here on (off-screen) a sound keeps only <see cref="FarGain"/> of its loudness.</summary>
        public const float FarDistance = 26f;
        public const float FarGain = 0.15f;
        /// <summary>Largest stereo pan (−1 left .. 1 right); kept small so the mix stays centred.</summary>
        public const float MaxPan = 0.35f;

        private readonly SoundThrottle throttle;
        private readonly GemCombo gemCombo = new GemCombo();
        private readonly Random random;

        public SoundMixer(int seed = 1, int globalVoices = SoundThrottle.DefaultGlobalVoices)
        {
            throttle = new SoundThrottle(globalVoices);
            random = new Random(seed);
        }

        public SoundThrottle Throttle => throttle;
        public GemCombo Gems => gemCombo;
        public SoundStats Stats { get; } = new SoundStats();

        /// <summary>
        /// Asks to play <paramref name="cue"/> at <paramref name="now"/>, <paramref name="distance"/> world units from the
        /// hero, at horizontal screen position <paramref name="viewportX"/> (0 left .. 1 right, 0.5 centre).
        /// </summary>
        public bool TryPlay(SoundCue cue, float now, float distance, float viewportX, out SoundPlay play)
        {
            play = default;
            if (cue == SoundCue.None)
            {
                return false;
            }

            Stats.CountRequested(cue);
            if (!throttle.TryStart(cue, now))
            {
                return false;
            }

            SoundCueInfo info = SoundCueInfo.For(cue);
            float volume = info.Volume * (1f + info.VolumeJitter * Signed());
            float pitch;
            if (cue == SoundCue.Gem)
            {
                pitch = info.Pitch * gemCombo.Next(now);
            }
            else
            {
                pitch = info.Pitch * (1f + info.PitchJitter * Signed());
            }

            float pan = 0f;
            if (info.Spatial)
            {
                volume *= DistanceGain(distance);
                pan = PanFromViewport(viewportX);
            }

            play = new SoundPlay(cue, Clamp01(volume), Math.Max(0.1f, pitch), pan, info.Bus, info.Important);
            Stats.CountPlayed(cue);
            return true;
        }

        /// <summary>A start that bypasses the limits but is still counted (music starts).</summary>
        public void CountForced(SoundCue cue)
        {
            Stats.CountRequested(cue);
            Stats.CountPlayed(cue);
        }

        /// <summary>New run: forget voices and the gem combo (the counts stay for the audio log).</summary>
        public void ResetVoices()
        {
            throttle.Reset();
            gemCombo.Reset();
        }

        /// <summary>A random clip index below <paramref name="count"/> (0 when there is at most one clip).</summary>
        public int PickClip(int count)
        {
            return count <= 1 ? 0 : random.Next(count);
        }

        /// <summary>1 near the hero, falling linearly to <see cref="FarGain"/> at <see cref="FarDistance"/> and beyond.</summary>
        public static float DistanceGain(float distance)
        {
            if (float.IsNaN(distance) || distance <= NearDistance)
            {
                return 1f;
            }
            if (distance >= FarDistance)
            {
                return FarGain;
            }

            float t = (distance - NearDistance) / (FarDistance - NearDistance);
            return 1f + (FarGain - 1f) * t;
        }

        /// <summary>Stereo pan from the horizontal viewport position (0.5 = centre), clamped to ±<see cref="MaxPan"/>.</summary>
        public static float PanFromViewport(float viewportX)
        {
            if (float.IsNaN(viewportX))
            {
                return 0f;
            }

            float pan = (viewportX - 0.5f) * 2f * MaxPan;
            return Math.Max(-MaxPan, Math.Min(MaxPan, pan));
        }

        private float Signed()
        {
            return (float)(random.NextDouble() * 2.0 - 1.0);
        }

        private static float Clamp01(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
        }
    }
}
