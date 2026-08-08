using System.Collections.Generic;
using Events;
using UnityEngine;

namespace Economy
{
    // Tracks purchased levels for every PrestigeUpgradeDefinition in GameManager.PrestigeUpgradeDatabase.
    // Mirrors UpgradeManager's shape exactly, but levelsByUpgradeId is NEVER cleared by
    // PrestigeManager.ExecutePrestige - that's the entire point of this being a separate manager
    // from UpgradeManager: prestige perks are the "meta" progression that survives every hard reset.
    // Singleton so it needs no scene wiring, matching UpgradeManager/Wallet/Depot.
    public class PrestigeUpgradeManager : Singleton<PrestigeUpgradeManager>
    {
        private static PrestigeUpgradeDatabase database => GameManager.PrestigeUpgradeDatabase;

        private readonly Dictionary<string, int> levelsByUpgradeId = new();

        public int GetLevel(PrestigeUpgradeDefinition def) => def != null && levelsByUpgradeId.TryGetValue(def.Id, out var lvl) ? lvl : 0;

        public bool IsMaxed(PrestigeUpgradeDefinition def) => def != null && GetLevel(def) >= def.MaxLevel;

        public bool IsUnlocked(PrestigeUpgradeDefinition def)
        {
            if (def == null) return false;
            if (def.Prerequisite == null) return true;
            return def.RequirePrerequisiteMaxed ? IsMaxed(def.Prerequisite) : GetLevel(def.Prerequisite) > 0;
        }

        public double GetNextCost(PrestigeUpgradeDefinition def) => def.GetCost(GetLevel(def));

        public bool CanPurchase(PrestigeUpgradeDefinition def)
        {
            if (def == null || IsMaxed(def) || !IsUnlocked(def)) return false;
            return PrestigePoints.Instance.Points >= GetNextCost(def);
        }

        public bool TryPurchase(PrestigeUpgradeDefinition def)
        {
            if (!CanPurchase(def)) return false;

            double cost = GetNextCost(def);
            if (!PrestigePoints.Instance.TrySpend(cost)) return false;

            int newLevel = GetLevel(def) + 1;
            levelsByUpgradeId[def.Id] = newLevel;
            GameManager.EventService.Dispatch(new PrestigeUpgradePurchasedEvent(def, newLevel));
            return true;
        }

        private int LevelOf(PrestigeUpgradeEffect effect) => GetLevel(database.Find(effect));

        private float EffectValuePerLevelOf(PrestigeUpgradeEffect effect)
        {
            var def = database.Find(effect);
            if (def == null)
            {
                Debug.LogError($"PrestigeUpgradeManager.EffectValuePerLevelOf: No PrestigeUpgradeDefinition found for effect {effect}. Check that the PrestigeUpgradeDatabase is properly populated.");
                return 0;
            }
            return def.EffectValuePerLevel;
        }

        // GameDesignDoc "Prestige > Mining > Increase grid size": added to the base grid width in
        // MapGenerationService before every prestige's map regeneration.
        public int GridWidthBonus => Mathf.RoundToInt(LevelOf(PrestigeUpgradeEffect.GridWidthBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.GridWidthBonus));

        // Stubs - no camera zoom control or layer-size-based generation exists yet to consume these.
        public float CameraZoomBonus => LevelOf(PrestigeUpgradeEffect.CameraZoomBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.CameraZoomBonus);
        public float LayerSizeReduction => LevelOf(PrestigeUpgradeEffect.LayerSizeReduction) * EffectValuePerLevelOf(PrestigeUpgradeEffect.LayerSizeReduction);

        // GameDesignDoc "Prestige > Economy": mineral value multiplier.
        public float MineralValueMultiplier => 1f + LevelOf(PrestigeUpgradeEffect.MineralValueMultiplier) * EffectValuePerLevelOf(PrestigeUpgradeEffect.MineralValueMultiplier);

        // Stub - no Processing Center system exists yet to consume this.
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

        // Stub - no "is it mathematically worth it" projection exists yet; purchasable, no auto-trigger.
        public bool AutoPrestigeUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.AutoPrestigeCapstone));

        // Stubs below - purchasable/persisted/displayed, but no consumer system exists yet.
        public float OreTierOddsBonus => LevelOf(PrestigeUpgradeEffect.OreTierOddsBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.OreTierOddsBonus);
        public float PowerUpEffectivenessBonus => LevelOf(PrestigeUpgradeEffect.PowerUpEffectivenessBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.PowerUpEffectivenessBonus);
        public float PowerUpSpawnRateBonus => LevelOf(PrestigeUpgradeEffect.PowerUpSpawnRateBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.PowerUpSpawnRateBonus);
        public int ShieldChargeCount => LevelOf(PrestigeUpgradeEffect.ShieldChargeCount);
        public float MoveSpeedBonus => LevelOf(PrestigeUpgradeEffect.MoveSpeedBonus) * EffectValuePerLevelOf(PrestigeUpgradeEffect.MoveSpeedBonus);
        public float FallDamageReduction => LevelOf(PrestigeUpgradeEffect.FallDamageReduction) * EffectValuePerLevelOf(PrestigeUpgradeEffect.FallDamageReduction);
        public float GasResistance => LevelOf(PrestigeUpgradeEffect.GasResistance) * EffectValuePerLevelOf(PrestigeUpgradeEffect.GasResistance);
        public bool DoublePassiveLayerBonusUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.DoublePassiveLayerBonus));
        public bool KeepPassiveLayerBonusUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.KeepPassiveLayerBonus));
        public bool KeepDigWhileFlyingUnlocked => IsMaxed(database.Find(PrestigeUpgradeEffect.KeepDigWhileFlying));

        // Bulk restore for SaveService - silent (no PrestigeUpgradePurchasedEvent), same convention
        // as UpgradeManager.SetLevel.
        public void SetLevel(string upgradeId, int level)
        {
            if (string.IsNullOrEmpty(upgradeId) || level < 0) return;
            levelsByUpgradeId[upgradeId] = level;
        }

        public IEnumerable<KeyValuePair<string, int>> AllLevels => levelsByUpgradeId;
    }
}
