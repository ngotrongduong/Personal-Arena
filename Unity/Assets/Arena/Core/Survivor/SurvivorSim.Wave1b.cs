using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>M9 wave 1b: barrier, boomerang, poison pool and the spiked-armor reflect. All state is preallocated.</summary>
    public sealed partial class SurvivorSim
    {
        private const float ZoneEpsilon = 1e-4f;
        /// <summary>A boomerang that has not come home after this many seconds simply ends (safety net).</summary>
        private const float BoomerangMaxAge = 12f;
        /// <summary>Poison pool: the densest-group search tests at most this many candidate centres.</summary>
        private const int ZoneCandidateLimit = 64;
        private const float ZoneFallbackDistance = 3f;

        /// <summary>Barrier hits still absorbable right now (0 while recharging or not owned).</summary>
        public int BarrierCharges => OwnedWeaponWithPattern(WeaponPattern.Barrier) >= 0 && barrierInit ? barrierCharges : 0;

        // ---- barrier ----

        /// <summary>Charges track the level (a level-up adds the difference while the shield is up); a broken shield returns when its timer ends.</summary>
        private void SyncBarrier(ItemDef def, int level)
        {
            int max = Math.Min(CountAtLevel(def.CountByLevel, level), SurvivorCatalog.MaxVolleyCount);
            if (!barrierInit) { barrierInit = true; barrierMax = max; barrierCharges = max; return; }
            if (max > barrierMax) { if (barrierCharges > 0) barrierCharges += max - barrierMax; barrierMax = max; }
        }

        private void UpdateBarrier(ItemDef def, int level)
        {
            SyncBarrier(def, level);
            if (barrierCharges > 0 || weaponCooldowns[def.CatalogIndex] > 0f) return;
            barrierCharges = barrierMax;
            AddEvent(SurvivorEventType.WeaponFired, extra: barrierCharges, id: def.CatalogIndex, point: Hero.Position);
        }

        /// <summary>
        /// Called for a hit that would hurt the hero (after a parry, before block and armor). A standing barrier eats it: no damage,
        /// no HeroDamaged, no retaliate, no reflect. The last charge breaks the shield and starts its recharge timer.
        /// </summary>
        private bool TryAbsorbHit()
        {
            int index = OwnedWeaponWithPattern(WeaponPattern.Barrier);
            if (index < 0) return false;
            ItemDef def = SurvivorCatalog.Get(index); int level = inventory.Level(index);
            SyncBarrier(def, level);
            if (barrierCharges <= 0) return false;
            barrierCharges--;
            if (barrierCharges == 0)
            {
                weaponCooldowns[index] = (def.BaseCooldown + def.CooldownPerLevel * (level - 1)) * stats.CooldownMul;
                AddEvent(SurvivorEventType.WeaponFired, extra: 0f, id: index, point: Hero.Position);
            }
            return true;
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
                b.Direction = count > 1 ? Rotate(aim, SurvivorCatalog.ThrustAngleOffset(k, count)) : aim;
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

        // ---- spiked armor ----

        /// <summary>Contact hits taken this tick were summed in reflectPending (a share of the damage actually taken); every enemy touching the hero takes it, flat.</summary>
        private void ResolveReflect()
        {
            if (reflectPending <= 0f) return;
            float damage = reflectPending; reflectPending = 0f;
            if (!Hero.Alive) return;
            float margin = Config.Tuning.ContactMargin;
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy e = enemies[i]; if (!e.Active) continue;
                float reach = Hero.Radius + e.Radius + margin;
                if ((e.Position - Hero.Position).LengthSquared <= reach * reach) DamageEnemy(e, damage, 0f, AwayFromHero(e), true);
            }
        }
    }
}
