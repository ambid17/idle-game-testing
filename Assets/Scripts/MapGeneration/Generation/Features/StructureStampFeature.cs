using UnityEngine;

namespace MapGeneration
{
    // Stamps a hand-authored StructureDefinition somewhere it fits on the layer: a random spot
    // (EdgeMargin from the chunk edges) where every footprint cell is Buildable - unclaimed,
    // unmined, not a building support. Orientation is rolled from the structure's allowed
    // mirror/rotation options. Skipped silently if nowhere fits.
    [CreateAssetMenu(fileName = "StructureStampFeature", menuName = "Map Generation/Features/Structure Stamp")]
    public class StructureStampFeature : MapFeatureDefinition
    {
        [SerializeField] private StructureDefinition structure;
        [Min(0)] [SerializeField] private int edgeMargin = 2;

        protected override void Apply(MapEditContext ctx, int instance)
        {
            if (structure == null)
            {
                Debug.LogError($"StructureStampFeature '{name}' has no structure assigned.");
                return;
            }

            var source = structure.Grid;
            int rotation = structure.AllowRotation ? Mathf.Min(3, Mathf.FloorToInt(ctx.Value01(instance, 0, SaltFor(instance, 3)) * 4)) : 0;
            bool mirror = structure.AllowMirror && ctx.Value01(instance, 0, SaltFor(instance, 4)) < 0.5f;
            var grid = Orient(source, rotation, mirror);

            int w = grid.GetLength(0), h = grid.GetLength(1);
            if (w == 0 || h == 0) return;
            if (!ctx.TryFindRect(w, h, edgeMargin, CellFilters.Buildable, SaltFor(instance, 5), out var rect)) return;

            if (structure.ClearHazardMargin >= 0) ctx.ClearHazards(rect, structure.ClearHazardMargin);

            int oreSalt = SaltFor(instance, 6);
            for (int gy = 0; gy < h; gy++)
            {
                for (int gx = 0; gx < w; gx++)
                {
                    int x = rect.xMin + gx, y = rect.yMin + gy;
                    structure.TryResolve(grid[gx, gy], out var action, out var block);
                    StampCell(ctx, x, y, action, block, oreSalt);
                    if (structure.ClaimFootprint || action != StructureCellAction.Keep) ctx.Claim(x, y);
                }
            }
        }

        private static void StampCell(MapEditContext ctx, int x, int y, StructureCellAction action, BlockType block, int oreSalt)
        {
            switch (action)
            {
                case StructureCellAction.Carve: ctx.Carve(x, y); break;
                case StructureCellAction.Block: if (block != null) ctx.SetBlock(x, y, (byte)block.Id); break;
                case StructureCellAction.OreThisLayer: ctx.SetBlock(x, y, ctx.RollOre(ctx.Value01(x, y, oreSalt), false)); break;
                case StructureCellAction.OreNextLayer: ctx.SetBlock(x, y, ctx.RollOre(ctx.Value01(x, y, oreSalt), true)); break;
                case StructureCellAction.Artifact: ctx.SetBlock(x, y, BlockTypeId.Artifact); break;
                case StructureCellAction.PowerUp: ctx.SetBlock(x, y, ctx.RollPowerUp(ctx.Value01(x, y, oreSalt))); break;
            }
        }

        // Rotates clockwise `quarterTurns` times, then optionally mirrors left-right.
        public static char[,] Orient(char[,] source, int quarterTurns, bool mirror)
        {
            var grid = source;
            for (int i = 0; i < quarterTurns; i++)
            {
                int w = grid.GetLength(0), h = grid.GetLength(1);
                var rotated = new char[h, w];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++) rotated[h - 1 - y, x] = grid[x, y];
                }
                grid = rotated;
            }

            if (!mirror) return grid;
            int mw = grid.GetLength(0), mh = grid.GetLength(1);
            var mirrored = new char[mw, mh];
            for (int y = 0; y < mh; y++)
            {
                for (int x = 0; x < mw; x++) mirrored[mw - 1 - x, y] = grid[x, y];
            }
            return mirrored;
        }
    }
}
