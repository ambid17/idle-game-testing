using System.Collections.Generic;
using MapGeneration;
using UnityEngine;

namespace Automation
{
    // Pure grid-math helper for Mining Automaton wandering: reachability depends on what's
    // actually been dug, so it's a graph search (BFS through already-mined cells).
    //
    // Grid convention (per VeinMiningPattern/ChunkTilemapView): +y is DOWN in chunk-local cell
    // space, opposite of Unity's usual world-space up-positive Y - so "down" here is (0, +1), not
    // Vector2Int.down.
    //
    // Known simplification: the radius-limited wander (GetAccessibleTiles) stays within a single
    // layer's chunk - fine in practice since MiningAutomaton only leans on it while there's still
    // nearby ground to dig. GetAccessibleTilesUnbounded and BuildWorldPath, used once a layer runs
    // dry, cross layer boundaries via TryStep: MineWorld stacks layers vertically (row 0 of layer
    // N+1 sits directly below layer N's last row), so walking off one chunk's edge continues into
    // the neighboring chunk's matching edge instead of stopping dead.
    //
    // GetAccessibleTiles tags each candidate with its hop-distance from the origin so
    // MiningAutomaton can weight target selection toward nearby tiles and toward continuing its
    // current digging direction (a "vein"), rather than picking uniformly at random - see
    // MiningAutomaton.PickWeightedTarget.
    //
    // Automatons only ever occupy open cells (IsWalkable): mined ground plus a short sky lane
    // above the surface. Every path - to a dig target or home to the Depot - is built through
    // those, never through solid ground.
    public static class AutomatonReachability
    {
        private static readonly Vector2Int GridUp = new(0, -1);
        private static readonly Vector2Int GridDown = new(0, 1);
        private static readonly Vector2Int GridLeft = new(-1, 0);
        private static readonly Vector2Int GridRight = new(1, 0);

        private static readonly Vector2Int[] WalkDirections = { GridUp, GridDown, GridLeft, GridRight };
        private static readonly Vector2Int[] DigDirections = { GridDown, GridLeft, GridRight };

        // How many rows of open sky above the surface count as walkable - enough for the lane
        // automatons travel along between shafts and the Depot, while keeping searches finite.
        private const int SkyLaneRows = 3;

        // Returns unmined, diggable cells (down/left/right of some reachable mined cell) within
        // `radius` walking hops from (originX, originY), each tagged with its own hop-distance
        // from the origin so callers can weight closer tiles higher (e.g. MiningAutomaton's
        // direction-persistent target selection) rather than picking uniformly at random. The
        // origin cell itself is always treated as walkable regardless of its Mined flag, covering
        // spawn-in on a not-yet-mined tile.
        public static List<(Vector2Int Cell, int Depth)> GetAccessibleTiles(MapGenerationService mapGen, int layerIndex, int originX, int originY, int radius)
        {
            var frontierDepth = new Dictionary<Vector2Int, int>();
            if (mapGen == null || radius <= 0) return new List<(Vector2Int, int)>();

            // TODO: potential bug, doesn't cross chunk boundaries. Might not matter because they will descend if out of tiles to mine
            var chunk = mapGen.World.GetOrGenerateChunk(layerIndex);
            var origin = new Vector2Int(originX, originY);

            var depthOf = new Dictionary<Vector2Int, int> { [origin] = 0 };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                int depth = depthOf[cell];

                foreach (var dir in DigDirections)
                {
                    var neighbor = cell + dir;
                    if (!InBounds(chunk, neighbor) || IsMined(chunk, neighbor) || IsBuildingSupported(chunk, neighbor) || IsUnmineableByAutomaton(chunk, neighbor)) continue;

                    int discoveredDepth = depth + 1;
                    if (frontierDepth.TryGetValue(neighbor, out var knownDepth) && knownDepth <= discoveredDepth) continue;
                    frontierDepth[neighbor] = discoveredDepth;
                }

                if (depth >= radius) continue;

                foreach (var dir in WalkDirections)
                {
                    var neighbor = cell + dir;
                    if (depthOf.ContainsKey(neighbor) || !IsWalkable(chunk, layerIndex, neighbor)) continue;

                    depthOf[neighbor] = depth + 1;
                    queue.Enqueue(neighbor);
                }
            }

            var result = new List<(Vector2Int, int)>(frontierDepth.Count);
            foreach (var kvp in frontierDepth) result.Add((kvp.Key, kvp.Value));
            return result;
        }

        // Fallback for when the radius-limited wander above finds nothing. That can happen even
        // with plenty of unmined ground left in the mine - e.g. everything within the normal
        // wander radius is exhausted and the only way onward is along the sky lane past a
        // building's unmineable footing, reaching a pocket that's simply farther than `radius` hops
        // away, or the whole current layer being fully mined out so the only ground left is past
        // its floor. Unlike the bounded wander, this crosses layer boundaries (via TryStep) rather
        // than stopping at the origin layer's chunk edge, so results are tagged with the layer they
        // were found in - callers (BuildWorldPath, MineCell) can no longer assume the origin layer.
        // Termination relies on freshly generated chunks always having unmined ground near their
        // entry row rather than an artificial cap - a real, but effectively unbounded, BFS.
        public static List<(int Layer, Vector2Int Cell)> GetAccessibleTilesUnbounded(MapGenerationService mapGen, int originLayer, int originX, int originY)
        {
            var frontier = new List<(int Layer, Vector2Int Cell)>();
            var frontierSeen = new HashSet<(int, Vector2Int)>();
            if (mapGen == null) return frontier;

            var origin = (originLayer, new Vector2Int(originX, originY));
            var visited = new HashSet<(int, Vector2Int)> { origin };
            var queue = new Queue<(int Layer, Vector2Int Cell)>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                var (layer, cell) = queue.Dequeue();

                foreach (var dir in DigDirections)
                {
                    if (!TryStep(mapGen, layer, cell, dir, out int dLayer, out Vector2Int dCell)) continue;
                    var dChunk = mapGen.World.GetOrGenerateChunk(dLayer);
                    if (!InBounds(dChunk, dCell) || IsMined(dChunk, dCell) || IsBuildingSupported(dChunk, dCell) || IsUnmineableByAutomaton(dChunk, dCell)) continue;

                    var key = (dLayer, dCell);
                    if (frontierSeen.Add(key)) frontier.Add(key);
                }

                foreach (var dir in WalkDirections)
                {
                    if (!TryStep(mapGen, layer, cell, dir, out int wLayer, out Vector2Int wCell)) continue;
                    var key = (wLayer, wCell);
                    if (visited.Contains(key)) continue;

                    if (!IsWalkable(mapGen.World.GetOrGenerateChunk(wLayer), wLayer, wCell)) continue;

                    visited.Add(key);
                    queue.Enqueue(key);
                }
            }

            return frontier;
        }

        // Builds a walkable cell path (through already-mined ground, ending on `target` even
        // though target itself is unmined - it's the cell about to be dug) from origin to target,
        // in world-space order, for GridPathMover.StepAlongPath to consume. Origin and target may
        // sit in different layers (e.g. target came from GetAccessibleTilesUnbounded crossing into
        // a deeper chunk) - the walk crosses that boundary via TryStep just like the search that
        // found the target did, so the two stay consistent.
        public static List<Vector3> BuildWorldPath(MapGenerationService mapGen, int originLayer, Vector2Int origin, int targetLayer, Vector2Int target)
        {
            var path = new List<Vector3>();
            if (mapGen == null) return path;

            var originNode = (Layer: originLayer, Cell: origin);
            var targetNode = (Layer: targetLayer, Cell: target);

            var cameFrom = new Dictionary<(int Layer, Vector2Int Cell), (int Layer, Vector2Int Cell)>();
            var visited = new HashSet<(int Layer, Vector2Int Cell)> { originNode };
            var queue = new Queue<(int Layer, Vector2Int Cell)>();
            queue.Enqueue(originNode);

            bool found = originNode.Equals(targetNode);
            while (queue.Count > 0 && !found)
            {
                var current = queue.Dequeue();
                foreach (var dir in WalkDirections)
                {
                    if (!TryStep(mapGen, current.Layer, current.Cell, dir, out int nLayer, out Vector2Int nCell)) continue;
                    var neighbor = (Layer: nLayer, Cell: nCell);
                    if (visited.Contains(neighbor)) continue;

                    bool isTarget = neighbor.Equals(targetNode);
                    if (!isTarget && !IsWalkable(mapGen.World.GetOrGenerateChunk(nLayer), nLayer, nCell)) continue;

                    visited.Add(neighbor);
                    cameFrom[neighbor] = current;
                    if (isTarget) { found = true; break; }
                    queue.Enqueue(neighbor);
                }
            }

            if (!found) return path;

            var nodes = new List<(int Layer, Vector2Int Cell)> { targetNode };
            var walk = targetNode;
            while (!walk.Equals(originNode))
            {
                walk = cameFrom[walk];
                nodes.Add(walk);
            }
            nodes.Reverse();

            foreach (var node in nodes)
            {
                path.Add(mapGen.CellToWorldCenter(node.Layer, node.Cell.x, node.Cell.y));
            }

            return path;
        }

        // Resolves the (layer, cell) landed on by moving `dir` from (layer, cell). A vertical step
        // that would leave the chunk crosses into the neighboring layer's matching edge instead of
        // stopping (per MineWorld: layer N+1's row 0 sits directly below layer N's last row).
        // Horizontal steps never cross layers - each chunk has its own width. Returns false only
        // when the step would go out of bounds with nowhere to cross into (off the grid
        // horizontally, or above the surface / below layer 0's chunk).
        private static bool TryStep(MapGenerationService mapGen, int layer, Vector2Int cell, Vector2Int dir, out int newLayer, out Vector2Int newCell)
        {
            var next = cell + dir;
            var chunk = mapGen.World.GetOrGenerateChunk(layer);

            if (next.x < 0 || next.x >= chunk.Width)
            {
                newLayer = layer;
                newCell = next;
                return false;
            }

            if (next.y < 0)
            {
                newLayer = layer;
                newCell = next;
                // Above layer 0 is the sky - a valid step, IsWalkable decides how high it may go.
                if (layer <= 0) return true;

                var above = mapGen.World.GetOrGenerateChunk(layer - 1);
                newLayer = layer - 1;
                newCell = new Vector2Int(next.x, above.Height - 1);
                return true;
            }

            if (next.y >= chunk.Height)
            {
                newLayer = layer + 1;
                newCell = new Vector2Int(next.x, 0);
                return true;
            }

            newLayer = layer;
            newCell = next;
            return true;
        }

        private static bool InBounds(ChunkData chunk, Vector2Int cell) =>
            cell.x >= 0 && cell.x < chunk.Width && cell.y >= 0 && cell.y < chunk.Height;

        // What an automaton may occupy: dug-out ground, or the open sky lane just above the surface
        // (layer 0, negative rows) that links the mine's shafts to each other and to the Depot.
        // Solid cells - including the unmineable GrassyDirt under buildings - never are.
        public static bool IsWalkable(MapGenerationService mapGen, int layer, Vector2Int cell) =>
            IsWalkable(mapGen.World.GetOrGenerateChunk(layer), layer, cell);

        private static bool IsWalkable(ChunkData chunk, int layer, Vector2Int cell)
        {
            if (layer == 0 && cell.y < 0)
                return cell.y >= -SkyLaneRows && cell.x >= 0 && cell.x < chunk.Width;
            return InBounds(chunk, cell) && IsMined(chunk, cell);
        }

        private static bool IsMined(ChunkData chunk, Vector2Int cell) =>
            chunk.Cells[chunk.Index(cell.x, cell.y)].Mined;

        private static bool IsBuildingSupported(ChunkData chunk, Vector2Int cell) =>
            chunk.Cells[chunk.Index(cell.x, cell.y)].BlockTypeId == (byte)BlockTypeId.GrassyDirt;

        // FallingRock is never directly mineable (MineWorld.TryMineCell refuses it - it only comes
        // loose once its support is mined out), and PowerUp blocks are player-only (TryMineCell
        // refuses them for anyone else), so neither must ever be offered as a dig target: an
        // automaton that picked one would walk up, accrue mining progress and "clear" its own
        // state as if it dug through, but MineCell would silently no-op and the block would stay
        // put - free to get re-picked and repeat the cycle indefinitely.
        private static bool IsUnmineableByAutomaton(ChunkData chunk, Vector2Int cell)
        {
            byte blockTypeId = chunk.Cells[chunk.Index(cell.x, cell.y)].BlockTypeId;
            if (blockTypeId == (byte)BlockTypeId.FallingRock) return true;

            var blockType = GameManager.BlockTypeDatabase.Get(blockTypeId);
            return blockType != null && (blockType.Category == BlockCategory.PowerUp || blockType.DrillProof);
        }
    }
}
