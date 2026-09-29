using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorProgressionTests
    {
        [Test]
        public void LevelUp_PausesAndOffersAreUnique()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 1); sim.GiveXpForTests(5f); sim.Step(default);
            Assert.That(sim.IsAwaitingPick, Is.True); HashSet<int> unique = new HashSet<int>(); for (int i = 0; i < sim.OfferCount; i++) unique.Add(sim.GetOffer(i).CatalogIndex); Assert.That(unique.Count, Is.EqualTo(sim.OfferCount));
        }

        [Test]
        public void Offer_RespectsPools()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 2); sim.GiveXpForTests(5f); sim.Step(default);
            for (int i = 0; i < sim.OfferCount; i++) Assert.That(sim.GetOffer(i).CatalogIndex, Is.AnyOf(0, 3, 6, 7, 9, 12));
        }

        [Test]
        public void Offer_FillersWhenEverythingMaxed()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 3); foreach (int item in new[] { 0, 3, 6, 7, 9, 12 }) sim.GiveItemForTests(item, 5);
            sim.GiveXpForTests(5f); sim.Step(default); Assert.That(sim.OfferCount, Is.EqualTo(2)); Assert.That(sim.GetOffer(0).CatalogIndex, Is.EqualTo(62)); Assert.That(sim.GetOffer(1).CatalogIndex, Is.EqualTo(63));
        }

        [Test]
        public void Offer_LuckGivesFourthOption()
        {
            int four = 0;
            for (int seed = 0; seed < 100; seed++) { SurvivorConfig config = SurvivorTestHelpers.Config(); config.Build.Points[(int)StatId.Luck] = 10; SurvivorSim sim = new SurvivorSim(config, seed); sim.GiveXpForTests(5f); sim.Step(default); if (sim.OfferCount == 4) four++; }
            Assert.That(four, Is.InRange(20, 50));
        }

        [Test]
        public void Pick_AppliesItem_AndResumes()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 4); sim.GiveXpForTests(5f); sim.Step(default); int item = sim.GetOffer(0).CatalogIndex; sim.Step(new SurvivorInput(0, 0, 1)); Assert.That(sim.Inventory.Level(item), Is.EqualTo(1).Or.EqualTo(2)); Assert.That(sim.IsAwaitingPick, Is.False);
        }

        [Test]
        public void MultipleLevelUps_OfferInSequence()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 5); sim.GiveXpForTests(20f); sim.Step(default); Assert.That(sim.IsAwaitingPick, Is.True); sim.Step(new SurvivorInput(0, 0, 1)); Assert.That(sim.IsAwaitingPick, Is.True); sim.Step(new SurvivorInput(0, 0, 1)); Assert.That(sim.IsAwaitingPick, Is.False);
        }

        [Test]
        public void Gems_MergeWhenPoolFull()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 1); for (int i = 0; i < 400; i++) sim.SpawnGemForTests(new Vec2(10 + (i % 20) * 0.01f, i / 20 * 0.01f), 1f);
            sim.SpawnGemForTests(new Vec2(10, 0), 5f); int active = 0; float total = 0; for (int i = 0; i < sim.Pickups.Count; i++) if (sim.Pickups[i].Active && sim.Pickups[i].Kind == PickupKind.Gem) { active++; total += sim.Pickups[i].Value; }
            Assert.That(active, Is.EqualTo(400)); Assert.That(total, Is.EqualTo(405f));
        }

        [Test]
        public void Gold_ScalesWithTierAndGreed()
        {
            SurvivorConfig lowConfig = SurvivorTestHelpers.Config(1); SurvivorConfig highConfig = SurvivorTestHelpers.Config(3); highConfig.Build.Points[(int)StatId.Greed] = 10;
            SurvivorSim low = new SurvivorSim(lowConfig, 12); SurvivorSim high = new SurvivorSim(highConfig, 12); SurvivorEnemy le = low.SpawnEnemyForTests(0, new Vec2(3, 0), true); SurvivorEnemy he = high.SpawnEnemyForTests(0, new Vec2(3, 0), true);
            low.DamageEnemyForTests(le, 100000f); high.DamageEnemyForTests(he, 100000f); Assert.That(GoldPickup(high), Is.EqualTo(GoldPickup(low) * 3f).Within(0.01f));
        }

        [Test]
        public void Meat_Heals()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 3); sim.SetHeroHpForTests(50f); sim.SpawnPickupForTests(PickupKind.Meat, Vec2.Zero, 30f); sim.Step(default); Assert.That(sim.Hero.Hp, Is.GreaterThanOrEqualTo(80f));
        }

        [Test]
        public void Events_ExtraFields()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 5); sim.SetXpForTests(4f); sim.GiveXpForTests(3f); SurvivorEvent xp = Find(sim.Events, SurvivorEventType.XpCollected); Assert.That(xp.Extra, Is.EqualTo(1f / 5f + 2f / 15f).Within(0.0001f));
            SurvivorEnemy source = sim.SpawnEnemyForTests(0, new Vec2(1, 0)); sim.DamageHeroForTests(8f, source); SurvivorEvent hurt = Find(sim.Events, SurvivorEventType.HeroDamaged); Assert.That(hurt.Extra, Is.EqualTo(hurt.Value / sim.Hero.MaxHp).Within(0.0001f)); Assert.That(hurt.Id, Is.EqualTo(source.Id));
            SurvivorEnemy boss = sim.SpawnEnemyForTests(4, new Vec2(3, 0)); sim.DamageEnemyForTests(boss, 100f); SurvivorEvent bossHit = Find(sim.Events, SurvivorEventType.BossDamaged); Assert.That(bossHit.Extra, Is.EqualTo(bossHit.Value / boss.MaxHp).Within(0.0001f));
        }

        [Test]
        public void Reward_Signs()
        {
            SurvivorRewardCalculator calc = new SurvivorRewardCalculator(new SurvivorRewardConfig());
            Assert.That(calc.Compute(new[] { new SurvivorEvent(SurvivorEventType.RunWon) }, 0), Is.Positive); Assert.That(calc.Compute(new[] { new SurvivorEvent(SurvivorEventType.RunTimeUp) }, 0), Is.Positive);
            Assert.That(calc.Compute(new[] { new SurvivorEvent(SurvivorEventType.HeroDied) }, 0), Is.Negative); Assert.That(calc.Compute(new[] { new SurvivorEvent(SurvivorEventType.HeroDamaged, extra: 0.1f) }, 0), Is.Negative);
            Assert.That(calc.Compute(new[] { new SurvivorEvent(SurvivorEventType.XpCollected, extra: 0.5f) }, 0), Is.Positive); Assert.That(calc.Compute(new[] { new SurvivorEvent(SurvivorEventType.GoldCollected, 10f) }, 0), Is.Positive);
            Assert.That(calc.Compute(new[] { new SurvivorEvent(SurvivorEventType.RunExpired), new SurvivorEvent(SurvivorEventType.ItemPicked) }, 0), Is.Zero);
        }

        [Test]
        public void Reward_OutcomeDominates()
        {
            SurvivorRewardConfig c = new SurvivorRewardConfig(); SurvivorRewardCalculator calc = new SurvivorRewardCalculator(c); float progressGold = c.PerLevelProgress * 50f + c.PerGold * 2000f;
            Assert.That(c.Win, Is.GreaterThan(progressGold)); Assert.That(c.Death, Is.GreaterThan(progressGold)); Assert.That(c.SurvivePerSecond * 900f, Is.GreaterThan(progressGold)); Assert.That(c.Death, Is.GreaterThan(c.HpLostPerMaxHp));
            Assert.That(calc.Compute(Array.Empty<SurvivorEvent>(), 1f), Is.Positive);
        }

        private static float GoldPickup(SurvivorSim sim) { for (int i = 0; i < sim.Pickups.Count; i++) if (sim.Pickups[i].Active && sim.Pickups[i].Kind == PickupKind.Gold) return sim.Pickups[i].Value; return 0; }
        private static SurvivorEvent Find(IReadOnlyList<SurvivorEvent> events, SurvivorEventType type) { for (int i = 0; i < events.Count; i++) if (events[i].Type == type) return events[i]; Assert.Fail("Missing event " + type); return default; }
    }
}
