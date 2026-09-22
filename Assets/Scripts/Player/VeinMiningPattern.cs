using System.Collections.Generic;
using MapGeneration;
using UnityEngine;

namespace Player
{
    // GameDesignDoc "Market Upgrades > Mining > Increase mining size" (vein mining): when the
    // primary mined block is Ore, each level of the upgrade lets the free chain reach one more
    // Ore block connected (directly or through other Ore) to the block that was just mined,
    // instead of the old fixed directional offset pattern.
    // Grid convention (per AutomatonReachability/MiningAreaPattern): +y is DOWN in cell-local
    // space, opposite of Unity's usual world-space up-positive Y.
    public static class VeinMiningPattern
    {
        private static readonly Vector2Int[] AdjacentDirections =
        {
            new(0, -1), // up
            new(0, 1),  // down
            new(-1, 0), // left
            new(1, 0),  // right
        };

        // BFS outward from (originX, originY) through Ore-category cells only, nearest-first,
        // capped at maxCount results. The origin cell itself is never included in the result -
        // callers already mine it separately as the primary target.
        public static List<Vector2Int> GetChainCells(MapGenerationService mapGen, int layerIndex, int originX, int originY, int maxCount)
        {
            var result = new List<Vector2Int>();
            if (mapGen == null || maxCount <= 0) return result;

            var origin = new Vector2Int(originX, originY);
            var visited = new HashSet<Vector2Int> { origin };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(origin);

            while (queue.Count > 0 && result.Count < maxCount)
            {
                var current = queue.Dequeue();
                foreach (var dir in AdjacentDirections)
                {
                    var next = current + dir;
                    if (visited.Contains(next)) continue;
                    visited.Add(next);

                    var blockType = mapGen.GetBlockTypeAt(layerIndex, next.x, next.y);
                    if (blockType == null || blockType.Category != BlockCategory.Ore) continue;

                    result.Add(next);
                    queue.Enqueue(next);
                    if (result.Count >= maxCount) break;
                }
            }

            return result;
        }
    }
}
