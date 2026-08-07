using System;

namespace MapGeneration
{
    // Deterministic hash-based RNG: derives a value purely from (seed, layer, x, y, salt),
    // so any cell can be re-rolled identically without storing or replaying a random stream.
    public static class MapRng
    {
        private static uint Scramble(uint x)
        {
            unchecked
            {
                x ^= 2747636419u;
                x *= 2654435769u;
                x ^= x >> 16;
                x *= 2654435769u;
                x ^= x >> 16;
                x *= 2654435769u;
                return x;
            }
        }

        public static uint HashCell(int seed, int layerIndex, int x, int y, int salt = 0)
        {
            unchecked
            {
                uint h = Scramble((uint)seed);
                h = Scramble(h ^ (uint)layerIndex * 0x9E3779B1u);
                h = Scramble(h ^ (uint)x * 0x85EBCA6Bu);
                h = Scramble(h ^ (uint)y * 0xC2B2AE35u);
                h = Scramble(h ^ (uint)salt * 0x27D4EB2Fu);
                return h;
            }
        }

        public static float Value01(int seed, int layerIndex, int x, int y, int salt = 0)
        {
            return HashCell(seed, layerIndex, x, y, salt) / (float)uint.MaxValue;
        }

        // Deterministic sequential stream for per-layer decisions (e.g. guaranteed-artifact fallback pick).
        public static Random CreateLayerRandom(int seed, int layerIndex, int salt = 0)
        {
            int derived = unchecked((int)HashCell(seed, layerIndex, 0, 0, salt));
            return new Random(derived);
        }
    }
}
