namespace SabaProps.Foliage
{
    /// <summary>
    /// Deterministic xorshift32 generator.
    /// <para>
    /// <see cref="UnityEngine.Random"/> shares global state and its sequence is
    /// not guaranteed across Unity versions, which would make foliage layouts
    /// drift between machines. This struct keeps a scatter reproducible from its
    /// seed alone, so a scene rebuilt on another PC produces identical results.
    /// </para>
    /// </summary>
    public struct FoliageRandom
    {
        private uint _state;

        public FoliageRandom(int seed)
        {
            unchecked
            {
                // Mix the seed so that nearby seeds (0, 1, 2 ...) diverge
                // immediately instead of producing correlated first values.
                uint s = (uint)seed * 2654435761u + 0x9E3779B9u;
                s ^= s >> 15;
                s *= 0x85EBCA6Bu;
                s ^= s >> 13;
                _state = s == 0u ? 0x9E3779B9u : s;
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }
        }

        /// <summary>Uniform value in [0, 1).</summary>
        public float Value01()
        {
            return (NextUInt() & 0xFFFFFFu) / 16777216f;
        }

        /// <summary>Uniform value in [-1, 1).</summary>
        public float Signed()
        {
            return Value01() * 2f - 1f;
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * Value01();
        }

        /// <summary>Uniform integer in [min, max).</summary>
        public int RangeInt(int min, int max)
        {
            if (max <= min)
            {
                return min;
            }

            return min + (int)(NextUInt() % (uint)(max - min));
        }

        public bool Chance(float probability)
        {
            return Value01() < probability;
        }
    }
}
