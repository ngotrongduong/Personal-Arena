using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed partial class SurvivorSim
    {
        private void FireWeapons()
        {
            for (int i = 0; i < inventory.WeaponCount; i++)
            {
                int index = inventory.WeaponAt(i);
                if (weaponCooldowns[index] > 0f) continue;
                ItemDef def = SurvivorCatalog.Get(index);
                int level = inventory.Level(index);
                if (def.Pattern == WeaponPattern.Sweep)
                {
                    Sweep(def, level);
                    weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                    AddEvent(SurvivorEventType.WeaponFired, id: index, point: Vec2.FromAngle(Hero.Facing));
                }
                else if (def.Pattern == WeaponPattern.Thrown && ThrowHammers(def, level))
                {
                    weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                    AddEvent(SurvivorEventType.WeaponFired, id: index, point: Vec2.FromAngle(Hero.Facing));
                }
            }
        }

        private void Sweep(ItemDef def, int level)
        {
            float range = def.BaseRange * (1f + 0.1f * (level - 1)) * stats.AreaMul;
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                Vec2 delta = enemy.Position - Hero.Position;
                if (delta.Length > range + enemy.Radius) continue;
                bool front = InArc(Hero.Facing, delta, 120f);
                bool back = level >= 5 && InArc(Hero.Facing + MathF.PI, delta, 120f);
                if (front || back) { DamageEnemy(enemy, damage, def.Knockback); }
            }
        }

        private bool ThrowHammers(ItemDef def, int level)
        {
            int count = level == 1 ? 1 : level <= 3 ? 2 : 3;
            int fired = 0;
            for (int hammer = 0; hammer < count; hammer++)
            {
                SurvivorEnemy target = RandomTarget(fired);
                if (target == null) break;
                SurvivorProjectile projectile = NewProjectile();
                if (projectile == null) break;
                Vec2 direction = (target.Position - Hero.Position).Normalized();
                projectile.Active = true; projectile.Id = nextProjectileId++; projectile.Position = Hero.Position;
                projectile.Velocity = direction * 12f; projectile.Radius = 0.4f * stats.AreaMul;
                projectile.Damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
                projectile.Knockback = def.Knockback; projectile.Lifetime = 1f; projectile.PierceRemaining = 1;
                projectile.HitCount = 0; hammerTargetIds[fired] = target.Id; fired++;
            }
            return fired > 0;
        }

        private SurvivorEnemy RandomTarget(int usedCount)
        {
            int count = 0;
            for (int i = 0; i < enemyLimit; i++) if (enemies[i].Active && Vec2.Distance(enemies[i].Position, Hero.Position) <= 10f && !UsedHammerTarget(enemies[i].Id, usedCount)) count++;
            bool allowUsed = count == 0;
            if (allowUsed) for (int i = 0; i < enemyLimit; i++) if (enemies[i].Active && Vec2.Distance(enemies[i].Position, Hero.Position) <= 10f) count++;
            if (count == 0) return null;
            int pick = rng.NextInt(count);
            for (int i = 0; i < enemyLimit; i++)
            {
                if (!enemies[i].Active || Vec2.Distance(enemies[i].Position, Hero.Position) > 10f || (!allowUsed && UsedHammerTarget(enemies[i].Id, usedCount))) continue;
                if (pick-- == 0) return enemies[i];
            }
            return null;
        }

        private bool UsedHammerTarget(int id, int count) { for (int i = 0; i < count; i++) if (hammerTargetIds[i] == id) return true; return false; }

        private SurvivorProjectile NewProjectile()
        {
            for (int i = 0; i < projectileLimit; i++) if (!projectiles[i].Active) return projectiles[i];
            if (projectileLimit < projectiles.Length) return projectiles[projectileLimit++];
            return null;
        }

        private void UpdateProjectiles()
        {
            for (int i = 0; i < projectileLimit; i++)
            {
                SurvivorProjectile p = projectiles[i]; if (!p.Active) continue;
                p.Position += p.Velocity * FixedDeltaTime; p.Lifetime -= FixedDeltaTime;
                if (p.Lifetime <= 0f || MathF.Abs(p.Position.X) > Config.MapHalfSize || MathF.Abs(p.Position.Y) > Config.MapHalfSize) { p.Active = false; continue; }
                for (int j = 0; j < enemyLimit; j++)
                {
                    SurvivorEnemy e = enemies[j]; if (!e.Active || AlreadyHit(p, e.Id)) continue;
                    float radius = p.Radius + e.Radius;
                    if ((p.Position - e.Position).LengthSquared > radius * radius) continue;
                    DamageEnemy(e, p.Damage, p.Knockback); p.HitIds[p.HitCount++] = e.Id;
                    if (p.PierceRemaining-- <= 0) { p.Active = false; break; }
                }
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
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy e = enemies[i]; if (!e.Active) continue;
                Vec2 delta = e.Position - Hero.Position;
                if (delta.Length > skill.Range + e.Radius || !InArc(Hero.Facing, delta, skill.ArcDegrees)) continue;
                DamageEnemy(e, skill.Damage, skill.Knockback); e.StunRemaining = MathF.Max(e.StunRemaining, skill.StunSeconds); hits++;
            }
            return hits;
        }

        private void DamageEnemy(SurvivorEnemy enemy, float baseDamage, float knockback)
        {
            if (enemy == null || !enemy.Active) return;
            if (testEnemiesInvulnerable) return;
            float damage = baseDamage * stats.Might;
            float critChance = enemy.StunRemaining > 0f ? MathF.Min(1f, stats.CritChance * 2f) : stats.CritChance;
            if (rng.NextFloat() < critChance) { damage *= stats.CritDamage; AddEvent(SurvivorEventType.Crit, id: enemy.Id, point: enemy.Position); }
            float removed = MathF.Min(enemy.Hp, damage); enemy.Hp -= removed; DamageDealtTotal += removed;
            AddEvent(SurvivorEventType.DamageDealt, removed, removed / enemy.MaxHp, enemy.Id, enemy.Position);
            if (enemy.IsBoss) { float fraction = removed / enemy.MaxHp; BossDamageFraction += fraction; AddEvent(SurvivorEventType.BossDamaged, removed, fraction, enemy.Id, enemy.Position); }
            if (knockback > 0f) PushEnemy(enemy, knockback);
            if (enemy.Hp <= 0f) KillEnemy(enemy);
        }

        private void KillEnemy(SurvivorEnemy enemy)
        {
            enemy.Active = false; Kills++;
            AddEvent(SurvivorEventType.EnemyKilled, enemy.TypeIndex, id: enemy.Id, point: enemy.Position);
            if (enemy.Elite) { EliteKills++; AddEvent(SurvivorEventType.EliteKilled, id: enemy.Id, point: enemy.Position); }
            if (enemy.IsBoss)
            {
                AddEvent(SurvivorEventType.BossKilled, id: enemy.Id, point: enemy.Position);
                float gold = 500f * stats.TierGold * stats.GreedMul; Gold += gold; AddEvent(SurvivorEventType.GoldCollected, gold, id: enemy.Id);
                EndReason = EndReason.Won; AddEvent(SurvivorEventType.RunWon); return;
            }
            SurvivorEnemyDef def = SurvivorDefaults.EnemyDef(enemy.TypeIndex);
            SpawnGem(enemy.Position, enemy.Elite ? 50f : def.Xp);
            if (enemy.Elite)
            {
                float gold = MathF.Floor(rng.Range(20f, 41f)) * stats.TierGold * stats.GreedMul;
                SpawnPickup(PickupKind.Gold, enemy.Position + Vec2.FromAngle(1.2f) * 0.3f, gold, true);
            }
            else if (rng.NextFloat() < 0.03f * (1f + stats.Luck / 100f))
            {
                float gold = MathF.Floor(rng.Range(1f, 6f)) * stats.TierGold * stats.GreedMul;
                SpawnPickup(PickupKind.Gold, enemy.Position + Vec2.FromAngle(1.2f) * 0.3f, gold, true);
            }
            if (rng.NextFloat() < 0.005f) SpawnPickup(PickupKind.Meat, enemy.Position + Vec2.FromAngle(2.4f) * 0.3f, 30f, true);
        }
    }
}
