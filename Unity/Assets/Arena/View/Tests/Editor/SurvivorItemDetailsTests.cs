using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View.Tests
{
    public sealed class SurvivorItemDetailsTests
    {
        [Test]
        public void EveryCatalogItem_HasASummary()
        {
            int items = 0;
            for (int index = 0; index < SurvivorCatalog.CatalogSize; index++)
            {
                ItemDef def = SurvivorCatalog.Get(index);
                if (def == null)
                {
                    continue;
                }
                items++;
                Assert.That(SurvivorItemDetails.Summary(index), Is.Not.Empty, def.Id);
                Assert.That(SurvivorItemDetails.CardText(index, 1), Is.Not.Empty, def.Id);
            }
            Assert.That(items, Is.GreaterThan(80));
        }

        [Test]
        public void EveryLevelOfAnItem_SaysWhatChanges()
        {
            for (int index = 0; index < SurvivorCatalog.CatalogSize; index++)
            {
                ItemDef def = SurvivorCatalog.Get(index);
                for (int level = 2; def != null && level <= def.MaxLevel; level++)
                {
                    Assert.That(SurvivorItemDetails.Upgrade(index, level), Is.Not.Empty, def.Id + " level " + level);
                }
            }
        }

        [Test]
        public void Stats_ReadTheCatalogNumbers()
        {
            string stats = SurvivorItemDetails.Stats(SurvivorCatalog.SweepIndex, 3);
            Assert.That(stats, Does.Contain("Damage 36"));
            Assert.That(stats, Does.Contain("Cooldown 1.2 s"));
            Assert.That(stats, Does.Contain("Reach 3 m"));
            Assert.That(stats, Does.Not.Contain("behind"));
            Assert.That(SurvivorItemDetails.Stats(SurvivorCatalog.SweepIndex, 5), Does.Contain("Also hits behind the hero"));
            Assert.That(SurvivorItemDetails.Stats(SurvivorCatalog.AuraIndex, 1), Does.Contain("Damage 5 every 0.4 s"));
            Assert.That(SurvivorItemDetails.Stats(SurvivorCatalog.IronHeartIndex, 2), Is.EqualTo("Now +20% max HP"));
        }

        [Test]
        public void Upgrade_ListsOnlyTheChangedNumbers()
        {
            string upgrade = SurvivorItemDetails.Upgrade(SurvivorCatalog.SpearThrustIndex, 3);
            Assert.That(upgrade, Does.Contain("Damage 40 → 50"));
            Assert.That(upgrade, Does.Contain("Lines 1 → 2"));
            Assert.That(upgrade, Does.Not.Contain("Cooldown"));
            Assert.That(SurvivorItemDetails.Upgrade(SurvivorCatalog.IronHeartIndex, 3), Is.EqualTo("Total +30% max HP\n(+10% max HP per level)"));
            Assert.That(SurvivorItemDetails.Upgrade(SurvivorCatalog.SweepIndex, 1), Is.Empty);
        }

        [Test]
        public void Evolution_NamesTheRecipeBothWays()
        {
            Assert.That(SurvivorItemDetails.Evolution(SurvivorCatalog.SweepIndex),
                Is.EqualTo("Evolves into Storm Blade: reach Lv 5, own Power Gauntlet, then open a chest."));
            Assert.That(SurvivorItemDetails.Evolution(SurvivorCatalog.EvolutionOf(SurvivorCatalog.SweepIndex)),
                Is.EqualTo("Evolved from Sweeping Blade with Power Gauntlet."));
            Assert.That(SurvivorItemDetails.Evolution(SurvivorCatalog.MightGauntletIndex), Does.StartWith("Evolves: Sweeping Blade → Storm Blade"));
            Assert.That(SurvivorItemDetails.Evolution(SurvivorCatalog.BonusGoldIndex), Is.Empty);
        }

        [Test]
        public void SkillTooltip_HasTheDescriptionAndTheNumbers()
        {
            SkillDef kick = new SkillDef { Id = "kick", Kind = SkillKind.Kick, Cooldown = 3f, Damage = 10f, Range = 1.8f, StunSeconds = 1.2f };
            string text = SurvivorItemDetails.SkillTooltip(kick);
            Assert.That(text, Does.StartWith("Kicks enemies in front away and stuns them."));
            Assert.That(text, Does.Contain("Cooldown 3 s"));
            Assert.That(text, Does.Contain("Stun 1.2 s"));
            Assert.That(SurvivorItemDetails.SkillTooltip(null), Is.Empty);
        }
    }
}
