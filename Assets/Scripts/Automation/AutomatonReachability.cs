using System.Collections.Generic;
using MapGeneration;
using UnityEngine;

namespace Automation
{
    // Pure grid-math helper for Mining Automaton wandering, playing the same role for automatons
    // that Player.MiningAreaPattern plays for the player's mining-radius upgrade - except
    // reachability here depends on what's actually been dug, so it's a graph search (BFS through
    // already-mined cells) rather than a fixed offset table.
    //
    // Grid convention (per MiningAreaPattern/ChunkTilemapView): +y is DOWN in chunk-local cell
    // space, opposite of Unity's usual world-space up-positive Y - so "down" here is (0, +1), not
    // Vector2Int.down.
    //
    // Known simplification: stays within a single layer. When wandering finds nothing within
    // radius (e.g. right at a layer's bottom edge), MiningAutomaton falls back to descending
    // straight down per the design doc, which naturally crosses into the next layer via
    // MapGenerationService.WorldToCell's own layer resolution - no BFS involved there.
    public static class AutomatonReachability
    {
        private static readonly Vector2Int GridUp = new(0, -1);
        private static readonly Vector2Int GridDown = new(0, 1);
        private static readonly Vector2Int GridLeft = new(-1, 0);
        private static readonly Vector2Int GridRight = new(1, 0);

        private static readonly Vector2Int[] WalkDirections = { GridUp, GridDown, GridLeft, GridRight };
        private static readonly Vector2Int[] DigDirections = { GridDown, GridLeft, GridRight };

        // Returns unmined, diggable cells (down/left/right of some reachable mined cell) within
        // `radius` walking hops from (originX, originY). The origin cell itself is always treated
        // as walkable regardless of its Mined flag, covering spawn-in on a not-yet-mined tile.
        public static List<Vector2Int> GetAccessibleTiles(MapGenerationService mapGen, int layerIndex, int originX, int originY, int radius)
        {
            var frontier = new HashSet<Vector2Int>();
            if (mapGen == null || radius <= 0) return new List<Vector2Int>();

            // TODO: potential bug, doesn't cross chunk boundaries. Might not matter because they will descend if out of tiles to mine
            var chunk = mapGen.World.GetOrGenerateChunk(layerIndex);
            var origin = new Vector2Int(originX, originY);

            // 0-1 BFS (deque instead of a plain FIFO queue): walking a mined cell costs 1 hop of
            // the wander budget, but crossing a building-support tile costs 0 - it's fixed, always-
            // passable ground, not newly explored territory. A plain FIFO queue can't mix those two
            // edge weights correctly (a cell could get settled via a longer path before a cheaper
            // one - e.g. reaching a support tile by walking under and up costs more than reaching it
            // sideways along the same free row - which would then block the cheaper route from ever
            // improving it), so we track best-known depth per cell and use front/back pushes instead.
            var bestDepth = new Dictionary<Vector2Int, int> { [origin] = 0 };
            var deque = new LinkedList<(Vector2Int cell, int depth)>();
            deque.AddFirst((origin, 0));

            while (deque.Count > 0)
            {
                var (cell, depth) = deque.First.Value;
                deque.RemoveFirst();
                if (depth > bestDepth[cell]) continue; // stale entry, already improved upon

                foreach (var dir in DigDirections)
                {
                    var neighbor = cell + dir;
                    if (!InBounds(chunk, neighbor) || IsMined(chunk, neighbor) || IsBuildingSupported(chunk, neighbor)) continue;
                    frontier.Add(neighbor);
                }

                if (depth >= radius) continue;

                foreach (var dir in WalkDirections)
                {
                    var neighbor = cell + dir;
                    if (!InBounds(chunk, neighbor)) continue;

                    bool supported = IsBuildingSupported(chunk, neighbor);
                    if (!IsMined(chunk, neighbor) && !supported) continue;

                    int neighborDepth = supported ? depth : depth + 1;
                    if (bestDepth.TryGetValue(neighbor, out var known) && known <= neighborDepth) continue;

                    bestDepth[neighbor] = neighborDepth;
                    if (supported) deque.AddFirst((neighbor, neighborDepth));
                    else deque.AddLast((neighbor, neighborDepth));
                }
            }

            return new List<Vector2Int>(frontier);
        }

        // Fallback for when the radius-limited wander above finds nothing. That can happen even
        // with plenty of unmined ground left in the layer - e.g. everything within the normal
        // wander radius is exhausted and the only way onward is walking around a building-support
        // run wider than the radius, or reaching a pocket that's simply farther than `radius` hops
        // away. Bounded by the chunk's own footprint (the longest any walk within one layer could
        // possibly need) rather than an arbitrary large number, so it stays a real, terminating BFS.
        public static List<Vector2Int> GetAccessibleTilesUnbounded(MapGenerationService mapGen, int layerIndex, int originX, int originY)
        {
            if (mapGen == null) return new List<Vector2Int>();
            var chunk = mapGen.World.GetOrGenerateChunk(layerIndex);
            return GetAccessibleTiles(mapGen, layerIndex, originX, originY, chunk.Width + chunk.Height);
        }

        // Builds a walkable cell path (through already-mined ground, ending on `target` even
        // though target itself is unmined - it's the cell about to be dug) from origin to target,
        // in world-space order, for GridPathMover.StepAlongPath to consume.
        public static List<Vector3> BuildWorldPath(MapGenerationService mapGen, int layerIndex, Vector2Int origin, Vector2Int target)
        {
            var path = new List<Vector3>();
            if (mapGen == null) return path;

            var chunk = mapGen.World.GetOrGenerateChunk(layerIndex);
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int> { origin };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(origin);

            bool found = origin == target;
            while (queue.Count > 0 && !found)
            {
                var cell = queue.Dequeue();
                foreach (var dir in WalkDirections)
                {
                    var neighbor = cell + dir;
                    if (!InBounds(chunk, neighbor) || visited.Contains(neighbor)) continue;
                    bool walkable = IsMined(chunk, neighbor) || IsBuildingSupported(chunk, neighbor);
                    if (neighbor != target && !walkable) continue;

                    visited.Add(neighbor);
                    cameFrom[neighbor] = cell;
                    if (neighbor == target) { found = true; break; }
                    queue.Enqueue(neighbor);
                }
            }

            if (!found) return path;

            var cells = new List<Vector2Int> { target };
            var walk = target;
            while (walk != origin)
            {
                walk = cameFrom[walk];
                cells.Add(walk);
            }
            cells.Reverse();

            foreach (var cell in cells)
            {
                path.Add(mapGen.CellToWorldCenter(layerIndex, cell.x, cell.y));
            }

            return path;
        }

        private static bool InBounds(ChunkData chunk, Vector2Int cell) =>
            cell.x >= 0 && cell.x < chunk.Width && cell.y >= 0 && cell.y < chunk.Height;

        private static bool IsMined(ChunkData chunk, Vector2Int cell) =>
            chunk.Cells[chunk.Index(cell.x, cell.y)].Mined;

        private static bool IsBuildingSupported(ChunkData chunk, Vector2Int cell) =>
            chunk.Cells[chunk.Index(cell.x, cell.y)].BlockTypeId == (byte)BlockTypeId.GrassyDirt;
    }
}
