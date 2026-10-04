using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>
    /// Area weapons that hit places rather than fly: strikes (lightning, arrow rain, fireball nova), magi stones, poison pools,
    /// the bomb ring's delayed blasts, purge and the time clock's freeze. All state is preallocated.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        private const float ZoneEpsilon = 1e-4f;
        /// <summary>Poison pool: the densest-group search tests at most this many candidate centres.</summary>
        private const int ZoneCandidateLimit = 64;
        private const float ZoneFallbackDistance = 3f;
        private const float PendingBlastEpsilon = 1e-4f;
        private float bombRingAngle;
        private readonly Vec2[] pendingBlastPosition = new Vec2[SurvivorCatalog.MaxPendingBlasts];
        private readonly float[] pendingBlastTimer = new float[SurvivorCatalog.MaxPendingBlasts];
        private readonly float[] pendingBlastDamage = new float[SurvivorCatalog.MaxPendingBlasts];
        private readonly float[] pendingBlastRadius = new float[SurvivorCatalog.MaxPendingBlasts];
        private readonly float[] pendingBlastKnockback = new float[SurvivorCatalog.MaxPendingBlasts];
        private readonly int[] pendingBlastSource = new int[SurvivorCatalog.MaxPendingBlasts];
        private readonly bool[] pendingBlastActive = new bool[SurvivorCatalog.MaxPendingBlasts];

        /// <summary>Bomb-ring explosions that have been placed but have not landed yet.</summary>
        public int PendingBlastCount { get { int n = 0; for (int i = 0; i < pendingBlastActive.Length; i++) if (pendingBlastActive[i]) n++; return n; } }

        // ---- strike / stone ----

        /// <summary>
        /// Strike: <c>count</c> blasts of radius Width × area. Lightning-style strikes land on random enemies in range;
        /// <see cref="ItemDef.Clustered"/> strikes (arrow rain) land on the densest group in range, each away from the previous ones.
        /// </summary>
        private bool StrikeTargets(ItemDef def, int level, out int count)
        {
            count = Math.Min(VolleyCount(def.CountByLevel, level), volleyTargetIds.Length);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            float radius = def.Width * stats.AreaMul;
            int fired = 0;
            for (int strike = 0; strike < count && !IsEnded; strike++)
            {
                SurvivorEnemy target = def.Clustered ? DensestTarget(fired, def.BaseRange, radius) : RandomTarget(fired, def.BaseRange);
                if (target == null) break;
                Vec2 center = target.Position; // the blast knocks the target away; the event keeps the blast centre
                volleyBlastCenters[fired] = center; volleyTargetIds[fired++] = target.Id;
                Blast(center, radius, damage, def.Knockback, def.StunSeconds);
                AddEvent(SurvivorEventType.StrikeLanded, radius, id: def.CatalogIndex, point: center);
            }
            count = fired;
            return fired > 0;
        }

        /// <summary>
        /// Enemy in range with the most enemies within <paramref name="radius"/> of it, skipping candidates closer than 1.5 radius to
        /// an earlier blast of this volley (<c>volleyBlastCenters</c>). At most <see cref="DensestSamples"/> evenly
        /// spaced candidates are scored, so the cost stays bounded in a full horde. Allocation-free and deterministic.
        /// </summary>
        private SurvivorEnemy DensestTarget(int usedCount, float range, float radius)
        {
            float rangeSquared = range * range, spacingSquared = radius * radius * 2.25f, radiusSquared = radius * radius;
            int total = 0;
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy e = enemies[i];
                if (!e.Active || (e.Position - Hero.Position).LengthSquared > rangeSquared || NearEarlierBlast(e.Position, usedCount, spacingSquared)) continue;
                enemyScratch[total++] = i;
            }
            if (total == 0) return null;
            int step = Math.Max(1, total / DensestSamples);
            SurvivorEnemy best = null; int bestCount = -1;
            for (int n = 0; n < total; n += step)
            {
                SurvivorEnemy candidate = enemies[enemyScratch[n]]; int count = 0;
                for (int i = 0; i < enemyLimit; i++)
                {
                    SurvivorEnemy e = enemies[i];
                    if (e.Active && (e.Position - candidate.Position).LengthSquared <= radiusSquared) count++;
                }
                if (count > bestCount) { bestCount = count; best = candidate; }
            }
            return best;
        }

        private const int DensestSamples = 16;

        private bool NearEarlierBlast(Vec2 point, int usedCount, float spacingSquared)
        {
            for (int k = 0; k < usedCount; k++) if ((volleyBlastCenters[k] - point).LengthSquared < spacingSquared) return true;
            return false;
        }

        /// <summary>Stone: instant hits on the <c>count</c> nearest distinct enemies in range for fixed damage (flat: no Might, no crit, no rng).</summary>
        private bool StrikeStones(ItemDef def, int level, out int count)
        {
            count = Math.Min(VolleyCount(def.CountByLevel, level), volleyTargetIds.Length);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            int fired = 0;
            for (int stone = 0; stone < count && !IsEnded; stone++)
            {
                SurvivorEnemy target = null; float nearest = float.PositiveInfinity;
                for (int i = 0; i < enemyLimit; i++)
                {
                    SurvivorEnemy enemy = enemies[i]; if (!enemy.Active || UsedVolleyTarget(enemy.Id, fired)) continue;
                    float distanceSquared = (enemy.Position - Hero.Position).LengthSquared; float reach = def.BaseRange + enemy.Radius;
                    if (distanceSquared <= reach * reach && distanceSquared < nearest) { nearest = distanceSquared; target = enemy; }
                }
                if (target == null) break;
                volleyTargetIds[fired++] = target.Id;
                AddEvent(SurvivorEventType.StrikeLanded, 0f, id: def.CatalogIndex, point: target.Position);
                DamageEnemy(target, damage, def.Knockback, AwayFromHero(target), true);
            }
            count = fired;
            return fired > 0;
        }

        // ---- poison pool ----

        /// <summary>
        /// Drops a zone on the enemy (within BaseRange) with the most enemies inside one zone radius around it (first in pool order on a tie);
        /// with nobody in range it lands 3 m from the hero at a random angle. Skipped when all three zones are alive.
        /// </summary>
        private bool DropZone(ItemDef def, int level, out Vec2 point)
        {
            point = Vec2.Zero;
            SurvivorZone zone = null;
            for (int i = 0; i < zones.Length; i++) if (!zones[i].Active) { zone = zones[i]; break; }
            if (zone == null) return false;
            float radius = def.Width * stats.AreaMul;
            float rangeSquared = def.BaseRange * def.BaseRange;
            int total = 0;
            for (int i = 0; i < enemyLimit; i++)
            {
                if (enemies[i].Active && (enemies[i].Position - Hero.Position).LengthSquared <= rangeSquared) enemyScratch[total++] = i;
            }
            if (total == 0)
            {
                point = Hero.Position + Vec2.FromAngle(rng.Range(0f, MathF.PI * 2f)) * ZoneFallbackDistance;
                float limit = Config.MapHalfSize - radius;
                point = new Vec2(MathF.Max(-limit, MathF.Min(limit, point.X)), MathF.Max(-limit, MathF.Min(limit, point.Y)));
            }
            else
            {
                int stride = (total + ZoneCandidateLimit - 1) / ZoneCandidateLimit, best = -1, bestCount = 0;
                for (int a = 0; a < total; a += stride)
                {
                    Vec2 center = enemies[enemyScratch[a]].Position; int inside = 0;
                    for (int b = 0; b < total; b++)
                    {
                        SurvivorEnemy other = enemies[enemyScratch[b]]; float reach = radius + other.Radius;
                        if ((other.Position - center).LengthSquared <= reach * reach) inside++;
                    }
                    if (inside > bestCount) { bestCount = inside; best = a; }
                }
                point = enemies[enemyScratch[best]].Position;
            }
            zone.Active = true; zone.Position = point; zone.Radius = radius; zone.Duration = def.Duration * stats.DurationMul; zone.Remaining = zone.Duration;
            zone.Damage = def.BaseDamage + def.DamagePerLevel * (level - 1); zone.TickTimer = def.HitInterval; zone.SourceIndex = def.CatalogIndex;
            return true;
        }

        private void UpdateZones()
        {
            for (int i = 0; i < zones.Length; i++)
            {
                SurvivorZone z = zones[i]; if (!z.Active) continue;
                ItemDef def = SurvivorCatalog.Get(z.SourceIndex);
                z.Remaining -= FixedDeltaTime; z.TickTimer -= FixedDeltaTime;
                if (z.TickTimer <= ZoneEpsilon)
                {
                    z.TickTimer += def.HitInterval;
                    Blast(z.Position, z.Radius, z.Damage, def.Knockback, 0f);
                    if (IsEnded) return;
                }
                if (z.Remaining <= ZoneEpsilon) z.Active = false;
            }
        }

        // ---- bomb ring ----

        /// <summary>
        /// Queues N blasts on a ring of radius BaseRange × area around the hero, one every HitInterval; the ring turns by a fixed step per salvo.
        /// Needs an enemy within ring radius + blast radius and N free slots (else nothing happens and the cooldown is kept).
        /// </summary>
        private bool StartBombRing(ItemDef def, int level, out int count)
        {
            count = Math.Min(VolleyCount(def.CountByLevel, level), pendingBlastActive.Length);
            float ring = def.BaseRange * stats.AreaMul, blast = def.Width * stats.AreaMul;
            if (!HasEnemyInRange(ring + blast)) return false;
            int free = 0;
            for (int i = 0; i < pendingBlastActive.Length; i++) if (!pendingBlastActive[i]) free++;
            if (free < count) return false;
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            float limit = Config.MapHalfSize - blast;
            int slot = 0;
            for (int k = 0; k < count; k++)
            {
                while (pendingBlastActive[slot]) slot++;
                Vec2 point = Hero.Position + Vec2.FromAngle(bombRingAngle + k * MathF.PI * 2f / count) * ring;
                pendingBlastPosition[slot] = new Vec2(MathF.Max(-limit, MathF.Min(limit, point.X)), MathF.Max(-limit, MathF.Min(limit, point.Y)));
                pendingBlastTimer[slot] = k * def.HitInterval; pendingBlastDamage[slot] = damage; pendingBlastRadius[slot] = blast; pendingBlastKnockback[slot] = def.Knockback; pendingBlastSource[slot] = def.CatalogIndex; pendingBlastActive[slot] = true;
            }
            bombRingAngle = WrapAngle(bombRingAngle + SurvivorCatalog.BombRingRotationStep);
            return true;
        }

        private void UpdatePendingBlasts()
        {
            for (int i = 0; i < pendingBlastActive.Length; i++)
            {
                if (!pendingBlastActive[i]) continue;
                pendingBlastTimer[i] -= FixedDeltaTime;
                if (pendingBlastTimer[i] > PendingBlastEpsilon) continue;
                pendingBlastActive[i] = false;
                AddEvent(SurvivorEventType.StrikeLanded, pendingBlastRadius[i], id: pendingBlastSource[i], point: pendingBlastPosition[i]);
                Blast(pendingBlastPosition[i], pendingBlastRadius[i], pendingBlastDamage[i], pendingBlastKnockback[i], 0f);
                if (IsEnded) return;
            }
        }

        // ---- purge ----

        /// <summary>Kills every normal enemy within BaseRange × area (normal kills: drops, XP, events); elites and the boss lose 5% of max HP.</summary>
        private bool Purge(ItemDef def, int level, out int kills)
        {
            kills = 0;
            float radius = def.BaseRange * stats.AreaMul;
            if (!HasEnemyInRange(radius)) return false;
            AddEvent(SurvivorEventType.StrikeLanded, radius, id: def.CatalogIndex, point: Hero.Position);
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy e = enemies[i]; if (!e.Active) continue;
                float reach = radius + e.Radius;
                if ((e.Position - Hero.Position).LengthSquared > reach * reach) continue;
                if (e.IsBoss || e.Elite) DamageEnemy(e, e.MaxHp * SurvivorCatalog.PurgeBossFraction, 0f, Vec2.Zero, true);
                else { DamageEnemy(e, e.Hp, 0f, Vec2.Zero, true); kills++; }
            }
            return true;
        }

        // ---- time clock ----

        /// <summary>When ready and an enemy is within range: spends the cooldown and, with the weapon's chance, stuns every non-boss enemy in range (elites for half the time).</summary>
        private void UpdateFreeze(ItemDef def, int level)
        {
            if (weaponCooldowns[def.CatalogIndex] > 0f || !HasEnemyInRange(def.BaseRange)) return;
            weaponCooldowns[def.CatalogIndex] = (def.BaseCooldown + def.CooldownPerLevel * (level - 1)) * stats.CooldownMul;
            float chance = MathF.Min(1f, def.Chance + def.ChancePerLevel * (level - 1));
            if (rng.NextFloat() >= chance) return;
            float seconds = (def.StunSeconds + def.StunPerLevel * (level - 1)) * stats.DurationMul;
            int stunned = 0;
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy e = enemies[i]; if (!e.Active || e.IsBoss) continue;
                float reach = def.BaseRange + e.Radius;
                if ((e.Position - Hero.Position).LengthSquared > reach * reach) continue;
                Stun(e, e.Elite ? seconds * SurvivorCatalog.FreezeEliteShare : seconds); stunned++;
            }
            AddEvent(SurvivorEventType.WeaponFired, seconds, stunned, def.CatalogIndex, Hero.Position);
        }
    }
}
