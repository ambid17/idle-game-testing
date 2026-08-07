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

    // Authored per 100-block layer: ore/dirt table, hazard/power-up table, dirt tint, mining speed.
    // Include a "Dirt" entry directly in OreTable to represent the dirt fallback weight.
    [CreateAssetMenu(fileName = "LayerConfig", menuName = "Map Generation/Layer Config")]
    public class LayerConfig : ScriptableObject
    {
        [Tooltip("depth / 100, 0-based.")]
        public int LayerIndex;

        public Color DirtTint = Color.white;
        [Range(0f, 3f)] public float MiningSpeedModifier = 1f;

        public List<WeightedBlockEntry> OreTable = new();
        public List<WeightedBlockEntry> HazardTable = new();

        [Range(0f, 1f)] public float HazardChancePerCell = 0.01f;
        [Range(0f, 1f)] public float ArtifactBonusChancePerCell = 0.0005f;
    }
}
