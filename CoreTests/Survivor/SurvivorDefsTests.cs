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
            Assert.That(warrior.WeaponPool, Is.EqualTo(new[] { 0, 3 })); Assert.That(warrior.PassivePool, Is.EqualTo(new[] { 6, 7, 9, 12 }));
            Assert.That(SurvivorCatalog.Get(0).Id, Is.EqualTo("sword-sweep")); Assert.That(SurvivorCatalog.Get(1), Is.Null); Assert.That(SurvivorCatalog.Get(61), Is.Null);
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
    }
}
