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
        private MapGenerationConfig mapGenerationConfig => GameManager.MapGenerationConfig;
        [SerializeField] private Transform poolParent;

        private MineWorld world;
        private readonly Dictionary<int, ChunkTilemapView> activeViews = new();
        private readonly Queue<ChunkTilemapView> tileMapQueue = new();
        private int currentFocusLayer = int.MinValue;

        public void Initialize(MineWorld mineWorld)
        {
            world = mineWorld;
            // Release every currently-active view before rebinding - otherwise a post-startup world
            // swap (e.g. restoring from save) leaves views whose layer stays within the window still
            // bound to the previous world's ChunkData instances, since UpdateWindow() only
            // Acquire()s layers that aren't already in activeViews.
            ClearAll();
            SetFocusDepth(0);
        }

        public void SetFocusDepth(float worldY)
        {
            int layerIndexAtDepth = layerConfigProvider.GetLayerIndexAtWorldY(worldY, mapGenerationConfig.CellSize);
            if (layerIndexAtDepth == currentFocusLayer) return;

            currentFocusLayer = layerIndexAtDepth;
            UpdateWindow();
        }

        private void UpdateWindow()
        {
            var wantedLayers = new HashSet<int>();
            int windowRadius = mapGenerationConfig.WindowRadius;
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
            view.transform.position = new Vector3(0f, -layerConfigProvider.GetLayerOffset(layerIndex) * mapGenerationConfig.CellSize, 0f);
            view.Bind(chunk, layerIndex);
            activeViews[layerIndex] = view;
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
