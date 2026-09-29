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

        public static PolicyBrain Brain(int observation = SurvivorObservation.Size, int move = 9, int skill = 5, int pick = 5)
        {
            using MemoryStream stream = new MemoryStream(); using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(new byte[] { (byte)'P', (byte)'A', (byte)'B', (byte)'R' }); writer.Write(PolicyBrain.FormatVersion);
            byte[] name = Encoding.UTF8.GetBytes("test"); writer.Write(name.Length); writer.Write(name); writer.Write(0L); writer.Write(observation); writer.Write(0); writer.Write(3);
            Layer(writer, observation, move); Layer(writer, observation, skill); Layer(writer, observation, pick); writer.Write(0); writer.Flush(); return PolicyBrain.Load(stream.ToArray());
        }

        private static void Layer(BinaryWriter writer, int inputs, int outputs)
        {
            writer.Write(inputs); writer.Write(outputs); for (int i = 0; i < inputs * outputs + outputs; i++) writer.Write(0f);
        }
    }
}
