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

        private readonly Dictionary<string, int> levelsByUpgradeName = new();

        // Purchased levels only - never includes a PrestigeUpgradeManager "kept tier" baseline.
        // Kept separate from GetLevel so persistence (SetLevel/AllLevels) only ever deals in what
        // the player actually bought at the Market, not perk-granted baselines.
        private int RawLevel(UpgradeDefinition def) => def != null && levelsByUpgradeName.TryGetValue(def.DisplayName, out var lvl) ? lvl : 0;

        // GameDesignDoc "Prestige > idle": purchased level plus any "keep tier" prestige perk
        // baseline for this effect, capped at MaxLevel. After PrestigeManager.ExecutePrestige wipes
        // levelsByUpgradeId, this is what lets a kept perk make the Market upgrade start above 0 -
        // GetCost (fed this same combined level) then resumes the cost curve at the kept tier
        // instead of restarting at tier-0 prices.
        public int GetLevelIncludingPrestige(UpgradeDefinition def) => def != null ? Mathf.Min(def.MaxLevel, RawLevel(def) + KeptBaseline(def)) : 0;

        private int LevelOf(UpgradeEffect effect) => GetLevelIncludingPrestige(database.Find(effect));

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

            int newLevel = GetLevelIncludingPrestige(def) + 1;
            levelsByUpgradeName[def.DisplayName] = newLevel;
            GameManager.EventService.Dispatch(new UpgradePurchasedEvent(def, newLevel));
            return true;
        }

        // Bulk restore for SaveService - silent (no UpgradePurchasedEvent) since AutomationSpawner
        // reconciles entity counts once after the whole save file is applied, not per-level.
        public void SetLevelFromSave(string upgradeId, int level)
        {
            if (string.IsNullOrEmpty(upgradeId) || level < 0) return;
            levelsByUpgradeName[upgradeId] = level;
            GameManager.EventService.Dispatch(new UpgradeLoadedEvent(database.Find(upgradeId), level));
        }

        // Maps a Market UpgradeEffect onto its matching PrestigeUpgradeManager "keep tier" perk, if
        // any. Only the Idle-branch automaton stats have a kept-tier perk today.
        private int KeptBaseline(UpgradeDefinition def)
        {
            var prestige = PrestigeUpgradeManager.Instance;
            return def.Effect switch
            {
                UpgradeEffect.AutomatonCount => prestige.KeptAutomatonCountBaseline,
                UpgradeEffect.AutomatonMiningSpeed => prestige.KeptAutomatonMiningSpeedBaseline,
                UpgradeEffect.AutomatonMiningRadius => prestige.KeptAutomatonMiningRadiusBaseline,
                UpgradeEffect.AutomatonMoveSpeed => prestige.KeptAutomatonMoveSpeedBaseline,
                _ => 0
            };
        }

        public bool IsMaxed(UpgradeDefinition def) => def != null && GetLevelIncludingPrestige(def) >= def.MaxLevel;

        // "Upgrades will be a skill tree ... requires the player to unlock the previous tier":
        // a plain prerequisite just needs one level purchased; capstones (RequirePrerequisiteMaxed)
        // need the prerequisite fully maxed first.
        public bool IsUnlocked(UpgradeDefinition def)
        {
            if (def == null) return false;
            if (def.Prerequisite == null) return true;
            return def.RequirePrerequisiteMaxed ? IsMaxed(def.Prerequisite) : GetLevelIncludingPrestige(def.Prerequisite) > 0;
        }

        public double GetNextCost(UpgradeDefinition def) => def.GetCost(GetLevelIncludingPrestige(def));

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

        public IEnumerable<KeyValuePair<string, int>> AllLevels => levelsByUpgradeName;

        // GameDesignDoc "# Prestige": hard reset of all purchased Market upgrade levels, called by
        // PrestigeManager.ExecutePrestige. Any "keep tier" prestige perk baselines still apply
        // afterward via GetLevel/KeptBaseline - this only clears what the player purchased with
        // Dollars this run.
        public void ResetAllLevels() => levelsByUpgradeName.Clear();


        #region Utils
        // GameDesignDoc "Mining > Increase mining size": current cumulative upgrade level: fed
        // into MiningAreaPattern.GetOffsets by PlayerMining to know which extra cells to mine.
        public int MiningAreaLevel => LevelOf(UpgradeEffect.MiningAreaRadius);

        // GameDesignDoc "Mining > Increase mining speed": "each tier adds 10% mining speed".
        public float MiningSpeedMultiplier => 1f + LevelOf(UpgradeEffect.MiningSpeed) * EffectValuePerLevelOf(UpgradeEffect.MiningSpeed);

        // GameDesignDoc "Mining > Increase mining speed": "the final upgrade makes dirt/stone an
        // instant mine" - interpreted as the Dirt category (the valueless filler blocks), since
        // the Ore-category "Stone" block is a sellable mineral, not filler. Its own capstone
        // (Mining_DirtInstaMine), not a side effect of maxing Mining Speed.
        public bool InstantMineDirt => IsMaxedEffect(UpgradeEffect.DirtInstaMineUnlock);

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
        #endregion
    }
}
