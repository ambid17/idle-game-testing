using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    // Decides whether a map-edit primitive may touch a cell. See CellFilters for the stock set.
    public delegate bool CellFilter(MapEditContext ctx, int x, int y);

    public static class CellFilters
    {
        // Plain, unmined, unclaimed Dirt - what ore veins, pockets and scatter clusters grow into.
        public static readonly CellFilter UnclaimedDirt = (ctx, x, y) =>
            !ctx.IsClaimed(x, y) && ctx.IsUnminedBlock(x, y, (byte)BlockTypeId.Dirt);

        // Anything a structure may overwrite: not claimed by an earlier structure, not a building
        // support (GrassyDirt), not already carved open.
        public static readonly CellFilter Buildable = (ctx, x, y) =>
            !ctx.IsClaimed(x, y) && !ctx.IsMined(x, y) && ctx.GetBlock(x, y) != (byte)BlockTypeId.GrassyDirt;

        // Like Buildable, but already-open ground is fine too - for carving tunnels through pockets.
        public static readonly CellFilter Carvable = (ctx, x, y) =>
            !ctx.IsClaimed(x, y) && ctx.GetBlock(x, y) != (byte)BlockTypeId.GrassyDirt;
    }

    // The shared toolkit every map edit goes through - ChunkGenerator's own passes (veins, empty
    // pockets, the Critter Shop cave) and every MapFeatureDefinition (run-modifier structures and
    // hand-authored StructureDefinitions). Pure C#, no scene access, so generation stays headless.
    //
    // Holds a generation-only claim mask: a structure claims its footprint, and every later pass
    // (veins, artifacts, pockets, other features) skips claimed cells, so nothing cuts through it.
    // The mask is never saved - it's rebuilt deterministically on every regeneration.
    public class MapEditContext
    {
        private static readonly (int dx, int dy)[] OrthogonalNeighbors = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        public readonly int WorldSeed;
        public readonly int LayerIndex;
        public readonly ChunkData Chunk;
        public readonly LayerConfig Config;
        public readonly LayerConfig NextConfig;

        private readonly bool[] claimed;

        public int Width => Chunk.Width;
        public int Height => Chunk.Height;

        public MapEditContext(int worldSeed, int layerIndex, ChunkData chunk, LayerConfig config, LayerConfig nextConfig)
        {
            WorldSeed = worldSeed;
            LayerIndex = layerIndex;
            Chunk = chunk;
            Config = config;
            NextConfig = nextConfig;
            claimed = new bool[chunk.Cells.Length];
        }

        // ---- Cell access ----

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
        public byte GetBlock(int x, int y) => Chunk.Cells[Chunk.Index(x, y)].BlockTypeId;
        public bool IsMined(int x, int y) => Chunk.Cells[Chunk.Index(x, y)].Mined;
        public bool IsUnminedBlock(int x, int y, byte blockTypeId)
        {
            var cell = Chunk.Cells[Chunk.Index(x, y)];
            return cell.BlockTypeId == blockTypeId && !cell.Mined;
        }

        public void SetBlock(int x, int y, byte blockTypeId) => Chunk.Cells[Chunk.Index(x, y)].BlockTypeId = blockTypeId;
        public void SetBlock(int x, int y, BlockTypeId blockTypeId) => SetBlock(x, y, (byte)blockTypeId);

        // Pre-carved open ground, same as an empty pocket: Dirt underneath, already mined, still
        // behind fog until the player reveals it.
        public void Carve(int x, int y)
        {
            int index = Chunk.Index(x, y);
            Chunk.Cells[index].BlockTypeId = (byte)BlockTypeId.Dirt;
            Chunk.Cells[index].Mined = true;
        }

        public bool IsClaimed(int x, int y) => claimed[Chunk.Index(x, y)];
        public void Claim(int x, int y) => claimed[Chunk.Index(x, y)] = true;

        public bool Passes(CellFilter filter, int x, int y) => InBounds(x, y) && (filter == null || filter(this, x, y));

        // ---- Deterministic randomness ----

        public float Value01(int x, int y, int salt) => MapRng.Value01(WorldSeed, LayerIndex, x, y, salt);
        public System.Random CreateRandom(int salt) => MapRng.CreateLayerRandom(WorldSeed, LayerIndex, salt);

        // Inclusive [min, max] from a single roll.
        public static int RollRange(float roll01, int min, int max)
        {
            int range = Mathf.Max(0, max - min);
            return min + Mathf.Min(range, Mathf.FloorToInt(roll01 * (range + 1)));
        }

        // ---- Ore / table rolls ----

        public static bool IsHazard(byte blockTypeId) =>
            blockTypeId == (byte)BlockTypeId.Explosive || blockTypeId == (byte)BlockTypeId.FallingRock
            || blockTypeId == (byte)BlockTypeId.GasPocket || blockTypeId == (byte)BlockTypeId.Lava;

        // An ore from this layer's table (or the next layer's, falling back to this one's when
        // there is no deeper table). Returns Dirt if the table has no ore at all.
        public byte RollOre(float roll01, bool fromNextLayer)
        {
            var config = fromNextLayer && NextConfig != null ? NextConfig : Config;
            var ore = config != null ? WeightedTables.PickWeightedOre(config.OreTable, roll01) : null;
            return ore != null ? (byte)ore.Id : (byte)BlockTypeId.Dirt;
        }

        // A power-up from this layer's table, or a Treasure Chest if the layer has none authored.
        public byte RollPowerUp(float roll01)
        {
            var powerUp = Config != null && Config.PowerUpTable.Count > 0 ? WeightedTables.PickWeighted(Config.PowerUpTable, roll01) : null;
            return powerUp != null ? (byte)powerUp.Id : (byte)BlockTypeId.TreasureChest;
        }

        // ---- Drawing primitives ----

        // Calls apply on every in-bounds cell of rect that passes filter. Returns how many.
        public int FillRect(RectInt rect, CellFilter filter, Action<int, int> apply)
        {
            int count = 0;
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (!Passes(filter, x, y)) continue;
                    apply(x, y);
                    count++;
                }
            }
            return count;
        }

        // Cells whose rounded distance from center is exactly radius (the shell), or below it (the disc).
        public int Ring(Vector2Int center, int radius, CellFilter filter, Action<int, int> apply) =>
            ForEachInRadius(center, radius, filter, apply, shellOnly: true);

        public int Disc(Vector2Int center, int radius, CellFilter filter, Action<int, int> apply) =>
            ForEachInRadius(center, radius, filter, apply, shellOnly: false);

        private int ForEachInRadius(Vector2Int center, int radius, CellFilter filter, Action<int, int> apply, bool shellOnly)
        {
            int count = 0;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int distance = Mathf.RoundToInt(Mathf.Sqrt(dx * dx + dy * dy));
                    if (shellOnly ? distance != radius : distance >= radius) continue;
                    int x = center.x + dx, y = center.y + dy;
                    if (!Passes(filter, x, y)) continue;
                    apply(x, y);
                    count++;
                }
            }
            return count;
        }

        // Swaps every hazard inside rect (grown by margin) for plain Dirt - keeps structure
        // doorsteps from blowing up or caving in.
        public void ClearHazards(RectInt rect, int margin)
        {
            var grown = new RectInt(rect.xMin - margin, rect.yMin - margin, rect.width + margin * 2, rect.height + margin * 2);
            FillRect(grown, (ctx, x, y) => IsHazard(ctx.GetBlock(x, y)), (x, y) => SetBlock(x, y, BlockTypeId.Dirt));
        }

        public void ClaimRect(RectInt rect) => FillRect(rect, null, Claim);

        // Random-walk flood fill from (seedX, seedY) - the shared algorithm behind ore veins, empty
        // pockets and scatter clusters. The seed cell itself is NOT touched (the caller decides what
        // it becomes); neighbors are pulled at random from a frontier of cells passing canSpread,
        // each accepted with spreadChance, until targetSize (seed included) is reached or the
        // frontier runs dry. Every roll is keyed off (seed cell, salt) so results are deterministic
        // regardless of generation order. Returns the final size including the seed.
        public int GrowBlob(int seedX, int seedY, int targetSize, float spreadChance, int salt, CellFilter canSpread, Action<int, int> apply)
        {
            if (targetSize <= 1) return 1;

            var frontier = new List<(int x, int y)>();
            AddNeighbors(seedX, seedY, canSpread, frontier);

            int currentSize = 1;
            int attempt = 0;
            while (currentSize < targetSize && frontier.Count > 0)
            {
                float pickRoll = Value01(seedX, seedY, salt + attempt);
                int pickIndex = Mathf.Min(frontier.Count - 1, Mathf.FloorToInt(pickRoll * frontier.Count));
                var (fx, fy) = frontier[pickIndex];
                frontier.RemoveAt(pickIndex);
                attempt++;

                if (!canSpread(this, fx, fy)) continue; // claimed by an overlapping blob already

                float spreadRoll = Value01(fx, fy, salt);
                if (spreadRoll > spreadChance) continue;

                apply(fx, fy);
                currentSize++;
                AddNeighbors(fx, fy, canSpread, frontier);
            }

            return currentSize;
        }

        private void AddNeighbors(int x, int y, CellFilter filter, List<(int x, int y)> frontier)
        {
            foreach (var (dx, dy) in OrthogonalNeighbors)
            {
                int nx = x + dx, ny = y + dy;
                if (Passes(filter, nx, ny)) frontier.Add((nx, ny));
            }
        }

        // A downward-winding path from `from` to `to` (to.y >= from.y), one row at a time, drifting
        // sideways at random but always steering early enough to arrive at to.x. `brushWidth` cells
        // wide (extending right of the path). Horizontal moves are painted too, so the path is
        // always 4-connected and walkable. Positions are clamped to [margin, Width - margin - brushWidth].
        public void DrunkWalk(Vector2Int from, Vector2Int to, int brushWidth, int margin, float wobbleChance, int salt, CellFilter filter, Action<int, int> apply)
        {
            int minX = margin;
            int maxX = Mathf.Max(minX, Width - margin - brushWidth);
            int x = Mathf.Clamp(from.x, minX, maxX);
            int targetX = Mathf.Clamp(to.x, minX, maxX);

            for (int y = from.y; y <= to.y; y++)
            {
                PaintBrush(x, y, brushWidth, filter, apply);
                if (y == to.y) break;

                int rowsLeft = to.y - y;
                int step;
                int distance = targetX - x;
                if (Mathf.Abs(distance) >= rowsLeft) step = Math.Sign(distance);
                else
                {
                    float roll = Value01(x, y, salt);
                    if (roll < wobbleChance) step = Value01(x, y, salt + 1) < 0.5f ? -1 : 1;
                    else step = Math.Sign(distance);
                }

                int nextX = Mathf.Clamp(x + step, minX, maxX);
                if (nextX != x)
                {
                    // Paint the sideways step on this row before dropping, keeping the path connected.
                    PaintBrush(nextX, y, brushWidth, filter, apply);
                    x = nextX;
                }

                // Something the filter refuses and that isn't already open (e.g. the Critter Shop's
                // unmineable floor) blocks the row below - slide sideways along this row until the
                // way down is clear, preferring the target's side.
                if (IsBrushBlocked(x, y + 1, brushWidth, filter))
                {
                    int preferred = targetX >= x ? 1 : -1;
                    int detourX = FindUnblockedX(x, y + 1, brushWidth, filter, minX, maxX, preferred);
                    while (x != detourX)
                    {
                        x += Math.Sign(detourX - x);
                        PaintBrush(x, y, brushWidth, filter, apply);
                    }
                }
            }

            // Arrived on the target row but not the target column - finish horizontally.
            while (x != targetX)
            {
                x += Math.Sign(targetX - x);
                PaintBrush(x, to.y, brushWidth, filter, apply);
            }
        }

        private bool IsBrushBlocked(int x, int y, int brushWidth, CellFilter filter)
        {
            for (int i = 0; i < brushWidth; i++)
            {
                if (InBounds(x + i, y) && !IsMined(x + i, y) && !Passes(filter, x + i, y)) return true;
            }
            return false;
        }

        // Nearest x (searching outward, `preferred` side first at each distance) whose brush on row
        // y is unblocked. Returns x unchanged if the whole row is blocked.
        private int FindUnblockedX(int x, int y, int brushWidth, CellFilter filter, int minX, int maxX, int preferred)
        {
            for (int distance = 1; distance <= maxX - minX; distance++)
            {
                int first = x + preferred * distance, second = x - preferred * distance;
                if (first >= minX && first <= maxX && !IsBrushBlocked(first, y, brushWidth, filter)) return first;
                if (second >= minX && second <= maxX && !IsBrushBlocked(second, y, brushWidth, filter)) return second;
            }
            return x;
        }

        private void PaintBrush(int x, int y, int brushWidth, CellFilter filter, Action<int, int> apply)
        {
            for (int i = 0; i < brushWidth; i++)
            {
                if (Passes(filter, x + i, y)) apply(x + i, y);
            }
        }

        // Every cell passing filter gets a gate roll; hits call apply. Row-major, deterministic.
        public int Scatter(float chancePerCell, int salt, CellFilter filter, Action<int, int> apply)
        {
            if (chancePerCell <= 0f) return 0;
            int count = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!Passes(filter, x, y)) continue;
                    if (Value01(x, y, salt) >= chancePerCell) continue;
                    apply(x, y);
                    count++;
                }
            }
            return count;
        }

        // ---- Placement helpers ----

        // A random w x h rect (at least `margin` from every chunk edge) where every cell passes
        // filter. False if none was found within `attempts` tries.
        public bool TryFindRect(int w, int h, int margin, CellFilter filter, int salt, out RectInt rect, int attempts = 30)
        {
            rect = default;
            int maxX = Width - margin - w;
            int maxY = Height - margin - h;
            if (maxX < margin || maxY < margin) return false;

            var rng = CreateRandom(salt);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var candidate = new RectInt(rng.Next(margin, maxX + 1), rng.Next(margin, maxY + 1), w, h);
                if (AllPass(candidate, filter))
                {
                    rect = candidate;
                    return true;
                }
            }
            return false;
        }

        public bool AllPass(RectInt rect, CellFilter filter)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (!Passes(filter, x, y)) return false;
                }
            }
            return true;
        }
    }
}
