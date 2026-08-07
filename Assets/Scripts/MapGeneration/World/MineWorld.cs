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

        private readonly LayerConfigProvider configProvider;
        private readonly BlockTypeDatabase blockTypes;
        private readonly Dictionary<int, ChunkData> chunksByLayer = new();

        public MineWorld(int seed, int gridWidth, LayerConfigProvider configProvider, BlockTypeDatabase blockTypes)
        {
            Seed = seed;
            GridWidth = gridWidth;
            this.configProvider = configProvider;
            this.blockTypes = blockTypes;
        }

        public ChunkData GetOrGenerateChunk(int layerIndex)
        {
            if (chunksByLayer.TryGetValue(layerIndex, out var chunk)) return chunk;

            var config = configProvider != null ? configProvider.GetConfig(layerIndex) : null;
            chunk = ChunkGenerator.Generate(Seed, layerIndex, GridWidth, config);
            chunksByLayer[layerIndex] = chunk;
            return chunk;
        }

        public IEnumerable<ChunkData> GetLoadedChunks() => chunksByLayer.Values;

        public bool TryMineCell(int layerIndex, int x, int y, out BlockType minedBlock, out bool artifactFound)
        {
            minedBlock = null;
            artifactFound = false;

            var chunk = GetOrGenerateChunk(layerIndex);
            if (x < 0 || x >= chunk.Width || y < 0 || y >= chunk.Height) return false;

            int idx = chunk.Index(x, y);
            var cell = chunk.Cells[idx];
            if (cell.Mined) return false;

            cell.Mined = true;
            chunk.Cells[idx] = cell;
            chunk.MinedCount++;

            minedBlock = blockTypes != null ? blockTypes.Get(cell.BlockTypeId) : null;
            artifactFound = cell.IsArtifact;
            return true;
        }

        // Radius reveal seeded from a freshly mined cell (fog "flood-fills" outward from mined ground).
        public List<Vector2Int> RevealFog(int layerIndex, int centerX, int centerY, int radius)
        {
            var chunk = GetOrGenerateChunk(layerIndex);
            var revealed = new List<Vector2Int>();

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
                    revealed.Add(new Vector2Int(x, y));
                }
            }

            return revealed;
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
