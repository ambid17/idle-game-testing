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
    // and dispatches the event this reacts to. The chest is held as pending until PlayerRevivedEvent
    // so the still-present body can't loot it straight back during the death animation/screen.
    // Also used by SaveService to respawn chests that were still active when the game was last
    // saved (RestoreFromSaveData), including a pending one (TryGetPendingChest).
    public class ChestSpawner : MonoBehaviour
    {
        [SerializeField] private Chest chestPrefab;
        [SerializeField] private PlayerController player;

        private ChestSpawnData? pendingDeathChest;

        private void Awake()
        {
            if (chestPrefab == null) Debug.LogError("ChestSpawner is missing chestPrefab.");
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) Debug.LogError("ChestSpawner: no PlayerController found in scene.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInventoryDroppedEvent>(OnPlayerInventoryDropped);
            GameManager.EventService.Add<ChestSpawnRequestedEvent>(OnChestSpawnRequested);
            GameManager.EventService.Add<PlayerRevivedEvent>(OnPlayerRevived);
            GameManager.EventService.Add<PrestigeCompletedEvent>(OnPrestigeCompleted);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInventoryDroppedEvent>(OnPlayerInventoryDropped);
            GameManager.EventService.Remove<ChestSpawnRequestedEvent>(OnChestSpawnRequested);
            GameManager.EventService.Remove<PlayerRevivedEvent>(OnPlayerRevived);
            GameManager.EventService.Remove<PrestigeCompletedEvent>(OnPrestigeCompleted);
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

            // Shouldn't happen (no second death before a revive), but never drop ore on the floor.
            if (pendingDeathChest.HasValue) SpawnPendingDeathChest();
            pendingDeathChest = new ChestSpawnData(spawnPosition, evt.OreCounts);
        }

        private void OnPlayerRevived() => SpawnPendingDeathChest();

        // Prestige wipes carried ore and regenerates the map, so chests from the previous run (death
        // drops and Treasure Chest overflow alike) would otherwise hand pre-prestige ore back. Runs
        // on PrestigeCompletedEvent, which PrestigeManager dispatches after its PlayerRevivedEvent,
        // so a pending death chest spawned by that revive is swept up here too.
        private void OnPrestigeCompleted(PrestigeCompletedEvent evt)
        {
            pendingDeathChest = null;
            foreach (var chest in new List<Chest>(ChestRegistry.Instance.ActiveChests))
            {
                Destroy(chest.gameObject);
            }
        }

        private void SpawnPendingDeathChest()
        {
            if (!pendingDeathChest.HasValue) return;

            var pending = pendingDeathChest.Value;
            pendingDeathChest = null;
            SpawnChest(pending.Position, pending.OreCounts);
        }

        // For SaveService - a death chest that hasn't spawned yet (player is still on the death
        // screen) is saved like any active chest, so quitting before respawning doesn't lose it.
        public bool TryGetPendingChest(out ChestSpawnData chest)
        {
            chest = pendingDeathChest.GetValueOrDefault();
            return pendingDeathChest.HasValue;
        }

        // Treasure Chest power-up overflow (see Player.PlayerPowerUps) - position is already a cell
        // center, so it's used as-is.
        private void OnChestSpawnRequested(ChestSpawnRequestedEvent evt)
        {
            if (chestPrefab == null || evt.OreCounts.All(kvp => kvp.Value <= 0)) return;
            SpawnChest(evt.Position, evt.OreCounts);
        }

        // Respawns chests that were still active (unlooted) at the last save - called by
        // SaveService.ApplyLoadedData. Positions were already snapped to a cell center when first
        // spawned, so they're used as-is rather than re-resolving a cell.
        public void RestoreFromSaveData(IEnumerable<ChestSpawnData> chests)
        {
            if (chestPrefab == null || chests == null) return;

            // A load replaces the world, so a death chest from before it no longer belongs anywhere.
            pendingDeathChest = null;

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
