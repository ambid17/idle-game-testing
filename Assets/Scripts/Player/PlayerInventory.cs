using System.Collections.Generic;
using Automation;
using Economy;
using Events;
using MapGeneration;
using UnityEngine;

namespace Player
{
    // Carried ore per GameDesignDoc "Inventory": each block type has a weight, and once
    // CurrentWeight reaches maxWeight the player can no longer mine Ore-category blocks (enforced by
    // PlayerMining) until they deposit at the Depot. Artifacts are banked directly to Wallet
    // (Economy.Wallet) instead of being carried here - see PlayerMining.CollectMinedBlock.
    //
    // The dictionary/weight bookkeeping itself lives in the shared Economy.OreInventory (also used
    // by MiningAutomaton/StorageDrone) - this class composes it and keeps its own public API
    // unchanged so PlayerMining/DepotUI/HUDUI/InventoryUI need no changes.
    //
    // Also implements IOreCarrier so Storage Drones (automationImplementation.md) can target the
    // player - registers with OreCarrierRegistry alongside its existing PlayerDiedEvent subscription.
    public class PlayerInventory : MonoBehaviour, IOreCarrier
    {
        private float baseMaxWeight = 50f;

        private OreInventory oreInventory;

        // GameDesignDoc "Market Upgrades > Economy > Inventory": each level adds carrying capacity.
        public float MaxWeight => baseMaxWeight + UpgradeManager.Instance.Economy_InventoryCapacityBonus;
        public float CurrentWeight => oreInventory.CurrentWeight;
        public bool IsFull => oreInventory.IsFull;
        public IReadOnlyDictionary<BlockTypeId, int> OreCounts => oreInventory.OreCounts;

        public Transform CarrierTransform => transform;
        public OreInventory Inventory => oreInventory;

        private void Awake()
        {
            // Self-heals rather than [RequireComponent] so the existing Player prefab doesn't need
            // a manual Editor step to pick up the new shared component.
            oreInventory = GetComponent<OreInventory>();
            if (oreInventory == null) oreInventory = gameObject.AddComponent<OreInventory>();
        }

        private void Start()
        {
            oreInventory.Initialize(() => MaxWeight);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerDiedEvent>(HandleDeath);

            OreCarrierRegistry.Instance.Register(this);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerDiedEvent>(HandleDeath);
            // HasInstance guard: teardown order across objects isn't guaranteed, so the registry's
            // singleton may already be destroyed by the time this runs. Instance would resurrect
            // it as a stray GameObject mid-unload - HasInstance doesn't.
            if (OreCarrierRegistry.HasInstance) OreCarrierRegistry.Instance.Unregister(this);
        }

        // Death drops everything the player was carrying into a chest at the death location
        // (Economy.ChestSpawner reacts to the dispatched event) instead of just discarding it.
        // Artifacts are unaffected - they're banked in Wallet, not carried here.
        private void HandleDeath(PlayerDiedEvent evt)
        {
            var droppedOre = oreInventory.WithdrawAllOre();
            GameManager.EventService.Dispatch<InventoryChangedEvent>();
            GameManager.EventService.Dispatch(new PlayerInventoryDroppedEvent(droppedOre));
        }

        public bool AddOre(BlockType blockType, int amount = 1)
        {
            if (!oreInventory.AddOre(blockType, amount)) return false;

            GameManager.EventService.Dispatch<InventoryChangedEvent>();
            return true;
        }

        // Snapshots and clears carried ore - called when depositing at the Depot.
        public Dictionary<BlockTypeId, int> WithdrawAllOre()
        {
            var snapshot = oreInventory.WithdrawAllOre();
            GameManager.EventService.Dispatch<InventoryChangedEvent>();
            return snapshot;
        }

        // Used by PrestigeManager.ExecutePrestige, which wipes Depot/carried ore on prestige.
        public void ClearOreOnly()
        {
            oreInventory.ClearAll();
            GameManager.EventService.Dispatch<InventoryChangedEvent>();
        }

        // Bulk restore for SaveService - silent (no InventoryChangedEvent) since this only ever
        // runs once at startup before any UI has subscribed.
        public void RestoreFromSaveData(IReadOnlyDictionary<BlockTypeId, int> oreCounts)
        {
            oreInventory.PopulateFromDictionary(oreCounts);
            GameManager.EventService.Dispatch<InventoryChangedEvent>();
        }
    }
}
