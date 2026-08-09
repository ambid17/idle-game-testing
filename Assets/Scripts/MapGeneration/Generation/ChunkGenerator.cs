using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    // Pure C# - no MonoBehaviour/rendering dependency, so it can run headless for offline/idle
    // miner simulation as well as for the live scene.
    public static class ChunkGenerator
    {
        private enum Salt
        {
            OrePick = 1,
            HazardGate = 2,
            HazardPick = 3,
            ArtifactPlacement = 4
        }

        public static ChunkData Generate(int worldSeed, int layerIndex, int gridWidth, LayerConfig config)
        {
            var chunk = new ChunkData
            {
                LayerIndex = layerIndex,
                Width = gridWidth,
                Height = config.LayerHeight,
                Cells = new CellData[gridWidth * config.LayerHeight],
            };

            if (config == null)
            {
                Debug.LogWarning($"No config for layer {layerIndex}, generating empty chunk");
                chunk.IsFullyGenerated = true;
                return chunk;
            }

            for (int y = 0; y < config.LayerHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    chunk.Cells[chunk.Index(x, y)] = RollCell(worldSeed, layerIndex, x, y, config);
                }
            }

            PlaceArtifacts(worldSeed, layerIndex, gridWidth, config, chunk);
            chunk.IsFullyGenerated = true;
            return chunk;
        }

        private static CellData RollCell(int worldSeed, int layerIndex, int x, int y, LayerConfig config)
        {
            var cell = new CellData();

            // grassy dirt for first layer
            if(layerIndex == 0 && y == 0)
            {
                cell.BlockTypeId = (byte) BlockTypeId.GrassyDirt;
                return cell;
            }

            var picked = PickWeighted(config.OreTable, MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.OrePick));

            // hazards are optional, so we only roll for them if the config has a chance and a table
            if (config.HazardChancePerCell > 0f && config.HazardTable.Count > 0)
            {
                float gate = MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.HazardGate);
                if (gate < config.HazardChancePerCell)
                {
                    var hazard = PickWeighted(config.HazardTable, MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.HazardPick));
                    if (hazard != null) picked = hazard;
                }
            }

            cell.BlockTypeId = picked != null ? (byte)picked.Id : (byte)0;
            return cell;
        }

        // 1 artifact is guaranteed per layer; each placement then has a repeating
        // ArtifactBonusChance to place one more, so bonus count follows a geometric distribution
        // (roll again after every success, stop on the first failure).
        private static void PlaceArtifacts(int worldSeed, int layerIndex, int gridWidth, LayerConfig config, ChunkData chunk)
        {
            var rng = MapRng.CreateLayerRandom(worldSeed, layerIndex, (int)Salt.ArtifactPlacement);

            PlaceArtifact(rng, gridWidth, config.LayerHeight, chunk);
            while (rng.NextDouble() < config.ArtifactBonusChance)
            {
                PlaceArtifact(rng, gridWidth, config.LayerHeight, chunk);
            }
        }

        private static void PlaceArtifact(System.Random rng, int gridWidth, int layerHeight, ChunkData chunk)
        {
            int x = rng.Next(0, gridWidth);
            int y = rng.Next(0, layerHeight);
            chunk.Cells[chunk.Index(x, y)].BlockTypeId = (byte)BlockTypeId.Artifact;
        }

        private static BlockType PickWeighted(IReadOnlyList<WeightedBlockEntry> table, float roll01)
        {
            float total = 0f;
            for (int i = 0; i < table.Count; i++) total += table[i].Weight;
            if (total <= 0f)
            {
                Debug.LogWarning("Weighted table has no weight, returning null");
                return null;
            }

            float target = roll01 * total;
            float cumulative = 0f;
            for (int i = 0; i < table.Count; i++)
            {
                cumulative += table[i].Weight;
                if (target <= cumulative) return table[i].BlockType;
            }

            return table[^1].BlockType;
        }
    }
}
