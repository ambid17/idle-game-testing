using UnityEngine;

namespace MapGeneration
{
    // Pre-carved winding shafts that run down through several layers - a fast way deep.
    //
    // Chunks generate independently (and in any order), so a shaft can't be traced top-to-bottom
    // in one go. Instead every roll that must agree across layers is layer-independent:
    //  - how many shafts the world has, and each shaft's start layer and span, are rolled from the
    //    world seed alone;
    //  - where shaft i crosses the seam between layer b-1 and layer b (its "boundary anchor") is a
    //    pure function of (seed, b, i).
    // Each layer then only carves its own segment, from its top anchor to its bottom anchor (or a
    // random start/end point on the shaft's first/last layer), and the segments meet at the seams.
    [CreateAssetMenu(fileName = "ShaftFeature", menuName = "Map Generation/Features/Shaft")]
    public class ShaftFeature : MapFeatureDefinition
    {
        [Tooltip("Shafts per world (not per layer). Placement.MinLayer/MaxLayer bound where a shaft may START; Placement's weight/count are unused.")]
        [Min(0)] [SerializeField] private int shaftCountMin = 2;
        [Min(0)] [SerializeField] private int shaftCountMax = 3;
        [Tooltip("How many layers one shaft runs through.")]
        [Min(1)] [SerializeField] private int layerSpanMin = 2;
        [Min(1)] [SerializeField] private int layerSpanMax = 4;
        [Tooltip("Start layer upper bound used when Placement.MaxLayer is unlimited (-1).")]
        [Min(0)] [SerializeField] private int defaultStartLayerRange = 6;
        [Min(1)] [SerializeField] private int brushWidth = 2;
        [Range(0f, 1f)] [SerializeField] private float wobbleChance = 0.35f;
        [Min(0)] [SerializeField] private int edgeMargin = 2;

        // Layer-independent roll (layer slot -1 is never a real layer).
        private float WorldValue01(MapEditContext ctx, int shaft, int purpose) => MapRng.Value01(ctx.WorldSeed, -1, shaft, 0, SaltFor(shaft, purpose));

        public override void Run(MapEditContext ctx)
        {
            int shaftCount = MapEditContext.RollRange(WorldValue01(ctx, 0, 1), shaftCountMin, shaftCountMax);
            int minStart = Placement.MinLayer;
            int maxStart = Placement.MaxLayer >= 0 ? Placement.MaxLayer : minStart + defaultStartLayerRange;

            for (int shaft = 0; shaft < shaftCount; shaft++)
            {
                int startLayer = MapEditContext.RollRange(WorldValue01(ctx, shaft, 2), minStart, maxStart);
                int span = MapEditContext.RollRange(WorldValue01(ctx, shaft, 3), layerSpanMin, layerSpanMax);
                int endLayer = startLayer + span - 1;
                if (ctx.LayerIndex < startLayer || ctx.LayerIndex > endLayer) continue;

                var from = ctx.LayerIndex == startLayer
                    ? new Vector2Int(RandomX(ctx, shaft, 4), MapEditContext.RollRange(ctx.Value01(shaft, 0, SaltFor(shaft, 5)), ctx.Height / 5, ctx.Height / 2))
                    : new Vector2Int(AnchorX(ctx, shaft, ctx.LayerIndex), 0);
                var to = ctx.LayerIndex == endLayer
                    ? new Vector2Int(RandomX(ctx, shaft, 6), MapEditContext.RollRange(ctx.Value01(shaft, 0, SaltFor(shaft, 7)), ctx.Height / 2, ctx.Height * 4 / 5))
                    : new Vector2Int(AnchorX(ctx, shaft, ctx.LayerIndex + 1), ctx.Height - 1);

                ctx.DrunkWalk(from, to, brushWidth, edgeMargin, wobbleChance, SaltFor(shaft, 8), CellFilters.Carvable, ctx.Carve);
            }
        }

        // Where shaft crosses the seam at the top of `boundaryLayer` - identical from both sides.
        private int AnchorX(MapEditContext ctx, int shaft, int boundaryLayer) =>
            MapEditContext.RollRange(MapRng.Value01(ctx.WorldSeed, boundaryLayer, shaft, 0, SaltFor(shaft, 9)), MinX, MaxX(ctx));

        private int RandomX(MapEditContext ctx, int shaft, int purpose) =>
            MapEditContext.RollRange(ctx.Value01(shaft, 0, SaltFor(shaft, purpose)), MinX, MaxX(ctx));

        // Must match DrunkWalk's clamp range so anchors are never shifted by it.
        private int MinX => edgeMargin;
        private int MaxX(MapEditContext ctx) => Mathf.Max(edgeMargin, ctx.Width - edgeMargin - brushWidth);

        // Shafts carve per layer from Run itself.
        protected override void Apply(MapEditContext ctx, int instance) { }
    }
}
