using System.Collections.Generic;
using Economy;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MapGeneration
{
    // One pooled instance per resident chunk: a terrain Tilemap (mined cells cleared) plus a
    // fog overlay Tilemap (revealed cells cleared). Repaints are batched via SetTiles so a
    // TilemapCollider2D/CompositeCollider2D on the terrain object doesn't refresh per-cell.
    public class ChunkTilemapView : MonoBehaviour
    {
        [SerializeField] private Tilemap terrainTilemap;
        [SerializeField] private Tilemap fogTilemap;
        [SerializeField] private TileBase fogTile;
        [SerializeField] private Tilemap backgroundTilemap;
        [SerializeField] private TileBase backgroundTile;
        // Sits in mined cells in place of null; a SolidNeighborRuleTile that renders fully
        // transparent by default and draws debris bleeding in from whichever side(s) still have
        // a solid neighbor, regardless of that neighbor's block type.
        [SerializeField] private TileBase edgeBleedTile;
        private BlockTypeDatabase blockTypes => GameManager.BlockTypeDatabase;
        [SerializeField] private bool fogDisabled;

        // Background tint: brown at the surface fading to purple by backgroundGradientLayers,
        // then flat purple for every layer beyond that.
        [SerializeField] private Color surfaceBackgroundColor = new(0.45f, 0.32f, 0.2f);
        [SerializeField] private Color deepBackgroundColor = new(0.25f, 0.1f, 0.35f);
        [SerializeField] private int backgroundGradientLayers = 10;

#if UNITY_EDITOR
        // Editor-only cell debug overlay (world position + cell index), drawn via Gizmos/Handles
        // so it never renders and never gets compiled into production/standalone builds.
        [SerializeField] private bool showDebugCellLabels;
        [SerializeField] private Color debugCellLabelColor = Color.yellow;
#endif

        // Layer 0 only: fog fades in from clear at the surface (row 0) to full opacity by this
        // row, so the mine entrance doesn't open into a hard fog wall.
        [SerializeField] private int surfaceFogGradientRows = 1;
        [SerializeField] private float defaultAlpha = 1;

        // Unrevealed cells within this many cells of any revealed cell fade in from clear
        // (adjacent to revealed) to full opacity (at/beyond this radius), so the fog edge
        // reads as a soft glow around explored ground instead of a hard boundary.
        [SerializeField] private int revealGradientRadius = 3;

        // GameDesignDoc "Lantern capstones > hazard sense: highlights hazard blocks".
        [SerializeField] private Color hazardSenseTint = new(1f, 0.4f, 0.4f);

        public int LayerIndex { get; private set; }

        private ChunkData chunk;

        public void Bind(ChunkData chunkData, int layerIndex)
        {
            chunk = chunkData;
            LayerIndex = layerIndex;
            RepaintAll();
            PaintBackground();

            if (fogDisabled)
            {
                fogTilemap.gameObject.SetActive(false);
            }
        }

        // Background is a flat, per-layer tint rather than per-cell data, so it's filled once
        // on bind rather than touched by RepaintCells - every cell gets the same tile, and the
        // whole tilemap's color is set once instead of per-tile.
        private void PaintBackground()
        {
            if (backgroundTilemap == null) return;

            int w = chunk.Width;
            int h = chunk.Height;
            int count = w * h;

            var positions = new Vector3Int[count];
            var tiles = new TileBase[count];
            int n = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    positions[n] = new Vector3Int(x, -y, 0);
                    tiles[n] = backgroundTile;
                    n++;
                }
            }
            backgroundTilemap.SetTiles(positions, tiles);

            float t = backgroundGradientLayers > 0 ? Mathf.Clamp01(LayerIndex / (float)backgroundGradientLayers) : 1f;
            backgroundTilemap.color = Color.Lerp(surfaceBackgroundColor, deepBackgroundColor, t);
        }

        public void RepaintAll()
        {
            int w = chunk.Width;
            int h = chunk.Height;
            int count = w * h;

            var terrainChanges = new TileChangeData[count];
            var fogChanges = new TileChangeData[count];

            int n = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var cell = chunk.Cells[chunk.Index(x, y)];
                    var pos = new Vector3Int(x, -y, 0);

                    terrainChanges[n] = BuildTerrainChange(pos, cell);
                    fogChanges[n] = BuildFogChange(pos, x, y, cell.Revealed);

                    n++;
                }
            }

            terrainTilemap.SetTiles(terrainChanges, true);
            fogTilemap.SetTiles(fogChanges, true);
        }

        public void RepaintCells(IReadOnlyList<Vector2Int> localCoords)
        {
            var expandedCoords = ExpandForFogGradient(localCoords);
            int count = expandedCoords.Count;
            var terrainChanges = new TileChangeData[count];
            var fogChanges = new TileChangeData[count];

            for (int i = 0; i < count; i++)
            {
                int x = expandedCoords[i].x;
                int y = expandedCoords[i].y;
                var cell = chunk.Cells[chunk.Index(x, y)];
                var pos = new Vector3Int(x, -y, 0);

                terrainChanges[i] = BuildTerrainChange(pos, cell);
                fogChanges[i] = BuildFogChange(pos, x, y, cell.Revealed);
            }

            terrainTilemap.SetTiles(terrainChanges, true);
            fogTilemap.SetTiles(fogChanges, true);
        }

        // Tints a revealed Hazard-category cell once Movement_HazardSense is unlocked, otherwise
        // paints the block's tile at full white (no tint).
        private TileChangeData BuildTerrainChange(Vector3Int pos, CellData cell)
        {
            if (cell.Mined) return new TileChangeData(pos, edgeBleedTile, Color.white, Matrix4x4.identity);

            var blockType = blockTypes != null ? blockTypes.Get(cell.BlockTypeId) : null;
            var tile = blockType != null ? blockType.Tile : null;

            bool highlightHazard = cell.Revealed && blockType != null && blockType.Category == BlockCategory.Hazard
                && UpgradeManager.Instance != null && UpgradeManager.Instance.HazardSenseUnlocked;

            return new TileChangeData(pos, tile, highlightHazard ? hazardSenseTint : Color.white, Matrix4x4.identity);
        }

        // A cell's reveal-distance fade (see BuildFogChange) depends on its neighbors' Revealed
        // state, so a partial repaint has to also touch every still-fogged cell within
        // revealGradientRadius of a newly-revealed/mined cell, not just the cells whose own flags
        // changed - otherwise the gradient around the new reveal never gets drawn until the next
        // full RepaintAll.
        private List<Vector2Int> ExpandForFogGradient(IReadOnlyList<Vector2Int> localCoords)
        {
            if (revealGradientRadius <= 0) return new List<Vector2Int>(localCoords);

            int w = chunk.Width;
            int h = chunk.Height;
            var seen = new HashSet<Vector2Int>();
            var result = new List<Vector2Int>();

            foreach (var coord in localCoords)
            {
                for (int dy = -revealGradientRadius; dy <= revealGradientRadius; dy++)
                {
                    int y = coord.y + dy;
                    if (y < 0 || y >= h) continue;

                    for (int dx = -revealGradientRadius; dx <= revealGradientRadius; dx++)
                    {
                        int x = coord.x + dx;
                        if (x < 0 || x >= w) continue;

                        var pos = new Vector2Int(x, y);
                        if (seen.Add(pos)) result.Add(pos);
                    }
                }
            }

            return result;
        }

        // Revealed cells clear the fog tile entirely. Otherwise alpha is the lowest (most see-
        // through) of two independent fades, each fully opaque by default:
        //  - layer 0's top rows fade in from 0 (surface) to 1 (by surfaceFogGradientRows), so the
        //    mine entrance doesn't open into a hard fog wall;
        //  - any cell within revealGradientRadius of a revealed cell fades in from 0 (adjacent)
        //    to 1 (at the radius), so the edge of explored ground reads as a soft glow.
        private TileChangeData BuildFogChange(Vector3Int pos, int x, int y, bool revealed)
        {
            if (revealed) return new TileChangeData(pos, null, Color.white, Matrix4x4.identity);

            float alpha = defaultAlpha;

            if (LayerIndex == 0 && y < surfaceFogGradientRows)
            {
                var surfacePercentage = Mathf.Clamp01(y / (float)(surfaceFogGradientRows - 1));
                alpha = Mathf.Min(alpha, surfacePercentage);
            }

            if (revealGradientRadius > 0)
            {
                float distance = DistanceToNearestRevealed(x, y, revealGradientRadius);
                if (distance >= 0f)
                {
                    var revealPercentage = Mathf.Clamp01(distance / revealGradientRadius);
                    alpha = Mathf.Min(alpha, revealPercentage);
                }
            }

            return new TileChangeData(pos, fogTile, new Color(1f, 1f, 1f, alpha), Matrix4x4.identity);
        }

        // Euclidean distance (in cells) to the closest Revealed cell within maxRadius, searched
        // as a square window and chunk-bounds clamped; -1 if none is that close.
        private float DistanceToNearestRevealed(int x, int y, int maxRadius)
        {
            int w = chunk.Width;
            int h = chunk.Height;
            float nearestSq = float.MaxValue;

            for (int dy = -maxRadius; dy <= maxRadius; dy++)
            {
                int ny = y + dy;
                if (ny < 0 || ny >= h) continue;

                for (int dx = -maxRadius; dx <= maxRadius; dx++)
                {
                    int nx = x + dx;
                    if (nx < 0 || nx >= w) continue;

                    int distSq = dx * dx + dy * dy;
                    if (distSq >= nearestSq) continue;
                    if (!chunk.Cells[chunk.Index(nx, ny)].Revealed) continue;

                    nearestSq = distSq;
                }
            }

            return nearestSq <= maxRadius * (float)maxRadius ? Mathf.Sqrt(nearestSq) : -1f;
        }

#if UNITY_EDITOR
        // Draws one label per cell showing its (x,y) chunk index and world-space center. Only
        // ever invoked by the Editor (Scene/Game view "Gizmos" toggle), so this - and the fields
        // above it - compile out of production builds entirely via the UNITY_EDITOR guard.
        private void OnDrawGizmos()
        {
            if (!showDebugCellLabels || chunk == null || terrainTilemap == null) return;

            UnityEditor.Handles.color = debugCellLabelColor;
            var labelStyle = new GUIStyle { normal = { textColor = debugCellLabelColor }, fontSize = 8, alignment = TextAnchor.MiddleCenter };

            for (int y = 0; y < chunk.Height; y++)
            {
                for (int x = 0; x < chunk.Width; x++)
                {
                    var cellPos = new Vector3Int(x, -y, 0);
                    Vector3 worldPos = terrainTilemap.GetCellCenterWorld(cellPos);
                    UnityEditor.Handles.Label(worldPos, $"[{x},{y}]\n{worldPos.ToFormattedString()}", labelStyle);
                }
            }
        }
#endif
    }
}
