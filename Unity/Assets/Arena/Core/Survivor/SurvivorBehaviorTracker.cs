using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed class SurvivorBehaviorStats
    {
        public float Aggression { get; set; } = -1f;
        public float Caution { get; set; } = -1f;
        public float Greed { get; set; } = -1f;
        public float Exploration { get; set; } = -1f;
        public float CrowdControl { get; set; } = -1f;
        public float BossHunting { get; set; } = -1f;
        public float SkillDiscipline { get; set; } = -1f;
        public float PreferredRange { get; set; } = -1f;
        public float KeepDistance { get; set; } = -1f;
        public int KickUses { get; set; }
        public int BlockUses { get; set; }
        public int DashUses { get; set; }
        public int EffectiveKicks { get; set; }
        public int EffectiveBlocks { get; set; }
        public int EffectiveDashes { get; set; }
    }

    /// <summary>Read-only behavior telemetry sampled alongside a Survivor run.</summary>
    public sealed class SurvivorBehaviorTracker
    {
        private const int RangeBins = 48;
        private const float RangeLimit = 12f;
        private const float ExplorationCellSize = 5f;
        private const int DashDelayTicks = 30;
        private const int MaxPendingDashes = 4096;

        private readonly int[] rangeHistogram = new int[RangeBins];
        private readonly int[] dashDueTicks = new int[MaxPendingDashes];
        private readonly int[] dashStartNearby = new int[MaxPendingDashes];
        private bool[] exploredCells = Array.Empty<bool>();
        private int mapCellsPerSide;
        private int simulatedTicks;
        private int sampledTicks;
        private int exploredCount;
        private int nearestSamples;
        private double nearestDistanceTotal;
        private int aggressionSamples;
        private int aggressiveSamples;
        private int cautionSamples;
        private int cautiousSamples;
        private int bossSamples;
        private int bossHuntingSamples;
        private int kickUses;
        private int crowdControlKicks;
        private int effectiveKicks;
        private int blockUses;
        private int effectiveBlocks;
        private bool blockActive;
        private bool blockEffective;
        private int dashHead;
        private int dashTail;
        private int dashUses;
        private int effectiveDashes;
        private int kickSlot;
        private int blockSlot;
        private int dashSlot;

        public void Reset(SurvivorSim sim)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            Array.Clear(rangeHistogram, 0, rangeHistogram.Length);
            mapCellsPerSide = Math.Max(1, (int)MathF.Ceiling(sim.Config.MapHalfSize * 2f / ExplorationCellSize));
            int cellCount = mapCellsPerSide * mapCellsPerSide;
            if (exploredCells.Length != cellCount) exploredCells = new bool[cellCount];
            else Array.Clear(exploredCells, 0, exploredCells.Length);
            simulatedTicks = 0; sampledTicks = 0; exploredCount = 0; nearestSamples = 0; nearestDistanceTotal = 0.0;
            aggressionSamples = 0; aggressiveSamples = 0; cautionSamples = 0; cautiousSamples = 0;
            bossSamples = 0; bossHuntingSamples = 0; kickUses = 0; crowdControlKicks = 0; effectiveKicks = 0;
            blockUses = 0; effectiveBlocks = 0; blockActive = false; blockEffective = false;
            dashHead = 0; dashTail = 0; dashUses = 0; effectiveDashes = 0;
            kickSlot = FindSkillSlot(sim, SkillKind.Kick); blockSlot = FindSkillSlot(sim, SkillKind.Block); dashSlot = FindSkillSlot(sim, SkillKind.Dash);
        }

        public void Observe(SurvivorSim sim)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            bool blockedThisTick = false;
            for (int i = 0; i < sim.Events.Count; i++)
            {
                SurvivorEvent survivorEvent = sim.Events[i];
                if (survivorEvent.Type == SurvivorEventType.Blocked || survivorEvent.Type == SurvivorEventType.Parry)
                {
                    blockedThisTick = true;
                }
                if (survivorEvent.Type != SurvivorEventType.SkillUsed) continue;
                int slot = (int)survivorEvent.Value;
                if (slot == kickSlot)
                {
                    kickUses++;
                    if (survivorEvent.Extra >= 1f) effectiveKicks++;
                    if (survivorEvent.Extra >= 3f) crowdControlKicks++;
                }
                else if (slot == blockSlot)
                {
                    if (blockActive) FinishBlock();
                    blockUses++; blockActive = true; blockEffective = false;
                }
                else if (slot == dashSlot && dashTail < MaxPendingDashes)
                {
                    dashDueTicks[dashTail] = simulatedTicks + 1 + DashDelayTicks;
                    dashStartNearby[dashTail] = CountNearbyEnemies(sim, 3f);
                    dashTail++;
                }
            }
            if (blockActive && blockedThisTick) blockEffective = true;
            if (blockActive && !sim.Hero.Blocking) FinishBlock();

            if (sim.LastStepSeconds <= 0f) return;
            simulatedTicks++;
            CheckDashes(sim);
            if (simulatedTicks % 5 == 0) SamplePosition(sim);
        }

        public SurvivorBehaviorStats Finish(SurvivorSim sim)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (blockActive) FinishBlock();
            int effective = effectiveKicks + effectiveBlocks + effectiveDashes;
            int uses = kickUses + blockUses + dashUses;
            return new SurvivorBehaviorStats
            {
                Aggression = Ratio(aggressiveSamples, aggressionSamples),
                Caution = Ratio(cautiousSamples, cautionSamples),
                Greed = sim.DropsSpawned > 0 ? (float)sim.DropsCollected / sim.DropsSpawned : -1f,
                Exploration = sampledTicks > 0 && exploredCells.Length > 0 ? (float)exploredCount / exploredCells.Length : -1f,
                CrowdControl = Ratio(crowdControlKicks, kickUses),
                BossHunting = Ratio(bossHuntingSamples, bossSamples),
                SkillDiscipline = Ratio(effective, uses),
                PreferredRange = HistogramMedian(),
                KeepDistance = nearestSamples > 0 ? (float)(nearestDistanceTotal / nearestSamples) : -1f,
                KickUses = kickUses, BlockUses = blockUses, DashUses = dashUses,
                EffectiveKicks = effectiveKicks, EffectiveBlocks = effectiveBlocks, EffectiveDashes = effectiveDashes
            };
        }

        private void SamplePosition(SurvivorSim sim)
        {
            sampledTicks++;
            int cellX = Math.Min(mapCellsPerSide - 1, Math.Max(0, (int)((sim.Hero.Position.X + sim.Config.MapHalfSize) / ExplorationCellSize)));
            int cellY = Math.Min(mapCellsPerSide - 1, Math.Max(0, (int)((sim.Hero.Position.Y + sim.Config.MapHalfSize) / ExplorationCellSize)));
            int cell = cellY * mapCellsPerSide + cellX;
            if (!exploredCells[cell]) { exploredCells[cell] = true; exploredCount++; }

            float nearestSquared = float.MaxValue;
            Vec2 nearestDirection = Vec2.Zero;
            Vec2 centroid = Vec2.Zero;
            int nearby = 0;
            for (int i = 0; i < sim.Enemies.Count; i++)
            {
                SurvivorEnemy enemy = sim.Enemies[i];
                if (!enemy.Active) continue;
                Vec2 direction = enemy.Position - sim.Hero.Position;
                float squared = direction.LengthSquared;
                if (squared < nearestSquared) { nearestSquared = squared; nearestDirection = direction; }
                if (squared <= RangeLimit * RangeLimit) { centroid += enemy.Position; nearby++; }
            }
            if (nearestSquared < float.MaxValue)
            {
                float distance = MathF.Sqrt(nearestSquared);
                nearestSamples++; nearestDistanceTotal += distance;
                int bin = Math.Min(RangeBins - 1, (int)(distance / RangeLimit * RangeBins));
                rangeHistogram[bin]++;
            }

            bool moved = sim.LastMove != 0 && sim.Hero.Velocity.LengthSquared > 0.0001f;
            if (!moved) return;
            Vec2 movement = sim.Hero.Velocity.Normalized();
            if (nearby > 0)
            {
                aggressionSamples++;
                Vec2 towardCentroid = centroid / nearby - sim.Hero.Position;
                if (towardCentroid.LengthSquared > 0.0001f && Vec2.Dot(movement, towardCentroid.Normalized()) > 0.5f) aggressiveSamples++;
            }
            if (sim.Hero.MaxHp > 0f && sim.Hero.Hp / sim.Hero.MaxHp < 0.35f && nearestSquared < 36f)
            {
                cautionSamples++;
                if (nearestDirection.LengthSquared > 0.0001f && Vec2.Dot(movement, nearestDirection.Normalized()) < -0.5f) cautiousSamples++;
            }
            SurvivorEnemy boss = sim.BossEnemy;
            if (boss != null)
            {
                Vec2 towardBoss = boss.Position - sim.Hero.Position;
                if (towardBoss.LengthSquared <= 225f)
                {
                    bossSamples++;
                    if (towardBoss.LengthSquared > 0.0001f && Vec2.Dot(movement, towardBoss.Normalized()) > 0.5f) bossHuntingSamples++;
                }
            }
        }

        private void CheckDashes(SurvivorSim sim)
        {
            while (dashHead < dashTail && dashDueTicks[dashHead] <= simulatedTicks)
            {
                int startNearby = dashStartNearby[dashHead++];
                int nowNearby = CountNearbyEnemies(sim, 3f);
                dashUses++;
                if (startNearby > 0 && nowNearby < startNearby) effectiveDashes++;
            }
            if (dashHead == dashTail) { dashHead = 0; dashTail = 0; }
        }

        private void FinishBlock()
        {
            if (blockEffective) effectiveBlocks++;
            blockActive = false; blockEffective = false;
        }

        private float HistogramMedian()
        {
            if (nearestSamples == 0) return -1f;
            int target = (nearestSamples + 1) / 2;
            int cumulative = 0;
            for (int i = 0; i < rangeHistogram.Length; i++)
            {
                cumulative += rangeHistogram[i];
                if (cumulative >= target) return (i + 0.5f) * RangeLimit / RangeBins;
            }
            return RangeLimit - RangeLimit / RangeBins * 0.5f;
        }

        private static int FindSkillSlot(SurvivorSim sim, SkillKind kind)
        {
            for (int i = 0; i < sim.Config.ClassDef.ActiveSkills.Length; i++)
                if (sim.Config.ClassDef.ActiveSkills[i].Kind == kind) return i;
            return -1;
        }

        private static int CountNearbyEnemies(SurvivorSim sim, float range)
        {
            int count = 0; float squaredRange = range * range;
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (sim.Enemies[i].Active && (sim.Enemies[i].Position - sim.Hero.Position).LengthSquared <= squaredRange) count++;
            return count;
        }

        private static float Ratio(int numerator, int denominator) => denominator > 0 ? (float)numerator / denominator : -1f;
    }
}
