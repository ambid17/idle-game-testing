using UnityEngine;

namespace MapGeneration
{
    // Seeds clusters of Block across a layer's plain Dirt: each Dirt cell has ChancePerCell to seed
    // a cluster, grown with the same GrowBlob random walk as ore veins. Used for hazard-heavy
    // layers (FallingRock clusters etc.), but works for any block.
    [CreateAssetMenu(fileName = "ScatterFeature", menuName = "Map Generation/Features/Scatter")]
    public class ScatterFeature : MapFeatureDefinition
    {
        [SerializeField] private BlockType block;
        [Range(0f, 1f)] [SerializeField] private float chancePerCell = 0.01f;
        [Min(1)] [SerializeField] private int clusterSizeMin = 1;
        [Min(1)] [SerializeField] private int clusterSizeMax = 3;
        [Range(0f, 1f)] [SerializeField] private float clusterSpreadChance = 0.6f;

        protected override void Apply(MapEditContext ctx, int instance)
        {
            if (block == null)
            {
                Debug.LogError($"ScatterFeature '{name}' has no block assigned.");
                return;
            }

            byte blockId = (byte)block.Id;
            int sizeSalt = SaltFor(instance, 4);
            int spreadSalt = SaltFor(instance, 5);
            ctx.Scatter(chancePerCell, SaltFor(instance, 3), CellFilters.UnclaimedDirt, (x, y) =>
            {
                ctx.SetBlock(x, y, blockId);
                int size = MapEditContext.RollRange(ctx.Value01(x, y, sizeSalt), clusterSizeMin, clusterSizeMax);
                ctx.GrowBlob(x, y, size, clusterSpreadChance, spreadSalt, CellFilters.UnclaimedDirt, (cx, cy) => ctx.SetBlock(cx, cy, blockId));
            });
        }
    }
}
