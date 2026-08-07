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
        [SerializeField] private BlockTypeDatabase blockTypes;

        public int LayerIndex { get; private set; }

        private ChunkData chunk;

        public void Bind(ChunkData chunkData, int layerIndex)
        {
            chunk = chunkData;
            LayerIndex = layerIndex;
            RepaintAll();
        }

        public void RepaintAll()
        {
            int w = chunk.Width;
            int h = chunk.Height;
            int count = w * h;

            var terrainPositions = new Vector3Int[count];
            var terrainTiles = new TileBase[count];
            var fogPositions = new Vector3Int[count];
            var fogTiles = new TileBase[count];

            int n = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var cell = chunk.Cells[chunk.Index(x, y)];
                    var pos = new Vector3Int(x, -y, 0);

                    terrainPositions[n] = pos;
                    terrainTiles[n] = cell.Mined ? null : ResolveTile(cell.BlockTypeId);

                    fogPositions[n] = pos;
                    fogTiles[n] = cell.Revealed ? null : fogTile;

                    n++;
                }
            }

            terrainTilemap.SetTiles(terrainPositions, terrainTiles);
            fogTilemap.SetTiles(fogPositions, fogTiles);
        }

        public void RepaintCells(IReadOnlyList<Vector2Int> localCoords)
        {
            int count = localCoords.Count;
            var terrainPositions = new Vector3Int[count];
            var terrainTiles = new TileBase[count];
            var fogPositions = new Vector3Int[count];
            var fogTiles = new TileBase[count];

            for (int i = 0; i < count; i++)
            {
                int x = localCoords[i].x;
                int y = localCoords[i].y;
                var cell = chunk.Cells[chunk.Index(x, y)];
                var pos = new Vector3Int(x, -y, 0);

                terrainPositions[i] = pos;
                terrainTiles[i] = cell.Mined ? null : ResolveTile(cell.BlockTypeId);

                fogPositions[i] = pos;
                fogTiles[i] = cell.Revealed ? null : fogTile;
            }

            terrainTilemap.SetTiles(terrainPositions, terrainTiles);
            fogTilemap.SetTiles(fogPositions, fogTiles);
        }

        private TileBase ResolveTile(byte blockTypeId)
        {
            var blockType = blockTypes != null ? blockTypes.Get(blockTypeId) : null;
            return blockType != null ? blockType.Tile : null;
        }
    }
}
