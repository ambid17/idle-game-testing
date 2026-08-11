using System.Collections.Generic;
using Events;
using MapGeneration;
using Processing;
using UnityEngine;

namespace Economy
{
    // Storage depot per GameDesignDoc "Map Layout > buildings > storage depot": the global bank of
    // accrued minerals (deposited by the player and, eventually, idle miners). Selling reads
    // BlockType.Value from the shared BlockTypeDatabase and credits the Wallet. Singleton so it
    // survives player death/scene reload and needs no manual scene wiring.
    //
    // Also banks crafted goods from the Processing Center (processingImplementation.md) in a
    // second, parallel dictionary keyed by ProcessingRecipeId rather than folding them into
    // storedOres/BlockTypeId - goods aren't terrain/ore, and BlockType carries mining-only fields
    // (Tile/Health/Weight/HazardBehavior) that don't apply to them.
    public class Depot : Singleton<Depot>
    {
        private static BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;
        private static ProcessingRecipeDatabase recipeDatabase => GameManager.ProcessingRecipeDatabase;

        private readonly Dictionary<BlockTypeId, int> storedOres = new();
        private readonly Dictionary<ProcessingRecipeId, int> storedGoods = new();

        public IReadOnlyDictionary<BlockTypeId, int> StoredOres => storedOres;
        public IReadOnlyDictionary<ProcessingRecipeId, int> StoredGoods => storedGoods;

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

        // Atomically checks-then-deducts every entry, for ProcessingManager.StartJob pulling a
        // recipe's ingredients - either the whole batch is affordable and gets consumed, or
        // nothing is touched. Single DepotChangedEvent on success, matching the bulk Deposit
        // overload above.
        public bool TryConsume(IReadOnlyDictionary<BlockTypeId, int> amounts)
        {
            if (amounts == null || amounts.Count == 0) return false;

            foreach (var kvp in amounts)
            {
                storedOres.TryGetValue(kvp.Key, out var current);
                if (current < kvp.Value) return false;
            }

            foreach (var kvp in amounts)
            {
                storedOres[kvp.Key] -= kvp.Value;
            }

            GameManager.EventService.Dispatch<DepotChangedEvent>();
            return true;
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

        // Called by ProcessingManager when a job completes - crafted goods are banked here rather
        // than auto-sold, so the player sells them manually just like mined ore.
        public void DepositGood(ProcessingRecipeId id, int amount)
        {
            if (amount <= 0) return;
            storedGoods.TryGetValue(id, out var current);
            storedGoods[id] = current + amount;
            GameManager.EventService.Dispatch<DepotChangedEvent>();
        }

        // Mirrors Sell, but reads ProcessingRecipeDefinition.SaleValue from the
        // ProcessingRecipeDatabase and applies UpgradeManager.ProcessingGoodsSellMultiplier
        // instead of the ore SellValueMultiplier, so the two upgrade paths stay independent.
        public double SellGood(ProcessingRecipeId id, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (fraction <= 0f) return 0;
            if (!storedGoods.TryGetValue(id, out var current) || current <= 0) return 0;

            int amountToSell = fraction >= 1f ? current : Mathf.Clamp(Mathf.RoundToInt(current * fraction), 1, current);

            var recipe = recipeDatabase != null ? recipeDatabase.Get(id) : null;
            if (recipe == null)
            {
                Debug.LogError($"Depot.SellGood: ProcessingRecipeDatabase missing or ProcessingRecipeId {id} not found. Cannot sell.");
                return 0;
            }
            double value = recipe.SaleValue * UpgradeManager.Instance.ProcessingGoodsSellMultiplier * amountToSell;

            int remaining = current - amountToSell;
            storedGoods[id] = Mathf.Max(0, remaining);

            if (value > 0 && Wallet.Instance != null) Wallet.Instance.Add(value);
            GameManager.EventService.Dispatch<DepotChangedEvent>();
            return value;
        }

        public double SellAllGoods()
        {
            double total = 0;
            foreach (var id in new List<ProcessingRecipeId>(storedGoods.Keys))
            {
                total += SellGood(id, 1f);
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
            storedGoods.Clear();
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

        // Bulk restore for SaveService - silent, mirrors RestoreFromSaveData above.
        public void RestoreGoodsFromSaveData(IReadOnlyDictionary<ProcessingRecipeId, int> goods)
        {
            storedGoods.Clear();
            if (goods == null) return;

            foreach (var kvp in goods)
            {
                storedGoods[kvp.Key] = kvp.Value;
            }
        }
    }
}
