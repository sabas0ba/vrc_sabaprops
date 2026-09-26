namespace SabaProps.Flock
{
    /// <summary>
    /// Stateless integer hash for per-individual values. The same seed and
    /// index always give the same value, independent of call order, so a
    /// rebuilt swarm is identical to the previous build.
    /// </summary>
    public static class FlockRandom
    {
        /// <summary>PCG-style avalanche hash.</summary>
        public static uint Hash(uint value)
        {
            uint state = value * 747796405u + 2891336453u;
            uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
            return (word >> 22) ^ word;
        }

        /// <summary>Uniform value in [0, 1) for a seed, an index and a stream.</summary>
        public static float Value(int seed, int index, int stream)
        {
            uint h = Hash((uint)seed ^ Hash((uint)index * 0x9E3779B9u ^ Hash((uint)stream + 0x85EBCA6Bu)));
            // 24 bits keep the value exactly representable as a float below 1.
            return (h >> 8) * (1f / 16777216f);
        }
    }
}
