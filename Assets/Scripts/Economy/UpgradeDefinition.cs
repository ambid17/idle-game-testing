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
    // Depot, MapGenerationService, ChunkGenerator, ...) reads on demand. Values are explicit and are
    // what's serialized into the UpgradeDefinition .asset files - reorder or remove members freely,
    // but never change an existing member's number or reuse a retired one. New members take the
    // next free number in their prefix's range. UpgradeDatabase.Validate() flags any asset whose
    // name doesn't match its Effect.
    public enum UpgradeEffect
    {
        // Automation 100-199
        Automation_AutomatonCount = 100,
        Automation_AutomatonInventoryCapacity = 101,
        Automation_AutomatonMiningRadius = 102,
        Automation_AutomatonMiningSpeed = 103,
        Automation_AutomatonMoveSpeed = 104,
        Automation_FuelDroneCount = 105,
        Automation_FuelDroneInventoryCapacity = 106,
        Automation_FuelDroneMoveSpeed = 107,
        // "Drone delivery > Market Sense" capstone - StorageDrone auto-sells on delivery once maxed.
        Automation_StorageDroneAutoSellUnlock = 108,
        Automation_StorageDroneCount = 109,
        Automation_StorageDroneInventoryCapacity = 110,
        Automation_StorageDroneMoveSpeed = 111,

        // Economy 200-299
        Economy_InventoryCapacity = 200,
        Economy_MarketingSellMultiplier = 201,
        Economy_Overflow = 202,

        // Mining 300-399
        Mining_AreaSize = 300,
        Mining_BaseInstaMineChance = 301,
        // "Lantern capstones > zoom, enhance" - drives CameraControl.CameraZoomController.
        Mining_CameraZoom = 302,
        // Drives UpgradeManager.InstantMineDirt.
        Mining_DirtInstaMine = 303,
        Mining_LanternRadius = 304,
        // Drives UpgradeManager.InstantMineScrapAlloy.
        Mining_ScrapAlloyInstaMine = 307,
        Mining_Speed = 305,
        Mining_TrueSight = 306,

        // Movement 400-499
        Movement_FallDamageReduction = 400,
        Movement_FlightSpeed = 401,
        Movement_FuelEfficiency = 402,
        Movement_FuelInventory = 403,
        Movement_GravityIncrease = 404,
        // "Lantern capstones > hazard sense" - drives ChunkTilemapView's hazard tile tint.
        Movement_HazardSense = 405,
        Movement_MoveSpeed = 406,

        // Processing 500-599
        Processing_DiamondRecipeUnlock = 503,
        Processing_EmeraldRecipeUnlock = 504,
        Processing_GoldRecipeUnlock = 505,
        Processing_IronRecipeUnlock = 506,
        Processing_QueueSlots = 500,
        Processing_SaleValueMultiplier = 501,
        Processing_ScrapRecipeUnlock = 508,
        Processing_SpeedMultiplier = 502,
        Processing_StoneRecipeUnlock = 507,
    }

    [CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Economy/Upgrade Definition")]
    public class UpgradeDefinition : UpgradeDefinitionBase
    {
        public UpgradeBranch Branch;
        public UpgradeEffect Effect;
    }
}
