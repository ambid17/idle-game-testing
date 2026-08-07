using System;
using System.Collections.Generic;
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
        [SerializeField] private float maxWeight = 100f;

        private readonly Dictionary<BlockTypeId, int> oreCounts = new();

        public event Action InventoryChanged;

        public float MaxWeight => maxWeight;
        public float CurrentWeight { get; private set; }
        public bool IsFull => CurrentWeight >= maxWeight;
        public int ArtifactCount { get; private set; }
        public IReadOnlyDictionary<BlockTypeId, int> OreCounts => oreCounts;

        public bool AddOre(BlockType blockType, int amount = 1)
        {
            if (blockType == null || amount <= 0) return false;

            oreCounts.TryGetValue(blockType.Id, out var current);
            oreCounts[blockType.Id] = current + amount;
            CurrentWeight += blockType.Weight * amount;

            InventoryChanged?.Invoke();
            return true;
        }

        public void AddArtifact()
        {
            ArtifactCount++;
            InventoryChanged?.Invoke();
        }

        // Snapshots and clears carried ore (not artifacts) - called when depositing at the Depot.
        public Dictionary<BlockTypeId, int> WithdrawAllOre()
        {
            var snapshot = new Dictionary<BlockTypeId, int>(oreCounts);
            oreCounts.Clear();
            CurrentWeight = 0f;
            InventoryChanged?.Invoke();
            return snapshot;
        }
    }
}
