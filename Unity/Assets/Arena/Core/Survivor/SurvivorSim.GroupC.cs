using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>M9 group C: bounce shot, momentum spirit, time clock, purge and bomb ring. All state is preallocated.</summary>
    public sealed partial class SurvivorSim
    {
        private const float PendingBlastEpsilon = 1e-4f;
        private float movementFactor;
        private float bombRingAngle;
        private readonly Vec2[] pendingBlastPosition = new Vec2[SurvivorCatalog.MaxPendingBlasts];
        private readonly float[] pendingBlastTimer = new float[SurvivorCatalog.MaxPendingBlasts];
        private readonly float[] pendingBlastDamage = new float[SurvivorCatalog.MaxPendingBlasts];
        private readonly float[] pendingBlastRadius = new float[SurvivorCatalog.MaxPendingBlasts];
        private readonly float[] pendingBlastKnockback = new float[SurvivorCatalog.MaxPendingBlasts];
        private readonly int[] pendingBlastSource = new int[SurvivorCatalog.MaxPendingBlasts];
        private readonly bool[] pendingBlastActive = new bool[SurvivorCatalog.MaxPendingBlasts];

        /// <summary>Moving average (0..1, time constant 1.5 s) of "the hero is moving"; scales momentum-spirit damage.</summary>
        public float MovementFactor => movementFactor;
        /// <summary>Bomb-ring explosions that have been placed but have not landed yet.</summary>
        public int PendingBlastCount { get { int n = 0; for (int i = 0; i < pendingBlastActive.Length; i++) if (pendingBlastActive[i]) n++; return n; } }

        private void UpdateMovementFactor()
        {
            float moving = Hero.Velocity.LengthSquared > SurvivorCatalog.MomentumSpeedThreshold * SurvivorCatalog.MomentumSpeedThreshold ? 1f : 0f;
            movementFactor += (moving - movementFactor) * (FixedDeltaTime / SurvivorCatalog.MomentumTimeConstant);
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
                Vec2 direction = count > 1 ? Rotate(aim, SurvivorCatalog.ThrustAngleOffset(k, count)) : aim;
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
    }
}
