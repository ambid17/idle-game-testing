using System.Collections.Generic;
using Interaction;
using MapGeneration;
using Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Economy
{
    // Spawned by ChestSpawner at the cell the player died in (see PlayerInventory.HandleDeath /
    // Events.PlayerInventoryDroppedEvent). Proximity+E interaction mirrors
    // PlayerInteractionDetector/BuildingInteractable, but is self-contained here since chests are
    // dynamically spawned rather than scene-authored singletons addressable by InteractableType.
    // Looting withdraws as much as fits into the player's inventory (OreInventory.WithdrawUpToWeight)
    // and leaves any remainder in the chest rather than requiring an all-or-nothing pickup.
    [RequireComponent(typeof(OreInventory))]
    public class Chest : MonoBehaviour
    {
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private float displayCapacity = 100f;
        [SerializeField] private ChestWeightBarUI weightBarUI;

        private OreInventory oreInventory;
        private PlayerInventory playerInRange;
        private InteractionPromptUI promptUI;
        private Keyboard keyboard => Keyboard.current;

        private void Awake()
        {
            oreInventory = GetComponent<OreInventory>();
        }

        private void Start()
        {
            oreInventory.Initialize(() => displayCapacity);
            promptUI = FindAnyObjectByType<InteractionPromptUI>();
            RefreshWeightBar();
        }

        // Called immediately after Instantiate by ChestSpawner to seed the dropped ore.
        public void Configure(IReadOnlyDictionary<BlockTypeId, int> initialOre)
        {
            oreInventory.RestoreFromSaveData(initialOre);
            RefreshWeightBar();
        }

        private void Update()
        {
            if (playerInRange != null && keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                Loot(playerInRange);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!playerLayer.Contains(other.gameObject.layer)) return;

            var inventory = other.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            playerInRange = inventory;
            promptUI?.Show("Press E to loot chest");
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (playerInRange == null) return;
            if (other.GetComponent<PlayerInventory>() != playerInRange) return;

            playerInRange = null;
            promptUI?.Hide();
        }

        private void Loot(PlayerInventory playerInventory)
        {
            float space = playerInventory.MaxWeight - playerInventory.CurrentWeight;
            if (space <= 0f) return;

            var withdrawn = oreInventory.WithdrawUpToWeight(space);
            foreach (var kvp in withdrawn)
            {
                if (kvp.Value <= 0) continue;
                var blockType = GameManager.BlockTypeDatabase.Get((byte)kvp.Key);
                playerInventory.AddOre(blockType, kvp.Value);
            }

            RefreshWeightBar();

            // Emptied - nothing left to loot, so despawn rather than leaving an inert chest behind.
            if (oreInventory.CurrentWeight <= 0f)
            {
                promptUI?.Hide();
                Destroy(gameObject);
            }
        }

        private void RefreshWeightBar()
        {
            weightBarUI?.SetWeight(oreInventory.CurrentWeight, displayCapacity);
        }
    }
}
