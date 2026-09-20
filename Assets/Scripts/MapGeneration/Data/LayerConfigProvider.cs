using System.Collections.Generic;
using Economy;
using UnityEngine;

namespace MapGeneration
{
    // Resolves a layer index to its authored LayerConfig. Beyond the deepest authored layer,
    // holds at the deepest one (each authored config already trends toward rarer/deeper ores) -
    // add more authored layers over time, or extend this with formula-driven scaling later.
    [CreateAssetMenu(fileName = "LayerConfigProvider", menuName = "Map Generation/Layer Config Provider")]
    public class LayerConfigProvider : ScriptableObject
    {
        // Floor so PrestigeUpgradeManager.LayerSizeReduction can't shrink a layer to nothing.
        private const int MinLayerHeight = 5;

        public List<LayerConfig> LayerConfigs = new();

        public LayerConfig GetConfig(int layerIndex)
        {
            if (LayerConfigs.Count == 0) return null;

            LayerConfig best = null;
            foreach (var layer in LayerConfigs)
            {
                if (layer.LayerIndex == layerIndex) return layer;
                if (layer.LayerIndex <= layerIndex && (best == null || layer.LayerIndex > best.LayerIndex))
                {
                    best = layer;
                }
            }

            return best != null ? best : LayerConfigs[0];
        }

        // GameDesignDoc "Prestige > Mining > adjust layer sizes": authored LayerHeight minus the
        // prestige perk's reduction. Deliberately never writes back to the LayerConfig asset itself
        // (a shared ScriptableObject - mutating its fields at runtime would corrupt the authored
        // asset), so every depth/generation calculation reads this instead of config.LayerHeight
        // directly.
        public int GetEffectiveLayerHeight(int layerIndex)
        {
            var config = GetConfig(layerIndex);
            if (config == null) return 0;
            int reduction = PrestigeUpgradeManager.Instance != null ? Mathf.RoundToInt(PrestigeUpgradeManager.Instance.LayerSizeReduction) : 0;
            return Mathf.Max(MinLayerHeight, config.LayerHeight - reduction);
        }

        // Depth (in blocks) at which this layer starts - inverse of GetLayerIndexAtDepth.
        public int GetLayerOffset(int layerIndex)
        {
            var yOffset = 0;
            for (int i = 0; i < layerIndex; i++)
            {
                yOffset += GetEffectiveLayerHeight(i);
            }
            return yOffset;
        }

        public int GetLayerIndexAtDepth(int depthInBlocks)
        {
            var totalLayerHeight = 0;
            for (int i = 0; i < LayerConfigs.Count; i++)
            {
                var layerHeight = GetEffectiveLayerHeight(i);
                if (depthInBlocks < totalLayerHeight + layerHeight)
                {
                    return i;
                }
                totalLayerHeight += layerHeight;
            }

            // if we are deeper than the last configured layer, use the last layer config for all deeper layers. This is a design choice to allow for infinite depth with the last layer's configuration.
            if (depthInBlocks >= totalLayerHeight)
            {
                var depthBeyondLastLayer = depthInBlocks - totalLayerHeight;
                var lastLayer = LayerConfigs[LayerConfigs.Count - 1];
                var lastLayerHeight = GetEffectiveLayerHeight(lastLayer.LayerIndex);
                var actualLayer = (depthBeyondLastLayer / lastLayerHeight) + lastLayer.LayerIndex + 1;
                return actualLayer;
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

        public int GetLayerIndexAtWorldY(float worldY, float cellSize)
        {
            var layerIndex = GetLayerIndexAtDepth(GetDepthInBlocksAtWorldY(worldY, cellSize));
            //Debug.Log($"GetLayerIndexAtWorldY: worldY={(int)worldY}, cellSize={cellSize.ToString("F1")}, layerIndex={layerIndex}");
            return layerIndex;
        }

        public void Validate()
        {
            if (LayerConfigs == null || LayerConfigs.Count == 0)
            {
                Debug.LogError("LayerConfigProvider has no LayerConfigs assigned.");
                return;
            }
            foreach (var layer in LayerConfigs)
            {
                if (layer == null)
                {
                    Debug.LogError("LayerConfigProvider contains a null LayerConfig.");
                    continue;
                }
                if (layer.LayerHeight <= 0)
                {
                    Debug.LogError($"LayerConfig '{layer.name}' has an invalid LayerHeight.");
                }
                if (layer.OreTable == null || layer.OreTable.Count == 0)
                {
                    Debug.LogError($"LayerConfig '{layer.name}' has no OreTable entries.");
                }
            }
        }
    }
}
