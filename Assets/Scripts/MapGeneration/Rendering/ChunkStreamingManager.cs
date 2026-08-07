using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    // Keeps a small window of chunks (focus layer +/- windowRadius) resident as live,
    // pooled ChunkTilemapView instances - repositioned rather than instantiated fresh per chunk.
    public class ChunkStreamingManager : MonoBehaviour
    {
        [SerializeField] private ChunkTilemapView chunkViewPrefab;
        [SerializeField] private Transform poolParent;
        [SerializeField] private int windowRadius = 1;
        [SerializeField] private float cellSize = 1f;

        private MineWorld world;
        private readonly Dictionary<int, ChunkTilemapView> activeViews = new();
        private readonly Queue<ChunkTilemapView> pool = new();
        private int currentFocusLayer = int.MinValue;

        public void Initialize(MineWorld mineWorld)
        {
            world = mineWorld;
            currentFocusLayer = int.MinValue;
        }

        public void SetFocusDepth(float depthInBlocks)
        {
            int layerIndex = Mathf.FloorToInt(depthInBlocks / ChunkGenerator.LayerHeight);
            if (layerIndex == currentFocusLayer) return;

            currentFocusLayer = layerIndex;
            UpdateWindow();
        }

        private void UpdateWindow()
        {
            var wanted = new HashSet<int>();
            for (int i = currentFocusLayer - windowRadius; i <= currentFocusLayer + windowRadius; i++)
            {
                if (i >= 0) wanted.Add(i);
            }

            foreach (var layerIndex in new List<int>(activeViews.Keys))
            {
                if (!wanted.Contains(layerIndex)) Release(layerIndex);
            }

            foreach (var layerIndex in wanted)
            {
                if (!activeViews.ContainsKey(layerIndex)) Acquire(layerIndex);
            }
        }

        private void Acquire(int layerIndex)
        {
            var chunk = world.GetOrGenerateChunk(layerIndex);
            var view = pool.Count > 0 ? pool.Dequeue() : Instantiate(chunkViewPrefab, poolParent);

            view.gameObject.SetActive(true);
            view.transform.position = new Vector3(0f, -layerIndex * ChunkGenerator.LayerHeight * cellSize, 0f);
            view.Bind(chunk, layerIndex);
            activeViews[layerIndex] = view;
        }

        private void Release(int layerIndex)
        {
            var view = activeViews[layerIndex];
            activeViews.Remove(layerIndex);
            view.gameObject.SetActive(false);
            pool.Enqueue(view);
        }

        public void NotifyCellMined(int layerIndex, int x, int y, IReadOnlyList<Vector2Int> revealedCells)
        {
            if (!activeViews.TryGetValue(layerIndex, out var view)) return;

            var affected = new List<Vector2Int>(revealedCells.Count + 1) { new(x, y) };
            affected.AddRange(revealedCells);
            view.RepaintCells(affected);
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
