using System.Collections.Generic;
using UnityEngine;

namespace Player
{
    // GameDesignDoc "Market Upgrades > Mining > Increase mining size": "each upgrade mines 1 more
    // block in one direction. The first upgrade mines a block to the left on each dig, second
    // mines a block to the right, third mines a block down and to the left, fourth mines a block
    // down and to the right, fifth+ upgrade repeats the cycle, adding another block of distance."
    // Offsets are in grid space, where +y is down (matching MapGenerationService's cell grid).
    public static class MiningAreaPattern
    {
        private static readonly Vector2Int[] CycleDirections =
        {
            Vector2Int.left,
            Vector2Int.right,
            new(-1, 1), // down-left
            new(1, 1),  // down-right
        };

        // Cumulative offsets (relative to the primary mined cell) unlocked at the given upgrade level.
        public static IReadOnlyList<Vector2Int> GetOffsets(int level)
        {
            var offsets = new List<Vector2Int>(Mathf.Max(level, 0));
            for (int i = 0; i < level; i++)
            {
                int distance = i / CycleDirections.Length + 1;
                var dir = CycleDirections[i % CycleDirections.Length];
                offsets.Add(dir * distance);
            }
            return offsets;
        }
    }
}
