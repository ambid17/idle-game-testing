using Events;
using UnityEngine;

namespace Economy
{
    // Tracks purchased levels for every UpgradeDefinition in GameManager.UpgradeDatabase and
    // exposes the resulting gameplay values. Per GameDesignDoc "Market Upgrades": "all regular
    // upgrades are purchased at the market using Dollars". Consumers (PlayerMining, PlayerController,
    // PlayerInventory, Depot, MapGenerationService, ...) pull the current effect value from the
    // properties below on demand rather than being pushed updates, so purchase order/timing never
    // matters to them. Singleton so it needs no scene wiring, matching Wallet/Depot.
    public class UpgradeManager : UpgradeManagerBase<UpgradeManager, UpgradeDefinition, UpgradeEffect>
    {
        private static UpgradeDatabase database => GameManager.UpgradeDatabase;

        public Sprite CurrencyIcon;

        protected override double CurrentCurrency => Wallet.Instance.Dollars;
        protected override bool TrySpendCurrency(double amount) => Wallet.Instance.TrySpend(amount);
        protected override string KeyOf(UpgradeDefinition def) => def.DisplayName;
        protected override UpgradeDefinition Find(UpgradeEffect effect) => database.Find(effect);
        protected override UpgradeDefinition Find(string key) => database.Find(key);
        protected override UpgradeDefinition PrerequisiteOf(UpgradeDefinition def) => def.Prerequisite as UpgradeDefinition;
        protected override void DispatchPurchased(UpgradeDefinition def, int newLevel) =>
            GameManager.EventService.Dispatch(new UpgradePurchasedEvent(def, newLevel));
        protected override void DispatchLoaded(UpgradeDefinition def, int newLevel) =>
            GameManager.EventService.Dispatch(new UpgradeLoadedEvent(def, newLevel));

        // GameDesignDoc "Prestige > idle": purchased level plus any "keep tier" prestige perk
        // baseline for this effect, capped at MaxLevel. After PrestigeManager.ExecutePrestige wipes
        // purchased levels, this is what lets a kept perk make the Market upgrade start above 0 -
        // GetNextCost (fed this same combined level) then resumes the cost curve at the kept tier
        // instead of restarting at tier-0 prices.
        protected override int EffectiveLevel(UpgradeDefinition def) =>
            def != null ? Mathf.Min(def.MaxLevel, RawLevel(def) + KeptBaseline(def)) : 0;

        // Kept as its previous public name since UI/other systems already call this directly.
        public int GetLevelIncludingPrestige(UpgradeDefinition def) => EffectiveLevel(def);

        // Bulk restore for SaveService - silent (no UpgradePurchasedEvent) since AutomationSpawner
        // reconciles entity counts once after the whole save file is applied, not per-level.
        public void SetLevelFromSave(string upgradeId, int level) => SetLevel(upgradeId, level);

        // Maps a Market UpgradeEffect onto its matching PrestigeUpgradeManager "keep tier" perk, if
        // any. Only the Idle-branch automaton stats have a kept-tier perk today.
        private int KeptBaseline(UpgradeDefinition def)
        {
            var prestige = PrestigeUpgradeManager.Instance;
            return def.Effect switch
            {
                UpgradeEffect.Automation_AutomatonCount => prestige.Idle_KeptAutomatonCountBaseline,
                UpgradeEffect.Automation_AutomatonMiningSpeed => prestige.Idle_KeptAutomatonMiningSpeedBaseline,
                UpgradeEffect.Automation_AutomatonMiningRadius => prestige.Idle_KeptAutomatonMiningRadiusBaseline,
                UpgradeEffect.Automation_AutomatonMoveSpeed => prestige.Idle_KeptAutomatonMoveSpeedBaseline,
                _ => 0
            };
        }

        // GameDesignDoc "# Prestige": hard reset of all purchased Market upgrade levels, called by
        // PrestigeManager.ExecutePrestige. Any "keep tier" prestige perk baselines still apply
        // afterward via EffectiveLevel/KeptBaseline - this only clears what the player purchased
        // with Dollars this run.
        public void ResetAllLevels() => ClearLevels();

        #region Utils
        // GameDesignDoc "Automation > Mining Automaton": level 0 = no automatons owned, matching
        // every other UpgradeManager effect - the first purchased level buys the first unit.
        public int Automation_AutomatonCount => LevelOf(UpgradeEffect.Automation_AutomatonCount);
        public float Automation_AutomatonInventoryCapacityMultiplier => 1f + LevelOf(UpgradeEffect.Automation_AutomatonInventoryCapacity) * EffectValuePerLevelOf(UpgradeEffect.Automation_AutomatonInventoryCapacity);
        public int Automation_AutomatonMiningRadiusBonus => Mathf.RoundToInt(LevelOf(UpgradeEffect.Automation_AutomatonMiningRadius) * EffectValuePerLevelOf(UpgradeEffect.Automation_AutomatonMiningRadius));
        public float Automation_AutomatonMiningSpeedMultiplier => 1f + LevelOf(UpgradeEffect.Automation_AutomatonMiningSpeed) * EffectValuePerLevelOf(UpgradeEffect.Automation_AutomatonMiningSpeed);
        public float Automation_AutomatonMoveSpeedMultiplier => 1f + LevelOf(UpgradeEffect.Automation_AutomatonMoveSpeed) * EffectValuePerLevelOf(UpgradeEffect.Automation_AutomatonMoveSpeed);

        // GameDesignDoc "Automation > Fuel Drone".
        public int Automation_FuelDroneCount => LevelOf(UpgradeEffect.Automation_FuelDroneCount);
        public float Automation_FuelDroneInventoryCapacityMultiplier => 1f + LevelOf(UpgradeEffect.Automation_FuelDroneInventoryCapacity) * EffectValuePerLevelOf(UpgradeEffect.Automation_FuelDroneInventoryCapacity);
        public float Automation_FuelDroneMoveSpeedMultiplier => 1f + LevelOf(UpgradeEffect.Automation_FuelDroneMoveSpeed) * EffectValuePerLevelOf(UpgradeEffect.Automation_FuelDroneMoveSpeed);

        // GameDesignDoc "Automation > Drone delivery > Market Sense" capstone: once maxed,
        // StorageDrone sells its delivered ore at the Depot immediately instead of leaving it
        // stored raw.
        public bool Automation_StorageDroneAutoSellUnlocked => IsMaxedEffect(UpgradeEffect.Automation_StorageDroneAutoSellUnlock);

        // GameDesignDoc "Automation > Storage Drone".
        public int Automation_StorageDroneCount => LevelOf(UpgradeEffect.Automation_StorageDroneCount);
        public float Automation_StorageDroneInventoryCapacityMultiplier => 1f + LevelOf(UpgradeEffect.Automation_StorageDroneInventoryCapacity) * EffectValuePerLevelOf(UpgradeEffect.Automation_StorageDroneInventoryCapacity);
        public float Automation_StorageDroneMoveSpeedMultiplier => 1f + LevelOf(UpgradeEffect.Automation_StorageDroneMoveSpeed) * EffectValuePerLevelOf(UpgradeEffect.Automation_StorageDroneMoveSpeed);

        // GameDesignDoc "Economy > Inventory: increase the player's max carrying weight".
        public float Economy_InventoryCapacityBonus => LevelOf(UpgradeEffect.Economy_InventoryCapacity) * EffectValuePerLevelOf(UpgradeEffect.Economy_InventoryCapacity);

        public float Economy_OverflowSellFraction
        {
            get
            {
                var def = database != null ? database.Find(UpgradeEffect.Economy_Overflow) : null;
                return def != null ? def.EffectValuePerLevel : 0f;
            }
        }

        // GameDesignDoc "Economy > Overflow: once inventory is full, you can continue to mine and
        // ores will auto-sell at a reduced value".
        public bool Economy_OverflowUnlocked => IsMaxedEffect(UpgradeEffect.Economy_Overflow);

        // GameDesignDoc "Economy > Marketing: increase sales value of minerals".
        public float Economy_SellValueMultiplier => 1f + LevelOf(UpgradeEffect.Economy_MarketingSellMultiplier) * EffectValuePerLevelOf(UpgradeEffect.Economy_MarketingSellMultiplier);

        // GameDesignDoc "Mining > Increase mining size" (vein mining): current cumulative upgrade
        // level: fed into Player.VeinMiningPattern.GetChainCells by PlayerMining to know how many
        // connected Ore cells the free chain can reach.
        public int Mining_AreaLevel => LevelOf(UpgradeEffect.Mining_AreaSize);

        // GameDesignDoc "Lantern capstones > zoom, enhance": additive camera zoom-out, read by
        // CameraZoomController.
        public float Mining_CameraZoomBonus => LevelOf(UpgradeEffect.Mining_CameraZoom) * EffectValuePerLevelOf(UpgradeEffect.Mining_CameraZoom);

        // GameDesignDoc "Mining > Insta-mine chance".
        public float Mining_InstaMineChance => LevelOf(UpgradeEffect.Mining_BaseInstaMineChance) * EffectValuePerLevelOf(UpgradeEffect.Mining_BaseInstaMineChance);

        // GameDesignDoc "Mining > Increase mining speed": "the final upgrade makes dirt/stone an
        // instant mine" - interpreted as the Dirt category (the valueless filler blocks), since
        // the Ore-category "Stone" block is a sellable mineral, not filler. Its own capstone
        // (Mining_DirtInstaMine), not a side effect of maxing Mining Speed.
        public bool Mining_InstantMineDirt => IsMaxedEffect(UpgradeEffect.Mining_DirtInstaMine);

        // GameDesignDoc "Mining > Increase mining speed" capstone for the ore side: targets
        // ScrapAlloy, the lowest-tier Ore block.
        public bool Mining_InstantMineScrapAlloy => IsMaxedEffect(UpgradeEffect.Mining_ScrapAlloyInstaMine);

        // GameDesignDoc "Mining > Lantern": extra fog-of-war reveal radius on top of the base.
        public int Mining_LanternFogRadiusBonus => Mathf.RoundToInt(LevelOf(UpgradeEffect.Mining_LanternRadius) * EffectValuePerLevelOf(UpgradeEffect.Mining_LanternRadius));

        // GameDesignDoc "Mining > Increase mining speed": "each tier adds 10% mining speed".
        public float Mining_SpeedMultiplier => 1f + LevelOf(UpgradeEffect.Mining_Speed) * EffectValuePerLevelOf(UpgradeEffect.Mining_Speed);

        // GameDesignDoc "Survival" branch (Movement in the Market): PlayerController reads these to
        // derive its effective movement/flight/fall-damage tunables from the serialized base values.
        public float Movement_FallDamageReductionMultiplier => Mathf.Max(0f, 1f - LevelOf(UpgradeEffect.Movement_FallDamageReduction) * EffectValuePerLevelOf(UpgradeEffect.Movement_FallDamageReduction));
        public float Movement_FlightSpeedMultiplier => 1f + LevelOf(UpgradeEffect.Movement_FlightSpeed) * EffectValuePerLevelOf(UpgradeEffect.Movement_FlightSpeed);
        public float Movement_FuelCapacityBonus => LevelOf(UpgradeEffect.Movement_FuelInventory) * EffectValuePerLevelOf(UpgradeEffect.Movement_FuelInventory);
        public float Movement_FuelEfficiencyMultiplier => Mathf.Max(0f, 1f - LevelOf(UpgradeEffect.Movement_FuelEfficiency) * EffectValuePerLevelOf(UpgradeEffect.Movement_FuelEfficiency));
        public float Movement_GravityMultiplier => 1f + LevelOf(UpgradeEffect.Movement_GravityIncrease) * EffectValuePerLevelOf(UpgradeEffect.Movement_GravityIncrease);

        // GameDesignDoc "Lantern capstones > hazard sense: highlights hazard blocks".
        public bool Movement_HazardSenseUnlocked => IsMaxedEffect(UpgradeEffect.Movement_HazardSense);

        public float Movement_MoveSpeedMultiplier => 1f + LevelOf(UpgradeEffect.Movement_MoveSpeed) * EffectValuePerLevelOf(UpgradeEffect.Movement_MoveSpeed);

        // "Upgrades > processed good sale value": mirrors Economy_SellValueMultiplier but only applies to
        // Depot.SellGood, kept independent of the ore MarketingSellMultiplier.
        public float Processing_GoodsSellMultiplier => 1f + LevelOf(UpgradeEffect.Processing_SaleValueMultiplier) * EffectValuePerLevelOf(UpgradeEffect.Processing_SaleValueMultiplier);

        // "Upgrades > processing queue": "allows multiple recipes to be running at once" - added on
        // top of ProcessingManager's 1 free base slot.
        public int Processing_QueueSlotCount => LevelOf(UpgradeEffect.Processing_QueueSlots);

        // GameDesignDoc processingImplementation.md "Upgrades > processing time": "multiplicatively
        // reduces the duration of all recipe crafting" - divides ProcessingManager's computed
        // duration, mirrors Mining_SpeedMultiplier's shape.
        public float Processing_SpeedMultiplier => 1f + LevelOf(UpgradeEffect.Processing_SpeedMultiplier) * EffectValuePerLevelOf(UpgradeEffect.Processing_SpeedMultiplier);
        #endregion
    }
}
