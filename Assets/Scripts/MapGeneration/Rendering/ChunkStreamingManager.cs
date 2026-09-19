using System.Collections.Generic;
using System.Linq;
using Economy;
using Events;
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
        private Dictionary<string, int> focusLayerByEntity = new();

        public void Initialize(MineWorld mineWorld)
        {
            world = mineWorld;
            // Release every currently-active view before rebinding - otherwise a post-startup world
            // swap (e.g. restoring from save) leaves views whose layer stays within the window still
            // bound to the previous world's ChunkData instances, since UpdateWindow() only
            // Acquire()s layers that aren't already in activeViews.
            ClearAll();
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

        private void OnUpgradeChanged(UpgradePurchasedEvent evt) => RefreshVisualsIfHazardSense(evt.Definition);
        private void OnUpgradeLoaded(UpgradeLoadedEvent evt) => RefreshVisualsIfHazardSense(evt.Definition);

        // Movement_HazardSense's tile tint (ChunkTilemapView.BuildTerrainChange) is only recomputed
        // on the next partial repaint, so already-revealed hazard cells wouldn't retint until the
        // player mines something nearby - repaint every visible chunk immediately on purchase/load
        // so the effect is visible right away for play-testing.
        private void RefreshVisualsIfHazardSense(UpgradeDefinition def)
        {
            if (def == null || def.Effect != UpgradeEffect.Movement_HazardSense) return;
            foreach (var view in tilemapsByLayer.Values)
            {
                if (view != null && view.gameObject.activeSelf) view.RepaintAll();
            }
        }

        public void SetFocusDepth(string entityName,float worldY)
        {
            int layerIndexAtDepth = layerConfigProvider.GetLayerIndexAtWorldY(worldY, mapGenerationConfig.CellSize);
            if (focusLayerByEntity.TryGetValue(entityName, out int currentFocusLayer) && currentFocusLayer == layerIndexAtDepth) return;

            focusLayerByEntity[entityName] = layerIndexAtDepth;
            UpdateWindow();
        }

        private void UpdateWindow()
        {
            var wantedLayers = new HashSet<int>();
            int windowRadius = mapGenerationConfig.WindowRadius;

            var layersWithEntity = new HashSet<int>();
            foreach(var layerIndex in focusLayerByEntity.Values)
            {
                layersWithEntity.Add(layerIndex);
            }

            foreach (var layerIndex in layersWithEntity)
            {
                // look up and down <windowRadius layers> from the current focus layer,
                for (int i = layerIndex - windowRadius; i <= layerIndex + windowRadius; i++)
                {
                    if (i >= 0) wantedLayers.Add(i);
                }
            }

            //Debug.Log($"ChunkStreamingManager.UpdateWindow: focus={string.Join(", ", focusLayerByEntity.Values.ToList())}, windowRadius={windowRadius}, wantedLayers=[{string.Join(", ", wantedLayers)}]");
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

            //Debug.Log($"ChunkStreamingManager.Acquire: {layerIndex}");

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
            //Debug.Log($"ChunkStreamingManager.Release: {layerIndex}");
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
            focusLayerByEntity.Clear();
        }
    }
}
