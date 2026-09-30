using System.Collections.Generic;
using System.Linq;
using Economy;
using Events;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.InputSystem;

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

        // Editor-only: F toggles fog on every chunk view, resident or pooled.
        private bool fogHidden;

        private void Update()
        {
            if (!Application.isEditor) return;
            if (!Keyboard.current.fKey.wasPressedThisFrame) return;

            fogHidden = !fogHidden;
            foreach (var view in tilemapsByLayer.Values)
            {
                view.SetFogHidden(fogHidden);
            }
        }

        public void Initialize(MineWorld mineWorld)
        {
            world = mineWorld;
            // Release every currently-active view before rebinding - otherwise a post-startup world
            // swap (e.g. restoring from save) leaves views whose layer stays within the window still
            // bound to the previous world's ChunkData instances, since UpdateWindow() only
            // Acquire()s layers that aren't already in activeViews.
            ClearAll();
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

            // In the editor, keep every authored layer resident (deeper layers still stream normally).
            if (Application.isEditor)
            {
                int deepestAuthoredLayer = layerConfigProvider.LayerConfigs.Max(config => config.LayerIndex);
                for (int i = 0; i <= deepestAuthoredLayer; i++)
                {
                    wantedLayers.Add(i);
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
                GameManager.EventService.Dispatch(new ChunkViewShownEvent(layerIndex));
                return;
            }

            var chunk = world.GetOrGenerateChunk(layerIndex);

            var newView = Instantiate(chunkViewPrefab, tilemapContainer);
            newView.gameObject.name = $"ChunkTilemapView_Layer{layerIndex}";
            newView.gameObject.SetActive(true);
            newView.transform.position = new Vector3(0f, -layerConfigProvider.GetLayerOffset(layerIndex) * mapGenerationConfig.CellSize, 0f);
            newView.Bind(chunk, layerIndex);
            newView.SetFogHidden(fogHidden);

            tilemapsByLayer[layerIndex] = newView;

            SyncBoundary(layerIndex - 1, layerIndex);
            SyncBoundary(layerIndex, layerIndex + 1);
            GameManager.EventService.Dispatch(new ChunkViewShownEvent(layerIndex));
        }

        // Stitches a "ghost" copy of each chunk's boundary row into the other's own Tilemap (see
        // ChunkTilemapView.PaintGhostRow) so RuleTile neighbor lookups across a layer seam resolve
        // to real ground instead of null - without this, edgeBleedTile draws a false edge along
        // every layer boundary regardless of mining state. No-ops unless both chunks are resident.
        private void SyncBoundary(int shallowerLayerIndex, int deeperLayerIndex)
        {
            if (!tilemapsByLayer.TryGetValue(shallowerLayerIndex, out var shallower) || !shallower.gameObject.activeSelf) return;
            if (!tilemapsByLayer.TryGetValue(deeperLayerIndex, out var deeper) || !deeper.gameObject.activeSelf) return;

            deeper.PaintGhostRow(aboveChunk: true, shallower.GetBoundaryRowTiles(bottomRow: true));
            shallower.PaintGhostRow(aboveChunk: false, deeper.GetBoundaryRowTiles(bottomRow: false));

            deeper.RefreshBoundaryRow(bottomRow: false);
            shallower.RefreshBoundaryRow(bottomRow: true);
        }

        private void Release(int layerIndex)
        {
            //Debug.Log($"ChunkStreamingManager.Release: {layerIndex}");
            var view = tilemapsByLayer[layerIndex];
            view.gameObject.SetActive(false);
            GameManager.EventService.Dispatch(new ChunkViewHiddenEvent(layerIndex));
        }

        public bool IsLayerResident(int layerIndex) =>
            tilemapsByLayer.TryGetValue(layerIndex, out var view) && view != null && view.gameObject.activeSelf;

        public void NotifyCellMined(int layerIndex, int x, int y, IReadOnlyList<Vector2Int> revealedCells)
        {
            if (!tilemapsByLayer.TryGetValue(layerIndex, out var view)) return;

            var affected = new List<Vector2Int>(revealedCells.Count + 1) { new(x, y) };
            affected.AddRange(revealedCells);
            view.RepaintCells(affected);

            SyncBoundary(layerIndex - 1, layerIndex);
            SyncBoundary(layerIndex, layerIndex + 1);
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
                GameManager.EventService.Dispatch(new ChunkViewHiddenEvent(layerIndex));
            }
            tilemapsByLayer.Clear();
            focusLayerByEntity.Clear();
        }
    }
}
