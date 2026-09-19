using System.Collections.Generic;
using System.Linq;
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
        public float CurrentWeight => oreCounts.Sum(kvp => blockTypeDatabase.Get((byte)kvp.Key).Weight * kvp.Value);
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
            return true;
        }

        public Dictionary<BlockTypeId, int> WithdrawAllOre()
        {
            var snapshot = new Dictionary<BlockTypeId, int>(oreCounts);
            ClearOreCounts();
            return snapshot;
        }

        // Drains ore up to `weightBudget` worth (by weight), greedily by dictionary order - the
        // design doc doesn't specify a priority among mineral types for partial storage-drone
        // pickups, so this just takes whatever fits within the budget.
        public Dictionary<BlockTypeId, int> WithdrawUpToWeight(float weightBudget)
        {
            var withdrawn = new Dictionary<BlockTypeId, int>();
            if (weightBudget <= 0f) return withdrawn;
            var remainingWeightBudget = weightBudget;

            foreach (var id in new List<BlockTypeId>(oreCounts.Keys))
            {
                if (remainingWeightBudget <= 0f) break;

                int count = oreCounts[id];
                if (count <= 0) continue;

                var blockType = blockTypeDatabase.Get((byte)id);
                float unitWeight = blockType.Weight;

                int amountToTake = count;
                if (unitWeight > 0f)
                {
                    int affordable = Mathf.FloorToInt(remainingWeightBudget / unitWeight);
                    amountToTake = Mathf.Clamp(affordable, 0, count);
                }
                if (amountToTake <= 0) continue;

                // remove from this inventory
                oreCounts[id] = count - amountToTake;
                remainingWeightBudget -= unitWeight * amountToTake;
                // add to withdrawn snapshot
                withdrawn[id] = amountToTake;
            }

            return withdrawn;
        }

        public void ClearAll()
        {
            ClearOreCounts();
        }

        public void PopulateFromDictionary(IReadOnlyDictionary<BlockTypeId, int> counts)
        {
            ClearOreCounts();
            if (counts == null) return;

            foreach (var kvp in counts)
            {
                if (kvp.Value <= 0) continue;
                oreCounts[kvp.Key] = kvp.Value;

                var blockType = blockTypeDatabase.Get((byte)kvp.Key);
            }
        }

        private void PopulateOreCounts()
        {
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
