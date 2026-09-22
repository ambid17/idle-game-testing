using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    // Pure C# - no MonoBehaviour/rendering dependency, so it can run headless for offline/idle
    // miner simulation as well as for the live scene.
    public static class ChunkGenerator
    {
        // The first layer is a special case: the buildings sit on tiles and we dont want to remove them.
        private static readonly List<int> firstLayerBlocksToIgnore = new List<int>() { 3, 4, 5, 9, 10, 11, 14, 15, 16, 20, 21, 22, 25, 26, 27 };

        private enum Salt
        {
            OrePick = 1,
            HazardGate = 2,
            HazardPick = 3,
            ArtifactPlacement = 4,
            PowerUpGate = 5,
            PowerUpPick = 6,
            VeinSize = 7,
            VeinSpread = 8,
        }

        private static readonly (int dx, int dy)[] OrthogonalNeighbors = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        // layerHeight is the caller-resolved effective height (authored LayerConfig.LayerHeight
        // minus PrestigeUpgradeManager.LayerSizeReduction) - ChunkGenerator stays pure/headless
        // (see class doc) so it can't read the singleton itself. artifactSpawnRateMultiplier,
        // oreTierOddsBonus, and powerUpSpawnRateBonus are likewise resolved by the caller.
        public static ChunkData Generate(int worldSeed, int layerIndex, int gridWidth, LayerConfig config, int layerHeight, float artifactSpawnRateMultiplier = 1f, float oreTierOddsBonus = 0f, float powerUpSpawnRateBonus = 0f)
        {
            var chunk = new ChunkData
            {
                LayerIndex = layerIndex,
                Width = gridWidth,
                Height = layerHeight,
                Cells = new CellData[gridWidth * layerHeight],
            };

            if (config == null)
            {
                Debug.LogWarning($"No config for layer {layerIndex}, generating empty chunk");
                chunk.IsFullyGenerated = true;
                return chunk;
            }

            for (int y = 0; y < layerHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    chunk.Cells[chunk.Index(x, y)] = RollCell(worldSeed, layerIndex, x, y, config, oreTierOddsBonus, powerUpSpawnRateBonus);
                }
            }

            GrowVeins(worldSeed, layerIndex, gridWidth, layerHeight, config, chunk);
            PlaceArtifacts(worldSeed, layerIndex, gridWidth, layerHeight, artifactSpawnRateMultiplier, config, chunk);
            chunk.IsFullyGenerated = true;
            return chunk;
        }

        private static CellData RollCell(int worldSeed, int layerIndex, int x, int y, LayerConfig config, float oreTierOddsBonus, float powerUpSpawnRateBonus)
        {
            var cell = new CellData();

            // grassy dirt for first layer blocks that aren't mineable
            if(layerIndex == 0 && y == 0 && firstLayerBlocksToIgnore.Contains(x))
            {
                cell.BlockTypeId = (byte) BlockTypeId.GrassyDirt;
                return cell;
            }

            var picked = PickWeighted(config.OreTable, MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.OrePick), oreTierOddsBonus);
            bool hazardAssigned = false;

            // hazards are optional, so we only roll for them if the config has a chance and a table
            if (config.HazardChancePerCell > 0f && config.HazardTable.Count > 0)
            {
                float gate = MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.HazardGate);
                if (gate < config.HazardChancePerCell)
                {
                    var hazard = PickWeighted(config.HazardTable, MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.HazardPick));
                    if (hazard != null) { picked = hazard; hazardAssigned = true; }
                }
            }

            // GameDesignDoc "Randomness blocks > positive": only rolled on cells that didn't already
            // get a hazard, mirroring the hazard roll's shape with its own gate/table/salt.
            // PowerUpSpawnRateBonus scales the gate chance rather than table weights.
            if (!hazardAssigned && config.PowerUpChancePerCell > 0f && config.PowerUpTable.Count > 0)
            {
                float gate = MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.PowerUpGate);
                float effectiveChance = Mathf.Clamp01(config.PowerUpChancePerCell * (1f + powerUpSpawnRateBonus));
                if (gate < effectiveChance)
                {
                    var powerUp = PickWeighted(config.PowerUpTable, MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.PowerUpPick));
                    if (powerUp != null) picked = powerUp;
                }
            }

            cell.BlockTypeId = picked != null ? (byte)picked.Id : (byte)0;
            return cell;
        }

        // Second pass over the already-rolled grid: any cell whose independently-rolled block is
        // Category.Ore becomes a vein seed and eats into its still-Dirt neighbors (every ore veins
        // by default; author VeinSizeMin/Max = 1 on a specific entry to opt it out). Runs after the
        // per-cell roll (so hazard/power-up cells are never touched) and before artifact placement
        // (so a guaranteed artifact can still land on/overwrite a vein cell, matching how it already
        // overwrites plain ore). Iterates in a fixed row-major order so results stay deterministic
        // for a given worldSeed regardless of how/when this is called.
        private static void GrowVeins(int worldSeed, int layerIndex, int gridWidth, int layerHeight, LayerConfig config, ChunkData chunk)
        {
            for (int y = 0; y < layerHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    var entry = FindOreEntry(config.OreTable, chunk.Cells[chunk.Index(x, y)].BlockTypeId);
                    if (entry == null || entry.BlockType.Category != BlockCategory.Ore) continue;

                    GrowVein(worldSeed, layerIndex, gridWidth, layerHeight, x, y, entry, chunk);
                }
            }
        }

        private static WeightedBlockEntry FindOreEntry(IReadOnlyList<WeightedBlockEntry> table, byte blockTypeId)
        {
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i].BlockType != null && (byte)table[i].BlockType.Id == blockTypeId) return table[i];
            }
            return null;
        }

        // Random-walk flood fill from the seed cell: repeatedly pulls a random still-Dirt neighbor
        // of the growing vein and converts it, until it hits the target size or runs out of Dirt to
        // spread into (e.g. boxed in by hazards/other ores/the grid edge).
        private static void GrowVein(int worldSeed, int layerIndex, int gridWidth, int layerHeight, int seedX, int seedY, WeightedBlockEntry entry, ChunkData chunk)
        {
            int sizeRange = Mathf.Max(0, entry.VeinSizeMax - entry.VeinSizeMin);
            float sizeRoll = MapRng.Value01(worldSeed, layerIndex, seedX, seedY, (int)Salt.VeinSize);
            int targetSize = entry.VeinSizeMin + Mathf.Min(sizeRange, Mathf.FloorToInt(sizeRoll * (sizeRange + 1)));
            if (targetSize <= 1) return;

            byte dirtId = (byte)BlockTypeId.Dirt;
            byte targetId = (byte)entry.BlockType.Id;

            var frontier = new List<(int x, int y)>();
            AddDirtNeighbors(gridWidth, layerHeight, seedX, seedY, chunk, dirtId, frontier);

            int currentSize = 1;
            int attempt = 0;
            while (currentSize < targetSize && frontier.Count > 0)
            {
                float pickRoll = MapRng.Value01(worldSeed, layerIndex, seedX, seedY, (int)Salt.VeinSpread + attempt);
                int pickIndex = Mathf.Min(frontier.Count - 1, Mathf.FloorToInt(pickRoll * frontier.Count));
                var (fx, fy) = frontier[pickIndex];
                frontier.RemoveAt(pickIndex);
                attempt++;

                int cellIndex = chunk.Index(fx, fy);
                if (chunk.Cells[cellIndex].BlockTypeId != dirtId) continue; // claimed by an overlapping vein already

                float spreadRoll = MapRng.Value01(worldSeed, layerIndex, fx, fy, (int)Salt.VeinSpread);
                if (spreadRoll > entry.VeinSpreadChance) continue;

                chunk.Cells[cellIndex].BlockTypeId = targetId;
                currentSize++;
                AddDirtNeighbors(gridWidth, layerHeight, fx, fy, chunk, dirtId, frontier);
            }
        }

        private static void AddDirtNeighbors(int gridWidth, int layerHeight, int x, int y, ChunkData chunk, byte dirtId, List<(int x, int y)> frontier)
        {
            foreach (var (dx, dy) in OrthogonalNeighbors)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= gridWidth || ny < 0 || ny >= layerHeight) continue;
                if (chunk.Cells[chunk.Index(nx, ny)].BlockTypeId == dirtId) frontier.Add((nx, ny));
            }
        }

        // 1 artifact is guaranteed per layer; each placement then has a repeating
        // ArtifactBonusChance to place one more, so bonus count follows a geometric distribution
        // (roll again after every success, stop on the first failure). GameDesignDoc
        // "Prestige > Prestige > increase artifact spawn rate" scales the bonus-chance roll.
        private static void PlaceArtifacts(int worldSeed, int layerIndex, int gridWidth, int layerHeight, float spawnRateMultiplier, LayerConfig config, ChunkData chunk)
        {
            var rng = MapRng.CreateLayerRandom(worldSeed, layerIndex, (int)Salt.ArtifactPlacement);

            PlaceArtifact(rng, gridWidth, layerHeight, chunk);
            float bonusChance = Mathf.Clamp01(config.ArtifactBonusChance * spawnRateMultiplier);
            while (rng.NextDouble() < bonusChance)
            {
                PlaceArtifact(rng, gridWidth, layerHeight, chunk);
            }
        }

        private static void PlaceArtifact(System.Random rng, int gridWidth, int layerHeight, ChunkData chunk)
        {
            int x = rng.Next(0, gridWidth);
            int y = rng.Next(0, layerHeight);
            chunk.Cells[chunk.Index(x, y)].BlockTypeId = (byte)BlockTypeId.Artifact;
        }

        // GameDesignDoc "Prestige > Progression > increase spawn odds of next tier of blocks":
        // tierOddsBonus (default 0 = no bias, existing HazardTable rolls are unaffected) skews the
        // roll toward entries with a higher BlockType.Value within this specific table, without a
        // full rarity-tier rework.
        private static BlockType PickWeighted(IReadOnlyList<WeightedBlockEntry> table, float roll01, float tierOddsBonus = 0f)
        {
            if (table.Count == 0)
            {
                Debug.LogWarning("Weighted table has no entries, returning null");
                return null;
            }

            float maxValue = 0f;
            if (tierOddsBonus > 0f)
            {
                for (int i = 0; i < table.Count; i++)
                {
                    if (table[i].BlockType != null) maxValue = Mathf.Max(maxValue, table[i].BlockType.Value);
                }
            }

            float total = 0f;
            for (int i = 0; i < table.Count; i++) total += EffectiveWeight(table[i], maxValue, tierOddsBonus);
            if (total <= 0f)
            {
                Debug.LogWarning("Weighted table has no weight, returning null");
                return null;
            }

            float target = roll01 * total;
            float cumulative = 0f;
            for (int i = 0; i < table.Count; i++)
            {
                cumulative += EffectiveWeight(table[i], maxValue, tierOddsBonus);
                if (target <= cumulative) return table[i].BlockType;
            }

            return table[^1].BlockType;
        }

        private static float EffectiveWeight(WeightedBlockEntry entry, float maxValue, float tierOddsBonus)
        {
            if (tierOddsBonus <= 0f || maxValue <= 0f || entry.BlockType == null) return entry.Weight;
            float normalizedValueRank = entry.BlockType.Value / maxValue;
            return entry.Weight * (1f + tierOddsBonus * normalizedValueRank);
        }
    }
}
