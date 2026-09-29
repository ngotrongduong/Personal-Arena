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
        public const int PickupCapacity = 600;
        public const int GemCapacity = 400;
        public const int ObstacleCapacity = 64;

        private readonly SurvivorEnemy[] enemies = CreateEnemies();
        private readonly SurvivorProjectile[] projectiles = CreateProjectiles();
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
        private readonly int[] hammerTargetIds = new int[3];
        private readonly Vec2[] enemyPreviousPositions = new Vec2[EnemyCapacity];
        private Rng rng;
        private SpatialHash spatialHash;
        private float spawnAccumulator;
        private int nextEnemyId;
        private int nextProjectileId;
        private int pendingLevelUps;
        private int nextEliteIndex;
        private bool bossSpawned;
        private int activeObstacleCount;
        private int enemyLimit;
        private int projectileLimit;
        private int pickupLimit;
        private bool testInvulnerable;
        private bool testEnemiesInvulnerable;
        private bool testDisablePickupCollection;

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
        public bool IsEnded => EndReason != EndReason.None;
        public bool IsAwaitingPick => OfferCount > 0;
        public int OfferCount { get; private set; }
        public SurvivorInventory Inventory => inventory;
        public IReadOnlyList<SurvivorEnemy> Enemies => enemies;
        public IReadOnlyList<SurvivorProjectile> Projectiles => projectiles;
        public IReadOnlyList<SurvivorPickup> Pickups => pickups;
        public IReadOnlyList<SurvivorObstacle> Obstacles => obstacles;
        public IReadOnlyList<SurvivorEvent> Events => events;
        public int LastMove { get; private set; }
        public int LastSkill { get; private set; }
        public float LastStepSeconds { get; private set; }
        public bool BossAlive => BossEnemy != null;
        public SurvivorEnemy BossEnemy { get { for (int i = 0; i < enemyLimit; i++) if (enemies[i].Active && enemies[i].IsBoss) return enemies[i]; return null; } }
        public int AliveEnemyCount { get { int count = 0; for (int i = 0; i < enemyLimit; i++) if (enemies[i].Active) count++; return count; } }
        internal SurvivorEnemy[] EnemyPool => enemies;
        internal SurvivorPickup[] PickupPool => pickups;
        internal SurvivorObstacle[] ObstaclePool => obstacles;
        internal int ActiveObstacleCount => activeObstacleCount;
        internal int EnemyLimit => enemyLimit;
        internal int PickupLimit => pickupLimit;

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
            inventory.Clear();
            inventory.Set(Config.ClassDef.StartingWeapon, 1);
            Array.Clear(weaponCooldowns, 0, weaponCooldowns.Length);
            Array.Clear(SkillUses, 0, SkillUses.Length);
            Time = 0f; Level = 1; Xp = 0f; Gold = 0f; Kills = 0; EliteKills = 0;
            TotalXp = 0f; DamageTaken = 0f; DamageDealtTotal = 0f; BossDamageFraction = 0f;
            DropsSpawned = 0; DropsCollected = 0; MinHpRatio = 1f; MinHpTime = 0f;
            EndReason = EndReason.None; DeathCause = DeathCause.None; OfferCount = 0;
            pendingLevelUps = 0; spawnAccumulator = 0f; nextEnemyId = 1; nextProjectileId = 1;
            nextEliteIndex = 0; bossSpawned = false; LastMove = 0; LastSkill = 0; LastStepSeconds = 0f;
            Hero.Position = Vec2.Zero; Hero.Velocity = Vec2.Zero; Hero.Facing = 0f;
            Hero.MaxEnergy = Config.ClassDef.MaxEnergy; Hero.Energy = Hero.MaxEnergy;
            Hero.Radius = Config.ClassDef.Radius; Hero.Alive = true; Hero.Blocking = false;
            Hero.Dashing = false; Hero.StunRemaining = 0f;
            Array.Clear(Hero.SkillCooldowns, 0, Hero.SkillCooldowns.Length);
            RecomputeStats(false); Hero.Hp = Hero.MaxHp;
            events.Clear();
        }

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
            LastMove = input.Move >= 0 && input.Move < SurvivorInput.MoveBranchSize ? input.Move : 0;
            LastSkill = input.Skill >= 0 && input.Skill < SurvivorInput.SkillBranchSize ? input.Skill : 0;
            Time += FixedDeltaTime;
            TickCooldowns();
            UpdateFacing();
            ApplySkill(input);
            MoveHero(input.Move);
            SpawnScheduledEnemies();
            FireWeapons();
            UpdateProjectiles();
            UpdateEnemies();
            ResolveBodyCollisions();
            FinalizeEnemyVelocities();
            if (IsEnded) return;
            UpdatePickups();
            Regenerate();
            CheckRunEnd();
            if (!IsEnded && pendingLevelUps > 0 && OfferCount == 0) OpenOffer();
        }

        private void TickCooldowns()
        {
            for (int i = 0; i < Hero.SkillCooldowns.Length; i++) Hero.SkillCooldowns[i] = MathF.Max(0f, Hero.SkillCooldowns[i] - FixedDeltaTime);
            for (int i = 0; i < weaponCooldowns.Length; i++) weaponCooldowns[i] = MathF.Max(0f, weaponCooldowns[i] - FixedDeltaTime);
            Hero.StunRemaining = MathF.Max(0f, Hero.StunRemaining - FixedDeltaTime);
        }

        private void ApplySkill(SurvivorInput input)
        {
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
            if (skill.Kind == SkillKind.Block)
            {
                if (!Hero.Blocking)
                {
                    Hero.Energy -= skill.EnergyCost;
                    Hero.BlockStarted = Time;
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
                Vec2 direction = MoveDirection(input.Move);
                if (direction.LengthSquared < 0.01f) direction = Vec2.FromAngle(Hero.Facing);
                Hero.Dashing = true; Hero.DashDirection = direction; Hero.DashRemaining = skill.DashDistance;
            }
            AddEvent(SurvivorEventType.SkillUsed, slot, hits);
        }

        private void MoveHero(int move)
        {
            if (Hero.StunRemaining > 0f) { Hero.Velocity = Vec2.Zero; return; }
            Vec2 old = Hero.Position;
            if (Hero.Dashing)
            {
                SkillDef dash = Config.ClassDef.ActiveSkills[2];
                float distance = MathF.Min(Hero.DashRemaining, dash.DashSpeed * FixedDeltaTime);
                Hero.Position += Hero.DashDirection * distance;
                Hero.DashRemaining -= distance;
                if (Hero.DashRemaining <= 0.0001f) Hero.Dashing = false;
            }
            else
            {
                Vec2 target = MoveDirection(move) * stats.MoveSpeed * (Hero.Blocking ? Config.ClassDef.ActiveSkills[1].BlockMoveMultiplier : 1f);
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
            float best = 144f;
            for (int i = 0; i < enemyLimit; i++)
            {
                if (!enemies[i].Active) continue;
                Vec2 delta = enemies[i].Position - Hero.Position;
                if (delta.LengthSquared < best) { best = delta.LengthSquared; direction = delta; }
            }
            if (direction.LengthSquared < 0.001f && Hero.Velocity.LengthSquared > 0.001f) direction = Hero.Velocity;
            if (direction.LengthSquared < 0.001f) return;
            Hero.Facing = TurnToward(Hero.Facing, direction.Angle(), 720f * MathF.PI / 180f * FixedDeltaTime);
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
            if (Config.RunSeconds < 900f && Time >= Config.RunSeconds)
            {
                EndReason = EndReason.TimeUp; AddEvent(SurvivorEventType.RunTimeUp); return;
            }
            if (Config.RunSeconds >= 900f && !bossSpawned && Time >= 900f)
            {
                SpawnBoss();
            }
            if (bossSpawned && BossAlive && Time >= Config.BossExpireSeconds)
            {
                EndReason = EndReason.Expired; AddEvent(SurvivorEventType.RunExpired);
            }
        }

        private void RecomputeStats(bool preserveHpGain)
        {
            float oldMax = Hero.MaxHp;
            CharacterBuild b = Config.Build;
            stats.MaxHp = Config.ClassDef.MaxHp * (1f + 0.1f * inventory.Level(6) + 0.05f * b.Points[(int)StatId.MaxHp]);
            stats.Armor = Config.ClassDef.Armor + inventory.Level(7) + 0.5f * b.Points[(int)StatId.Armor];
            stats.Regen = Config.ClassDef.Regen + 0.1f * b.Points[(int)StatId.Regen];
            stats.Might = 1f + 0.08f * inventory.Level(8) + 0.03f * b.Points[(int)StatId.Might];
            stats.CritChance = MathF.Min(1f, Config.ClassDef.CritChance + 0.04f * inventory.Level(9) + 0.02f * b.Points[(int)StatId.Crit]);
            stats.CritDamage = Config.ClassDef.CritDamage + 0.1f * b.Points[(int)StatId.CritDamage];
            stats.CooldownMul = MathF.Max(0.4f, 1f - 0.06f * inventory.Level(10) - 0.02f * b.Points[(int)StatId.Cooldown]);
            stats.AreaMul = 1f + 0.08f * inventory.Level(11) + 0.03f * b.Points[(int)StatId.Area];
            stats.MoveSpeed = Config.ClassDef.MoveSpeed * (1f + 0.08f * inventory.Level(12) + 0.02f * b.Points[(int)StatId.MoveSpeed]);
            stats.PickupRadius = Config.ClassDef.PickupRadius * (1f + 0.25f * inventory.Level(13) + 0.1f * b.Points[(int)StatId.Magnet]);
            stats.Luck = 5f * b.Points[(int)StatId.Luck];
            stats.GreedMul = 1f + 0.05f * b.Points[(int)StatId.Greed];
            stats.GrowthMul = 1f + 0.03f * b.Points[(int)StatId.Growth];
            stats.TierGold = 1f + 0.5f * (b.Tier - 1);
            Hero.MaxHp = stats.MaxHp;
            if (preserveHpGain && stats.MaxHp > oldMax) Hero.Hp += stats.MaxHp - oldMax;
            Hero.Hp = MathF.Min(Hero.Hp, Hero.MaxHp);
        }

        private void ClearPools()
        {
            for (int i = 0; i < enemies.Length; i++) enemies[i].Active = false;
            for (int i = 0; i < projectiles.Length; i++) projectiles[i].Active = false;
            for (int i = 0; i < pickups.Length; i++) pickups[i].Active = false;
            for (int i = 0; i < obstacles.Length; i++) obstacles[i].Active = false;
            activeObstacleCount = 0;
            enemyLimit = 0; projectileLimit = 0; pickupLimit = 0;
            testInvulnerable = false;
            testEnemiesInvulnerable = false;
            testDisablePickupCollection = false;
        }

        private void GenerateObstacles()
        {
            int made = 0;
            for (int attempt = 0; attempt < Config.ObstacleCount * 64 && made < Config.ObstacleCount; attempt++)
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

        private void ClampAndPushOut(ref Vec2 point, float radius)
        {
            float limit = Config.MapHalfSize - radius;
            point = new Vec2(MathF.Max(-limit, MathF.Min(limit, point.X)), MathF.Max(-limit, MathF.Min(limit, point.Y)));
            for (int i = 0; i < activeObstacleCount; i++)
            {
                if (!obstacles[i].Active) continue;
                Vec2 delta = point - obstacles[i].Position;
                float needed = radius + obstacles[i].Radius;
                if (delta.LengthSquared >= needed * needed) continue;
                Vec2 direction = delta.LengthSquared > 1e-8f ? delta.Normalized() : Vec2.FromAngle((i + 1) * 1.618f);
                point = obstacles[i].Position + direction * needed;
            }
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
        private static SurvivorPickup[] CreatePickups() { SurvivorPickup[] a = new SurvivorPickup[PickupCapacity]; for (int i = 0; i < a.Length; i++) a[i] = new SurvivorPickup(); return a; }
        private static SurvivorObstacle[] CreateObstacles() { SurvivorObstacle[] a = new SurvivorObstacle[ObstacleCapacity]; for (int i = 0; i < a.Length; i++) a[i] = new SurvivorObstacle(); return a; }
        private void AddEvent(SurvivorEventType type, float value = 0f, float extra = 0f, int id = -1, Vec2 point = default) => events.Add(new SurvivorEvent(type, value, extra, id, point));

        internal void SetTimeForTests(float time) { Time = time; }
        internal void SetHeroInvulnerableForTests() { testInvulnerable = true; }
        internal void SetEnemiesInvulnerableForTests() { testEnemiesInvulnerable = true; }
        internal void DisablePickupCollectionForTests() { testDisablePickupCollection = true; }
        internal SurvivorEnemy SpawnEnemyForTests(int type, Vec2 position, bool elite = false) => SpawnEnemy(type, position, elite, type == 4);
        internal void DamageEnemyForTests(SurvivorEnemy enemy, float damage) => DamageEnemy(enemy, damage, 0f);
        internal void GiveXpForTests(float xp) => CollectXp(xp);
        internal void SetXpForTests(float xp) { Xp = xp; }
        internal void SpawnGemForTests(Vec2 point, float value) => SpawnGem(point, value);
        internal void GiveItemForTests(int index, int level) { inventory.Set(index, level); RecomputeStats(true); }
        internal void SetHeroHpForTests(float hp) { Hero.Hp = MathF.Min(Hero.MaxHp, hp); }
        internal void SetHeroStateForTests(Vec2 position, Vec2 velocity, float facing) { Hero.Position = position; Hero.Velocity = velocity; Hero.Facing = facing; }
        internal void SetEnemyVelocityForTests(SurvivorEnemy enemy, Vec2 velocity) { enemy.Velocity = velocity; }
        internal void DamageHeroForTests(float raw, SurvivorEnemy source, bool contact = true) => DamageHero(raw, source, contact);
        internal SurvivorPickup SpawnPickupForTests(PickupKind kind, Vec2 point, float value) => SpawnPickup(kind, point, value, false);

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
