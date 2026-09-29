using System;
using System.IO;
using System.Text;

namespace PersonalArena.Core
{
    /// <summary>
    /// A trained discrete policy (ML-Agents SimpleActor: Linear+Swish body, one linear head per branch)
    /// evaluated in plain C#, so a player build can run checkpoints exported by Trainer/export_brain.py.
    /// </summary>
    public sealed class PolicyBrain
    {
        public const int FormatVersion = 1;
        private static readonly byte[] Magic = { (byte)'P', (byte)'A', (byte)'B', (byte)'R' };

        private readonly DenseLayer[] body;
        private readonly DenseLayer[] branches;
        private readonly float[][] activations;
        private readonly float[] selfTestObservation;
        private readonly float[] selfTestLogits;

        private PolicyBrain(
            string behaviorName,
            long step,
            int observationSize,
            DenseLayer[] body,
            DenseLayer[] branches,
            float[] selfTestObservation,
            float[] selfTestLogits)
        {
            BehaviorName = behaviorName;
            Step = step;
            ObservationSize = observationSize;
            this.body = body;
            this.branches = branches;
            this.selfTestObservation = selfTestObservation;
            this.selfTestLogits = selfTestLogits;
            activations = new float[body.Length][];
            for (int i = 0; i < body.Length; i++)
            {
                activations[i] = new float[body[i].Outputs];
            }
        }

        public string BehaviorName { get; }
        public long Step { get; }
        public int ObservationSize { get; }
        public int BranchCount => branches.Length;
        public bool HasSelfTest => selfTestObservation != null;

        public int BranchSize(int branch) => branches[branch].Outputs;

        public static PolicyBrain Load(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            using (MemoryStream stream = new MemoryStream(data, false))
            {
                return Load(stream);
            }
        }

        public static PolicyBrain Load(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                byte[] magic = reader.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != Magic[0] || magic[1] != Magic[1] ||
                    magic[2] != Magic[2] || magic[3] != Magic[3])
                {
                    throw new InvalidDataException("Not a Personal Arena brain file.");
                }

                int version = reader.ReadInt32();
                if (version != FormatVersion)
                {
                    throw new InvalidDataException("Unsupported brain format version " + version + ".");
                }

                int nameLength = ReadCount(reader, 256);
                string behaviorName = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                long step = reader.ReadInt64();
                int observationSize = ReadCount(reader, 65536);
                if (observationSize == 0)
                {
                    throw new InvalidDataException("Observation size must be positive.");
                }

                DenseLayer[] body = new DenseLayer[ReadCount(reader, 16)];
                int width = observationSize;
                for (int i = 0; i < body.Length; i++)
                {
                    body[i] = DenseLayer.Read(reader, width);
                    width = body[i].Outputs;
                }

                DenseLayer[] branches = new DenseLayer[ReadCount(reader, 16)];
                if (branches.Length == 0)
                {
                    throw new InvalidDataException("A brain needs at least one action branch.");
                }

                int logitCount = 0;
                for (int i = 0; i < branches.Length; i++)
                {
                    branches[i] = DenseLayer.Read(reader, width);
                    logitCount += branches[i].Outputs;
                }

                float[] testObservation = null;
                float[] testLogits = null;
                int selfTests = ReadCount(reader, 1);
                if (selfTests == 1)
                {
                    testObservation = ReadFloats(reader, observationSize);
                    testLogits = ReadFloats(reader, logitCount);
                }

                return new PolicyBrain(behaviorName, step, observationSize, body, branches, testObservation, testLogits);
            }
        }

        /// <summary>Raw (unmasked) logits for every branch, concatenated in branch order.</summary>
        public void Evaluate(float[] observation, float[] logits)
        {
            if (observation == null)
            {
                throw new ArgumentNullException(nameof(observation));
            }

            if (logits == null)
            {
                throw new ArgumentNullException(nameof(logits));
            }

            if (observation.Length < ObservationSize)
            {
                throw new ArgumentException("Observation is too small for this brain.", nameof(observation));
            }

            float[] input = observation;
            for (int i = 0; i < body.Length; i++)
            {
                body[i].Forward(input, activations[i], 0);
                float[] output = activations[i];
                for (int j = 0; j < output.Length; j++)
                {
                    output[j] = Swish(output[j]);
                }
                input = output;
            }

            int offset = 0;
            for (int i = 0; i < branches.Length; i++)
            {
                if (logits.Length < offset + branches[i].Outputs)
                {
                    throw new ArgumentException("Logit buffer is too small for this brain.", nameof(logits));
                }

                branches[i].Forward(input, logits, offset);
                offset += branches[i].Outputs;
            }
        }

        /// <summary>Largest absolute difference between this runtime and the exporter's reference logits.</summary>
        public float SelfTestError()
        {
            if (!HasSelfTest)
            {
                return 0f;
            }

            float[] logits = new float[selfTestLogits.Length];
            Evaluate(selfTestObservation, logits);
            float worst = 0f;
            for (int i = 0; i < logits.Length; i++)
            {
                float scale = MathF.Max(1f, MathF.Abs(selfTestLogits[i]));
                worst = MathF.Max(worst, MathF.Abs(logits[i] - selfTestLogits[i]) / scale);
            }

            return worst;
        }

        /// <summary>
        /// Picks one action from a branch's logits, honouring a mask the way ML-Agents does
        /// (blocked actions are never chosen). Returns the argmax when <paramref name="rng"/> is null.
        /// </summary>
        public static int ChooseAction(float[] logits, int offset, int count, bool[] allowed, Rng rng)
        {
            if (logits == null)
            {
                throw new ArgumentNullException(nameof(logits));
            }

            if (count <= 0 || offset < 0 || offset + count > logits.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            float maximum = float.NegativeInfinity;
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (IsAllowed(allowed, i) && logits[offset + i] > maximum)
                {
                    maximum = logits[offset + i];
                    best = i;
                }
            }

            if (best < 0)
            {
                return 0;
            }

            if (rng == null)
            {
                return best;
            }

            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                if (IsAllowed(allowed, i))
                {
                    total += MathF.Exp(logits[offset + i] - maximum);
                }
            }

            float target = rng.NextFloat() * total;
            float cumulative = 0f;
            int last = best;
            for (int i = 0; i < count; i++)
            {
                if (!IsAllowed(allowed, i))
                {
                    continue;
                }

                cumulative += MathF.Exp(logits[offset + i] - maximum);
                last = i;
                if (target < cumulative)
                {
                    return i;
                }
            }

            return last;
        }

        private static bool IsAllowed(bool[] allowed, int index) => allowed == null || allowed[index];

        private static float Swish(float value) => value / (1f + MathF.Exp(-value));

        private static int ReadCount(BinaryReader reader, int maximum)
        {
            int value = reader.ReadInt32();
            if (value < 0 || value > maximum)
            {
                throw new InvalidDataException("Corrupt brain file (count " + value + ").");
            }

            return value;
        }

        private static float[] ReadFloats(BinaryReader reader, int count)
        {
            byte[] bytes = reader.ReadBytes(count * sizeof(float));
            if (bytes.Length != count * sizeof(float))
            {
                throw new EndOfStreamException("Brain file ended early.");
            }

            float[] values = new float[count];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            if (!BitConverter.IsLittleEndian)
            {
                throw new PlatformNotSupportedException("Brain files are little-endian.");
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                {
                    throw new InvalidDataException("Brain file contains a non-finite number.");
                }
            }

            return values;
        }

        private sealed class DenseLayer
        {
            private readonly float[] weights;
            private readonly float[] bias;

            private DenseLayer(int inputs, int outputs, float[] weights, float[] bias)
            {
                Inputs = inputs;
                Outputs = outputs;
                this.weights = weights;
                this.bias = bias;
            }

            public int Inputs { get; }
            public int Outputs { get; }

            public static DenseLayer Read(BinaryReader reader, int expectedInputs)
            {
                int inputs = ReadCount(reader, 65536);
                int outputs = ReadCount(reader, 65536);
                if (inputs != expectedInputs || outputs == 0)
                {
                    throw new InvalidDataException(
                        "Layer shape " + inputs + "x" + outputs + " does not follow width " + expectedInputs + ".");
                }

                float[] weights = ReadFloats(reader, inputs * outputs);
                float[] bias = ReadFloats(reader, outputs);
                return new DenseLayer(inputs, outputs, weights, bias);
            }

            public void Forward(float[] input, float[] output, int offset)
            {
                for (int row = 0; row < Outputs; row++)
                {
                    float sum = bias[row];
                    int start = row * Inputs;
                    for (int column = 0; column < Inputs; column++)
                    {
                        sum += weights[start + column] * input[column];
                    }

                    output[offset + row] = sum;
                }
            }
        }
    }
}
