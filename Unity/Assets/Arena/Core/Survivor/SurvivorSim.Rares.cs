using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>
    /// M11 enemies: the charger's dash, the splitter's copies, the shaman's heal and the rare golden enemy.
    /// Golden rolls use their own random stream, so the sequence of every older roll is unchanged.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        public const float GoldenHpMul = 3f;
        public const float GoldenGoldMul = 5f;
        public const float GoldenRadiusMul = 1.15f;
        public const float ChargeSpeed = 13f;
        public const float ChargeSeconds = 0.65f;
        public const float SplitHpMul = 0.4f;
        public const float SplitRadiusMul = 0.65f;
        public const float SplitDamageMul = 0.6f;
        public const float ShamanHealRadius = 5f;
        public const float ShamanHealFraction = 0.12f;
        private const float GoldenDropAngle = 2.6f;
        private const float SplitOffset = 0.45f;

        private Rng rareRng;

        private void ResetRares(int seed) { rareRng = new Rng(seed ^ 0x474F4C44); }

        private void MaybeMakeGolden(SurvivorEnemy e)
        {
            float chance = Config.Tuning.GoldenChance;
            if (e == null || chance <= 0f || rareRng.NextFloat() >= chance) return;
            MakeGolden(e);
        }

        private void MakeGolden(SurvivorEnemy e)
        {
            e.Golden = true; e.MaxHp *= GoldenHpMul; e.Hp = e.MaxHp; e.Radius *= GoldenRadiusMul;
            if (e.Radius > maxEnemyRadius) maxEnemyRadius = e.Radius;
            AddEvent(SurvivorEventType.GoldenSpawned, id: e.Id, point: e.Position);
        }

        /// <summary>A golden enemy always drops five times a normal gold drop.</summary>
        private void DropGoldenGold(SurvivorEnemy enemy)
        {
            SurvivorTuning tuning = Config.Tuning;
            float gold = MathF.Floor(rareRng.Range(tuning.GoldMin, tuning.GoldMax)) * GoldenGoldMul * stats.TierGold * stats.GreedMul;
            SpawnPickup(PickupKind.Gold, enemy.Position + Vec2.FromAngle(GoldenDropAngle) * tuning.DropOffset, gold, true);
            AddEvent(SurvivorEventType.GoldenKilled, gold, id: enemy.Id, point: enemy.Position);
        }

        private void Split(SurvivorEnemy enemy, SurvivorEnemyDef def)
        {
            Vec2 side = Vec2.FromAngle(enemy.Facing + MathF.PI * 0.5f) * SplitOffset;
            Vec2 origin = enemy.Position; int type = enemy.TypeIndex, id = enemy.Id, made = 0;
            for (int k = 0; k < def.SplitCount; k++)
            {
                SurvivorEnemy child = SpawnEnemy(type, origin + side * (k % 2 == 0 ? 1f + k / 2 : -1f - k / 2), false, false);
                if (child == null) break;
                child.Small = true; child.MaxHp *= SplitHpMul; child.Hp = child.MaxHp; child.Radius *= SplitRadiusMul;
                child.Mass *= 0.5f; child.Damage *= SplitDamageMul; made++;
            }
            if (made > 0) AddEvent(SurvivorEventType.EnemySplit, made, id: id, point: origin);
        }

        private void StartCharge(SurvivorEnemy e, SurvivorEnemyDef def)
        {
            e.ChargeRemaining = ChargeSeconds; e.AttackCooldown = def.RecoverSeconds + ChargeSeconds;
            AddEvent(SurvivorEventType.EnemyCharged, ChargeSeconds, id: e.Id, point: e.Position);
        }

        /// <summary>The dash keeps the facing locked at the wind-up, so stepping sideways avoids it.</summary>
        private void TickCharge(SurvivorEnemy e)
        {
            e.ChargeRemaining = MathF.Max(0f, e.ChargeRemaining - FixedDeltaTime);
            e.Position += Vec2.FromAngle(e.Facing) * (ChargeSpeed * FixedDeltaTime);
            float reach = Hero.Radius + e.Radius + Config.Tuning.ContactMargin;
            if ((Hero.Position - e.Position).LengthSquared > reach * reach) return;
            e.ChargeRemaining = 0f;
            DamageHero(e.Damage, e, false);
        }

        private void HealAround(SurvivorEnemy shaman)
        {
            int healed = 0;
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy other = enemies[i];
                if (!other.Active || other == shaman || other.IsBoss || other.Hp >= other.MaxHp) continue;
                float reach = ShamanHealRadius + other.Radius;
                if ((other.Position - shaman.Position).LengthSquared > reach * reach) continue;
                other.Hp = MathF.Min(other.MaxHp, other.Hp + other.MaxHp * ShamanHealFraction); healed++;
            }
            if (healed > 0) AddEvent(SurvivorEventType.EnemyHealed, ShamanHealRadius, id: shaman.Id, point: shaman.Position);
        }

        internal SurvivorEnemy SpawnGoldenForTests(int type, Vec2 point)
        {
            SurvivorEnemy e = SpawnEnemy(type, point, false, false);
            if (e != null) MakeGolden(e);
            return e;
        }
    }
}
