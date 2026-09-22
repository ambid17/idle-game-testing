using Events;
using UnityEngine;

namespace Automation
{
    // GameDesignDoc "Control Center": targeting choice for Storage/Fuel Drones and the Fuel Drone
    // spending cap are runtime settings, not UpgradeDefinition-backed upgrades, so they live here
    // rather than in UpgradeManager. Persisted by Persistence.SaveService.
    public enum TargetMode
    {
        PlayerAlways,
        FullestInventory
    }

    // Only takes effect once UpgradeManager.StorageDroneAutoSellUnlocked (the "Market Sense"
    // capstone) is purchased - StorageDrone still deposits raw ore at the Depot regardless of this
    // setting until then. See StorageDroneDashboardUI for the gating UI.
    public enum StorageDroneDepositMode
    {
        Deposit,
        AutoSell
    }

    public class AutomationSettings : Singleton<AutomationSettings>
    {
        public TargetMode StorageDroneTargetMode { get; private set; } = TargetMode.FullestInventory;
        public TargetMode FuelDroneTargetMode { get; private set; } = TargetMode.PlayerAlways;
        public float FuelSpendingCapPercent { get; private set; } = 0.5f;
        public StorageDroneDepositMode StorageDroneDepositMode { get; private set; } = StorageDroneDepositMode.Deposit;

        public void SetStorageDroneTargetMode(TargetMode mode)
        {
            StorageDroneTargetMode = mode;
            GameManager.EventService.Dispatch<AutomationSettingsChangedEvent>();
        }

        public void SetStorageDroneDepositMode(StorageDroneDepositMode mode)
        {
            StorageDroneDepositMode = mode;
            GameManager.EventService.Dispatch<AutomationSettingsChangedEvent>();
        }

        public void SetFuelDroneTargetMode(TargetMode mode)
        {
            FuelDroneTargetMode = mode;
            GameManager.EventService.Dispatch<AutomationSettingsChangedEvent>();
        }

        public void SetFuelSpendingCapPercent(float percent)
        {
            FuelSpendingCapPercent = Mathf.Clamp01(percent);
            GameManager.EventService.Dispatch<AutomationSettingsChangedEvent>();
        }

        // Bulk restore from SaveService - silent (no event) since this only ever runs once at
        // startup before any UI has subscribed.
        public void RestoreFromSaveData(TargetMode storageMode, TargetMode fuelMode, float spendingCapPercent, StorageDroneDepositMode storageDepositMode)
        {
            StorageDroneTargetMode = storageMode;
            FuelDroneTargetMode = fuelMode;
            FuelSpendingCapPercent = Mathf.Clamp01(spendingCapPercent);
            StorageDroneDepositMode = storageDepositMode;
        }
    }
}
