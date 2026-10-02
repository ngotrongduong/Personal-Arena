using System;
using System.Collections.Generic;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed partial class SurvivorSim
    {
        private void FireWeapons()
        {
            for (int i = 0; i < inventory.WeaponCount && !IsEnded; i++)
            {
                int index = inventory.WeaponAt(i);
                ItemDef def = SurvivorCatalog.Get(index);
                int level = inventory.Level(index);
                if (def.Pattern == WeaponPattern.Orbit) { UpdateOrbitAxes(def, level); continue; }
                if (def.Pattern == WeaponPattern.Shockwave) { UpdateShockwave(def, level); continue; }
                if (def.Pattern == WeaponPattern.Combo) { UpdateCombo(def, level); continue; }
                if (def.Pattern == WeaponPattern.Retaliate) continue;
                if (def.Pattern == WeaponPattern.Barrier) { UpdateBarrier(def, level); continue; }
                if (weaponCooldowns[index] > 0f) continue;
                if (def.Pattern == WeaponPattern.Sweep)
                {
                    Vec2 facing = Vec2.FromAngle(Hero.Facing);
                    Sweep(def, level);
                    weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                    AddEvent(SurvivorEventType.WeaponFired, id: index, point: facing);
                }
                else if (def.Pattern == WeaponPattern.Thrown && ThrowHammers(def, level, out Vec2 throwDirection))
                {
                    weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                    AddEvent(SurvivorEventType.WeaponFired, id: index, point: throwDirection);
                }
                else if (def.Pattern == WeaponPattern.Thrust && ThrustSpears(def, level, out Vec2 thrustDirection, out int spearCount))
                {
                    weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                    AddEvent(SurvivorEventType.WeaponFired, extra: spearCount, id: index, point: thrustDirection);
                }
                else if (def.Pattern == WeaponPattern.Strike && StrikeTargets(def, level, out int strikeCount))
                {
                    weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                    AddEvent(SurvivorEventType.WeaponFired, extra: strikeCount, id: index);
                }
                else if (def.Pattern == WeaponPattern.Fan && FireFan(def, level, out Vec2 fanDirection))
                {
                    weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                    AddEvent(SurvivorEventType.WeaponFired, id: index, point: fanDirection);
                }
                else if (def.Pattern == WeaponPattern.Bomb && ThrowBomb(def, level, out Vec2 bombDirection))
                {
                    weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                    AddEvent(SurvivorEventType.WeaponFired, id: index, point: bombDirection);
                }
                else if (def.Pattern == WeaponPattern.Boomerang && ThrowBoomerangs(def, level, out Vec2 boomerangDirection, out int boomerangCount))
                {
                    weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                    AddEvent(SurvivorEventType.WeaponFired, extra: boomerangCount, id: index, point: boomerangDirection);
                }
                else if (def.Pattern == WeaponPattern.Zone && DropZone(def, level, out Vec2 zonePoint))
                {
                    weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                    AddEvent(SurvivorEventType.WeaponFired, stats.DurationMul * def.Duration, id: index, point: zonePoint);
                }
                else if (def.Pattern == WeaponPattern.Aura)
                {
                    TickAura(def, level);
                    weaponCooldowns[index] = def.HitInterval * stats.CooldownMul;
                    AddEvent(SurvivorEventType.WeaponFired, id: index);
                }
            }
        }

        private void Sweep(ItemDef def, int level, float damageMul = 1f)
        {
            float range = def.BaseRange * (1f + def.RangePerLevel * (level - 1)) * stats.AreaMul;
            float damage = (def.BaseDamage + def.DamagePerLevel * (level - 1)) * damageMul;
            bool hasBackArc = def.BackArcLevel > 0 && level >= def.BackArcLevel;
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                Vec2 delta = enemy.Position - Hero.Position;
                float reach = range + enemy.Radius;
                if (delta.LengthSquared > reach * reach) continue;
                bool front = InArc(Hero.Facing, delta, def.ArcDegrees);
                bool back = hasBackArc && InArc(Hero.Facing + MathF.PI, delta, def.ArcDegrees);
                if (front || back) DamageEnemy(enemy, damage, def.Knockback, AwayFromHero(enemy));
            }
        }

        private bool ThrowHammers(ItemDef def, int level, out Vec2 firstDirection)
        {
            firstDirection = Vec2.Zero;
            int count = Math.Min(VolleyCount(def.CountByLevel, level), hammerTargetIds.Length);
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
                projectile.HitCount = 0; hammerTargetIds[fired] = target.Id; fired++;
            }
            return fired > 0;
        }

        /// <summary>
        /// Uniform pick among enemies within <paramref name="range"/> that no hammer of this volley
        /// targets yet; when every enemy in range is taken, among all enemies in range. One pass.
        /// </summary>
        private SurvivorEnemy RandomTarget(int usedCount, float range)
        {
            float rangeSquared = range * range;
            int total = 0, fresh = 0;
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy e = enemies[i];
                if (!e.Active || (e.Position - Hero.Position).LengthSquared > rangeSquared) continue;
                enemyScratch[total++] = i;
                if (!UsedHammerTarget(e.Id, usedCount)) fresh++;
            }
            if (total == 0) return null;
            if (fresh == 0) return enemies[enemyScratch[rng.NextInt(total)]];
            int pick = rng.NextInt(fresh);
            for (int n = 0; n < total; n++)
            {
                SurvivorEnemy e = enemies[enemyScratch[n]];
                if (UsedHammerTarget(e.Id, usedCount)) continue;
                if (pick-- == 0) return e;
            }
            return null;
        }

        private bool UsedHammerTarget(int id, int count) { for (int i = 0; i < count; i++) if (hammerTargetIds[i] == id) return true; return false; }

        private SurvivorProjectile NewProjectile()
        {
            for (int i = 0; i < projectileLimit; i++) if (!projectiles[i].Active) { projectiles[i].ExplodeOnExpire = false; return projectiles[i]; }
            if (projectileLimit < projectiles.Length) { SurvivorProjectile fresh = projectiles[projectileLimit++]; fresh.ExplodeOnExpire = false; return fresh; }
            return null;
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
                if (p.Lifetime <= 0f || MathF.Abs(p.Position.X) > Config.MapHalfSize || MathF.Abs(p.Position.Y) > Config.MapHalfSize)
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

        private static bool AlreadyHit(SurvivorProjectile p, int enemyId)
        {
            for (int i = 0; i < p.HitCount; i++) if (p.HitIds[i] == enemyId) return true;
            return false;
        }

        private int Kick(SkillDef skill)
        {
            int hits = 0;
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy e = enemies[i]; if (!e.Active) continue;
                Vec2 delta = e.Position - Hero.Position;
                float reach = skill.Range + e.Radius;
                if (delta.LengthSquared > reach * reach || !InArc(Hero.Facing, delta, skill.ArcDegrees)) continue;
                DamageEnemy(e, skill.Damage, skill.Knockback, AwayFromHero(e)); e.StunRemaining = MathF.Max(e.StunRemaining, skill.StunSeconds); hits++;
            }
            return hits;
        }

        /// <summary>Deals damage (crit chance doubled against stunned enemies; <paramref name="flat"/> skips Might, crit and the crit roll) and knocks the enemy along <paramref name="pushDirection"/>.</summary>
        private void DamageEnemy(SurvivorEnemy enemy, float baseDamage, float knockback, Vec2 pushDirection, bool flat = false)
        {
            if (IsEnded || enemy == null || !enemy.Active) return;
            if (testEnemiesInvulnerable) return;
            float damage = flat ? baseDamage : baseDamage * stats.Might;
            float critChance = flat ? 0f : enemy.StunRemaining > 0f ? MathF.Min(1f, stats.CritChance * 2f) : stats.CritChance;
            if (!flat && rng.NextFloat() < critChance) { damage *= stats.CritDamage; AddEvent(SurvivorEventType.Crit, id: enemy.Id, point: enemy.Position); }
            float removed = MathF.Min(enemy.Hp, damage); enemy.Hp -= removed; DamageDealtTotal += removed; enemy.LastHitTime = Time;
            AddEvent(SurvivorEventType.DamageDealt, removed, removed / enemy.MaxHp, enemy.Id, enemy.Position);
            if (enemy.IsBoss) { float fraction = removed / enemy.MaxHp; BossDamageFraction += fraction; AddEvent(SurvivorEventType.BossDamaged, removed, fraction, enemy.Id, enemy.Position); }
            if (knockback > 0f) PushEnemy(enemy, pushDirection, knockback);
            if (enemy.Hp <= 0f) KillEnemy(enemy);
        }

        private void KillEnemy(SurvivorEnemy enemy)
        {
            SurvivorTuning tuning = Config.Tuning;
            enemy.Active = false; Kills++; aliveEnemyCount--;
            if (enemy.IsBoss) bossEnemy = null; else aliveNormalCount--;
            AddEvent(SurvivorEventType.EnemyKilled, enemy.TypeIndex, id: enemy.Id, point: enemy.Position);
            if (enemy.Elite) { EliteKills++; AddEvent(SurvivorEventType.EliteKilled, id: enemy.Id, point: enemy.Position); }
            if (enemy.IsBoss)
            {
                AddEvent(SurvivorEventType.BossKilled, id: enemy.Id, point: enemy.Position);
                float gold = tuning.BossGold * stats.TierGold * stats.GreedMul; AddGold(gold, GoldSource.Boss); AddEvent(SurvivorEventType.GoldCollected, gold, id: enemy.Id);
                EndReason = EndReason.Won; AddEvent(SurvivorEventType.RunWon); return;
            }
            SurvivorEnemyDef def = SurvivorDefaults.EnemyDef(enemy.TypeIndex);
            SpawnGem(enemy.Position, (enemy.Elite ? tuning.EliteXp : def.Xp) * tuning.XpMul);
            Vec2 goldPoint = enemy.Position + Vec2.FromAngle(tuning.GoldDropAngle) * tuning.DropOffset;
            if (enemy.Elite)
            {
                float gold = MathF.Floor(rng.Range(tuning.EliteGoldMin, tuning.EliteGoldMax)) * stats.TierGold * stats.GreedMul;
                SpawnPickup(PickupKind.Gold, goldPoint, gold, true, GoldSource.Elite);
                SpawnPickup(PickupKind.Chest, enemy.Position + Vec2.FromAngle(SurvivorCatalog.ChestDropAngle) * tuning.DropOffset, 0f, false);
            }
            else if (rng.NextFloat() < tuning.GoldChance * (1f + stats.Luck / 100f))
            {
                float gold = MathF.Floor(rng.Range(tuning.GoldMin, tuning.GoldMax)) * stats.TierGold * stats.GreedMul;
                SpawnPickup(PickupKind.Gold, goldPoint, gold, true);
            }
            if (rng.NextFloat() < EffectiveMeatChance) SpawnPickup(PickupKind.Meat, enemy.Position + Vec2.FromAngle(tuning.MeatDropAngle) * tuning.DropOffset, tuning.MeatHeal, true);
            if (!enemy.Elite && RollMagnetDrop())
                SpawnPickup(PickupKind.Magnet, enemy.Position + Vec2.FromAngle(SurvivorCatalog.MagnetDropAngle) * tuning.DropOffset, 0f, false);
        }

        private bool RollMagnetDrop() => rng.NextFloat() < SurvivorCatalog.MagnetChance;
    }
}
