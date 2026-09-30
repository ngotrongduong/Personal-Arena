using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed partial class SurvivorSim
    {
        private void SpawnOpeningRing()
        {
            int count = Config.OpeningRing;
            if (count <= 0) return;
            const float distance = 6f;
            float bodyRadius = SpawnRadius(0, false);
            float edge = Config.MapHalfSize - bodyRadius;
            float clearance = bodyRadius + Config.Tuning.SpawnObstacleMargin;
            float step = MathF.PI * 2f / count;
            for (int i = 0; i < count; i++)
            {
                Vec2 point = Hero.Position + Vec2.FromAngle(i * step) * distance;
                if (MathF.Abs(point.X) > edge || MathF.Abs(point.Y) > edge) continue;
                if (OverlapsObstacle(point, clearance)) continue;
                SpawnEnemy(0, point, false, false);
            }
        }

        /// <summary>
        /// Spawns scheduled enemies. The accumulator is drained by 1 per pending spawn even when the
        /// max-alive cap blocks it, so spawns are dropped (not banked) while the cap is reached.
        /// </summary>
        private void SpawnScheduledEnemies()
        {
            SurvivorTuning tuning = Config.Tuning;
            if (Time >= tuning.BossSpawnSeconds) return;
            while (nextEliteIndex < tuning.EliteCount && Time >= tuning.EliteIntervalSeconds * (nextEliteIndex + 1))
            {
                SpawnElite(); nextEliteIndex++;
            }
            SpawnPhase phase = SurvivorDefaults.PhaseAt(Time);
            if (phase == null) return;
            float scale = 1f + tuning.SpawnPerTier * (Config.Build.Tier - 1);
            int max = Math.Min(EnemyCapacity - tuning.SummonReserve, (int)MathF.Floor(phase.MaxAlive * scale));
            spawnAccumulator += phase.SpawnsPerSecond * scale * FixedDeltaTime;
            while (spawnAccumulator >= 1f)
            {
                spawnAccumulator -= 1f;
                if (aliveNormalCount >= max) continue;
                int type = WeightedType(phase);
                if (TrySpawnPoint(tuning.SpawnAttempts, false, SpawnRadius(type, false), out Vec2 point)) SpawnEnemy(type, point, false, false);
            }
        }

        private int WeightedType(SpawnPhase phase)
        {
            int total = 0;
            for (int i = 0; i < phase.Weights.Count; i++) total += phase.Weights[i];
            int roll = rng.NextInt(total);
            for (int i = 0; i < phase.Weights.Count; i++) { if (roll < phase.Weights[i]) return i; roll -= phase.Weights[i]; }
            return 0;
        }

        private float SpawnRadius(int type, bool elite)
        {
            SurvivorEnemyDef def = SurvivorDefaults.EnemyDef(type);
            if (def == null) return 0f;
            return def.Radius * (elite ? Config.Tuning.EliteRadiusMul : 1f);
        }

        private void SpawnElite()
        {
            SpawnPhase phase = SurvivorDefaults.PhaseAt(MathF.Min(Time, Config.Tuning.BossSpawnSeconds - 0.1f));
            int best = 0;
            for (int i = 1; i < phase.Weights.Count; i++) if (phase.Weights[i] >= phase.Weights[best]) best = i;
            float radius = SpawnRadius(best, true);
            if (!TrySpawnPoint(Config.Tuning.EliteSpawnAttempts, false, radius, out Vec2 point)) point = NearestValidRingPoint(radius);
            SpawnEnemy(best, point, true, false);
        }

        /// <summary>Returns false when the pool had no room; the caller retries on the next tick.</summary>
        private bool SpawnBoss()
        {
            float radius = SpawnRadius(BossTypeIndex, false);
            if (!TrySpawnPoint(Config.Tuning.EliteSpawnAttempts, false, radius, out Vec2 point)) point = NearestValidRingPoint(radius);
            if (SpawnEnemy(BossTypeIndex, point, false, true) == null) return false;
            bossSpawned = true;
            return true;
        }

        private SurvivorEnemy SpawnEnemy(int type, Vec2 point, bool elite, bool boss)
        {
            if (IsEnded) return null;
            SurvivorEnemyDef def = SurvivorDefaults.EnemyDef(type);
            if (def == null) return null;
            int slot = -1;
            for (int i = 0; i < enemyLimit; i++) if (!enemies[i].Active) { slot = i; break; }
            if (slot < 0 && enemyLimit < enemies.Length) { slot = enemyLimit; enemyLimit++; }
            if (slot < 0) return null;
            SurvivorTuning tuning = Config.Tuning;
            SurvivorEnemy e = enemies[slot];
            float hpTime = boss ? 1f : 1f + tuning.HpPerMinute * Time / 60f;
            float damageTime = boss ? 1f : 1f + tuning.DamagePerMinute * Time / 60f;
            float hpTier = 1f + tuning.HpPerTier * (Config.Build.Tier - 1);
            float damageTier = 1f + tuning.DamagePerTier * (Config.Build.Tier - 1);
            e.Active = true; e.Id = nextEnemyId++; e.TypeIndex = type; e.Position = point; e.Velocity = Vec2.Zero;
            e.Facing = (Hero.Position - point).Angle(); e.Radius = def.Radius * (elite ? tuning.EliteRadiusMul : 1f);
            e.Mass = def.Mass * (elite ? tuning.EliteMassMul : 1f); e.MaxHp = def.BaseHp * hpTime * hpTier * (elite ? tuning.EliteHpMul : 1f);
            e.Hp = e.MaxHp; e.Damage = def.AttackDamage * damageTime * damageTier * (elite ? tuning.EliteDamageMul : 1f);
            e.KnockbackResist = elite ? MathF.Max(def.KnockbackResist, tuning.EliteMinKnockbackResist) : def.KnockbackResist;
            e.Elite = elite; e.IsBoss = boss; e.WindupRemaining = 0f; e.StunRemaining = 0f;
            e.RelocatedThisTick = false; e.Separated = false; enemyPreviousPositions[slot] = point;
            e.AttackCooldown = 0f; e.ContactCooldown = 0f; e.SummonCooldown = boss ? tuning.BossSummonIntervalSeconds : 0f;
            if (e.Radius > maxEnemyRadius) maxEnemyRadius = e.Radius;
            if (boss) { bossEnemy = e; bossSpawned = true; } else aliveNormalCount++;
            aliveEnemyCount++;
            AddEvent(boss ? SurvivorEventType.BossSpawned : elite ? SurvivorEventType.EliteSpawned : SurvivorEventType.EnemySpawned, id: e.Id, point: point);
            return e;
        }

        private bool TrySpawnPoint(int attempts, bool inFront, float bodyRadius, out Vec2 point)
        {
            SurvivorTuning tuning = Config.Tuning;
            float edge = Config.MapHalfSize - tuning.SpawnMapMargin;
            float clearance = bodyRadius + tuning.SpawnObstacleMargin;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                float angle;
                if (inFront && Hero.Velocity.LengthSquared > 0.01f) angle = Hero.Velocity.Angle() + rng.Range(-tuning.RelocationHalfArc, tuning.RelocationHalfArc);
                else angle = rng.Range(-MathF.PI, MathF.PI);
                point = Hero.Position + Vec2.FromAngle(angle) * rng.Range(tuning.SpawnRingMin, tuning.SpawnRingMax);
                if (MathF.Abs(point.X) > edge || MathF.Abs(point.Y) > edge) continue;
                if (!OverlapsObstacle(point, clearance)) return true;
            }
            point = Vec2.Zero; return false;
        }

        private Vec2 NearestValidRingPoint(float bodyRadius)
        {
            Vec2 point = Hero.Position + Vec2.FromAngle(Hero.Facing) * Config.Tuning.SpawnRingMin;
            ClampAndPushOut(ref point, bodyRadius + Config.Tuning.SpawnObstacleMargin);
            return point;
        }

        private void UpdateEnemies()
        {
            SurvivorTuning tuning = Config.Tuning;
            float relocationSquared = tuning.RelocationDistance * tuning.RelocationDistance;
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy e = enemies[i];
                if (!e.Active) continue;
                SurvivorEnemyDef def = SurvivorDefaults.EnemyDef(e.TypeIndex);
                e.ContactCooldown = MathF.Max(0f, e.ContactCooldown - FixedDeltaTime);
                e.AttackCooldown = MathF.Max(0f, e.AttackCooldown - FixedDeltaTime);
                if (e.StunRemaining > 0f)
                {
                    e.StunRemaining = MathF.Max(0f, e.StunRemaining - FixedDeltaTime);
                    e.WindupRemaining = 0f;
                }
                else if (e.WindupRemaining > 0f)
                {
                    e.WindupRemaining -= FixedDeltaTime;
                    if (e.WindupRemaining <= 0f) ResolveSwing(e, def);
                }
                else
                {
                    Vec2 toHero = Hero.Position - e.Position;
                    float targetFacing = toHero.Angle();
                    float maxTurn = def.TurnSpeedDegPerSec * MathF.PI / 180f * FixedDeltaTime;
                    bool fullyTurned = MathF.Abs(WrapAngle(targetFacing - e.Facing)) <= maxTurn;
                    e.Facing = TurnToward(e.Facing, targetFacing, maxTurn);
                    Vec2 moveDirection = fullyTurned ? toHero.Normalized() : Vec2.FromAngle(e.Facing);
                    if (def.AttackKind == SurvivorAttackKind.Contact)
                    {
                        e.Position += moveDirection * def.MoveSpeed * FixedDeltaTime;
                        float contactRange = Hero.Radius + e.Radius + tuning.ContactMargin;
                        if (e.ContactCooldown <= 0f && toHero.LengthSquared <= contactRange * contactRange)
                        { DamageHero(e.Damage, e, true); e.ContactCooldown = tuning.ContactIntervalSeconds; }
                    }
                    else if (e.AttackCooldown <= 0f && toHero.LengthSquared <= (def.AttackRange + Hero.Radius) * (def.AttackRange + Hero.Radius) && InArc(e.Facing, toHero, def.AttackArcDegrees))
                    { e.WindupRemaining = def.WindupSeconds; }
                    else e.Position += moveDirection * def.MoveSpeed * FixedDeltaTime;
                }
                if (IsEnded) return;
                if (e.IsBoss)
                {
                    e.SummonCooldown -= FixedDeltaTime;
                    if (e.SummonCooldown <= 0f) { SummonBossWalkers(e); e.SummonCooldown += tuning.BossSummonIntervalSeconds; }
                }
                Vec2 corrected = e.Position;
                ClampAndPushOut(ref corrected, e.Radius);
                e.Position = corrected;
                if (!e.IsBoss && (e.Position - Hero.Position).LengthSquared > relocationSquared && TrySpawnPoint(tuning.SpawnAttempts, true, e.Radius, out Vec2 relocated))
                { e.Position = relocated; e.RelocatedThisTick = true; }
            }
        }

        private void ResolveSwing(SurvivorEnemy e, SurvivorEnemyDef def)
        {
            Vec2 toHero = Hero.Position - e.Position;
            if (toHero.Length <= def.AttackRange + Hero.Radius && InArc(e.Facing, toHero, def.AttackArcDegrees)) DamageHero(e.Damage, e, false);
            e.AttackCooldown = def.RecoverSeconds;
        }

        private void SummonBossWalkers(SurvivorEnemy boss)
        {
            SurvivorTuning tuning = Config.Tuning;
            float step = MathF.PI * 2f / Math.Max(1, tuning.BossSummonCount);
            for (int i = 0; i < tuning.BossSummonCount; i++) SpawnEnemy(0, boss.Position + Vec2.FromAngle(i * step) * tuning.BossSummonRadius, false, false);
        }

        private void ResolveBodyCollisions()
        {
            RebuildHash();
            SeparateHeroFromNearbyEnemies();
            float factor = Config.Tuning.EnemySeparation;
            float margin = maxEnemyRadius;
            int[] heads = spatialHash.Heads; int[] next = spatialHash.Next;
            bool anySeparated = false;
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy a = enemies[i]; if (!a.Active) continue;
                float neighborRadius = a.Radius + margin;
                int minX = spatialHash.MinCell(a.Position.X - neighborRadius);
                int maxX = spatialHash.MinCell(a.Position.X + neighborRadius);
                int minY = spatialHash.MinCell(a.Position.Y - neighborRadius);
                int maxY = spatialHash.MinCell(a.Position.Y + neighborRadius);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        // Cell chains hold pool indices in descending order, so stop at j <= i (each pair once).
                        for (int j = heads[spatialHash.CellIndex(x, y)]; j > i; j = next[j])
                        {
                            SurvivorEnemy b = enemies[j];
                            Vec2 delta = b.Position - a.Position; float needed = a.Radius + b.Radius; float distanceSquared = delta.LengthSquared;
                            if (distanceSquared >= needed * needed || !b.Active) continue;
                            float distance = MathF.Sqrt(distanceSquared);
                            Vec2 direction = distance > 1e-5f ? delta / distance : Vec2.FromAngle((a.Id + b.Id) * 0.7f);
                            float correction = (needed - distance) * factor; float total = a.Mass + b.Mass;
                            a.Position -= direction * correction * (b.Mass / total); b.Position += direction * correction * (a.Mass / total);
                            a.Separated = true; b.Separated = true; anySeparated = true;
                        }
                    }
                }
            }
            if (!anySeparated) return;
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy e = enemies[i];
                if (!e.Separated) continue;
                e.Separated = false;
                if (!e.Active) continue;
                Vec2 corrected = e.Position; ClampAndPushOut(ref corrected, e.Radius); e.Position = corrected;
            }
        }

        private void SeparateHeroFromNearbyEnemies()
        {
            if (Hero.Dashing) return;
            float range = Hero.Radius + maxEnemyRadius;
            int minX = spatialHash.MinCell(Hero.Position.X - range); int maxX = spatialHash.MinCell(Hero.Position.X + range);
            int minY = spatialHash.MinCell(Hero.Position.Y - range); int maxY = spatialHash.MinCell(Hero.Position.Y + range);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int index = spatialHash.Heads[spatialHash.CellIndex(x, y)];
                    while (index >= 0) { if (enemies[index].Active) SeparateHero(enemies[index]); index = spatialHash.Next[index]; }
                }
            }
        }

        private void SeparateHero(SurvivorEnemy e)
        {
            Vec2 delta = Hero.Position - e.Position; float needed = Hero.Radius + e.Radius; float distanceSquared = delta.LengthSquared;
            if (distanceSquared >= needed * needed) return;
            float distance = MathF.Sqrt(distanceSquared);
            Vec2 direction = distance > 1e-5f ? delta / distance : Vec2.FromAngle(e.Id * 0.9f);
            float overlap = needed - distance; float total = Config.ClassDef.Mass + e.Mass;
            Hero.Position += direction * overlap * (e.Mass / total);
            e.Position -= direction * overlap * (Config.ClassDef.Mass / total);
            Vec2 heroPoint = Hero.Position; ClampAndPushOut(ref heroPoint, Hero.Radius); Hero.Position = heroPoint;
            Vec2 enemyPoint = e.Position; ClampAndPushOut(ref enemyPoint, e.Radius); e.Position = enemyPoint;
        }

        private void DamageHero(float raw, SurvivorEnemy source, bool contact)
        {
            if (IsEnded || !Hero.Alive || source == null) return;
            if (testInvulnerable) return;
            SkillDef block = Config.ClassDef.ActiveSkills[BlockSlot];
            bool covered = Hero.Blocking && InArc(Hero.Facing, source.Position - Hero.Position, 180f);
            if (covered && !contact && Time - Hero.BlockStarted <= block.ParryWindowSeconds)
            {
                source.StunRemaining = block.StunSeconds;
                PushEnemy(source, AwayFromHero(source), block.BlockPushback);
                AddEvent(SurvivorEventType.Parry, id: source.Id); return;
            }
            float multiplier = covered ? block.BlockDamageMultiplier : 1f;
            float damage = MathF.Max(1f, raw * multiplier - stats.Armor);
            damage = MathF.Min(damage, Hero.Hp); Hero.Hp -= damage; DamageTaken += damage;
            float hpRatio = Hero.MaxHp > 0f ? Hero.Hp / Hero.MaxHp : 0f;
            if (hpRatio < MinHpRatio) { MinHpRatio = hpRatio; MinHpTime = Time; }
            AddEvent(SurvivorEventType.HeroDamaged, damage, damage / Hero.MaxHp, source.Id, Hero.Position);
            if (covered)
            {
                AddEvent(SurvivorEventType.Blocked, id: source.Id);
                if (!contact) { source.StunRemaining = block.BlockStaggerSeconds; PushEnemy(source, AwayFromHero(source), block.BlockPushback); }
            }
            if (Hero.Hp <= 0f) KillHero(source, contact);
        }

        private void KillHero(SurvivorEnemy source, bool contact)
        {
            if (IsEnded) return;
            Hero.Alive = false; EndReason = EndReason.Died;
            float margin = Config.Tuning.ContactMargin;
            int touching = 0;
            for (int i = 0; i < enemyLimit; i++) if (enemies[i].Active && Vec2.Distance(enemies[i].Position, Hero.Position) <= Hero.Radius + enemies[i].Radius + margin) touching++;
            if (touching >= Config.Tuning.SurroundedCount) DeathCause = DeathCause.Surrounded;
            else if (source.IsBoss) DeathCause = DeathCause.Boss;
            else if (!contact && source.TypeIndex == BruteTypeIndex) DeathCause = DeathCause.Brute;
            else DeathCause = DeathCause.Contact;
            AddEvent(SurvivorEventType.HeroDied, id: source.Id, point: Hero.Position);
        }

        /// <summary>Unit vector from the hero to the enemy, or the hero's facing when they coincide.</summary>
        private Vec2 AwayFromHero(SurvivorEnemy e)
        {
            Vec2 delta = e.Position - Hero.Position;
            float lengthSquared = delta.LengthSquared;
            return lengthSquared > 1e-8f ? delta / MathF.Sqrt(lengthSquared) : Vec2.FromAngle(Hero.Facing);
        }

        /// <summary>Moves the enemy along <paramref name="direction"/> (a unit vector) by distance × (1 − resist).</summary>
        private void PushEnemy(SurvivorEnemy e, Vec2 direction, float distance)
        {
            float moved = distance * (1f - e.KnockbackResist);
            if (moved <= 0f) return;
            e.Position += direction * moved;
            Vec2 corrected = e.Position; ClampAndPushOut(ref corrected, e.Radius); e.Position = corrected;
            hashDrift += moved;
        }

        private static bool InArc(float facing, Vec2 delta, float arcDegrees) => MathF.Abs(WrapAngle(delta.Angle() - facing)) <= arcDegrees * MathF.PI / 360f;
    }
}
