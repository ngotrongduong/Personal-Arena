using System.Collections.Generic;
using System.Text;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View
{
    /// <summary>The pages of the codex panel.</summary>
    public enum CodexTab
    {
        Weapons,
        Passives,
        Evolutions,
        Skills
    }

    /// <summary>A skill listed in the codex: the class it belongs to and its slot (0-based).</summary>
    public readonly struct CodexSkill
    {
        public readonly SkillDef Skill;
        public readonly string ClassId;
        public readonly int Slot;

        public CodexSkill(SkillDef skill, string classId, int slot)
        {
            Skill = skill;
            ClassId = classId;
            Slot = slot;
        }
    }

    /// <summary>
    /// What the codex lists and says: every weapon, passive, evolution and skill, for one class or for all of
    /// them, with the full text of an entry (numbers of every level, evolution recipe, classes).
    /// </summary>
    public static class CodexLogic
    {
        /// <summary>Catalog rows of a page in catalog order; a null or unknown class lists every class's rows.</summary>
        public static List<int> Items(CodexTab tab, string classId)
        {
            List<int> items = new List<int>();
            SurvivorClassDef kit = string.IsNullOrEmpty(classId) ? null : SurvivorDefaults.ForClass(classId);
            for (int index = 0; index < SurvivorCatalog.CatalogSize; index++)
            {
                ItemDef def = SurvivorCatalog.Get(index);
                if (def != null && OnPage(def, tab) && (kit == null || Offered(kit, def)))
                {
                    items.Add(index);
                }
            }
            return items;
        }

        /// <summary>The skills of a class in slot order; a null or unknown class lists every class's skills.</summary>
        public static List<CodexSkill> Skills(string classId)
        {
            List<CodexSkill> skills = new List<CodexSkill>();
            foreach (string id in ClassViewLogic.ClassIds)
            {
                SurvivorClassDef kit = SurvivorDefaults.ForClass(id);
                if (kit == null || (!string.IsNullOrEmpty(classId) && SurvivorDefaults.ForClass(classId) != null && id != classId))
                {
                    continue;
                }
                for (int slot = 0; slot < kit.ActiveSkills.Length; slot++)
                {
                    if (SurvivorViewLogic.SkillVisible(kit.ActiveSkills[slot]))
                    {
                        skills.Add(new CodexSkill(kit.ActiveSkills[slot], id, slot));
                    }
                }
            }
            return skills;
        }

        /// <summary>True when the class can get the row: a weapon or passive of its pools, its starting weapon, or the evolution of one of its weapons.</summary>
        public static bool Offered(SurvivorClassDef kit, ItemDef def)
        {
            if (kit == null || def == null)
            {
                return false;
            }
            if (def.EvolvesFrom >= 0)
            {
                return HasWeapon(kit, def.EvolvesFrom);
            }
            if (def.Kind == ItemKind.Weapon)
            {
                return HasWeapon(kit, def.CatalogIndex);
            }
            return def.Kind == ItemKind.Passive && Contains(kit.PassivePool, def.CatalogIndex);
        }

        /// <summary>The classes that can get the row, e.g. "Warrior, Mage".</summary>
        public static string ClassesOf(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            StringBuilder builder = new StringBuilder();
            foreach (string id in ClassViewLogic.ClassIds)
            {
                if (Offered(SurvivorDefaults.ForClass(id), def))
                {
                    builder.Append(builder.Length == 0 ? string.Empty : ", ").Append(ClassViewLogic.DisplayName(id));
                }
            }
            return builder.ToString();
        }

        /// <summary>Full text of an item: what it does, its numbers, what every level adds, its evolution and its classes.</summary>
        public static string Details(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(SurvivorItemDetails.Summary(catalogIndex));
            if (def.Kind == ItemKind.Weapon)
            {
                Block(builder, def.MaxLevel > 1 ? "LEVEL 1" : "NUMBERS", SurvivorItemDetails.Stats(catalogIndex, 1));
                if (def.MaxLevel > 1)
                {
                    StringBuilder upgrades = new StringBuilder();
                    for (int level = 2; level <= def.MaxLevel; level++)
                    {
                        upgrades.Append(level == 2 ? string.Empty : "\n").Append("Lv ").Append(level).Append("   ")
                            .Append(SurvivorItemDetails.Upgrade(catalogIndex, level).Replace("\n", ", "));
                    }
                    Block(builder, "UPGRADES", upgrades.ToString());
                }
            }
            else if (def.Kind == ItemKind.Passive)
            {
                Block(builder, "AT MAX LEVEL (" + def.MaxLevel + ")", SurvivorItemDetails.PassiveTotal(catalogIndex, def.MaxLevel));
            }

            Block(builder, def.EvolvesFrom >= 0 ? "RECIPE" : "EVOLUTION", Recipe(def));
            Block(builder, "CLASSES", ClassesOf(catalogIndex));
            return builder.ToString();
        }

        /// <summary>Full text of a skill: its description, numbers, class and slot.</summary>
        public static string Details(CodexSkill entry)
        {
            StringBuilder builder = new StringBuilder(SurvivorItemDetails.SkillTooltip(entry.Skill));
            Block(builder, "CLASS", ClassViewLogic.DisplayName(entry.ClassId) + ", skill " + (entry.Slot + 1));
            return builder.ToString();
        }

        private static string Recipe(ItemDef def)
        {
            if (def.EvolvesFrom < 0)
            {
                return SurvivorItemDetails.Evolution(def.CatalogIndex);
            }
            ItemDef baseDef = SurvivorCatalog.Get(def.EvolvesFrom);
            ItemDef passive = SurvivorCatalog.Get(def.EvolutionPassive);
            return baseDef == null ? string.Empty : baseDef.Name + " at Lv " + baseDef.MaxLevel +
                (passive != null ? " + " + passive.Name : string.Empty) + ", then open a chest.";
        }

        private static bool OnPage(ItemDef def, CodexTab tab)
        {
            switch (tab)
            {
                case CodexTab.Weapons: return def.Kind == ItemKind.Weapon && def.EvolvesFrom < 0;
                case CodexTab.Passives: return def.Kind == ItemKind.Passive;
                case CodexTab.Evolutions: return def.EvolvesFrom >= 0;
                default: return false;
            }
        }

        private static bool HasWeapon(SurvivorClassDef kit, int weapon)
        {
            return kit.StartingWeapon == weapon || Contains(kit.WeaponPool, weapon);
        }

        private static bool Contains(int[] pool, int index)
        {
            return pool != null && System.Array.IndexOf(pool, index) >= 0;
        }

        private static void Block(StringBuilder builder, string heading, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }
            builder.Append(heading).Append('\n').Append(text);
        }
    }
}
