using System.Collections.Generic;
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

        // The un-upgraded default width, used by PrestigeManager.ExecutePrestige to recompute the
        // absolute width (base + PrestigeUpgradeManager.GridWidthBonus) on every prestige, rather
        // than compounding bonuses onto whatever World.GridWidth already grew to.
        public int BaseGridWidth => gridWidth;

        private void Awake()
        {
            World = new MineWorld(worldSeed, gridWidth);
            streamingManager.Initialize(World);
        }

        // Swaps in a world restored from save data (SaveService.ApplyMapData), replacing the
        // throwaway default one created in Awake(), and forces the streaming manager to rebind its
        // view window to the new world's chunks.
        public void RestoreWorld(MineWorld restoredWorld)
        {
            World = restoredWorld;
            streamingManager.Initialize(World);
        }

        /// <summary>
        /// Mines cell at coords. Cell is not able to be mined if it was already
        /// </summary>
        /// <param name="layerIndex"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="fogRadiusOverride"></param>
        /// <returns>True if the cell was able to be mined.</returns>
        public bool MineCell(int layerIndex, int x, int y, int fogRadiusOverride = -1)
        {
            // Can't mine if already mined
            if (!World.TryMineCell(layerIndex, x, y, out var block, out var artifactFound)) return false;

            HandleFogUpdate(layerIndex, x, y, fogRadiusOverride);
            if (block != null && block.Category == BlockCategory.Hazard)
            {
                GameManager.EventService.Dispatch(new HazardTriggeredEvent(layerIndex, x, y, block.HazardBehavior));
            }

            GameManager.EventService.Dispatch(new CellMinedEvent(layerIndex, x, y, block, artifactFound));
            return true;
        }

        private void HandleFogUpdate(int layerIndex, int x, int y, int fogRadiusOverride = -1)
        {
            int radius = fogRadiusOverride >= 0 ? fogRadiusOverride : GetFogRevealRadius();
            var revealedByLayer = World.RevealFog(layerIndex, x, y, radius);

            revealedByLayer.TryGetValue(layerIndex, out var revealedInOriginLayer);
            streamingManager.NotifyCellMined(layerIndex, x, y, (IReadOnlyList<Vector2Int>)revealedInOriginLayer ?? System.Array.Empty<Vector2Int>());

            foreach (var (revealedLayerIndex, revealedCells) in revealedByLayer)
            {
                if (revealedLayerIndex == layerIndex) continue;
                streamingManager.NotifyFogRevealed(revealedLayerIndex, revealedCells);
            }
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
            depthInBlocks++; // Convert to 1-based depth for layer offset calculations.
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
            return new Vector3((x + 0.5f) * cellSize, -(depthInBlocks - 0.5f) * cellSize, 0f);
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
