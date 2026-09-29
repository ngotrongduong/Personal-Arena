using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed partial class SurvivorSim
    {
        private void SpawnScheduledEnemies()
        {
            if (Time >= 900f) return;
            while (nextEliteIndex < 4 && Time >= 180f * (nextEliteIndex + 1))
            {
                SpawnElite(); nextEliteIndex++;
            }
            SpawnPhase phase = SurvivorDefaults.PhaseAt(Time);
            if (phase == null) return;
            float scale = 1f + 0.15f * (Config.Build.Tier - 1);
            int max = Math.Min(EnemyCapacity - 20, (int)MathF.Floor(phase.MaxAlive * scale));
            spawnAccumulator += phase.SpawnsPerSecond * scale * FixedDeltaTime;
            while (spawnAccumulator >= 1f)
            {
                spawnAccumulator -= 1f;
                if (CountNormals() >= max) continue;
                int type = WeightedType(phase);
                if (TrySpawnPoint(8, false, out Vec2 point)) SpawnEnemy(type, point, false, false);
            }
        }

        private int CountNormals()
        {
            int count = 0;
            for (int i = 0; i < enemyLimit; i++) if (enemies[i].Active && !enemies[i].IsBoss) count++;
            return count;
        }

        private int WeightedType(SpawnPhase phase)
        {
            int total = 0;
            for (int i = 0; i < phase.Weights.Count; i++) total += phase.Weights[i];
            int roll = rng.NextInt(total);
            for (int i = 0; i < phase.Weights.Count; i++) { if (roll < phase.Weights[i]) return i; roll -= phase.Weights[i]; }
            return 0;
        }

        private void SpawnElite()
        {
            SpawnPhase phase = SurvivorDefaults.PhaseAt(MathF.Min(Time, 899.9f));
            int best = 0;
            for (int i = 1; i < phase.Weights.Count; i++) if (phase.Weights[i] >= phase.Weights[best]) best = i;
            if (!TrySpawnPoint(32, false, out Vec2 point)) point = NearestValidRingPoint();
            SpawnEnemy(best, point, true, false);
        }

        private void SpawnBoss()
        {
            Vec2 point;
            if (!TrySpawnPoint(32, false, out point)) point = NearestValidRingPoint();
            SpawnEnemy(4, point, false, true);
            bossSpawned = true;
        }

        private SurvivorEnemy SpawnEnemy(int type, Vec2 point, bool elite, bool boss)
        {
            SurvivorEnemyDef def = SurvivorDefaults.EnemyDef(type);
            if (def == null) return null;
            int slot = -1;
            for (int i = 0; i < enemyLimit; i++) if (!enemies[i].Active) { slot = i; break; }
            if (slot < 0 && enemyLimit < enemies.Length) { slot = enemyLimit; enemyLimit++; }
            if (slot < 0) return null;
            SurvivorEnemy e = enemies[slot];
            float hpTime = boss ? 1f : 1f + 0.12f * Time / 60f;
            float damageTime = boss ? 1f : 1f + 0.05f * Time / 60f;
            float hpTier = 1f + 0.35f * (Config.Build.Tier - 1);
            float damageTier = 1f + 0.2f * (Config.Build.Tier - 1);
            e.Active = true; e.Id = nextEnemyId++; e.TypeIndex = type; e.Position = point; e.Velocity = Vec2.Zero;
            e.Facing = (Hero.Position - point).Angle(); e.Radius = def.Radius * (elite ? 1.6f : 1f);
            e.Mass = def.Mass * (elite ? 3f : 1f); e.MaxHp = def.BaseHp * hpTime * hpTier * (elite ? 10f : 1f);
            e.Hp = e.MaxHp; e.Damage = def.AttackDamage * damageTime * damageTier * (elite ? 1.5f : 1f);
            e.KnockbackResist = elite ? MathF.Max(def.KnockbackResist, 0.8f) : def.KnockbackResist;
            e.Elite = elite; e.IsBoss = boss; e.WindupRemaining = 0f; e.StunRemaining = 0f;
            e.RelocatedThisTick = false; enemyPreviousPositions[slot] = point;
            e.AttackCooldown = 0f; e.ContactCooldown = 0f; e.SummonCooldown = boss ? 10f : 0f;
            AddEvent(boss ? SurvivorEventType.BossSpawned : elite ? SurvivorEventType.EliteSpawned : SurvivorEventType.EnemySpawned, id: e.Id, point: point);
            return e;
        }

        private bool TrySpawnPoint(int attempts, bool inFront, out Vec2 point)
        {
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                float angle;
                if (inFront && Hero.Velocity.LengthSquared > 0.01f) angle = Hero.Velocity.Angle() + rng.Range(-MathF.PI / 3f, MathF.PI / 3f);
                else angle = rng.Range(-MathF.PI, MathF.PI);
                point = Hero.Position + Vec2.FromAngle(angle) * rng.Range(18f, 24f);
                if (MathF.Abs(point.X) > Config.MapHalfSize - 1f || MathF.Abs(point.Y) > Config.MapHalfSize - 1f) continue;
                bool blocked = false;
                for (int i = 0; i < obstacles.Length; i++) if (obstacles[i].Active && Vec2.Distance(point, obstacles[i].Position) < obstacles[i].Radius + 0.8f) { blocked = true; break; }
                if (!blocked) return true;
            }
            point = Vec2.Zero; return false;
        }

        private Vec2 NearestValidRingPoint()
        {
            Vec2 point = Hero.Position + Vec2.FromAngle(Hero.Facing) * 18f;
            ClampAndPushOut(ref point, 1.8f);
            return point;
        }

        private void UpdateEnemies()
        {
            spatialHash.Rebuild(enemies, enemyLimit);
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
                        float contactRange = Hero.Radius + e.Radius + 0.1f;
                        if (e.ContactCooldown <= 0f && toHero.LengthSquared <= contactRange * contactRange)
                        { DamageHero(e.Damage, e, true); e.ContactCooldown = 0.5f; }
                    }
                    else if (e.AttackCooldown <= 0f && toHero.LengthSquared <= (def.AttackRange + Hero.Radius) * (def.AttackRange + Hero.Radius) && InArc(e.Facing, toHero, def.AttackArcDegrees))
                    { e.WindupRemaining = def.WindupSeconds; }
                    else e.Position += moveDirection * def.MoveSpeed * FixedDeltaTime;
                }
                if (e.IsBoss)
                {
                    e.SummonCooldown -= FixedDeltaTime;
                    if (e.SummonCooldown <= 0f) { SummonBossWalkers(e); e.SummonCooldown += 10f; }
                }
                Vec2 corrected = e.Position;
                ClampAndPushOut(ref corrected, e.Radius);
                e.Position = corrected;
                if (!e.IsBoss && (e.Position - Hero.Position).LengthSquared > 1600f && TrySpawnPoint(8, true, out Vec2 relocated)) { e.Position = relocated; e.RelocatedThisTick = true; }
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
            for (int i = 0; i < 8; i++) SpawnEnemy(0, boss.Position + Vec2.FromAngle(i * MathF.PI / 4f) * 3f, false, false);
        }

        private void ResolveBodyCollisions()
        {
            SeparateHeroFromNearbyEnemies();
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy a = enemies[i]; if (!a.Active) continue;
                float neighborRadius = a.Radius + (bossSpawned ? 1.75f : 0.7f);
                int minX = spatialHash.MinCell(a.Position.X - neighborRadius);
                int maxX = spatialHash.MinCell(a.Position.X + neighborRadius);
                int minY = spatialHash.MinCell(a.Position.Y - neighborRadius);
                int maxY = spatialHash.MinCell(a.Position.Y + neighborRadius);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        int j = spatialHash.Heads[spatialHash.CellIndex(x, y)];
                        while (j >= 0)
                        {
                            SurvivorEnemy b = enemies[j];
                            if (j > i && b.Active)
                            {
                                Vec2 delta = b.Position - a.Position; float needed = a.Radius + b.Radius; float distanceSquared = delta.LengthSquared;
                                if (distanceSquared < needed * needed)
                                {
                                    float distance = MathF.Sqrt(distanceSquared);
                                    Vec2 direction = distance > 1e-5f ? delta / distance : Vec2.FromAngle((a.Id + b.Id) * 0.7f);
                                    float correction = (needed - distance) * 0.5f; float total = a.Mass + b.Mass;
                                    a.Position -= direction * correction * (b.Mass / total); b.Position += direction * correction * (a.Mass / total);
                                }
                            }
                            j = spatialHash.Next[j];
                        }
                    }
                }
            }
        }

        private void SeparateHeroFromNearbyEnemies()
        {
            if (Hero.Dashing) return;
            float range = Hero.Radius + (bossSpawned ? 1.75f : 0.7f);
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
            if (!Hero.Alive || source == null) return;
            if (testInvulnerable) return;
            bool covered = Hero.Blocking && InArc(Hero.Facing, source.Position - Hero.Position, 180f);
            if (covered && !contact && Time - Hero.BlockStarted <= Config.ClassDef.ActiveSkills[1].ParryWindowSeconds)
            {
                source.StunRemaining = Config.ClassDef.ActiveSkills[1].StunSeconds;
                PushEnemy(source, Config.ClassDef.ActiveSkills[1].BlockPushback);
                AddEvent(SurvivorEventType.Parry, id: source.Id); return;
            }
            float multiplier = covered ? Config.ClassDef.ActiveSkills[1].BlockDamageMultiplier : 1f;
            float damage = MathF.Max(1f, raw * multiplier - stats.Armor);
            damage = MathF.Min(damage, Hero.Hp); Hero.Hp -= damage; DamageTaken += damage;
            float hpRatio = Hero.MaxHp > 0f ? Hero.Hp / Hero.MaxHp : 0f;
            if (hpRatio < MinHpRatio) { MinHpRatio = hpRatio; MinHpTime = Time; }
            AddEvent(SurvivorEventType.HeroDamaged, damage, damage / Hero.MaxHp, source.Id, Hero.Position);
            if (covered)
            {
                AddEvent(SurvivorEventType.Blocked, id: source.Id);
                if (!contact) { source.StunRemaining = Config.ClassDef.ActiveSkills[1].BlockStaggerSeconds; PushEnemy(source, Config.ClassDef.ActiveSkills[1].BlockPushback); }
            }
            if (Hero.Hp <= 0f) KillHero(source, contact);
        }

        private void KillHero(SurvivorEnemy source, bool contact)
        {
            Hero.Alive = false; EndReason = EndReason.Died;
            int touching = 0;
            for (int i = 0; i < enemyLimit; i++) if (enemies[i].Active && Vec2.Distance(enemies[i].Position, Hero.Position) <= Hero.Radius + enemies[i].Radius + 0.1f) touching++;
            if (touching >= 6) DeathCause = DeathCause.Surrounded;
            else if (source.IsBoss) DeathCause = DeathCause.Boss;
            else if (!contact && source.TypeIndex == 2) DeathCause = DeathCause.Brute;
            else DeathCause = DeathCause.Contact;
            AddEvent(SurvivorEventType.HeroDied, id: source.Id, point: Hero.Position);
        }

        private void PushEnemy(SurvivorEnemy e, float distance)
        {
            e.Position += Vec2.FromAngle(Hero.Facing) * distance * (1f - e.KnockbackResist);
            Vec2 corrected = e.Position; ClampAndPushOut(ref corrected, e.Radius); e.Position = corrected;
        }

        private static bool InArc(float facing, Vec2 delta, float arcDegrees) => MathF.Abs(WrapAngle(delta.Angle() - facing)) <= arcDegrees * MathF.PI / 360f;
    }
}
