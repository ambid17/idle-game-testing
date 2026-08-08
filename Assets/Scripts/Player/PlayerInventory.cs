using System.Collections.Generic;
using Automation;
using Economy;
using Events;
using MapGeneration;
using UnityEngine;

namespace Player
{
    // Carried ore + artifacts per GameDesignDoc "Inventory": each block type has a weight, and once
    // CurrentWeight reaches maxWeight the player can no longer mine Ore-category blocks (enforced by
    // PlayerMining) until they deposit at the Depot. Artifacts don't count against weight and can
    // only be turned in at the Museum, never sold/stored at the Depot.
    //
    // The dictionary/weight bookkeeping itself lives in the shared Economy.OreInventory (also used
    // by MiningAutomaton/StorageDrone) - this class composes it and keeps its own public API
    // unchanged so PlayerMining/DepotUI/HUDUI/InventoryUI need no changes.
    //
    // Also implements IOreCarrier so Storage Drones (automationImplementation.md) can target the
    // player - registers with OreCarrierRegistry alongside its existing PlayerDiedEvent subscription.
    public class PlayerInventory : MonoBehaviour, IOreCarrier
    {
        [SerializeField] private float baseMaxWeight = 100f;

        private OreInventory oreInventory;

        // GameDesignDoc "Market Upgrades > Economy > Inventory": each level adds carrying capacity.
        public float MaxWeight => baseMaxWeight + UpgradeManager.Instance.InventoryCapacityBonus;
        public float CurrentWeight => oreInventory.CurrentWeight;
        public bool IsFull => oreInventory.IsFull;
        public int ArtifactCount { get; private set; }
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
            GameManager.EventService.Add<PlayerDiedEvent>(ClearAll);
            OreCarrierRegistry.Instance.Register(this);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerDiedEvent>(ClearAll);
            OreCarrierRegistry.Instance.Unregister(this);
        }

        // Death wipes everything the player was carrying, ore and artifacts alike.
        public void ClearAll()
        {
            oreInventory.ClearAll();
            ArtifactCount = 0;
            GameManager.EventService.Dispatch<InventoryChangedEvent>();
        }

        public bool AddOre(BlockType blockType, int amount = 1)
        {
            if (!oreInventory.AddOre(blockType, amount)) return false;

            GameManager.EventService.Dispatch<InventoryChangedEvent>();
            return true;
        }

        public void AddArtifact()
        {
            ArtifactCount++;
            GameManager.EventService.Dispatch<InventoryChangedEvent>();
        }

        // Snapshots and clears carried ore (not artifacts) - called when depositing at the Depot.
        public Dictionary<BlockTypeId, int> WithdrawAllOre()
        {
            var snapshot = oreInventory.WithdrawAllOre();
            GameManager.EventService.Dispatch<InventoryChangedEvent>();
            return snapshot;
        }

        // Bulk restore for SaveService - silent (no InventoryChangedEvent) since this only ever
        // runs once at startup before any UI has subscribed.
        public void RestoreFromSaveData(IReadOnlyDictionary<BlockTypeId, int> oreCounts, int artifactCount)
        {
            oreInventory.RestoreFromSaveData(oreCounts);
            ArtifactCount = Mathf.Max(0, artifactCount);
        }
    }
}
