using System;
using System.Collections.Generic;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>Enemy definition table and the spawn phases. Both static tables stay in this file (the class-kit partial has none).</summary>
    public static partial class SurvivorDefaults
    {
        private static readonly SurvivorEnemyDef[] EnemyDefs =
        {
            Enemy(0, "walker", 15f, 2f, 0.45f, 1f, SurvivorAttackKind.Contact, 8f, 0f, 0f, 0f, 0.5f, 1, 0f, 360f),
            Enemy(1, "runner", 10f, 4f, 0.4f, 1f, SurvivorAttackKind.Contact, 5f, 0f, 0f, 0f, 0.5f, 1, 0f, 360f),
            Enemy(2, "brute", 80f, 1.6f, 0.7f, 3f, SurvivorAttackKind.Melee, 25f, 1.8f, 90f, 0.9f, 1.8f, 5, 0.6f, 180f),
            new SurvivorEnemyDef
            {
                TypeIndex = 3, Id = "spitter", BaseHp = 20f, MoveSpeed = 2.4f, Radius = 0.4f, Mass = 1f,
                AttackKind = SurvivorAttackKind.Ranged, AttackDamage = 10f, PreferredDistance = 7f,
                AttackRange = 9f, AttackArcDegrees = 60f, WindupSeconds = 0.5f, RecoverSeconds = 2.5f,
                ProjectileSpeed = 7f, ProjectileRadius = 0.3f, ProjectileRange = 12f,
                Xp = 2, KnockbackResist = 0f, TurnSpeedDegPerSec = 360f
            },
            Enemy(4, "bone-lord", 6000f, 1.4f, 1.75f, 1000f, SurvivorAttackKind.Melee, 40f, 3.5f, 180f, 1.2f, 4.8f, 0, 1f, 180f, true),
            new SurvivorEnemyDef
            {
                TypeIndex = ExploderTypeIndex, Id = "exploder", BaseHp = 25f, MoveSpeed = 2.8f, Radius = 0.45f, Mass = 1f,
                AttackKind = SurvivorAttackKind.Explode, AttackDamage = 30f, AttackRange = 1f, AttackArcDegrees = 360f,
                WindupSeconds = 0.6f, ExplodeRadius = 2.2f, Xp = 2, KnockbackResist = 0f, TurnSpeedDegPerSec = 360f
            },
            new SurvivorEnemyDef
            {
                TypeIndex = GhostTypeIndex, Id = "ghost", BaseHp = 12f, MoveSpeed = 3.6f, Radius = 0.35f, Mass = 0.5f,
                AttackKind = SurvivorAttackKind.Contact, AttackDamage = 6f, Xp = 2, KnockbackResist = 0f,
                TurnSpeedDegPerSec = 360f, IgnoresObstacles = true
            },
            new SurvivorEnemyDef
            {
                TypeIndex = NecromancerTypeIndex, Id = "necromancer", BaseHp = 60f, MoveSpeed = 2f, Radius = 0.5f, Mass = 1.5f,
                AttackKind = SurvivorAttackKind.Ranged, AttackDamage = 8f, PreferredDistance = 9f,
                AttackRange = 11f, AttackArcDegrees = 60f, WindupSeconds = 0.6f, RecoverSeconds = 3.5f,
                ProjectileSpeed = 6f, ProjectileRadius = 0.3f, ProjectileRange = 13f,
                Xp = 6, KnockbackResist = 0.3f, TurnSpeedDegPerSec = 240f, SummonInterval = 6f, SummonCount = 3
            }
        };

        public const int EnemyTypeCount = 8;
        /// <summary>Elites are only drawn from the four basic types.</summary>
        public const int EliteTypeCount = 4;
        public const int ExploderTypeIndex = 5;
        public const int GhostTypeIndex = 6;
        public const int NecromancerTypeIndex = 7;

        // New enemies only appear from 300 s, so the spawn sequence before that is unchanged.
        private static readonly SpawnPhase[] Phases =
        {
            Phase(0, 60, 1, 0, 0, 0, 30, 1), Phase(60, 180, 3, 1, 0, 0, 60, 2),
            Phase(180, 300, 3, 2, 1, 0, 90, 3), Phase(300, 420, 2, 2, 1, 1, 120, 4, exploder: 1),
            Phase(420, 600, 2, 3, 1, 2, 160, 5, exploder: 1, ghost: 1),
            Phase(600, 720, 1, 3, 2, 2, 200, 6, exploder: 2, ghost: 1, necromancer: 1),
            Phase(720, 900, 1, 3, 3, 3, 250, 8, exploder: 2, ghost: 2, necromancer: 1)
        };

        public static SurvivorEnemyDef EnemyDef(int typeIndex) =>
            typeIndex >= 0 && typeIndex < EnemyDefs.Length ? EnemyDefs[typeIndex] : null;

        public static SpawnPhase PhaseAt(float seconds)
        {
            for (int i = 0; i < Phases.Length; i++)
            {
                if (seconds >= Phases[i].From && seconds < Phases[i].To) return Phases[i];
            }
            return null;
        }

        public static int PhaseCount => Phases.Length;
        public static SpawnPhase GetPhase(int index) => Phases[index];

        private static SurvivorEnemyDef Enemy(int type, string id, float hp, float speed, float radius,
            float mass, SurvivorAttackKind attack, float damage, float range, float arc, float windup,
            float recover, int xp, float resist, float turn, bool boss = false)
        {
            return new SurvivorEnemyDef { TypeIndex = type, Id = id, BaseHp = hp, MoveSpeed = speed,
                Radius = radius, Mass = mass, AttackKind = attack, AttackDamage = damage,
                AttackRange = range, AttackArcDegrees = arc, WindupSeconds = windup,
                RecoverSeconds = recover, Xp = xp, KnockbackResist = resist,
                TurnSpeedDegPerSec = turn, IsBoss = boss };
        }

        private static SpawnPhase Phase(float from, float to, int w, int r, int b, int s, int max, float rate,
            int exploder = 0, int ghost = 0, int necromancer = 0) =>
            new SpawnPhase
            {
                From = from, To = to, MaxAlive = max, SpawnsPerSecond = rate,
                Weights = Array.AsReadOnly(new[] { w, r, b, s, 0, exploder, ghost, necromancer })
            };
    }
}
