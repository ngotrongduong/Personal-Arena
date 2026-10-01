using System;
using System.Collections.Generic;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// The clips of the survivor viewer: one or more clips per <see cref="SoundCue"/> (one is picked at random) and
    /// the two music loops. Filled by the editor's SurvivorSoundSetBuilder from Assets/ThirdParty/Audio.
    /// </summary>
    [CreateAssetMenu(menuName = "Personal Arena/Survivor Sound Set")]
    public sealed class SurvivorSoundSet : ScriptableObject
    {
        [Serializable]
        public sealed class CueClips
        {
            public SoundCue Cue;
            public AudioClip[] Clips = Array.Empty<AudioClip>();
        }

        public CueClips[] Cues = Array.Empty<CueClips>();
        [Tooltip("Calm, eerie loop while the run goes on.")]
        public AudioClip RunMusic;
        [Tooltip("Tenser loop once the boss is out.")]
        public AudioClip BossMusic;

        [NonSerialized] private AudioClip[][] byCue;

        /// <summary>The clips of <paramref name="cue"/> (empty when it has none).</summary>
        public AudioClip[] ClipsFor(SoundCue cue)
        {
            if (byCue == null)
            {
                BuildLookup();
            }

            int index = (int)cue;
            return index >= 0 && index < byCue.Length && byCue[index] != null ? byCue[index] : Array.Empty<AudioClip>();
        }

        private void OnValidate()
        {
            byCue = null;
        }

        private void BuildLookup()
        {
            byCue = new AudioClip[SoundCueInfo.CueCount][];
            if (Cues == null)
            {
                return;
            }

            foreach (CueClips entry in Cues)
            {
                int index = entry != null ? (int)entry.Cue : -1;
                if (index < 0 || index >= byCue.Length || entry.Clips == null)
                {
                    continue;
                }

                List<AudioClip> clips = new List<AudioClip>(entry.Clips.Length);
                foreach (AudioClip clip in entry.Clips)
                {
                    if (clip != null)
                    {
                        clips.Add(clip);
                    }
                }
                byCue[index] = clips.ToArray();
            }
        }
    }
}
