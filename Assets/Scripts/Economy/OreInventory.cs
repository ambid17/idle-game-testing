using System.Collections.Generic;
using MapGeneration;
using UnityEngine;

namespace Economy
{
    // Generic weight-capped ore carrier extracted from Player.PlayerInventory so automatons and
    // storage drones (Automation namespace) can carry ore without duplicating the dictionary/weight
    // bookkeeping. Deliberately dispatches no events - the owning component (PlayerInventory,
    // MiningAutomaton, StorageDrone) dispatches its own change events after calling in here, same
    // as the existing dumb-component convention (UpgradeNodeUI, OreRowUI).
    public class OreInventory : MonoBehaviour
    {
        private readonly Dictionary<BlockTypeId, int> oreCounts = new();
        private System.Func<float> maxWeightProvider;
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        public float CurrentWeight { get; private set; }
        public float MaxWeight => maxWeightProvider != null ? maxWeightProvider() : 0f;
        public bool IsFull => CurrentWeight >= MaxWeight;
        public IReadOnlyDictionary<BlockTypeId, int> OreCounts => oreCounts;

        // Owner injects its own capacity formula (base + upgrades) since that varies per entity type.
        public void Initialize(System.Func<float> maxWeightProvider)
        {
            this.maxWeightProvider = maxWeightProvider;
            PopulateOreCounts();
        }

        public bool AddOre(BlockType blockType, int amount = 1)
        {
            if (blockType == null || amount <= 0) return false;

            oreCounts.TryGetValue(blockType.Id, out var current);
            oreCounts[blockType.Id] = current + amount;
            CurrentWeight += blockType.Weight * amount;
            return true;
        }

        public Dictionary<BlockTypeId, int> WithdrawAllOre()
        {
            var snapshot = new Dictionary<BlockTypeId, int>(oreCounts);
            ClearOreCounts();
            CurrentWeight = 0f;
            return snapshot;
        }

        // Drains ore up to `weightBudget` worth (by weight), greedily by dictionary order - the
        // design doc doesn't specify a priority among mineral types for partial storage-drone
        // pickups, so this just takes whatever fits within the budget.
        public Dictionary<BlockTypeId, int> WithdrawUpToWeight(float weightBudget)
        {
            var withdrawn = new Dictionary<BlockTypeId, int>();
            if (weightBudget <= 0f) return withdrawn;

            foreach (var id in new List<BlockTypeId>(oreCounts.Keys))
            {
                if (weightBudget <= 0f) break;

                int count = oreCounts[id];
                if (count <= 0) continue;

                var blockType = blockTypeDatabase != null ? blockTypeDatabase.Get((byte)id) : null;
                float unitWeight = blockType != null ? blockType.Weight : 0f;

                int amountToTake = count;
                if (unitWeight > 0f)
                {
                    int affordable = Mathf.FloorToInt(weightBudget / unitWeight);
                    amountToTake = Mathf.Clamp(affordable, 0, count);
                }
                if (amountToTake <= 0) continue;

                oreCounts[id] = count - amountToTake;
                CurrentWeight -= unitWeight * amountToTake;
                weightBudget -= unitWeight * amountToTake;
                withdrawn[id] = amountToTake;
            }

            return withdrawn;
        }

        public void ClearAll()
        {
            ClearOreCounts();
            CurrentWeight = 0f;
        }

        private void PopulateOreCounts()
        {
            if (blockTypeDatabase == null) return;

            foreach (var blockType in blockTypeDatabase.BlockTypes)
            {
                if (blockType.Category == BlockCategory.Ore && !oreCounts.ContainsKey(blockType.Id))
                {
                    oreCounts[blockType.Id] = 0;
                }
            }
        }

        private void ClearOreCounts()
        {
            foreach (var key in new List<BlockTypeId>(oreCounts.Keys))
            {
                oreCounts[key] = 0;
            }
        }
    }
}
