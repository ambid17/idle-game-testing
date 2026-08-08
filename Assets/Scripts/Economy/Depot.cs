using System.Collections.Generic;
using Events;
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

        public IReadOnlyDictionary<BlockTypeId, int> StoredOres => storedOres;

        public void Deposit(IReadOnlyDictionary<BlockTypeId, int> ores)
        {
            if (ores == null || ores.Count == 0) return;

            foreach (var kvp in ores)
            {
                storedOres.TryGetValue(kvp.Key, out var current);
                storedOres[kvp.Key] = current + kvp.Value;
            }

            GameManager.EventService.Dispatch<DepotChangedEvent>();
        }

        public void Deposit(BlockTypeId id, int amount)
        {
            if (amount <= 0) return;
            storedOres.TryGetValue(id, out var current);
            storedOres[id] = current + amount;
            GameManager.EventService.Dispatch<DepotChangedEvent>();
        }

        // Sells `fraction` (0-1] of the stored amount for this ore type, crediting the Wallet.
        // Per GameDesignDoc: "sell any percentage of a certain type, or all of them."
        public double Sell(BlockTypeId id, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (fraction <= 0f) return 0;
            if (!storedOres.TryGetValue(id, out var current) || current <= 0) return 0;

            int amountToSell = fraction >= 1f ? current : Mathf.Clamp(Mathf.RoundToInt(current * fraction), 1, current);

            var blockType = blockTypeDatabase.Get((byte)id);
            if (blockType == null)
            {
                Debug.LogError($"Depot.Sell: BlockTypeDatabase missing or BlockTypeId {id} not found. Cannot sell.");
                return 0;
            }
            double value = blockType.Value * UpgradeManager.Instance.SellValueMultiplier * amountToSell;

            int remaining = current - amountToSell;
            storedOres[id] = Mathf.Max(0, remaining);

            if (value > 0 && Wallet.Instance != null) Wallet.Instance.Add(value);
            GameManager.EventService.Dispatch<DepotChangedEvent>();
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

        // GameDesignDoc "# Prestige": wipes all stored minerals/processed goods for
        // PrestigeManager.ExecutePrestige's hard reset. Kept distinct from RestoreFromSaveData even
        // though the body is identical - "restore from save" and "wipe for a live prestige" are
        // different intents, and dispatches DepotChangedEvent (unlike the silent save-restore path)
        // since the Depot UI may be live-refreshing.
        public void ClearAll()
        {
            storedOres.Clear();
            GameManager.EventService.Dispatch<DepotChangedEvent>();
        }

        // Bulk restore for SaveService - silent (no DepotChangedEvent) since this only ever runs
        // once at startup before any UI has subscribed.
        public void RestoreFromSaveData(IReadOnlyDictionary<BlockTypeId, int> ores)
        {
            storedOres.Clear();
            if (ores == null) return;

            foreach (var kvp in ores)
            {
                storedOres[kvp.Key] = kvp.Value;
            }
        }
    }
}
