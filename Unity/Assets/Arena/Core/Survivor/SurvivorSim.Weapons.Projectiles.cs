using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>
    /// Weapons that launch pooled projectiles (hammers, bolts, fans, trio, quad, bombs, bounce shots, momentum spirit) plus the
    /// projectile update, and the boomerangs that fly out and back. All state is preallocated.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        /// <summary>A boomerang that has not come home after this many seconds simply ends (safety net).</summary>
        private const float BoomerangMaxAge = 12f;
        private float movementFactor;

        /// <summary>Moving average (0..1, time constant 1.5 s) of "the hero is moving"; scales momentum-spirit damage.</summary>
        public float MovementFactor => movementFactor;

        // ---- projectile pool ----

        private SurvivorProjectile NewProjectile()
        {
            for (int i = 0; i < projectileLimit; i++) if (!projectiles[i].Active) { projectiles[i].ExplodeOnExpire = false; projectiles[i].Bouncing = false; return projectiles[i]; }
            if (projectileLimit < projectiles.Length) { SurvivorProjectile fresh = projectiles[projectileLimit++]; fresh.ExplodeOnExpire = false; fresh.Bouncing = false; return fresh; }
            return null;
        }

        private bool LaunchProjectile(int source, Vec2 direction, float speed, float radius, float damage, float knockback,
            float range, int pierce, float explodeRadius, float stun, bool explodeOnExpire = false, int bounces = -1)
        {
            SurvivorProjectile projectile = NewProjectile();
            if (projectile == null) return false;
            projectile.Active = true; projectile.Id = nextProjectileId++; projectile.Position = Hero.Position;
            projectile.Velocity = direction * speed; projectile.Radius = radius; projectile.Damage = damage;
            projectile.Knockback = knockback; projectile.Lifetime = speed > 0f ? range / speed : 0f;
            projectile.PierceRemaining = pierce; projectile.HitCount = 0; projectile.SourceIndex = source;
            projectile.ExplodeRadius = explodeRadius; projectile.StunSeconds = stun; projectile.ExplodeOnExpire = explodeOnExpire;
            projectile.Bouncing = bounces >= 0; projectile.BouncesLeft = bounces; projectile.BounceCount = 0;
            if (projectile.Bouncing) for (int i = 0; i < projectile.BounceIds.Length; i++) { projectile.BounceIds[i] = 0; projectile.BounceUntil[i] = 0f; }
            return true;
        }

        /// <summary>
        /// Moves projectiles and resolves hits through the spatial hash. Hits of one projectile are
        /// applied in ascending enemy-pool order (same order as a full scan).
        /// </summary>
        private void UpdateProjectiles()
        {
            bool any = false;
            for (int i = 0; i < projectileLimit; i++) if (projectiles[i].Active) { any = true; break; }
            if (!any) return;
            RebuildHash();
            int[] heads = spatialHash.Heads; int[] next = spatialHash.Next;
            for (int i = 0; i < projectileLimit; i++)
            {
                SurvivorProjectile p = projectiles[i]; if (!p.Active) continue;
                p.Position += p.Velocity * FixedDeltaTime; p.Lifetime -= FixedDeltaTime;
                if (p.Bouncing)
                {
                    if (p.Lifetime <= 0f || !BounceProjectile(p)) { p.Active = false; continue; }
                }
                else if (p.Lifetime <= 0f || MathF.Abs(p.Position.X) > Config.MapHalfSize || MathF.Abs(p.Position.Y) > Config.MapHalfSize)
                {
                    p.Active = false;
                    if (p.ExplodeOnExpire && p.ExplodeRadius > 0f)
                    {
                        AddEvent(SurvivorEventType.StrikeLanded, p.ExplodeRadius, id: p.SourceIndex, point: p.Position);
                        Blast(p.Position, p.ExplodeRadius, p.Damage, p.Knockback, p.StunSeconds);
                        if (IsEnded) return;
                    }
                    continue;
                }
                float query = p.Radius + maxEnemyRadius + hashDrift;
                int minX = spatialHash.MinCell(p.Position.X - query), maxX = spatialHash.MinCell(p.Position.X + query);
                int minY = spatialHash.MinCell(p.Position.Y - query), maxY = spatialHash.MinCell(p.Position.Y + query);
                int found = 0;
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int j = heads[spatialHash.CellIndex(x, y)]; j >= 0; j = next[j])
                        {
                            SurvivorEnemy e = enemies[j];
                            if (!e.Active) continue;
                            float radius = p.Radius + e.Radius;
                            if ((p.Position - e.Position).LengthSquared > radius * radius || AlreadyHit(p, e.Id)) continue;
                            int slot = found++;
                            while (slot > 0 && enemyScratch[slot - 1] > j) { enemyScratch[slot] = enemyScratch[slot - 1]; slot--; }
                            enemyScratch[slot] = j;
                        }
                    }
                }
                if (found == 0) continue;
                if (p.ExplodeRadius > 0f)
                {
                    p.Active = false;
                    AddEvent(SurvivorEventType.StrikeLanded, p.ExplodeRadius, id: p.SourceIndex, point: p.Position);
                    Blast(p.Position, p.ExplodeRadius, p.Damage, p.Knockback, p.StunSeconds);
                    if (IsEnded) return;
                    continue;
                }
                Vec2 pushDirection = p.Velocity.Normalized();
                if (p.Bouncing)
                {
                    bool struck = false; Vec2 struckAt = Vec2.Zero;
                    for (int n = 0; n < found; n++)
                    {
                        SurvivorEnemy e = enemies[enemyScratch[n]];
                        if (!RecordBounceHit(p, e.Id)) continue;
                        if (!struck) { struck = true; struckAt = e.Position; }
                        DamageEnemy(e, p.Damage, p.Knockback, pushDirection);
                    }
                    if (IsEnded) return;
                    if (struck) RicochetOffEnemy(p, struckAt);
                    continue;
                }
                for (int n = 0; n < found; n++)
                {
                    SurvivorEnemy e = enemies[enemyScratch[n]];
                    DamageEnemy(e, p.Damage, p.Knockback, pushDirection);
                    if (p.StunSeconds > 0f) Stun(e, p.StunSeconds);
                    if (p.HitCount < p.HitIds.Length) p.HitIds[p.HitCount++] = e.Id;
                    if (p.PierceRemaining-- <= 0 || p.HitCount >= p.HitIds.Length) { p.Active = false; break; }
                }
                if (IsEnded) return;
            }
        }

        internal bool AlreadyHit(SurvivorProjectile p, int enemyId)
        {
            if (p.Bouncing)
            {
                for (int i = 0; i < p.BounceIds.Length; i++) if (p.BounceIds[i] == enemyId && p.BounceUntil[i] > Time) return true;
                return false;
            }
            for (int i = 0; i < p.HitCount; i++) if (p.HitIds[i] == enemyId) return true;
            return false;
        }

        /// <summary>
        /// Bounce shot: remembers <paramref name="enemyId"/> for <see cref="SurvivorCatalog.BounceRehitSeconds"/> in a free or
        /// expired slot. Returns false (no hit) when every slot is still live, so an enemy is never forgotten early.
        /// </summary>
        internal bool RecordBounceHit(SurvivorProjectile p, int enemyId)
        {
            for (int i = 0; i < p.BounceIds.Length; i++)
            {
                if (p.BounceUntil[i] > Time) continue;
                p.BounceIds[i] = enemyId; p.BounceUntil[i] = Time + SurvivorCatalog.BounceRehitSeconds;
                return true;
            }
            return false;
        }

        // ---- thrown hammer / bolt / arrow ----

        private bool ThrowHammers(ItemDef def, int level, out Vec2 firstDirection)
        {
            firstDirection = Vec2.Zero;
            int count = Math.Min(VolleyCount(def.CountByLevel, level), volleyTargetIds.Length);
            float speed = def.ProjectileSpeed;
            int fired = 0;
            for (int hammer = 0; hammer < count; hammer++)
            {
                SurvivorEnemy target = RandomTarget(fired, def.BaseRange);
                if (target == null) break;
                SurvivorProjectile projectile = NewProjectile();
                if (projectile == null) break;
                Vec2 direction = (target.Position - Hero.Position).Normalized();
                if (fired == 0) firstDirection = direction;
                projectile.Active = true; projectile.Id = nextProjectileId++; projectile.Position = Hero.Position;
                projectile.Velocity = direction * speed; projectile.Radius = def.ProjectileRadius * stats.AreaMul;
                projectile.Damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
                projectile.Knockback = def.Knockback; projectile.Lifetime = speed > 0f ? def.ProjectileRange / speed : 0f;
                projectile.PierceRemaining = def.Pierce; projectile.SourceIndex = def.CatalogIndex;
                projectile.ExplodeRadius = 0f; projectile.StunSeconds = def.StunSeconds;
                projectile.HitCount = 0; volleyTargetIds[fired] = target.Id; fired++;
            }
            return fired > 0;
        }

        private bool UsedVolleyTarget(int id, int count) { for (int i = 0; i < count; i++) if (volleyTargetIds[i] == id) return true; return false; }

        // ---- fan / trio / quad ----

        /// <summary>Fan: <c>count</c> projectiles spread over ArcDegrees, centred on the nearest enemy in range.</summary>
        private bool FireFan(ItemDef def, int level, out Vec2 aim)
        {
            aim = Vec2.Zero;
            SurvivorEnemy target = NearestEnemy(def.BaseRange);
            if (target == null) return false;
            aim = (target.Position - Hero.Position).Normalized();
            if (aim.LengthSquared < 1e-8f) aim = Vec2.FromAngle(Hero.Facing);
            int count = VolleyCount(def.CountByLevel, level);
            float step = count > 1 ? def.ArcDegrees * MathF.PI / 180f / (count - 1) : 0f;
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            int fired = 0;
            for (int k = 0; k < count; k++)
            {
                Vec2 direction = Rotate(aim, (k - (count - 1) * 0.5f) * step);
                if (!LaunchProjectile(def.CatalogIndex, direction, def.ProjectileSpeed, def.ProjectileRadius * stats.AreaMul,
                    damage, def.Knockback, def.ProjectileRange, def.Pierce, 0f, 0f)) break;
                fired++;
            }
            return fired > 0; // a full projectile pool fires nothing and keeps the cooldown, like the other volleys
        }

        /// <summary>Trio: <c>count</c> projectiles in a tight spread of ArcDegrees at ONE random enemy in range (the only rng draw).</summary>
        private bool FireTrio(ItemDef def, int level, out Vec2 aim, out int count)
        {
            aim = Vec2.Zero; count = 0;
            SurvivorEnemy target = RandomTarget(0, def.BaseRange);
            if (target == null) return false;
            aim = (target.Position - Hero.Position).Normalized();
            if (aim.LengthSquared < 1e-8f) aim = Vec2.FromAngle(Hero.Facing);
            int shots = VolleyCount(def.CountByLevel, level);
            float step = shots > 1 ? def.ArcDegrees * MathF.PI / 180f / (shots - 1) : 0f;
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            for (int k = 0; k < shots; k++)
            {
                if (!LaunchProjectile(def.CatalogIndex, Rotate(aim, (k - (shots - 1) * 0.5f) * step), def.ProjectileSpeed, def.ProjectileRadius * stats.AreaMul,
                    damage, def.Knockback, def.ProjectileRange, def.Pierce, 0f, 0f)) break;
                count++;
            }
            return count > 0;
        }

        /// <summary>Quad: fires with or without enemies, <c>count</c> projectiles (staggered) in each of facing, +90, +180, +270 degrees. A full projectile pool skips the rest; nothing fired keeps the cooldown.</summary>
        private bool FireQuad(ItemDef def, int level, out Vec2 aim, out int perDirection)
        {
            aim = Vec2.FromAngle(Hero.Facing); perDirection = VolleyCount(def.CountByLevel, level);
            float step = SurvivorCatalog.QuadStaggerDegrees * MathF.PI / 180f;
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            int fired = 0;
            for (int quarter = 0; quarter < 4; quarter++)
            {
                Vec2 baseDirection = quarter == 0 ? aim : quarter == 1 ? new Vec2(-aim.Y, aim.X) : quarter == 2 ? new Vec2(-aim.X, -aim.Y) : new Vec2(aim.Y, -aim.X);
                for (int k = 0; k < perDirection; k++)
                {
                    Vec2 direction = perDirection > 1 ? Rotate(baseDirection, (k - (perDirection - 1) * 0.5f) * step) : baseDirection;
                    if (!LaunchProjectile(def.CatalogIndex, direction, def.ProjectileSpeed, def.ProjectileRadius * stats.AreaMul,
                        damage, def.Knockback, def.ProjectileRange, def.Pierce, 0f, 0f)) return fired > 0;
                    fired++;
                }
            }
            return fired > 0;
        }

        // ---- bomb ----

        /// <summary>Bomb: a slow projectile at the nearest enemy in range that explodes (radius Width × area, full damage) on its first hit or when its range runs out.</summary>
        private bool ThrowBomb(ItemDef def, int level, out Vec2 aim)
        {
            aim = Vec2.Zero;
            SurvivorEnemy target = NearestEnemy(def.BaseRange);
            if (target == null) return false;
            aim = (target.Position - Hero.Position).Normalized();
            if (aim.LengthSquared < 1e-8f) aim = Vec2.FromAngle(Hero.Facing);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            return LaunchProjectile(def.CatalogIndex, aim, def.ProjectileSpeed, def.ProjectileRadius * stats.AreaMul, damage, def.Knockback,
                def.ProjectileRange, 0, def.Width * stats.AreaMul, 0f, true);
        }

        // ---- bounce shot ----

        /// <summary>Count projectiles (fanned 20 degrees apart) at the nearest enemy within BaseRange; each bounces off walls and obstacles.</summary>
        private bool FireBounce(ItemDef def, int level, out Vec2 aim, out int fired)
        {
            aim = Vec2.Zero; fired = 0;
            SurvivorEnemy target = NearestEnemy(def.BaseRange);
            if (target == null) return false;
            aim = (target.Position - Hero.Position).Normalized();
            if (aim.LengthSquared < 1e-8f) aim = Vec2.FromAngle(Hero.Facing);
            int count = VolleyCount(def.CountByLevel, level), bounces = CountAtLevel(def.BouncesByLevel, level);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            for (int k = 0; k < count; k++)
            {
                Vec2 direction = count > 1 ? Rotate(aim, SurvivorCatalog.ThrustAngleOffset(k, count, Config.Tuning.CenteredEvenVolleys)) : aim;
                if (!LaunchProjectile(def.CatalogIndex, direction, def.ProjectileSpeed, def.ProjectileRadius * stats.AreaMul, damage, def.Knockback,
                    def.ProjectileRange, 0, 0f, 0f, false, bounces)) break;
                fired++;
            }
            return fired > 0;
        }

        /// <summary>Reflects a bouncing projectile off the map walls and obstacles (one bounce per tick); false when it has no bounce left for the contact.</summary>
        private bool BounceProjectile(SurvivorProjectile p)
        {
            float limit = Config.MapHalfSize - p.Radius;
            float x = p.Position.X, y = p.Position.Y, vx = p.Velocity.X, vy = p.Velocity.Y;
            bool hit = false;
            if (x > limit && vx > 0f) { x = limit; vx = -vx; hit = true; } else if (x < -limit && vx < 0f) { x = -limit; vx = -vx; hit = true; }
            if (y > limit && vy > 0f) { y = limit; vy = -vy; hit = true; } else if (y < -limit && vy < 0f) { y = -limit; vy = -vy; hit = true; }
            Vec2 position = new Vec2(x, y), velocity = new Vec2(vx, vy);
            if (!hit)
            {
                int o = FindObstacle(position, p.Radius);
                if (o >= 0)
                {
                    Vec2 normal = AwayFromPoint(position, obstacles[o].Position);
                    float along = Vec2.Dot(velocity, normal);
                    position = obstacles[o].Position + normal * (obstacles[o].Radius + p.Radius);
                    if (along < 0f) { velocity -= normal * (2f * along); hit = true; }
                }
            }
            if (!hit) { p.Position = position; return true; }
            if (p.BouncesLeft <= 0) return false;
            p.BouncesLeft--; p.BounceCount++; p.Position = position; p.Velocity = velocity;
            return true;
        }

        /// <summary>
        /// Bounce shot hit an enemy: it turns toward the nearest enemy it has not hit lately (within
        /// <see cref="SurvivorCatalog.BounceRicochetRange"/>), or reflects off the struck enemy when there is none.
        /// Unlike wall bounces this uses up no bounce.
        /// </summary>
        private void RicochetOffEnemy(SurvivorProjectile p, Vec2 struckAt)
        {
            float speed = p.Velocity.Length;
            if (speed < 1e-4f) return;
            SurvivorEnemy target = null; float nearest = SurvivorCatalog.BounceRicochetRange * SurvivorCatalog.BounceRicochetRange;
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active || AlreadyHit(p, enemy.Id)) continue;
                float distanceSquared = (enemy.Position - p.Position).LengthSquared;
                if (distanceSquared < nearest && distanceSquared > 1e-6f) { nearest = distanceSquared; target = enemy; }
            }
            if (target != null) { p.Velocity = (target.Position - p.Position).Normalized() * speed; return; }
            Vec2 normal = AwayFromPoint(p.Position, struckAt);
            float along = Vec2.Dot(p.Velocity, normal);
            if (along < 0f) p.Velocity -= normal * (2f * along);
        }

        /// <summary>Index of an obstacle that a disc at <paramref name="point"/> overlaps (first in grid order), or −1.</summary>
        private int FindObstacle(Vec2 point, float clearance)
        {
            if (activeObstacleCount == 0) return -1;
            if (clearance > ObstacleGridBodyRadius)
            {
                for (int i = 0; i < activeObstacleCount; i++) if (Blocks(i, point, clearance)) return i;
                return -1;
            }
            int cell = ObstacleCell(point);
            int end = obstacleCellStart[cell + 1];
            for (int n = obstacleCellStart[cell]; n < end; n++) if (Blocks(obstacleCellItems[n], point, clearance)) return obstacleCellItems[n];
            return -1;
        }

        // ---- momentum spirit ----

        private void UpdateMovementFactor()
        {
            float moving = Hero.Velocity.LengthSquared > SurvivorCatalog.MomentumSpeedThreshold * SurvivorCatalog.MomentumSpeedThreshold ? 1f : 0f;
            movementFactor += (moving - movementFactor) * (FixedDeltaTime / SurvivorCatalog.MomentumTimeConstant);
        }

        /// <summary>A small fan of projectiles along the hero's movement (the facing when standing still); damage × (0.4 + 1.2 × movement factor).</summary>
        private bool FireMomentum(ItemDef def, int level, out Vec2 aim, out int fired)
        {
            fired = 0;
            float threshold = SurvivorCatalog.MomentumSpeedThreshold;
            aim = Hero.Velocity.LengthSquared > threshold * threshold ? Hero.Velocity.Normalized() : Vec2.FromAngle(Hero.Facing);
            int count = VolleyCount(def.CountByLevel, level);
            float step = count > 1 ? def.ArcDegrees * MathF.PI / 180f / (count - 1) : 0f;
            float damage = (def.BaseDamage + def.DamagePerLevel * (level - 1)) * (SurvivorCatalog.MomentumMinMul + SurvivorCatalog.MomentumSpanMul * MathF.Max(movementFactor, def.MomentumFloor));
            for (int k = 0; k < count; k++)
            {
                Vec2 direction = Rotate(aim, (k - (count - 1) * 0.5f) * step);
                if (!LaunchProjectile(def.CatalogIndex, direction, def.ProjectileSpeed, def.ProjectileRadius * stats.AreaMul, damage, def.Knockback,
                    def.ProjectileRange, 0, 0f, 0f)) break;
                fired++;
            }
            return fired > 0;
        }

        // ---- boomerang ----

        /// <summary>Throws up to count boomerangs (fanned 20 degrees apart) at the nearest enemy within BaseRange; skipped when every slot is flying.</summary>
        private bool ThrowBoomerangs(ItemDef def, int level, out Vec2 aim, out int fired)
        {
            aim = Vec2.Zero; fired = 0;
            int free = 0;
            for (int i = 0; i < boomerangs.Length; i++) if (!boomerangs[i].Active) free++;
            if (free == 0) return false;
            SurvivorEnemy target = NearestEnemy(def.BaseRange);
            if (target == null) return false;
            aim = (target.Position - Hero.Position).Normalized();
            if (aim.LengthSquared < 1e-8f) aim = Vec2.FromAngle(Hero.Facing);
            int count = Math.Min(VolleyCount(def.CountByLevel, level), free);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            int slot = 0;
            for (int k = 0; k < count; k++)
            {
                while (boomerangs[slot].Active) slot++;
                SurvivorBoomerang b = boomerangs[slot];
                b.Active = true; b.Returning = false; b.Position = Hero.Position;
                b.Direction = count > 1 ? Rotate(aim, SurvivorCatalog.ThrustAngleOffset(k, count, Config.Tuning.CenteredEvenVolleys)) : aim;
                b.Radius = def.ProjectileRadius * stats.AreaMul; b.Damage = damage; b.SourceIndex = def.CatalogIndex;
                b.Travelled = 0f; b.MaxRange = def.ProjectileRange; b.Age = 0f; b.OutCount = 0; b.BackCount = 0;
                fired++;
            }
            return fired > 0;
        }

        private void UpdateBoomerangs()
        {
            for (int i = 0; i < boomerangs.Length; i++)
            {
                SurvivorBoomerang b = boomerangs[i]; if (!b.Active) continue;
                ItemDef def = SurvivorCatalog.Get(b.SourceIndex);
                float step = def.ProjectileSpeed * FixedDeltaTime;
                b.Age += FixedDeltaTime;
                if (b.Age > BoomerangMaxAge) { b.Active = false; continue; }
                if (!b.Returning)
                {
                    b.Position += b.Direction * step; b.Travelled += step;
                    HitWithBoomerang(b, def, b.OutIds, ref b.OutCount);
                    if (IsEnded) return;
                    if (b.Travelled >= b.MaxRange || MathF.Abs(b.Position.X) > Config.MapHalfSize || MathF.Abs(b.Position.Y) > Config.MapHalfSize || OverlapsObstacle(b.Position, b.Radius))
                        b.Returning = true;
                }
                else
                {
                    Vec2 toHero = Hero.Position - b.Position; float distance = toHero.Length;
                    if (distance <= Hero.Radius + b.Radius + step) { b.Active = false; continue; }
                    b.Direction = toHero / distance; b.Position += b.Direction * step;
                    HitWithBoomerang(b, def, b.BackIds, ref b.BackCount);
                    if (IsEnded) return;
                }
            }
        }

        /// <summary>Hits every enemy touching the boomerang that is not in this direction's list yet (lists hold SurvivorBoomerang.HitCapacity ids; later enemies are spared).</summary>
        private void HitWithBoomerang(SurvivorBoomerang b, ItemDef def, int[] ids, ref int count)
        {
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy e = enemies[i]; if (!e.Active) continue;
                float reach = b.Radius + e.Radius;
                if ((e.Position - b.Position).LengthSquared > reach * reach) continue;
                bool seen = false;
                for (int n = 0; n < count; n++) if (ids[n] == e.Id) { seen = true; break; }
                if (seen || count >= ids.Length) continue;
                ids[count++] = e.Id;
                DamageEnemy(e, b.Damage, def.Knockback, b.Direction);
            }
        }
    }
}
