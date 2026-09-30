using System.Collections.Generic;
using MapGeneration;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Atmosphere
{
    // Hints at valuable ore veins still hidden in the fog near explored ground - rewarding the
    // player for digging toward them. Each connected vein (same ore, 4-neighbour) gets an
    // occasional glint - a thin white diagonal line (Custom/OreShine) sweeping across its
    // still-hidden cells from bottom-left to top-right.
    // A vein only glints once its nearest hidden cell is within AtmosphereConfig.OreGlowHintRadius
    // of revealed ground (fading in as the player closes in), and goes dark once every cell is
    // revealed - by then the ore sprite speaks for itself.
    // "Valuable" is relative to the layer: anything worth at least as much as the layer's
    // AtmosphereConfig.GlowingOresPerLayer-th most valuable ore glints (so deeper, rarer ores
    // swapped in by the ore-tier perk glint too), tinted by the ore's minimap colour and brighter
    // the rarer it is. Added and driven by ChunkTilemapView (Rebuild on bind, RefreshCells on repaint).
    public class OreGlowLayer : MonoBehaviour
    {
        private class Vein
        {
            public readonly List<int> HiddenCells = new();
            public RectInt Bounds;
            public Color Color;
            public float Visibility;
            public float TargetVisibility;
            // One shine quad per hidden cell while a glint is sweeping, empty otherwise.
            public readonly List<SpriteRenderer> Glints = new();
            public float GlintTimer;
            // World x + y of the shine line; it runs from GlintSweep up to GlintSweepEnd.
            public float GlintSweep;
            public float GlintSweepEnd;
        }

        private static AtmosphereConfig config => GameManager.AtmosphereConfig;
        private static BlockTypeDatabase blockTypes => GameManager.BlockTypeDatabase;
        private static readonly int SweepId = Shader.PropertyToID("_Sweep");
        private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");

        private readonly List<Vein> veins = new();
        private readonly Dictionary<int, Vein> veinByCell = new();
        private readonly Stack<SpriteRenderer> glintPool = new();
        private Transform glintRoot;
        private ChunkData chunk;
        private Tilemap tilemap;
        private MaterialPropertyBlock glintProperties;

        public void Rebuild(ChunkData chunkData, int layerIndex, Tilemap terrainTilemap)
        {
            chunk = chunkData;
            tilemap = terrainTilemap;
            glintProperties ??= new MaterialPropertyBlock();
            if (glintRoot == null)
            {
                glintRoot = new GameObject("OreGlints").transform;
                glintRoot.SetParent(transform, false);
            }

            foreach (var vein in veins) ReleaseVein(vein);
            veins.Clear();
            veinByCell.Clear();

            var layerConfig = GameManager.LayerConfigProvider.GetConfig(layerIndex);
            if (layerConfig == null) return;
            float threshold = GetValueThreshold(layerConfig);
            float topValue = GetTopValue(layerConfig);

            var visited = new HashSet<int>();
            for (int y = 0; y < chunk.Height; y++)
            {
                for (int x = 0; x < chunk.Width; x++)
                {
                    int index = chunk.Index(x, y);
                    if (visited.Contains(index) || !IsGlowingOre(chunk.Cells[index], threshold)) continue;

                    var block = blockTypes.Get(chunk.Cells[index].BlockTypeId);
                    var vein = FloodFillVein(x, y, chunk.Cells[index].BlockTypeId, threshold, visited);
                    if (vein.HiddenCells.Count == 0) continue;

                    // Rarer = brighter: the threshold ore glints at 60%, the layer's best (or better) at 100%.
                    float strength = topValue > threshold ? Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(threshold, topValue, block.Value)) : 1f;
                    // Full-brightness version of the minimap hue - an additive tint from a dark
                    // colour (coal's grey) would barely register.
                    var color = block.MinimapColor;
                    float brightest = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                    if (brightest > 0f) color = new Color(color.r / brightest, color.g / brightest, color.b / brightest);
                    color.a = strength;

                    vein.Color = color;
                    vein.GlintTimer = Random.Range(0f, config.OreGlintIntervalMax);
                    vein.TargetVisibility = ComputeVisibility(vein);
                    // Snap on bind so re-entering a layer doesn't fade every vein in from nothing.
                    vein.Visibility = vein.TargetVisibility;
                    veins.Add(vein);
                }
            }
        }

        // Newly revealed/mined cells drop out of their vein (killing it once none are left hidden),
        // and any vein within hint range of a change re-checks how close explored ground now is.
        public void RefreshCells(IReadOnlyList<Vector2Int> localCoords)
        {
            if (chunk == null || localCoords.Count == 0) return;

            var changedVeins = new HashSet<Vein>();
            var changedArea = new RectInt(localCoords[0], Vector2Int.zero);
            foreach (var coord in localCoords)
            {
                changedArea.SetMinMax(Vector2Int.Min(changedArea.min, coord), Vector2Int.Max(changedArea.max, coord));

                int index = chunk.Index(coord.x, coord.y);
                if (!veinByCell.TryGetValue(index, out var vein) || IsHidden(chunk.Cells[index])) continue;

                vein.HiddenCells.Remove(index);
                veinByCell.Remove(index);
                changedVeins.Add(vein);
                // Its shine quads were laid out over the old cells - cut the sweep short.
                StopGlint(vein);
            }

            int radius = config.OreGlowHintRadius;
            var reach = new RectInt(changedArea.xMin - radius, changedArea.yMin - radius, changedArea.width + radius * 2 + 1, changedArea.height + radius * 2 + 1);
            for (int i = veins.Count - 1; i >= 0; i--)
            {
                var vein = veins[i];
                if (vein.HiddenCells.Count == 0)
                {
                    ReleaseVein(vein);
                    veins.RemoveAt(i);
                    continue;
                }

                if (changedVeins.Contains(vein) || reach.Overlaps(vein.Bounds)) vein.TargetVisibility = ComputeVisibility(vein);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (var vein in veins)
            {
                vein.Visibility = Mathf.MoveTowards(vein.Visibility, vein.TargetVisibility, dt * config.OreGlowFadeSpeed);
                UpdateGlint(vein, vein.Visibility > 0.001f, dt);
            }
        }

        // Idle for a random interval, then sweep a shine line across every hidden cell from the
        // vein's bottom-left to its top-right at OreGlintSpeed.
        private void UpdateGlint(Vein vein, bool visible, float dt)
        {
            if (vein.Glints.Count == 0)
            {
                vein.GlintTimer -= dt;
                if (vein.GlintTimer > 0f || !visible) return;
                StartGlint(vein);
            }

            float cellSize = tilemap.cellSize.x;
            // The line moves perpendicular to itself, so x + y advances sqrt(2) per unit travelled.
            vein.GlintSweep += config.OreGlintSpeed * cellSize * 1.41421356f * dt;
            if (vein.GlintSweep >= vein.GlintSweepEnd)
            {
                StopGlint(vein);
                return;
            }

            // Mostly white with a hint of the ore's hue, so it reads as a shine, not more glow.
            var color = Color.Lerp(vein.Color, Color.white, 0.6f);
            color.a = vein.Color.a * config.OreGlintAlpha * vein.Visibility;
            glintProperties.SetFloat(SweepId, vein.GlintSweep);
            glintProperties.SetFloat(LineWidthId, config.OreGlintLineWidth * cellSize);
            foreach (var glint in vein.Glints)
            {
                glint.color = color;
                glint.SetPropertyBlock(glintProperties);
            }
        }

        // Lays a shine quad over each hidden cell and starts the line just outside the vein's
        // bottom-left-most cell, ending just past its top-right-most.
        private void StartGlint(Vein vein)
        {
            float cellSize = tilemap.cellSize.x;
            float minDiagonal = float.MaxValue;
            float maxDiagonal = float.MinValue;
            foreach (int cell in vein.HiddenCells)
            {
                var center = CellCenter(cell);
                var glint = AcquireGlint();
                glint.transform.position = center;
                glint.transform.localScale = Vector3.one * cellSize;
                glint.color = Color.clear;
                vein.Glints.Add(glint);

                minDiagonal = Mathf.Min(minDiagonal, center.x + center.y);
                maxDiagonal = Mathf.Max(maxDiagonal, center.x + center.y);
            }

            vein.GlintSweep = minDiagonal - cellSize * 2f;
            vein.GlintSweepEnd = maxDiagonal + cellSize * 2f;
        }

        private void StopGlint(Vein vein)
        {
            if (vein.Glints.Count == 0) return;

            foreach (var glint in vein.Glints) ReleaseGlint(glint);
            vein.Glints.Clear();
            vein.GlintTimer = Random.Range(config.OreGlintIntervalMin, config.OreGlintIntervalMax);
        }

        // 4-neighbour flood fill over unmined cells of the same glowing ore. Revealed cells still
        // join (so the vein's shape is stable) but only hidden ones are tracked.
        private Vein FloodFillVein(int startX, int startY, byte blockTypeId, float threshold, HashSet<int> visited)
        {
            var vein = new Vein();
            var min = new Vector2Int(startX, startY);
            var max = min;
            var stack = new Stack<Vector2Int>();
            stack.Push(min);
            visited.Add(chunk.Index(startX, startY));

            while (stack.Count > 0)
            {
                var p = stack.Pop();
                int index = chunk.Index(p.x, p.y);
                if (IsHidden(chunk.Cells[index]))
                {
                    vein.HiddenCells.Add(index);
                    veinByCell[index] = vein;
                    min = Vector2Int.Min(min, p);
                    max = Vector2Int.Max(max, p);
                }

                TryVisit(p.x + 1, p.y);
                TryVisit(p.x - 1, p.y);
                TryVisit(p.x, p.y + 1);
                TryVisit(p.x, p.y - 1);
            }

            vein.Bounds = new RectInt(min, max - min + Vector2Int.one);
            return vein;

            void TryVisit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= chunk.Width || y >= chunk.Height) return;
                int index = chunk.Index(x, y);
                var cell = chunk.Cells[index];
                if (cell.BlockTypeId != blockTypeId || visited.Contains(index) || !IsGlowingOre(cell, threshold)) return;
                visited.Add(index);
                stack.Push(new Vector2Int(x, y));
            }
        }

        // 1 when a hidden cell touches revealed ground, fading to 0 at OreGlowHintRadius and beyond.
        private float ComputeVisibility(Vein vein)
        {
            int radius = config.OreGlowHintRadius;
            float nearestSq = float.MaxValue;
            foreach (int cell in vein.HiddenCells)
            {
                int x = cell % chunk.Width;
                int y = cell / chunk.Width;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= chunk.Height) continue;

                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= chunk.Width) continue;

                        int distSq = dx * dx + dy * dy;
                        if (distSq >= nearestSq || !chunk.Cells[chunk.Index(nx, ny)].Revealed) continue;
                        nearestSq = distSq;
                    }
                }
            }

            if (nearestSq > radius * (float)radius) return 0f;
            return 1f - Mathf.InverseLerp(1f, radius, Mathf.Sqrt(nearestSq));
        }

        private bool IsGlowingOre(CellData cell, float threshold)
        {
            if (cell.Mined) return false;
            var block = blockTypes.Get(cell.BlockTypeId);
            return block != null && block.Category == BlockCategory.Ore && block.Value > 0f && block.Value >= threshold;
        }

        private static bool IsHidden(CellData cell) => !cell.Mined && !cell.Revealed;

        private Vector3 CellCenter(int cellIndex) => tilemap.GetCellCenterWorld(new Vector3Int(cellIndex % chunk.Width, -(cellIndex / chunk.Width), 0));

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

        private SpriteRenderer AcquireGlint()
        {
            if (glintPool.Count > 0)
            {
                var pooled = glintPool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            var renderer = GlowSprites.CreateGlow(glintRoot, Color.clear, 1f);
            renderer.sprite = GlowSprites.Square;
            renderer.sharedMaterial = GlowSprites.OreShineMaterial;
            renderer.gameObject.name = "OreGlint";
            return renderer;
        }

        private void ReleaseVein(Vein vein)
        {
            foreach (int cell in vein.HiddenCells) veinByCell.Remove(cell);
            foreach (var glint in vein.Glints) ReleaseGlint(glint);
            vein.Glints.Clear();
        }

        private void ReleaseGlint(SpriteRenderer glint)
        {
            glint.gameObject.SetActive(false);
            glintPool.Push(glint);
        }
    }
}
