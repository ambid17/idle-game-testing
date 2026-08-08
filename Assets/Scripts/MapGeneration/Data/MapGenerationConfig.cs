using UnityEngine;

namespace MapGeneration
{
    // Centralized defaults for a *fresh* (non-restored) MineWorld and its streaming/rendering
    // setup. Restored worlds (MapPersistenceService.Restore) use the save file's own Seed/GridWidth
    // instead - MineWorld's runtime Seed/GridWidth fields remain the per-instance source of truth
    // once a world exists; this asset only supplies the initial values for a brand-new world.
    [CreateAssetMenu(fileName = "MapGenerationConfig", menuName = "Map Generation/Map Generation Config")]
    public class MapGenerationConfig : ScriptableObject
    {
        [SerializeField] private int seed = 12345;
        [SerializeField] private int gridWidth = 30;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private int windowRadius = 1;

        public int Seed => seed;
        public int GridWidth => gridWidth;
        public float CellSize => cellSize;
        public int WindowRadius => windowRadius;
    }
}
