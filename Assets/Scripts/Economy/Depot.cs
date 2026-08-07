using System;
using System.Collections.Generic;
using MapGeneration;
using UnityEngine;

namespace Economy
{
    // Storage depot per GameDesignDoc "Map Layout > buildings > storage depot": the global bank of
    // accrued minerals (deposited by the player and, eventually, idle miners). Selling reads
    // BlockType.Value from the shared BlockTypeDatabase and credits the Wallet. Singleton so it
    // survives player death/scene reload and needs no manual scene wiring.
    public class Depot : Singleton<Depot>
    {
        private static BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        private readonly Dictionary<BlockTypeId, int> storedOres = new();

        public event Action DepotChanged;

        public IReadOnlyDictionary<BlockTypeId, int> StoredOres => storedOres;

        public void Deposit(IReadOnlyDictionary<BlockTypeId, int> ores)
        {
            if (ores == null || ores.Count == 0) return;

            foreach (var kvp in ores)
            {
                storedOres.TryGetValue(kvp.Key, out var current);
                storedOres[kvp.Key] = current + kvp.Value;
            }

            DepotChanged?.Invoke();
        }

        public void Deposit(BlockTypeId id, int amount)
        {
            if (amount <= 0) return;
            storedOres.TryGetValue(id, out var current);
            storedOres[id] = current + amount;
            DepotChanged?.Invoke();
        }

        // Sells `fraction` (0-1] of the stored amount for this ore type, crediting the Wallet.
        // Per GameDesignDoc: "sell any percentage of a certain type, or all of them."
        public double Sell(BlockTypeId id, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (fraction <= 0f) return 0;
            if (!storedOres.TryGetValue(id, out var current) || current <= 0) return 0;

            int amountToSell = fraction >= 1f ? current : Mathf.Clamp(Mathf.RoundToInt(current * fraction), 1, current);

            var blockType = blockTypeDatabase != null ? blockTypeDatabase.Get((byte)id) : null;
            double value = (blockType != null ? blockType.Value : 0f) * amountToSell;

            int remaining = current - amountToSell;
            if (remaining <= 0) storedOres.Remove(id);
            else storedOres[id] = remaining;

            if (value > 0 && Wallet.Instance != null) Wallet.Instance.Add(value);
            DepotChanged?.Invoke();
            return value;
        }

        public double SellAll()
        {
            double total = 0;
            foreach (var id in new List<BlockTypeId>(storedOres.Keys))
            {
                total += Sell(id, 1f);
            }
            return total;
        }
    }
}
