using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public class PolicyBrainTests
    {
        [Test]
        public void EvaluateMatchesHandComputedSwishNetwork()
        {
            // 2 inputs -> 2 hidden (Swish) -> branch of 3 logits.
            byte[] data = BrainBytes(
                observationSize: 2,
                body: new[] { Layer(2, 2, new[] { 1f, 0f, 0f, -2f }, new[] { 0f, 0.5f }) },
                branches: new[] { Layer(2, 3, new[] { 1f, 0f, 0f, 1f, 1f, 1f }, new[] { 0f, 0f, 1f }) });
            PolicyBrain brain = PolicyBrain.Load(data);

            float[] logits = new float[3];
            brain.Evaluate(new[] { 2f, 1f }, logits);

            float h0 = Swish(2f);
            float h1 = Swish(-2f + 0.5f);
            Assert.That(logits[0], Is.EqualTo(h0).Within(1e-6f));
            Assert.That(logits[1], Is.EqualTo(h1).Within(1e-6f));
            Assert.That(logits[2], Is.EqualTo(h0 + h1 + 1f).Within(1e-6f));
            Assert.That(brain.BehaviorName, Is.EqualTo("Warrior"));
            Assert.That(brain.Step, Is.EqualTo(500000));
        }

        [Test]
        public void SelfTestReportsAgreementWithExporterLogits()
        {
            float[] observation = { 2f, 1f };
            float h0 = Swish(2f);
            float h1 = Swish(-1.5f);
            float[] expected = { h0, h1, h0 + h1 + 1f };
            byte[] good = BrainBytes(2,
                new[] { Layer(2, 2, new[] { 1f, 0f, 0f, -2f }, new[] { 0f, 0.5f }) },
                new[] { Layer(2, 3, new[] { 1f, 0f, 0f, 1f, 1f, 1f }, new[] { 0f, 0f, 1f }) },
                observation, expected);
            expected[2] += 1f;
            byte[] bad = BrainBytes(2,
                new[] { Layer(2, 2, new[] { 1f, 0f, 0f, -2f }, new[] { 0f, 0.5f }) },
                new[] { Layer(2, 3, new[] { 1f, 0f, 0f, 1f, 1f, 1f }, new[] { 0f, 0f, 1f }) },
                observation, expected);

            Assert.That(PolicyBrain.Load(good).SelfTestError(), Is.LessThan(1e-5f));
            Assert.That(PolicyBrain.Load(bad).SelfTestError(), Is.GreaterThan(0.1f));
        }

        [Test]
        public void LoadRejectsWrongMagicAndMismatchedShapes()
        {
            byte[] data = BrainBytes(2,
                new[] { Layer(2, 2, new float[4], new float[2]) },
                new[] { Layer(2, 3, new float[6], new float[3]) });
            data[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => PolicyBrain.Load(data));

            byte[] mismatched = BrainBytes(2,
                new[] { Layer(3, 2, new float[6], new float[2]) },
                new[] { Layer(2, 3, new float[6], new float[3]) });
            Assert.Throws<InvalidDataException>(() => PolicyBrain.Load(mismatched));

            byte[] truncated = BrainBytes(2,
                new[] { Layer(2, 2, new float[4], new float[2]) },
                new[] { Layer(2, 3, new float[6], new float[3]) });
            Array.Resize(ref truncated, truncated.Length - 10);
            Assert.That(() => PolicyBrain.Load(truncated), Throws.InstanceOf<Exception>());
        }

        [Test]
        public void ChooseActionNeverPicksMaskedActions()
        {
            float[] logits = { 0f, 50f, 1f, 2f };
            bool[] allowed = { true, false, true, true };

            Assert.That(PolicyBrain.ChooseAction(logits, 0, 4, allowed, null), Is.EqualTo(3));
            Rng rng = new Rng(3);
            for (int i = 0; i < 2000; i++)
            {
                Assert.That(PolicyBrain.ChooseAction(logits, 0, 4, allowed, rng), Is.Not.EqualTo(1));
            }
        }

        [Test]
        public void ChooseActionSamplesInProportionToSoftmax()
        {
            float[] logits = { 9f, 0f, MathF.Log(3f) };
            Rng rng = new Rng(11);
            int[] counts = new int[2];
            const int samples = 20000;
            for (int i = 0; i < samples; i++)
            {
                int action = PolicyBrain.ChooseAction(logits, 1, 2, null, rng);
                counts[action]++;
            }

            // Branch starts at offset 1: logits {0, ln 3} -> probabilities 0.25 / 0.75.
            Assert.That(counts[1] / (float)samples, Is.EqualTo(0.75f).Within(0.02f));
        }

        [Test]
        public void PilotDecidesEveryFiveTicksWithTrainingLayout()
        {
            BrainPilot pilot = new BrainPilot(5);
            int observationSize = pilot.ObservationSize;
            // Bias-only brain: move 3, turn branch 2 (+1), skill 1 (strike) are clearly preferred.
            byte[] data = BrainBytes(observationSize,
                new[] { Layer(observationSize, 4, new float[observationSize * 4], new float[4]) },
                new[]
                {
                    Layer(4, HeroInput.MoveBranchSize, new float[4 * HeroInput.MoveBranchSize], OneHot(HeroInput.MoveBranchSize, 3, 40f)),
                    Layer(4, HeroInput.TurnBranchSize, new float[4 * HeroInput.TurnBranchSize], OneHot(HeroInput.TurnBranchSize, 2, 40f)),
                    Layer(4, HeroInput.SkillBranchSize, new float[4 * HeroInput.SkillBranchSize], OneHot(HeroInput.SkillBranchSize, 1, 40f))
                });
            PolicyBrain brain = PolicyBrain.Load(data);
            Assert.That(BrainPilot.Validate(brain), Is.Null);
            pilot.SetBrain(brain);
            pilot.Deterministic = true;

            ArenaSim sim = TestHelpers.Sim(zombieCount: 1);
            HeroInput[] inputs = new HeroInput[20];
            for (int tick = 0; tick < inputs.Length; tick++)
            {
                inputs[tick] = pilot.NextInput(sim);
                sim.Step(inputs[tick]);
            }

            Assert.That(inputs[0].Move, Is.EqualTo(3));
            Assert.That(inputs[0].Turn, Is.EqualTo(HeroInput.BranchToTurn(2)));
            Assert.That(inputs[0].Skill, Is.EqualTo(1));
            for (int tick = 0; tick < inputs.Length; tick++)
            {
                HeroInput blockStart = inputs[tick - tick % BrainPilot.DecisionPeriod];
                Assert.That(inputs[tick].Move, Is.EqualTo(blockStart.Move));
                Assert.That(inputs[tick].Turn, Is.EqualTo(blockStart.Turn));
                Assert.That(inputs[tick].Skill, Is.EqualTo(blockStart.Skill));
            }

            // Strike is on cooldown after the first swing, so the mask must steer later decisions away from it.
            Assert.That(inputs[BrainPilot.DecisionPeriod].Skill, Is.Not.EqualTo(1));
        }

        [Test]
        public void ValidateRejectsBrainsForAnotherGameLayout()
        {
            byte[] data = BrainBytes(2,
                new[] { Layer(2, 2, new float[4], new float[2]) },
                new[] { Layer(2, 3, new float[6], new float[3]) });
            Assert.That(BrainPilot.Validate(PolicyBrain.Load(data)), Is.Not.Null);
            Assert.Throws<ArgumentException>(() => new BrainPilot().SetBrain(PolicyBrain.Load(data)));
        }

        [Test]
        public void ExportedTrainingBrainsMatchTheirReferenceLogits()
        {
            string runs = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Trainer", "runs"));
            string[] files = Directory.Exists(runs)
                ? Directory.GetFiles(runs, "*.brain", SearchOption.AllDirectories)
                : Array.Empty<string>();
            if (files.Length == 0)
            {
                Assert.Ignore("No exported brains under Trainer/runs.");
            }

            foreach (string file in files)
            {
                PolicyBrain brain = PolicyBrain.Load(File.ReadAllBytes(file));
                Assert.That(brain.HasSelfTest, Is.True, file);
                Assert.That(brain.SelfTestError(), Is.LessThan(1e-3f), file);
                Assert.That(BrainPilot.Validate(brain), Is.Null, file);
            }
        }

        private static float Swish(float x) => x / (1f + MathF.Exp(-x));

        private static float[] OneHot(int size, int index, float value)
        {
            float[] values = new float[size];
            values[index] = value;
            return values;
        }

        private static (int Inputs, int Outputs, float[] Weights, float[] Bias) Layer(int inputs, int outputs, float[] weights, float[] bias) =>
            (inputs, outputs, weights, bias);

        private static byte[] BrainBytes(
            int observationSize,
            (int Inputs, int Outputs, float[] Weights, float[] Bias)[] body,
            (int Inputs, int Outputs, float[] Weights, float[] Bias)[] branches,
            float[] testObservation = null,
            float[] testLogits = null)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Encoding.ASCII.GetBytes("PABR"));
                writer.Write(PolicyBrain.FormatVersion);
                byte[] name = Encoding.UTF8.GetBytes("Warrior");
                writer.Write(name.Length);
                writer.Write(name);
                writer.Write(500000L);
                writer.Write(observationSize);
                WriteLayers(writer, body);
                WriteLayers(writer, branches);
                writer.Write(testObservation == null ? 0 : 1);
                if (testObservation != null)
                {
                    WriteFloats(writer, testObservation);
                    WriteFloats(writer, testLogits);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void WriteLayers(BinaryWriter writer, (int Inputs, int Outputs, float[] Weights, float[] Bias)[] layers)
        {
            writer.Write(layers.Length);
            foreach ((int inputs, int outputs, float[] weights, float[] bias) in layers)
            {
                writer.Write(inputs);
                writer.Write(outputs);
                WriteFloats(writer, weights);
                WriteFloats(writer, bias);
            }
        }

        private static void WriteFloats(BinaryWriter writer, float[] values)
        {
            foreach (float value in values)
            {
                writer.Write(value);
            }
        }
    }
}
