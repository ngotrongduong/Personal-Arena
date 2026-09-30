using System;
using System.Collections.Generic;

namespace PersonalArena.Core.Survivor
{
    public enum ChronicleKind { NewItem, ItemMaxed, StyleChange, NearDeath, EliteKilled, ChestOpened, BossSpawned, BossKilled, End }

    /// <summary>One moment of the post-run story. Fields that do not apply keep their defaults (ItemIndex −1).</summary>
    public struct ChronicleEntry
    {
        public float Time;
        public ChronicleKind Kind;
        public int ItemIndex;
        public int Level;
        public float Value;
        public SpectatorLabel Label;
        public DeathCause Cause;
    }

    /// <summary>The data of the post-run story. Core gives data only; the View writes the Vietnamese text.</summary>
    public sealed class RunChronicle
    {
        /// <summary>Entries in time order (at most <see cref="RunChronicleRecorder.Cap"/>).</summary>
        public readonly List<ChronicleEntry> Entries = new List<ChronicleEntry>();
        public bool Finished;
        public EndReason EndReason;
        public DeathCause DeathCause;
        public float SurvivedSeconds;
        public int Level;
        public int Kills;
        public float Gold;
        public float BossDamageFraction;
        public float MinHpRatio = 1f;
        public float MinHpTime;
    }

    /// <summary>
    /// Records the run story. Call <see cref="Reset"/> at run start, <see cref="Observe"/> after each
    /// <see cref="SurvivorSim.Step"/> (with the spectator label shown that tick) and <see cref="Finish"/>
    /// at the end. Allocates only when it adds an entry (viewer / Auto Farm use, not training).
    /// </summary>
    public sealed class RunChronicleRecorder
    {
        public const int Cap = 64;
        /// <summary>A near-death dip starts when the HP ratio drops below this.</summary>
        public const float NearDeathRatio = 0.2f;
        /// <summary>A dip ends when the HP ratio is back above this.</summary>
        public const float NearDeathRecoverRatio = 0.4f;
        /// <summary>At most one NearDeath entry per this many seconds.</summary>
        public const float NearDeathMinGapSeconds = 60f;
        public const float StyleMinuteSeconds = 60f;
        /// <summary>A minute's style must hold at least this share of the minute.</summary>
        public const float StyleMinShare = 0.4f;
        /// <summary>Tolerance on the minute boundary (sim time is a float sum of fixed ticks).</summary>
        private const float MinuteEpsilon = 1e-3f;

        private readonly float[] minuteLabelSeconds = new float[SpectatorLabels.Count];
        private int completedMinutes;
        private SpectatorLabel lastStyle;
        private bool inDip;
        private int dipEntryIndex;
        private float lastNearDeathTime;

        public RunChronicleRecorder() { Reset(); }

        public RunChronicle Result { get; private set; }

        public void Reset()
        {
            Result = new RunChronicle();
            Array.Clear(minuteLabelSeconds, 0, minuteLabelSeconds.Length);
            completedMinutes = 0; lastStyle = SpectatorLabel.None;
            inDip = false; dipEntryIndex = -1; lastNearDeathTime = float.NegativeInfinity;
        }

        public void Observe(SurvivorSim sim, SpectatorLabel shownLabel)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (Result.Finished) return;
            ReadEvents(sim);
            TrackNearDeath(sim);
            TrackStyle(sim, shownLabel);
        }

        /// <summary>Adds the End entry and the summary. Idempotent.</summary>
        public void Finish(SurvivorSim sim)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            RunChronicle r = Result;
            if (r.Finished) return;
            r.Finished = true;
            r.EndReason = sim.EndReason; r.DeathCause = sim.DeathCause; r.SurvivedSeconds = sim.Time;
            r.Level = sim.Level; r.Kills = sim.Kills; r.Gold = sim.Gold; r.BossDamageFraction = sim.BossDamageFraction;
            r.MinHpRatio = sim.MinHpRatio; r.MinHpTime = sim.MinHpTime;
            Add(Entry(sim.Time, ChronicleKind.End, value: sim.Time, cause: sim.DeathCause));
        }

        private void ReadEvents(SurvivorSim sim)
        {
            IReadOnlyList<SurvivorEvent> events = sim.Events;
            for (int i = 0; i < events.Count; i++)
            {
                SurvivorEvent e = events[i];
                switch (e.Type)
                {
                    case SurvivorEventType.ItemPicked:
                        AddItemProgress(sim.Time, e.Id, (int)e.Value);
                        break;
                    case SurvivorEventType.ChestOpened:
                        Add(Entry(sim.Time, ChronicleKind.ChestOpened, e.Id, (int)e.Extra, e.Value));
                        if (e.Id >= 0) AddItemProgress(sim.Time, e.Id, (int)e.Extra);
                        break;
                    case SurvivorEventType.EliteKilled: Add(Entry(sim.Time, ChronicleKind.EliteKilled)); break;
                    case SurvivorEventType.BossSpawned: Add(Entry(sim.Time, ChronicleKind.BossSpawned)); break;
                    case SurvivorEventType.BossKilled: Add(Entry(sim.Time, ChronicleKind.BossKilled)); break;
                }
            }
        }

        private void AddItemProgress(float time, int index, int level)
        {
            ItemDef def = SurvivorCatalog.Get(index);
            if (def == null || def.Kind == ItemKind.Filler || level <= 0) return;
            if (level == 1) Add(Entry(time, ChronicleKind.NewItem, index, 1));
            if (level >= def.MaxLevel) Add(Entry(time, ChronicleKind.ItemMaxed, index, level));
        }

        private void TrackNearDeath(SurvivorSim sim)
        {
            SurvivorHero hero = sim.Hero;
            if (!hero.Alive || hero.MaxHp <= 0f) return;
            float ratio = hero.Hp / hero.MaxHp;
            if (inDip)
            {
                if (ratio > NearDeathRecoverRatio) { inDip = false; dipEntryIndex = -1; return; }
                if (dipEntryIndex >= 0 && ratio < Result.Entries[dipEntryIndex].Value)
                {
                    ChronicleEntry entry = Result.Entries[dipEntryIndex]; entry.Value = ratio; Result.Entries[dipEntryIndex] = entry;
                }
                return;
            }
            if (ratio >= NearDeathRatio) return;
            inDip = true; dipEntryIndex = -1;
            if (sim.Time - lastNearDeathTime < NearDeathMinGapSeconds) return;
            DeathCause cause = sim.TouchingEnemyCount() >= sim.Config.Tuning.SurroundedCount ? DeathCause.Surrounded : sim.LastHitCause;
            if (Add(Entry(sim.Time, ChronicleKind.NearDeath, value: ratio, cause: cause)))
            {
                dipEntryIndex = Result.Entries.Count - 1; lastNearDeathTime = sim.Time;
            }
        }

        private void TrackStyle(SurvivorSim sim, SpectatorLabel shownLabel)
        {
            int label = (int)shownLabel;
            if (label >= 0 && label < minuteLabelSeconds.Length) minuteLabelSeconds[label] += sim.LastStepSeconds;
            int minute = (int)MathF.Floor((sim.Time + MinuteEpsilon) / StyleMinuteSeconds);
            if (minute <= completedMinutes) return;
            completedMinutes = minute;
            int best = -1;
            for (int i = 1; i < minuteLabelSeconds.Length; i++)
                if (minuteLabelSeconds[i] > 0f && (best < 0 || minuteLabelSeconds[i] > minuteLabelSeconds[best])) best = i;
            if (best > 0 && minuteLabelSeconds[best] >= StyleMinShare * StyleMinuteSeconds - MinuteEpsilon && (SpectatorLabel)best != lastStyle)
            {
                if (Add(Entry(sim.Time, ChronicleKind.StyleChange, label: (SpectatorLabel)best))) lastStyle = (SpectatorLabel)best;
            }
            Array.Clear(minuteLabelSeconds, 0, minuteLabelSeconds.Length);
        }

        /// <summary>
        /// Appends an entry, keeping at most <see cref="Cap"/>. At the cap: a new StyleChange is dropped;
        /// a new NewItem replaces the latest StyleChange (else it is dropped); any other kind replaces the
        /// latest StyleChange, else the latest NewItem, else it is dropped — except End, which then
        /// replaces the last entry. Returns true when the entry was added.
        /// </summary>
        private bool Add(ChronicleEntry entry)
        {
            List<ChronicleEntry> entries = Result.Entries;
            if (entries.Count < Cap) { entries.Add(entry); return true; }
            if (entry.Kind == ChronicleKind.StyleChange) return false;
            int victim = LastIndexOf(ChronicleKind.StyleChange);
            if (victim < 0 && entry.Kind != ChronicleKind.NewItem) victim = LastIndexOf(ChronicleKind.NewItem);
            if (victim < 0)
            {
                if (entry.Kind != ChronicleKind.End) return false;
                victim = entries.Count - 1;
            }
            entries.RemoveAt(victim);
            if (dipEntryIndex == victim) dipEntryIndex = -1; else if (dipEntryIndex > victim) dipEntryIndex--;
            entries.Add(entry);
            return true;
        }

        private int LastIndexOf(ChronicleKind kind)
        {
            List<ChronicleEntry> entries = Result.Entries;
            for (int i = entries.Count - 1; i >= 0; i--) if (entries[i].Kind == kind) return i;
            return -1;
        }

        private static ChronicleEntry Entry(float time, ChronicleKind kind, int itemIndex = -1, int level = 0, float value = 0f,
            SpectatorLabel label = SpectatorLabel.None, DeathCause cause = DeathCause.None) =>
            new ChronicleEntry { Time = time, Kind = kind, ItemIndex = itemIndex, Level = level, Value = value, Label = label, Cause = cause };
    }
}
