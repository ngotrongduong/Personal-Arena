using System;
using System.Collections.Generic;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>Where <see cref="SoundSettings"/> are kept: PlayerPrefs in the viewer, memory in tests.</summary>
    public interface ISoundSettingsStorage
    {
        bool HasKey(string key);
        float GetFloat(string key, float fallback);
        int GetInt(string key, int fallback);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
        void Save();
    }

    /// <summary>The viewer's storage: Unity PlayerPrefs (per user, survives restarts).</summary>
    public sealed class PlayerPrefsSoundStorage : ISoundSettingsStorage
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>In-memory storage for tests (never touches the real PlayerPrefs).</summary>
    public sealed class MemorySoundStorage : ISoundSettingsStorage
    {
        private readonly Dictionary<string, float> floats = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> ints = new Dictionary<string, int>(StringComparer.Ordinal);

        public int SaveCount { get; private set; }

        public bool HasKey(string key) => floats.ContainsKey(key) || ints.ContainsKey(key);
        public float GetFloat(string key, float fallback) => floats.TryGetValue(key, out float value) ? value : fallback;
        public int GetInt(string key, int fallback) => ints.TryGetValue(key, out int value) ? value : fallback;
        public void SetFloat(string key, float value) => floats[key] = value;
        public void SetInt(string key, int value) => ints[key] = value;
        public void Save() => SaveCount++;
    }

    /// <summary>
    /// The owner's sound settings: master, music and effects volume (0..1), mute (M), and mute while the window is
    /// not in front (default on, the viewer runs for hours in the background). Volumes are clamped; bad stored
    /// values fall back to the defaults.
    /// </summary>
    public sealed class SoundSettings
    {
        public const float DefaultMaster = 1f;
        public const float DefaultMusic = 0.55f;
        public const float DefaultEffects = 0.8f;

        public const string MasterKey = "Audio.MasterVolume";
        public const string MusicKey = "Audio.MusicVolume";
        public const string EffectsKey = "Audio.EffectsVolume";
        public const string MutedKey = "Audio.Muted";
        public const string MuteInBackgroundKey = "Audio.MuteInBackground";

        private float master = DefaultMaster;
        private float music = DefaultMusic;
        private float effects = DefaultEffects;

        public float Master { get => master; set => master = Clamp(value, DefaultMaster); }
        public float Music { get => music; set => music = Clamp(value, DefaultMusic); }
        public float Effects { get => effects; set => effects = Clamp(value, DefaultEffects); }
        public bool Muted { get; set; }
        public bool MuteInBackground { get; set; } = true;

        /// <summary>Loads from <paramref name="storage"/>; missing keys keep the defaults.</summary>
        public static SoundSettings Load(ISoundSettingsStorage storage)
        {
            SoundSettings settings = new SoundSettings();
            if (storage == null)
            {
                return settings;
            }

            settings.Master = storage.GetFloat(MasterKey, DefaultMaster);
            settings.Music = storage.GetFloat(MusicKey, DefaultMusic);
            settings.Effects = storage.GetFloat(EffectsKey, DefaultEffects);
            settings.Muted = storage.GetInt(MutedKey, 0) != 0;
            settings.MuteInBackground = storage.GetInt(MuteInBackgroundKey, 1) != 0;
            return settings;
        }

        public void Save(ISoundSettingsStorage storage)
        {
            if (storage == null)
            {
                return;
            }

            storage.SetFloat(MasterKey, master);
            storage.SetFloat(MusicKey, music);
            storage.SetFloat(EffectsKey, effects);
            storage.SetInt(MutedKey, Muted ? 1 : 0);
            storage.SetInt(MuteInBackgroundKey, MuteInBackground ? 1 : 0);
            storage.Save();
        }

        /// <summary>
        /// The whole mix loudness (for <c>AudioListener.volume</c>): 0 when muted, forced silent (automated runs) or in
        /// the background with <see cref="MuteInBackground"/> on; else <see cref="Master"/>.
        /// </summary>
        public float ListenerVolume(bool windowFocused, bool forcedSilent)
        {
            if (forcedSilent || Muted || (!windowFocused && MuteInBackground))
            {
                return 0f;
            }

            return master;
        }

        /// <summary>The slider of a bus.</summary>
        public float BusVolume(SoundBus bus)
        {
            return bus == SoundBus.Music ? music : effects;
        }

        private static float Clamp(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
