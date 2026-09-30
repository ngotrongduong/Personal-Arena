using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Tools.SurvivorEval
{
    internal static class Program
    {
        private sealed class Options
        {
            public string Brain;
            public int Seeds = 100;
            public int SeedStart = 1000000;
            public int Tier = 1;
            public float RunSeconds = 900f;
            public bool Deterministic;
            public int Threads = Environment.ProcessorCount;
            public string Out;
            public bool SelfTest;
        }

        private sealed class Output
        {
            [JsonPropertyName("summary")]
            public SurvivorEvalSummary Summary { get; set; }
            [JsonPropertyName("passes_m4a")]
            public bool PassesM4A { get; set; }
            [JsonPropertyName("runs")]
            public SurvivorRunStats[] Runs { get; set; }
        }

        public static int Main(string[] args)
        {
            try
            {
                Options options = Parse(args);
                byte[] brainData = options.SelfTest ? FakeBrain() : File.ReadAllBytes(options.Brain);
                string validation = SurvivorPilot.Validate(PolicyBrain.Load(brainData));
                if (validation != null) throw new ArgumentException(validation);
                SurvivorRunStats[] runs = new SurvivorRunStats[options.Seeds];
                // One brain per worker thread: PolicyBrain keeps evaluation buffers and is not thread-safe.
                Parallel.For(0, runs.Length, new ParallelOptions { MaxDegreeOfParallelism = options.Threads },
                    () => PolicyBrain.Load(brainData),
                    (i, state, brain) =>
                    {
                        SurvivorConfig config = new SurvivorConfig { RunSeconds = options.RunSeconds };
                        config.Build.Tier = options.Tier;
                        runs[i] = new SurvivorEvaluator().RunOne(brain, config, options.SeedStart + i, options.Deterministic);
                        return brain;
                    },
                    brain => { });
                SurvivorEvaluator evaluator = new SurvivorEvaluator(); SurvivorEvalSummary summary = evaluator.Summarize(runs); bool passes = evaluator.PassesM4A(summary);
                Print(summary, passes);
                if (options.Out != null)
                {
                    JsonSerializerOptions jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    jsonOptions.Converters.Add(new JsonStringEnumConverter());
                    File.WriteAllText(options.Out, JsonSerializer.Serialize(new Output { Summary = summary, PassesM4A = passes, Runs = runs }, jsonOptions));
                }
                return 0;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException || exception is FormatException || exception is OverflowException)
            {
                Console.Error.WriteLine("Error: " + exception.Message); return 2;
            }
            catch (AggregateException exception)
            {
                // Errors thrown inside Parallel.For arrive wrapped; report each inner message.
                foreach (Exception inner in exception.Flatten().InnerExceptions) Console.Error.WriteLine("Error: " + inner.Message);
                return 2;
            }
        }

        private static Options Parse(string[] args)
        {
            Options result = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--deterministic") result.Deterministic = true;
                else if (arg == "--self-test") { result.SelfTest = true; result.Seeds = 3; result.RunSeconds = 60f; result.Deterministic = true; }
                else
                {
                    if (++i >= args.Length) throw new ArgumentException("Missing value for " + arg + ".");
                    string value = args[i];
                    if (arg == "--brain") result.Brain = value;
                    else if (arg == "--seeds") result.Seeds = int.Parse(value);
                    else if (arg == "--seed-start") result.SeedStart = int.Parse(value);
                    else if (arg == "--tier") result.Tier = int.Parse(value);
                    else if (arg == "--run-seconds") result.RunSeconds = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    else if (arg == "--threads") result.Threads = int.Parse(value);
                    else if (arg == "--out") result.Out = value;
                    else throw new ArgumentException("Unknown argument " + arg + ".");
                }
            }
            if (!result.SelfTest && string.IsNullOrWhiteSpace(result.Brain)) throw new ArgumentException("--brain is required.");
            if (result.Seeds <= 0 || result.Threads <= 0 || result.Tier < 1 || result.Tier > 10 || result.RunSeconds < 60f || result.RunSeconds > 900f) throw new ArgumentException("An argument is outside its valid range.");
            return result;
        }

        private static void Print(SurvivorEvalSummary s, bool passes)
        {
            Console.WriteLine($"Runs: {s.Runs}"); Console.WriteLine($"Survival median / P10 / mean: {s.MedianSurvivedSeconds:0.0}s / {s.P10SurvivedSeconds:0.0}s / {s.MeanSurvivedSeconds:0.0}s");
            Console.WriteLine($"Win rate: {s.WinRate:P1}; catastrophic: {s.CatastrophicCount}");
            Console.WriteLine($"Gold/min: {s.GoldPerMinute:0.0}; XP/min: {s.XpPerMinute:0.0}; damage taken/min: {s.DamageTakenPerMinute:0.0}");
            Console.WriteLine("M4A acceptance: " + (passes ? "PASS" : "FAIL"));
        }

        private static byte[] FakeBrain()
        {
            using MemoryStream stream = new MemoryStream(); using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(new byte[] { (byte)'P', (byte)'A', (byte)'B', (byte)'R' }); writer.Write(PolicyBrain.FormatVersion);
            byte[] name = Encoding.UTF8.GetBytes("SurvivorSelfTest"); writer.Write(name.Length); writer.Write(name); writer.Write(0L); writer.Write(SurvivorObservation.Size); writer.Write(0); writer.Write(3);
            WriteLayer(writer, SurvivorObservation.Size, SurvivorInput.MoveBranchSize); WriteLayer(writer, SurvivorObservation.Size, SurvivorInput.SkillBranchSize); WriteLayer(writer, SurvivorObservation.Size, SurvivorInput.PickBranchSize); writer.Write(0); writer.Flush(); return stream.ToArray();
        }

        private static void WriteLayer(BinaryWriter writer, int inputs, int outputs)
        {
            writer.Write(inputs); writer.Write(outputs); for (int i = 0; i < inputs * outputs + outputs; i++) writer.Write(0f);
        }
    }
}
