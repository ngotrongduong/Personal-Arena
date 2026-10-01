using System;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorM8BalanceTests
    {
        [Test]
        public void BossHpMul_ScalesBossHp_ButNotNormalEnemies()
        {
            SurvivorConfig plainConfig = SurvivorTestHelpers.Config(); plainConfig.Tuning.BossHpMul = 1f;
            SurvivorConfig halfConfig = SurvivorTestHelpers.Config(); halfConfig.Tuning.BossHpMul = 0.5f;
            SurvivorSim plain = new SurvivorSim(plainConfig, 6), half = new SurvivorSim(halfConfig, 6);
            SurvivorEnemy plainBoss = plain.SpawnBossForTests(new Vec2(10f, 0f)), halfBoss = half.SpawnBossForTests(new Vec2(10f, 0f));
            Assert.That(plainBoss.MaxHp, Is.EqualTo(SurvivorDefaults.EnemyDef(plainBoss.TypeIndex).BaseHp).Within(1e-3f));
            Assert.That(halfBoss.MaxHp, Is.EqualTo(plainBoss.MaxHp * 0.5f).Within(1e-3f));
            SurvivorEnemy plainWalker = plain.SpawnEnemyForTests(0, new Vec2(-8f, 0f)), halfWalker = half.SpawnEnemyForTests(0, new Vec2(-8f, 0f));
            Assert.That(halfWalker.MaxHp, Is.EqualTo(plainWalker.MaxHp));
        }

        [Test]
        public void XpMul_ScalesTheGemOfAKill()
        {
            Assert.That(GemValueAfterKill(1f, false), Is.EqualTo(SurvivorDefaults.EnemyDef(0).Xp).Within(1e-4f));
            Assert.That(GemValueAfterKill(1.5f, false), Is.EqualTo(GemValueAfterKill(1f, false) * 1.5f).Within(1e-4f));
            Assert.That(GemValueAfterKill(2f, true), Is.EqualTo(new SurvivorTuning().EliteXp * 2f).Within(1e-3f));
        }

        [Test]
        public void Validate_RejectsZeroBossHpMul_AndNegativeXpMul()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SurvivorTuning { BossHpMul = 0f }.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() => new SurvivorTuning { XpMul = -1f }.Validate());
            Assert.DoesNotThrow(() => new SurvivorTuning().Validate());
        }

        private static float GemValueAfterKill(float xpMul, bool elite)
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.Tuning.XpMul = xpMul;
            SurvivorSim sim = new SurvivorSim(config, 5);
            SurvivorEnemy enemy = sim.SpawnEnemyForTests(0, new Vec2(8f, 0f), elite: elite);
            sim.DamageEnemyForTests(enemy, enemy.MaxHp + 1f);
            float total = 0f;
            foreach (SurvivorPickup p in sim.Pickups) if (p.Active && p.Kind == PickupKind.Gem) total += p.Value;
            return total;
        }
    }
}
