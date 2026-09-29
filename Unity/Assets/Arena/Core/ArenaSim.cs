using System;
using System.Collections.Generic;

namespace PersonalArena.Core
{
    /// <summary>Headless deterministic 60 Hz arena simulation.</summary>
    public sealed class ArenaSim
    {
        public const float FixedDeltaTime = 1f / 60f;

        private const float DegreesToRadians = MathF.PI / 180f;
        private readonly List<ZombieState> zombies = new List<ZombieState>();
        private readonly List<SimEvent> events = new List<SimEvent>(32);
        private Rng rng;
        private int tick;

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
            events.Capacity = Math.Max(events.Capacity, Config.ZombieCount * 3 + 8);
            rng = new Rng(Config.Seed);
            Reset();
        }

        public HeroState Hero { get; }
        public IReadOnlyList<ZombieState> Zombies => zombies;
        public float Time => tick * FixedDeltaTime;
        public bool Done { get; private set; }
        public IReadOnlyList<SimEvent> Events => events;
        public ArenaConfig Config { get; }
        public HeroClassDef HeroDef { get; }

        public void Reset(int? seed = null)
        {
            Config.Validate();
            rng = new Rng(seed ?? Config.Seed);
            tick = 0;
            Done = false;
            events.Clear();

            Hero.Position = new Vec2(Config.Width * 0.5f, Config.Height * 0.5f);
            Hero.Facing = MathF.PI * 0.5f;
            Hero.Velocity = Vec2.Zero;
            Hero.Hp = HeroDef.MaxHp;
            Hero.Energy = HeroDef.MaxEnergy;
            Hero.IsBlocking = false;
            Hero.BlockStartTime = 0f;
            Hero.StunRemaining = 0f;
            Hero.SlowFactor = 1f;
            Hero.SlowRemaining = 0f;
            Hero.Alive = true;
            Hero.Id = 0;
            for (int i = 0; i < Hero.CooldownRemaining.Length; i++)
            {
                Hero.CooldownRemaining[i] = 0f;
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

            ResolveAllOverlaps();
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
            bool blockDepleted = UpdateBlocking(holdingBlock, heldSkill);

            if (!Hero.IsBlocking && !blockDepleted)
            {
                Hero.Energy = MathF.Min(HeroDef.MaxEnergy, Hero.Energy + HeroDef.EnergyRegenPerSec * FixedDeltaTime);
            }

            if (Hero.StunRemaining > 0f)
            {
                Hero.Velocity = Vec2.Zero;
                return;
            }

            Hero.Facing = NormalizeAngle(Hero.Facing +
                input.Turn * HeroDef.TurnSpeedDegPerSec * DegreesToRadians * FixedDeltaTime);

            Vec2 moveDirection = DirectionFromInput(input.Move);
            float moveMultiplier = Hero.SlowRemaining > 0f ? Hero.SlowFactor : 1f;
            if (Hero.IsBlocking)
            {
                moveMultiplier *= heldSkill.BlockMoveMultiplier;
            }

            Hero.Velocity = moveDirection * (HeroDef.MoveSpeed * moveMultiplier);
            if (moveDirection.LengthSquared > 0f)
            {
                Vec2 before = Hero.Position;
                Hero.Position += Hero.Velocity * FixedDeltaTime;
                if (ClampHero() && Hero.Position.Equals(before))
                {
                    events.Add(new SimEvent(SimEventType.HitWall));
                }
                else if (WasMovementClamped(before, Hero.Velocity))
                {
                    events.Add(new SimEvent(SimEventType.HitWall));
                }
            }

            if (input.Skill > 0 && !holdingBlock)
            {
                UseSkill(input.Skill - 1, heldSkill, moveDirection);
            }
        }

        private bool UpdateBlocking(bool holdingBlock, SkillDef block)
        {
            if (!holdingBlock)
            {
                Hero.IsBlocking = false;
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
                events.Add(new SimEvent(SimEventType.SkillUsed, 0f, -1));
            }

            Hero.Energy = MathF.Max(0f, Hero.Energy - block.EnergyPerSecond * FixedDeltaTime);
            if (Hero.Energy <= 0f)
            {
                Hero.IsBlocking = false;
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
                case SkillKind.AreaBurst:
                case SkillKind.Teleport:
                    throw new NotSupportedException($"Skill kind {skill.Kind} is not implemented in M1.");
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

                Vec2 away = (zombie.Position - Hero.Position).Normalized();
                if (away.LengthSquared <= 0f)
                {
                    away = Vec2.FromAngle(Hero.Facing);
                }

                zombie.Position += away * skill.Knockback;
                ClampZombie(zombie);
                zombie.StunRemaining = MathF.Max(zombie.StunRemaining, skill.StunSeconds);
                zombie.AttackPhase = ZombieAttackPhase.Idle;
                zombie.AttackTimer = 0f;
                zombie.Velocity = Vec2.Zero;
            }
        }

        private void PerformDash(SkillDef skill, Vec2 moveDirection)
        {
            Vec2 direction = moveDirection.LengthSquared > 0f
                ? moveDirection
                : Vec2.FromAngle(Hero.Facing);
            Hero.Position += direction * skill.DashDistance;
            if (ClampHero())
            {
                events.Add(new SimEvent(SimEventType.HitWall));
            }

            ResolveHeroZombieOverlaps();
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
                zombie.Alive = false;
                zombie.Velocity = Vec2.Zero;
                zombie.AttackPhase = ZombieAttackPhase.Idle;
                zombie.AttackTimer = 0f;
                zombie.RespawnRemaining = Config.RespawnKilledZombies ? 1.5f : 0f;
                events.Add(new SimEvent(SimEventType.ZombieKilled, 1f, zombie.Id));
            }
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
                        ResetZombie(zombie, true);
                    }
                }

                return;
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
                    ResolveZombieAttack(zombie);
                    zombie.AttackPhase = ZombieAttackPhase.Recover;
                    zombie.AttackTimer = zombie.Def.AttackCooldown;
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

            float stopDistance = zombie.Def.AttackRange * 0.9f;
            if (toHero.LengthSquared > stopDistance * stopDistance)
            {
                float slow = zombie.SlowRemaining > 0f ? zombie.SlowFactor : 1f;
                zombie.Velocity = toHero.Normalized() *
                    (zombie.Def.MoveSpeed * Config.SpeedMultiplier * slow);
                zombie.Position += zombie.Velocity * FixedDeltaTime;
                ClampZombie(zombie);
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
            bool frontalBlock = Hero.IsBlocking && AngleBetween(Hero.Facing, heroToZombie) <= MathF.PI * 0.5f;
            float damage = zombie.Def.AttackDamage * Config.DamageMultiplier;

            if (frontalBlock)
            {
                SkillDef block = ActiveBlockSkill();
                if (block != null && Time - Hero.BlockStartTime <= block.ParryWindowSeconds + 1e-6f)
                {
                    zombie.StunRemaining = MathF.Max(zombie.StunRemaining, block.StunSeconds);
                    events.Add(new SimEvent(SimEventType.Parry, 1f, zombie.Id));
                    return;
                }

                if (block != null)
                {
                    damage *= block.BlockDamageMultiplier;
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
            if (fromBehind)
            {
                events.Add(new SimEvent(SimEventType.HeroDamagedFromBehind, removed, zombie.Id));
            }

            if (Hero.Hp <= 0f)
            {
                Hero.Hp = 0f;
                Hero.Alive = false;
                Hero.IsBlocking = false;
                Hero.Velocity = Vec2.Zero;
                events.Add(new SimEvent(SimEventType.HeroDied, 1f, zombie.Id));
            }
        }

        private SkillDef ActiveBlockSkill()
        {
            for (int i = 0; i < HeroDef.Skills.Length; i++)
            {
                SkillDef skill = HeroDef.Skills[i];
                if (skill != null && skill.Kind == SkillKind.Block)
                {
                    return skill;
                }
            }

            return null;
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

            ClampHero();
            for (int i = 0; i < zombies.Count; i++)
            {
                if (zombies[i].Alive)
                {
                    ClampZombie(zombies[i]);
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

        private bool ClampHero()
        {
            Vec2 before = Hero.Position;
            Hero.Position = ClampPosition(Hero.Position, HeroDef.Radius);
            return !Hero.Position.Equals(before);
        }

        private bool WasMovementClamped(Vec2 before, Vec2 velocity)
        {
            Vec2 intended = before + velocity * FixedDeltaTime;
            return !Hero.Position.Equals(intended);
        }

        private void ClampZombie(ZombieState zombie)
        {
            zombie.Position = ClampPosition(zombie.Position, zombie.Def.Radius);
        }

        private Vec2 ClampPosition(Vec2 position, float radius)
        {
            return new Vec2(
                Math.Clamp(position.X, radius, Config.Width - radius),
                Math.Clamp(position.Y, radius, Config.Height - radius));
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

        private void ResetZombie(ZombieState zombie, bool wallSide)
        {
            zombie.Position = FindSpawnPosition(zombie.Def.Radius, wallSide);
            zombie.Facing = rng.Range(-MathF.PI, MathF.PI);
            zombie.Velocity = Vec2.Zero;
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
            zombie.RespawnRemaining = 0f;
        }

        private Vec2 FindSpawnPosition(float radius, bool wallSide)
        {
            for (int attempt = 0; attempt < 128; attempt++)
            {
                Vec2 candidate;
                if (wallSide)
                {
                    int side = rng.NextInt(4);
                    if (side == 0)
                    {
                        candidate = new Vec2(radius, rng.Range(radius, Config.Height - radius));
                    }
                    else if (side == 1)
                    {
                        candidate = new Vec2(Config.Width - radius, rng.Range(radius, Config.Height - radius));
                    }
                    else if (side == 2)
                    {
                        candidate = new Vec2(rng.Range(radius, Config.Width - radius), radius);
                    }
                    else
                    {
                        candidate = new Vec2(rng.Range(radius, Config.Width - radius), Config.Height - radius);
                    }
                }
                else
                {
                    candidate = new Vec2(
                        rng.Range(radius, Config.Width - radius),
                        rng.Range(radius, Config.Height - radius));
                }

                if (Vec2.Distance(candidate, Hero.Position) >= 4f)
                {
                    return candidate;
                }
            }

            Vec2[] corners =
            {
                new Vec2(radius, radius),
                new Vec2(Config.Width - radius, radius),
                new Vec2(radius, Config.Height - radius),
                new Vec2(Config.Width - radius, Config.Height - radius)
            };
            Vec2 farthest = corners[0];
            float farthestDistance = Vec2.Distance(farthest, Hero.Position);
            for (int i = 1; i < corners.Length; i++)
            {
                float distance = Vec2.Distance(corners[i], Hero.Position);
                if (distance > farthestDistance)
                {
                    farthest = corners[i];
                    farthestDistance = distance;
                }
            }

            return farthest;
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
