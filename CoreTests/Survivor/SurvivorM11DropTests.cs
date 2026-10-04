using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorM11DropTests
    {
        private static SurvivorSim Sim(int seed = 7) => new SurvivorSim(SurvivorTestHelpers.Config(), seed);

        [Test]
        public void ManaPotionRestoresHalfOfMaxEnergy()
        {
            SurvivorSim sim = Sim(); sim.SetHeroEnergyForTests(0f);
            sim.SpawnPickupForTests(PickupKind.Mana, sim.Hero.Position, 0f); sim.Step(default);
            Assert.That(sim.Hero.Energy, Is.GreaterThanOrEqualTo(sim.Hero.MaxEnergy * SurvivorSim.ManaRestoreFraction));
            Assert.That(sim.Hero.Energy, Is.LessThan(sim.Hero.MaxEnergy * 0.6f));
        }

        [Test]
        public void BuffItemsStartTheirTimersAndExpire()
        {
            SurvivorSim sim = Sim();
            sim.SpawnPickupForTests(PickupKind.Rage, sim.Hero.Position, 0f);
            sim.SpawnPickupForTests(PickupKind.Shield, sim.Hero.Position, 0f);
            sim.SpawnPickupForTests(PickupKind.Haste, sim.Hero.Position, 0f);
            sim.Step(default);
            Assert.That(sim.RageRemaining, Is.EqualTo(SurvivorSim.RageSeconds).Within(0.1f));
            Assert.That(sim.ShieldRemaining, Is.EqualTo(SurvivorSim.ShieldSeconds).Within(0.1f));
            Assert.That(sim.HasteRemaining, Is.EqualTo(SurvivorSim.HasteSeconds).Within(0.1f));
            sim.SetHeroInvulnerableForTests();
            float until = sim.Time + SurvivorSim.RageSeconds + 0.5f;
            int guard = 0;
            while (sim.Time < until && guard++ < 100000) { if (sim.IsAwaitingPick) sim.Step(new SurvivorInput(0, 0, 1)); else sim.Step(default); }
            Assert.That(sim.RageRemaining, Is.Zero); Assert.That(sim.ShieldRemaining, Is.Zero); Assert.That(sim.HasteRemaining, Is.Zero);
        }

        [Test]
        public void ShieldCutsDamageTaken()
        {
            SurvivorSim plain = Sim(); plain.DamageHeroForTests(40f);
            SurvivorSim shielded = Sim(); shielded.SpawnPickupForTests(PickupKind.Shield, shielded.Hero.Position, 0f); shielded.Step(default);
            float before = shielded.DamageTaken; shielded.DamageHeroForTests(40f);
            Assert.That(shielded.DamageTaken - before, Is.EqualTo(plain.DamageTaken * SurvivorSim.ShieldDamageTakenMul).Within(0.01f));
        }

        [Test]
        public void BombHurtsEnemiesAroundTheHero()
        {
            SurvivorSim sim = Sim(); sim.SetHeroInvulnerableForTests();
            SurvivorEnemy near = sim.SpawnEnemyForTests(2, new Vec2(4f, 0f));
            SurvivorEnemy far = sim.SpawnEnemyForTests(2, new Vec2(SurvivorSim.BombRadius + 6f, 0f));
            float farHp = far.Hp;
            sim.SetWeaponCooldownForTests(sim.Config.ClassDef.StartingWeapon, 999f);
            sim.SpawnPickupForTests(PickupKind.Bomb, sim.Hero.Position, 0f); sim.Step(default);
            Assert.That(!near.Active || near.Hp < near.MaxHp, Is.True, "enemy inside the blast is hurt");
            Assert.That(far.Hp, Is.EqualTo(farHp), "enemy outside the blast is not");
        }

        [Test]
        public void LiveTuning_AreasStartTwentyPercentLarger_AndBonusDropsAppear()
        {
            SurvivorSim live = new SurvivorSim(new SurvivorConfig { ObstacleCount = 0 }, 3);
            Assert.That(live.DerivedStats.AreaMul, Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(new SurvivorTuning().MeatChance, Is.EqualTo(0.009f));

            SurvivorConfig config = SurvivorTestHelpers.Config(); config.Tuning.ManaChance = 1f;
            SurvivorSim sim = new SurvivorSim(config, 5); sim.SetHeroInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            sim.SpawnEnemyForTests(0, new Vec2(1.5f, 0f));
            bool mana = false;
            for (int step = 0; step < 600 && !mana; step++)
            {
                if (sim.IsAwaitingPick) sim.Step(new SurvivorInput(0, 0, 1)); else sim.Step(default);
                for (int i = 0; i < sim.Pickups.Count; i++) mana |= sim.Pickups[i].Active && sim.Pickups[i].Kind == PickupKind.Mana;
            }
            Assert.That(mana, Is.True, "a kill drops a mana potion when the chance is 1");
        }

        [Test]
        public void NewPickupKindsStayInsideTheSchemaV5PickupChannels()
        {
            Assert.That(SurvivorObservation.PickupChannel(PickupKind.Magnet), Is.EqualTo(5));
            Assert.That(SurvivorObservation.PickupChannel(PickupKind.Mana), Is.EqualTo(6));
            foreach (PickupKind kind in new[] { PickupKind.Bomb, PickupKind.Rage, PickupKind.Shield, PickupKind.Haste })
                Assert.That(SurvivorObservation.PickupChannel(kind), Is.EqualTo(5), kind + " reads as a magnet");
        }
    }
}
