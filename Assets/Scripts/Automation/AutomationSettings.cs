using Events;
using UnityEngine;

namespace Automation
{
    // GameDesignDoc "Control Center": targeting choice for Storage/Fuel Drones is a runtime
    // setting, not UpgradeDefinition-backed upgrades, so they live here
    // rather than in UpgradeManager. Persisted by Persistence.SaveService.
    public enum TargetMode
    {
        PlayerAlways,
        FullestInventory
    }

    // Only takes effect once UpgradeManager.Automation_StorageDroneAutoSellUnlocked (the "Market Sense"
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
        public StorageDroneDepositMode StorageDroneDepositMode { get; private set; } = StorageDroneDepositMode.Deposit;
        // Whether automatons and drones report to the player with toasts (deposit reports, the
        // fuel drone's "refueled you"). Off only silences the toasts - the Miner Dashboard still counts.
        public bool DroneNotifications { get; private set; } = true;

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

        public void SetDroneNotifications(bool enabled)
        {
            DroneNotifications = enabled;
            GameManager.EventService.Dispatch<AutomationSettingsChangedEvent>();
        }

        // Bulk restore from SaveService - silent (no event) since this only ever runs once at
        // startup before any UI has subscribed.
        public void RestoreFromSaveData(TargetMode storageMode, TargetMode fuelMode, StorageDroneDepositMode storageDepositMode, bool droneNotifications)
        {
            DroneNotifications = droneNotifications;
            StorageDroneTargetMode = storageMode;
            FuelDroneTargetMode = fuelMode;
            StorageDroneDepositMode = storageDepositMode;
        }
    }
}
