using System;

namespace PersonalArena.Core
{
    /// <summary>Deterministic PCG32 random number generator.</summary>
    public sealed class Rng
    {
        private ulong state;
        private readonly ulong increment;

        public Rng(int seed)
        {
            state = 0UL;
            increment = ((ulong)(uint)seed << 1) | 1UL;
            NextUInt();
            state += (uint)seed ^ 0x9E3779B9U;
            NextUInt();
        }

        /// <summary>Returns a value in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Returns a value in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Returns an integer in [0, max).</summary>
        public int NextInt(int max)
        {
            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), "Maximum must be positive.");
            }

            uint bound = (uint)max;
            uint threshold = unchecked((uint)(0 - bound)) % bound;
            uint value;
            do
            {
                value = NextUInt();
            }
            while (value < threshold);

            return (int)(value % bound);
        }

        private uint NextUInt()
        {
            ulong oldState = state;
            state = oldState * 6364136223846793005UL + increment;
            uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rotation = (int)(oldState >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }
    }
}
