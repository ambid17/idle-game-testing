using System.Collections.Generic;

namespace MapGeneration
{
    // Per-layer adjustments layered on top of an authored LayerConfig at generation time -
    // resolved by the caller (RunModifiers.RunModifierResolver, from the run's active modifier) so
    // ChunkGenerator stays pure and LayerConfig assets are never mutated. Every default is the
    // identity, and ChunkGenerator skips the modified code paths entirely when a field is at its
    // default, so None generates exactly what the un-tweaked generator did.
    public class LayerGenerationTweaks
    {
        public static readonly LayerGenerationTweaks None = new();

        // ---- Ore ----
        // Scales every ore entry's weight against the implicit Dirt filler (more/fewer ore seeds).
        public float AllOreWeightMultiplier = 1f;
        // Per-ore weight scaling, on top of AllOreWeightMultiplier. Null = none.
        public Dictionary<BlockTypeId, float> OreWeightMultipliers;
        // Roll this layer's ore from the next layer's table instead of its own.
        public bool UseNextLayerOreTable;
        // Added to the caller's oreTierOddsBonus (chance an ore is swapped for a next-layer ore).
        public float OreTierOddsBonus;

        // ---- Veins ----
        public bool DisableVeins;
        public float VeinSizeMultiplier = 1f;
        // Per-ore vein size scaling, on top of VeinSizeMultiplier. Null = none.
        public Dictionary<BlockTypeId, float> OreVeinSizeMultipliers;

        // ---- Hazards / power-ups ----
        public float HazardChanceMultiplier = 1f;
        // Per-behavior hazard weight scaling. Null = none.
        public Dictionary<CustomBehavior, float> HazardWeightMultipliers;
        // Added to the caller's powerUpSpawnRateBonus.
        public float PowerUpSpawnRateBonus;

        // ---- Artifacts ----
        public int ExtraGuaranteedArtifacts;
        public float ArtifactBonusChanceMultiplier = 1f;

        // ---- Empty pockets ----
        public float EmptyPocketChanceMultiplier = 1f;
        public float EmptyPocketSizeMultiplier = 1f;

        // ---- Map features ----
        // Run on top of LayerConfig.Features, each at its own Phase.
        public readonly List<MapFeatureDefinition> Features = new();
    }
}
