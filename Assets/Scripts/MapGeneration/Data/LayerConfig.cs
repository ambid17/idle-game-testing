using System;
using System.Collections.Generic;
using Atmosphere;
using Critters;
using UnityEngine;
using UnityEngine.Rendering;

namespace MapGeneration
{
    [Serializable]
    public class WeightedBlockEntry
    {
        public BlockType BlockType;
        [Min(0f)] public float Weight = 1f;
    }

    [Serializable]
    public class WeightedCritterEntry
    {
        public CritterDefinition Critter;
        [Min(0f)] public float Weight = 1f;
    }

    // Authored per layer: ore/dirt table, hazard/power-up table, dirt tint, mining speed.
    // Don't author Dirt in OreTable - every layer rolls Dirt as filler at ChunkGenerator.DirtFillerWeight.
    [CreateAssetMenu(fileName = "LayerConfig", menuName = "Map Generation/Layer Config")]
    public class LayerConfig : ScriptableObject
    {
        [Tooltip("depth / layerHeight, 0-based.")]
        public int LayerIndex;

        [Range(0, 100)] public int LayerHeight = 30;

        [Tooltip("Tint applied to all dirt blocks in this layer, based on depth")]
        public Color LayerDirtTint = Color.white;
        [Range(0f, 3f)] public float BlockHealth = 1f;

        public List<WeightedBlockEntry> OreTable = new();
        public List<WeightedBlockEntry> HazardTable = new();
        public List<WeightedBlockEntry> PowerUpTable = new();

        [Range(0f, 1f)] public float HazardChancePerCell = 0.01f;

        // GameDesignDoc "Randomness blocks > positive" (treasure chest / sight potion). Defaults to
        // 0 so existing authored layers stay unaffected until a PowerUpTable is populated.
        [Range(0f, 1f)] public float PowerUpChancePerCell = 0f;

        [Tooltip("Chance to place an additional artifact beyond the 1 guaranteed per layer. " +
                 "Re-rolled after every success, so it's really a geometric distribution of bonus artifacts.")]
        [Range(0f, 1f)] public float ArtifactBonusChance = 0.1f;

        [Header("Empty pockets (pre-carved caverns grown with the same vein logic as ores, to break up long stretches of uniform dirt)")]
        [Tooltip("Chance, checked per still-Dirt cell after ore veins/artifacts are placed, that it seeds an empty pocket. 0 = no pockets (default, existing authored layers are unaffected).")]
        [Range(0f, 1f)] public float EmptyPocketChancePerCell = 0f;
        [Tooltip("Chance, checked per candidate cell, that the pocket spreads into it. Higher = denser/more compact pockets.")]
        [Range(0f, 1f)] public float EmptyPocketSpreadChance = 0.5f;
        [Tooltip("Total cells in the pocket including the seed cell. A random value in [Min, Max] is picked per seed.")]
        [Min(1)] public int EmptyPocketSizeMin = 2;
        [Min(1)] public int EmptyPocketSizeMax = 5;

        [Header("Map features (hand-authored structures/layouts stamped onto this layer - see MapFeatureDefinition)")]
        [Tooltip("Run on every generation of this layer, each at its own phase and with its own placement rules. Run modifiers can add more on top.")]
        public List<MapFeatureDefinition> Features = new();

        [Header("Critters (spawned by Critters.CritterSpawner inside this layer's empty pockets)")]
        [Tooltip("This layer's critter set - one entry is rolled (by weight) per pocket that passes CritterChancePerPocket.")]
        public List<WeightedCritterEntry> CritterTable = new();
        [Tooltip("Chance, per empty pocket, that a critter lives in it.")]
        [Range(0f, 1f)] public float CritterChancePerPocket = 0.15f;

        [Header("Atmosphere (Atmosphere.MineAtmosphere - ambient particles and sound while the player is in this layer)")]
        [Tooltip("Tint for this layer's floating spores and falling dust.")]
        public Color AmbientParticleColor = new(1f, 0.9f, 0.6f, 1f);
        [Tooltip("Glowing spores drifting up through open ground near the player, per second. 0 = none (shallow layers).")]
        [Min(0f)] public float SporesPerSecond = 0f;
        [Tooltip("Water drips falling from open ceilings near the player, per second.")]
        [Min(0f)] public float DripsPerSecond = 0.5f;
        [Tooltip("Looping ambience that crossfades in while the player is in this layer. Null keeps whatever is already playing.")]
        public AudioClip AmbientLoop;
        [Tooltip("Colour grading (Atmosphere.BiomeGrading) that fades in over the base post-processing while the player is in this layer. Layers of one biome share a profile. Null = base grading only.")]
        public VolumeProfile GradingProfile;

        [Tooltip("Parallax depth planes (Atmosphere.ParallaxBackdrop) seen through dug-out cells. Layers of one biome share one - consecutive layers with the same backdrop form one biome span. Null = ChunkTilemapView's flat tinted background instead.")]
        public BiomeBackdrop Backdrop;


        [Header("Vein (applies only when BlockType.Category is Ore - every ore entry veins by default; set VeinSizeMin/Max to 1 to opt a specific ore out)")]
        [Tooltip("Chance, checked per candidate cell, that the vein spreads into it. Higher = denser/more compact veins.")]
        [Range(0f, 1f)] public float VeinSpreadChance = 0.5f;
        [Tooltip("Total cells in the vein including the seed cell. A random value in [Min, Max] is picked per seed.")]
        [Min(1)] public int VeinSizeMin = 2;
        [Min(1)] public int VeinSizeMax = 4;
    }
}
