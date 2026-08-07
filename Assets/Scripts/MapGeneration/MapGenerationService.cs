using System;
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
        [SerializeField] private LayerConfigProvider layerConfigProvider;
        [SerializeField] private BlockTypeDatabase blockTypeDatabase;
        [SerializeField] private ChunkStreamingManager streamingManager;

        [Tooltip("Placeholder default - exact base radius and Lantern-tier scaling is an open design item (see MapGenerationImplementation.md).")]
        [SerializeField] private int baseFogRevealRadius = 3;

        public MineWorld World { get; private set; }

        public event Action<int, int, int, BlockType, bool> CellMined;
        public event Action<int, int, int, HazardBehavior> HazardTriggered;

        private void Awake()
        {
            World = new MineWorld(worldSeed, gridWidth, layerConfigProvider, blockTypeDatabase);
            streamingManager.Initialize(World);
        }

        public bool MineCell(int layerIndex, int x, int y, int fogRadiusOverride = -1)
        {
            if (!World.TryMineCell(layerIndex, x, y, out var block, out var artifactFound)) return false;

            int radius = fogRadiusOverride >= 0 ? fogRadiusOverride : baseFogRevealRadius;
            var revealed = World.RevealFog(layerIndex, x, y, radius);
            streamingManager.NotifyCellMined(layerIndex, x, y, revealed);

            if (block != null && block.Category == BlockCategory.Hazard)
            {
                HazardTriggered?.Invoke(layerIndex, x, y, block.HazardBehavior);
            }

            CellMined?.Invoke(layerIndex, x, y, block, artifactFound);
            return true;
        }

        public void SetFocusDepth(float depthInBlocks) => streamingManager.SetFocusDepth(depthInBlocks);

        // New seed, all tunnels wiped; grid width upgrade level is left untouched so it carries over.
        public void PrestigeReset(int newSeed)
        {
            World.ResetForPrestige(newSeed);
            streamingManager.ClearAll();
        }

        public void ApplyGridWidthUpgrade(int newGridWidth) => World.SetGridWidth(newGridWidth);
    }
}
