using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>
    /// Active skill effects: kick, skill projectile, area burst and teleport, plus the M9 skills 5 and 6 (leap slam, whirlwind,
    /// caltrop trap, arrow barrage, fire wall, chain lightning). All state is preallocated.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        private const float SkillEpsilon = 1e-4f;
        /// <summary>Chain lightning never hits more targets than this (size of the scratch list).</summary>
        private const int ChainCapacity = 8;
        /// <summary>Value of the StrikeLanded event of each chain lightning strike.</summary>
        private const float ChainStrikeRadius = 0.6f;
        private readonly int[] chainIds = new int[ChainCapacity];
        private float whirlRemaining;
        private float whirlTimer;
        private SkillDef whirlSkill;
        private int hazardOrder;

        /// <summary>True while the whirlwind spin is on.</summary>
        public bool Whirling => whirlRemaining > 0f && Hero.Alive;
        /// <summary>Seconds of spin left (0 when not spinning).</summary>
        public float WhirlRemaining => Whirling ? whirlRemaining : 0f;

        /// <summary>Displacement from blinks during the current tick; excluded from the hero's velocity.</summary>
        private Vec2 teleportShift;

        // ---- kick ----

        private int Kick(SkillDef skill)
        {
            int hits = 0;
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy e = enemies[i]; if (!e.Active) continue;
                Vec2 delta = e.Position - Hero.Position;
                float reach = skill.Range + e.Radius;
                if (delta.LengthSquared > reach * reach || !InArc(Hero.Facing, delta, skill.ArcDegrees)) continue;
                DamageEnemy(e, skill.Damage, skill.Knockback, AwayFromHero(e));
                if (!e.IsBoss || Config.Tuning.KickStunsBoss) e.StunRemaining = MathF.Max(e.StunRemaining, skill.StunSeconds);
                hits++;
            }
            return hits;
        }

        // ---- skill projectile ----

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

        // ---- area burst ----

        private int AreaBurst(SkillDef skill)
        {
            float radius = skill.AreaRadius * stats.AreaMul;
            AddEvent(SurvivorEventType.StrikeLanded, radius, id: -1, point: Hero.Position);
            return Blast(Hero.Position, radius, skill.Damage, skill.Knockback, skill.StunSeconds);
        }

        // ---- teleport ----

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

        // ---- warrior: leap slam ----

        /// <summary>
        /// Starts a leap (the dash movement at DashSpeed): toward the nearest enemy within Range, stopping when the bodies touch
        /// (at most DashDistance), else along the move direction or the facing for the full distance. The blast comes on landing.
        /// </summary>
        private void StartLeap(SkillDef skill, int move)
        {
            SurvivorEnemy target = NearestEnemy(skill.Range);
            Vec2 direction; float distance = skill.DashDistance;
            if (target != null)
            {
                Vec2 delta = target.Position - Hero.Position; float length = delta.Length;
                direction = length > 1e-4f ? delta / length : Vec2.FromAngle(Hero.Facing);
                distance = MathF.Min(distance, MathF.Max(0f, length - Hero.Radius - target.Radius));
            }
            else
            {
                direction = MoveDirection(move);
                if (direction.LengthSquared < 0.01f) direction = Vec2.FromAngle(Hero.Facing);
            }
            Hero.Dashing = true; Hero.DashDirection = direction; Hero.DashRemaining = distance; Hero.DashSkill = skill;
        }

        private void LandLeap(SkillDef skill)
        {
            float radius = skill.AreaRadius * stats.AreaMul;
            AddEvent(SurvivorEventType.StrikeLanded, radius, id: -1, point: Hero.Position);
            Blast(Hero.Position, radius, skill.Damage, skill.Knockback, skill.StunSeconds);
        }

        // ---- warrior: whirlwind ----

        private void StartWhirlwind(SkillDef skill)
        {
            whirlSkill = skill; whirlRemaining = skill.Duration * stats.DurationMul; whirlTimer = skill.TickSeconds;
        }

        private void UpdateWhirlwind()
        {
            if (whirlRemaining <= 0f) return;
            if (!Hero.Alive) { whirlRemaining = 0f; return; }
            whirlRemaining -= FixedDeltaTime; whirlTimer -= FixedDeltaTime;
            if (whirlTimer <= SkillEpsilon)
            {
                whirlTimer += whirlSkill.TickSeconds;
                float radius = whirlSkill.AreaRadius * stats.AreaMul;
                AddEvent(SurvivorEventType.StrikeLanded, radius, id: -1, point: Hero.Position);
                Blast(Hero.Position, radius, whirlSkill.Damage, whirlSkill.Knockback, 0f);
                if (IsEnded) return;
            }
            if (whirlRemaining <= SkillEpsilon) whirlRemaining = 0f;
        }

        // ---- archer: caltrop trap ----

        /// <summary>Drops a trap at the hero; with MaxAlive traps already down the oldest is replaced.</summary>
        private void DropTrap(SkillDef skill)
        {
            int limit = Math.Max(1, Math.Min(skill.MaxAlive, traps.Length)), alive = 0, free = -1, oldest = -1;
            for (int i = 0; i < traps.Length; i++)
            {
                if (!traps[i].Active) { if (free < 0) free = i; continue; }
                alive++;
                if (oldest < 0 || traps[i].Order < traps[oldest].Order) oldest = i;
            }
            SurvivorTrap trap = alive >= limit || free < 0 ? traps[oldest] : traps[free];
            trap.Active = true; trap.Position = Hero.Position; trap.Radius = skill.AreaRadius * stats.AreaMul;
            trap.Duration = skill.Duration * stats.DurationMul; trap.Remaining = trap.Duration; trap.Damage = skill.Damage;
            trap.TickSeconds = skill.TickSeconds; trap.TickTimer = skill.TickSeconds; trap.SlowFactor = skill.SlowFactor; trap.SlowSeconds = skill.SlowSeconds;
            trap.Order = hazardOrder++;
        }

        /// <summary>Every tick: enemies inside are slowed (refreshed for SlowSeconds); every TickSeconds they take damage.</summary>
        private void UpdateTraps()
        {
            for (int t = 0; t < traps.Length; t++)
            {
                SurvivorTrap trap = traps[t]; if (!trap.Active) continue;
                trap.Remaining -= FixedDeltaTime; trap.TickTimer -= FixedDeltaTime;
                for (int i = 0; i < enemyLimit; i++)
                {
                    SurvivorEnemy e = enemies[i]; if (!e.Active) continue;
                    float reach = trap.Radius + e.Radius;
                    if ((e.Position - trap.Position).LengthSquared > reach * reach) continue;
                    e.SlowMultiplier = e.SlowRemaining > 0f ? MathF.Min(e.SlowMultiplier, trap.SlowFactor) : trap.SlowFactor;
                    e.SlowRemaining = MathF.Max(e.SlowRemaining, trap.SlowSeconds);
                }
                if (trap.TickTimer <= SkillEpsilon)
                {
                    trap.TickTimer += trap.TickSeconds;
                    AddEvent(SurvivorEventType.StrikeLanded, trap.Radius, id: -1, point: trap.Position);
                    Blast(trap.Position, trap.Radius, trap.Damage, 0f, 0f);
                    if (IsEnded) return;
                }
                if (trap.Remaining <= SkillEpsilon) trap.Active = false;
            }
        }

        // ---- archer: arrow barrage ----

        /// <summary>Count arrows over ArcDegrees, centred on the nearest enemy within Range (else the facing); each pierces one more enemy. Returns the arrows launched.</summary>
        private int FireBarrage(int slot, SkillDef skill)
        {
            SurvivorEnemy target = NearestEnemy(skill.Range);
            Vec2 aim = target != null ? (target.Position - Hero.Position).Normalized() : Vec2.FromAngle(Hero.Facing);
            if (aim.LengthSquared < 1e-8f) aim = Vec2.FromAngle(Hero.Facing);
            int count = Math.Max(1, skill.Count);
            float step = count > 1 ? skill.ArcDegrees * MathF.PI / 180f / (count - 1) : 0f;
            int launched = 0;
            for (int k = 0; k < count; k++)
            {
                Vec2 direction = Rotate(aim, (k - (count - 1) * 0.5f) * step);
                if (!LaunchProjectile(-1 - slot, direction, skill.ProjectileSpeed, skill.ProjectileRadius * stats.AreaMul, skill.Damage,
                    skill.Knockback, skill.Range * 1.2f, skill.Pierce ? 1 : 0, 0f, 0f)) break;
                launched++;
            }
            return launched;
        }

        // ---- mage: fire wall ----

        /// <summary>Raises a wall Range metres in front of the hero, across the facing; with MaxAlive walls up the oldest is replaced.</summary>
        private void RaiseWall(SkillDef skill)
        {
            int limit = Math.Max(1, Math.Min(skill.MaxAlive, walls.Length)), alive = 0, free = -1, oldest = -1;
            for (int i = 0; i < walls.Length; i++)
            {
                if (!walls[i].Active) { if (free < 0) free = i; continue; }
                alive++;
                if (oldest < 0 || walls[i].Order < walls[oldest].Order) oldest = i;
            }
            SurvivorWall wall = alive >= limit || free < 0 ? walls[oldest] : walls[free];
            Vec2 direction = Vec2.FromAngle(Hero.Facing);
            wall.Active = true; wall.Direction = direction; wall.Position = Hero.Position + direction * skill.Range;
            wall.Width = skill.Width * stats.AreaMul; wall.Depth = skill.Depth * stats.AreaMul;
            wall.Duration = skill.Duration * stats.DurationMul; wall.Remaining = wall.Duration; wall.Damage = skill.Damage;
            wall.TickSeconds = skill.TickSeconds; wall.TickTimer = skill.TickSeconds; wall.Order = hazardOrder++;
        }

        private void UpdateWalls()
        {
            for (int w = 0; w < walls.Length; w++)
            {
                SurvivorWall wall = walls[w]; if (!wall.Active) continue;
                wall.Remaining -= FixedDeltaTime; wall.TickTimer -= FixedDeltaTime;
                if (wall.TickTimer <= SkillEpsilon)
                {
                    wall.TickTimer += wall.TickSeconds;
                    AddEvent(SurvivorEventType.StrikeLanded, wall.Width * 0.5f, wall.Depth * 0.5f, -1, wall.Position);
                    for (int i = 0; i < enemyLimit && !IsEnded; i++)
                    {
                        SurvivorEnemy e = enemies[i];
                        if (e.Active && InsideWall(wall, e)) DamageEnemy(e, wall.Damage, 0f, wall.Direction);
                    }
                    if (IsEnded) return;
                }
                if (wall.Remaining <= SkillEpsilon) wall.Active = false;
            }
        }

        /// <summary>Circle against the wall's rectangle (across = half width, along = half depth).</summary>
        private static bool InsideWall(SurvivorWall wall, SurvivorEnemy e)
        {
            Vec2 offset = e.Position - wall.Position;
            float along = MathF.Abs(Vec2.Dot(offset, wall.Direction)), across = MathF.Abs(offset.X * -wall.Direction.Y + offset.Y * wall.Direction.X);
            float dx = MathF.Max(0f, along - wall.Depth * 0.5f), dy = MathF.Max(0f, across - wall.Width * 0.5f);
            return dx * dx + dy * dy <= e.Radius * e.Radius;
        }

        // ---- mage: chain lightning ----

        /// <summary>
        /// Strikes the nearest enemy within Range, then jumps to the nearest enemy not yet hit within JumpRange of the last one,
        /// up to Count targets; each strike deals Falloff times the previous. Returns the targets hit.
        /// </summary>
        private int ChainLightning(SkillDef skill)
        {
            SurvivorEnemy current = NearestEnemy(skill.Range);
            int count = Math.Min(Math.Max(1, skill.Count), ChainCapacity), hit = 0;
            float damage = skill.Damage, jumpSquared = skill.JumpRange * skill.JumpRange;
            while (current != null && hit < count && !IsEnded)
            {
                chainIds[hit++] = current.Id;
                Vec2 from = current.Position;
                AddEvent(SurvivorEventType.StrikeLanded, ChainStrikeRadius, id: -1, point: from);
                Vec2 push = AwayFromHero(current);
                DamageEnemy(current, damage, 0f, push);
                Stun(current, skill.StunSeconds);
                damage *= skill.Falloff;
                current = null; float best = float.PositiveInfinity;
                for (int i = 0; i < enemyLimit; i++)
                {
                    SurvivorEnemy e = enemies[i]; if (!e.Active) continue;
                    float d = (e.Position - from).LengthSquared;
                    if (d > jumpSquared || d >= best) continue;
                    bool seen = false;
                    for (int n = 0; n < hit; n++) if (chainIds[n] == e.Id) { seen = true; break; }
                    if (!seen) { best = d; current = e; }
                }
            }
            return hit;
        }
    }
}
