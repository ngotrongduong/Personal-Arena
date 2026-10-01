using System;
using System.Collections.Generic;
using System.Threading;
using PersonalArena.Core;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View
{
    /// <summary>
    /// Auto Farm (GDD §3.3) on one below-normal background thread. The runner loads its OWN
    /// <see cref="PolicyBrain"/> from the brain file bytes (a brain keeps activation buffers and is never shared
    /// with the viewer pilot), so a newer brain hot-loaded by the viewer does not change a running session.
    /// Only the worker calls <see cref="FarmSession.RunNext"/>; the main thread reads progress, cancels, and
    /// books the results with <see cref="Record"/>. No Unity API is used on the worker.
    /// </summary>
    public sealed class AutoFarmRunner
    {
        private readonly FarmSession session;
        private readonly CharacterBuild build;
        private readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        private Thread worker;
        private volatile bool workerFinished;
        private volatile string workerError;
        private FarmSummary summary;

        /// <param name="brainBytes">Bytes of the brain file the viewer plays with.</param>
        /// <param name="build">The owner's build (<see cref="ProfileRules.ToBuild"/>); copied.</param>
        /// <param name="runSeconds">Match length (900 normally; shorter only in tests).</param>
        /// <param name="classId">Class whose kit the matches use (the selected character's class); null = Warrior.</param>
        public AutoFarmRunner(byte[] brainBytes, CharacterBuild build, int runCount, int baseSeed, float runSeconds = 900f, string classId = null)
        {
            if (brainBytes == null || brainBytes.Length == 0)
            {
                throw new ArgumentException("Auto Farm needs the bytes of a brain file.", nameof(brainBytes));
            }
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            PolicyBrain brain = PolicyBrain.Load(brainBytes);
            string problem = SurvivorPilot.Validate(brain);
            if (problem != null)
            {
                throw new ArgumentException(problem, nameof(brainBytes));
            }

            this.build = build.Clone();
            session = new FarmSession(brain, this.build, runCount, baseSeed, runSeconds, classId);
        }

        public int RunCount => session.RunCount;
        public string ClassId => session.ClassId;
        public int Completed => session.Completed;
        public float TotalGold => session.TotalGold;
        public int Tier => build.Tier;
        public bool IsStarted => worker != null;
        /// <summary>True once the worker has finished (all matches, cancelled, or stopped by an error).</summary>
        public bool IsDone => session.IsDone || workerFinished;
        public bool IsRunning => worker != null && !IsDone;
        public bool IsCancelled => session.IsCancelled;
        public bool IsRecorded => summary != null;
        public FarmSummary Summary => summary;

        /// <summary>The error that stopped the session early, or null. Meaningful once <see cref="IsDone"/>.</summary>
        public string Error
        {
            get
            {
                string error = workerError;
                if (!string.IsNullOrEmpty(error))
                {
                    return error;
                }
                return session.IsDone ? session.Error : null;
            }
        }

        /// <summary>Seconds since <see cref="Start"/>; frozen once done. Main thread only.</summary>
        public float ElapsedSeconds
        {
            get
            {
                if (IsDone && clock.IsRunning)
                {
                    clock.Stop();
                }
                return (float)clock.Elapsed.TotalSeconds;
            }
        }

        /// <summary>Estimated seconds until every match is done, or -1 before the first match finished.</summary>
        public float EstimatedSecondsLeft
        {
            get
            {
                int done = Completed;
                if (IsDone)
                {
                    return 0f;
                }
                if (done <= 0)
                {
                    return -1f;
                }
                return ElapsedSeconds / done * (RunCount - done);
            }
        }

        /// <summary>Starts the worker thread (once).</summary>
        public void Start()
        {
            if (worker != null)
            {
                return;
            }

            clock.Start();
            worker = new Thread(Work)
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal,
                Name = "Auto Farm"
            };
            worker.Start();
        }

        /// <summary>The match in progress finishes; no new match starts.</summary>
        public void Cancel()
        {
            session.Cancel();
        }

        /// <summary>Waits up to <paramref name="milliseconds"/> for the worker. True when it has finished.</summary>
        public bool Wait(int milliseconds)
        {
            if (worker == null)
            {
                return true;
            }
            return worker.Join(Math.Max(0, milliseconds));
        }

        /// <summary>
        /// Books every finished match into the profile (main thread, once): <see cref="ProfileRules.RecordRun"/>
        /// with Farm = true per match, one "farm" economy line per match, then
        /// <see cref="ProfileRules.RecordFarmSession"/>. When called while the worker still runs (quit timeout),
        /// only the matches finished so far are booked and the session is cancelled.
        /// </summary>
        public FarmSummary Record(PlayerProfile profile, CharacterProfile character, int loadout, string loadoutName, List<string> economyLines)
        {
            if (summary != null)
            {
                return summary;
            }
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            bool finished = IsDone;
            if (!finished)
            {
                session.Cancel();
            }

            // Results[i] is added before Completed is incremented, so the first Completed entries are always there.
            int count = finished ? session.Results.Count : Math.Min(session.Completed, session.Results.Count);
            FarmSummary result = new FarmSummary
            {
                Cancelled = session.IsCancelled || !finished,
                Error = Error,
                UnlockedTier = profile.UnlockedTier
            };
            double seconds = 0;
            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < count; i++)
            {
                SurvivorRunStats stats = session.Results[i];
                if (stats == null)
                {
                    continue;
                }

                RunReward reward = ProfileRules.RecordRun(profile, character, loadout, MetaViewLogic.ToRunResult(stats, build.Tier, true));
                result.Runs++;
                if (stats.EndReason == EndReason.Won)
                {
                    result.Wins++;
                }
                result.Gold += reward.GoldAdded;
                seconds += stats.SurvivedSeconds;
                if (reward.TierUnlocked)
                {
                    result.TierUnlocked = true;
                }
                result.UnlockedTier = reward.UnlockedTier;
                economyLines?.Add(EconomyLog.Line(now, EconomyLog.FarmMode, character.ClassId, build.Tier, loadoutName, build, stats));
            }

            result.AverageSeconds = result.Runs > 0 ? (float)(seconds / result.Runs) : 0f;
            ProfileRules.RecordFarmSession(profile);
            summary = result;
            return result;
        }

        private void Work()
        {
            try
            {
                while (session.RunNext())
                {
                }
            }
            catch (Exception exception)
            {
                // RunNext already catches match errors; this only guards against the unexpected.
                workerError = exception.Message;
            }
            finally
            {
                workerFinished = true;
            }
        }
    }
}
