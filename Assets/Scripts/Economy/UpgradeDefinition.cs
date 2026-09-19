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
        Movement,
        Processing
    }

    // What purchasing a level of this upgrade actually does. UpgradeManager exposes one computed
    // property per effect that the relevant system (PlayerMining, PlayerInventory, Depot,
    // MapGenerationService) reads on demand.
    public enum UpgradeEffect
    {
        // Assets/Docs/UpgradeIdeas.pdf entries with no live gameplay hook yet - SO assets exist so
        // the skill tree is exhaustive per the doc, matching the "stub" convention already used
        // throughout PrestigeUpgradeEffect for the same reason (system doesn't exist yet).

        // Automation
        Automation_AutomatonCount,
        Automation_AutomatonInventoryCapacity,
        Automation_AutomatonMiningRadius,
        Automation_AutomatonMiningSpeed,
        Automation_AutomatonMoveSpeed,
        Automation_FuelDroneCount,
        Automation_FuelDroneInventoryCapacity,
        Automation_FuelDroneMoveSpeed,
        Automation_StorageDroneAutoSellUnlock, // stub
        Automation_StorageDroneCount,
        Automation_StorageDroneInventoryCapacity,
        Automation_StorageDroneMoveSpeed,


        // Economy
        Economy_GridWidthBonus, // stub
        Economy_InventoryCapacity,
        Economy_MarketingSellMultiplier,
        Economy_Overflow,

        // Mining
        Mining_AreaSize,
        Mining_BaseInstaMineChance,
        Mining_CameraZoom, // stub
        // Real effect (Dirt block category already exists) - appended after the stubs, out of
        // branch order, so every earlier member keeps its serialized int stable in existing
        // UpgradeDefinition assets. Drives UpgradeManager.InstantMineDirt.
        Mining_DirtInstaMine,
        Mining_LanternRadius,
        Mining_Speed,
        Mining_TrueSight,
        Mining_WoodInstaMine, // stub


        // Movement
        Movement_FallDamageReduction, // stub
        Movement_FlightSpeed, // stub
        Movement_FuelEfficiency, // stub
        Movement_FuelInventory, // stub
        Movement_GravityIncrease, // stub
        Movement_HazardSense, // stub
        Movement_MoveSpeed, // stub

        // Processing
        Processing_QueueSlots,
        Processing_SaleValueMultiplier, // stub - Processing Center doesn't exist yet
        Processing_SpeedMultiplier, // stub - Processing Center doesn't exist yet
        Processing_DiamondRecipeUnlock,
        Processing_EmeraldRecipeUnlock,
        Processing_GoldRecipeUnlock,
        Processing_IronRecipeUnlock,
        Processing_StoneRecipeUnlock,
        Processing_ScrapRecipeUnlock,
    }

    [CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Economy/Upgrade Definition")]
    public class UpgradeDefinition : UpgradeDefinitionBase
    {
        public UpgradeBranch Branch;
        public UpgradeEffect Effect;

        [Tooltip("Must be unlocked (or maxed, if Require Prerequisite Maxed) before this can be purchased. Leave empty for a branch's first tier.")]
        public UpgradeDefinition Prerequisite;
    }
}
