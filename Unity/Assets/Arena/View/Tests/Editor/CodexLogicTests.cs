using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View.Tests
{
    public sealed class CodexLogicTests
    {
        [Test]
        public void Pages_ShareNoRow_AndEveryRowHasDetails()
        {
            HashSet<int> seen = new HashSet<int>();
            foreach (CodexTab tab in new[] { CodexTab.Weapons, CodexTab.Passives, CodexTab.Evolutions })
            {
                List<int> items = CodexLogic.Items(tab, null);
                Assert.That(items, Is.Not.Empty, tab.ToString());
                foreach (int item in items)
                {
                    Assert.That(seen.Add(item), Is.True, "row " + item + " is on two pages");
                    Assert.That(CodexLogic.Details(item), Does.Contain("CLASSES"), "row " + item);
                }
            }
            Assert.That(CodexLogic.Items(CodexTab.Skills, null), Is.Empty, "skills are listed by Skills()");
            Assert.That(seen.Contains(SurvivorCatalog.BonusGoldIndex), Is.False, "one-off rewards are not in the codex");
        }

        [TestCase("warrior")]
        [TestCase("mage")]
        [TestCase("archer")]
        public void AClass_ListsItsOwnPoolsAndTheEvolutionsOfItsWeapons(string classId)
        {
            SurvivorClassDef kit = SurvivorDefaults.ForClass(classId);
            List<int> weapons = CodexLogic.Items(CodexTab.Weapons, classId);
            Assert.That(weapons, Does.Contain(kit.StartingWeapon));
            foreach (int weapon in kit.WeaponPool)
            {
                Assert.That(weapons, Does.Contain(weapon));
            }
            Assert.That(weapons.Count, Is.LessThan(CodexLogic.Items(CodexTab.Weapons, null).Count), "other classes' weapons are left out");
            Assert.That(CodexLogic.Items(CodexTab.Passives, classId), Is.EquivalentTo(kit.PassivePool));
            foreach (int evolution in CodexLogic.Items(CodexTab.Evolutions, classId))
            {
                Assert.That(weapons, Does.Contain(SurvivorCatalog.Get(evolution).EvolvesFrom));
            }

            List<CodexSkill> skills = CodexLogic.Skills(classId);
            Assert.That(skills.Count, Is.GreaterThanOrEqualTo(4));
            foreach (CodexSkill skill in skills)
            {
                Assert.That(skill.ClassId, Is.EqualTo(classId));
                Assert.That(CodexLogic.Details(skill), Does.Contain("CLASS"));
            }
            Assert.That(CodexLogic.Skills(null).Count, Is.GreaterThan(skills.Count));
        }

        [Test]
        public void Details_GiveEveryLevelAndTheRecipe()
        {
            string sword = CodexLogic.Details(SurvivorCatalog.SweepIndex);
            Assert.That(sword, Does.Contain("LEVEL 1\nDamage 20"));
            Assert.That(sword, Does.Contain("Lv 2   Damage 20 → 28"));
            Assert.That(sword, Does.Contain("Lv 5   "));
            Assert.That(sword, Does.Contain("EVOLUTION\nEvolves into Storm Blade"));
            Assert.That(sword, Does.Contain("CLASSES\nWarrior"));

            string storm = CodexLogic.Details(SurvivorCatalog.EvolutionOf(SurvivorCatalog.SweepIndex));
            Assert.That(storm, Does.Contain("RECIPE\nSweeping Blade at Lv 5 + Power Gauntlet, then open a chest."));
            Assert.That(storm, Does.Not.Contain("UPGRADES"));

            string heart = CodexLogic.Details(SurvivorCatalog.IronHeartIndex);
            Assert.That(heart, Does.Contain("AT MAX LEVEL (5)\n+50% max HP"));
            Assert.That(heart, Does.Contain("EVOLUTION\nEvolves: "));
        }
    }
}
