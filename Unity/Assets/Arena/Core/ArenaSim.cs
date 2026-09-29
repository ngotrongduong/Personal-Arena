using System;
using System.Collections.Generic;

namespace PersonalArena.Core
{
    /// <summary>
    /// Headless deterministic 60 Hz arena simulation. The arena is a round platform surrounded by
    /// an abyss: anything whose centre leaves the platform falls and dies.
    /// </summary>
    public sealed class ArenaSim
    {
        public const float FixedDeltaTime = 1f / 60f;
        public const float PotionRadius = 0.35f;
        public const int ProjectilePoolSize = 128;

        /// <summary>
        /// Version of the game rules and observation layout. V3 adds zombie types, projectiles,
        /// hero classes, and the 883-value observation. Brains trained under another version
        /// cannot play this game; the trainer writes it to each run's rules_version.txt.
        /// </summary>
        public const int RulesVersion = 3;

        /// <summary>Exponential decay rate (1/s) of knockback velocity.</summary>
        public const float KnockbackDamping = 9f;

        private const float DegreesToRadians = MathF.PI / 180f;
        private const float KnockbackStopSpeed = 0.05f;
        private const float SpawnMinHeroDistance = 4f;
        private static readonly float KnockbackDecay = MathF.Exp(-KnockbackDamping * FixedDeltaTime);

        private readonly List<ZombieState> zombies = new List<ZombieState>();
        private readonly List<PotionState> potions = new List<PotionState>();
        private readonly List<SimEvent> events = new List<SimEvent>(ProjectilePoolSize * 2 + 32);
        private readonly ProjectileState[] projectiles = new ProjectileState[ProjectilePoolSize];
        private Rng rng;
        private int tick;
        private int nextPotionId;
        private int nextProjectileId;

        public ArenaSim(HeroClassDef hero, ArenaConfig config)
        {
            HeroDef = hero ?? throw new ArgumentNullException(nameof(hero));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            if (hero.Skills == null || hero.Skills.Length != 4)
            {
                throw new ArgumentException("Hero Skills must contain exactly four slots.", nameof(hero));
            }

            Config.Validate();
            Hero = new HeroState();
            events.Capacity = Math.Max(events.Capacity, Config.ZombieCount * 4 + ProjectilePoolSize * 2 + 16);
            for (int i = 0; i < Config.MaxPotions; i++)
            {
                potions.Add(new PotionState());
            }

            for (int i = 0; i < projectiles.Length; i++)
            {
                projectiles[i] = new ProjectileState();
            }

            rng = new Rng(Config.Seed);
            Reset();
        }

        public HeroState Hero { get; }
        public IReadOnlyList<ZombieState> Zombies => zombies;

        /// <summary>Projectile pool slots; only entries with <see cref="ProjectileState.Active"/> are in flight.</summary>
        public IReadOnlyList<ProjectileState> Projectiles => projectiles;

        /// <summary>Potion slots; only entries with <see cref="PotionState.Active"/> are on the floor.</summary>
        public IReadOnlyList<PotionState> Potions => potions;

        public float Time => tick * FixedDeltaTime;
        public bool Done { get; private set; }
        public IReadOnlyList<SimEvent> Events => events;
        public ArenaConfig Config { get; }
        public HeroClassDef HeroDef { get; }

        /// <summary>Knockback start speed that makes a push travel <paramref name="distance"/> metres.</summary>
        public static float KnockbackSpeedFor(float distance) =>
            distance * (1f - KnockbackDecay) / FixedDeltaTime;

        /// <summary>True when <paramref name="position"/> is over the abyss.</summary>
        public bool IsOverAbyss(Vec2 position)
        {
            float radius = Config.Radius;
            return (position - Config.Center).LengthSquared > radius * radius;
        }

        public void Reset(int? seed = null)
        {
            Config.Validate();
            rng = new Rng(seed ?? Config.Seed);
            tick = 0;
            nextPotionId = 0;
            nextProjectileId = 0;
            Done = false;
            events.Clear();

            Hero.Position = Config.Center;
            Hero.Facing = MathF.PI * 0.5f;
            Hero.Velocity = Vec2.Zero;
            Hero.Hp = HeroDef.MaxHp;
            Hero.Energy = HeroDef.MaxEnergy;
            Hero.IsBlocking = false;
            Hero.BlockStartTime = 0f;
            Hero.StunRemaining = 0f;
            Hero.SlowFactor = 1f;
            Hero.SlowRemaining = 0f;
            Hero.DashRemaining = 0f;
            Hero.DashDirection = Vec2.Zero;
            Hero.DashSpeed = 0f;
            Hero.BlockSkillSlot = -1;
            Hero.Alive = true;
            Hero.FellOff = false;
            Hero.Id = 0;
            for (int i = 0; i < Hero.CooldownRemaining.Length; i++)
            {
                Hero.CooldownRemaining[i] = 0f;
            }

            for (int i = 0; i < potions.Count; i++)
            {
                potions[i].Active = false;
            }

            for (int i = 0; i < projectiles.Length; i++)
            {
                projectiles[i].Active = false;
            }

            zombies.Clear();
            for (int i = 0; i < Config.ZombieCount; i++)
            {
                ZombieState zombie = new ZombieState { Id = i, Def = ChooseZombieType() };
                ResetZombie(zombie, false);
                zombies.Add(zombie);
            }

            ResolveAllOverlaps();
        }

        public void Step(HeroInput input)
        {
            events.Clear();
            if (Done)
            {
                return;
            }

            ValidateInput(input);
            TickHeroTimers();
            UpdateHero(input);
            for (int i = 0; i < zombies.Count; i++)
            {
                UpdateZombie(zombies[i]);
            }

            UpdateProjectiles();
            ResolveAllOverlaps();
            CheckFalls();
            UpdatePotions();
            tick++;

            if (!Hero.Alive)
            {
                Done = true;
            }
            else if (Time >= Config.EpisodeSeconds)
            {
                Done = true;
                events.Add(new SimEvent(SimEventType.EpisodeTimeout));
            }
            else if (!Config.RespawnKilledZombies && AllZombiesDead())
            {
                Done = true;
            }
        }

        private static void ValidateInput(HeroInput input)
        {
            if (input.Move < 0 || input.Move > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(input), "Move must be between 0 and 8.");
            }

            if (input.Turn < -1 || input.Turn > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(input), "Turn must be -1, 0, or 1.");
            }

            if (input.Skill < 0 || input.Skill > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(input), "Skill must be between 0 and 4.");
            }
        }

        private void TickHeroTimers()
        {
            for (int i = 0; i < Hero.CooldownRemaining.Length; i++)
            {
                Hero.CooldownRemaining[i] = MathF.Max(0f, Hero.CooldownRemaining[i] - FixedDeltaTime);
            }

            Hero.StunRemaining = MathF.Max(0f, Hero.StunRemaining - FixedDeltaTime);
            if (Hero.SlowRemaining > 0f)
            {
                Hero.SlowRemaining = MathF.Max(0f, Hero.SlowRemaining - FixedDeltaTime);
                if (Hero.SlowRemaining <= 0f)
                {
                    Hero.SlowFactor = 1f;
                }
            }
        }

        private void UpdateHero(HeroInput input)
        {
            SkillDef heldSkill = input.Skill > 0 ? HeroDef.Skills[input.Skill - 1] : null;
            bool holdingBlock = heldSkill != null && heldSkill.Kind == SkillKind.Block;
            bool blockDepleted = UpdateBlocking(holdingBlock, heldSkill, input.Skill - 1);

            if (!Hero.IsBlocking && !blockDepleted)
            {
                Hero.Energy = MathF.Min(HeroDef.MaxEnergy, Hero.Energy + HeroDef.EnergyRegenPerSec * FixedDeltaTime);
            }

            if (Hero.StunRemaining > 0f)
            {
                Hero.Velocity = Vec2.Zero;
                Hero.DashRemaining = 0f;
                return;
            }

            Hero.Facing = NormalizeAngle(Hero.Facing +
                input.Turn * HeroDef.TurnSpeedDegPerSec * DegreesToRadians * FixedDeltaTime);

            Vec2 moveDirection = DirectionFromInput(input.Move);
            if (Hero.DashRemaining > 0f)
            {
                MoveDashing();
            }
            else
            {
                float moveMultiplier = Hero.SlowRemaining > 0f ? Hero.SlowFactor : 1f;
                if (Hero.IsBlocking)
                {
                    moveMultiplier *= heldSkill.BlockMoveMultiplier;
                }

                Vec2 target = moveDirection * (HeroDef.MoveSpeed * moveMultiplier);
                float maxChange = HeroDef.Acceleration > 0f
                    ? HeroDef.Acceleration * FixedDeltaTime
                    : float.PositiveInfinity;
                Hero.Velocity = MoveTowards(Hero.Velocity, target, maxChange);
                Hero.Position += Hero.Velocity * FixedDeltaTime;
            }

            if (input.Skill > 0 && !holdingBlock)
            {
                UseSkill(input.Skill - 1, heldSkill, moveDirection);
            }
        }

        private void MoveDashing()
        {
            float speed = Hero.DashSpeed > 0f ? Hero.DashSpeed : HeroDef.MoveSpeed;
            float seconds = MathF.Min(Hero.DashRemaining, FixedDeltaTime);
            Hero.Position += Hero.DashDirection * (speed * seconds);
            Hero.DashRemaining = MathF.Max(0f, Hero.DashRemaining - FixedDeltaTime);
            Hero.Velocity = Hero.DashRemaining > 0f
                ? Hero.DashDirection * speed
                : Hero.DashDirection * HeroDef.MoveSpeed;
        }

        private bool UpdateBlocking(bool holdingBlock, SkillDef block, int slot)
        {
            if (!holdingBlock)
            {
                Hero.IsBlocking = false;
                Hero.BlockSkillSlot = -1;
                return false;
            }

            if (!Hero.IsBlocking)
            {
                if (Hero.Energy < block.EnergyCost)
                {
                    events.Add(new SimEvent(SimEventType.SkillFailedEnergy));
                    return false;
                }

                Hero.Energy -= block.EnergyCost;
                Hero.IsBlocking = true;
                Hero.BlockStartTime = Time;
                Hero.BlockSkillSlot = slot;
                events.Add(new SimEvent(SimEventType.SkillUsed, 0f, -1));
            }
            else
            {
                Hero.BlockSkillSlot = slot;
            }

            Hero.Energy = MathF.Max(0f, Hero.Energy - block.EnergyPerSecond * FixedDeltaTime);
            if (Hero.Energy <= 0f)
            {
                Hero.IsBlocking = false;
                Hero.BlockSkillSlot = -1;
                return true;
            }

            return false;
        }

        private void UseSkill(int slot, SkillDef skill, Vec2 moveDirection)
        {
            if (skill == null || skill.Kind == SkillKind.None)
            {
                return;
            }

            if (Hero.CooldownRemaining[slot] > 0f)
            {
                events.Add(new SimEvent(SimEventType.SkillFailedCooldown));
                return;
            }

            if (Hero.Energy < skill.EnergyCost)
            {
                events.Add(new SimEvent(SimEventType.SkillFailedEnergy));
                return;
            }

            Hero.Energy -= skill.EnergyCost;
            Hero.CooldownRemaining[slot] = skill.Cooldown;
            events.Add(new SimEvent(SimEventType.SkillUsed, slot + 1));
            switch (skill.Kind)
            {
                case SkillKind.MeleeStrike:
                    PerformStrike(skill);
                    break;
                case SkillKind.Kick:
                    PerformKick(skill);
                    break;
                case SkillKind.Dash:
                    PerformDash(skill, moveDirection);
                    break;
                case SkillKind.Projectile:
                    SpawnHeroProjectile(skill, slot);
                    break;
                case SkillKind.AreaBurst:
                    PerformAreaBurst(skill);
                    break;
                case SkillKind.Teleport:
                    PerformTeleport(skill, moveDirection);
                    break;
                case SkillKind.Block:
                case SkillKind.None:
                    break;
            }
        }

        private void PerformStrike(SkillDef skill)
        {
            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieState zombie = zombies[i];
                if (!IsHeroSkillTarget(zombie, skill))
                {
                    continue;
                }

                if (IsBehindTarget(zombie.Facing, zombie.Position, Hero.Position))
                {
                    float removed = zombie.Hp;
                    DamageZombie(zombie, removed);
                    events.Add(new SimEvent(SimEventType.Backstab, 1f, zombie.Id));
                }
                else
                {
                    DamageZombie(zombie, skill.Damage);
                }
            }
        }

        private void PerformKick(SkillDef skill)
        {
            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieState zombie = zombies[i];
                if (!IsHeroSkillTarget(zombie, skill))
                {
                    continue;
                }

                DamageZombie(zombie, skill.Damage);
                events.Add(new SimEvent(SimEventType.Kick, 1f, zombie.Id));
                if (!zombie.Alive)
                {
                    continue;
                }

                PushZombie(zombie, skill.Knockback);
                zombie.StunRemaining = MathF.Max(zombie.StunRemaining, skill.StunSeconds);
                zombie.AttackPhase = ZombieAttackPhase.Idle;
                zombie.AttackTimer = 0f;
                zombie.Velocity = Vec2.Zero;
            }
        }

        private void PushZombie(ZombieState zombie, float distance)
        {
            Vec2 away = (zombie.Position - Hero.Position).Normalized();
            if (away.LengthSquared <= 0f)
            {
                away = Vec2.FromAngle(Hero.Facing);
            }

            PushZombie(zombie, distance, away);
        }

        private static void PushZombie(ZombieState zombie, float distance, Vec2 direction)
        {
            float resistedDistance = distance * (1f - zombie.Def.KnockbackResist);
            zombie.KnockbackVelocity = direction.Normalized() * KnockbackSpeedFor(resistedDistance);
        }

        private void PerformDash(SkillDef skill, Vec2 moveDirection)
        {
            Hero.DashDirection = skill.DashBackward
                ? -Vec2.FromAngle(Hero.Facing)
                : moveDirection.LengthSquared > 0f
                    ? moveDirection
                    : Vec2.FromAngle(Hero.Facing);
            if (skill.DashSpeed > 0f)
            {
                Hero.DashSpeed = skill.DashSpeed;
                Hero.DashRemaining = skill.DashDistance / skill.DashSpeed;
                Hero.Velocity = Hero.DashDirection * skill.DashSpeed;
            }
            else
            {
                Hero.Position += Hero.DashDirection * skill.DashDistance;
            }
        }

        private void PerformAreaBurst(SkillDef skill)
        {
            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieState zombie = zombies[i];
                if (!zombie.Alive)
                {
                    continue;
                }

                Vec2 offset = zombie.Position - Hero.Position;
                float range = skill.AreaRadius + zombie.Def.Radius;
                if (offset.LengthSquared > range * range)
                {
                    continue;
                }

                DamageZombie(zombie, skill.Damage);
                if (!zombie.Alive)
                {
                    continue;
                }

                ApplySlow(zombie, skill.SlowFactor, skill.SlowSeconds);
                if (skill.Knockback > 0f)
                {
                    Vec2 direction = offset.Normalized();
                    if (direction.LengthSquared <= 0f)
                    {
                        direction = Vec2.FromAngle(Hero.Facing);
                    }

                    PushZombie(zombie, skill.Knockback, direction);
                }

                ApplyStun(zombie, skill.StunSeconds);
            }
        }

        private void PerformTeleport(SkillDef skill, Vec2 moveDirection)
        {
            Vec2 direction = moveDirection.LengthSquared > 0f
                ? moveDirection
                : Vec2.FromAngle(Hero.Facing);
            float safeRadius = MathF.Max(0f, Config.Radius - HeroDef.Radius - 0.3f);
            float edgeDistance = RaySensor.EdgeDistance(Hero.Position, direction, Config.Center, safeRadius);
            float distance = MathF.Min(skill.DashDistance, edgeDistance);
            Hero.Position += direction * distance;
            Hero.DashRemaining = 0f;
            Hero.DashSpeed = 0f;
            Hero.Velocity = Vec2.Zero;
            events.Add(new SimEvent(SimEventType.HeroTeleported, distance));
        }

        private void SpawnHeroProjectile(SkillDef skill, int slot)
        {
            Vec2 direction = Vec2.FromAngle(Hero.Facing);
            SpawnProjectile(
                true,
                -1,
                Hero.Position + direction * (HeroDef.Radius + skill.ProjectileRadius),
                direction,
                skill.ProjectileSpeed,
                skill.ProjectileRadius,
                skill.Damage,
                skill.Range,
                skill.Pierce,
                skill.Knockback,
                skill.StunSeconds,
                skill.SlowFactor,
                skill.SlowSeconds,
                skill.AreaRadius,
                slot);
        }

        private void SpawnZombieProjectile(ZombieState zombie)
        {
            Vec2 direction = (Hero.Position - zombie.Position).Normalized();
            if (direction.LengthSquared <= 0f)
            {
                direction = Vec2.FromAngle(zombie.Facing);
            }

            float speed = zombie.Def.ProjectileSpeed * Config.SpeedMultiplier;
            SpawnProjectile(
                false,
                zombie.Id,
                zombie.Position + direction * (zombie.Def.Radius + zombie.Def.ProjectileRadius),
                direction,
                speed,
                zombie.Def.ProjectileRadius,
                zombie.Def.AttackDamage * Config.DamageMultiplier,
                zombie.Def.ProjectileRange,
                false,
                0f,
                0f,
                0f,
                0f,
                0f,
                -1);
        }

        private void SpawnProjectile(
            bool fromHero,
            int sourceZombieId,
            Vec2 position,
            Vec2 direction,
            float speed,
            float radius,
            float damage,
            float range,
            bool pierce,
            float knockback,
            float stunSeconds,
            float slowFactor,
            float slowSeconds,
            float areaRadius,
            int skillSlot)
        {
            if (speed <= 0f || range <= 0f)
            {
                return;
            }

            for (int i = 0; i < projectiles.Length; i++)
            {
                ProjectileState projectile = projectiles[i];
                if (projectile.Active)
                {
                    continue;
                }

                projectile.Active = true;
                projectile.Id = nextProjectileId++;
                projectile.FromHero = fromHero;
                projectile.SourceZombieId = sourceZombieId;
                projectile.Position = position;
                projectile.Velocity = direction.Normalized() * speed;
                projectile.Radius = radius;
                projectile.Damage = damage;
                projectile.RemainingSeconds = range / speed;
                projectile.Pierce = pierce;
                projectile.HitMask = 0UL;
                projectile.Knockback = knockback;
                projectile.StunSeconds = stunSeconds;
                projectile.SlowFactor = slowFactor;
                projectile.SlowSeconds = slowSeconds;
                projectile.AreaRadius = areaRadius;
                projectile.SkillSlot = skillSlot;
                events.Add(new SimEvent(SimEventType.ProjectileFired, projectile.Id, sourceZombieId));
                return;
            }
        }

        private void UpdateProjectiles()
        {
            for (int i = 0; i < projectiles.Length; i++)
            {
                ProjectileState projectile = projectiles[i];
                if (!projectile.Active)
                {
                    continue;
                }

                Vec2 oldPosition = projectile.Position;
                // The last tick only covers what is left of the range.
                Vec2 newPosition = oldPosition + projectile.Velocity * MathF.Min(FixedDeltaTime, projectile.RemainingSeconds);
                projectile.Position = newPosition;
                if (projectile.FromHero)
                {
                    ResolveHeroProjectile(projectile, oldPosition, newPosition);
                }
                else
                {
                    ResolveZombieProjectile(projectile, oldPosition, newPosition);
                }

                projectile.RemainingSeconds -= FixedDeltaTime;
                if (projectile.RemainingSeconds <= 0f)
                {
                    projectile.Active = false;
                }
            }
        }

        private void ResolveHeroProjectile(ProjectileState projectile, Vec2 start, Vec2 end)
        {
            while (projectile.Active)
            {
                ZombieState target = null;
                float earliest = float.PositiveInfinity;
                for (int i = 0; i < zombies.Count; i++)
                {
                    ZombieState zombie = zombies[i];
                    ulong bit = 1UL << zombie.Id;
                    if (!zombie.Alive || (projectile.HitMask & bit) != 0UL)
                    {
                        continue;
                    }

                    float hitFraction;
                    if (!SegmentCircleHitFraction(
                        start, end, zombie.Position, projectile.Radius + zombie.Def.Radius, out hitFraction))
                    {
                        continue;
                    }

                    if (hitFraction < earliest - 1e-6f ||
                        (MathF.Abs(hitFraction - earliest) <= 1e-6f &&
                         (target == null || zombie.Id < target.Id)))
                    {
                        earliest = hitFraction;
                        target = zombie;
                    }
                }

                if (target == null)
                {
                    return;
                }

                projectile.HitMask |= 1UL << target.Id;
                Vec2 impact = start + (end - start) * earliest;
                float before = target.Hp;
                DamageZombie(target, projectile.Damage);
                events.Add(new SimEvent(SimEventType.ProjectileHit, before - target.Hp, target.Id));
                if (target.Alive)
                {
                    if (projectile.Knockback > 0f)
                    {
                        PushZombie(target, projectile.Knockback, projectile.Velocity);
                    }

                    ApplyStun(target, projectile.StunSeconds);
                    ApplySlow(target, projectile.SlowFactor, projectile.SlowSeconds);
                }

                ApplyProjectileSplash(projectile, target.Id, impact);
                if (!projectile.Pierce)
                {
                    projectile.Active = false;
                    return;
                }
            }
        }

        private void ApplyProjectileSplash(ProjectileState projectile, int directTargetId, Vec2 impact)
        {
            if (projectile.AreaRadius <= 0f)
            {
                return;
            }

            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieState zombie = zombies[i];
                if (!zombie.Alive || zombie.Id == directTargetId)
                {
                    continue;
                }

                float range = projectile.AreaRadius + zombie.Def.Radius;
                if ((zombie.Position - impact).LengthSquared <= range * range)
                {
                    DamageZombie(zombie, projectile.Damage * 0.5f);
                }
            }
        }

        private void ResolveZombieProjectile(ProjectileState projectile, Vec2 start, Vec2 end)
        {
            if (!Hero.Alive)
            {
                return;
            }

            float hitFraction;
            if (!SegmentCircleHitFraction(
                start, end, Hero.Position, projectile.Radius + HeroDef.Radius, out hitFraction))
            {
                return;
            }

            projectile.Active = false;
            SkillDef block = ActiveBlockSkill();
            Vec2 towardSource = -projectile.Velocity;
            bool covered = Hero.IsBlocking && block != null &&
                (block.BlockAllDirections || AngleBetween(Hero.Facing, towardSource) <= MathF.PI * 0.5f);
            if (covered)
            {
                events.Add(new SimEvent(SimEventType.ProjectileBlocked, 0f, projectile.SourceZombieId));
                return;
            }

            float removed = MathF.Min(Hero.Hp, projectile.Damage);
            Hero.Hp -= removed;
            events.Add(new SimEvent(SimEventType.HeroDamaged, removed, projectile.SourceZombieId));
            if (Hero.Hp <= 0f)
            {
                Hero.Hp = 0f;
                KillHero(projectile.SourceZombieId);
            }
        }

        private static void ApplyStun(ZombieState zombie, float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            zombie.StunRemaining = MathF.Max(zombie.StunRemaining, seconds);
            zombie.AttackPhase = ZombieAttackPhase.Idle;
            zombie.AttackTimer = 0f;
            zombie.Velocity = Vec2.Zero;
        }

        private static void ApplySlow(ZombieState zombie, float factor, float seconds)
        {
            if (seconds <= 0f || factor <= 0f || factor >= 1f)
            {
                return;
            }

            if (zombie.SlowRemaining <= 0f || factor < zombie.SlowFactor)
            {
                zombie.SlowFactor = factor;
                zombie.SlowRemaining = seconds;
            }
            else if (factor == zombie.SlowFactor)
            {
                zombie.SlowRemaining = MathF.Max(zombie.SlowRemaining, seconds);
            }
        }

        private static bool SegmentCircleHitFraction(
            Vec2 start, Vec2 end, Vec2 centre, float radius, out float fraction)
        {
            Vec2 offset = start - centre;
            float radiusSquared = radius * radius;
            if (offset.LengthSquared <= radiusSquared)
            {
                fraction = 0f;
                return true;
            }

            Vec2 delta = end - start;
            float a = delta.LengthSquared;
            if (a <= 1e-12f)
            {
                fraction = 0f;
                return false;
            }

            float b = 2f * Vec2.Dot(offset, delta);
            float c = offset.LengthSquared - radiusSquared;
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                fraction = 0f;
                return false;
            }

            float root = MathF.Sqrt(discriminant);
            float first = (-b - root) / (2f * a);
            if (first >= 0f && first <= 1f)
            {
                fraction = first;
                return true;
            }

            float second = (-b + root) / (2f * a);
            if (second >= 0f && second <= 1f)
            {
                fraction = second;
                return true;
            }

            fraction = 0f;
            return false;
        }

        private bool IsHeroSkillTarget(ZombieState zombie, SkillDef skill)
        {
            if (!zombie.Alive)
            {
                return false;
            }

            Vec2 offset = zombie.Position - Hero.Position;
            float range = skill.Range + zombie.Def.Radius;
            if (offset.LengthSquared > range * range)
            {
                return false;
            }

            return IsWithinArc(Hero.Facing, offset, skill.ArcDegrees);
        }

        private void DamageZombie(ZombieState zombie, float requestedDamage)
        {
            float removed = MathF.Min(zombie.Hp, requestedDamage);
            zombie.Hp -= removed;
            events.Add(new SimEvent(SimEventType.HeroDealtDamage, removed, zombie.Id));
            if (zombie.Hp <= 0f)
            {
                zombie.Hp = 0f;
                KillZombie(zombie);
                TryDropPotion(zombie.Position);
            }
        }

        private void KillZombie(ZombieState zombie)
        {
            zombie.Alive = false;
            zombie.Velocity = Vec2.Zero;
            zombie.KnockbackVelocity = Vec2.Zero;
            zombie.StunRemaining = 0f;
            zombie.AttackPhase = ZombieAttackPhase.Idle;
            zombie.AttackTimer = 0f;
            zombie.RespawnRemaining = Config.RespawnKilledZombies ? 1.5f : 0f;
            events.Add(new SimEvent(SimEventType.ZombieKilled, 1f, zombie.Id));
        }

        private void UpdateZombie(ZombieState zombie)
        {
            if (!zombie.Alive)
            {
                if (Config.RespawnKilledZombies)
                {
                    zombie.RespawnRemaining -= FixedDeltaTime;
                    if (zombie.RespawnRemaining <= 1e-6f)
                    {
                        zombie.Def = ChooseZombieType();
                        ResetZombie(zombie, true);
                    }
                }

                return;
            }

            if (zombie.KnockbackVelocity.LengthSquared > 0f)
            {
                zombie.Position += zombie.KnockbackVelocity * FixedDeltaTime;
                zombie.KnockbackVelocity *= KnockbackDecay;
                if (zombie.KnockbackVelocity.LengthSquared < KnockbackStopSpeed * KnockbackStopSpeed)
                {
                    zombie.KnockbackVelocity = Vec2.Zero;
                }
            }

            if (zombie.StunRemaining > 0f)
            {
                zombie.StunRemaining = MathF.Max(0f, zombie.StunRemaining - FixedDeltaTime);
                zombie.Velocity = Vec2.Zero;
                zombie.AttackPhase = ZombieAttackPhase.Idle;
                zombie.AttackTimer = 0f;
                return;
            }

            if (zombie.SlowRemaining > 0f)
            {
                zombie.SlowRemaining = MathF.Max(0f, zombie.SlowRemaining - FixedDeltaTime);
                if (zombie.SlowRemaining <= 0f)
                {
                    zombie.SlowFactor = 1f;
                }
            }

            if (zombie.AttackPhase == ZombieAttackPhase.Windup)
            {
                zombie.Velocity = Vec2.Zero;
                zombie.AttackTimer -= FixedDeltaTime;
                if (zombie.AttackTimer <= 0f)
                {
                    if (zombie.Def.Behavior == ZombieBehavior.Ranged)
                    {
                        SpawnZombieProjectile(zombie);
                    }
                    else
                    {
                        ResolveZombieAttack(zombie);
                    }

                    if (zombie.AttackPhase == ZombieAttackPhase.Windup)
                    {
                        zombie.AttackPhase = ZombieAttackPhase.Recover;
                        zombie.AttackTimer = zombie.Def.AttackCooldown;
                    }
                }

                return;
            }

            Vec2 toHero = Hero.Position - zombie.Position;
            float desiredFacing = toHero.Angle();
            float maxTurn = zombie.Def.TurnSpeedDegPerSec * Config.SpeedMultiplier *
                DegreesToRadians * FixedDeltaTime;
            zombie.Facing = MoveAngleTowards(zombie.Facing, desiredFacing, maxTurn);

            if (zombie.AttackPhase == ZombieAttackPhase.Recover)
            {
                zombie.AttackTimer -= FixedDeltaTime;
                if (zombie.AttackTimer <= 0f)
                {
                    zombie.AttackPhase = ZombieAttackPhase.Idle;
                    zombie.AttackTimer = 0f;
                }
            }

            if (zombie.Def.Behavior == ZombieBehavior.Ranged)
            {
                float distance = toHero.Length;
                float slow = zombie.SlowRemaining > 0f ? zombie.SlowFactor : 1f;
                Vec2 moveDirection = Vec2.Zero;
                // While knocked back it only drifts: walking itself off the rim must not count as a push.
                if (zombie.KnockbackVelocity.LengthSquared > 0f)
                {
                }
                else if (distance > zombie.Def.PreferredDistance + 1f)
                {
                    moveDirection = toHero.Normalized();
                }
                else if (distance < zombie.Def.PreferredDistance - 1.5f)
                {
                    moveDirection = -toHero.Normalized();
                }

                zombie.Velocity = moveDirection *
                    (zombie.Def.MoveSpeed * Config.SpeedMultiplier * slow);
                zombie.Position += zombie.Velocity * FixedDeltaTime;

                if (zombie.AttackPhase == ZombieAttackPhase.Idle &&
                    distance <= zombie.Def.AttackRange &&
                    IsWithinArc(zombie.Facing, toHero, zombie.Def.AttackArcDegrees))
                {
                    zombie.AttackPhase = ZombieAttackPhase.Windup;
                    zombie.AttackTimer = zombie.Def.AttackWindupSeconds;
                    zombie.Velocity = Vec2.Zero;
                }
            }
            else
            {
                float stopDistance = zombie.Def.AttackRange * 0.9f;
                if (toHero.LengthSquared > stopDistance * stopDistance)
                {
                    float slow = zombie.SlowRemaining > 0f ? zombie.SlowFactor : 1f;
                    zombie.Velocity = toHero.Normalized() *
                        (zombie.Def.MoveSpeed * Config.SpeedMultiplier * slow);
                    zombie.Position += zombie.Velocity * FixedDeltaTime;
                }
                else
                {
                    zombie.Velocity = Vec2.Zero;
                    if (zombie.AttackPhase == ZombieAttackPhase.Idle)
                    {
                        zombie.AttackPhase = ZombieAttackPhase.Windup;
                        zombie.AttackTimer = zombie.Def.AttackWindupSeconds;
                    }
                }
            }
        }

        private void ResolveZombieAttack(ZombieState zombie)
        {
            if (!Hero.Alive)
            {
                return;
            }

            Vec2 toHero = Hero.Position - zombie.Position;
            float range = zombie.Def.AttackRange + HeroDef.Radius;
            if (toHero.LengthSquared > range * range ||
                !IsWithinArc(zombie.Facing, toHero, zombie.Def.AttackArcDegrees))
            {
                return;
            }

            Vec2 heroToZombie = zombie.Position - Hero.Position;
            bool fromBehind = AngleBetween(Hero.Facing, heroToZombie) > 120f * DegreesToRadians;
            SkillDef block = ActiveBlockSkill();
            bool covered = Hero.IsBlocking && block != null &&
                (block.BlockAllDirections || AngleBetween(Hero.Facing, heroToZombie) <= MathF.PI * 0.5f);
            float damage = zombie.Def.AttackDamage * Config.DamageMultiplier;

            if (covered)
            {
                if (block.ParryWindowSeconds > 0f &&
                    Time - Hero.BlockStartTime <= block.ParryWindowSeconds + 1e-6f)
                {
                    zombie.StunRemaining = MathF.Max(zombie.StunRemaining, block.StunSeconds);
                    zombie.AttackPhase = ZombieAttackPhase.Idle;
                    zombie.AttackTimer = 0f;
                    PushZombie(zombie, block.BlockPushback * 1.5f);
                    events.Add(new SimEvent(SimEventType.Parry, 1f, zombie.Id));
                    return;
                }

                damage *= block.BlockDamageMultiplier;
                if (block.BlockStaggerSeconds > 0f)
                {
                    zombie.StunRemaining = MathF.Max(zombie.StunRemaining, block.BlockStaggerSeconds);
                    zombie.AttackPhase = ZombieAttackPhase.Idle;
                    zombie.AttackTimer = 0f;
                    PushZombie(zombie, block.BlockPushback);
                    events.Add(new SimEvent(SimEventType.Stagger, block.BlockStaggerSeconds, zombie.Id));
                }

                events.Add(new SimEvent(SimEventType.BlockedHit, damage, zombie.Id));
            }
            else if (fromBehind)
            {
                damage *= 2f;
            }

            float removed = MathF.Min(Hero.Hp, damage);
            Hero.Hp -= removed;
            events.Add(new SimEvent(SimEventType.HeroDamaged, removed, zombie.Id));
            if (fromBehind && !covered)
            {
                events.Add(new SimEvent(SimEventType.HeroDamagedFromBehind, removed, zombie.Id));
            }

            if (Hero.Hp <= 0f)
            {
                Hero.Hp = 0f;
                KillHero(zombie.Id);
            }
        }

        private void KillHero(int zombieId)
        {
            Hero.Alive = false;
            Hero.IsBlocking = false;
            Hero.BlockSkillSlot = -1;
            Hero.DashRemaining = 0f;
            Hero.DashSpeed = 0f;
            Hero.Velocity = Vec2.Zero;
            events.Add(new SimEvent(SimEventType.HeroDied, 1f, zombieId));
        }

        private SkillDef ActiveBlockSkill()
        {
            int slot = Hero.BlockSkillSlot;
            if (slot < 0 || slot >= HeroDef.Skills.Length)
            {
                return null;
            }

            SkillDef skill = HeroDef.Skills[slot];
            return skill != null && skill.Kind == SkillKind.Block ? skill : null;
        }

        private void ResolveAllOverlaps()
        {
            ResolveHeroZombieOverlaps();
            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieState first = zombies[i];
                if (!first.Alive)
                {
                    continue;
                }

                for (int j = i + 1; j < zombies.Count; j++)
                {
                    ZombieState second = zombies[j];
                    if (!second.Alive)
                    {
                        continue;
                    }

                    Separate(first, second);
                }
            }

            // Zombies brace themselves at the rim; only a knockback can throw them into the abyss.
            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieState zombie = zombies[i];
                if (zombie.Alive && zombie.KnockbackVelocity.LengthSquared <= 0f)
                {
                    KeepOnPlatform(zombie);
                }
            }
        }

        private void ResolveHeroZombieOverlaps()
        {
            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieState zombie = zombies[i];
                if (!zombie.Alive)
                {
                    continue;
                }

                Vec2 delta = zombie.Position - Hero.Position;
                float minimum = HeroDef.Radius + zombie.Def.Radius;
                float distanceSquared = delta.LengthSquared;
                if (distanceSquared >= minimum * minimum)
                {
                    continue;
                }

                float distance = MathF.Sqrt(distanceSquared);
                Vec2 normal = distance > 1e-6f
                    ? delta / distance
                    : Vec2.FromAngle((zombie.Id + 1) * 2.39996323f);
                float correction = (minimum - distance) * 0.5f;
                Hero.Position -= normal * correction;
                zombie.Position += normal * correction;
            }
        }

        private void Separate(ZombieState first, ZombieState second)
        {
            Vec2 delta = second.Position - first.Position;
            float minimum = first.Def.Radius + second.Def.Radius;
            float distanceSquared = delta.LengthSquared;
            if (distanceSquared >= minimum * minimum)
            {
                return;
            }

            float distance = MathF.Sqrt(distanceSquared);
            Vec2 normal = distance > 1e-6f
                ? delta / distance
                : Vec2.FromAngle((first.Id + second.Id + 1) * 2.39996323f);
            Vec2 correction = normal * ((minimum - distance) * 0.5f);
            first.Position -= correction;
            second.Position += correction;
        }

        private void KeepOnPlatform(ZombieState zombie)
        {
            float limit = MathF.Max(0f, Config.Radius - zombie.Def.Radius);
            Vec2 offset = zombie.Position - Config.Center;
            if (offset.LengthSquared > limit * limit)
            {
                zombie.Position = Config.Center + offset.Normalized() * limit;
            }
        }

        private void CheckFalls()
        {
            if (Hero.Alive && IsOverAbyss(Hero.Position))
            {
                Hero.FellOff = true;
                events.Add(new SimEvent(SimEventType.HeroFell, 1f));
                KillHero(-1);
            }

            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieState zombie = zombies[i];
                if (zombie.Alive && IsOverAbyss(zombie.Position))
                {
                    zombie.FellOff = true;
                    events.Add(new SimEvent(SimEventType.ZombieFell, 1f, zombie.Id));
                    KillZombie(zombie);
                }
            }
        }

        private void TryDropPotion(Vec2 position)
        {
            if (Config.MaxPotions <= 0 || Config.PotionDropChance <= 0f)
            {
                return;
            }

            if (rng.NextFloat() >= Config.PotionDropChance)
            {
                return;
            }

            float limit = Config.Radius - 0.6f;
            if ((position - Config.Center).LengthSquared > limit * limit)
            {
                return;
            }

            for (int i = 0; i < potions.Count; i++)
            {
                PotionState potion = potions[i];
                if (potion.Active)
                {
                    continue;
                }

                potion.Active = true;
                potion.Id = nextPotionId++;
                potion.Position = position;
                potion.Remaining = Config.PotionLifetime;
                events.Add(new SimEvent(SimEventType.PotionDropped, 0f, potion.Id));
                return;
            }
        }

        private void UpdatePotions()
        {
            float reach = HeroDef.Radius + PotionRadius;
            for (int i = 0; i < potions.Count; i++)
            {
                PotionState potion = potions[i];
                if (!potion.Active)
                {
                    continue;
                }

                if (Hero.Alive && Hero.Hp < HeroDef.MaxHp &&
                    (potion.Position - Hero.Position).LengthSquared <= reach * reach)
                {
                    float healed = MathF.Min(Config.PotionHeal, HeroDef.MaxHp - Hero.Hp);
                    Hero.Hp += healed;
                    potion.Active = false;
                    events.Add(new SimEvent(SimEventType.PotionPicked, healed, potion.Id));
                    continue;
                }

                potion.Remaining -= FixedDeltaTime;
                if (potion.Remaining <= 0f)
                {
                    potion.Active = false;
                }
            }
        }

        private ZombieTypeDef ChooseZombieType()
        {
            float total = 0f;
            for (int i = 0; i < Config.ZombieSpawns.Length; i++)
            {
                total += Config.ZombieSpawns[i].Weight;
            }

            float choice = rng.Range(0f, total);
            for (int i = 0; i < Config.ZombieSpawns.Length; i++)
            {
                choice -= Config.ZombieSpawns[i].Weight;
                if (choice < 0f)
                {
                    return Config.ZombieSpawns[i].Type;
                }
            }

            return Config.ZombieSpawns[Config.ZombieSpawns.Length - 1].Type;
        }

        private void ResetZombie(ZombieState zombie, bool atRim)
        {
            zombie.Position = FindSpawnPosition(zombie.Def.Radius, atRim);
            zombie.Facing = (Hero.Position - zombie.Position).Angle();
            zombie.Velocity = Vec2.Zero;
            zombie.KnockbackVelocity = Vec2.Zero;
            zombie.Hp = zombie.Def.MaxHp * Config.HpMultiplier;
            zombie.Energy = 0f;
            zombie.IsBlocking = false;
            zombie.BlockStartTime = 0f;
            zombie.StunRemaining = 0f;
            zombie.SlowFactor = 1f;
            zombie.SlowRemaining = 0f;
            zombie.AttackPhase = ZombieAttackPhase.Idle;
            zombie.AttackTimer = 0f;
            zombie.Alive = true;
            zombie.FellOff = false;
            zombie.RespawnRemaining = 0f;
        }

        private Vec2 FindSpawnPosition(float radius, bool atRim)
        {
            Vec2 centre = Config.Center;
            float rim = MathF.Max(0f, Config.Radius - radius - 0.2f);
            for (int attempt = 0; attempt < 128; attempt++)
            {
                Vec2 candidate;
                if (atRim)
                {
                    candidate = centre + Vec2.FromAngle(rng.Range(-MathF.PI, MathF.PI)) * rim;
                }
                else
                {
                    // Uniform over the disc (sqrt keeps the density even).
                    float distance = MathF.Sqrt(rng.NextFloat()) * MathF.Max(0f, Config.Radius - 1.5f);
                    candidate = centre + Vec2.FromAngle(rng.Range(-MathF.PI, MathF.PI)) * distance;
                }

                if (Vec2.Distance(candidate, Hero.Position) >= SpawnMinHeroDistance)
                {
                    return candidate;
                }
            }

            Vec2 away = (centre - Hero.Position).Normalized();
            if (away.LengthSquared <= 0f)
            {
                away = new Vec2(1f, 0f);
            }

            return centre + away * rim;
        }

        private bool AllZombiesDead()
        {
            for (int i = 0; i < zombies.Count; i++)
            {
                if (zombies[i].Alive)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vec2 DirectionFromInput(int move)
        {
            return move == 0 ? Vec2.Zero : Vec2.FromAngle((move - 1) * MathF.PI * 0.25f);
        }

        private static Vec2 MoveTowards(Vec2 current, Vec2 target, float maximumDelta)
        {
            Vec2 delta = target - current;
            float length = delta.Length;
            if (length <= maximumDelta || length <= 1e-6f)
            {
                return target;
            }

            return current + delta * (maximumDelta / length);
        }

        private static bool IsWithinArc(float facing, Vec2 offset, float arcDegrees)
        {
            if (offset.LengthSquared <= 1e-12f)
            {
                return true;
            }

            return AngleBetween(facing, offset) <= arcDegrees * DegreesToRadians * 0.5f + 1e-6f;
        }

        private static bool IsBehindTarget(float targetFacing, Vec2 targetPosition, Vec2 attackerPosition)
        {
            return AngleBetween(targetFacing, attackerPosition - targetPosition) >
                120f * DegreesToRadians;
        }

        private static float AngleBetween(float facing, Vec2 direction)
        {
            if (direction.LengthSquared <= 1e-12f)
            {
                return 0f;
            }

            return MathF.Abs(ShortestAngle(direction.Angle() - facing));
        }

        private static float MoveAngleTowards(float current, float target, float maximumDelta)
        {
            float delta = ShortestAngle(target - current);
            if (MathF.Abs(delta) <= maximumDelta)
            {
                return NormalizeAngle(target);
            }

            return NormalizeAngle(current + MathF.Sign(delta) * maximumDelta);
        }

        private static float ShortestAngle(float angle)
        {
            while (angle > MathF.PI)
            {
                angle -= MathF.PI * 2f;
            }

            while (angle < -MathF.PI)
            {
                angle += MathF.PI * 2f;
            }

            return angle;
        }

        private static float NormalizeAngle(float angle) => ShortestAngle(angle);
    }
}
