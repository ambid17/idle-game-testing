using Events;
using UnityEngine;

namespace Economy
{
    // Tracks purchased levels for every PrestigeUpgradeDefinition in GameManager.PrestigeUpgradeDatabase.
    // Mirrors UpgradeManager's shape (via UpgradeManagerBase), but levels are NEVER cleared by
    // PrestigeManager.ExecutePrestige - that's the entire point of this being a separate manager
    // from UpgradeManager: prestige perks are the "meta" progression that survives every hard reset.
    // Singleton so it needs no scene wiring, matching UpgradeManager/Wallet/Depot.
    public class PrestigeUpgradeManager : UpgradeManagerBase<PrestigeUpgradeManager, PrestigeUpgradeDefinition, PrestigeUpgradeEffect>
    {
        private static PrestigeUpgradeDatabase database => GameManager.PrestigeUpgradeDatabase;

        public Sprite CurrencyIcon;

        protected override double CurrentCurrency => PrestigePoints.Instance.Points;
        protected override bool TrySpendCurrency(double amount) => PrestigePoints.Instance.TrySpend(amount);
        protected override string KeyOf(PrestigeUpgradeDefinition def) => def.DisplayName;
        protected override PrestigeUpgradeDefinition Find(PrestigeUpgradeEffect effect) => database.Find(effect);
        protected override PrestigeUpgradeDefinition Find(string key) => database.Find(key);
        protected override PrestigeUpgradeDefinition PrerequisiteOf(PrestigeUpgradeDefinition def) => def.Prerequisite as PrestigeUpgradeDefinition;
        protected override void DispatchPurchased(PrestigeUpgradeDefinition def, int newLevel) =>
            GameManager.EventService.Dispatch(new PrestigeUpgradePurchasedEvent(def, newLevel));
        // DispatchLoaded intentionally left at the base default (same event as a live purchase) -
        // every listener (e.g. MuseumUI) reacts identically whether a level came from a purchase or
        // a save file.

        // Kept as its previous public name since UI/other systems already call this directly.
        public int GetLevel(PrestigeUpgradeDefinition def) => EffectiveLevel(def);

        // GameDesignDoc "Prestige > Mining > Increase grid size": added to the base grid width in
        // MapGenerationService before every prestige's map regeneration.
        public int GridWidthBonus => Mathf.RoundToInt(LevelOf(PrestigeUpgradeEffect.Mining_GridWidthBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Mining_GridWidthBonus));

        // GameDesignDoc "Prestige > Mining > view": combined with UpgradeManager.CameraZoomBonus by
        // CameraZoomController.
        public float CameraZoomBonus => LevelOf(PrestigeUpgradeEffect.Mining_CameraZoomBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Mining_CameraZoomBonus);

        // GameDesignDoc "Prestige > Mining > adjust layer sizes": subtracted from LayerConfig's
        // authored LayerHeight once per prestige, for not-yet-generated layers only.
        public float LayerSizeReduction => LevelOf(PrestigeUpgradeEffect.Mining_LayerSizeReduction) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Mining_LayerSizeReduction);

        // GameDesignDoc "Prestige > Economy": mineral value multiplier.
        public float MineralValueMultiplier => 1f + LevelOf(PrestigeUpgradeEffect.Economy_MineralValueMultiplier) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Economy_MineralValueMultiplier);

        // GameDesignDoc "Prestige > Economy > processing": processed good production multiplier.
        public float ProcessedGoodMultiplier => 1f + LevelOf(PrestigeUpgradeEffect.Economy_ProcessedGoodMultiplier) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Economy_ProcessedGoodMultiplier);

        // GameDesignDoc "Prestige > idle": the purchased level of each "keep tier" perk directly
        // *is* the kept baseline UpgradeManager adds back to the matching Market effect after a
        // reset (level 2 owned = Market upgrade starts at effective level 2), not a multiplier.
        public int KeptAutomatonCountBaseline => LevelOf(PrestigeUpgradeEffect.Idle_KeepAutomatonCount);
        public int KeptAutomatonMiningSpeedBaseline => LevelOf(PrestigeUpgradeEffect.Idle_KeepAutomatonMiningSpeed);
        public int KeptAutomatonMiningRadiusBaseline => LevelOf(PrestigeUpgradeEffect.Idle_KeepAutomatonMiningRadius);
        public int KeptAutomatonMoveSpeedBaseline => LevelOf(PrestigeUpgradeEffect.Idle_KeepAutomatonMoveSpeed);

        // GameDesignDoc "Prestige > Prestige": artifact spawn rate / points-per-artifact / passive gain.
        public float ArtifactSpawnRateMultiplier => 1f + LevelOf(PrestigeUpgradeEffect.Prestige_ArtifactSpawnRateMultiplier) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Prestige_ArtifactSpawnRateMultiplier);
        public float PrestigePointsPerArtifactMultiplier => 1f + LevelOf(PrestigeUpgradeEffect.Prestige_PrestigePointsPerArtifactMultiplier) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Prestige_PrestigePointsPerArtifactMultiplier);
        public float PassivePrestigePointRate => LevelOf(PrestigeUpgradeEffect.Prestige_PassivePrestigePointRate) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Prestige_PassivePrestigePointRate);

        // GameDesignDoc "Prestige > Prestige" capstone: "auto-prestige when it's mathematically
        // worth it" - intentionally left as a purchasable/displayed flag with no auto-trigger; a
        // real profitability projection is a separate feature, not upgrade-application.
        public bool AutoPrestigeUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.Prestige_AutoPrestigeCapstone));

        // GameDesignDoc "Prestige > Progression".
        public float OreTierOddsBonus => LevelOf(PrestigeUpgradeEffect.Progression_OreTierOddsBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Progression_OreTierOddsBonus);
        public float PowerUpEffectivenessBonus => LevelOf(PrestigeUpgradeEffect.Progression_PowerUpEffectivenessBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Progression_PowerUpEffectivenessBonus);
        public float PowerUpSpawnRateBonus => LevelOf(PrestigeUpgradeEffect.Progression_PowerUpSpawnRateBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Progression_PowerUpSpawnRateBonus);

        // GameDesignDoc "Prestige > Survival".
        public int ShieldChargeCount => LevelOf(PrestigeUpgradeEffect.Survival_ShieldChargeCount);
        public float MoveSpeedBonus => LevelOf(PrestigeUpgradeEffect.Survival_MoveSpeedBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Survival_MoveSpeedBonus);
        public float FallDamageReduction => LevelOf(PrestigeUpgradeEffect.Survival_FallDamageReduction) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Survival_FallDamageReduction);
        public float GasResistance => LevelOf(PrestigeUpgradeEffect.Survival_GasResistance) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Survival_GasResistance);

        // Deepened hazards pass: one resistance perk each for the other 3 hazards that now do more
        // than flat proximity damage (Explosive/FallingRock/Lava), same shape as GasResistance.
        public float BlastResistance => LevelOf(PrestigeUpgradeEffect.Survival_BlastResistance) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Survival_BlastResistance);
        public float FallingRockResistance => LevelOf(PrestigeUpgradeEffect.Survival_FallingRockResistance) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Survival_FallingRockResistance);
        public float LavaResistance => LevelOf(PrestigeUpgradeEffect.Survival_LavaResistance) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Survival_LavaResistance);

        // GameDesignDoc "Prestige > Economy" capstones on the passive layer bonus (see
        // Economy.LayerBonusTracker for the base mechanic these modify).
        public bool DoublePassiveLayerBonusUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.Economy_DoublePassiveLayerBonus));
        public bool KeepPassiveLayerBonusUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.Economy_KeepPassiveLayerBonus));

        // GameDesignDoc "Prestige > Mining": keep "digging while flying" between prestige runs.
        public bool KeepDigWhileFlyingUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.Mining_KeepDigWhileFlying));
    }
}
