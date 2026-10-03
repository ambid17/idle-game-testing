using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    // Pure C# - no MonoBehaviour/rendering dependency, so it can run headless for offline/idle
    // miner simulation as well as for the live scene. Every edit after the per-cell roll goes
    // through MapEditContext (the shared map-editing toolkit also used by MapFeatureDefinitions).
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
            EmptyPocketGate = 9,
            EmptyPocketSize = 10,
            EmptyPocketSpread = 11,
            OreTierGate = 12,
            OreTierPick = 13,
            ShopLayer = 14,
            ShopCavePosition = 15,
            LayerFeaturePick = 16,
        }

        // Critter Shop cave (see CarveShopCave): lands on one layer in [ShopCaveMinLayer,
        // ShopCaveMaxLayer] per seed - below the surface buildings on layer 0, above layer 4.
        public const int ShopCaveMinLayer = 1;
        public const int ShopCaveMaxLayer = 3;
        private const int ShopCaveWidth = 7;
        private const int ShopCaveHeight = 4;
        // Keeps the cave off the grid's side walls and the layer's top/bottom seams.
        private const int ShopCaveMargin = 2;

        // Every layer's ore roll includes Dirt as filler at this weight, alongside the authored
        // LayerConfig.OreTable weights - Dirt is never authored in the table itself.
        public const float DirtFillerWeight = 100f;

        // layerHeight is the caller-resolved effective height (authored LayerConfig.LayerHeight
        // minus PrestigeUpgradeManager.Mining_LayerSizeReduction) - ChunkGenerator stays pure/headless
        // (see class doc) so it can't read the singleton itself. artifactSpawnRateMultiplier,
        // oreTierOddsBonus, and powerUpSpawnRateBonus are likewise resolved by the caller, as is
        // nextLayerConfig (the source table for oreTierOddsBonus's deeper-layer ore swaps) and
        // tweaks (the run modifier's adjustments for this layer - null/None = none).
        public static ChunkData Generate(int worldSeed, int layerIndex, int gridWidth, LayerConfig config, int layerHeight, float artifactSpawnRateMultiplier = 1f, float oreTierOddsBonus = 0f, float powerUpSpawnRateBonus = 0f, LayerConfig nextLayerConfig = null, LayerGenerationTweaks tweaks = null)
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

            tweaks ??= LayerGenerationTweaks.None;
            var roll = new CellRollSettings(config, nextLayerConfig, tweaks, oreTierOddsBonus + tweaks.OreTierOddsBonus, powerUpSpawnRateBonus + tweaks.PowerUpSpawnRateBonus);

            for (int y = 0; y < layerHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    chunk.Cells[chunk.Index(x, y)] = RollCell(worldSeed, layerIndex, x, y, roll);
                }
            }

            var ctx = new MapEditContext(worldSeed, layerIndex, chunk, config, nextLayerConfig);
            if (!tweaks.DisableVeins) GrowVeins(ctx, tweaks);
            if (layerIndex == GetShopLayerIndex(worldSeed)) CarveShopCave(ctx);
            var layerFeature = PickLayerFeature(ctx);
            RunFeatures(ctx, MapFeaturePhase.Structures, layerFeature, tweaks);
            PlaceArtifacts(ctx, artifactSpawnRateMultiplier, tweaks);
            CarveEmptyPockets(ctx, tweaks);
            RunFeatures(ctx, MapFeaturePhase.Overlay, layerFeature, tweaks);
            chunk.IsFullyGenerated = true;
            return chunk;
        }

        // Exactly one of LayerConfig.Features runs per layer: the first eligible Guaranteed feature
        // (story rooms), otherwise a weighted pick among the eligible ones. Null when none are
        // eligible (e.g. layer 0, which hosts the surface buildings).
        private static MapFeatureDefinition PickLayerFeature(MapEditContext ctx)
        {
            float totalWeight = 0f;
            foreach (var feature in ctx.Config.Features)
            {
                if (feature == null || !feature.Placement.AllowsLayer(ctx.LayerIndex)) continue;
                if (feature.Placement.Guaranteed) return feature;
                totalWeight += feature.Placement.Weight;
            }
            if (totalWeight <= 0f) return null;

            float roll = ctx.Value01(0, 0, (int)Salt.LayerFeaturePick) * totalWeight;
            MapFeatureDefinition last = null;
            foreach (var feature in ctx.Config.Features)
            {
                if (feature == null || !feature.Placement.AllowsLayer(ctx.LayerIndex) || feature.Placement.Weight <= 0f) continue;
                last = feature;
                roll -= feature.Placement.Weight;
                if (roll < 0f) return feature;
            }
            return last;
        }

        // The layer's picked feature first, then every run-modifier feature.
        private static void RunFeatures(MapEditContext ctx, MapFeaturePhase phase, MapFeatureDefinition layerFeature, LayerGenerationTweaks tweaks)
        {
            if (layerFeature != null && layerFeature.Phase == phase) layerFeature.Run(ctx);
            foreach (var feature in tweaks.Features)
            {
                if (feature != null && feature.Phase == phase) feature.Run(ctx);
            }
        }

        // Everything RollCell needs, resolved once per chunk rather than per cell.
        private readonly struct CellRollSettings
        {
            public readonly LayerConfig Config;
            public readonly LayerConfig NextConfig;
            public readonly List<WeightedBlockEntry> OreTable;
            public readonly Func<WeightedBlockEntry, float> OreWeightOf;
            public readonly Func<WeightedBlockEntry, float> HazardWeightOf;
            public readonly float HazardChance;
            public readonly float OreTierOddsBonus;
            public readonly float PowerUpSpawnRateBonus;

            public CellRollSettings(LayerConfig config, LayerConfig nextConfig, LayerGenerationTweaks tweaks, float oreTierOddsBonus, float powerUpSpawnRateBonus)
            {
                Config = config;
                NextConfig = nextConfig;
                OreTable = tweaks.UseNextLayerOreTable && nextConfig != null ? nextConfig.OreTable : config.OreTable;
                OreTierOddsBonus = oreTierOddsBonus;
                PowerUpSpawnRateBonus = powerUpSpawnRateBonus;
                HazardChance = config.HazardChancePerCell * tweaks.HazardChanceMultiplier;

                // Null weight funcs keep WeightedTables on the exact un-modified path.
                OreWeightOf = null;
                if (tweaks.AllOreWeightMultiplier != 1f || tweaks.OreWeightMultipliers != null)
                {
                    float all = tweaks.AllOreWeightMultiplier;
                    var perOre = tweaks.OreWeightMultipliers;
                    OreWeightOf = entry =>
                    {
                        if (!WeightedTables.IsOreEntry(entry)) return entry.Weight;
                        float weight = entry.Weight * all;
                        if (perOre != null && perOre.TryGetValue(entry.BlockType.Id, out float multiplier)) weight *= multiplier;
                        return weight;
                    };
                }

                HazardWeightOf = null;
                if (tweaks.HazardWeightMultipliers != null)
                {
                    var perHazard = tweaks.HazardWeightMultipliers;
                    HazardWeightOf = entry =>
                        entry.BlockType != null && perHazard.TryGetValue(entry.BlockType.CustomBehavior, out float multiplier)
                            ? entry.Weight * multiplier
                            : entry.Weight;
                }
            }
        }

        private static CellData RollCell(int worldSeed, int layerIndex, int x, int y, in CellRollSettings roll)
        {
            var cell = new CellData();
            var config = roll.Config;

            // grassy dirt for first layer blocks that aren't mineable
            if(layerIndex == 0 && y == 0 && firstLayerBlocksToIgnore.Contains(x))
            {
                cell.BlockTypeId = (byte) BlockTypeId.GrassyDirt;
                return cell;
            }

            // Null means the roll landed on the implicit Dirt filler (see DirtFillerWeight).
            var picked = WeightedTables.PickWeighted(roll.OreTable, MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.OrePick), DirtFillerWeight, roll.OreWeightOf);

            // GameDesignDoc "Prestige > Progression > increase spawn odds of next tier of blocks":
            // each rolled ore has an oreTierOddsBonus chance to be swapped for an ore from the next
            // layer's table instead (dirt/hazard/power-up rolls are never swapped).
            if (picked != null && picked.Category == BlockCategory.Ore && roll.NextConfig != null && roll.OreTierOddsBonus > 0f
                && MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.OreTierGate) < roll.OreTierOddsBonus)
            {
                var deeperOre = WeightedTables.PickWeightedOre(roll.NextConfig.OreTable, MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.OreTierPick), roll.OreWeightOf);
                if (deeperOre != null) picked = deeperOre;
            }
            bool hazardAssigned = false;

            // hazards are optional, so we only roll for them if the config has a chance and a table
            if (roll.HazardChance > 0f && config.HazardTable.Count > 0)
            {
                float gate = MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.HazardGate);
                if (gate < roll.HazardChance)
                {
                    var hazard = WeightedTables.PickWeighted(config.HazardTable, MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.HazardPick), 0f, roll.HazardWeightOf);
                    if (hazard != null) { picked = hazard; hazardAssigned = true; }
                }
            }

            // GameDesignDoc "Randomness blocks > positive": only rolled on cells that didn't already
            // get a hazard, mirroring the hazard roll's shape with its own gate/table/salt.
            // PowerUpSpawnRateBonus scales the gate chance rather than table weights.
            if (!hazardAssigned && config.PowerUpChancePerCell > 0f && config.PowerUpTable.Count > 0)
            {
                float gate = MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.PowerUpGate);
                float effectiveChance = Mathf.Clamp01(config.PowerUpChancePerCell * (1f + roll.PowerUpSpawnRateBonus));
                if (gate < effectiveChance)
                {
                    var powerUp = WeightedTables.PickWeighted(config.PowerUpTable, MapRng.Value01(worldSeed, layerIndex, x, y, (int)Salt.PowerUpPick));
                    if (powerUp != null) picked = powerUp;
                }
            }

            cell.BlockTypeId = picked != null ? (byte)picked.Id : (byte)BlockTypeId.Dirt;
            return cell;
        }

        // Second pass over the already-rolled grid: any cell whose independently-rolled block is
        // Category.Ore becomes a vein seed and eats into its still-Dirt neighbors (every ore veins
        // by default; author VeinSizeMin/Max = 1 on a specific entry to opt it out). Runs after the
        // per-cell roll (so hazard/power-up cells are never touched) and before artifact placement
        // (so a guaranteed artifact can still land on/overwrite a vein cell, matching how it already
        // overwrites plain ore). Iterates in a fixed row-major order so results stay deterministic
        // for a given worldSeed regardless of how/when this is called.
        private static void GrowVeins(MapEditContext ctx, LayerGenerationTweaks tweaks)
        {
            var config = ctx.Config;
            var nextLayerConfig = ctx.NextConfig;
            for (int y = 0; y < ctx.Height; y++)
            {
                for (int x = 0; x < ctx.Width; x++)
                {
                    // Falls back to the next layer's table so ores swapped in by oreTierOddsBonus
                    // (or a whole layer rolled from the next table) still vein using their own
                    // authored vein settings.
                    byte blockTypeId = ctx.GetBlock(x, y);
                    var entry = FindOreEntry(config.OreTable, blockTypeId)
                        ?? (nextLayerConfig != null ? FindOreEntry(nextLayerConfig.OreTable, blockTypeId) : null);
                    if (entry == null || entry.BlockType.Category != BlockCategory.Ore) continue;

                    GrowVein(ctx, x, y, entry, tweaks);
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

        // Random-walk flood fill (MapEditContext.GrowBlob) from the seed cell into still-Dirt
        // neighbors, until it hits the target size or runs out of Dirt to spread into (e.g. boxed
        // in by hazards/other ores/the grid edge).
        private static void GrowVein(MapEditContext ctx, int seedX, int seedY, WeightedBlockEntry entry, LayerGenerationTweaks tweaks)
        {
            float sizeRoll = ctx.Value01(seedX, seedY, (int)Salt.VeinSize);
            int targetSize = MapEditContext.RollRange(sizeRoll, ctx.Config.VeinSizeMin, ctx.Config.VeinSizeMax);

            float sizeMultiplier = tweaks.VeinSizeMultiplier;
            if (tweaks.OreVeinSizeMultipliers != null && tweaks.OreVeinSizeMultipliers.TryGetValue(entry.BlockType.Id, out float oreMultiplier)) sizeMultiplier *= oreMultiplier;
            if (sizeMultiplier != 1f) targetSize = Mathf.Max(1, Mathf.RoundToInt(targetSize * sizeMultiplier));
            if (targetSize <= 1) return;

            byte targetId = (byte)entry.BlockType.Id;
            ctx.GrowBlob(seedX, seedY, targetSize, ctx.Config.VeinSpreadChance, (int)Salt.VeinSpread, CellFilters.UnclaimedDirt,
                (x, y) => ctx.SetBlock(x, y, targetId));
        }

        // Some of whatever Dirt is left over after ore veins, structures and artifacts have claimed
        // theirs seeds a pre-carved empty pocket, grown with the same GrowBlob random walk as veins
        // but flipping Mined instead of swapping BlockTypeId - breaks up long stretches of uniform
        // dirt without handing out free ore. Pockets stay behind fog until the player reveals them
        // normally (see MineWorld.RevealFog), so they read as a hidden cavern opening up rather than
        // an obvious freebie. Runs after artifacts so it only ever eats into leftover Dirt, never an
        // ore vein/hazard/power-up/artifact cell or a claimed structure - those all fail
        // CellFilters.UnclaimedDirt.
        private static void CarveEmptyPockets(MapEditContext ctx, LayerGenerationTweaks tweaks)
        {
            var config = ctx.Config;
            float chance = config.EmptyPocketChancePerCell * tweaks.EmptyPocketChanceMultiplier;
            if (chance <= 0f) return;

            int sizeMin = config.EmptyPocketSizeMin;
            int sizeMax = config.EmptyPocketSizeMax;
            if (tweaks.EmptyPocketSizeMultiplier != 1f)
            {
                sizeMin = Mathf.Max(1, Mathf.RoundToInt(sizeMin * tweaks.EmptyPocketSizeMultiplier));
                sizeMax = Mathf.Max(sizeMin, Mathf.RoundToInt(sizeMax * tweaks.EmptyPocketSizeMultiplier));
            }

            for (int y = 0; y < ctx.Height; y++)
            {
                for (int x = 0; x < ctx.Width; x++)
                {
                    if (!CellFilters.UnclaimedDirt(ctx, x, y)) continue;

                    float gate = ctx.Value01(x, y, (int)Salt.EmptyPocketGate);
                    if (gate >= chance) continue;

                    CarveEmptyPocket(ctx, x, y, sizeMin, sizeMax, config.EmptyPocketSpreadChance);
                }
            }
        }

        private static void CarveEmptyPocket(MapEditContext ctx, int seedX, int seedY, int sizeMin, int sizeMax, float spreadChance)
        {
            float sizeRoll = ctx.Value01(seedX, seedY, (int)Salt.EmptyPocketSize);
            int targetSize = MapEditContext.RollRange(sizeRoll, sizeMin, sizeMax);

            ctx.Carve(seedX, seedY);
            var pocketCells = new List<Vector2Int> { new(seedX, seedY) };
            ctx.Chunk.EmptyPockets.Add(pocketCells);

            ctx.GrowBlob(seedX, seedY, targetSize, spreadChance, (int)Salt.EmptyPocketSpread, CellFilters.UnclaimedDirt, (x, y) =>
            {
                ctx.Carve(x, y);
                pocketCells.Add(new Vector2Int(x, y));
            });
        }

        // Which layer hosts the Critter Shop for this seed - a pure function of the seed so
        // Critters.CritterShopController can find the cave without scanning chunks.
        public static int GetShopLayerIndex(int worldSeed)
        {
            int span = ShopCaveMaxLayer - ShopCaveMinLayer + 1;
            return ShopCaveMinLayer + (int)(MapRng.HashCell(worldSeed, 0, 0, 0, (int)Salt.ShopLayer) % (uint)span);
        }

        // A guaranteed, larger-than-any-pocket cavern for the Critter Shop: a ShopCaveWidth x
        // ShopCaveHeight room (top corners rounded off) floored with unmineable GrassyDirt so the
        // building can never be undermined, with any hazard in the surrounding ring swapped for
        // Dirt so the shopkeeper's doorstep can't blow up or cave in. Runs after veins (so it cuts
        // cleanly through them) and before structures/artifacts/pockets, claiming the room and its
        // floor so none of those land inside it. Stays behind fog like any pocket until found.
        private static void CarveShopCave(MapEditContext ctx)
        {
            int layerIndex = ctx.LayerIndex;
            int gridWidth = ctx.Width;
            int layerHeight = ctx.Height;
            int caveHeight = Mathf.Min(ShopCaveHeight, layerHeight - ShopCaveMargin * 2 - 1);
            int caveWidth = Mathf.Min(ShopCaveWidth, gridWidth - ShopCaveMargin * 2);
            if (caveHeight < 2 || caveWidth < 3)
            {
                Debug.LogWarning($"ChunkGenerator: layer {layerIndex} ({gridWidth}x{layerHeight}) is too small for the Critter Shop cave.");
                return;
            }

            var rng = ctx.CreateRandom((int)Salt.ShopCavePosition);
            int x0 = rng.Next(ShopCaveMargin, gridWidth - ShopCaveMargin - caveWidth + 1);
            // The floor row (y0 + caveHeight) must also fit inside the layer.
            int y0 = rng.Next(ShopCaveMargin, layerHeight - ShopCaveMargin - caveHeight);
            var cave = new RectInt(x0, y0, caveWidth, caveHeight);

            ctx.ClearHazards(cave, 1);

            var floor = new RectInt(cave.xMin, cave.yMax, cave.width, 1);
            ctx.FillRect(floor, null, (x, y) =>
            {
                ctx.SetBlock(x, y, BlockTypeId.GrassyDirt);
                ctx.Claim(x, y);
            });
            ctx.FillRect(cave, null, (x, y) =>
            {
                bool isRoundedCorner = y == cave.yMin && (x == cave.xMin || x == cave.xMax - 1);
                if (isRoundedCorner) return;
                ctx.Carve(x, y);
                ctx.Claim(x, y);
            });

            ctx.Chunk.ShopCave = cave;
        }

        // 1 artifact is guaranteed per layer (plus any the run modifier adds); each placement then
        // has a repeating ArtifactBonusChance to place one more, so bonus count follows a geometric
        // distribution (roll again after every success, stop on the first failure). GameDesignDoc
        // "Prestige > Prestige > increase artifact spawn rate" scales the bonus-chance roll.
        private static void PlaceArtifacts(MapEditContext ctx, float spawnRateMultiplier, LayerGenerationTweaks tweaks)
        {
            var rng = ctx.CreateRandom((int)Salt.ArtifactPlacement);

            PlaceArtifact(ctx, rng);
            for (int i = 0; i < tweaks.ExtraGuaranteedArtifacts; i++) PlaceArtifact(ctx, rng);

            float bonusChance = Mathf.Clamp01(ctx.Config.ArtifactBonusChance * spawnRateMultiplier * tweaks.ArtifactBonusChanceMultiplier);
            while (rng.NextDouble() < bonusChance)
            {
                PlaceArtifact(ctx, rng);
            }
        }

        // Rerolls off already-mined and claimed cells (the Critter Shop cave, carved structures) so
        // an artifact is never placed where it would be invisible, unminable, or inside a
        // structure. Layers without either never reroll, so their rng stream - and existing saves'
        // artifact spots - are unchanged.
        private const int ArtifactPlacementAttempts = 20;

        private static void PlaceArtifact(MapEditContext ctx, System.Random rng)
        {
            for (int attempt = 0; attempt < ArtifactPlacementAttempts; attempt++)
            {
                int x = rng.Next(0, ctx.Width);
                int y = rng.Next(0, ctx.Height);
                if (ctx.IsMined(x, y) || ctx.IsClaimed(x, y) || ctx.GetBlock(x, y) == (byte)BlockTypeId.GrassyDirt) continue;

                ctx.SetBlock(x, y, BlockTypeId.Artifact);
                return;
            }
        }
    }
}
