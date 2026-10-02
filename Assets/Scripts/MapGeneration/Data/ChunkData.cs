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
        public int MinedCount;
        public bool IsFullyGenerated;

        // Every pre-carved empty pocket (ChunkGenerator.CarveEmptyPockets), each as its own cell
        // list with the seed cell first. Rebuilt on every generation rather than persisted - restored
        // chunks regenerate from the seed before their mined bits are applied - so Critters.CritterSpawner
        // can key spawns off a pocket's seed cell and get the same answer every load.
        public readonly List<List<Vector2Int>> EmptyPockets = new();

        // The Critter Shop's cave (ChunkGenerator.CarveShopCave), in chunk-local cells - only ever set
        // on the single layer ChunkGenerator.GetShopLayerIndex picks for this seed. The row directly
        // below it (yMax) is the unmineable GrassyDirt floor the shop stands on.
        public RectInt? ShopCave;

        // Every StructureDefinition stamped onto this layer (StructureStampFeature) and where it
        // landed, in chunk-local cells. Rebuilt on every generation like EmptyPockets; only read by
        // UI.DevPanelSetPiecesTab to find rooms.
        public readonly List<(StructureDefinition structure, RectInt rect)> StampedStructures = new();

        public int TotalCells => Width * Height;
        public float CompletionRatio => TotalCells == 0 ? 0f : (float)MinedCount / TotalCells;

        public int Index(int x, int y) => y * Width + x;
    }
}
