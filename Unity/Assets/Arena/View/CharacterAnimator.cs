using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PersonalArena.View
{
    /// <summary>
    /// Small crossfading clip player on top of the Playables API. One looping base state
    /// (idle, run, block...) plus an optional one-shot that overrides it until it finishes.
    /// Time is driven by the caller so animation follows simulation speed and pause.
    /// </summary>
    public sealed class CharacterAnimator : IDisposable
    {
        private const float FadeSeconds = 0.12f;

        private readonly AnimationClip[] clips;
        private readonly bool[] loops;
        private readonly AnimationClipPlayable[] playables;
        private readonly float[] times;
        private readonly float[] speeds;
        private readonly float[] weights;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;

        private int baseState = -1;
        private int oneShot = -1;
        private bool holdOneShot;

        public CharacterAnimator(Animator animator, AnimationClip[] stateClips, bool[] stateLoops, string graphName)
        {
            clips = stateClips;
            loops = stateLoops;
            int count = clips.Length;
            playables = new AnimationClipPlayable[count];
            times = new float[count];
            speeds = new float[count];
            weights = new float[count];

            graph = PlayableGraph.Create(graphName);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            mixer = AnimationMixerPlayable.Create(graph, count);
            output.SetSourcePlayable(mixer);
            for (int i = 0; i < count; i++)
            {
                speeds[i] = 1f;
                if (clips[i] == null)
                {
                    continue;
                }

                playables[i] = AnimationClipPlayable.Create(graph, clips[i]);
                playables[i].SetApplyFootIK(false);
                graph.Connect(playables[i], 0, mixer, i);
                mixer.SetInputWeight(i, 0f);
            }
            graph.Play();
        }

        public int OneShot => oneShot;

        /// <summary>Snaps to a base state with no crossfade and no one-shot (used on respawn/reset).</summary>
        public void ResetTo(int state)
        {
            oneShot = -1;
            holdOneShot = false;
            baseState = Valid(state) ? state : -1;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = i == baseState ? 1f : 0f;
                times[i] = 0f;
            }
            Apply();
        }

        public void SetBase(int state, float speed)
        {
            if (!Valid(state))
            {
                return;
            }
            if (state != baseState && weights[state] <= 0.001f)
            {
                times[state] = 0f;
            }
            baseState = state;
            if (oneShot != state)
            {
                speeds[state] = speed;
            }
        }

        /// <summary>Plays a clip once from the start; with hold it freezes on its last frame until reset.</summary>
        public void PlayOneShot(int state, float speed, bool hold)
        {
            if (!Valid(state))
            {
                return;
            }
            oneShot = state;
            holdOneShot = hold;
            times[state] = 0f;
            speeds[state] = speed;
        }

        public void Tick(float deltaTime)
        {
            if (!graph.IsValid())
            {
                return;
            }

            if (oneShot >= 0 && !holdOneShot && times[oneShot] >= clips[oneShot].length - FadeSeconds * speeds[oneShot])
            {
                oneShot = -1;
            }

            int target = oneShot >= 0 ? oneShot : baseState;
            float fade = deltaTime / FadeSeconds;
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (clips[i] == null)
                {
                    continue;
                }
                weights[i] = Mathf.MoveTowards(weights[i], i == target ? 1f : 0f, fade);
                if (weights[i] > 0f)
                {
                    float length = Mathf.Max(0.01f, clips[i].length);
                    times[i] += deltaTime * speeds[i];
                    times[i] = loops[i] ? Mathf.Repeat(times[i], length) : Mathf.Min(times[i], length);
                }
                total += weights[i];
            }

            if (total > 0f)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] /= total;
                }
            }
            Apply();
        }

        private void Apply()
        {
            for (int i = 0; i < weights.Length; i++)
            {
                if (clips[i] == null)
                {
                    continue;
                }
                mixer.SetInputWeight(i, weights[i]);
                playables[i].SetTime(times[i]);
            }
            graph.Evaluate(0f);
        }

        private bool Valid(int state)
        {
            return state >= 0 && state < clips.Length && clips[state] != null;
        }

        public void Dispose()
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }
        }
    }
}
