using System.Collections.Generic;
using UnityEngine;

namespace Player
{
    // GameDesignDoc Control Center "increase mining radius by 1 (max 2)": fixed directional
    // offset pattern used by MiningAutomaton's radius upgrade. The player's own mining-size
    // upgrade (Mining_AreaSize) moved to vein-chain mining - see Player.VeinMiningPattern - this
    // class now only serves the automaton's Control Center upgrade.
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
