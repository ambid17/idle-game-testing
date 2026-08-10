using UnityEngine;

namespace Economy
{
    // Branch grouping per GameDesignDoc "Market Upgrades" (Mining / Economy / Automation /
    // Progression) and Assets/Docs/UpgradeIdeas.pdf. Some Economy/Progression effects are stubs
    // (see UpgradeEffect) since their gameplay systems (Processing Center, player movement hooks)
    // don't exist yet.
    public enum UpgradeBranch
    {
        Mining,
        Economy,
        Automation,
        Progression
    }

    // What purchasing a level of this upgrade actually does. UpgradeManager exposes one computed
    // property per effect that the relevant system (PlayerMining, PlayerInventory, Depot,
    // MapGenerationService) reads on demand.
    public enum UpgradeEffect
    {
        MiningAreaRadius,
        MiningSpeed,
        InstaMineChance,
        LanternFogRadius,
        LanternTrueSight,
        InventoryCapacity,
        MarketingSellMultiplier,
        Overflow,
        AutomatonCount,
        AutomatonMiningSpeed,
        AutomatonMoveSpeed,
        AutomatonMiningRadius,
        AutomatonInventoryCapacity,
        StorageDroneCount,
        StorageDroneMoveSpeed,
        StorageDroneInventoryCapacity,
        FuelDroneCount,
        FuelDroneMoveSpeed,
        FuelDroneInventoryCapacity,

        // Assets/Docs/UpgradeIdeas.pdf entries with no live gameplay hook yet - SO assets exist so
        // the skill tree is exhaustive per the doc, matching the "stub" convention already used
        // throughout PrestigeUpgradeEffect for the same reason (system doesn't exist yet).
        CameraZoomBonus, // stub
        WoodInstaMineUnlock, // stub
        StorageDroneAutoSellUnlock, // stub
        GridWidthBonus, // stub
        ProcessingSaleValueMultiplier, // stub - Processing Center doesn't exist yet
        ProcessingRecipeUnlock, // stub - Processing Center doesn't exist yet
        ProcessingSpeedMultiplier, // stub - Processing Center doesn't exist yet
        FallSpeedBonus, // stub
        FallDamageReductionBonus, // stub
        PlayerMoveSpeedBonus, // stub
        FlightSpeedBonus, // stub
        GravityBonus, // stub
        FuelInventoryCapacity, // stub
        FuelEfficiencyBonus, // stub
        HazardSenseUnlock // stub
    }

    [CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Economy/Upgrade Definition")]
    public class UpgradeDefinition : ScriptableObject
    {
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public UpgradeBranch Branch;
        public UpgradeEffect Effect;

        [Tooltip("Value added per purchased level. Meaning depends on Effect - see UpgradeManager's accessor for this Effect.")]
        public float EffectValuePerLevel = 1f;

        [Tooltip("Number of purchasable levels. Use 1 for a one-time unlock (e.g. a capstone).")]
        public int MaxLevel = 1;

        public double BaseCost = 100;
        [Tooltip("Cost multiplier applied per level already purchased.")]
        public float CostGrowth = 1.15f;

        [Tooltip("Must be unlocked (or maxed, if Require Prerequisite Maxed) before this can be purchased. Leave empty for a branch's first tier.")]
        public UpgradeDefinition Prerequisite;
        [Tooltip("If set, Prerequisite must be fully maxed rather than just purchased once. Used for capstones.")]
        public bool RequirePrerequisiteMaxed;

        public double GetCost(int currentLevel) => BaseCost * System.Math.Pow(CostGrowth, currentLevel);
    }
}
