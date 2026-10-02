using System;
using System.Collections.Generic;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed partial class SurvivorSim
    {
        private int orbitAxeCount;
        private float orbitAngle;
        private float orbitRemaining;
        private float orbitRadius;
        private float orbitAxeRadius;
        private bool shockwaveActive;
        private int shockwaveId;
        private float shockwaveRadius;
        private float shockwaveMaxRadius;
        private Vec2 shockwaveCenter;
        private int comboHitsLeft;
        private float comboTimer;
        private bool retaliatePending;
        private float reflectPending;
        private float lastHeroDamage;
        private bool barrierInit;
        private int barrierCharges;
        private int barrierMax;
        private readonly Vec2[] orbitAxePositions = new Vec2[8];
        /// <summary>Catalog index of the orbit weapon of the current volley (its cooldown waits while it spins).</summary>
        private int orbitWeaponIndex = -1;

        public int OrbitAxeCount => orbitAxeCount;
        public float OrbitAxeRadius => orbitAxeCount > 0 ? orbitAxeRadius : 0f;
        /// <summary>Catalog index of the spinning orbit weapon, or −1.</summary>
        public int OrbitWeaponIndex => orbitAxeCount > 0 ? orbitWeaponIndex : -1;
        /// <summary>Catalog index of the owned aura weapon, or −1.</summary>
        public int AuraWeaponIndex => OwnedWeaponWithPattern(WeaponPattern.Aura);
        /// <summary>Catalog index of the owned shockwave weapon, or −1.</summary>
        public int ShockwaveWeaponIndex => OwnedWeaponWithPattern(WeaponPattern.Shockwave);
        public float AuraRadius
        {
            get
            {
                int index = AuraWeaponIndex;
                if (index < 0) return 0f;
                ItemDef def = SurvivorCatalog.Get(index);
                return def.BaseRange * (1f + def.RangePerLevel * (inventory.Level(index) - 1)) * stats.AreaMul;
            }
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
        public float ShockwaveRadius => shockwaveActive ? shockwaveRadius : 0f;
        public Vec2 ShockwaveCenter => shockwaveCenter;
        public float ShockwaveMaxRadius => shockwaveMaxRadius;

        public Vec2 GetOrbitAxePosition(int k)
        {
            if (k < 0 || k >= orbitAxeCount) throw new ArgumentOutOfRangeException(nameof(k));
            return Hero.Position + Vec2.FromAngle(orbitAngle + k * MathF.PI * 2f / orbitAxeCount) * orbitRadius;
        }

        private void ResetContentState()
        {
            orbitAxeCount = 0; orbitAngle = 0f; orbitRemaining = 0f; orbitRadius = 0f; orbitAxeRadius = 0f; orbitWeaponIndex = -1;
            comboHitsLeft = 0; comboTimer = 0f; retaliatePending = false; reflectPending = 0f; lastHeroDamage = 0f;
            barrierInit = false; barrierCharges = 0; barrierMax = 0;
            movementFactor = 0f; bombRingAngle = 0f;
            whirlRemaining = 0f; whirlTimer = 0f; whirlSkill = null; hazardOrder = 0;
            for (int i = 0; i < pendingBlastActive.Length; i++) pendingBlastActive[i] = false;
            shockwaveActive = false; shockwaveId = 0; shockwaveRadius = 0f; shockwaveMaxRadius = 0f; shockwaveCenter = Vec2.Zero;
        }

        private bool ThrustSpears(ItemDef def, int level, out Vec2 aim, out int count)
        {
            aim = Vec2.Zero; count = VolleyCount(def.CountByLevel, level);
            float length = def.BaseRange * stats.AreaMul;
            SurvivorEnemy target = null; float nearest = float.PositiveInfinity;
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                Vec2 delta = enemy.Position - Hero.Position; float distanceSquared = delta.LengthSquared;
                float reach = length + enemy.Radius;
                if (distanceSquared <= reach * reach && distanceSquared < nearest) { nearest = distanceSquared; target = enemy; }
            }
            if (target == null) return false;
            aim = (target.Position - Hero.Position).Normalized();
            if (aim.LengthSquared < 1e-8f) aim = Vec2.FromAngle(Hero.Facing);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            float halfWidth = def.Width * stats.AreaMul;
            for (int spear = 0; spear < count && !IsEnded; spear++)
            {
                Vec2 direction = Rotate(aim, SurvivorCatalog.ThrustAngleOffset(spear, count));
                for (int i = 0; i < enemyLimit && !IsEnded; i++)
                {
                    SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                    if (CircleIntersectsSegment(enemy.Position, enemy.Radius + halfWidth, Hero.Position, direction, length))
                        DamageEnemy(enemy, damage, def.Knockback, direction);
                }
            }
            return true;
        }

        private void UpdateOrbitAxes(ItemDef def, int level)
        {
            if (orbitAxeCount == 0)
            {
                if (weaponCooldowns[def.CatalogIndex] > 0f) return;
                orbitAxeCount = Math.Min(VolleyCount(def.CountByLevel, level), orbitAxePositions.Length);
                orbitWeaponIndex = def.CatalogIndex;
                orbitAngle = Hero.Facing; orbitRemaining = def.Duration * stats.DurationMul;
                orbitRadius = def.BaseRange * stats.AreaMul; orbitAxeRadius = def.ProjectileRadius * stats.AreaMul;
                AddEvent(SurvivorEventType.WeaponFired, extra: orbitAxeCount, id: def.CatalogIndex);
                ApplyOrbitHits(def, level);
                return;
            }
            orbitWeaponIndex = def.CatalogIndex; // a chest may evolve the weapon mid-spin
            orbitAngle = WrapAngle(orbitAngle + def.AngularSpeedDegrees * MathF.PI / 180f * FixedDeltaTime);
            orbitRemaining -= FixedDeltaTime;
            if (orbitRemaining <= 0f)
            {
                orbitAxeCount = 0; orbitRemaining = 0f;
                weaponCooldowns[def.CatalogIndex] = def.BaseCooldown * stats.CooldownMul;
                return;
            }
            ApplyOrbitHits(def, level);
        }

        private void ApplyOrbitHits(ItemDef def, int level)
        {
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            int axes = Math.Min(orbitAxeCount, orbitAxePositions.Length);
            for (int axe = 0; axe < axes; axe++) orbitAxePositions[axe] = GetOrbitAxePosition(axe);
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy enemy = enemies[i];
                if (!enemy.Active || Time + 1e-6f < enemy.OrbitNextHitTime) continue;
                float reach = orbitAxeRadius + enemy.Radius; bool hit = false;
                for (int axe = 0; axe < axes; axe++)
                {
                    if ((orbitAxePositions[axe] - enemy.Position).LengthSquared <= reach * reach) { hit = true; break; }
                }
                if (!hit) continue;
                enemy.OrbitNextHitTime = Time + def.HitInterval;
                DamageEnemy(enemy, damage, def.Knockback, AwayFromHero(enemy));
            }
        }

        private void TickAura(ItemDef def, int level)
        {
            float radius = def.BaseRange * (1f + def.RangePerLevel * (level - 1)) * stats.AreaMul;
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                float reach = radius + enemy.Radius;
                if ((enemy.Position - Hero.Position).LengthSquared <= reach * reach)
                    DamageEnemy(enemy, damage, def.Knockback, AwayFromHero(enemy));
            }
        }

        /// <summary>
        /// Combo blade: when ready, strikes at once, then twice more every <see cref="ItemDef.HitInterval"/> seconds
        /// (a sweep each time, along the facing at that moment); the last strike deals double. The cooldown starts with
        /// the first strike. A swing that has begun finishes even if the weapon is replaced.
        /// </summary>
        private void UpdateCombo(ItemDef def, int level)
        {
            if (comboHitsLeft > 0)
            {
                comboTimer -= FixedDeltaTime;
                if (comboTimer > 1e-6f) return;
                comboHitsLeft--; comboTimer += def.HitInterval;
                bool last = comboHitsLeft == 0;
                Vec2 facing = Vec2.FromAngle(Hero.Facing);
                Sweep(def, level, last ? SurvivorCatalog.ComboFinisherMul : 1f);
                AddEvent(SurvivorEventType.WeaponFired, extra: SurvivorCatalog.ComboHits - comboHitsLeft, id: def.CatalogIndex, point: facing);
                return;
            }
            if (weaponCooldowns[def.CatalogIndex] > 0f) return;
            Vec2 first = Vec2.FromAngle(Hero.Facing);
            Sweep(def, level);
            comboHitsLeft = SurvivorCatalog.ComboHits - 1; comboTimer = def.HitInterval;
            weaponCooldowns[def.CatalogIndex] = def.BaseCooldown * stats.CooldownMul;
            AddEvent(SurvivorEventType.WeaponFired, extra: 1, id: def.CatalogIndex, point: first);
        }

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

        /// <summary>
        /// Retaliate: when the hero lost HP this tick, blasts a ring around the hero (radius BaseRange × area) and starts
        /// its internal cooldown (BaseCooldown). Resolved after the enemy phase so no enemy dies mid-update.
        /// </summary>
        private void ResolveRetaliate()
        {
            if (!retaliatePending) return;
            retaliatePending = false;
            int index = OwnedWeaponWithPattern(WeaponPattern.Retaliate);
            if (index < 0 || weaponCooldowns[index] > 0f || !Hero.Alive) return;
            ItemDef def = SurvivorCatalog.Get(index);
            float radius = def.BaseRange * stats.AreaMul;
            float damage = def.BaseDamage + def.DamagePerLevel * (inventory.Level(index) - 1);
            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
            AddEvent(SurvivorEventType.WeaponFired, id: index, point: Hero.Position);
            AddEvent(SurvivorEventType.StrikeLanded, radius, id: index, point: Hero.Position);
            Blast(Hero.Position, radius, damage, def.Knockback, 0f);
        }

        private void UpdateShockwave(ItemDef def, int level)
        {
            if (!shockwaveActive)
            {
                if (weaponCooldowns[def.CatalogIndex] > 0f) return;
                float maxRadius = def.BaseRange * stats.AreaMul;
                if (!HasEnemyInRange(maxRadius)) return;
                shockwaveActive = true; shockwaveCenter = Hero.Position; shockwaveRadius = 0f; shockwaveMaxRadius = maxRadius; shockwaveId++;
                weaponCooldowns[def.CatalogIndex] = (def.BaseCooldown + def.CooldownPerLevel * (level - 1)) * stats.CooldownMul;
                AddEvent(SurvivorEventType.WeaponFired, id: def.CatalogIndex, point: shockwaveCenter);
                return;
            }
            shockwaveRadius = MathF.Min(shockwaveMaxRadius, shockwaveRadius + def.ProjectileSpeed * FixedDeltaTime);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active || enemy.LastShockwaveId == shockwaveId) continue;
                float reach = shockwaveRadius + enemy.Radius;
                if ((enemy.Position - shockwaveCenter).LengthSquared > reach * reach) continue;
                enemy.LastShockwaveId = shockwaveId;
                DamageEnemy(enemy, damage, def.Knockback, AwayFromPoint(enemy.Position, shockwaveCenter));
                if (def.StunSeconds > 0f) Stun(enemy, def.StunSeconds);
            }
            if (shockwaveRadius >= shockwaveMaxRadius) shockwaveActive = false;
        }

        /// <summary>Strike: <c>count</c> random enemies in range each get a blast of radius Width × area.</summary>
        private bool StrikeTargets(ItemDef def, int level, out int count)
        {
            count = Math.Min(VolleyCount(def.CountByLevel, level), hammerTargetIds.Length);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            float radius = def.Width * stats.AreaMul;
            int fired = 0;
            for (int strike = 0; strike < count && !IsEnded; strike++)
            {
                SurvivorEnemy target = RandomTarget(fired, def.BaseRange);
                if (target == null) break;
                hammerTargetIds[fired++] = target.Id;
                Vec2 center = target.Position; // the blast knocks the target away; the event keeps the blast centre
                Blast(center, radius, damage, def.Knockback, def.StunSeconds);
                AddEvent(SurvivorEventType.StrikeLanded, radius, id: def.CatalogIndex, point: center);
            }
            count = fired;
            return fired > 0;
        }

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
            for (int k = 0; k < count; k++)
            {
                Vec2 direction = Rotate(aim, (k - (count - 1) * 0.5f) * step);
                if (!LaunchProjectile(def.CatalogIndex, direction, def.ProjectileSpeed, def.ProjectileRadius * stats.AreaMul,
                    damage, def.Knockback, def.ProjectileRange, def.Pierce, 0f, 0f)) break;
            }
            return true;
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

        /// <summary>Stone: instant hits on the <c>count</c> nearest distinct enemies in range for fixed damage (flat: no Might, no crit, no rng).</summary>
        private bool StrikeStones(ItemDef def, int level, out int count)
        {
            count = Math.Min(VolleyCount(def.CountByLevel, level), hammerTargetIds.Length);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            int fired = 0;
            for (int stone = 0; stone < count && !IsEnded; stone++)
            {
                SurvivorEnemy target = null; float nearest = float.PositiveInfinity;
                for (int i = 0; i < enemyLimit; i++)
                {
                    SurvivorEnemy enemy = enemies[i]; if (!enemy.Active || UsedHammerTarget(enemy.Id, fired)) continue;
                    float distanceSquared = (enemy.Position - Hero.Position).LengthSquared; float reach = def.BaseRange + enemy.Radius;
                    if (distanceSquared <= reach * reach && distanceSquared < nearest) { nearest = distanceSquared; target = enemy; }
                }
                if (target == null) break;
                hammerTargetIds[fired++] = target.Id;
                AddEvent(SurvivorEventType.StrikeLanded, 0f, id: def.CatalogIndex, point: target.Position);
                DamageEnemy(target, damage, def.Knockback, AwayFromHero(target), true);
            }
            count = fired;
            return fired > 0;
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
            projectile.Bouncing = bounces >= 0; projectile.BouncesLeft = bounces; projectile.BounceCount = 0; projectile.BounceNext = 0;
            if (projectile.Bouncing) for (int i = 0; i < projectile.BounceIds.Length; i++) { projectile.BounceIds[i] = 0; projectile.BounceUntil[i] = 0f; }
            return true;
        }

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

        /// <summary>Skill projectile: aims at the nearest enemy within Range, else along the facing.</summary>
        private void FireSkillProjectile(int slot, SkillDef skill)
        {
            SurvivorEnemy target = NearestEnemy(skill.Range);
            Vec2 direction = target != null ? (target.Position - Hero.Position).Normalized() : Vec2.FromAngle(Hero.Facing);
            if (direction.LengthSquared < 1e-8f) direction = Vec2.FromAngle(Hero.Facing);
            int pierce = skill.Pierce ? 2 : 0;
            LaunchProjectile(-1 - slot, direction, skill.ProjectileSpeed, skill.ProjectileRadius * stats.AreaMul, skill.Damage,
                skill.Knockback, skill.Range * 1.2f, pierce, skill.AreaRadius * stats.AreaMul, skill.StunSeconds);
        }

        private int AreaBurst(SkillDef skill)
        {
            float radius = skill.AreaRadius * stats.AreaMul;
            AddEvent(SurvivorEventType.StrikeLanded, radius, id: -1, point: Hero.Position);
            return Blast(Hero.Position, radius, skill.Damage, skill.Knockback, skill.StunSeconds);
        }

        /// <summary>
        /// Jumps DashDistance along the move direction (else the facing), then leaves obstacles and stays
        /// inside the map. The jump is recorded in <see cref="teleportShift"/> so it never counts as velocity.
        /// </summary>
        private void Teleport(SkillDef skill, int move)
        {
            Vec2 direction = MoveDirection(move);
            if (direction.LengthSquared < 0.01f) direction = Vec2.FromAngle(Hero.Facing);
            Vec2 point = Hero.Position + direction * skill.DashDistance;
            ClampAndPushOut(ref point, Hero.Radius);
            float limit = Config.MapHalfSize - Hero.Radius;
            point = new Vec2(MathF.Max(-limit, MathF.Min(limit, point.X)), MathF.Max(-limit, MathF.Min(limit, point.Y)));
            teleportShift += point - Hero.Position;
            Hero.Position = point;
        }

        /// <summary>Displacement from blinks during the current tick; excluded from the hero's velocity.</summary>
        private Vec2 teleportShift;

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

        /// <summary>Count at a level plus the duplicator's extra, capped at <see cref="SurvivorCatalog.MaxVolleyCount"/> (the size of the shared buffers).</summary>
        private int VolleyCount(IReadOnlyList<int> counts, int level) => Math.Min(CountAtLevel(counts, level) + stats.Amount, SurvivorCatalog.MaxVolleyCount);

        private static int CountAtLevel(IReadOnlyList<int> counts, int level) =>
            counts.Count == 0 ? 1 : counts[Math.Max(1, Math.Min(level, counts.Count)) - 1];

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
