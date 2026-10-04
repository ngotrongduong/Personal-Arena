using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>What the hero is visibly doing, for the spectator overlay (GDD §5). Computed from sim data only.</summary>
    public enum SpectatorLabel { None, Kiting, Looting, Charging, Escaping }

    public static class SpectatorLabels
    {
        public const int Count = 5;

        public static string DisplayName(SpectatorLabel label)
        {
            switch (label)
            {
                case SpectatorLabel.Kiting: return "KITING";
                case SpectatorLabel.Looting: return "LOOTING GOLD";
                case SpectatorLabel.Charging: return "CHARGING IN";
                case SpectatorLabel.Escaping: return "BREAKING OUT";
                default: return string.Empty;
            }
        }
    }

    /// <summary>
    /// Honest spectator label. Call <see cref="Observe"/> after each <see cref="SurvivorSim.Step"/> with the
    /// sim time that passed (normally <see cref="SurvivorSim.LastStepSeconds"/>; 0 while a level-up pick
    /// is open, which changes nothing). The raw label is recomputed every tick; the shown label
    /// (<see cref="Current"/>) follows it with hysteresis. Allocation-free after construction.
    /// </summary>
    public sealed class SpectatorLabeler
    {
        /// <summary>The hero counts as moving when its speed is above this fraction of its max move speed.</summary>
        public const float MovingFraction = 0.3f;
        /// <summary>Enemies within this distance form the group (centroid C, count n10) (m).</summary>
        public const float GroupRadius = 10f;
        /// <summary>Enemies within this distance count as close (n3) (m).</summary>
        public const float CloseRadius = 3f;
        /// <summary>Escaping when at least this many enemies are close.</summary>
        public const int EscapeCloseCount = 4;
        /// <summary>…or when HP is below this ratio and the nearest enemy is within DangerDistance.</summary>
        public const float LowHpRatio = 0.35f;
        public const float DangerDistance = 4f;
        /// <summary>Looting considers the nearest gold/chest/magnet/meat pickup within this distance (m).</summary>
        public const float LootRadius = 6f;
        /// <summary>Cosine of the 45° looting cone.</summary>
        public const float LootCos = 0.70710677f;
        /// <summary>Charging when dot(u, w) is above this, with at least MinGroupCount enemies in the group.</summary>
        public const float ChargeDot = 0.5f;
        /// <summary>Kiting when dot(u, w) is below this, the group is big enough and the hero dealt damage recently.</summary>
        public const float KiteDot = -0.3f;
        public const float KiteDamageWindow = 1f;
        public const int MinGroupCount = 3;
        /// <summary>The raw label must stay the same this long before the shown label changes (s).</summary>
        public const float HoldSeconds = 0.5f;
        /// <summary>A shown label other than None stays at least this long (s).</summary>
        public const float MinShowSeconds = 1f;
        /// <summary>Tolerance for summed fixed ticks (30 × 1/60 is slightly below 0.5 in float).</summary>
        private const float TimeEpsilon = 1e-4f;

        private readonly float[] secondsIn = new float[SpectatorLabels.Count];
        private SpectatorLabel pendingRaw;
        private float pendingSeconds;
        private float shownSeconds;
        private float lastDamageTime;

        public SpectatorLabeler() { Reset(); }

        /// <summary>The label shown to the viewer.</summary>
        public SpectatorLabel Current { get; private set; }
        /// <summary>The label computed from the last observed tick, before hysteresis.</summary>
        public SpectatorLabel Raw { get; private set; }
        /// <summary>Sim time observed since <see cref="Reset"/>.</summary>
        public float TotalSeconds { get; private set; }

        /// <summary>Time the shown label was <paramref name="label"/> since <see cref="Reset"/> (s).</summary>
        public float SecondsIn(SpectatorLabel label)
        {
            int index = (int)label;
            return index >= 0 && index < secondsIn.Length ? secondsIn[index] : 0f;
        }

        public void Reset()
        {
            Array.Clear(secondsIn, 0, secondsIn.Length);
            Current = SpectatorLabel.None; Raw = SpectatorLabel.None; pendingRaw = SpectatorLabel.None;
            pendingSeconds = 0f; shownSeconds = 0f; TotalSeconds = 0f; lastDamageTime = float.NegativeInfinity;
        }

        public void Observe(SurvivorSim sim, float dt)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            var events = sim.Events;
            for (int i = 0; i < events.Count; i++) if (events[i].Type == SurvivorEventType.DamageDealt) { lastDamageTime = sim.Time; break; }
            if (dt <= 0f) return;
            ObserveRaw(ComputeRaw(sim), dt);
        }

        /// <summary>Applies the hysteresis to one raw label lasting <paramref name="dt"/> seconds.</summary>
        internal void ObserveRaw(SpectatorLabel raw, float dt)
        {
            Raw = raw;
            if (raw == pendingRaw) pendingSeconds += dt; else { pendingRaw = raw; pendingSeconds = dt; }
            shownSeconds += dt;
            if (raw != Current && pendingSeconds >= HoldSeconds - TimeEpsilon && (Current == SpectatorLabel.None || shownSeconds >= MinShowSeconds - TimeEpsilon))
            {
                Current = raw; shownSeconds = 0f;
            }
            secondsIn[(int)Current] += dt;
            TotalSeconds += dt;
        }

        /// <summary>The raw label of the sim's current state (rules 1–5 of the T-023 spec, in priority order).</summary>
        internal SpectatorLabel ComputeRaw(SurvivorSim sim)
        {
            SurvivorHero hero = sim.Hero;
            Vec2 heroPosition = hero.Position;
            Vec2 velocity = hero.Velocity;
            float speed = velocity.Length;
            bool moving = speed > MovingFraction * sim.DerivedStats.MoveSpeed && speed > 1e-6f;
            if (!moving) return SpectatorLabel.None;
            Vec2 u = velocity / speed;

            SurvivorEnemy[] enemies = sim.EnemyPool;
            int limit = sim.EnemyLimit;
            int n10 = 0, n3 = 0;
            float sumX = 0f, sumY = 0f;
            float nearestSquared = float.PositiveInfinity;
            float groupSquared = GroupRadius * GroupRadius, closeSquared = CloseRadius * CloseRadius;
            for (int i = 0; i < limit; i++)
            {
                SurvivorEnemy e = enemies[i];
                if (!e.Active) continue;
                Vec2 delta = e.Position - heroPosition;
                float distanceSquared = delta.LengthSquared;
                if (distanceSquared < nearestSquared) nearestSquared = distanceSquared;
                if (distanceSquared > groupSquared) continue;
                n10++; sumX += e.Position.X; sumY += e.Position.Y;
                if (distanceSquared <= closeSquared) n3++;
            }
            float dotGroup = 0f;
            if (n10 > 0)
            {
                Vec2 toCentroid = new Vec2(sumX / n10, sumY / n10) - heroPosition;
                float length = toCentroid.Length;
                if (length > 1e-6f) dotGroup = Vec2.Dot(u, toCentroid / length);
            }

            float hpRatio = hero.MaxHp > 0f ? hero.Hp / hero.MaxHp : 0f;
            bool danger = n3 >= EscapeCloseCount || (hpRatio < LowHpRatio && nearestSquared < DangerDistance * DangerDistance);
            if (danger && dotGroup < 0f) return SpectatorLabel.Escaping;

            SurvivorPickup[] pickups = sim.PickupPool;
            int pickupLimit = sim.PickupLimit;
            float bestSquared = LootRadius * LootRadius;
            Vec2 bestDelta = Vec2.Zero; bool found = false;
            for (int i = 0; i < pickupLimit; i++)
            {
                SurvivorPickup p = pickups[i];
                if (!p.Active || !IsLoot(p.Kind)) continue;
                Vec2 delta = p.Position - heroPosition;
                float distanceSquared = delta.LengthSquared;
                if (distanceSquared <= bestSquared) { bestSquared = distanceSquared; bestDelta = delta; found = true; }
            }
            if (found)
            {
                float length = bestDelta.Length;
                if (length > 1e-6f && Vec2.Dot(u, bestDelta / length) >= LootCos) return SpectatorLabel.Looting;
            }

            if (n10 >= MinGroupCount && dotGroup > ChargeDot) return SpectatorLabel.Charging;
            if (n10 >= MinGroupCount && dotGroup < KiteDot && sim.Time - lastDamageTime <= KiteDamageWindow + TimeEpsilon) return SpectatorLabel.Kiting;
            return SpectatorLabel.None;
        }

        private static bool IsLoot(PickupKind kind) =>
            kind != PickupKind.None && kind != PickupKind.Gem;
    }
}
