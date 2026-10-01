using System;

namespace PersonalArena.Core.Survivor
{
    public sealed class SurvivorConfig
    {
        public float MapHalfSize = 50f;
        public int ObstacleCount = 40;
        public float ObstacleRadiusMin = 0.5f;
        public float ObstacleRadiusMax = 1.5f;
        public float ObstacleClearRadius = 6f;
        public float RunSeconds = 900f;
        public float BossExpireSeconds = 1020f;
        public int OpeningRing;
        public CharacterBuild Build = new CharacterBuild();
        public SurvivorClassDef ClassDef = SurvivorDefaults.Warrior();
        public SurvivorRewardConfig Rewards = new SurvivorRewardConfig();
        /// <summary>Balance numbers of the run (scaling, spawning, loot, hero turning).</summary>
        public SurvivorTuning Tuning = new SurvivorTuning();

        public void Validate()
        {
            if (Tuning == null) throw new ArgumentNullException(nameof(Tuning));
            Tuning.Validate();
            if (!Finite(MapHalfSize) || MapHalfSize <= 10f) throw new ArgumentOutOfRangeException(nameof(MapHalfSize));
            if (ObstacleCount < 0 || ObstacleCount > 64) throw new ArgumentOutOfRangeException(nameof(ObstacleCount));
            if (!Finite(ObstacleRadiusMin) || !Finite(ObstacleRadiusMax) || ObstacleRadiusMin <= 0f || ObstacleRadiusMax < ObstacleRadiusMin) throw new ArgumentOutOfRangeException(nameof(ObstacleRadiusMin));
            if (!Finite(ObstacleClearRadius) || ObstacleClearRadius < 0f) throw new ArgumentOutOfRangeException(nameof(ObstacleClearRadius));
            if (!Finite(RunSeconds) || RunSeconds < 60f || RunSeconds > 900f) throw new ArgumentOutOfRangeException(nameof(RunSeconds));
            if (!Finite(BossExpireSeconds) || BossExpireSeconds < 900f) throw new ArgumentOutOfRangeException(nameof(BossExpireSeconds));
            if (OpeningRing < 0 || OpeningRing > SurvivorSim.EnemyCapacity) throw new ArgumentOutOfRangeException(nameof(OpeningRing));
            if (Build == null || ClassDef == null || Rewards == null) throw new ArgumentNullException();
            Build.Validate();
            Rewards.Validate();
            if (ClassDef.ActiveSkills == null || ClassDef.ActiveSkills.Length != 4) throw new ArgumentException("Class needs four active skill slots.");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Every balance number the simulation uses that is not part of an item, enemy or class def.
    /// The defaults are the T-014 starting balance; <see cref="SurvivorConfig.Validate"/> checks them.
    /// </summary>
    public sealed class SurvivorTuning
    {
        // Hero.
        /// <summary>The hero faces the nearest enemy within this distance (m).</summary>
        public float FacingRange = 12f;
        public float HeroTurnRateDegPerSec = 720f;

        // Enemy scaling at spawn time (t = run seconds, N = tier).
        /// <summary>HP × (1 + HpPerMinute · t / 60).</summary>
        public float HpPerMinute = 0.12f;
        /// <summary>Damage × (1 + DamagePerMinute · t / 60).</summary>
        public float DamagePerMinute = 0.05f;
        /// <summary>HP × (1 + HpPerTier · (N − 1)).</summary>
        public float HpPerTier = 0.35f;
        /// <summary>Damage × (1 + DamagePerTier · (N − 1)).</summary>
        public float DamagePerTier = 0.2f;
        /// <summary>Max alive and spawns per second × (1 + SpawnPerTier · (N − 1)).</summary>
        public float SpawnPerTier = 0.15f;
        /// <summary>Enemy pool slots kept free for boss summons.</summary>
        public int SummonReserve = 20;
        /// <summary>Boss HP × this (the boss ignores HpPerMinute; HpPerTier still applies).</summary>
        public float BossHpMul = 0.4f;
        /// <summary>Every XP gem dropped by a kill × this.</summary>
        public float XpMul = 1.5f;

        // Elites.
        public float EliteRadiusMul = 1.6f;
        public float EliteMassMul = 3f;
        public float EliteHpMul = 10f;
        public float EliteDamageMul = 1.5f;
        public float EliteMinKnockbackResist = 0.8f;
        public float EliteXp = 50f;
        /// <summary>Elites spawn at k · EliteIntervalSeconds for k = 1..EliteCount.</summary>
        public float EliteIntervalSeconds = 180f;
        public int EliteCount = 4;

        // Spawning and relocation.
        public float SpawnRingMin = 18f;
        public float SpawnRingMax = 24f;
        /// <summary>A spawn point must be this far inside the map edge (m).</summary>
        public float SpawnMapMargin = 1f;
        /// <summary>A spawn point must clear every obstacle by the enemy radius plus this margin (m).</summary>
        public float SpawnObstacleMargin = 0.1f;
        public int SpawnAttempts = 8;
        public int EliteSpawnAttempts = 32;
        /// <summary>Normal and elite enemies farther than this from the hero are moved in front of it (m).</summary>
        public float RelocationDistance = 40f;
        /// <summary>Half width of the relocation arc around the hero's velocity (radians).</summary>
        public float RelocationHalfArc = MathF.PI / 3f;
        /// <summary>Normal spawning stops, and the boss spawns, at this time (full runs only).</summary>
        public float BossSpawnSeconds = 900f;
        public int BossSummonCount = 8;
        public float BossSummonRadius = 3f;
        public float BossSummonIntervalSeconds = 10f;

        // Contact and body rules.
        /// <summary>Contact attackers hit within heroR + enemyR + ContactMargin.</summary>
        public float ContactMargin = 0.1f;
        public float ContactIntervalSeconds = 0.5f;
        /// <summary>Death counts as Surrounded when at least this many enemies touch the hero.</summary>
        public int SurroundedCount = 6;
        /// <summary>Enemy–enemy overlap removed per tick (fraction of half the overlap).</summary>
        public float EnemySeparation = 0.5f;
        public int ObstacleAttemptsPerObstacle = 64;

        // Loot and pickups.
        public float GoldChance = 0.045f;
        public float GoldMin = 1f;
        /// <summary>Exclusive upper bound of the floored gold roll.</summary>
        public float GoldMax = 6f;
        public float EliteGoldMin = 20f;
        public float EliteGoldMax = 41f;
        public float MeatChance = 0.005f;
        public float MeatHeal = 30f;
        public float BossGold = 500f;
        public float DropOffset = 0.3f;
        public float GoldDropAngle = 1.2f;
        public float MeatDropAngle = 2.4f;
        public float PickupFlySpeed = 12f;
        /// <summary>Pickups are collected within heroR + CollectMargin.</summary>
        public float CollectMargin = 0.3f;

        // Level-up fillers.
        public float FillerGold = 25f;
        public float FillerHeal = 30f;

        // Tier modifiers (GDD §3.5; see TierModifiers). Tier 1 uses none of these.
        /// <summary>DenserSpawns (tier ≥ 2): max alive and spawns per second × this, on top of SpawnPerTier.</summary>
        public float DenserSpawnsMul = 1.1f;
        /// <summary>EarlyElite (tier ≥ 3): one extra scheduled elite at this time (s).</summary>
        public float EarlyEliteSeconds = 90f;
        /// <summary>FastRunners (tier ≥ 4): runner (type 1) move speed × this.</summary>
        public float FastRunnerSpeedMul = 1.15f;
        /// <summary>LessMeat (tier ≥ 5): meat drop chance × this.</summary>
        public float LessMeatMul = 0.5f;
        /// <summary>EarlyBrutes (tier ≥ 6): phases starting at or after this time, before brutes normally appear, get brute weight ≥ EarlyBruteMinWeight.</summary>
        public float EarlyBruteFromSeconds = 60f;
        public int EarlyBruteMinWeight = 1;
        /// <summary>DoubleElites (tier ≥ 7): elites spawned at every scheduled elite time.</summary>
        public int DoubleEliteCount = 2;
        /// <summary>EnemyRegen (tier ≥ 8): a non-boss enemy not damaged for this long (s) regenerates.</summary>
        public float RegenDelaySeconds = 3f;
        /// <summary>EnemyRegen: fraction of max HP regained per second.</summary>
        public float RegenFractionPerSecond = 0.02f;
        /// <summary>BossSummonsFaster (tier ≥ 9): boss summon interval × this.</summary>
        public float BossSummonFasterMul = 0.5f;
        /// <summary>Nightmare (tier 10): every enemy's move speed × this.</summary>
        public float NightmareSpeedMul = 1.1f;
        /// <summary>Nightmare (tier 10): elite HP × this (on top of EliteHpMul).</summary>
        public float NightmareEliteHpMul = 1.5f;

        public void Validate()
        {
            Check(FacingRange); Check(HeroTurnRateDegPerSec); Check(HpPerMinute); Check(DamagePerMinute); Check(HpPerTier);
            Check(DamagePerTier); Check(SpawnPerTier); Check(EliteRadiusMul); Check(EliteMassMul); Check(EliteHpMul);
            Check(EliteDamageMul); Check(EliteMinKnockbackResist); Check(EliteXp); Check(EliteIntervalSeconds); Check(SpawnRingMin);
            Check(SpawnRingMax); Check(SpawnMapMargin); Check(SpawnObstacleMargin); Check(RelocationDistance); Check(RelocationHalfArc);
            Check(BossSpawnSeconds); Check(BossSummonRadius); Check(BossSummonIntervalSeconds); Check(ContactMargin); Check(ContactIntervalSeconds);
            Check(EnemySeparation); Check(GoldChance); Check(GoldMin); Check(GoldMax); Check(EliteGoldMin); Check(EliteGoldMax);
            Check(MeatChance); Check(MeatHeal); Check(BossGold); Check(DropOffset); Check(GoldDropAngle); Check(MeatDropAngle);
            Check(PickupFlySpeed); Check(CollectMargin); Check(FillerGold); Check(FillerHeal);
            Check(DenserSpawnsMul); Check(EarlyEliteSeconds); Check(FastRunnerSpeedMul); Check(LessMeatMul); Check(EarlyBruteFromSeconds);
            Check(RegenDelaySeconds); Check(RegenFractionPerSecond); Check(BossSummonFasterMul); Check(NightmareSpeedMul); Check(NightmareEliteHpMul);
            Check(BossHpMul); Check(XpMul);
            if (BossHpMul <= 0f) throw new ArgumentOutOfRangeException(nameof(BossHpMul));
            if (EarlyBruteMinWeight < 0 || DoubleEliteCount < 1 || BossSummonFasterMul <= 0f) throw new ArgumentOutOfRangeException(nameof(DoubleEliteCount));
            if (SpawnRingMax < SpawnRingMin || GoldMax < GoldMin || EliteGoldMax < EliteGoldMin) throw new ArgumentOutOfRangeException(nameof(SpawnRingMax), "A range has max < min.");
            if (SummonReserve < 0 || EliteCount < 0 || SurroundedCount < 1 || BossSummonCount < 0) throw new ArgumentOutOfRangeException(nameof(SummonReserve));
            if (SpawnAttempts < 1 || EliteSpawnAttempts < 1 || ObstacleAttemptsPerObstacle < 1) throw new ArgumentOutOfRangeException(nameof(SpawnAttempts));
            if (EliteIntervalSeconds <= 0f || BossSummonIntervalSeconds <= 0f || EliteMinKnockbackResist > 1f) throw new ArgumentOutOfRangeException(nameof(EliteIntervalSeconds));
        }

        private static void Check(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) throw new ArgumentOutOfRangeException(nameof(value), "Tuning values must be finite and >= 0.");
        }
    }

    public readonly struct SurvivorInput
    {
        public const int MoveBranchSize = 9;
        public const int SkillBranchSize = 5;
        public const int PickBranchSize = 5;
        public readonly int Move;
        public readonly int Skill;
        public readonly int Pick;
        public SurvivorInput(int move, int skill, int pick) { Move = move; Skill = skill; Pick = pick; }
    }
}
