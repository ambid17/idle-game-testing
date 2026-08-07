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
    }
}
