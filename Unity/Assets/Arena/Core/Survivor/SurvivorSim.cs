using System;
using System.Collections.Generic;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed partial class SurvivorSim
    {
        public const float FixedDeltaTime = 1f / 60f;
        public const int EnemyCapacity = 400;
        public const int ProjectileCapacity = 128;
        public const int EnemyProjectileCapacity = 64;
        public const int PickupCapacity = 600;
        public const int GemCapacity = 400;
        public const int ObstacleCapacity = 64;
        private const int BossTypeIndex = 4;
        private const int BruteTypeIndex = 2;
        private const int RunnerTypeIndex = 1;
        /// <summary>Cell size of the obstacle lookup grid (m).</summary>
        private const float ObstacleCellSize = 4f;
        /// <summary>Bodies up to this radius use the obstacle grid; larger ones scan every obstacle.</summary>
        private const float ObstacleGridBodyRadius = 2f;

        private readonly SurvivorEnemy[] enemies = CreateEnemies();
        private readonly SurvivorProjectile[] projectiles = CreateProjectiles();
        private readonly SurvivorEnemyProjectile[] enemyProjectiles = CreateEnemyProjectiles();
        private readonly SurvivorPickup[] pickups = CreatePickups();
        private readonly SurvivorObstacle[] obstacles = CreateObstacles();
        private readonly List<SurvivorEvent> events = new List<SurvivorEvent>(4096);
        private readonly SurvivorInventory inventory = new SurvivorInventory();
        private readonly SurvivorDerivedStats stats = new SurvivorDerivedStats();
        private readonly int[] offers = new int[4];
        private readonly int[] offerLevels = new int[4];
        private readonly int[] candidates = new int[SurvivorCatalog.CatalogSize];
        private readonly bool[] moveMask = new bool[SurvivorInput.MoveBranchSize];
        private readonly bool[] skillMask = new bool[SurvivorInput.SkillBranchSize];
        private readonly bool[] pickMask = new bool[SurvivorInput.PickBranchSize];
        private readonly float[] weaponCooldowns = new float[SurvivorCatalog.CatalogSize];
        private readonly int[] hammerTargetIds = new int[8];
        private readonly int[] enemyScratch = new int[EnemyCapacity];
        private readonly Vec2[] enemyPreviousPositions = new Vec2[EnemyCapacity];
        private Rng rng;
        private SpatialHash spatialHash;
        private int[] obstacleCellStart = Array.Empty<int>();
        private int[] obstacleCellFill = Array.Empty<int>();
        private int[] obstacleCellItems = Array.Empty<int>();
        private int obstacleGridSide;
        private float spawnAccumulator;
        private int nextEnemyId;
        private int nextProjectileId;
        private int nextEnemyProjectileId;
        private int pendingLevelUps;
        private int nextEliteIndex;
        private bool bossSpawned;
        private SurvivorEnemy bossEnemy;
        private int aliveEnemyCount;
        private int aliveNormalCount;
        /// <summary>Largest radius of any enemy spawned this run; only grows until Reset.</summary>
        private float maxEnemyRadius;
        /// <summary>Total knockback distance since the last spatial hash rebuild (widens hash queries).</summary>
        private float hashDrift;
        private int activeObstacleCount;
        private int enemyLimit;
        private int projectileLimit;
        private int enemyProjectileLimit;
        private int pickupLimit;
        private bool testInvulnerable;
        private bool testEnemiesInvulnerable;
        private bool testDisablePickupCollection;
        /// <summary>Tier 3+: whether the extra 90 s elite has spawned this run.</summary>
        private bool earlyEliteSpawned;
        private readonly float[] goldBySource = new float[GoldSourceCount];
        public const int GoldSourceCount = 5;

        public SurvivorSim(SurvivorConfig config, int seed)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Config.Validate();
            Hero = new SurvivorHero();
            SkillUses = new int[4];
            spatialHash = new SpatialHash(Config.MapHalfSize, EnemyCapacity);
            Reset(seed);
        }

        public SurvivorConfig Config { get; }
        public SurvivorHero Hero { get; }
        public SurvivorDerivedStats DerivedStats => stats;
        public float Time { get; private set; }
        public int Level { get; private set; }
        public float Xp { get; private set; }
        public int XpToNext => XpCurve.Required(Level);
        public float Gold { get; private set; }
        public int Kills { get; private set; }
        public int EliteKills { get; private set; }
        public float TotalXp { get; private set; }
        public float DamageTaken { get; private set; }
        public float DamageDealtTotal { get; private set; }
        public float BossDamageFraction { get; private set; }
        public float MinHpRatio { get; private set; }
        public float MinHpTime { get; private set; }
        public int[] SkillUses { get; }
        public int DropsSpawned { get; private set; }
        public int DropsCollected { get; private set; }
        public EndReason EndReason { get; private set; }
        public DeathCause DeathCause { get; private set; }
        /// <summary>
        /// Kind of the last enemy hit taken (Boss, Brute for a brute swing, Projectile, else Contact);
        /// None until the hero is first hit. Used by the run story; does not change the sim.
        /// </summary>
        public DeathCause LastHitCause { get; private set; }
        public bool IsEnded => EndReason != EndReason.None;
        public bool IsAwaitingPick => OfferCount > 0;
        public int OfferCount { get; private set; }
        public SurvivorInventory Inventory => inventory;
        public IReadOnlyList<SurvivorEnemy> Enemies => enemies;
        public IReadOnlyList<SurvivorProjectile> Projectiles => projectiles;
        public IReadOnlyList<SurvivorEnemyProjectile> EnemyProjectiles => enemyProjectiles;
        public IReadOnlyList<SurvivorPickup> Pickups => pickups;
        public IReadOnlyList<SurvivorObstacle> Obstacles => obstacles;
        public IReadOnlyList<SurvivorEvent> Events => events;
        public int LastMove { get; private set; }
        /// <summary>Skill branch value that was actually applied last tick (0 when none, masked or failed).</summary>
        public int LastSkill { get; private set; }
        public float LastStepSeconds { get; private set; }
        public bool BossAlive => BossEnemy != null;
        public SurvivorEnemy BossEnemy => bossEnemy != null && bossEnemy.Active ? bossEnemy : null;
        public int AliveEnemyCount => aliveEnemyCount;
        internal SurvivorEnemy[] EnemyPool => enemies;
        internal SurvivorPickup[] PickupPool => pickups;
        internal SurvivorObstacle[] ObstaclePool => obstacles;
        internal int ActiveObstacleCount => activeObstacleCount;
        internal int EnemyLimit => enemyLimit;
        internal int PickupLimit => pickupLimit;
        public int EnemyProjectileLimit => enemyProjectileLimit;
        internal SurvivorEnemyProjectile[] EnemyProjectilePool => enemyProjectiles;

        public (int CatalogIndex, int NextLevel) GetOffer(int index)
        {
            if (index < 0 || index >= OfferCount) throw new ArgumentOutOfRangeException(nameof(index));
            return (offers[index], offerLevels[index]);
        }

        public void Reset(int seed)
        {
            Config.Validate();
            rng = new Rng(seed);
            if (MathF.Abs(Config.MapHalfSize * 2f / SpatialHash.CellSize - spatialHash.Side) > 0.01f)
                spatialHash = new SpatialHash(Config.MapHalfSize, EnemyCapacity);
            ClearPools();
            GenerateObstacles();
            BuildObstacleGrid();
            inventory.Clear();
            inventory.Set(Config.ClassDef.StartingWeapon, 1);
            Array.Clear(weaponCooldowns, 0, weaponCooldowns.Length);
            Array.Clear(SkillUses, 0, SkillUses.Length);
            Time = 0f; Level = 1; Xp = 0f; Gold = 0f; Kills = 0; EliteKills = 0;
            TotalXp = 0f; DamageTaken = 0f; DamageDealtTotal = 0f; BossDamageFraction = 0f;
            DropsSpawned = 0; DropsCollected = 0; MinHpRatio = 1f; MinHpTime = 0f;
            EndReason = EndReason.None; DeathCause = DeathCause.None; LastHitCause = DeathCause.None; OfferCount = 0;
            earlyEliteSpawned = false; Array.Clear(goldBySource, 0, goldBySource.Length);
            pendingLevelUps = 0; spawnAccumulator = 0f; nextEnemyId = 1; nextProjectileId = 1; nextEnemyProjectileId = 1;
            nextEliteIndex = 0; bossSpawned = false; LastMove = 0; LastSkill = 0; LastStepSeconds = 0f;
            bossEnemy = null; aliveEnemyCount = 0; aliveNormalCount = 0; maxEnemyRadius = 0f; hashDrift = 0f;
            Hero.Position = Vec2.Zero; Hero.Velocity = Vec2.Zero; Hero.Facing = 0f;
            Hero.MaxEnergy = Config.ClassDef.MaxEnergy; Hero.Energy = Hero.MaxEnergy;
            Hero.Radius = Config.ClassDef.Radius; Hero.Alive = true; Hero.Blocking = false;
            Hero.Dashing = false; Hero.StunRemaining = 0f; Hero.BlockSkill = null; Hero.DashSkill = null;
            Array.Clear(Hero.SkillCooldowns, 0, Hero.SkillCooldowns.Length);
            RecomputeStats(false); Hero.Hp = Hero.MaxHp;
            events.Clear();
            SpawnOpeningRing();
        }

        /// <summary>
        /// Advances one fixed tick. Once the run ends (won, died, time up, expired) the remaining
        /// phases of the tick are skipped, so no hero damage, death or spawn can follow the end event.
        /// </summary>
        public void Step(SurvivorInput input)
        {
            events.Clear(); LastStepSeconds = 0f;
            if (IsEnded) return;
            if (IsAwaitingPick)
            {
                HandlePick(input.Pick);
                return;
            }

            LastStepSeconds = FixedDeltaTime;
            CaptureEnemyPositions();
            Vec2 heroStart = Hero.Position;
            teleportShift = Vec2.Zero;
            LastMove = input.Move >= 0 && input.Move < SurvivorInput.MoveBranchSize ? input.Move : 0;
            Time += FixedDeltaTime;
            TickCooldowns();
            UpdateFacing();
            ApplySkill(input);
            MoveHero(input.Move);
            SpawnScheduledEnemies();
            FireWeapons();
            if (!IsEnded) UpdateProjectiles();
            if (!IsEnded) UpdateEnemies();
            if (!IsEnded) UpdateEnemyProjectiles();
            if (!IsEnded) ResolveBodyCollisions();
            FinalizeEnemyVelocities();
            Hero.Velocity = (Hero.Position - heroStart - teleportShift) / FixedDeltaTime;
            if (IsEnded) return;
            UpdatePickups();
            Regenerate();
            CheckRunEnd();
            if (!IsEnded && pendingLevelUps > 0 && OfferCount == 0) OpenOffer();
        }

        private void TickCooldowns()
        {
            for (int i = 0; i < Hero.SkillCooldowns.Length; i++) Hero.SkillCooldowns[i] = MathF.Max(0f, Hero.SkillCooldowns[i] - FixedDeltaTime);
            for (int i = 0; i < weaponCooldowns.Length; i++)
            {
                if (i == orbitWeaponIndex && OrbitAxeCount > 0) continue;
                weaponCooldowns[i] = MathF.Max(0f, weaponCooldowns[i] - FixedDeltaTime);
            }
            Hero.StunRemaining = MathF.Max(0f, Hero.StunRemaining - FixedDeltaTime);
        }

        private void ApplySkill(SurvivorInput input)
        {
            LastSkill = 0;
            SurvivorActionMask.WriteMask(this, moveMask, skillMask, pickMask);
            int selected = input.Skill;
            if (selected < 0 || selected >= skillMask.Length || !skillMask[selected])
            {
                if (selected > 0) AddEvent(SurvivorEventType.SkillFailed, selected - 1);
                Hero.Blocking = false;
                return;
            }
            if (selected == 0) { Hero.Blocking = false; return; }
            int slot = selected - 1;
            SkillDef skill = Config.ClassDef.ActiveSkills[slot];
            LastSkill = selected;
            if (skill.Kind == SkillKind.Block)
            {
                if (!Hero.Blocking)
                {
                    Hero.Energy -= skill.EnergyCost;
                    Hero.BlockStarted = Time; Hero.BlockSkill = skill;
                    SkillUses[slot]++;
                    AddEvent(SurvivorEventType.SkillUsed, slot);
                }
                Hero.Blocking = true;
                Hero.Energy = MathF.Max(0f, Hero.Energy - skill.EnergyPerSecond * FixedDeltaTime);
                if (Hero.Energy <= 0f) Hero.Blocking = false;
                return;
            }
            Hero.Blocking = false;
            Hero.Energy -= skill.EnergyCost;
            Hero.SkillCooldowns[slot] = skill.Cooldown;
            SkillUses[slot]++;
            int hits = 0;
            if (skill.Kind == SkillKind.Kick) hits = Kick(skill);
            else if (skill.Kind == SkillKind.Dash)
            {
                Vec2 direction = skill.DashBackward ? Vec2.FromAngle(Hero.Facing + MathF.PI) : MoveDirection(input.Move);
                if (direction.LengthSquared < 0.01f) direction = Vec2.FromAngle(Hero.Facing);
                Hero.Dashing = true; Hero.DashDirection = direction; Hero.DashRemaining = skill.DashDistance; Hero.DashSkill = skill;
            }
            else if (skill.Kind == SkillKind.Projectile) FireSkillProjectile(slot, skill);
            else if (skill.Kind == SkillKind.AreaBurst) hits = AreaBurst(skill);
            else if (skill.Kind == SkillKind.Teleport) Teleport(skill, input.Move);
            AddEvent(SurvivorEventType.SkillUsed, slot, hits);
        }

        /// <summary>
        /// Moves the hero. Velocity set here is provisional (used by relocation this tick);
        /// <see cref="Step"/> replaces it with the actual motion after body separation.
        /// </summary>
        private void MoveHero(int move)
        {
            if (Hero.StunRemaining > 0f) { Hero.Velocity = Vec2.Zero; return; }
            Vec2 old = Hero.Position;
            if (Hero.Dashing)
            {
                float distance = MathF.Min(Hero.DashRemaining, Hero.DashSkill.DashSpeed * FixedDeltaTime);
                Hero.Position += Hero.DashDirection * distance;
                Hero.DashRemaining -= distance;
                if (Hero.DashRemaining <= 0.0001f) Hero.Dashing = false;
            }
            else
            {
                Vec2 target = MoveDirection(move) * stats.MoveSpeed * (Hero.Blocking && Hero.BlockSkill != null ? Hero.BlockSkill.BlockMoveMultiplier : 1f);
                Vec2 delta = target - Hero.Velocity;
                float maxChange = Config.ClassDef.Acceleration * FixedDeltaTime;
                if (delta.Length > maxChange) delta = delta.Normalized() * maxChange;
                Hero.Velocity += delta;
                Hero.Position += Hero.Velocity * FixedDeltaTime;
            }
            Vec2 corrected = Hero.Position;
            ClampAndPushOut(ref corrected, Hero.Radius);
            Hero.Position = corrected;
            Hero.Velocity = (Hero.Position - old) / FixedDeltaTime;
        }

        private void UpdateFacing()
        {
            Vec2 direction = Vec2.Zero;
            float best = Config.Tuning.FacingRange * Config.Tuning.FacingRange;
            for (int i = 0; i < enemyLimit; i++)
            {
                if (!enemies[i].Active) continue;
                Vec2 delta = enemies[i].Position - Hero.Position;
                if (delta.LengthSquared < best) { best = delta.LengthSquared; direction = delta; }
            }
            if (direction.LengthSquared < 0.001f && Hero.Velocity.LengthSquared > 0.001f) direction = Hero.Velocity;
            if (direction.LengthSquared < 0.001f) return;
            Hero.Facing = TurnToward(Hero.Facing, direction.Angle(), Config.Tuning.HeroTurnRateDegPerSec * MathF.PI / 180f * FixedDeltaTime);
        }

        private void Regenerate()
        {
            if (!Hero.Alive) return;
            if (!Hero.Blocking) Hero.Energy = MathF.Min(Hero.MaxEnergy, Hero.Energy + Config.ClassDef.EnergyRegen * FixedDeltaTime);
            Heal(stats.Regen * FixedDeltaTime, false);
            float ratio = Hero.MaxHp > 0f ? Hero.Hp / Hero.MaxHp : 0f;
            if (ratio < MinHpRatio) { MinHpRatio = ratio; MinHpTime = Time; }
        }

        private void CheckRunEnd()
        {
            float bossTime = Config.Tuning.BossSpawnSeconds;
            if (Config.RunSeconds < bossTime && Time >= Config.RunSeconds)
            {
                EndReason = EndReason.TimeUp; AddEvent(SurvivorEventType.RunTimeUp); return;
            }
            if (Config.RunSeconds < bossTime) return;
            // A failed boss spawn (enemy pool full) is retried every tick until the expiry time.
            if (!bossSpawned && Time >= bossTime) SpawnBoss();
            if (Time >= Config.BossExpireSeconds && (BossAlive || !bossSpawned))
            {
                EndReason = EndReason.Expired; AddEvent(SurvivorEventType.RunExpired);
            }
        }

        private void RecomputeStats(bool preserveHpGain)
        {
            float oldMax = Hero.MaxHp;
            CharacterBuild b = Config.Build;
            SurvivorClassDef c = Config.ClassDef;
            stats.MaxHp = c.MaxHp * (1f + Passive(SurvivorCatalog.IronHeartIndex) + Point(b, StatId.MaxHp));
            stats.Armor = c.Armor + Passive(SurvivorCatalog.BoneArmorIndex) + Point(b, StatId.Armor);
            stats.Regen = c.Regen + Point(b, StatId.Regen);
            stats.Might = 1f + Passive(SurvivorCatalog.MightGauntletIndex) + Point(b, StatId.Might);
            stats.CritChance = MathF.Min(1f, c.CritChance + Passive(SurvivorCatalog.CritEyeIndex) + Point(b, StatId.Crit));
            stats.CritDamage = c.CritDamage + Point(b, StatId.CritDamage);
            // Hourglass lowers the cooldown multiplier; the Cooldown stat's per-point value is already negative.
            stats.CooldownMul = MathF.Max(0.4f, 1f - Passive(SurvivorCatalog.HourglassIndex) + Point(b, StatId.Cooldown));
            stats.AreaMul = 1f + Passive(SurvivorCatalog.AreaCharmIndex) + Point(b, StatId.Area);
            stats.MoveSpeed = c.MoveSpeed * (1f + Passive(SurvivorCatalog.WindBootsIndex) + Point(b, StatId.MoveSpeed));
            stats.PickupRadius = c.PickupRadius * (1f + Passive(SurvivorCatalog.MagnetCharmIndex) + Point(b, StatId.Magnet));
            stats.Luck = Point(b, StatId.Luck);
            stats.GreedMul = 1f + Point(b, StatId.Greed);
            stats.GrowthMul = 1f + Point(b, StatId.Growth);
            stats.TierGold = 1f + 0.5f * (b.Tier - 1);
            Hero.MaxHp = stats.MaxHp;
            if (preserveHpGain && stats.MaxHp > oldMax) Hero.Hp += stats.MaxHp - oldMax;
            Hero.Hp = MathF.Min(Hero.Hp, Hero.MaxHp);
        }

        private float Passive(int index) => SurvivorCatalog.PassivePerLevel(index) * inventory.Level(index);
        private static float Point(CharacterBuild build, StatId stat) => StatInfo.PerPoint(stat) * build.Points[(int)stat];

        private void ClearPools()
        {
            for (int i = 0; i < enemies.Length; i++) { enemies[i].Active = false; enemies[i].Separated = false; }
            for (int i = 0; i < projectiles.Length; i++) projectiles[i].Active = false;
            for (int i = 0; i < enemyProjectiles.Length; i++) enemyProjectiles[i].Active = false;
            for (int i = 0; i < pickups.Length; i++) pickups[i].Active = false;
            for (int i = 0; i < obstacles.Length; i++) obstacles[i].Active = false;
            activeObstacleCount = 0; gemCount = 0;
            enemyLimit = 0; projectileLimit = 0; enemyProjectileLimit = 0; pickupLimit = 0;
            ResetContentState();
            testInvulnerable = false;
            testEnemiesInvulnerable = false;
            testDisablePickupCollection = false;
        }

        private void GenerateObstacles()
        {
            int made = 0;
            for (int attempt = 0; attempt < Config.ObstacleCount * Config.Tuning.ObstacleAttemptsPerObstacle && made < Config.ObstacleCount; attempt++)
            {
                float radius = rng.Range(Config.ObstacleRadiusMin, Config.ObstacleRadiusMax);
                Vec2 point = new Vec2(rng.Range(-Config.MapHalfSize + radius, Config.MapHalfSize - radius), rng.Range(-Config.MapHalfSize + radius, Config.MapHalfSize - radius));
                if (point.Length < Config.ObstacleClearRadius + radius) continue;
                bool overlap = false;
                for (int j = 0; j < made; j++) overlap |= Vec2.Distance(point, obstacles[j].Position) < radius + obstacles[j].Radius;
                if (overlap) continue;
                obstacles[made].Active = true; obstacles[made].Position = point; obstacles[made].Radius = radius; made++;
            }
            activeObstacleCount = made;
        }

        /// <summary>
        /// Builds per-cell obstacle lists (ascending obstacle index). An obstacle is listed in every cell
        /// whose square lies within its radius + <see cref="ObstacleGridBodyRadius"/>, so any body of radius
        /// up to that bound that overlaps the obstacle is in a cell that lists it. Allocates only in Reset.
        /// </summary>
        private void BuildObstacleGrid()
        {
            int side = Math.Max(1, (int)MathF.Ceiling(Config.MapHalfSize * 2f / ObstacleCellSize));
            int cells = side * side;
            obstacleGridSide = side;
            if (obstacleCellStart.Length < cells + 1) { obstacleCellStart = new int[cells + 1]; obstacleCellFill = new int[cells]; }
            Array.Clear(obstacleCellStart, 0, cells + 1);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < activeObstacleCount; i++)
                {
                    float reach = obstacles[i].Radius + ObstacleGridBodyRadius + 0.01f;
                    Vec2 center = obstacles[i].Position;
                    int minX = ObstacleCellCoord(center.X - reach), maxX = ObstacleCellCoord(center.X + reach);
                    int minY = ObstacleCellCoord(center.Y - reach), maxY = ObstacleCellCoord(center.Y + reach);
                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            int cell = y * side + x;
                            if (pass == 0) obstacleCellStart[cell + 1]++;
                            else obstacleCellItems[obstacleCellFill[cell]++] = i;
                        }
                    }
                }
                if (pass == 0)
                {
                    for (int cell = 0; cell < cells; cell++) obstacleCellStart[cell + 1] += obstacleCellStart[cell];
                    if (obstacleCellItems.Length < obstacleCellStart[cells]) obstacleCellItems = new int[obstacleCellStart[cells]];
                    Array.Copy(obstacleCellStart, obstacleCellFill, cells);
                }
            }
        }

        private int ObstacleCellCoord(float coordinate)
        {
            int value = (int)MathF.Floor((coordinate + Config.MapHalfSize) / ObstacleCellSize);
            return value < 0 ? 0 : value >= obstacleGridSide ? obstacleGridSide - 1 : value;
        }

        private int ObstacleCell(Vec2 point) => ObstacleCellCoord(point.Y) * obstacleGridSide + ObstacleCellCoord(point.X);

        /// <summary>
        /// Clamps a body inside the map and pushes it out of obstacles, in obstacle index order.
        /// Same result as scanning every obstacle: until the first push the point cannot overlap an
        /// obstacle outside its cell list, and after a push the remaining obstacles are scanned in full.
        /// </summary>
        private void ClampAndPushOut(ref Vec2 point, float radius)
        {
            float limit = Config.MapHalfSize - radius;
            point = new Vec2(MathF.Max(-limit, MathF.Min(limit, point.X)), MathF.Max(-limit, MathF.Min(limit, point.Y)));
            if (activeObstacleCount == 0) return;
            if (radius > ObstacleGridBodyRadius) { PushOutFrom(ref point, radius, 0); return; }
            int cell = ObstacleCell(point);
            int end = obstacleCellStart[cell + 1];
            for (int n = obstacleCellStart[cell]; n < end; n++)
            {
                int i = obstacleCellItems[n];
                if (PushOut(ref point, radius, i)) { PushOutFrom(ref point, radius, i + 1); return; }
            }
        }

        private void PushOutFrom(ref Vec2 point, float radius, int first)
        {
            for (int i = first; i < activeObstacleCount; i++) PushOut(ref point, radius, i);
        }

        private bool PushOut(ref Vec2 point, float radius, int i)
        {
            SurvivorObstacle obstacle = obstacles[i];
            if (!obstacle.Active) return false;
            Vec2 delta = point - obstacle.Position;
            float needed = radius + obstacle.Radius;
            if (delta.LengthSquared >= needed * needed) return false;
            Vec2 direction = delta.LengthSquared > 1e-8f ? delta.Normalized() : Vec2.FromAngle((i + 1) * 1.618f);
            point = obstacle.Position + direction * needed;
            return true;
        }

        /// <summary>True when a disc at <paramref name="point"/> comes closer than <paramref name="clearance"/> to an obstacle's edge.</summary>
        private bool OverlapsObstacle(Vec2 point, float clearance)
        {
            if (activeObstacleCount == 0) return false;
            if (clearance > ObstacleGridBodyRadius)
            {
                for (int i = 0; i < activeObstacleCount; i++) if (Blocks(i, point, clearance)) return true;
                return false;
            }
            int cell = ObstacleCell(point);
            int end = obstacleCellStart[cell + 1];
            for (int n = obstacleCellStart[cell]; n < end; n++) if (Blocks(obstacleCellItems[n], point, clearance)) return true;
            return false;
        }

        private bool Blocks(int i, Vec2 point, float clearance)
        {
            if (!obstacles[i].Active) return false;
            float needed = obstacles[i].Radius + clearance;
            return (point - obstacles[i].Position).LengthSquared < needed * needed;
        }

        private void RebuildHash()
        {
            spatialHash.Rebuild(enemies, enemyLimit);
            hashDrift = 0f;
        }

        private static Vec2 MoveDirection(int move) => move <= 0 || move >= 9 ? Vec2.Zero : Vec2.FromAngle((move - 1) * MathF.PI / 4f);
        private static float TurnToward(float current, float target, float maximum)
        {
            float delta = WrapAngle(target - current);
            if (delta > maximum) delta = maximum; else if (delta < -maximum) delta = -maximum;
            return WrapAngle(current + delta);
        }
        private static float WrapAngle(float angle) { while (angle > MathF.PI) angle -= MathF.PI * 2f; while (angle < -MathF.PI) angle += MathF.PI * 2f; return angle; }
        private static SurvivorEnemy[] CreateEnemies() { SurvivorEnemy[] a = new SurvivorEnemy[EnemyCapacity]; for (int i = 0; i < a.Length; i++) a[i] = new SurvivorEnemy(); return a; }
        private static SurvivorProjectile[] CreateProjectiles() { SurvivorProjectile[] a = new SurvivorProjectile[ProjectileCapacity]; for (int i = 0; i < a.Length; i++) a[i] = new SurvivorProjectile(); return a; }
        private static SurvivorEnemyProjectile[] CreateEnemyProjectiles() { SurvivorEnemyProjectile[] a = new SurvivorEnemyProjectile[EnemyProjectileCapacity]; for (int i = 0; i < a.Length; i++) a[i] = new SurvivorEnemyProjectile(); return a; }
        private static SurvivorPickup[] CreatePickups() { SurvivorPickup[] a = new SurvivorPickup[PickupCapacity]; for (int i = 0; i < a.Length; i++) a[i] = new SurvivorPickup(); return a; }
        private static SurvivorObstacle[] CreateObstacles() { SurvivorObstacle[] a = new SurvivorObstacle[ObstacleCapacity]; for (int i = 0; i < a.Length; i++) a[i] = new SurvivorObstacle(); return a; }
        private void AddEvent(SurvivorEventType type, float value = 0f, float extra = 0f, int id = -1, Vec2 point = default) => events.Add(new SurvivorEvent(type, value, extra, id, point));

        /// <summary>Gold of this run that came from <paramref name="source"/>; the five sources sum to <see cref="Gold"/>.</summary>
        public float GetGold(GoldSource source)
        {
            int index = (int)source;
            return index >= 0 && index < goldBySource.Length ? goldBySource[index] : 0f;
        }

        private void AddGold(float amount, GoldSource source)
        {
            Gold += amount;
            goldBySource[(int)source] += amount;
        }

        /// <summary>Number of active enemies touching the hero (same rule as the Surrounded death cause).</summary>
        public int TouchingEnemyCount()
        {
            float margin = Config.Tuning.ContactMargin;
            int touching = 0;
            for (int i = 0; i < enemyLimit; i++) if (enemies[i].Active && Vec2.Distance(enemies[i].Position, Hero.Position) <= Hero.Radius + enemies[i].Radius + margin) touching++;
            return touching;
        }

        private bool Has(TierModifier modifier) => TierModifiers.Has(Config.Build.Tier, modifier);

        /// <summary>Meat drop chance after the LessMeat modifier.</summary>
        internal float EffectiveMeatChance => Has(TierModifier.LessMeat) ? Config.Tuning.MeatChance * Config.Tuning.LessMeatMul : Config.Tuning.MeatChance;

        /// <summary>Boss summon interval after the BossSummonsFaster modifier.</summary>
        internal float EffectiveBossSummonInterval => Has(TierModifier.BossSummonsFaster) ? Config.Tuning.BossSummonIntervalSeconds * Config.Tuning.BossSummonFasterMul : Config.Tuning.BossSummonIntervalSeconds;

        /// <summary>Elites spawned at each scheduled elite time (DoubleElites modifier).</summary>
        internal int ElitesPerScheduledTime => Has(TierModifier.DoubleElites) ? Config.Tuning.DoubleEliteCount : 1;

        internal void SetTimeForTests(float time) { Time = time; }
        internal void SetHeroInvulnerableForTests() { testInvulnerable = true; }
        internal void SetEnemiesInvulnerableForTests() { testEnemiesInvulnerable = true; }
        internal void DisablePickupCollectionForTests() { testDisablePickupCollection = true; }
        internal SurvivorEnemy SpawnEnemyForTests(int type, Vec2 position, bool elite = false) => SpawnEnemy(type, position, elite, type == BossTypeIndex);
        internal void DamageEnemyForTests(SurvivorEnemy enemy, float damage) => DamageEnemy(enemy, damage, 0f, Vec2.Zero);
        internal void GiveXpForTests(float xp) => CollectXp(xp);
        internal void SetXpForTests(float xp) { Xp = xp; }
        internal void SpawnGemForTests(Vec2 point, float value) => SpawnGem(point, value);
        internal void GiveItemForTests(int index, int level) { inventory.Set(index, level); RecomputeStats(true); }
        internal void SetHeroHpForTests(float hp) { Hero.Hp = MathF.Min(Hero.MaxHp, hp); }
        internal void SetHeroStateForTests(Vec2 position, Vec2 velocity, float facing) { Hero.Position = position; Hero.Velocity = velocity; Hero.Facing = facing; }
        internal void SetEnemyVelocityForTests(SurvivorEnemy enemy, Vec2 velocity) { enemy.Velocity = velocity; }
        internal void DamageHeroForTests(float raw, SurvivorEnemy source, bool contact = true) => DamageHero(raw, source, contact);
        internal SurvivorPickup SpawnPickupForTests(PickupKind kind, Vec2 point, float value, GoldSource source = GoldSource.Normal) => SpawnPickup(kind, point, value, false, source);
        internal SurvivorEnemy SpawnBossForTests(Vec2 position) => SpawnEnemy(BossTypeIndex, position, false, true);
        internal float SummonCooldownForTests(SurvivorEnemy enemy) => enemy.SummonCooldown;
        internal SurvivorEnemyProjectile SpawnEnemyProjectileForTests(Vec2 point, Vec2 velocity, float radius, float damage, float lifetime, int sourceId = -1) =>
            SpawnEnemyProjectile(point, velocity, radius, damage, lifetime, sourceId);
        internal float WeaponCooldownForTests(int index) => weaponCooldowns[index];
        internal void SetWeaponCooldownForTests(int index, float value) => weaponCooldowns[index] = value;
        internal bool RollMagnetDropForTests() => RollMagnetDrop();

        private void CaptureEnemyPositions()
        {
            for (int i = 0; i < enemyLimit; i++) if (enemies[i].Active) { enemyPreviousPositions[i] = enemies[i].Position; enemies[i].RelocatedThisTick = false; }
        }

        private void FinalizeEnemyVelocities()
        {
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy enemy = enemies[i];
                if (enemy.Active) enemy.Velocity = enemy.RelocatedThisTick ? Vec2.Zero : (enemy.Position - enemyPreviousPositions[i]) / FixedDeltaTime;
            }
        }
    }
}
