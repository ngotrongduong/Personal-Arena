using System;
using System.Collections.Generic;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>
    /// Weapon dispatch and the helpers every weapon shares (targeting, damage, kills, blasts). Each mechanic lives in its own
    /// partial: <c>Weapons.Melee</c>, <c>Weapons.AroundHero</c>, <c>Weapons.Projectiles</c>, <c>Weapons.Area</c>, <c>Weapons.Defense</c>.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        private void FireWeapons()
        {
            for (int i = 0; i < inventory.WeaponCount && !IsEnded; i++)
            {
                int index = inventory.WeaponAt(i);
                ItemDef def = SurvivorCatalog.Get(index);
                int level = inventory.Level(index);
                // Weapons with their own timers run every tick, before the cooldown check.
                switch (def.Pattern)
                {
                    case WeaponPattern.Orbit: UpdateOrbitAxes(def, level); continue;
                    case WeaponPattern.Ring: UpdateRing(i, def, level); continue;
                    case WeaponPattern.Shockwave: UpdateShockwave(def, level); continue;
                    case WeaponPattern.Combo: UpdateCombo(def, level); continue;
                    case WeaponPattern.Retaliate: continue;
                    case WeaponPattern.Barrier: UpdateBarrier(def, level); continue;
                    case WeaponPattern.Freeze: UpdateFreeze(def, level); continue;
                }
                if (weaponCooldowns[index] > 0f) continue;
                switch (def.Pattern)
                {
                    case WeaponPattern.Sweep:
                    {
                        Vec2 facing = Vec2.FromAngle(Hero.Facing);
                        if (def.ProjectileSpeed > 0f) LaunchSwordWaves(def, level, facing); else Sweep(def, level);
                        weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                        AddEvent(SurvivorEventType.WeaponFired, id: index, point: facing);
                        break;
                    }
                    case WeaponPattern.Thrown:
                        if (ThrowHammers(def, level, out Vec2 throwDirection))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, id: index, point: throwDirection);
                        }
                        break;
                    case WeaponPattern.Thrust:
                        if (ThrustSpears(def, level, out Vec2 thrustDirection, out int spearCount))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, extra: spearCount, id: index, point: thrustDirection);
                        }
                        break;
                    case WeaponPattern.Strike:
                        if (StrikeTargets(def, level, out int strikeCount))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, extra: strikeCount, id: index);
                        }
                        break;
                    case WeaponPattern.Fan:
                        if (FireFan(def, level, out Vec2 fanDirection))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, id: index, point: fanDirection);
                        }
                        break;
                    case WeaponPattern.Bomb:
                        if (ThrowBomb(def, level, out Vec2 bombDirection))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, id: index, point: bombDirection);
                        }
                        break;
                    case WeaponPattern.Boomerang:
                        if (ThrowBoomerangs(def, level, out Vec2 boomerangDirection, out int boomerangCount))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, extra: boomerangCount, id: index, point: boomerangDirection);
                        }
                        break;
                    case WeaponPattern.Zone:
                        if (DropZone(def, level, out Vec2 zonePoint))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, stats.DurationMul * def.Duration, id: index, point: zonePoint);
                        }
                        break;
                    case WeaponPattern.Bounce:
                        if (FireBounce(def, level, out Vec2 bounceDirection, out int bounceCount))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, extra: bounceCount, id: index, point: bounceDirection);
                        }
                        break;
                    case WeaponPattern.Momentum:
                        if (FireMomentum(def, level, out Vec2 momentumDirection, out int momentumCount))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, movementFactor, momentumCount, index, momentumDirection);
                        }
                        break;
                    case WeaponPattern.Purge:
                        if (Purge(def, level, out int purgeKills))
                        {
                            weaponCooldowns[index] = (def.BaseCooldown + def.CooldownPerLevel * (level - 1)) * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, def.BaseRange * stats.AreaMul, purgeKills, index, Hero.Position);
                        }
                        break;
                    case WeaponPattern.BombRing:
                        if (StartBombRing(def, level, out int ringCount))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, extra: ringCount, id: index, point: Hero.Position);
                        }
                        break;
                    case WeaponPattern.Trio:
                        if (FireTrio(def, level, out Vec2 trioDirection, out int trioCount))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, extra: trioCount, id: index, point: trioDirection);
                        }
                        break;
                    case WeaponPattern.Quad:
                        if (FireQuad(def, level, out Vec2 quadDirection, out int quadCount))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, extra: quadCount, id: index, point: quadDirection);
                        }
                        break;
                    case WeaponPattern.Stone:
                        if (StrikeStones(def, level, out int stoneCount))
                        {
                            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
                            AddEvent(SurvivorEventType.WeaponFired, extra: stoneCount, id: index);
                        }
                        break;
                    case WeaponPattern.Aura:
                        TickAura(def, level);
                        weaponCooldowns[index] = def.HitInterval * stats.CooldownMul;
                        AddEvent(SurvivorEventType.WeaponFired, id: index);
                        break;
                }
            }
        }

        private void ResetContentState()
        {
            orbitAxeCount = 0; orbitAngle = 0f; orbitRemaining = 0f; orbitRadius = 0f; orbitAxeRadius = 0f; orbitWeaponIndex = -1;
            ResetRings();
            comboHitsLeft = 0; comboTimer = 0f; retaliatePending = false; reflectPending = 0f; lastHeroDamage = 0f;
            barrierInit = false; barrierCharges = 0; barrierMax = 0;
            movementFactor = 0f; bombRingAngle = 0f;
            whirlRemaining = 0f; whirlTimer = 0f; whirlSkill = null; hazardOrder = 0;
            for (int i = 0; i < pendingBlastActive.Length; i++) pendingBlastActive[i] = false;
            shockwaveActive = false; shockwaveId = 0; shockwaveRadius = 0f; shockwaveMaxRadius = 0f; shockwaveCenter = Vec2.Zero;
        }

        private int OwnedWeaponWithPattern(WeaponPattern pattern)
        {
            for (int i = 0; i < inventory.WeaponCount; i++)
            {
                int index = inventory.WeaponAt(i);
                if (SurvivorCatalog.Get(index).Pattern == pattern) return index;
            }
            return -1;
        }

        /// <summary>Count at a level plus the duplicator's extra, capped at <see cref="SurvivorCatalog.MaxVolleyCount"/> (the size of the shared buffers).</summary>
        private int VolleyCount(IReadOnlyList<int> counts, int level) => Math.Min(CountAtLevel(counts, level) + stats.Amount, SurvivorCatalog.MaxVolleyCount);

        private static int CountAtLevel(IReadOnlyList<int> counts, int level) =>
            counts.Count == 0 ? 1 : counts[Math.Max(1, Math.Min(level, counts.Count)) - 1];

        private bool HasEnemyInRange(float range)
        {
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                float reach = range + enemy.Radius;
                if ((enemy.Position - Hero.Position).LengthSquared <= reach * reach) return true;
            }
            return false;
        }

        private SurvivorEnemy NearestEnemy(float range)
        {
            SurvivorEnemy target = null; float nearest = float.PositiveInfinity;
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                float distanceSquared = (enemy.Position - Hero.Position).LengthSquared;
                float reach = range + enemy.Radius;
                if (distanceSquared <= reach * reach && distanceSquared < nearest) { nearest = distanceSquared; target = enemy; }
            }
            return target;
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
                if (!UsedVolleyTarget(e.Id, usedCount)) fresh++;
            }
            if (total == 0) return null;
            if (fresh == 0) return enemies[enemyScratch[rng.NextInt(total)]];
            int pick = rng.NextInt(fresh);
            for (int n = 0; n < total; n++)
            {
                SurvivorEnemy e = enemies[enemyScratch[n]];
                if (UsedVolleyTarget(e.Id, usedCount)) continue;
                if (pick-- == 0) return e;
            }
            return null;
        }

        /// <summary>Deals damage (crit chance doubled against stunned enemies; <paramref name="flat"/> skips Might, crit and the crit roll) and knocks the enemy along <paramref name="pushDirection"/>.</summary>
        private void DamageEnemy(SurvivorEnemy enemy, float baseDamage, float knockback, Vec2 pushDirection, bool flat = false)
        {
            if (IsEnded || enemy == null || !enemy.Active) return;
            if (testEnemiesInvulnerable) return;
            float damage = flat ? baseDamage : baseDamage * stats.Might * (RageRemaining > 0f ? RageDamageMul : 1f);
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
            SpawnGem(enemy.Position, (enemy.Elite ? tuning.EliteXp : enemy.Small ? 1 : def.Xp) * tuning.XpMul);
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
            if (!enemy.Elite) RollBonusDrop(enemy);
            if (enemy.Golden) DropGoldenGold(enemy);
            if (def.SplitCount > 0 && !enemy.Small) Split(enemy, def);
        }

        private bool RollMagnetDrop() => rng.NextFloat() < SurvivorCatalog.MagnetChance;

        /// <summary>Damages every enemy within <paramref name="radius"/> of <paramref name="center"/>; returns the number hit.</summary>
        private int Blast(Vec2 center, float radius, float damage, float knockback, float stun)
        {
            int hits = 0;
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                float reach = radius + enemy.Radius;
                if ((enemy.Position - center).LengthSquared > reach * reach) continue;
                DamageEnemy(enemy, damage, knockback, AwayFromPoint(enemy.Position, center));
                if (stun > 0f) Stun(enemy, stun);
                hits++;
            }
            return hits;
        }

        private static void Stun(SurvivorEnemy enemy, float seconds)
        {
            if (enemy.Active && !enemy.IsBoss) enemy.StunRemaining = MathF.Max(enemy.StunRemaining, seconds);
        }

        private static Vec2 Rotate(Vec2 value, float angle)
        {
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            return new Vec2(value.X * c - value.Y * s, value.X * s + value.Y * c);
        }

        private static bool CircleIntersectsSegment(Vec2 center, float radius, Vec2 start, Vec2 direction, float length)
        {
            Vec2 delta = center - start;
            float along = MathF.Max(0f, MathF.Min(length, Vec2.Dot(delta, direction)));
            Vec2 nearest = start + direction * along;
            return (center - nearest).LengthSquared <= radius * radius;
        }

        private static Vec2 AwayFromPoint(Vec2 point, Vec2 origin)
        {
            Vec2 delta = point - origin;
            return delta.LengthSquared > 1e-8f ? delta.Normalized() : new Vec2(1f, 0f);
        }
    }
}
