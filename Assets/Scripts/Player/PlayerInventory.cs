using System.Collections.Generic;
using System.Linq;
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
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private float baseMaxWeight = 100f;

        private readonly Dictionary<BlockTypeId, int> oreCounts = new();

        // GameDesignDoc "Market Upgrades > Economy > Inventory": each level adds carrying capacity.
        public float MaxWeight => baseMaxWeight + UpgradeManager.Instance.InventoryCapacityBonus;
        public float CurrentWeight { get; private set; }
        public bool IsFull => CurrentWeight >= MaxWeight;
        public int ArtifactCount { get; private set; }
        public IReadOnlyDictionary<BlockTypeId, int> OreCounts => oreCounts;
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        private void Start()
        {
            PopulateOreCounts();
        }

        private void OnEnable() => GameManager.EventService.Add<PlayerDiedEvent>(ClearAll);
        private void OnDisable() => GameManager.EventService.Remove<PlayerDiedEvent>(ClearAll);

        // Death wipes everything the player was carrying, ore and artifacts alike.
        public void ClearAll()
        {
            ClearOreCounts();
            CurrentWeight = 0f;
            ArtifactCount = 0;
            GameManager.EventService.Dispatch<InventoryChangedEvent>();
        }

        public bool AddOre(BlockType blockType, int amount = 1)
        {
            if (blockType == null || amount <= 0) return false;

            oreCounts.TryGetValue(blockType.Id, out var current);
            oreCounts[blockType.Id] = current + amount;
            CurrentWeight += blockType.Weight * amount;

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
            var snapshot = new Dictionary<BlockTypeId, int>(oreCounts);
            ClearOreCounts();
            CurrentWeight = 0f;
            GameManager.EventService.Dispatch<InventoryChangedEvent>();
            return snapshot;
        }

        private void PopulateOreCounts()
        {
            foreach (var blockType in blockTypeDatabase.BlockTypes)
            {
                if (blockType.Category == BlockCategory.Ore)
                {
                    oreCounts[blockType.Id] = 0;
                }
            }
        }

        private void ClearOreCounts()
        {
            foreach(var key in oreCounts.Keys.ToList())
            {
                oreCounts[key] = 0;
            }
        }
    }
}
