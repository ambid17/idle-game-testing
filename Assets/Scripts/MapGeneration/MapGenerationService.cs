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
        private LayerConfigProvider layerConfigProvider => GameManager.LayerConfigProvider;
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;
        private ChunkStreamingManager streamingManager => GameManager.ChunkStreamingManager;
        private MapGenerationConfig mapGenerationConfig => GameManager.MapGenerationConfig;

        [Tooltip("Placeholder default - exact base radius and Lantern-tier scaling is an open design item (see MapGenerationImplementation.md).")]
        [SerializeField] private int baseFogRevealRadius = 3;

        // Invisible physical walls (BoxCollider2D, no renderer) at the grid's horizontal extent -
        // stop the player's Rigidbody2D from walking/flying past the edge. Tall enough to cover
        // any depth the player can reach, since layers generate on demand with no hard floor.
        private const float BoundaryWallThickness = 1f;
        private const float BoundaryWallHeight = 20000f;

        private BoxCollider2D leftBoundaryWall;
        private BoxCollider2D rightBoundaryWall;

        public MineWorld World { get; private set; }

        // The un-upgraded default width, used by PrestigeManager.ExecutePrestige to recompute the
        // absolute width (base + PrestigeUpgradeManager.GridWidthBonus) on every prestige, rather
        // than compounding bonuses onto whatever World.GridWidth already grew to.
        public int BaseGridWidth => mapGenerationConfig.GridWidth;

        private void Awake()
        {
            World = new MineWorld(mapGenerationConfig.Seed, mapGenerationConfig.GridWidth);
            streamingManager.Initialize(World);
            CreateBoundaryWalls();
            UpdateBoundaryWalls();
        }

        // Swaps in a world restored from save data (SaveService.ApplyMapData), replacing the
        // throwaway default one created in Awake(), and forces the streaming manager to rebind its
        // view window to the new world's chunks.
        public void RestoreWorld(MineWorld restoredWorld)
        {
            World = restoredWorld;
            streamingManager.Initialize(World);
            UpdateBoundaryWalls();
        }

        private void CreateBoundaryWalls()
        {
            leftBoundaryWall = CreateBoundaryWall("LeftBoundaryWall");
            rightBoundaryWall = CreateBoundaryWall("RightBoundaryWall");
        }

        private BoxCollider2D CreateBoundaryWall(string wallName)
        {
            var wall = new GameObject(wallName);
            wall.transform.SetParent(transform, false);

            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(BoundaryWallThickness, BoundaryWallHeight);
            // Frictionless so the player doesn't stick to the wall while sliding down it mid-fall.
            collider.sharedMaterial = new PhysicsMaterial2D($"{wallName}Material") { friction = 0f, bounciness = 0f };
            return collider;
        }

        // Re-centers the two walls on the grid's current horizontal extent. Called whenever
        // GridWidth can change (grid-width upgrade, prestige, save restore) rather than baked
        // once, since the grid-width upgrade widens the playable area over time.
        private void UpdateBoundaryWalls()
        {
            if (leftBoundaryWall == null || rightBoundaryWall == null) return;

            float gridWorldWidth = World.GridWidth * mapGenerationConfig.CellSize;

            leftBoundaryWall.transform.position = new Vector3(-BoundaryWallThickness * 0.5f, 0, 0f);
            rightBoundaryWall.transform.position = new Vector3(gridWorldWidth + BoundaryWallThickness * 0.5f, 0, 0f);
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
            // Can't mine if: already mined, or target is a building support
            if (!World.TryMineCell(layerIndex, x, y, out var block)) return false;

            HandleFogUpdate(layerIndex, x, y, fogRadiusOverride);
            if (block != null && block.Category == BlockCategory.Hazard)
            {
                GameManager.EventService.Dispatch(new HazardTriggeredEvent(layerIndex, x, y, block.HazardBehavior));
            }
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
            if (upgrades != null && upgrades.TrueSightUnlocked) return mapGenerationConfig.GridWidth;
            return baseFogRevealRadius + (upgrades != null ? upgrades.LanternFogRadiusBonus : 0);
        }

        // Inverts ChunkTilemapView's cell->world placement (pos = (x, -y) within a chunk root
        // positioned at -layerOffset*cellSize) so player-facing systems can resolve which cell
        // a world position falls in.
        public bool TryWorldToCellInBounds(Vector3 worldPos, out int layerIndex, out int x, out int y)
        {
            float cellSize = mapGenerationConfig.CellSize;
            int depthInBlocks = layerConfigProvider.GetDepthInBlocksAtWorldY(worldPos.y, cellSize);
            layerIndex = layerConfigProvider.GetLayerIndexAtDepth(depthInBlocks);
            x = Mathf.FloorToInt(worldPos.x / cellSize);
            y = depthInBlocks - layerConfigProvider.GetLayerOffset(layerIndex);

            var chunk = World.GetOrGenerateChunk(layerIndex);
            var inHorizontalBounds = x >= 0 && x < chunk.Width;
            var inVerticalBounds = y < chunk.Height;
            return inHorizontalBounds && inVerticalBounds;
        }

        public Vector3 CellToWorldCenter(int layerIndex, int x, int y)
        {
            float cellSize = mapGenerationConfig.CellSize;
            int depthInBlocks = layerConfigProvider.GetLayerOffset(layerIndex) + y;
            return new Vector3((x + 0.5f) * cellSize, -(depthInBlocks - 0.5f) * cellSize, 0f);
        }

        public float CellSize => mapGenerationConfig.CellSize;

        // Null if out of bounds or already mined - both mean "nothing here to mine".
        public BlockType GetBlockTypeAt(int layerIndex, int x, int y)
        {
            var chunk = World.GetOrGenerateChunk(layerIndex);
            if (x < 0 || x >= chunk.Width || y < 0 || y >= chunk.Height) return null;

            var cell = chunk.Cells[chunk.Index(x, y)];
            if (cell.Mined) return null;

            return blockTypeDatabase != null ? blockTypeDatabase.Get(cell.BlockTypeId) : null;
        }

        public float GetBlockHealthMultiplier(int layerIndex) => layerConfigProvider.GetConfig(layerIndex).BlockHealth;

        // New seed, all tunnels wiped; grid width upgrade level is left untouched so it carries over.
        public void PrestigeReset(int newSeed)
        {
            World.ResetForPrestige(newSeed);
            streamingManager.ClearAll();
        }

        public void ApplyGridWidthUpgrade(int newGridWidth)
        {
            World.SetGridWidth(newGridWidth);
            UpdateBoundaryWalls();
        }
    }
}
