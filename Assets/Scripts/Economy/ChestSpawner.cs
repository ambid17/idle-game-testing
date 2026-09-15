using Events;
using Player;
using UnityEngine;

namespace Economy
{
    // Drops the player's lost inventory into a lootable Chest at the center of the map cell they
    // died in, instead of discarding it - see PlayerInventory.HandleDeath, which withdraws the ore
    // and dispatches the event this reacts to.
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
            if (chestPrefab == null || player == null) return;

            var mapGenerationService = GameManager.MapGenerationService;
            Vector3 deathPosition = player.transform.position;
            Vector3 spawnPosition = deathPosition;
            if (mapGenerationService.TryWorldToCellInBounds(deathPosition, out var layerIndex, out var x, out var y))
            {
                spawnPosition = mapGenerationService.CellToWorldCenter(layerIndex, x, y);
            }

            var chest = Instantiate(chestPrefab, spawnPosition, Quaternion.identity);
            chest.Configure(evt.OreCounts);
        }
    }
}
