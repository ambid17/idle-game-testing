using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    [Serializable]
    public class WeightedBlockEntry
    {
        public BlockType BlockType;
        [Min(0f)] public float Weight = 1f;
    }

    // Authored per layer: ore/dirt table, hazard/power-up table, dirt tint, mining speed.
    // Include a "Dirt" entry directly in OreTable to represent the dirt fallback weight.
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

        [Range(0f, 1f)] public float HazardChancePerCell = 0.01f;

        [Tooltip("Chance to place an additional artifact beyond the 1 guaranteed per layer. " +
                 "Re-rolled after every success, so it's really a geometric distribution of bonus artifacts.")]
        [Range(0f, 1f)] public float ArtifactBonusChance = 0.1f;
    }
}
