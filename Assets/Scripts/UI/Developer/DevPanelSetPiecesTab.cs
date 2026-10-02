using System.Collections.Generic;
using Events;
using MapGeneration;
using Player;
using TMPro;
using UnityEngine;

namespace UI
{
    // Dev Panel tab for testing set-piece rooms (StructureDefinitions) without hunting for a seed:
    //   Spawn - stamps the structure into the live world just under the player, unmirrored, so it
    //     can be entered the way a player would find it. Not saved: chunks regenerate from the
    //     seed on load, so only the cells dug out of it survive a reload.
    //   Go - teleports into a room the generator placed in the current map.
    public class DevPanelSetPiecesTab : MonoBehaviour
    {
        // Rows of solid ground left between the player's cell and a spawned structure's top row.
        private const int SpawnGapRows = 1;

        [SerializeField] private PlayerController playerController;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Transform spawnRowContainer;
        [SerializeField] private Transform generatedRowContainer;
        [SerializeField] private DevUpgradeRowUI rowPrefab;

        private MapGenerationService map => GameManager.MapGenerationService;
        private LayerConfigProvider layerConfigs => GameManager.LayerConfigProvider;

        private void Start()
        {
            if (playerController == null) Debug.LogError("DevPanelSetPiecesTab.playerController is not assigned.");
            if (statusLabel == null) Debug.LogError("DevPanelSetPiecesTab.statusLabel is not assigned.");
            if (spawnRowContainer == null) Debug.LogError("DevPanelSetPiecesTab.spawnRowContainer is not assigned.");
            if (generatedRowContainer == null) Debug.LogError("DevPanelSetPiecesTab.generatedRowContainer is not assigned.");
            if (rowPrefab == null) Debug.LogError("DevPanelSetPiecesTab.rowPrefab is not assigned.");

            BuildSpawnRows();
        }

        // The generated list depends on the current map (seed, run modifier, earlier spawns), so
        // it's rebuilt every time the tab is shown.
        private void OnEnable() => BuildGeneratedRows();

        // Every structure any layer or run modifier can stamp, in first-seen order.
        private List<StructureStampFeature> CollectStampFeatures()
        {
            var features = new List<StructureStampFeature>();
            var seen = new HashSet<StructureDefinition>();

            foreach (var config in layerConfigs.LayerConfigs) AddStampFeatures(config.Features, features, seen);
            foreach (var modifier in GameManager.RunModifierDatabase.Modifiers)
            {
                AddStampFeatures(modifier.Features, features, seen);
                AddStampFeatures(modifier.TargetLayerFeatures, features, seen);
            }
            return features;
        }

        private static void AddStampFeatures(List<MapFeatureDefinition> source, List<StructureStampFeature> features, HashSet<StructureDefinition> seen)
        {
            foreach (var feature in source)
            {
                if (feature is StructureStampFeature stamp && stamp.Structure != null && seen.Add(stamp.Structure)) features.Add(stamp);
            }
        }

        private void BuildSpawnRows()
        {
            foreach (var feature in CollectStampFeatures())
            {
                var structure = feature.Structure;
                var grid = structure.Grid;
                var placement = feature.Placement;
                string layers = placement.MaxLayer < 0 ? $"layer {placement.MinLayer}+" : $"layers {placement.MinLayer}-{placement.MaxLayer}";
                string label = $"{DisplayName(structure)}  ({grid.GetLength(0)}x{grid.GetLength(1)}, {layers}, {placement.ChancePerLayer:P0})";

                var row = Instantiate(rowPrefab, spawnRowContainer);
                row.Bind(label, () => SpawnUnderPlayer(structure), "Spawn");
            }
        }

        private void BuildGeneratedRows()
        {
            foreach (Transform child in generatedRowContainer) Destroy(child.gameObject);

            int deepestAuthoredLayer = 0;
            foreach (var config in layerConfigs.LayerConfigs) deepestAuthoredLayer = Mathf.Max(deepestAuthoredLayer, config.LayerIndex);

            int count = 0;
            for (int layerIndex = 0; layerIndex <= deepestAuthoredLayer; layerIndex++)
            {
                var chunk = map.World.GetOrGenerateChunk(layerIndex);
                foreach (var (structure, rect) in chunk.StampedStructures)
                {
                    int layer = layerIndex;
                    var row = Instantiate(rowPrefab, generatedRowContainer);
                    row.Bind($"{DisplayName(structure)}  (layer {layer}, cell {rect.xMin},{rect.yMin})", () => GoTo(structure, layer, rect), "Go");
                    count++;
                }
            }

            statusLabel.text = $"Seed {map.World.Seed}: {count} set piece(s) in layers 0-{deepestAuthoredLayer}. Spawned ones are not saved.";
        }

        private static string DisplayName(StructureDefinition structure) => structure.name.Replace("Structure_", "");

        private void SpawnUnderPlayer(StructureDefinition structure)
        {
            var grid = structure.Grid;
            int w = grid.GetLength(0), h = grid.GetLength(1);

            map.TryWorldToCellInBounds(playerController.transform.position, out int layerIndex, out int playerX, out int playerY);
            var chunk = map.World.GetOrGenerateChunk(layerIndex);

            // Layer 0's top row holds the building supports.
            int y0 = Mathf.Max(playerY + 1 + SpawnGapRows, layerIndex == 0 ? 1 : 0);
            if (y0 + h > chunk.Height)
            {
                layerIndex++;
                chunk = map.World.GetOrGenerateChunk(layerIndex);
                y0 = 0;
            }

            if (w > chunk.Width || h > chunk.Height)
            {
                statusLabel.text = $"{DisplayName(structure)} ({w}x{h}) doesn't fit layer {layerIndex} ({chunk.Width}x{chunk.Height}).";
                return;
            }

            int x0 = Mathf.Clamp(playerX - w / 2, 0, chunk.Width - w);
            var rect = new RectInt(x0, y0, w, h);
            if (OverlapsProtectedGround(chunk, rect))
            {
                statusLabel.text = $"Can't spawn {DisplayName(structure)} here - it would overwrite a building support or the Critter Shop. Move and retry.";
                return;
            }

            // Back to solid ground first, so the stamp lands the same as it would during generation.
            ForEachCell(chunk, rect, 0, (ref CellData cell) => cell.Mined = false);

            var ctx = new MapEditContext(map.World.Seed, layerIndex, chunk, layerConfigs.GetConfig(layerIndex), layerConfigs.GetConfig(layerIndex + 1));
            StructureStampFeature.Stamp(ctx, structure, grid, x0, y0, Time.frameCount);

            chunk.MinedCount = 0;
            foreach (var cell in chunk.Cells)
            {
                if (cell.Mined) chunk.MinedCount++;
            }

            Reveal(layerIndex, chunk, rect, Mathf.Max(1, structure.ClearHazardMargin));
            // Re-runs the per-layer fixture scan (StructureTrapResolver's crusher pistons).
            if (GameManager.ChunkStreamingManager.IsLayerResident(layerIndex)) GameManager.EventService.Dispatch(new ChunkViewShownEvent(layerIndex));

            GameManager.EventService.Dispatch<UICloseEvent>();
        }

        private static bool OverlapsProtectedGround(ChunkData chunk, RectInt rect)
        {
            if (chunk.ShopCave is { } cave && cave.Overlaps(rect)) return true;

            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (chunk.Cells[chunk.Index(x, y)].BlockTypeId == (byte)BlockTypeId.GrassyDirt) return true;
                }
            }
            return false;
        }

        private void GoTo(StructureDefinition structure, int layerIndex, RectInt rect)
        {
            var chunk = map.World.GetOrGenerateChunk(layerIndex);
            if (!TryFindStandingCell(chunk, rect, out var cell))
            {
                // Solid all the way through - open one cell on top of it to arrive in.
                cell = new Vector2Int(rect.xMin + rect.width / 2, Mathf.Max(0, rect.yMin - 1));
                map.World.ForceClearCell(layerIndex, cell.x, cell.y);
            }

            Reveal(layerIndex, chunk, rect, 1);
            playerController.TeleportTo(map.CellToWorldCenter(layerIndex, cell.x, cell.y));
            GameManager.EventService.Dispatch<UICloseEvent>();
        }

        // An open cell inside the room, preferring one with a floor under it.
        private static bool TryFindStandingCell(ChunkData chunk, RectInt rect, out Vector2Int cell)
        {
            cell = default;
            bool found = false;
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (!chunk.Cells[chunk.Index(x, y)].Mined) continue;

                    bool hasFloor = y + 1 < chunk.Height && !chunk.Cells[chunk.Index(x, y + 1)].Mined;
                    if (found && !hasFloor) continue;

                    cell = new Vector2Int(x, y);
                    if (hasFloor) return true;
                    found = true;
                }
            }
            return found;
        }

        // Lifts the fog off rect (grown by margin) and repaints it.
        private void Reveal(int layerIndex, ChunkData chunk, RectInt rect, int margin)
        {
            var cells = ForEachCell(chunk, rect, margin, (ref CellData cell) => cell.Revealed = true);
            map.RefreshCellVisuals(layerIndex, cells);
        }

        private delegate void CellEdit(ref CellData cell);

        // Applies edit to every in-bounds cell of rect grown by margin. Returns the cells touched.
        private static List<Vector2Int> ForEachCell(ChunkData chunk, RectInt rect, int margin, CellEdit edit)
        {
            var cells = new List<Vector2Int>();
            for (int y = Mathf.Max(0, rect.yMin - margin); y < Mathf.Min(chunk.Height, rect.yMax + margin); y++)
            {
                for (int x = Mathf.Max(0, rect.xMin - margin); x < Mathf.Min(chunk.Width, rect.xMax + margin); x++)
                {
                    edit(ref chunk.Cells[chunk.Index(x, y)]);
                    cells.Add(new Vector2Int(x, y));
                }
            }
            return cells;
        }
    }
}
