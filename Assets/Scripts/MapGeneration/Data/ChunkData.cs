using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    // One layer (width x height) worth of generated cells - the streaming/persistence unit.
    public class ChunkData
    {
        public int LayerIndex;
        public int Width;
        public int Height;
        public CellData[] Cells;
        public List<Vector2Int> ArtifactCells = new();
        public int MinedCount;
        public bool IsFullyGenerated;

        public int TotalCells => Width * Height;
        public float CompletionRatio => TotalCells == 0 ? 0f : (float)MinedCount / TotalCells;

        public int Index(int x, int y) => y * Width + x;
    }
}
