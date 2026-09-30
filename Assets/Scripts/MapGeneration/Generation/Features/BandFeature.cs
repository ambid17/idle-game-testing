using UnityEngine;

namespace MapGeneration
{
    // A horizontal band of Block (typically unmineable Hardpan) spanning the layer, broken by a few
    // random gaps the player has to find. Optionally enriches the rows directly beneath the band
    // with extra ore - the reward for getting through.
    [CreateAssetMenu(fileName = "BandFeature", menuName = "Map Generation/Features/Band")]
    public class BandFeature : MapFeatureDefinition
    {
        [SerializeField] private BlockType block;
        [Min(1)] [SerializeField] private int thicknessMin = 1;
        [Min(1)] [SerializeField] private int thicknessMax = 2;
        [Min(1)] [SerializeField] private int gapCountMin = 2;
        [Min(1)] [SerializeField] private int gapCountMax = 3;
        [Min(1)] [SerializeField] private int gapWidthMin = 2;
        [Min(1)] [SerializeField] private int gapWidthMax = 3;
        [Tooltip("Rows kept clear of the band at the layer's top and bottom seams.")]
        [Min(0)] [SerializeField] private int edgeMargin = 3;

        [Header("Enrichment beneath the band")]
        [Min(0)] [SerializeField] private int enrichDepth = 3;
        [Tooltip("Chance each plain-Dirt cell in the enrich rows becomes ore.")]
        [Range(0f, 1f)] [SerializeField] private float enrichChance = 0.12f;
        [SerializeField] private bool enrichFromNextLayer;

        protected override void Apply(MapEditContext ctx, int instance)
        {
            if (block == null)
            {
                Debug.LogError($"BandFeature '{name}' has no block assigned.");
                return;
            }

            int thickness = MapEditContext.RollRange(ctx.Value01(instance, 0, SaltFor(instance, 3)), thicknessMin, thicknessMax);
            int maxY = ctx.Height - edgeMargin - thickness;
            if (maxY < edgeMargin) return;
            int bandY = MapEditContext.RollRange(ctx.Value01(instance, 0, SaltFor(instance, 4)), edgeMargin, maxY);

            var isGap = new bool[ctx.Width];
            int gapCount = MapEditContext.RollRange(ctx.Value01(instance, 0, SaltFor(instance, 5)), gapCountMin, gapCountMax);
            for (int g = 0; g < gapCount; g++)
            {
                int gapWidth = MapEditContext.RollRange(ctx.Value01(g, 0, SaltFor(instance, 6)), gapWidthMin, gapWidthMax);
                int gapX = MapEditContext.RollRange(ctx.Value01(g, 0, SaltFor(instance, 7)), 0, Mathf.Max(0, ctx.Width - gapWidth));
                for (int x = gapX; x < gapX + gapWidth && x < ctx.Width; x++) isGap[x] = true;
            }

            byte blockId = (byte)block.Id;
            ctx.FillRect(new RectInt(0, bandY, ctx.Width, thickness), CellFilters.Buildable, (x, y) =>
            {
                if (isGap[x]) return;
                ctx.SetBlock(x, y, blockId);
                ctx.Claim(x, y);
            });

            if (enrichDepth <= 0 || enrichChance <= 0f) return;
            int gateSalt = SaltFor(instance, 8);
            int oreSalt = SaltFor(instance, 9);
            ctx.FillRect(new RectInt(0, bandY + thickness, ctx.Width, enrichDepth), CellFilters.UnclaimedDirt, (x, y) =>
            {
                if (ctx.Value01(x, y, gateSalt) < enrichChance) ctx.SetBlock(x, y, ctx.RollOre(ctx.Value01(x, y, oreSalt), enrichFromNextLayer));
            });
        }
    }
}
