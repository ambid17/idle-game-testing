using System.Collections.Generic;
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
        private BlockTypeDatabase blockTypes => GameManager.BlockTypeDatabase;
        [SerializeField] private bool fogDisabled;

#if UNITY_EDITOR
        // Editor-only cell debug overlay (world position + cell index), drawn via Gizmos/Handles
        // so it never renders and never gets compiled into production/standalone builds.
        [SerializeField] private bool showDebugCellLabels;
        [SerializeField] private Color debugCellLabelColor = Color.yellow;
#endif

        // Layer 0 only: fog fades in from clear at the surface (row 0) to full opacity by this
        // row, so the mine entrance doesn't open into a hard fog wall.
        [SerializeField] private int surfaceFogGradientRows = 10;

        public int LayerIndex { get; private set; }

        private ChunkData chunk;

        public void Bind(ChunkData chunkData, int layerIndex)
        {
            chunk = chunkData;
            LayerIndex = layerIndex;
            RepaintAll();

            if (fogDisabled)
            {
                fogTilemap.gameObject.SetActive(false);
            }
        }

        public void RepaintAll()
        {
            int w = chunk.Width;
            int h = chunk.Height;
            int count = w * h;

            var terrainPositions = new Vector3Int[count];
            var terrainTiles = new TileBase[count];
            var fogChanges = new TileChangeData[count];

            int n = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var cell = chunk.Cells[chunk.Index(x, y)];
                    var pos = new Vector3Int(x, -y, 0);

                    terrainPositions[n] = pos;
                    terrainTiles[n] = cell.Mined ? null : ResolveTile(cell.BlockTypeId);

                    fogChanges[n] = BuildFogChange(pos, y, cell.Revealed);

                    n++;
                }
            }

            terrainTilemap.SetTiles(terrainPositions, terrainTiles);
            fogTilemap.SetTiles(fogChanges, true);
        }

        public void RepaintCells(IReadOnlyList<Vector2Int> localCoords)
        {
            int count = localCoords.Count;
            var terrainPositions = new Vector3Int[count];
            var terrainTiles = new TileBase[count];
            var fogChanges = new TileChangeData[count];

            for (int i = 0; i < count; i++)
            {
                int x = localCoords[i].x;
                int y = localCoords[i].y;
                var cell = chunk.Cells[chunk.Index(x, y)];
                var pos = new Vector3Int(x, -y, 0);

                terrainPositions[i] = pos;
                terrainTiles[i] = cell.Mined ? null : ResolveTile(cell.BlockTypeId);

                fogChanges[i] = BuildFogChange(pos, y, cell.Revealed);
            }

            terrainTilemap.SetTiles(terrainPositions, terrainTiles);
            fogTilemap.SetTiles(fogChanges, true);
        }

        // Revealed cells clear the fog tile entirely. Otherwise, layer 0's top rows fade the
        // fog tile's alpha in from 0 (surface) to 1 (by surfaceFogGradientRows) instead of
        // snapping straight to full opacity; every other layer/row stays fully opaque.
        private TileChangeData BuildFogChange(Vector3Int pos, int y, bool revealed)
        {
            if (revealed) return new TileChangeData(pos, null, Color.white, Matrix4x4.identity);

            var defaultAlpha = 0.97f;
            var gradientPercentage = Mathf.Clamp01(y / (float)(surfaceFogGradientRows - 1));
            float alpha = LayerIndex == 0 && y < surfaceFogGradientRows
                ? Mathf.Min(defaultAlpha, gradientPercentage)
                : defaultAlpha;
            return new TileChangeData(pos, fogTile, new Color(1f, 1f, 1f, alpha), Matrix4x4.identity);
        }

        private TileBase ResolveTile(byte blockTypeId)
        {
            var blockType = blockTypes != null ? blockTypes.Get(blockTypeId) : null;
            return blockType != null ? blockType.Tile : null;
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
