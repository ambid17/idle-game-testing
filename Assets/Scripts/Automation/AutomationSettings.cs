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

    public class AutomationSettings : Singleton<AutomationSettings>
    {
        public TargetMode StorageDroneTargetMode { get; private set; } = TargetMode.FullestInventory;
        public TargetMode FuelDroneTargetMode { get; private set; } = TargetMode.PlayerAlways;
        public float FuelSpendingCapPercent { get; private set; } = 0.5f;

        public void SetStorageDroneTargetMode(TargetMode mode)
        {
            StorageDroneTargetMode = mode;
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
        public void RestoreFromSaveData(TargetMode storageMode, TargetMode fuelMode, float spendingCapPercent)
        {
            StorageDroneTargetMode = storageMode;
            FuelDroneTargetMode = fuelMode;
            FuelSpendingCapPercent = Mathf.Clamp01(spendingCapPercent);
        }
    }
}
