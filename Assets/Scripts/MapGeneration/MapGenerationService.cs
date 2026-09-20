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
        [SerializeField] private int baseFogRevealRadius = 2;

        // Invisible physical walls (BoxCollider2D, no renderer) at the grid's horizontal extent -
        // stop the player's Rigidbody2D from walking/flying past the edge. Tall enough to cover
        // any depth the player can reach, since layers generate on demand with no hard floor.
        private const float BoundaryWallThickness = 1f;
        private const float BoundaryWallHeight = 20000f;

        // Invisible one-way platform spanning the grid at the top of row 0 (surface level) -
        // QoL so the player can drive across the surface once a column has been dug deep enough
        // to leave a real gap (see CLAUDE.md UI note: no design doc entry, requested directly).
        // A PlatformEffector2D lets the player fly up through it from below but catches them on
        // the way back down instead of falling into the gaps.
        //
        // One BoxCollider2D per row-0 column (all as sibling components on one GameObject
        // alongside the single PlatformEffector2D - multiple Collider2D per GameObject is
        // supported, so this stays one object instead of one per column), enabled only once that
        // column's row-1 cell (the tile directly below the surface) has been mined - digging out
        // just the top row still leaves row 1 as solid ground, so there's nothing to catch yet;
        // once row 1 is gone too there's an actual multi-tile drop, and that's when the gate
        // should hold the player up. UpdateSurfaceFloorSegmentEnabled is the single source of
        // truth for a segment's enabled state, driven off World's own mined data rather than a
        // separate tracked flag. PlayerController.DropThroughSurfaceFloor briefly disables every
        // segment when the player presses S to intentionally descend back into the mine.
        private const float SurfaceFloorThickness = 0.1f;
        private const float SurfaceFloorDropThroughDuration = 0.5f;

        private BoxCollider2D leftBoundaryWall;
        private BoxCollider2D rightBoundaryWall;
        private GameObject surfaceFloorObject;
        private readonly Dictionary<int, BoxCollider2D> surfaceFloorSegmentsByX = new();
        private Coroutine surfaceFloorDropThroughRoutine;

        public GameObject SurfaceFloorObject => surfaceFloorObject;

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
            CreateSurfaceFloor();
            UpdateBoundaryWalls();
            RebuildSurfaceFloorSegments();

            // HazardEffectResolver is a pure event listener with no scene reference pointing at
            // it (same shape as PowerUpEffectResolver) - nothing else ever touches .Instance, so
            // without this force-wake its Singleton<T> GameObject would never get created and
            // HazardTriggeredEvent would go unhandled. Mirrors GameManager.Start() force-waking
            // SaveService.Instance for the same reason.
            _ = HazardEffectResolver.Instance;
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradeChanged);
            GameManager.EventService.Add<UpgradeLoadedEvent>(OnUpgradeLoaded);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradeChanged);
            GameManager.EventService.Remove<UpgradeLoadedEvent>(OnUpgradeLoaded);
        }

        private void OnUpgradeChanged(UpgradePurchasedEvent evt) => ApplyGridWidthIfRelevant(evt.Definition);
        private void OnUpgradeLoaded(UpgradeLoadedEvent evt) => ApplyGridWidthIfRelevant(evt.Definition);

        // Economy_GridWidthBonus is Dollar-purchased, so - unlike the Prestige grid-width perk,
        // which only reapplies once per ExecutePrestige - it needs to widen the live world the
        // instant it's bought (or restored from a save). Safe to call mid-run: it only affects
        // World.GridWidth (used by future chunk generation) and the boundary walls' position, never
        // already-generated chunks.
        private void ApplyGridWidthIfRelevant(UpgradeDefinition def)
        {
            if (def == null || def.Effect != UpgradeEffect.Economy_GridWidthBonus) return;
            ApplyGridWidthUpgrade(BaseGridWidth + UpgradeManager.Instance.EconomyGridWidthBonus + PrestigeUpgradeManager.Instance.GridWidthBonus);
        }

        // Swaps in a world restored from save data (SaveService.ApplyMapData), replacing the
        // throwaway default one created in Awake(), and forces the streaming manager to rebind its
        // view window to the new world's chunks.
        public void RestoreWorld(MineWorld restoredWorld)
        {
            World = restoredWorld;
            streamingManager.Initialize(World);
            UpdateBoundaryWalls();
            RebuildSurfaceFloorSegments();
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

        private void CreateSurfaceFloor()
        {
            surfaceFloorObject = new GameObject("SurfaceFloorGate");
            surfaceFloorObject.transform.SetParent(transform, false);
            // World-space (0,0,0) regardless of this service's own transform, since every
            // segment's Collider2D.offset below is an absolute world coordinate.
            surfaceFloorObject.transform.position = Vector3.zero;
            // Ground layer so PlayerController's ground check (and IsGrounded-gated systems like
            // PlayerMining) treat standing on this the same as standing on real terrain.
            surfaceFloorObject.layer = LayerMask.NameToLayer("Ground");

            var effector = surfaceFloorObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
        }

        // Rebuilds one segment per row-0 column and sets its enabled state from World's current
        // row-1 mined data. Called on world init, restore, prestige reset, and grid-width
        // upgrades - the places GridWidth or the mined state can change out from under the
        // existing segments all at once; a single mined tile is instead handled incrementally by
        // UpdateSurfaceFloorSegmentEnabled.
        private void RebuildSurfaceFloorSegments()
        {
            foreach (var segment in surfaceFloorSegmentsByX.Values)
            {
                if (segment != null) Destroy(segment);
            }
            surfaceFloorSegmentsByX.Clear();

            float cellSize = mapGenerationConfig.CellSize;
            float segmentY = CellToWorldCenter(0, 0, 0).y + cellSize * 0.5f + SurfaceFloorThickness * 0.5f;

            for (int x = 0; x < World.GridWidth; x++)
            {
                CreateSurfaceFloorSegment(x, cellSize, segmentY);
                UpdateSurfaceFloorSegmentEnabled(x);
            }
        }

        private void CreateSurfaceFloorSegment(int x, float cellSize, float segmentY)
        {
            var segment = surfaceFloorObject.AddComponent<BoxCollider2D>();
            segment.usedByEffector = true;
            segment.size = new Vector2(cellSize, SurfaceFloorThickness);
            segment.offset = new Vector2((x + 0.5f) * cellSize, segmentY);
            surfaceFloorSegmentsByX[x] = segment;
        }

        // Single source of truth for whether a column's gate segment should be solid: on once
        // that column's row-1 cell has been mined, off otherwise. Called after mining row 1 (see
        // MineCell) and whenever segments are rebuilt.
        private void UpdateSurfaceFloorSegmentEnabled(int x)
        {
            if (!surfaceFloorSegmentsByX.TryGetValue(x, out var segment) || segment == null) return;

            var chunk = World.GetOrGenerateChunk(0);
            segment.enabled = chunk.Cells[chunk.Index(x, 1)].Mined;
        }

        // Called by PlayerController when the player presses S while standing on the surface
        // floor gate - briefly disables every currently-solid segment so gravity carries them
        // back down into the mine instead of the one-way platform catching them again immediately.
        public void DropThroughSurfaceFloor()
        {
            if (surfaceFloorDropThroughRoutine != null) StopCoroutine(surfaceFloorDropThroughRoutine);
            surfaceFloorDropThroughRoutine = StartCoroutine(SurfaceFloorDropThroughRoutine());
        }

        private System.Collections.IEnumerator SurfaceFloorDropThroughRoutine()
        {
            foreach (var segment in surfaceFloorSegmentsByX.Values)
            {
                if (segment != null) segment.enabled = false;
            }

            yield return new WaitForSeconds(SurfaceFloorDropThroughDuration);

            // Re-derive from World rather than blindly re-enabling: a column mined mid-wait
            // should end up in whatever state UpdateSurfaceFloorSegmentEnabled would give it, not
            // necessarily back on.
            foreach (var x in surfaceFloorSegmentsByX.Keys)
            {
                UpdateSurfaceFloorSegmentEnabled(x);
            }
            surfaceFloorDropThroughRoutine = null;
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

            // Digging out row 1 (the tile beneath the surface) is what actually opens a fall-
            // through gap at that column - see the SurfaceFloor* fields' comment above.
            if (layerIndex == 0 && y == 1) UpdateSurfaceFloorSegmentEnabled(x);

            HandleFogUpdate(layerIndex, x, y, fogRadiusOverride);
            if (block != null && block.Category == BlockCategory.Hazard)
            {
                GameManager.EventService.Dispatch(new HazardTriggeredEvent(layerIndex, x, y, block.HazardBehavior));
            }
            else if (block != null && block.Category == BlockCategory.PowerUp)
            {
                GameManager.EventService.Dispatch(new PowerUpTriggeredEvent(layerIndex, x, y, block.HazardBehavior));
            }
            return true;
        }

        // Called by MapGeneration.PowerUpEffectResolver for a SightPotion's reveal burst - same
        // underlying World.RevealFog + streaming notification as a normal mine, just triggered
        // externally with an explicit radius rather than from MineCell itself.
        public void RevealAround(int layerIndex, int x, int y, int radius) => HandleFogUpdate(layerIndex, x, y, radius);

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

        public bool IsHazardousSurface(int layerIndex, int x, int y) => World.IsHazardousSurface(layerIndex, x, y);

        // Marks a just-mined Lava cell as a persistent hazardous surface and repaints it - separate
        // from MineCell's own repaint (which already ran before HazardEffectResolver's
        // HazardTriggeredEvent handler gets a chance to flag the cell), so a follow-up repaint is
        // needed here rather than relying on MineCell's.
        public void MarkHazardousSurface(int layerIndex, int x, int y)
        {
            if (!World.TrySetHazardousSurface(layerIndex, x, y)) return;
            RefreshCellVisual(layerIndex, x, y);
        }

        public void RefreshCellVisual(int layerIndex, int x, int y) =>
            streamingManager.NotifyCellMined(layerIndex, x, y, System.Array.Empty<Vector2Int>());

        // New seed, all tunnels wiped; grid width upgrade level is left untouched so it carries over.
        public void PrestigeReset(int newSeed)
        {
            World.ResetForPrestige(newSeed);
            streamingManager.ClearAll();
            RebuildSurfaceFloorSegments();
        }

        public void ApplyGridWidthUpgrade(int newGridWidth)
        {
            World.SetGridWidth(newGridWidth);
            UpdateBoundaryWalls();
            RebuildSurfaceFloorSegments();
        }
    }
}
