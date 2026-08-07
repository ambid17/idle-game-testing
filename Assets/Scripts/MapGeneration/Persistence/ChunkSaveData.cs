using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    [Serializable]
    public class ChunkSaveData
    {
        public int LayerIndex;
        public int Width;
        public int Height;

        // Dense bit arrays (1 bit/cell) - a few hundred bytes per chunk even fully mined.
        public byte[] MinedBits;
        public byte[] RevealedBits;

        public List<Vector2Int> DiscoveredArtifactCells = new();
    }

    [Serializable]
    public class MapSaveData
    {
        public int Seed;
        public int GridWidth;
        public List<ChunkSaveData> Chunks = new();
    }
}
