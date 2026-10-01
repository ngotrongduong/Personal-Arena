using System;
using System.Collections.Generic;
using System.Threading;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Meta
{
    /// <summary>
    /// Auto Farm (GDD §3.3): the brain plays <see cref="RunCount"/> matches with no rendering.
    /// Threading rule: one FarmSession is driven by exactly one worker thread (it calls
    /// <see cref="RunNext"/>). Any other thread may read <see cref="Completed"/>, <see cref="TotalGold"/>
    /// and <see cref="IsDone"/> and call <see cref="Cancel"/>; it reads <see cref="Results"/> only after
    /// <see cref="IsDone"/> is true. Pass a dedicated <see cref="PolicyBrain"/> instance: a brain keeps
    /// internal activation buffers and must never be shared with the viewer's pilot.
    /// </summary>
    public sealed class FarmSession
    {
        public const int MinRuns = 1;
        public const int MaxRuns = 100;

        private readonly PolicyBrain brain;
        private readonly CharacterBuild build;
        private readonly float runSeconds;
        private readonly string classId;
        private readonly SurvivorEvaluator evaluator = new SurvivorEvaluator();
        private readonly List<SurvivorRunStats> results = new List<SurvivorRunStats>();
        private int completed;
        private volatile float totalGold;
        private volatile bool done;
        private volatile bool cancelled;

        /// <param name="runSeconds">Run length of every match (the normal 900 s; shorter only for tests).</param>
        /// <param name="classId">Class whose kit the matches use (M7: warrior, mage, archer); null means the Warrior.</param>
        public FarmSession(PolicyBrain brain, CharacterBuild build, int runCount, int baseSeed, float runSeconds = 900f, string classId = null)
        {
            if (runCount < MinRuns || runCount > MaxRuns) throw new ArgumentOutOfRangeException(nameof(runCount));
            this.brain = brain ?? throw new ArgumentNullException(nameof(brain));
            if (build == null) throw new ArgumentNullException(nameof(build));
            build.Validate();
            this.classId = classId ?? ProfileRules.WarriorId;
            if (SurvivorDefaults.ForClass(this.classId) == null) throw new ArgumentException("Unknown class '" + classId + "'.", nameof(classId));
            this.build = build.Clone();
            this.runSeconds = runSeconds;
            RunCount = runCount;
            BaseSeed = baseSeed;
            NewConfig().Validate();
        }

        public int RunCount { get; }
        /// <summary>Class id of the kit every match uses.</summary>
        public string ClassId => classId;
        public int BaseSeed { get; }
        /// <summary>Stats of the finished matches, in seed order. Read only after <see cref="IsDone"/>.</summary>
        public IReadOnlyList<SurvivorRunStats> Results => results;
        public int Completed => Volatile.Read(ref completed);
        public float TotalGold => totalGold;
        /// <summary>True once every match ran or the session was cancelled (and no match is running).</summary>
        public bool IsDone => done;
        public bool IsCancelled => cancelled;
        /// <summary>Message of the exception that stopped the session early, or null. Read after <see cref="IsDone"/>.</summary>
        public string Error { get; private set; }

        /// <summary>
        /// Asks the session to stop: the match in progress finishes, no new match starts. Only the worker
        /// thread sets <see cref="IsDone"/> (at the end of that match, or on its next <see cref="RunNext"/>
        /// call), so the worker loop is simply <c>while (session.RunNext()) { }</c>.
        /// </summary>
        public void Cancel() { cancelled = true; }

        /// <summary>Runs one match (seed <c>BaseSeed + index</c>). Returns false when done or cancelled.</summary>
        public bool RunNext()
        {
            if (done) return false;
            int index = Completed;
            if (cancelled || index >= RunCount) { done = true; return false; }
            SurvivorConfig config = NewConfig();
            SurvivorRunStats stats;
            try
            {
                stats = evaluator.RunOne(brain, config, BaseSeed + index, true);
            }
            catch (Exception exception)
            {
                // Never leave a UI waiting on IsDone: stop the session and keep the finished matches.
                Error = exception.Message;
                done = true;
                return false;
            }
            results.Add(stats);
            totalGold += stats.Gold;
            Interlocked.Increment(ref completed);
            if (cancelled || Completed >= RunCount) done = true;
            return true;
        }

        private SurvivorConfig NewConfig() =>
            new SurvivorConfig { Build = build.Clone(), RunSeconds = runSeconds, ClassDef = SurvivorDefaults.ForClass(classId) };
    }
}
