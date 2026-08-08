using System.Collections.Generic;
using System.Linq;
using Unity.Jobs;
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
        [SerializeField] private Transform tilemapContainer;

        private MineWorld world;
        private readonly Dictionary<int, ChunkTilemapView> tilemapsByLayer = new();
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

            Debug.Log($"ChunkStreamingManager.UpdateWindow: focus={currentFocusLayer}, windowRadius={windowRadius}, wantedLayers=[{string.Join(", ", wantedLayers)}]");
            var activeLayers = tilemapsByLayer.Where(kvp => kvp.Value.gameObject.activeSelf).Select(kvp => kvp.Key).ToList();
            foreach (var layerIndex in activeLayers)
            {
                if (!wantedLayers.Contains(layerIndex)) Release(layerIndex);
            }

            foreach (var layerIndex in wantedLayers)
            {
                if (!activeLayers.Contains(layerIndex)) Acquire(layerIndex);
            }
        }

        private void Acquire(int layerIndex)
        {
            if (layerIndex < 0) return;

            Debug.Log($"ChunkStreamingManager.Acquire: {layerIndex}");

            if (tilemapsByLayer.ContainsKey(layerIndex))
            {
                tilemapsByLayer[layerIndex].gameObject.SetActive(true);
                return;
            }

            var chunk = world.GetOrGenerateChunk(layerIndex);

            var newView = Instantiate(chunkViewPrefab, tilemapContainer);
            newView.gameObject.name = $"ChunkTilemapView_Layer{layerIndex}";
            newView.gameObject.SetActive(true);
            newView.transform.position = new Vector3(0f, -layerConfigProvider.GetLayerOffset(layerIndex) * mapGenerationConfig.CellSize, 0f);
            newView.Bind(chunk, layerIndex);

            tilemapsByLayer[layerIndex] = newView;
        }

        private void Release(int layerIndex)
        {
            Debug.Log($"ChunkStreamingManager.Release: {layerIndex}");
            var view = tilemapsByLayer[layerIndex];
            view.gameObject.SetActive(false);
        }

        public void NotifyCellMined(int layerIndex, int x, int y, IReadOnlyList<Vector2Int> revealedCells)
        {
            if (!tilemapsByLayer.TryGetValue(layerIndex, out var view)) return;

            var affected = new List<Vector2Int>(revealedCells.Count + 1) { new(x, y) };
            affected.AddRange(revealedCells);
            view.RepaintCells(affected);
        }

        // For fog reveals that spilled into a neighboring layer's chunk (no mined cell of its own here).
        public void NotifyFogRevealed(int layerIndex, IReadOnlyList<Vector2Int> revealedCells)
        {
            if (revealedCells.Count == 0) return;
            if (!tilemapsByLayer.TryGetValue(layerIndex, out var view)) return;

            view.RepaintCells(revealedCells);
        }

        public void ClearAll()
        {
            foreach (var layerIndex in new List<int>(tilemapsByLayer.Keys))
            {
                if(tilemapsByLayer[layerIndex] == null) continue;
                Destroy(tilemapsByLayer[layerIndex].gameObject);
            }
            tilemapsByLayer.Clear();
            currentFocusLayer = int.MinValue;
        }
    }
}
