using UnityEngine;

namespace Economy
{
    // Category grouping per GameDesignDoc "# Prestige" - the Museum's perk tree, parallel to but
    // distinct from the Market's UpgradeBranch (these are permanent, bought with Prestige points,
    // and survive every future hard reset).
    public enum PrestigeUpgradeBranch
    {
        Mining,
        Economy,
        Idle,
        Prestige,
        Progression,
        Survival
    }

    // What purchasing a level of this prestige perk actually does. PrestigeUpgradeManager exposes
    // one computed property per effect - see Assets/Docs/GameDesignDoc.md "# Prestige". Ordering
    // here is fixed by the serialized int already baked into existing PrestigeUpgradeDefinition
    // assets - never reorder or remove a member, only append. AutoPrestigeCapstone is the one
    // deliberate exception left unconsumed: it's a purchasable/displayed flag with no auto-trigger,
    // since "prestige when mathematically worth it" needs a real profitability projection that's
    // out of scope for upgrade-application work.
    public enum PrestigeUpgradeEffect
    {
        Mining_GridWidthBonus,
        Mining_KeepDigWhileFlying,
        Mining_CameraZoomBonus,
        Mining_LayerSizeReduction,
        Economy_MineralValueMultiplier,
        Economy_ProcessedGoodMultiplier,
        // GameDesignDoc "Prestige > idle > auto miner" lists 4 kept-tier perks (count, speed, dig
        // speed, move speed) but the Market only has 3 distinct automaton stats besides count
        // (AutomatonMiningSpeed, AutomatonMiningRadius, AutomatonMoveSpeed) - mapped 1:1 onto those
        // by name below rather than guessing at the doc's "speed" vs "dig speed" wording.
        Idle_KeepAutomatonCount,
        Idle_KeepAutomatonMiningSpeed,
        Idle_KeepAutomatonMiningRadius,
        Idle_KeepAutomatonMoveSpeed,
        Prestige_ArtifactSpawnRateMultiplier,
        Prestige_PrestigePointsPerArtifactMultiplier,
        Prestige_PassivePrestigePointRate,
        Prestige_AutoPrestigeCapstone,
        Progression_OreTierOddsBonus,
        Progression_PowerUpEffectivenessBonus,
        Progression_PowerUpSpawnRateBonus,
        Survival_ShieldChargeCount,
        Survival_MoveSpeedBonus,
        Survival_FallDamageReduction,
        Survival_GasResistance,
        Economy_DoublePassiveLayerBonus,
        Economy_KeepPassiveLayerBonus,
        Survival_BlastResistance,
        Survival_FallingRockResistance,
        Survival_LavaResistance
    }

    [CreateAssetMenu(fileName = "PrestigeUpgradeDefinition", menuName = "Economy/Prestige Upgrade Definition")]
    public class PrestigeUpgradeDefinition : UpgradeDefinitionBase
    {
        public PrestigeUpgradeBranch Branch;
        public PrestigeUpgradeEffect Effect;

        // Prestige perks default to a cheaper base cost / steeper growth curve than Market
        // upgrades (UpgradeDefinitionBase's defaults) - only applies to newly created assets.
        private void Reset()
        {
            BaseCost = 10;
            CostGrowth = 1.5f;
        }
    }
}
