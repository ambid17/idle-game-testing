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
        protected override string KeyOf(PrestigeUpgradeDefinition def) => def.Id;
        protected override PrestigeUpgradeDefinition Find(PrestigeUpgradeEffect effect) => database.Find(effect);
        protected override PrestigeUpgradeDefinition Find(string key) => database.Find(key);
        protected override PrestigeUpgradeDefinition PrerequisiteOf(PrestigeUpgradeDefinition def) => def.Prerequisite;
        protected override void DispatchPurchased(PrestigeUpgradeDefinition def, int newLevel) =>
            GameManager.EventService.Dispatch(new PrestigeUpgradePurchasedEvent(def, newLevel));
        // DispatchLoaded intentionally left at the base default (same event as a live purchase) -
        // every listener (e.g. MuseumUI) reacts identically whether a level came from a purchase or
        // a save file.

        // Kept as its previous public name since UI/other systems already call this directly.
        public int GetLevel(PrestigeUpgradeDefinition def) => EffectiveLevel(def);

        // GameDesignDoc "Prestige > Mining > Increase grid size": added to the base grid width in
        // MapGenerationService before every prestige's map regeneration.
        public int GridWidthBonus => Mathf.RoundToInt(LevelOf(PrestigeUpgradeEffect.GridWidthBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.GridWidthBonus));

        // GameDesignDoc "Prestige > Mining > view": combined with UpgradeManager.CameraZoomBonus by
        // CameraZoomController.
        public float CameraZoomBonus => LevelOf(PrestigeUpgradeEffect.CameraZoomBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.CameraZoomBonus);

        // GameDesignDoc "Prestige > Mining > adjust layer sizes": subtracted from LayerConfig's
        // authored LayerHeight once per prestige, for not-yet-generated layers only.
        public float LayerSizeReduction => LevelOf(PrestigeUpgradeEffect.LayerSizeReduction) * EffectValuePerLevelOf(PrestigeUpgradeEffect.LayerSizeReduction);

        // GameDesignDoc "Prestige > Economy": mineral value multiplier.
        public float MineralValueMultiplier => 1f + LevelOf(PrestigeUpgradeEffect.MineralValueMultiplier) * EffectValuePerLevelOf(PrestigeUpgradeEffect.MineralValueMultiplier);

        // GameDesignDoc "Prestige > Economy > processing": processed good production multiplier.
        public float ProcessedGoodMultiplier => 1f + LevelOf(PrestigeUpgradeEffect.ProcessedGoodMultiplier) * EffectValuePerLevelOf(PrestigeUpgradeEffect.ProcessedGoodMultiplier);

        // GameDesignDoc "Prestige > idle": the purchased level of each "keep tier" perk directly
        // *is* the kept baseline UpgradeManager adds back to the matching Market effect after a
        // reset (level 2 owned = Market upgrade starts at effective level 2), not a multiplier.
        public int KeptAutomatonCountBaseline => LevelOf(PrestigeUpgradeEffect.KeepAutomatonCount);
        public int KeptAutomatonMiningSpeedBaseline => LevelOf(PrestigeUpgradeEffect.KeepAutomatonMiningSpeed);
        public int KeptAutomatonMiningRadiusBaseline => LevelOf(PrestigeUpgradeEffect.KeepAutomatonMiningRadius);
        public int KeptAutomatonMoveSpeedBaseline => LevelOf(PrestigeUpgradeEffect.KeepAutomatonMoveSpeed);

        // GameDesignDoc "Prestige > Prestige": artifact spawn rate / points-per-artifact / passive gain.
        public float ArtifactSpawnRateMultiplier => 1f + LevelOf(PrestigeUpgradeEffect.ArtifactSpawnRateMultiplier) * EffectValuePerLevelOf(PrestigeUpgradeEffect.ArtifactSpawnRateMultiplier);
        public float PrestigePointsPerArtifactMultiplier => 1f + LevelOf(PrestigeUpgradeEffect.PrestigePointsPerArtifactMultiplier) * EffectValuePerLevelOf(PrestigeUpgradeEffect.PrestigePointsPerArtifactMultiplier);
        public float PassivePrestigePointRate => LevelOf(PrestigeUpgradeEffect.PassivePrestigePointRate) * EffectValuePerLevelOf(PrestigeUpgradeEffect.PassivePrestigePointRate);

        // GameDesignDoc "Prestige > Prestige" capstone: "auto-prestige when it's mathematically
        // worth it" - intentionally left as a purchasable/displayed flag with no auto-trigger; a
        // real profitability projection is a separate feature, not upgrade-application.
        public bool AutoPrestigeUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.AutoPrestigeCapstone));

        // GameDesignDoc "Prestige > Progression".
        public float OreTierOddsBonus => LevelOf(PrestigeUpgradeEffect.OreTierOddsBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.OreTierOddsBonus);
        public float PowerUpEffectivenessBonus => LevelOf(PrestigeUpgradeEffect.PowerUpEffectivenessBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.PowerUpEffectivenessBonus);
        public float PowerUpSpawnRateBonus => LevelOf(PrestigeUpgradeEffect.PowerUpSpawnRateBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.PowerUpSpawnRateBonus);

        // GameDesignDoc "Prestige > Survival".
        public int ShieldChargeCount => LevelOf(PrestigeUpgradeEffect.ShieldChargeCount);
        public float MoveSpeedBonus => LevelOf(PrestigeUpgradeEffect.MoveSpeedBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.MoveSpeedBonus);
        public float FallDamageReduction => LevelOf(PrestigeUpgradeEffect.FallDamageReduction) * EffectValuePerLevelOf(PrestigeUpgradeEffect.FallDamageReduction);
        public float GasResistance => LevelOf(PrestigeUpgradeEffect.GasResistance) * EffectValuePerLevelOf(PrestigeUpgradeEffect.GasResistance);

        // GameDesignDoc "Prestige > Economy" capstones on the passive layer bonus (see
        // Economy.LayerBonusTracker for the base mechanic these modify).
        public bool DoublePassiveLayerBonusUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.DoublePassiveLayerBonus));
        public bool KeepPassiveLayerBonusUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.KeepPassiveLayerBonus));

        // GameDesignDoc "Prestige > Mining": keep "digging while flying" between prestige runs.
        public bool KeepDigWhileFlyingUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.KeepDigWhileFlying));
    }
}
