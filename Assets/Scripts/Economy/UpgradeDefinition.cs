using UnityEngine;

namespace Economy
{
    // Branch grouping per GameDesignDoc "Market Upgrades" (Mining / Economy / Automation /
    // Progression) and Assets/Docs/UpgradeIdeas.pdf.
    public enum UpgradeBranch
    {
        Mining,
        Economy,
        Automation,
        Movement,
        Processing
    }

    // What purchasing a level of this upgrade actually does. UpgradeManager exposes one computed
    // property per effect that the relevant system (PlayerMining, PlayerController, PlayerInventory,
    // Depot, MapGenerationService, ChunkGenerator, ...) reads on demand. Ordering here is fixed by
    // the serialized int already baked into existing UpgradeDefinition assets - never reorder or
    // remove a member, only append.
    public enum UpgradeEffect
    {
        // Automation
        Automation_AutomatonCount,
        Automation_AutomatonInventoryCapacity,
        Automation_AutomatonMiningRadius,
        Automation_AutomatonMiningSpeed,
        Automation_AutomatonMoveSpeed,
        Automation_FuelDroneCount,
        Automation_FuelDroneInventoryCapacity,
        Automation_FuelDroneMoveSpeed,
        // "Drone delivery > Market Sense" capstone - StorageDrone auto-sells on delivery once maxed.
        Automation_StorageDroneAutoSellUnlock,
        Automation_StorageDroneCount,
        Automation_StorageDroneInventoryCapacity,
        Automation_StorageDroneMoveSpeed,


        // Economy
        // Dollar-purchased counterpart to PrestigeUpgradeEffect.GridWidthBonus - applied
        // immediately by MapGenerationService rather than waiting for the next prestige.
        Economy_GridWidthBonus,
        Economy_InventoryCapacity,
        Economy_MarketingSellMultiplier,
        Economy_Overflow,

        // Mining
        Mining_AreaSize,
        Mining_BaseInstaMineChance,
        // "Lantern capstones > zoom, enhance" - drives CameraControl.CameraZoomController.
        Mining_CameraZoom,
        // Real effect (Dirt block category already exists) - appended out of branch order so
        // every earlier member keeps its serialized int stable in existing UpgradeDefinition
        // assets. Drives UpgradeManager.InstantMineDirt.
        Mining_DirtInstaMine,
        Mining_LanternRadius,
        Mining_Speed,
        Mining_TrueSight,
        // Current BlockTypeId set has no "Wood" block - targets ScrapAlloy instead (see
        // UpgradeManager.InstantMineScrapAlloy).
        Mining_WoodInstaMine,


        // Movement
        Movement_FallDamageReduction,
        Movement_FlightSpeed,
        Movement_FuelEfficiency,
        Movement_FuelInventory,
        Movement_GravityIncrease,
        // "Lantern capstones > hazard sense" - drives ChunkTilemapView's hazard tile tint.
        Movement_HazardSense,
        Movement_MoveSpeed,

        // Processing
        Processing_QueueSlots,
        Processing_SaleValueMultiplier,
        Processing_SpeedMultiplier,
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
    }
}
