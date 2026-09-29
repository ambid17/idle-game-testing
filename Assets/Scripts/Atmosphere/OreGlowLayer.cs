using System.Collections.Generic;
using MapGeneration;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Atmosphere
{
    // Soft pulsing glows over a chunk's valuable ore cells, sorted above the fog so a rich vein
    // shows faintly through unexplored dirt - rewarding the player for scanning the screen.
    // "Valuable" is relative to the layer: anything worth at least as much as the layer's
    // AtmosphereConfig.GlowingOresPerLayer-th most valuable ore glows (so deeper, rarer ores
    // swapped in by the ore-tier perk glow too), tinted by the ore's minimap colour and brighter
    // the rarer it is. Added and driven by ChunkTilemapView (Rebuild on bind, RefreshCells on repaint).
    public class OreGlowLayer : MonoBehaviour
    {
        private struct Glow
        {
            public int CellIndex;
            public SpriteRenderer Renderer;
            public Color Color;
            public float Phase;
        }

        private static AtmosphereConfig config => GameManager.AtmosphereConfig;
        private static BlockTypeDatabase blockTypes => GameManager.BlockTypeDatabase;

        private readonly List<Glow> glows = new();
        private readonly Dictionary<int, int> glowByCell = new();
        private readonly Stack<SpriteRenderer> pool = new();
        private Transform glowRoot;
        private ChunkData chunk;

        public void Rebuild(ChunkData chunkData, int layerIndex, Tilemap terrainTilemap)
        {
            chunk = chunkData;
            if (glowRoot == null)
            {
                glowRoot = new GameObject("OreGlows").transform;
                glowRoot.SetParent(transform, false);
            }

            foreach (var glow in glows) Release(glow.Renderer);
            glows.Clear();
            glowByCell.Clear();

            var layerConfig = GameManager.LayerConfigProvider.GetConfig(layerIndex);
            if (layerConfig == null) return;
            float threshold = GetValueThreshold(layerConfig);
            float topValue = GetTopValue(layerConfig);

            for (int y = 0; y < chunk.Height; y++)
            {
                for (int x = 0; x < chunk.Width; x++)
                {
                    int index = chunk.Index(x, y);
                    var cell = chunk.Cells[index];
                    if (cell.Mined) continue;

                    var block = blockTypes.Get(cell.BlockTypeId);
                    if (block == null || block.Category != BlockCategory.Ore || block.Value <= 0f || block.Value < threshold) continue;

                    // Rarer = brighter: the threshold ore glows at 60%, the layer's best (or better) at 100%.
                    float strength = topValue > threshold ? Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(threshold, topValue, block.Value)) : 1f;
                    // Full-brightness version of the minimap hue - additive glow from a dark
                    // colour (coal's grey) would barely register.
                    var color = block.MinimapColor;
                    float brightest = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                    if (brightest > 0f) color = new Color(color.r / brightest, color.g / brightest, color.b / brightest);
                    color.a = config.OreGlowAlpha * strength;

                    var renderer = Acquire();
                    renderer.transform.position = terrainTilemap.GetCellCenterWorld(new Vector3Int(x, -y, 0));
                    renderer.color = color;

                    glowByCell[index] = glows.Count;
                    glows.Add(new Glow { CellIndex = index, Renderer = renderer, Color = color, Phase = Random.value * Mathf.PI * 2f });
                }
            }
        }

        // Mined cells lose their glow. Only ever removes - mining never creates ore.
        public void RefreshCells(IReadOnlyList<Vector2Int> localCoords)
        {
            if (chunk == null) return;

            foreach (var coord in localCoords)
            {
                int index = chunk.Index(coord.x, coord.y);
                if (!chunk.Cells[index].Mined || !glowByCell.TryGetValue(index, out int glowIndex)) continue;
                RemoveAt(glowIndex);
            }
        }

        private void Update()
        {
            float t = Time.time * config.OreGlowPulseSpeed;
            foreach (var glow in glows)
            {
                float pulse = 0.7f + 0.3f * Mathf.Sin(t + glow.Phase);
                var color = glow.Color;
                color.a *= pulse;
                glow.Renderer.color = color;
            }
        }

        // Swap-remove so the list stays dense for Update.
        private void RemoveAt(int glowIndex)
        {
            var removed = glows[glowIndex];
            Release(removed.Renderer);
            glowByCell.Remove(removed.CellIndex);

            int last = glows.Count - 1;
            if (glowIndex != last)
            {
                glows[glowIndex] = glows[last];
                glowByCell[glows[glowIndex].CellIndex] = glowIndex;
            }
            glows.RemoveAt(last);
        }

        // Both must hold: among the layer's GlowingOresPerLayer most valuable ores, and well above
        // what a typical ore there is worth (weighted by spawn weight).
        private float GetValueThreshold(LayerConfig layerConfig)
        {
            var values = new List<float>();
            float weightedValue = 0f;
            float totalWeight = 0f;
            foreach (var entry in layerConfig.OreTable)
            {
                if (entry.BlockType == null || entry.BlockType.Category != BlockCategory.Ore || entry.BlockType.Value <= 0f) continue;
                values.Add(entry.BlockType.Value);
                weightedValue += entry.BlockType.Value * entry.Weight;
                totalWeight += entry.Weight;
            }
            if (values.Count == 0 || totalWeight <= 0f) return float.MaxValue;

            values.Sort((a, b) => b.CompareTo(a));
            float rankThreshold = values[Mathf.Min(config.GlowingOresPerLayer, values.Count) - 1];
            return Mathf.Max(rankThreshold, weightedValue / totalWeight * config.OreGlowValueMultiplier);
        }

        private static float GetTopValue(LayerConfig layerConfig)
        {
            float top = 0f;
            foreach (var entry in layerConfig.OreTable)
            {
                if (entry.BlockType != null && entry.BlockType.Category == BlockCategory.Ore) top = Mathf.Max(top, entry.BlockType.Value);
            }
            return top;
        }

        private SpriteRenderer Acquire()
        {
            if (pool.Count > 0)
            {
                var pooled = pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            var renderer = GlowSprites.CreateGlow(glowRoot, Color.clear, config.OreGlowSize);
            renderer.gameObject.name = "OreGlow";
            return renderer;
        }

        private void Release(SpriteRenderer renderer)
        {
            renderer.gameObject.SetActive(false);
            pool.Push(renderer);
        }
    }
}
