using System;
using System.IO;
using System.Text;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    internal static class SurvivorTestHelpers
    {
        public static SurvivorConfig Config(int tier = 1, float runSeconds = 900f)
        {
            SurvivorConfig config = new SurvivorConfig { ObstacleCount = 0, RunSeconds = runSeconds };
            config.Build.Tier = tier; return config;
        }

        /// <summary>
        /// The pre-M9 rules: 4 + 4 slots, the old item pools, no fourth Warrior skill and no skills 5-6 for any class. Used by the bit-exact goldens and by tests
        /// of the old slot behaviour; new-content tests use <see cref="Config"/> as is.
        /// </summary>
        public static SurvivorConfig OldRules(SurvivorConfig config)
        {
            config.Tuning.MaxWeaponSlots = 4; config.Tuning.MaxPassiveSlots = 4;
            config.ClassDef.PassivePool = new[] { 6, 7, 8, 9, 10, 11, 12, 13 };
            for (int slot = 4; slot < SurvivorInput.SkillSlotCount; slot++) config.ClassDef.ActiveSkills[slot] = new SkillDef { Id = "none", Kind = SkillKind.None };
            if (config.ClassDef.Id == "mage") config.ClassDef.WeaponPool = new[] { 14, 15, 16, 17, 18, 19 };
            if (config.ClassDef.Id == "archer") config.ClassDef.WeaponPool = new[] { 20, 21, 22, 23, 24, 25 };
            if (config.ClassDef.Id == "warrior")
            {
                config.ClassDef.WeaponPool = new[] { 0, 1, 2, 3, 4, 5 };
                config.ClassDef.ActiveSkills[3] = new SkillDef { Id = "none", Kind = SkillKind.None };
            }
            return config;
        }

        public static SurvivorConfig OldConfig(int tier = 1, float runSeconds = 900f) => OldRules(Config(tier, runSeconds));

        public static void Step(SurvivorSim sim, int ticks, SurvivorInput input = default)
        {
            for (int i = 0; i < ticks && !sim.IsEnded; i++)
            {
                sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : input);
            }
        }

        /// <summary>Zero-weight brain; a non-negative favoured index gets a bias of 10 in its branch.</summary>
        public static PolicyBrain Brain(int observation = SurvivorObservation.Size, int move = SurvivorInput.MoveBranchSize, int skill = SurvivorInput.SkillBranchSize, int pick = SurvivorInput.PickBranchSize, int favouredMove = -1, int favouredPick = -1)
        {
            using MemoryStream stream = new MemoryStream(); using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(new byte[] { (byte)'P', (byte)'A', (byte)'B', (byte)'R' }); writer.Write(PolicyBrain.FormatVersion);
            byte[] name = Encoding.UTF8.GetBytes("test"); writer.Write(name.Length); writer.Write(name); writer.Write(0L); writer.Write(observation); writer.Write(0); writer.Write(3);
            Layer(writer, observation, move, favouredMove); Layer(writer, observation, skill, -1); Layer(writer, observation, pick, favouredPick); writer.Write(0); writer.Flush(); return PolicyBrain.Load(stream.ToArray());
        }

        private static void Layer(BinaryWriter writer, int inputs, int outputs, int favoured)
        {
            writer.Write(inputs); writer.Write(outputs); for (int i = 0; i < inputs * outputs; i++) writer.Write(0f);
            for (int i = 0; i < outputs; i++) writer.Write(i == favoured ? 10f : 0f);
        }
    }
}
