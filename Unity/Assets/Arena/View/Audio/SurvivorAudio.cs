using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Sound of the survivor viewer (M8). Turns the events of every watched sim step into cues
    /// (<see cref="SoundCueMap"/>), limits them (<see cref="SoundMixer"/>), and plays them as 2D sounds with a little
    /// stereo pan from a pool of AudioSources. Two music sources crossfade between the run loop and the boss loop;
    /// one more source plays the run-end jingle. Rules stay in Core and the pure classes; this only plays clips.
    /// Viewer only: Auto Farm runs and the training scene never reach it. <see cref="Configure"/> with forced silence
    /// (automated runs) keeps the listener at volume 0 while still counting cues for <c>-audioLog</c>.
    /// </summary>
    public sealed class SurvivorAudio : MonoBehaviour
    {
        private const int EffectSourceCount = 28;
        private const float CrossfadeSeconds = 1.5f;
        private const float MusicLevel = 0.8f;
        private const float PauseDuck = 0.25f;
        private const float EndDuck = 0.3f;
        private const float OfferDuck = 0.6f;
        private const float DuckSpeed = 2.5f;

        [SerializeField] private SurvivorSoundSet soundSet;

        private readonly List<SoundRequest> requests = new List<SoundRequest>(64);
        private readonly AudioSource[] music = new AudioSource[2];
        private AudioSource[] effects;
        private float[] effectStarted;
        private bool[] effectImportant;
        private AudioSource jingle;
        private SoundMixer mixer;
        private SoundSettings settings;
        private ISoundSettingsStorage storage;
        private Camera viewCamera;
        private bool[] missingWarned;
        private int activeMusic;
        private float musicFade = 1f;
        private float duck = 1f;
        private bool forcedSilent;
        private string audioLogPath;
        private string logHeader;
        private bool jinglePlayed;
        private bool bossMusic;
        private bool paused;
        private bool ended;
        private bool offer;

        public SoundSettings Settings => settings;
        public SoundStats Stats => mixer.Stats;
        public bool ForcedSilent => forcedSilent;

        /// <summary>The scene's sound set (set by PlaySceneBuilder; settable for tests and tools).</summary>
        public SurvivorSoundSet SoundSet
        {
            get => soundSet;
            set => soundSet = value;
        }

        private void Awake()
        {
            storage = new PlayerPrefsSoundStorage();
            settings = SoundSettings.Load(storage);
            mixer = new SoundMixer(Environment.TickCount);
            missingWarned = new bool[SoundCueInfo.CueCount];
            CreateSources();
            if (soundSet == null)
            {
                Debug.LogWarning("SurvivorAudio: no sound set assigned; the viewer stays silent.");
            }
        }

        private void OnEnable()
        {
            UiSounds.Requested += Play;
        }

        private void OnDisable()
        {
            UiSounds.Requested -= Play;
        }

        /// <summary>
        /// Command-line options from the controller: <paramref name="silent"/> forces volume 0 (screenshot, smoke test,
        /// -mute); <paramref name="logPath"/> (-audioLog) receives the cue counts at quit.
        /// </summary>
        public void Configure(bool silent, string logPath, string header)
        {
            forcedSilent = silent;
            audioLogPath = string.IsNullOrWhiteSpace(logPath) ? null : logPath;
            logHeader = header;
            ApplyListenerVolume();
        }

        /// <summary>The camera used for the stereo pan of world sounds.</summary>
        public void SetCamera(Camera camera)
        {
            viewCamera = camera;
        }

        /// <summary>A new run: forget voices and the gem combo, allow the next jingle, back to the run music.</summary>
        public void OnRunStarted()
        {
            jinglePlayed = false;
            ended = false;
            offer = false;
            bossMusic = false;
            mixer.ResetVoices();
            if (jingle != null)
            {
                jingle.Stop();
            }
            SwitchMusic(soundSet != null ? soundSet.RunMusic : null, SoundCue.MusicRun);
        }

        /// <summary>After every sim step: the sounds of the step's events (boss spawn also switches the music).</summary>
        public void OnStep(SurvivorSim sim)
        {
            if (sim == null)
            {
                return;
            }

            SoundCueMap.MapStep(sim.Events, sim.Config.ClassDef, requests);
            Vec2 hero = sim.Hero.Position;
            for (int i = 0; i < requests.Count; i++)
            {
                SoundRequest request = requests[i];
                if (request.Cue == SoundCue.BossSpawn && !bossMusic)
                {
                    bossMusic = true;
                    SwitchMusic(soundSet != null ? soundSet.BossMusic : null, SoundCue.MusicBoss);
                }

                Vec2 point = request.AtHero ? hero : request.Point;
                PlayAt(request.Cue, Vec2.Distance(point, hero), point);
            }
        }

        /// <summary>The level-up cards appeared: chime and dip the music while the AI chooses.</summary>
        public void OnCardsShown()
        {
            offer = true;
            Play(SoundCue.CardsShown);
        }

        /// <summary>The AI's pick is highlighted (its card sound came from the pick step): music back up.</summary>
        public void OnPickHighlighted()
        {
            offer = false;
        }

        /// <summary>The run ended: one jingle per run, and the music dips under it for the end screen.</summary>
        public void OnRunEnded(EndReason reason)
        {
            ended = true;
            if (jinglePlayed)
            {
                return;
            }

            jinglePlayed = true;
            SoundCue cue = SoundCueMap.JingleFor(reason);
            if (cue == SoundCue.None || !mixer.TryPlay(cue, Time.unscaledTime, 0f, 0.5f, out SoundPlay play))
            {
                return;
            }

            AudioClip clip = PickClip(cue);
            if (clip == null || jingle == null)
            {
                return;
            }

            jingle.Stop();
            jingle.clip = clip;
            jingle.pitch = play.Pitch;
            jingle.volume = play.Volume * settings.Effects;
            jingle.Play();
        }

        /// <summary>The class changed (a fresh run follows): a short switch sound.</summary>
        public void OnClassSwitched()
        {
            Play(SoundCue.UiToggle);
        }

        /// <summary>Esc pause: the music dips while paused.</summary>
        public void SetPaused(bool isPaused)
        {
            paused = isPaused;
        }

        /// <summary>M: mute or unmute (saved). Returns the new muted state.</summary>
        public bool ToggleMute()
        {
            settings.Muted = !settings.Muted;
            settings.Save(storage);
            ApplyListenerVolume();
            if (!settings.Muted)
            {
                Play(SoundCue.UiToggle);
            }
            return settings.Muted;
        }

        /// <summary>A hero-centred cue (menus, cards, switches).</summary>
        public void Play(SoundCue cue)
        {
            PlayAt(cue, 0f, default, true);
        }

        /// <summary>Writes the cue counts to the -audioLog path (no-op without one).</summary>
        public void WriteAudioLog()
        {
            if (string.IsNullOrEmpty(audioLogPath) || mixer == null)
            {
                return;
            }

            try
            {
                string full = Path.GetFullPath(audioLogPath);
                string directory = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string header = "forcedSilent " + (forcedSilent ? "true" : "false") +
                    "\nlistenerVolume " + AudioListener.volume.ToString("0.###", CultureInfo.InvariantCulture) +
                    "\nsoundSet " + (soundSet != null ? soundSet.name : "none") +
                    (string.IsNullOrEmpty(logHeader) ? string.Empty : "\n" + logHeader);
                File.WriteAllText(full, mixer.Stats.Format(header));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Audio log " + audioLogPath + " not written: " + exception.Message);
            }
        }

        private void Update()
        {
            ApplyListenerVolume();
            float delta = Time.unscaledDeltaTime;
            float duckTarget = paused ? PauseDuck : ended ? EndDuck : offer ? OfferDuck : 1f;
            duck = Mathf.MoveTowards(duck, duckTarget, DuckSpeed * delta);
            musicFade = Mathf.Min(1f, musicFade + delta / CrossfadeSeconds);

            float level = settings.Music * MusicLevel * duck;
            AudioSource current = music[activeMusic];
            AudioSource previous = music[1 - activeMusic];
            if (current != null)
            {
                current.volume = level * musicFade;
            }
            if (previous != null)
            {
                previous.volume = level * (1f - musicFade);
                if (musicFade >= 1f && previous.isPlaying)
                {
                    previous.Stop();
                }
            }
        }

        private void OnApplicationQuit()
        {
            WriteAudioLog();
        }

        private void ApplyListenerVolume()
        {
            if (settings != null)
            {
                AudioListener.volume = settings.ListenerVolume(Application.isFocused, forcedSilent);
            }
        }

        private void PlayAt(SoundCue cue, float distance, Vec2 point, bool centred = false)
        {
            if (mixer == null || cue == SoundCue.None)
            {
                return;
            }

            if (!mixer.TryPlay(cue, Time.unscaledTime, distance, 0.5f, out SoundPlay play))
            {
                return;
            }

            // Pan only for accepted world sounds (a horde requests thousands of cues per second).
            float pan = play.Pan;
            if (!centred && viewCamera != null && distance > 0.5f && SoundCueInfo.For(cue).Spatial)
            {
                pan = SoundMixer.PanFromViewport(viewCamera.WorldToViewportPoint(ArenaSpace.ToWorld(point)).x);
            }

            AudioClip clip = PickClip(cue);
            if (clip == null)
            {
                return;
            }

            int index = FreeSource(play.Important);
            AudioSource source = effects[index];
            source.Stop();
            source.clip = clip;
            source.volume = play.Volume * settings.BusVolume(play.Bus);
            source.pitch = play.Pitch;
            source.panStereo = pan;
            source.Play();
            effectStarted[index] = Time.unscaledTime;
            effectImportant[index] = play.Important;
        }

        /// <summary>A free effect source, else the oldest ordinary one, else the oldest of all.</summary>
        private int FreeSource(bool important)
        {
            int oldestOrdinary = -1;
            int oldest = 0;
            for (int i = 0; i < effects.Length; i++)
            {
                if (!effects[i].isPlaying)
                {
                    return i;
                }
                if (!effectImportant[i] && (oldestOrdinary < 0 || effectStarted[i] < effectStarted[oldestOrdinary]))
                {
                    oldestOrdinary = i;
                }
                if (effectStarted[i] < effectStarted[oldest])
                {
                    oldest = i;
                }
            }

            return oldestOrdinary >= 0 ? oldestOrdinary : oldest;
        }

        private AudioClip PickClip(SoundCue cue)
        {
            AudioClip[] clips = soundSet != null ? soundSet.ClipsFor(cue) : Array.Empty<AudioClip>();
            if (clips.Length == 0)
            {
                int index = (int)cue;
                if (soundSet != null && index >= 0 && index < missingWarned.Length && !missingWarned[index])
                {
                    missingWarned[index] = true;
                    mixer.Stats.CountMissingClip();
                    Debug.LogWarning("SurvivorAudio: missing clip for cue " + cue + ".");
                }
                return null;
            }

            return clips[mixer.PickClip(clips.Length)];
        }

        /// <summary>Starts <paramref name="clip"/> on the idle music source and crossfades to it (no-op if already playing).</summary>
        private void SwitchMusic(AudioClip clip, SoundCue cue)
        {
            if (clip == null || music[0] == null)
            {
                return;
            }

            AudioSource current = music[activeMusic];
            if (current.clip == clip && current.isPlaying)
            {
                return;
            }

            mixer.CountForced(cue);
            activeMusic = 1 - activeMusic;
            AudioSource next = music[activeMusic];
            next.Stop();
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            musicFade = current.isPlaying ? 0f : 1f;
            if (!current.isPlaying)
            {
                next.volume = settings.Music * MusicLevel * duck;
            }
        }

        private void CreateSources()
        {
            effects = new AudioSource[EffectSourceCount];
            effectStarted = new float[EffectSourceCount];
            effectImportant = new bool[EffectSourceCount];
            GameObject effectRoot = new GameObject("Effect Sources");
            effectRoot.transform.SetParent(transform, false);
            for (int i = 0; i < effects.Length; i++)
            {
                effects[i] = CreateSource(effectRoot, false, 128);
                effectStarted[i] = float.NegativeInfinity;
            }

            GameObject musicRoot = new GameObject("Music Sources");
            musicRoot.transform.SetParent(transform, false);
            music[0] = CreateSource(musicRoot, true, 0);
            music[1] = CreateSource(musicRoot, true, 0);
            jingle = CreateSource(musicRoot, false, 8);
        }

        private static AudioSource CreateSource(GameObject owner, bool loop, int priority)
        {
            AudioSource source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.priority = priority;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            return source;
        }
    }
}
