using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>Owner decision 2026-10-02: a kick never stuns the boss (old rules kept for the goldens); a fan volley that fires nothing keeps its cooldown.</summary>
    public sealed class SurvivorKickAndFanTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Kick_StunsTheBossOnlyUnderTheOldRule(bool oldRules)
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); if (oldRules) config = SurvivorTestHelpers.OldRules(config);
            SurvivorSim sim = new SurvivorSim(config, 7); sim.SetHeroInvulnerableForTests();
            sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 0f);
            SurvivorEnemy boss = sim.SpawnBossForTests(new Vec2(1.2f + 0.5f, 0f));
            SurvivorEnemy walker = sim.SpawnEnemyForTests(0, new Vec2(0.9f, 0.4f));
            sim.Step(new SurvivorInput(0, 1, 0)); // skill 1 = kick
            Assert.That(walker.StunRemaining, Is.GreaterThan(0f), "a normal enemy is still stunned");
            if (oldRules) Assert.That(boss.StunRemaining, Is.GreaterThan(0f));
            else Assert.That(boss.StunRemaining, Is.EqualTo(0f));
        }

        [Test]
        public void Fan_FullProjectilePool_FiresNothingAndKeepsTheCooldown()
        {
            SurvivorSim sim = new SurvivorSim(new SurvivorConfig { ClassDef = SurvivorDefaults.Archer() }, 9); sim.SetHeroInvulnerableForTests();
            sim.GiveItemForTests(SurvivorCatalog.MultiShotIndex, 5);
            SurvivorEnemy target = sim.SpawnEnemyForTests(2, new Vec2(5f, 0f)); target.StunRemaining = 1000f;
            sim.FillProjectilePoolForTests(new Vec2(45f, 45f));
            sim.SetWeaponCooldownForTests(SurvivorCatalog.MultiShotIndex, 0f);
            sim.Step(default);
            Assert.That(sim.WeaponCooldownForTests(SurvivorCatalog.MultiShotIndex), Is.EqualTo(0f), "nothing fired, so no cooldown");
            for (int e = 0; e < sim.Events.Count; e++) Assert.That(sim.Events[e].Type == SurvivorEventType.WeaponFired && sim.Events[e].Id == SurvivorCatalog.MultiShotIndex, Is.False);
        }
    }
}
