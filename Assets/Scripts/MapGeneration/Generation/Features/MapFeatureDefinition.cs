using System;
using UnityEngine;

namespace MapGeneration
{
    // Where in ChunkGenerator.Generate's fixed pass order a feature runs:
    //   RollCell -> GrowVeins -> CarveShopCave -> [Structures] -> PlaceArtifacts -> CarveEmptyPockets -> [Overlay]
    public enum MapFeaturePhase
    {
        // Carves/stamps terrain after ore veins and the shop cave exist, before artifacts and
        // pockets - so artifacts and pockets route around whatever the structure claims.
        Structures = 0,
        // Last pass - sprinkles on top of the finished layer (hazard scatter etc.).
        Overlay = 1,
    }

    [Serializable]
    public class MapFeaturePlacement
    {
        [Tooltip("First layer this feature may appear on. Layer 0 hosts the surface buildings, so it defaults to 1.")]
        [Min(0)] public int MinLayer = 1;
        [Tooltip("Last layer this feature may appear on. -1 = no limit.")]
        public int MaxLayer = -1;
        [Tooltip("LayerConfig.Features only: always takes the layer's one feature slot when eligible (story rooms). Ignores Weight.")]
        public bool Guaranteed;
        [Tooltip("LayerConfig.Features only: relative odds of being the one feature picked for an eligible layer. Unused by run-modifier features, which always run.")]
        [Min(0f)] public float Weight = 1f;
        [Tooltip("Instances placed each time this feature runs - random in [Min, Max].")]
        [Min(0)] public int CountMin = 1;
        [Min(0)] public int CountMax = 1;

        public bool AllowsLayer(int layerIndex) => layerIndex >= MinLayer && (MaxLayer < 0 || layerIndex <= MaxLayer);
    }

    // Base for every map edit that isn't part of ChunkGenerator's core passes - run-modifier
    // structures (geodes, hardpan bands, tunnels, relic chambers, hazard scatter) and hand-authored
    // StructureDefinition stamps. Features are attached per layer via LayerConfig.Features, or for a
    // whole run via RunModifierDefinition. All drawing goes through MapEditContext.
    //
    // Must stay pure (no scene access) - chunks are regenerated from the seed on every load and by
    // the headless idle simulation, and every roll must come from ctx's deterministic RNG salted
    // with this feature's Salt (derived from Id, not list order, so adding a feature never shifts
    // another feature's rolls).
    public abstract class MapFeatureDefinition : ScriptableObject
    {
        [Tooltip("Stable, unique id. Seeds this feature's randomness - changing it re-rolls every map it appears on.")]
        [SerializeField] private string id;
        [SerializeField] private MapFeaturePhase phase = MapFeaturePhase.Structures;
        [SerializeField] private MapFeaturePlacement placement = new();

        public string Id => id;
        public MapFeaturePhase Phase => phase;
        public MapFeaturePlacement Placement => placement;

        // FNV-1a over Id - stable across runs/platforms, unlike string.GetHashCode.
        public int Salt
        {
            get
            {
                unchecked
                {
                    uint hash = 2166136261u;
                    foreach (char c in id ?? name) hash = (hash ^ c) * 16777619u;
                    return (int)hash;
                }
            }
        }

        // Distinct salt per (instance, purpose) so one feature's rolls never correlate.
        protected int SaltFor(int instance, int purpose) => unchecked(Salt + instance * 7919 + purpose * 104729);

        // Layer eligibility + instance count, then Apply once per instance. Whether a LayerConfig
        // feature runs at all is decided by ChunkGenerator's one-feature-per-layer pick.
        // Override for features that span layers (see ShaftFeature).
        public virtual void Run(MapEditContext ctx)
        {
            if (!placement.AllowsLayer(ctx.LayerIndex)) return;

            int count = MapEditContext.RollRange(ctx.Value01(0, 0, SaltFor(0, 2)), placement.CountMin, placement.CountMax);
            for (int i = 0; i < count; i++) Apply(ctx, i);
        }

        protected abstract void Apply(MapEditContext ctx, int instance);
    }
}
