using Events;
using MapGeneration;
using Player;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Economy
{
    // In-memory transfer object between SaveService and ChestSpawner.RestoreFromSaveData - the
    // serializable save-file shape (Persistence.ChestSaveEntry) stays in Persistence so Economy
    // doesn't need to depend on that namespace.
    public readonly struct ChestSpawnData
    {
        public readonly Vector3 Position;
        public readonly IReadOnlyDictionary<BlockTypeId, int> OreCounts;

        public ChestSpawnData(Vector3 position, IReadOnlyDictionary<BlockTypeId, int> oreCounts)
        {
            Position = position;
            OreCounts = oreCounts;
        }
    }

    // Drops the player's lost inventory into a lootable Chest at the center of the map cell they
    // died in, instead of discarding it - see PlayerInventory.HandleDeath, which withdraws the ore
    // and dispatches the event this reacts to. Also used by SaveService to respawn chests that were
    // still active when the game was last saved (RestoreFromSaveData).
    public class ChestSpawner : MonoBehaviour
    {
        [SerializeField] private Chest chestPrefab;
        [SerializeField] private PlayerController player;

        private void Awake()
        {
            if (chestPrefab == null) Debug.LogError("ChestSpawner is missing chestPrefab.");
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) Debug.LogError("ChestSpawner: no PlayerController found in scene.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInventoryDroppedEvent>(OnPlayerInventoryDropped);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInventoryDroppedEvent>(OnPlayerInventoryDropped);
        }

        private void OnPlayerInventoryDropped(PlayerInventoryDroppedEvent evt)
        {
            if (chestPrefab == null || player == null || evt.OreCounts.All(kvp => kvp.Value <= 0)) return;

            var mapGenerationService = GameManager.MapGenerationService;
            Vector3 deathPosition = player.transform.position;
            Vector3 spawnPosition = deathPosition;
            if (mapGenerationService.TryWorldToCellInBounds(deathPosition, out var layerIndex, out var x, out var y))
            {
                spawnPosition = mapGenerationService.CellToWorldCenter(layerIndex, x, y);
            }

            SpawnChest(spawnPosition, evt.OreCounts);
        }

        // Respawns chests that were still active (unlooted) at the last save - called by
        // SaveService.ApplyLoadedData. Positions were already snapped to a cell center when first
        // spawned, so they're used as-is rather than re-resolving a cell.
        public void RestoreFromSaveData(IEnumerable<ChestSpawnData> chests)
        {
            if (chestPrefab == null || chests == null) return;

            foreach (var entry in chests)
            {
                if (entry.OreCounts == null || entry.OreCounts.All(kvp => kvp.Value <= 0)) continue;
                SpawnChest(entry.Position, entry.OreCounts);
            }
        }

        private void SpawnChest(Vector3 position, IReadOnlyDictionary<BlockTypeId, int> oreCounts)
        {
            var chest = Instantiate(chestPrefab, position, Quaternion.identity);
            chest.Configure(oreCounts);
        }
    }
}
