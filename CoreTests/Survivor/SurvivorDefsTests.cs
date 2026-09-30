using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorDefsTests
    {
        [Test]
        public void XpCurve_MatchesTable()
        {
            Assert.Multiple(() => { Assert.That(XpCurve.Required(1), Is.EqualTo(5)); Assert.That(XpCurve.Required(2), Is.EqualTo(15)); Assert.That(XpCurve.Required(19), Is.EqualTo(185)); Assert.That(XpCurve.Required(20), Is.EqualTo(798)); Assert.That(XpCurve.Required(21), Is.EqualTo(211)); Assert.That(XpCurve.Required(39), Is.EqualTo(445)); Assert.That(XpCurve.Required(40), Is.EqualTo(2861)); Assert.That(XpCurve.Required(41), Is.EqualTo(477)); });
        }

        [Test]
        public void Catalog_IndicesAndPools()
        {
            SurvivorClassDef warrior = SurvivorDefaults.Warrior();
            Assert.That(warrior.WeaponPool, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 })); Assert.That(warrior.PassivePool, Is.EqualTo(new[] { 6, 7, 8, 9, 10, 11, 12, 13 }));
            Assert.That(SurvivorCatalog.Get(0).Id, Is.EqualTo("sword-sweep")); Assert.That(SurvivorCatalog.Get(1).Id, Is.EqualTo("spear-thrust")); Assert.That(SurvivorCatalog.Get(61), Is.Null);
            Assert.That(SurvivorCatalog.Get(62).Kind, Is.EqualTo(ItemKind.Filler)); Assert.That(SurvivorCatalog.Get(63).Kind, Is.EqualTo(ItemKind.Filler));
        }

        [Test]
        public void BuildRandomizer_RespectsCapsAndRange()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                CharacterBuild build = BuildRandomizer.Random(new Rng(seed), 50, 2, 7); build.Validate();
                Assert.That(build.Level, Is.InRange(0, 50)); Assert.That(build.Tier, Is.InRange(2, 7));
            }
        }

        [Test]
        public void Passives_ChangeDerivedStats()
        {
            SurvivorSim baseline = new SurvivorSim(SurvivorTestHelpers.Config(), 1);
            SurvivorSim heart = new SurvivorSim(SurvivorTestHelpers.Config(), 1); heart.GiveItemForTests(6, 1);
            SurvivorSim armor = new SurvivorSim(SurvivorTestHelpers.Config(), 1); armor.GiveItemForTests(7, 1);
            SurvivorSim crit = new SurvivorSim(SurvivorTestHelpers.Config(), 1); crit.GiveItemForTests(9, 1);
            SurvivorSim boots = new SurvivorSim(SurvivorTestHelpers.Config(), 1); boots.GiveItemForTests(12, 1);
            Assert.That(heart.DerivedStats.MaxHp, Is.GreaterThan(baseline.DerivedStats.MaxHp)); Assert.That(armor.DerivedStats.Armor, Is.EqualTo(1f));
            Assert.That(crit.DerivedStats.CritChance, Is.GreaterThan(baseline.DerivedStats.CritChance)); Assert.That(boots.DerivedStats.MoveSpeed, Is.GreaterThan(baseline.DerivedStats.MoveSpeed));
            for (int stat = 0; stat < StatInfo.UsedCount; stat++)
            {
                SurvivorConfig config = SurvivorTestHelpers.Config(); config.Build.Points[stat] = 1; SurvivorSim changed = new SurvivorSim(config, 2);
                Assert.That(changed.Config.Build.Level, Is.EqualTo(1));
            }
        }

        [Test]
        public void StatPoint_ChangesExactlyItsOwnDerivedStat()
        {
            SurvivorClassDef warrior = SurvivorDefaults.Warrior();
            float[] baseline = Derived(new SurvivorSim(SurvivorTestHelpers.Config(), 1).DerivedStats);
            for (int stat = 0; stat < StatInfo.UsedCount; stat++)
            {
                SurvivorConfig config = SurvivorTestHelpers.Config(); config.Build.Points[stat] = 1;
                float[] changed = Derived(new SurvivorSim(config, 1).DerivedStats);
                // Derived() lists the stats in StatId order, so stat k must move only entry k.
                float perPoint = StatInfo.PerPoint((StatId)stat);
                float scale = (StatId)stat switch { StatId.MaxHp => warrior.MaxHp, StatId.MoveSpeed => warrior.MoveSpeed, StatId.Magnet => warrior.PickupRadius, _ => 1f };
                for (int k = 0; k < changed.Length; k++)
                {
                    if (k == stat) Assert.That(changed[k] - baseline[k], Is.EqualTo(perPoint * scale).Within(1e-4f), ((StatId)stat).ToString());
                    else Assert.That(changed[k], Is.EqualTo(baseline[k]), ((StatId)stat) + " also moved derived stat " + k);
                }
            }
        }

        [Test]
        public void PassiveLevel_ChangesExactlyItsOwnDerivedStat()
        {
            SurvivorClassDef warrior = SurvivorDefaults.Warrior();
            float[] baseline = Derived(new SurvivorSim(SurvivorTestHelpers.Config(), 1).DerivedStats);
            int[] items = { SurvivorCatalog.IronHeartIndex, SurvivorCatalog.BoneArmorIndex, SurvivorCatalog.MightGauntletIndex, SurvivorCatalog.CritEyeIndex,
                SurvivorCatalog.HourglassIndex, SurvivorCatalog.AreaCharmIndex, SurvivorCatalog.WindBootsIndex, SurvivorCatalog.MagnetCharmIndex };
            int[] derivedIndex = { (int)StatId.MaxHp, (int)StatId.Armor, (int)StatId.Might, (int)StatId.Crit,
                (int)StatId.Cooldown, (int)StatId.Area, (int)StatId.MoveSpeed, (int)StatId.Magnet };
            float[] expected = { warrior.MaxHp * 0.1f * 2f, 2f, 0.08f * 2f, 0.04f * 2f,
                -0.06f * 2f, 0.08f * 2f, warrior.MoveSpeed * 0.08f * 2f, warrior.PickupRadius * 0.25f * 2f };
            for (int n = 0; n < items.Length; n++)
            {
                Assert.That(SurvivorCatalog.Get(items[n]).PerLevel, Is.EqualTo(SurvivorCatalog.PassivePerLevel(items[n])));
                SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 1); sim.GiveItemForTests(items[n], 2);
                float[] changed = Derived(sim.DerivedStats);
                for (int k = 0; k < changed.Length; k++)
                {
                    if (k == derivedIndex[n]) Assert.That(changed[k] - baseline[k], Is.EqualTo(expected[n]).Within(1e-4f), "item " + items[n]);
                    else Assert.That(changed[k], Is.EqualTo(baseline[k]), "item " + items[n] + " also moved derived stat " + k);
                }
            }
        }

        [Test]
        public void Tuning_DefaultsValidate_AndRejectBadValues()
        {
            Assert.DoesNotThrow(() => new SurvivorConfig().Validate());
            SurvivorConfig negative = new SurvivorConfig(); negative.Tuning.GoldChance = -0.1f;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => negative.Validate());
            SurvivorConfig ring = new SurvivorConfig(); ring.Tuning.SpawnRingMin = 30f;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ring.Validate());
        }

        // Same order as StatId (MaxHp..Growth), then TierGold.
        private static float[] Derived(SurvivorDerivedStats s) => new[]
        {
            s.MaxHp, s.Armor, s.Regen, s.Might, s.CritChance, s.CritDamage, s.CooldownMul, s.AreaMul,
            s.MoveSpeed, s.PickupRadius, s.Luck, s.GreedMul, s.GrowthMul, s.TierGold
        };
    }
}
