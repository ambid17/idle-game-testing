using UnityEngine;

namespace MapGeneration
{
    // Builds/restores a MapSaveData from a MineWorld. Only chunks the world has actually
    // generated (i.e. visited) are included, per the "only visited chunks need to exist in
    // save data" design goal.
    public static class MapPersistenceService
    {
        public static MapSaveData BuildSaveData(MineWorld world)
        {
            var save = new MapSaveData
            {
                Seed = world.Seed,
                GridWidth = world.GridWidth,
            };

            foreach (var chunk in world.GetLoadedChunks())
            {
                save.Chunks.Add(ChunkSerializer.ToSaveData(chunk));
            }

            return save;
        }

        public static MineWorld Restore(MapSaveData save)
        {
            var world = new MineWorld(save.Seed, save.GridWidth);

            foreach (var chunkSave in save.Chunks)
            {
                var chunk = world.GetOrGenerateChunk(chunkSave.LayerIndex);
                ChunkSerializer.ApplyToChunk(chunk, chunkSave);
            }

            return world;
        }

        public static string ToJson(MapSaveData save) => JsonUtility.ToJson(save);

        public static MapSaveData FromJson(string json) => JsonUtility.FromJson<MapSaveData>(json);
    }
}
