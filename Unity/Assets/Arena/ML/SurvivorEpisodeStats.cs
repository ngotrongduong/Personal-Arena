using System;
using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.ML
{
    /// <summary>End-of-episode statistics written to TensorBoard under the <c>Arena/</c> prefix.</summary>
    public static class SurvivorEpisodeStats
    {
        private static readonly DeathCause[] Causes = (DeathCause[])Enum.GetValues(typeof(DeathCause));

        /// <summary>Appends every stat for a finished run to <paramref name="output"/>.</summary>
        public static void Collect(SurvivorSim sim, List<KeyValuePair<string, float>> output)
        {
            if (sim == null)
            {
                throw new ArgumentNullException(nameof(sim));
            }
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            bool died = sim.EndReason == EndReason.Died;
            Add(output, "Arena/SurvivedSeconds", sim.Time);
            Add(output, "Arena/SurvivedRatio", sim.Config.RunSeconds > 0f ? Math.Min(1f, sim.Time / sim.Config.RunSeconds) : 0f);
            Add(output, "Arena/RunSeconds", sim.Config.RunSeconds);
            Add(output, "Arena/Level", sim.Level);
            Add(output, "Arena/Gold", sim.Gold);
            Add(output, "Arena/Xp", sim.TotalXp);
            Add(output, "Arena/Kills", sim.Kills);
            Add(output, "Arena/EliteKills", sim.EliteKills);
            Add(output, "Arena/DamageTaken", sim.DamageTaken);
            Add(output, "Arena/DamageDealt", sim.DamageDealtTotal);
            Add(output, "Arena/BossDamage", sim.BossDamageFraction);
            Add(output, "Arena/MinHpRatio", sim.MinHpRatio);
            Add(output, "Arena/Died", died ? 1f : 0f);
            Add(output, "Arena/Won", sim.EndReason == EndReason.Won ? 1f : 0f);
            Add(output, "Arena/Catastrophic", died && sim.Time < 180f ? 1f : 0f);
            Add(output, "Arena/Tier", sim.Config.Build.Tier);
            Add(output, "Arena/BuildLevel", sim.Config.Build.Level);

            for (int i = 0; i < Causes.Length; i++)
            {
                if (Causes[i] != DeathCause.None)
                {
                    Add(output, "Arena/DeathCause/" + Causes[i], died && sim.DeathCause == Causes[i] ? 1f : 0f);
                }
            }

            SkillDef[] skills = sim.Config.ClassDef.ActiveSkills;
            for (int i = 0; i < skills.Length && i < sim.SkillUses.Length; i++)
            {
                if (skills[i] != null && skills[i].Kind != SkillKind.None)
                {
                    Add(output, "Arena/Skill/" + skills[i].Id, sim.SkillUses[i]);
                }
            }

            AddItems(output, sim, sim.Config.ClassDef.WeaponPool);
            AddItems(output, sim, sim.Config.ClassDef.PassivePool);
        }

        private static void AddItems(List<KeyValuePair<string, float>> output, SurvivorSim sim, int[] pool)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                ItemDef def = SurvivorCatalog.Get(pool[i]);
                if (def != null)
                {
                    Add(output, "Arena/Item/" + def.Id, sim.Inventory.Level(pool[i]));
                }
            }
        }

        private static void Add(List<KeyValuePair<string, float>> output, string key, float value)
        {
            output.Add(new KeyValuePair<string, float>(key, value));
        }
    }
}
