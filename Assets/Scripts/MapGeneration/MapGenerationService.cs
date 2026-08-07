using Economy;
using Events;
using UnityEngine;

namespace MapGeneration
{
    // Scene-level entry point wiring MineWorld + ChunkStreamingManager together. MineCell() is
    // the single mining codepath meant to be called by both the player and idle miners (the
    // underlying MineWorld calls are headless; this wrapper just also drives the live view).
    public class MapGenerationService : MonoBehaviour
    {
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private int gridWidth = 30;
        private LayerConfigProvider layerConfigProvider => GameManager.LayerConfigProvider;
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;
        private ChunkStreamingManager streamingManager => GameManager.ChunkStreamingManager;

        [Tooltip("Placeholder default - exact base radius and Lantern-tier scaling is an open design item (see MapGenerationImplementation.md).")]
        [SerializeField] private int baseFogRevealRadius = 3;

        public MineWorld World { get; private set; }

        private void Awake()
        {
            World = new MineWorld(worldSeed, gridWidth);
            streamingManager.Initialize(World);
        }

        public bool MineCell(int layerIndex, int x, int y, int fogRadiusOverride = -1)
        {
            if (!World.TryMineCell(layerIndex, x, y, out var block, out var artifactFound)) return false;

            int radius = fogRadiusOverride >= 0 ? fogRadiusOverride : GetFogRevealRadius();
            var revealed = World.RevealFog(layerIndex, x, y, radius);
            streamingManager.NotifyCellMined(layerIndex, x, y, revealed);

            if (block != null && block.Category == BlockCategory.Hazard)
            {
                GameManager.EventService.Dispatch(new HazardTriggeredEvent(layerIndex, x, y, block.HazardBehavior));
            }

            GameManager.EventService.Dispatch(new CellMinedEvent(layerIndex, x, y, block, artifactFound));
            return true;
        }

        // GameDesignDoc "Market Upgrades > Mining > Lantern": base radius plus purchased levels,
        // or the whole chunk width once the "true sight" capstone is unlocked.
        private int GetFogRevealRadius()
        {
            var upgrades = UpgradeManager.Instance;
            if (upgrades != null && upgrades.TrueSightUnlocked) return gridWidth;
            return baseFogRevealRadius + (upgrades != null ? upgrades.LanternFogRadiusBonus : 0);
        }

        // Inverts ChunkTilemapView's cell->world placement (pos = (x, -y) within a chunk root
        // positioned at -layerOffset*cellSize) so player-facing systems can resolve which cell
        // a world position falls in.
        public bool WorldToCell(Vector3 worldPos, out int layerIndex, out int x, out int y)
        {
            float cellSize = streamingManager.CellSize;
            int depthInBlocks = Mathf.FloorToInt(-worldPos.y / cellSize);
            if (worldPos.y > 0)
            {
                depthInBlocks = 0;
            }
            layerIndex = streamingManager.GetLayerIndexAtDepth(depthInBlocks);
            x = Mathf.FloorToInt(worldPos.x / cellSize);
            y = depthInBlocks - streamingManager.GetLayerOffset(layerIndex);

            var chunk = World.GetOrGenerateChunk(layerIndex);
            return x >= 0 && x < chunk.Width && y >= 0 && y < chunk.Height;
        }

        public Vector3 CellToWorldCenter(int layerIndex, int x, int y)
        {
            float cellSize = streamingManager.CellSize;
            int depthInBlocks = streamingManager.GetLayerOffset(layerIndex) + y;
            return new Vector3((x + 0.5f) * cellSize, -(depthInBlocks + 0.5f) * cellSize, 0f);
        }

        public float CellSize => streamingManager.CellSize;

        // Null if out of bounds or already mined - both mean "nothing here to mine".
        public BlockType GetBlockTypeAt(int layerIndex, int x, int y)
        {
            var chunk = World.GetOrGenerateChunk(layerIndex);
            if (x < 0 || x >= chunk.Width || y < 0 || y >= chunk.Height) return null;

            var cell = chunk.Cells[chunk.Index(x, y)];
            if (cell.Mined) return null;

            return blockTypeDatabase != null ? blockTypeDatabase.Get(cell.BlockTypeId) : null;
        }

        // Peeks whether the cell holds an undiscovered artifact without mining it - lets callers
        // (PlayerMining) know to credit an artifact once the mine completes.
        public bool IsArtifactAt(int layerIndex, int x, int y)
        {
            var chunk = World.GetOrGenerateChunk(layerIndex);
            if (x < 0 || x >= chunk.Width || y < 0 || y >= chunk.Height) return false;

            var cell = chunk.Cells[chunk.Index(x, y)];
            return !cell.Mined && cell.IsArtifact;
        }

        public float GetBlockHealthMultiplier(int layerIndex) => layerConfigProvider.GetConfig(layerIndex).BlockHealth;

        // New seed, all tunnels wiped; grid width upgrade level is left untouched so it carries over.
        public void PrestigeReset(int newSeed)
        {
            World.ResetForPrestige(newSeed);
            streamingManager.ClearAll();
        }

        public void ApplyGridWidthUpgrade(int newGridWidth) => World.SetGridWidth(newGridWidth);
    }
}
