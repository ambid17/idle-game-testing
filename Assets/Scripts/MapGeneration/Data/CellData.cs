using System;

namespace MapGeneration
{
    [Flags]
    public enum CellFlags : byte
    {
        None = 0,
        Mined = 1 << 0,
        Revealed = 1 << 1,
        Artifact = 1 << 2,
    }

    // Packed per-cell state: 1 byte block type id + 1 byte flag bits.
    public struct CellData
    {
        public byte BlockTypeId;
        public CellFlags Flags;

        public bool Mined
        {
            get => (Flags & CellFlags.Mined) != 0;
            set => SetFlag(CellFlags.Mined, value);
        }

        public bool Revealed
        {
            get => (Flags & CellFlags.Revealed) != 0;
            set => SetFlag(CellFlags.Revealed, value);
        }

        public bool IsArtifact
        {
            get => (Flags & CellFlags.Artifact) != 0;
            set => SetFlag(CellFlags.Artifact, value);
        }

        private void SetFlag(CellFlags bit, bool on) => Flags = on ? (Flags | bit) : (Flags & ~bit);
    }
}
