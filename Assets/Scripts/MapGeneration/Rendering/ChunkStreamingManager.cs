using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    // Keeps a small window of chunks (focus layer +/- windowRadius) resident as live,
    // pooled ChunkTilemapView instances - repositioned rather than instantiated fresh per chunk.
    public class ChunkStreamingManager : MonoBehaviour
    {
        [SerializeField] private ChunkTilemapView chunkViewPrefab;
        private LayerConfigProvider layerConfigProvider => GameManager.LayerConfigProvider;
        [SerializeField] private Transform poolParent;
        [SerializeField] private int windowRadius = 1;
        [SerializeField] private float cellSize = 1f;

        private MineWorld world;
        private readonly Dictionary<int, ChunkTilemapView> activeViews = new();
        private readonly Queue<ChunkTilemapView> tileMapQueue = new();
        private int currentFocusLayer = int.MinValue;
        public float CellSize => cellSize;

        public void Initialize(MineWorld mineWorld)
        {
            world = mineWorld;
            SetFocusDepth(0);
        }

        public void SetFocusDepth(float depthInBlocks)
        {
            int layerIndexAtDepth = GetLayerIndexAtDepth((int)depthInBlocks);
            if (layerIndexAtDepth == currentFocusLayer) return;

            currentFocusLayer = layerIndexAtDepth;
            UpdateWindow();
        }

        private void UpdateWindow()
        {
            var wantedLayers = new HashSet<int>();
            // look up and down <windowRadius layers> from the current focus layer, 
            for (int i = currentFocusLayer - windowRadius; i <= currentFocusLayer + windowRadius; i++)
            {
                if (i >= 0) wantedLayers.Add(i);
            }

            foreach (var layerIndex in new List<int>(activeViews.Keys))
            {
                if (!wantedLayers.Contains(layerIndex)) Release(layerIndex);
            }

            foreach (var layerIndex in wantedLayers)
            {
                if (!activeViews.ContainsKey(layerIndex)) Acquire(layerIndex);
            }
        }

        private void Acquire(int layerIndex)
        {
            if(layerIndex < 0) return;
            var chunk = world.GetOrGenerateChunk(layerIndex);
            var view = tileMapQueue.Count > 0 ? tileMapQueue.Dequeue() : Instantiate(chunkViewPrefab, poolParent);

            view.gameObject.name = $"ChunkTilemapView_Layer{layerIndex}";
            view.gameObject.SetActive(true);
            view.transform.position = new Vector3(0f, -GetLayerOffset(layerIndex) * cellSize, 0f);
            view.Bind(chunk, layerIndex);
            activeViews[layerIndex] = view;
        }

        // Depth (in blocks) at which this layer starts - inverse of GetLayerIndexAtDepth.
        // Public so player-facing systems (e.g. PlayerMining) can convert world position <-> grid cell.
        public int GetLayerOffset(int layerIndex)
        {
            var yOffset = 0;
            for(int i = 0; i < layerIndex; i++)
            {
                yOffset += layerConfigProvider.GetConfig(i).LayerHeight;
            }
            return yOffset;
        }

        public int GetLayerIndexAtDepth(int depthInBlocks)
        {
            var currentTotalDepth = 0;
            for (int i = 0; i < layerConfigProvider.AuthoredLayers.Count; i++)
            {
                var config = layerConfigProvider.GetConfig(i);
                if (depthInBlocks < currentTotalDepth + config.LayerHeight)
                {
                    return i;
                }
                currentTotalDepth += config.LayerHeight;
            }
            return 0;
        }

        private void Release(int layerIndex)
        {
            var view = activeViews[layerIndex];
            activeViews.Remove(layerIndex);
            view.gameObject.SetActive(false);
            tileMapQueue.Enqueue(view);
        }

        public void NotifyCellMined(int layerIndex, int x, int y, IReadOnlyList<Vector2Int> revealedCells)
        {
            if (!activeViews.TryGetValue(layerIndex, out var view)) return;

            var affected = new List<Vector2Int>(revealedCells.Count + 1) { new(x, y) };
            affected.AddRange(revealedCells);
            view.RepaintCells(affected);
        }

        // For fog reveals that spilled into a neighboring layer's chunk (no mined cell of its own here).
        public void NotifyFogRevealed(int layerIndex, IReadOnlyList<Vector2Int> revealedCells)
        {
            if (revealedCells.Count == 0) return;
            if (!activeViews.TryGetValue(layerIndex, out var view)) return;

            view.RepaintCells(revealedCells);
        }

        public void ClearAll()
        {
            foreach (var layerIndex in new List<int>(activeViews.Keys))
            {
                Release(layerIndex);
            }
            currentFocusLayer = int.MinValue;
        }
    }
}
