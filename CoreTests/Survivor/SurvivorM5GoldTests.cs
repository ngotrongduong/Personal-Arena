using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorM5GoldTests
    {
        [Test]
        public void SourcesSumToGold_AfterASteppedRun()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(tier: 3, runSeconds: 300f), 41);
            sim.SetHeroInvulnerableForTests();
            for (int tick = 0; tick < 200 * 60 && !sim.IsEnded; tick++)
            {
                sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : new SurvivorInput((tick / 40) % 9, 0, 0));
                if (tick % 600 == 0) Assert.That(SumSources(sim), Is.EqualTo(sim.Gold).Within(Tolerance(sim.Gold)));
            }
            Assert.That(sim.Gold, Is.GreaterThan(0f));
            Assert.That(SumSources(sim), Is.EqualTo(sim.Gold).Within(Tolerance(sim.Gold)));
            Assert.That(sim.GetGold(GoldSource.Normal) + sim.GetGold(GoldSource.Elite) + sim.GetGold(GoldSource.Chest), Is.GreaterThan(0f));
            sim.Reset(41);
            for (int i = 0; i < SurvivorSim.GoldSourceCount; i++) Assert.That(sim.GetGold((GoldSource)i), Is.EqualTo(0f));
        }

        [Test]
        public void EvaluatorRun_FillsGoldBySource_ThatSumsToGold()
        {
            SurvivorRunStats stats = new SurvivorEvaluator().RunOne(SurvivorTestHelpers.Brain(favouredMove: 1), SurvivorTestHelpers.Config(tier: 3, runSeconds: 120f), 9, true);
            Assert.That(stats.GoldBySource, Has.Length.EqualTo(SurvivorSim.GoldSourceCount));
            float sum = 0f; foreach (float value in stats.GoldBySource) { Assert.That(value, Is.GreaterThanOrEqualTo(0f)); sum += value; }
            Assert.That(sum, Is.EqualTo(stats.Gold).Within(Tolerance(stats.Gold)));
        }

        [Test]
        public void ChestGold_GoesToChest()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 3);
            sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
            sim.Step(default);
            Assert.That(sim.Events, Has.Some.Matches<SurvivorEvent>(e => e.Type == SurvivorEventType.ChestOpened));
            Assert.That(sim.GetGold(GoldSource.Chest), Is.GreaterThan(0f));
            Assert.That(sim.GetGold(GoldSource.Chest), Is.EqualTo(sim.Gold));
        }

        [Test]
        public void GoldPickupSource_IsKept_NormalByDefault()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 3);
            sim.SpawnPickupForTests(PickupKind.Gold, sim.Hero.Position, 7f);
            sim.Step(default);
            Assert.That(sim.GetGold(GoldSource.Normal), Is.EqualTo(7f)); Assert.That(sim.Gold, Is.EqualTo(7f));
        }

        [Test]
        public void EliteGoldDrop_GoesToElite()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 5);
            SurvivorEnemy elite = sim.SpawnEnemyForTests(0, new Vec2(8f, 0f), elite: true);
            sim.DamageEnemyForTests(elite, elite.MaxHp + 1f);
            SurvivorPickup gold = null;
            foreach (SurvivorPickup p in sim.Pickups) if (p.Active && p.Kind == PickupKind.Gold) gold = p;
            Assert.That(gold, Is.Not.Null); Assert.That(gold.Source, Is.EqualTo(GoldSource.Elite));
            float value = gold.Value;
            sim.SetHeroStateForTests(gold.Position, Vec2.Zero, 0f);
            sim.Step(default);
            Assert.That(sim.GetGold(GoldSource.Elite), Is.EqualTo(value));
            Assert.That(sim.GetGold(GoldSource.Normal), Is.EqualTo(0f));
            Assert.That(SumSources(sim), Is.EqualTo(sim.Gold).Within(Tolerance(sim.Gold)));
        }

        [Test]
        public void BossKillGold_GoesToBoss()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 6);
            SurvivorEnemy boss = sim.SpawnBossForTests(new Vec2(10f, 0f));
            sim.DamageEnemyForTests(boss, boss.MaxHp + 1f);
            Assert.That(sim.EndReason, Is.EqualTo(EndReason.Won));
            Assert.That(sim.GetGold(GoldSource.Boss), Is.GreaterThan(0f));
            Assert.That(sim.GetGold(GoldSource.Boss), Is.EqualTo(sim.Gold));
        }

        [Test]
        public void LevelUpFillerGold_GoesToFiller()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.OldConfig(), 7);
            // Every slot full and maxed: the offer falls back to the fillers (bonus gold first).
            foreach (int index in new[] { 0, 1, 2, 3, 6, 7, 8, 9 }) sim.GiveItemForTests(index, SurvivorCatalog.Get(index).MaxLevel);
            sim.GiveXpForTests(5f);
            sim.Step(default);
            Assert.That(sim.IsAwaitingPick, Is.True);
            Assert.That(sim.GetOffer(0).CatalogIndex, Is.EqualTo(SurvivorCatalog.BonusGoldIndex));
            float before = sim.Gold;
            sim.Step(new SurvivorInput(0, 0, 1));
            Assert.That(sim.GetGold(GoldSource.Filler), Is.GreaterThan(0f));
            Assert.That(sim.Gold - before, Is.EqualTo(sim.GetGold(GoldSource.Filler)));
            Assert.That(SumSources(sim), Is.EqualTo(sim.Gold).Within(Tolerance(sim.Gold)));
        }

        private static float SumSources(SurvivorSim sim)
        {
            float sum = 0f;
            for (int i = 0; i < SurvivorSim.GoldSourceCount; i++) sum += sim.GetGold((GoldSource)i);
            return sum;
        }

        /// <summary>The per-source totals add the same float values in a different grouping; allow float rounding.</summary>
        private static float Tolerance(float gold) => 1e-5f * gold + 1e-3f;
    }
}
