using System;
using System.Collections.Generic;

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
    }

    [Serializable]
    public class MapSaveData
    {
        public int Seed;
        public int GridWidth;
        // Must be restored before any chunk regenerates (see MapPersistenceService.Restore). Older
        // saves deserialize this as an empty (inactive) state.
        public RunModifiers.RunModifierState RunModifier = new();
        public List<ChunkSaveData> Chunks = new();
    }
}
