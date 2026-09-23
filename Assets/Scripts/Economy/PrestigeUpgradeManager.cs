using System.Collections.Generic;
using Events;
using UnityEngine;

namespace Economy
{
    // Tracks purchased levels for every PrestigeUpgradeDefinition in GameManager.PrestigeUpgradeDatabase.
    // Mirrors UpgradeManager's shape (via UpgradeManagerBase), but levels are NEVER cleared by
    // PrestigeManager.ExecutePrestige - that's the entire point of this being a separate manager
    // from UpgradeManager: prestige perks are the "meta" progression that survives every hard reset.
    // Singleton so it needs no scene wiring, matching UpgradeManager/Wallet/Depot.
    //
    // Purchases are queued, not applied: TryPurchase (inherited from UpgradeManagerBase) still spends
    // currency immediately, but RecordPurchase below writes into queuedLevels instead of the base
    // class's applied `levels` dict, so EffectiveLevel/LevelOf (what every gameplay accessor below
    // reads) never moves until CommitQueuedUpgrades is called by PrestigeManager.ExecutePrestige.
    // This is what guarantees a map-generation perk (e.g. GridWidthBonus) can't apply mid-run against
    // an already-generated map - every prestige perk, not just the map-gen ones, waits for the same
    // moment.
    public class PrestigeUpgradeManager : UpgradeManagerBase<PrestigeUpgradeManager, PrestigeUpgradeDefinition, PrestigeUpgradeEffect>
    {
        private static PrestigeUpgradeDatabase database => GameManager.PrestigeUpgradeDatabase;

        private readonly Dictionary<string, int> queuedLevels = new();

        public Sprite CurrencyIcon;

        protected override double CurrentCurrency => Wallet.Instance.ArtifactCount;
        protected override bool TrySpendCurrency(double amount) => Wallet.Instance.TrySpendArtifacts(Mathf.CeilToInt((float)amount));
        protected override string KeyOf(PrestigeUpgradeDefinition def) => def.DisplayName;
        protected override PrestigeUpgradeDefinition Find(PrestigeUpgradeEffect effect) => database.Find(effect);
        protected override PrestigeUpgradeDefinition Find(string key) => database.Find(key);
        protected override PrestigeUpgradeDefinition PrerequisiteOf(PrestigeUpgradeDefinition def) => def.Prerequisite as PrestigeUpgradeDefinition;
        protected override void DispatchPurchased(PrestigeUpgradeDefinition def, int newLevel) =>
            GameManager.EventService.Dispatch(new PrestigeUpgradePurchasedEvent(def, newLevel));
        // DispatchLoaded intentionally left at the base default (same event as a live purchase) -
        // every listener (e.g. MuseumUI) reacts identically whether a level came from a purchase or
        // a save file. CommitQueuedUpgrades below also routes through SetLevel/DispatchLoaded for
        // the same reason: an applied level looks the same to listeners regardless of how it arrived.

        // Purchase-time bookkeeping (cost/maxed/unlock) counts applied + queued levels together, so
        // costs escalate correctly across queued purchases and a branch can be planned/queued ahead
        // of a prerequisite actually being applied.
        protected override int PurchaseLevel(PrestigeUpgradeDefinition def) => RawLevel(def) + QueuedRawLevel(def);

        protected override void RecordPurchase(PrestigeUpgradeDefinition def, string key, int newLevel)
        {
            queuedLevels[key] = newLevel - RawLevel(def);
            GameManager.EventService.Dispatch(new PrestigeUpgradeQueuedEvent(def, newLevel));
        }

        private int QueuedRawLevel(PrestigeUpgradeDefinition def) =>
            def != null && queuedLevels.TryGetValue(KeyOf(def), out var lvl) ? lvl : 0;

        // Kept as its previous public name since UI/other systems already call this directly.
        // Applied level only - see the class comment above.
        public int GetLevel(PrestigeUpgradeDefinition def) => EffectiveLevel(def);

        // How many additional levels are queued (paid for, not yet applied) on top of GetLevel.
        public int GetQueuedLevel(PrestigeUpgradeDefinition def) => QueuedRawLevel(def);

        // Bulk restore for SaveService, mirroring SetLevel but for the queued store - a player who
        // queues upgrades, spending artifacts, then closes the game before prestiging must not lose
        // that queue on reload (they already paid for it).
        public void SetQueuedLevel(string key, int amount)
        {
            if (string.IsNullOrEmpty(key) || amount <= 0) return;

            var def = Find(key);
            if (def == null)
            {
                Debug.LogError($"{nameof(PrestigeUpgradeManager)}.SetQueuedLevel: no {nameof(PrestigeUpgradeDefinition)} found for key '{key}'. Save data may be stale (renamed/removed upgrade) - queued level discarded.");
                return;
            }

            queuedLevels[key] = amount;
            GameManager.EventService.Dispatch(new PrestigeUpgradeQueuedEvent(def, RawLevel(def) + amount));
        }

        public IEnumerable<KeyValuePair<string, int>> AllQueuedLevels => queuedLevels;

        // Dev-only hard reset of both applied and queued prestige upgrade levels, for
        // DevPanelProgressionTab's "Remove All Prestige Upgrades" cheat button. Real gameplay never
        // wipes these - PrestigeManager.ExecutePrestige only ever commits queued levels via
        // CommitQueuedUpgrades below, never clears applied ones.
        public void ResetAllLevels()
        {
            ClearLevels();
            queuedLevels.Clear();
        }

        // Called once by PrestigeManager.ExecutePrestige, before anything else, so every applied
        // level (including map-gen ones like GridWidthBonus) is in place before the rest of the
        // prestige reads them. Routes through the base class's SetLevel/DispatchLoaded so committed
        // levels dispatch the same PrestigeUpgradePurchasedEvent a live purchase used to fire
        // immediately - UI just refreshes, it doesn't need to know queued vs committed.
        public void CommitQueuedUpgrades()
        {
            if (queuedLevels.Count == 0) return;

            foreach (var kvp in new Dictionary<string, int>(queuedLevels))
            {
                var def = Find(kvp.Key);
                int committedLevel = (def != null ? RawLevel(def) : 0) + kvp.Value;
                SetLevel(kvp.Key, committedLevel);
            }

            queuedLevels.Clear();
        }

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

        // GameDesignDoc "Prestige > Prestige": artifact spawn rate / value-per-mine / passive gain.
        public float ArtifactSpawnRateMultiplier => 1f + LevelOf(PrestigeUpgradeEffect.Prestige_ArtifactSpawnRateMultiplier) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Prestige_ArtifactSpawnRateMultiplier);
        // How many artifacts a single artifact-ore mine grants - see Wallet.AddArtifact.
        public float ArtifactValueMultiplier => 1f + LevelOf(PrestigeUpgradeEffect.Prestige_ArtifactValueMultiplier) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Prestige_ArtifactValueMultiplier);
        // Artifacts per minute, passively - see PassivePrestigeIncomeTicker.
        public float PassiveArtifactRate => LevelOf(PrestigeUpgradeEffect.Prestige_PassiveArtifactRate) * EffectValuePerLevelOf(PrestigeUpgradeEffect.Prestige_PassiveArtifactRate);

        // GameDesignDoc "Prestige > Prestige" capstone: "auto-prestige when it's mathematically
        // worth it" - intentionally left as a purchasable/displayed flag with no auto-trigger; a
        // real profitability projection is a separate feature, not upgrade-application.
        public bool AutoPrestigeUnlocked => IsEffectMaxedAndApplied(PrestigeUpgradeEffect.Prestige_AutoPrestigeCapstone);

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
        public bool DoublePassiveLayerBonusUnlocked => IsEffectMaxedAndApplied(PrestigeUpgradeEffect.Economy_DoublePassiveLayerBonus);
        public bool KeepPassiveLayerBonusUnlocked => IsEffectMaxedAndApplied(PrestigeUpgradeEffect.Economy_KeepPassiveLayerBonus);

        // GameDesignDoc "Prestige > Mining": keep "digging while flying" between prestige runs.
        public bool KeepDigWhileFlyingUnlocked => IsEffectMaxedAndApplied(PrestigeUpgradeEffect.Mining_KeepDigWhileFlying);

        // GameDesignDoc "Prestige > Mining > true sight": reveals all fog of war - read by
        // MapGenerationService.GetFogRevealRadius.
        public bool TrueSightUnlocked => IsEffectMaxedAndApplied(PrestigeUpgradeEffect.Mining_TrueSight);

        // Gameplay-effect flag for a capstone: applied (post-prestige) level only. Distinct from the
        // base class's IsMaxed, which now also counts not-yet-applied queued levels for
        // purchase-gating/UI purposes (see PurchaseLevel override above) - a queued-but-uncommitted
        // capstone purchase must not unlock its effect early.
        private bool IsEffectMaxedAndApplied(PrestigeUpgradeEffect effect)
        {
            var def = database.Find(effect);
            return def != null && EffectiveLevel(def) >= def.MaxLevel;
        }
    }
}
