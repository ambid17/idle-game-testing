using System.Collections.Generic;
using Events;
using UnityEngine;

namespace Economy
{
    // Tracks purchased levels for every UpgradeDefinition in GameManager.UpgradeDatabase and
    // exposes the resulting gameplay values. Per GameDesignDoc "Market Upgrades": "all regular
    // upgrades are purchased at the market using Dollars". Consumers (PlayerMining,
    // PlayerInventory, Depot, MapGenerationService) pull the current effect value from the
    // properties below on demand rather than being pushed updates, so purchase order/timing never
    // matters to them. Singleton so it needs no scene wiring, matching Wallet/Depot.
    public class UpgradeManager : Singleton<UpgradeManager>
    {
        private static UpgradeDatabase database => GameManager.UpgradeDatabase;

        private readonly Dictionary<string, int> levelsByUpgradeId = new();

        public int GetLevel(UpgradeDefinition def) => def != null && levelsByUpgradeId.TryGetValue(def.Id, out var lvl) ? lvl : 0;

        public bool IsMaxed(UpgradeDefinition def) => def != null && GetLevel(def) >= def.MaxLevel;

        // "Upgrades will be a skill tree ... requires the player to unlock the previous tier":
        // a plain prerequisite just needs one level purchased; capstones (RequirePrerequisiteMaxed)
        // need the prerequisite fully maxed first.
        public bool IsUnlocked(UpgradeDefinition def)
        {
            if (def == null) return false;
            if (def.Prerequisite == null) return true;
            return def.RequirePrerequisiteMaxed ? IsMaxed(def.Prerequisite) : GetLevel(def.Prerequisite) > 0;
        }

        public double GetNextCost(UpgradeDefinition def) => def.GetCost(GetLevel(def));

        public bool CanPurchase(UpgradeDefinition def)
        {
            if (def == null || IsMaxed(def) || !IsUnlocked(def)) return false;
            return Wallet.Instance.Dollars >= GetNextCost(def);
        }

        public bool TryPurchase(UpgradeDefinition def)
        {
            if (!CanPurchase(def)) return false;

            double cost = GetNextCost(def);
            if (!Wallet.Instance.TrySpend(cost)) return false;

            int newLevel = GetLevel(def) + 1;
            levelsByUpgradeId[def.Id] = newLevel;
            GameManager.EventService.Dispatch(new UpgradePurchasedEvent(def, newLevel));
            return true;
        }

        private int LevelOf(UpgradeEffect effect) => GetLevel(database.Find(effect));

        private float EffectValuePerLevelOf(UpgradeEffect effect)
        {
            var def = database.Find(effect);
            if(def == null)
            {
                Debug.LogError($"UpgradeManager.ValuePerLevelOf: No UpgradeDefinition found for effect {effect}. Check that the UpgradeDatabase is properly populated.");
                return 0;
            }
            return def.EffectValuePerLevel;
        }

        private bool IsMaxedEffect(UpgradeEffect effect)
        {
            var def = database.Find(effect);
            if (def == null)
            {
                Debug.LogError($"UpgradeManager.IsMaxedEffect: No UpgradeDefinition found for effect {effect}. Check that the UpgradeDatabase is properly populated.");
                return true;
            }
            return IsMaxed(def);
        }

        // GameDesignDoc "Mining > Increase mining size": current cumulative upgrade level: fed
        // into MiningAreaPattern.GetOffsets by PlayerMining to know which extra cells to mine.
        public int MiningAreaLevel => LevelOf(UpgradeEffect.MiningAreaRadius);

        // GameDesignDoc "Mining > Increase mining speed": "each tier adds 10% mining speed".
        public float MiningSpeedMultiplier => 1f + LevelOf(UpgradeEffect.MiningSpeed) * EffectValuePerLevelOf(UpgradeEffect.MiningSpeed);

        // GameDesignDoc "Mining > Increase mining speed": "the final upgrade makes dirt/stone an
        // instant mine" - interpreted as the Dirt category (the valueless filler blocks), since
        // the Ore-category "Stone" block is a sellable mineral, not filler.
        public bool InstantMineDirt => IsMaxedEffect(UpgradeEffect.MiningSpeed);

        // GameDesignDoc "Mining > Insta-mine chance".
        public float InstaMineChance => LevelOf(UpgradeEffect.InstaMineChance) * EffectValuePerLevelOf(UpgradeEffect.InstaMineChance);

        // GameDesignDoc "Mining > Lantern": extra fog-of-war reveal radius on top of the base.
        public int LanternFogRadiusBonus => Mathf.RoundToInt(LevelOf(UpgradeEffect.LanternFogRadius) * EffectValuePerLevelOf(UpgradeEffect.LanternFogRadius));

        // GameDesignDoc "Lantern capstones > true sight: reveals all fog of war".
        public bool TrueSightUnlocked => IsMaxedEffect(UpgradeEffect.LanternTrueSight);

        // GameDesignDoc "Economy > Inventory: increase the player's max carrying weight".
        public float InventoryCapacityBonus => LevelOf(UpgradeEffect.InventoryCapacity) * EffectValuePerLevelOf(UpgradeEffect.InventoryCapacity);

        // GameDesignDoc "Economy > Marketing: increase sales value of minerals".
        public float SellValueMultiplier => 1f + LevelOf(UpgradeEffect.MarketingSellMultiplier) * EffectValuePerLevelOf(UpgradeEffect.MarketingSellMultiplier);

        // GameDesignDoc "Economy > Overflow: once inventory is full, you can continue to mine and
        // ores will auto-sell at a reduced value".
        public bool OverflowUnlocked => IsMaxedEffect(UpgradeEffect.Overflow);

        public float OverflowSellFraction
        {
            get
            {
                var def = database != null ? database.Find(UpgradeEffect.Overflow) : null;
                return def != null ? def.EffectValuePerLevel : 0f;
            }
        }

        // GameDesignDoc "Automation > Mining Automaton": level 0 = no automatons owned, matching
        // every other UpgradeManager effect - the first purchased level buys the first unit.
        public int AutomatonCount => LevelOf(UpgradeEffect.AutomatonCount);
        public float AutomatonMiningSpeedMultiplier => 1f + LevelOf(UpgradeEffect.AutomatonMiningSpeed) * EffectValuePerLevelOf(UpgradeEffect.AutomatonMiningSpeed);
        public float AutomatonMoveSpeedMultiplier => 1f + LevelOf(UpgradeEffect.AutomatonMoveSpeed) * EffectValuePerLevelOf(UpgradeEffect.AutomatonMoveSpeed);
        public int AutomatonMiningRadiusBonus => Mathf.RoundToInt(LevelOf(UpgradeEffect.AutomatonMiningRadius) * EffectValuePerLevelOf(UpgradeEffect.AutomatonMiningRadius));
        public float AutomatonInventoryCapacityMultiplier => 1f + LevelOf(UpgradeEffect.AutomatonInventoryCapacity) * EffectValuePerLevelOf(UpgradeEffect.AutomatonInventoryCapacity);

        // GameDesignDoc "Automation > Storage Drone".
        public int StorageDroneCount => LevelOf(UpgradeEffect.StorageDroneCount);
        public float StorageDroneMoveSpeedMultiplier => 1f + LevelOf(UpgradeEffect.StorageDroneMoveSpeed) * EffectValuePerLevelOf(UpgradeEffect.StorageDroneMoveSpeed);
        public float StorageDroneInventoryCapacityMultiplier => 1f + LevelOf(UpgradeEffect.StorageDroneInventoryCapacity) * EffectValuePerLevelOf(UpgradeEffect.StorageDroneInventoryCapacity);

        // GameDesignDoc "Automation > Fuel Drone".
        public int FuelDroneCount => LevelOf(UpgradeEffect.FuelDroneCount);
        public float FuelDroneMoveSpeedMultiplier => 1f + LevelOf(UpgradeEffect.FuelDroneMoveSpeed) * EffectValuePerLevelOf(UpgradeEffect.FuelDroneMoveSpeed);
        public float FuelDroneInventoryCapacityMultiplier => 1f + LevelOf(UpgradeEffect.FuelDroneInventoryCapacity) * EffectValuePerLevelOf(UpgradeEffect.FuelDroneInventoryCapacity);

        // Bulk restore for SaveService - silent (no UpgradePurchasedEvent) since AutomationSpawner
        // reconciles entity counts once after the whole save file is applied, not per-level.
        public void SetLevel(string upgradeId, int level)
        {
            if (string.IsNullOrEmpty(upgradeId) || level < 0) return;
            levelsByUpgradeId[upgradeId] = level;
        }

        public IEnumerable<KeyValuePair<string, int>> AllLevels => levelsByUpgradeId;
    }
}
