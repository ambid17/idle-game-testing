namespace MapGeneration
{
    public static class ChunkSerializer
    {
        public static ChunkSaveData ToSaveData(ChunkData chunk)
        {
            var save = new ChunkSaveData
            {
                LayerIndex = chunk.LayerIndex,
                Width = chunk.Width,
                Height = chunk.Height,
                MinedBits = PackMined(chunk),
                RevealedBits = PackRevealed(chunk),
            };

            foreach (var pos in chunk.ArtifactCells)
            {
                var cell = chunk.Cells[chunk.Index(pos.x, pos.y)];
                if (cell.Mined || cell.Revealed)
                {
                    save.DiscoveredArtifactCells.Add(pos);
                }
            }

            return save;
        }

        public static void ApplyToChunk(ChunkData chunk, ChunkSaveData save)
        {
            UnpackMined(save.MinedBits, chunk);
            UnpackRevealed(save.RevealedBits, chunk);
        }

        private static byte[] PackMined(ChunkData chunk)
        {
            var bytes = new byte[(chunk.Cells.Length + 7) / 8];
            for (int i = 0; i < chunk.Cells.Length; i++)
            {
                if (chunk.Cells[i].Mined)
                {
                    bytes[i / 8] |= (byte)(1 << (i % 8));
                }
            }
            return bytes;
        }

        private static byte[] PackRevealed(ChunkData chunk)
        {
            var bytes = new byte[(chunk.Cells.Length + 7) / 8];
            for (int i = 0; i < chunk.Cells.Length; i++)
            {
                if (chunk.Cells[i].Revealed)
                {
                    bytes[i / 8] |= (byte)(1 << (i % 8));
                }
            }
            return bytes;
        }

        private static void UnpackMined(byte[] bits, ChunkData chunk)
        {
            if (bits == null) return;

            int minedCount = 0;
            for (int i = 0; i < chunk.Cells.Length; i++)
            {
                bool mined = (bits[i / 8] & (1 << (i % 8))) != 0;
                if (!mined) continue;

                var cell = chunk.Cells[i];
                cell.Mined = true;
                chunk.Cells[i] = cell;
                minedCount++;
            }
            chunk.MinedCount = minedCount;
        }

        private static void UnpackRevealed(byte[] bits, ChunkData chunk)
        {
            if (bits == null) return;

            for (int i = 0; i < chunk.Cells.Length; i++)
            {
                bool revealed = (bits[i / 8] & (1 << (i % 8))) != 0;
                if (!revealed) continue;

                var cell = chunk.Cells[i];
                cell.Revealed = true;
                chunk.Cells[i] = cell;
            }
        }
    }
}
