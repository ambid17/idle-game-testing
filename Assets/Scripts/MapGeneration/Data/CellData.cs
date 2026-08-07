namespace MapGeneration
{
    // Packed per-cell state: 1 byte block type id + 1 byte flag bits.
    public struct CellData
    {
        private const byte MinedBit = 1 << 0;
        private const byte RevealedBit = 1 << 1;
        private const byte ArtifactBit = 1 << 2;

        public byte BlockTypeId;
        private byte flags;

        public bool Mined
        {
            get => (flags & MinedBit) != 0;
            set => flags = SetBit(flags, MinedBit, value);
        }

        public bool Revealed
        {
            get => (flags & RevealedBit) != 0;
            set => flags = SetBit(flags, RevealedBit, value);
        }

        public bool IsArtifact
        {
            get => (flags & ArtifactBit) != 0;
            set => flags = SetBit(flags, ArtifactBit, value);
        }

        private static byte SetBit(byte b, byte bit, bool on) => on ? (byte)(b | bit) : (byte)(b & ~bit);
    }
}
