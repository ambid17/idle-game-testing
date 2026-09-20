using System.Collections.Generic;
using Events;
using Interaction;
using MapGeneration;
using Player;
using UnityEngine;

namespace Economy
{
    // Spawned by ChestSpawner at the cell the player died in (see PlayerInventory.HandleDeath /
    // Events.PlayerInventoryDroppedEvent). Proximity+E interaction goes through the shared
    // PlayerInteractionDetector/IInteractable system (same as BuildingInteractable) rather than a
    // bespoke trigger, so a chest can never fire alongside another nearby interactable on one press.
    // Looting withdraws as much as fits into the player's inventory (OreInventory.WithdrawUpToWeight)
    // and leaves any remainder in the chest rather than requiring an all-or-nothing pickup.
    [RequireComponent(typeof(OreInventory))]
    public class Chest : MonoBehaviour, IInteractable
    {
        [SerializeField] private ChestWeightBarUI weightBarUI;
        private InteractableType interactableType = InteractableType.Chest;
        public InteractableType InteractableType { get { return interactableType; } }

        private OreInventory oreInventory;
        private PlayerInventory playerInventory;

        public string PromptText => "Press E to pick up lost ores";

        // Snapshot of what's currently in the chest, for SaveService (via ChestRegistry) to persist.
        public IReadOnlyDictionary<BlockTypeId, int> OreCounts => oreInventory.OreCounts;

        private void Awake()
        {
            oreInventory = GetComponent<OreInventory>();
        }

        private void Start()
        {
            playerInventory = FindAnyObjectByType<PlayerInventory>();
            if (playerInventory == null) Debug.LogError("Chest: no PlayerInventory found in scene.");
        }

        private void OnEnable()
        {
            ChestRegistry.Instance.Register(this);
            GameManager.EventService.Add<PlayerInteractedEvent>(OnPlayerInteracted);
        }
        // HasInstance guard: teardown order across objects isn't guaranteed when Stopping the
        // Player, so ChestRegistry's singleton may already be destroyed by the time this runs.
        // (Instance would resurrect it as a stray GameObject mid-unload - HasInstance doesn't.)
        private void OnDisable()
        {
            if (ChestRegistry.HasInstance) ChestRegistry.Instance.Unregister(this);
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnPlayerInteracted);
        }

        // Called immediately after Instantiate by ChestSpawner to seed the dropped ore.
        public void Configure(IReadOnlyDictionary<BlockTypeId, int> initialOre)
        {
            oreInventory.PopulateFromDictionary(initialOre);
            var currentWeight = oreInventory.CurrentWeight;
            oreInventory.Initialize(() => currentWeight);
            RefreshWeightBar();
        }

        private void OnPlayerInteracted(PlayerInteractedEvent e)
        {
            if (e.InteractableType == InteractableType.Chest && e.InteractionType == InteractionType.Primary)
            {
                Interact();
            }
        }

        public void Interact()
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
                Destroy(gameObject);
            }
        }

        private void RefreshWeightBar()
        {
            weightBarUI?.SetWeight(oreInventory.CurrentWeight, oreInventory.MaxWeight);
        }
    }
}
