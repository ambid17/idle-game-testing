using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    // Resolves a layer index to its authored LayerConfig. Beyond the deepest authored layer,
    // holds at the deepest one (each authored config already trends toward rarer/deeper ores) -
    // add more authored layers over time, or extend this with formula-driven scaling later.
    [CreateAssetMenu(fileName = "LayerConfigProvider", menuName = "Map Generation/Layer Config Provider")]
    public class LayerConfigProvider : ScriptableObject
    {
        public List<LayerConfig> AuthoredLayers = new();

        public LayerConfig GetConfig(int layerIndex)
        {
            if (AuthoredLayers.Count == 0) return null;

            LayerConfig best = null;
            foreach (var layer in AuthoredLayers)
            {
                if (layer.LayerIndex == layerIndex) return layer;
                if (layer.LayerIndex <= layerIndex && (best == null || layer.LayerIndex > best.LayerIndex))
                {
                    best = layer;
                }
            }

            return best != null ? best : AuthoredLayers[0];
        }

        // Depth (in blocks) at which this layer starts - inverse of GetLayerIndexAtDepth.
        public int GetLayerOffset(int layerIndex)
        {
            var yOffset = 0;
            for (int i = 0; i < layerIndex; i++)
            {
                yOffset += GetConfig(i).LayerHeight;
            }
            return yOffset;
        }

        public int GetLayerIndexAtDepth(int depthInBlocks)
        {
            var currentTotalDepth = 0;
            for (int i = 0; i < AuthoredLayers.Count; i++)
            {
                var config = GetConfig(i);
                if (depthInBlocks < currentTotalDepth + config.LayerHeight)
                {
                    return i;
                }
                currentTotalDepth += config.LayerHeight;
            }
            return 0;
        }

        // Single source of truth for world-Y -> depth-in-blocks conversion. cellSize is passed in
        // rather than owned here since it's a MapGenerationConfig concern, not layer-geometry data.
        public int GetDepthInBlocksAtWorldY(float worldY, float cellSize)
        {
            int depthInBlocks = Mathf.FloorToInt(-worldY / cellSize);
            depthInBlocks++; // Convert to 1-based depth for layer offset calculations.
            return depthInBlocks;
        }

        public int GetLayerIndexAtWorldY(float worldY, float cellSize) =>
            GetLayerIndexAtDepth(GetDepthInBlocksAtWorldY(worldY, cellSize));
    }
}
