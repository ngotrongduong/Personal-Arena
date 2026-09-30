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

        public static void Step(SurvivorSim sim, int ticks, SurvivorInput input = default)
        {
            for (int i = 0; i < ticks && !sim.IsEnded; i++)
            {
                sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : input);
            }
        }

        /// <summary>Zero-weight brain; a non-negative favoured index gets a bias of 10 in its branch.</summary>
        public static PolicyBrain Brain(int observation = SurvivorObservation.Size, int move = 9, int skill = 5, int pick = 5, int favouredMove = -1, int favouredPick = -1)
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
