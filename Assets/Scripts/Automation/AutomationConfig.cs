using UnityEngine;

namespace Automation
{
    // Shared base numbers for all automaton/storage-drone/fuel-drone instances per
    // automationImplementation.md. Upgrade multipliers/bonuses live on UpgradeManager and are
    // applied on top of these bases by the consuming behavior scripts.
    [CreateAssetMenu(fileName = "AutomationConfig", menuName = "Automation/Automation Config")]
    public class AutomationConfig : ScriptableObject
    {
        [Header("Mining Automaton")]
        public float AutomatonBaseMiningSpeed = 1f;
        public float AutomatonBaseMoveSpeed = 5f;
        public int AutomatonBaseMiningRadius = 1;
        public float AutomatonBaseInventoryWeight = 50f;
        public int AutomatonWanderRadius = 3;

        [Header("Storage Drone")]
        public float StorageDroneBaseMoveSpeed = 4f;
        public float StorageDroneBaseInventoryWeight = 20f;

        [Header("Fuel Drone")]
        public float FuelDroneBaseMoveSpeed = 4f;
        public float FuelDroneBaseFuelCapacity = 20f;
        public float FuelCostPerUnit = 5f;
        [Range(0f, 1f)] public float FuelNeedThresholdFraction = 0.10f;

        [Header("Player HP Refill")]
        public float HpCostPerUnit = 5f;

        public void Validate()
        {
            if (AutomatonBaseMiningSpeed <= 0)
                Debug.LogError("AutomationConfig has an invalid AutomatonBaseMiningSpeed.");
            if (AutomatonBaseMoveSpeed <= 0)
                Debug.LogError("AutomationConfig has an invalid AutomatonBaseMoveSpeed.");
            if (AutomatonBaseMiningRadius <= 0)
                Debug.LogError("AutomationConfig has an invalid AutomatonBaseMiningRadius.");
            if (AutomatonBaseInventoryWeight <= 0)
                Debug.LogError("AutomationConfig has an invalid AutomatonBaseInventoryWeight.");
            if (AutomatonWanderRadius <= 0)
                Debug.LogError("AutomationConfig has an invalid AutomatonWanderRadius.");
            if (StorageDroneBaseMoveSpeed <= 0)
                Debug.LogError("AutomationConfig has an invalid StorageDroneBaseMoveSpeed.");
            if (StorageDroneBaseInventoryWeight <= 0)
                Debug.LogError("AutomationConfig has an invalid StorageDroneBaseInventoryWeight.");
            if (FuelDroneBaseMoveSpeed <= 0)
                Debug.LogError("AutomationConfig has an invalid FuelDroneBaseMoveSpeed.");
            if (FuelDroneBaseFuelCapacity <= 0)
                Debug.LogError("AutomationConfig has an invalid FuelDroneBaseFuelCapacity.");
            if (FuelCostPerUnit <= 0)
                Debug.LogError("AutomationConfig has an invalid FuelCostPerUnit.");
            if (HpCostPerUnit <= 0)
                Debug.LogError("AutomationConfig has an invalid HpCostPerUnit.");
        }
    }
}
