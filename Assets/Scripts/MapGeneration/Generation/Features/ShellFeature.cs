using UnityEngine;

namespace MapGeneration
{
    // A hollow shell of ShellBlock (a geode): a ring of shell cells around a core that's part ore
    // (from this layer's or the next layer's table), part carved-open hollow. The whole footprint
    // is claimed, so artifacts/pockets/other structures stay out of it.
    [CreateAssetMenu(fileName = "ShellFeature", menuName = "Map Generation/Features/Shell (Geode)")]
    public class ShellFeature : MapFeatureDefinition
    {
        [SerializeField] private BlockType shellBlock;
        [Min(2)] [SerializeField] private int radiusMin = 2;
        [Min(2)] [SerializeField] private int radiusMax = 3;
        [Tooltip("Chance each core cell is ore; the rest is carved hollow.")]
        [Range(0f, 1f)] [SerializeField] private float coreOreChance = 0.6f;
        [Tooltip("Roll core ore from the next layer's table (falls back to this layer's on the deepest layer).")]
        [SerializeField] private bool coreFromNextLayer = true;
        [Min(0)] [SerializeField] private int edgeMargin = 1;

        protected override void Apply(MapEditContext ctx, int instance)
        {
            if (shellBlock == null)
            {
                Debug.LogError($"ShellFeature '{name}' has no shell block assigned.");
                return;
            }

            int radius = MapEditContext.RollRange(ctx.Value01(instance, 0, SaltFor(instance, 3)), radiusMin, radiusMax);
            int size = radius * 2 + 1;
            if (!ctx.TryFindRect(size, size, edgeMargin, CellFilters.Buildable, SaltFor(instance, 4), out var rect)) return;

            var center = new Vector2Int(rect.xMin + radius, rect.yMin + radius);
            byte shellId = (byte)shellBlock.Id;
            int coreSalt = SaltFor(instance, 5);
            int oreSalt = SaltFor(instance, 6);

            ctx.Disc(center, radius, null, (x, y) =>
            {
                if (ctx.Value01(x, y, coreSalt) < coreOreChance) ctx.SetBlock(x, y, ctx.RollOre(ctx.Value01(x, y, oreSalt), coreFromNextLayer));
                else ctx.Carve(x, y);
            });
            ctx.Ring(center, radius, null, (x, y) => ctx.SetBlock(x, y, shellId));
            ctx.ClaimRect(rect);
        }
    }
}
