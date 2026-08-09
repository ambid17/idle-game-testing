using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    // Headless-callable facade over the generated chunks - the single mining/reveal codepath
    // shared by the live player and offline/idle miner simulation, per the implementation plan.
    public class MineWorld
    {
        public int Seed { get; private set; }
        public int GridWidth { get; private set; }

        private LayerConfigProvider configProvider => GameManager.LayerConfigProvider;
        private BlockTypeDatabase blockTypes => GameManager.BlockTypeDatabase;
        private readonly Dictionary<int, ChunkData> chunksByLayer = new();

        public MineWorld(int seed, int gridWidth)
        {
            Seed = seed;
            GridWidth = gridWidth;
        }

        public ChunkData GetOrGenerateChunk(int layerIndex)
        {
            if (chunksByLayer.TryGetValue(layerIndex, out var chunk)) return chunk;

            Debug.Log($"Generating chunk for layer {layerIndex}");

            var config = configProvider != null ? configProvider.GetConfig(layerIndex) : null;
            chunk = ChunkGenerator.Generate(Seed, layerIndex, GridWidth, config);
            chunksByLayer[layerIndex] = chunk;
            return chunk;
        }

        public IEnumerable<ChunkData> GetLoadedChunks() => chunksByLayer.Values;

        public bool TryMineCell(int layerIndex, int x, int y, out BlockType minedBlock)
        {
            minedBlock = null;

            var chunk = GetOrGenerateChunk(layerIndex);
            if (x < 0 || x >= chunk.Width || y < 0 || y >= chunk.Height)
            {
                Debug.LogWarning($"TryMineCell: coordinates out of bounds for layer {layerIndex}: ({x}, {y})");
                return false;
            }

            int idx = chunk.Index(x, y);
            var cell = chunk.Cells[idx];
            if (cell.Mined)
            {
                Debug.LogWarning($"TryMineCell: cell already mined for layer {layerIndex}: ({x}, {y})");
                return false;
            }
            cell.Mined = true;
            chunk.Cells[idx] = cell;
            chunk.MinedCount++;

            minedBlock = blockTypes != null ? blockTypes.Get(cell.BlockTypeId) : null;
            return true;
        }

        // Radius reveal seeded from a freshly mined cell (fog "flood-fills" outward from mined ground).
        // Layers stack vertically (row 0 of layer N sits directly below the last row of layer N-1),
        // so a reveal circle near a chunk's top/bottom edge continues into the neighboring layer's
        // chunk instead of clipping there - otherwise fog stays hard-edged right at layer boundaries.
        // Returns cells revealed per layer, keyed by layer index.
        public Dictionary<int, List<Vector2Int>> RevealFog(int layerIndex, int centerX, int centerY, int radius)
        {
            var revealedByLayer = new Dictionary<int, List<Vector2Int>>();
            RevealFogInLayer(layerIndex, centerX, centerY, radius, revealedByLayer);

            var chunk = GetOrGenerateChunk(layerIndex);
            if (centerY - radius < 0 && layerIndex > 0)
            {
                int prevHeight = configProvider != null ? configProvider.GetConfig(layerIndex - 1).LayerHeight : chunk.Height;
                RevealFogAcrossBoundary(layerIndex - 1, -1, centerX, centerY + prevHeight, radius, revealedByLayer);
            }
            if (centerY + radius >= chunk.Height)
            {
                RevealFogAcrossBoundary(layerIndex + 1, 1, centerX, centerY - chunk.Height, radius, revealedByLayer);
            }

            return revealedByLayer;
        }

        // Walks further layers in a single direction only (-1 toward the surface, +1 toward depth) so
        // a wide reveal near a boundary doesn't bounce back into the layer it just came from.
        private void RevealFogAcrossBoundary(int layerIndex, int direction, int centerX, int centerY, int radius, Dictionary<int, List<Vector2Int>> revealedByLayer)
        {
            if (layerIndex < 0) return;

            var chunk = GetOrGenerateChunk(layerIndex);
            RevealFogInLayer(layerIndex, centerX, centerY, radius, revealedByLayer);

            if (direction < 0 && centerY - radius < 0 && layerIndex > 0)
            {
                int prevHeight = configProvider != null ? configProvider.GetConfig(layerIndex - 1).LayerHeight : chunk.Height;
                RevealFogAcrossBoundary(layerIndex - 1, direction, centerX, centerY + prevHeight, radius, revealedByLayer);
            }
            else if (direction > 0 && centerY + radius >= chunk.Height)
            {
                RevealFogAcrossBoundary(layerIndex + 1, direction, centerX, centerY - chunk.Height, radius, revealedByLayer);
            }
        }

        private void RevealFogInLayer(int layerIndex, int centerX, int centerY, int radius, Dictionary<int, List<Vector2Int>> revealedByLayer)
        {
            var chunk = GetOrGenerateChunk(layerIndex);
            List<Vector2Int> revealed = null;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius) continue;

                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (x < 0 || x >= chunk.Width || y < 0 || y >= chunk.Height) continue;

                    int idx = chunk.Index(x, y);
                    var cell = chunk.Cells[idx];
                    if (cell.Revealed) continue;

                    cell.Revealed = true;
                    chunk.Cells[idx] = cell;
                    (revealed ??= new List<Vector2Int>()).Add(new Vector2Int(x, y));
                }
            }

            if (revealed == null) return;

            if (revealedByLayer.TryGetValue(layerIndex, out var existing)) existing.AddRange(revealed);
            else revealedByLayer[layerIndex] = revealed;
        }

        public void UnloadChunk(int layerIndex) => chunksByLayer.Remove(layerIndex);

        // Grid-width prestige upgrade: set independently of ResetForPrestige so it survives resets.
        public void SetGridWidth(int newWidth) => GridWidth = newWidth;

        public void ResetForPrestige(int newSeed)
        {
            Seed = newSeed;
            chunksByLayer.Clear();
        }
    }
}
