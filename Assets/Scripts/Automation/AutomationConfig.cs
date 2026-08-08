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
    }
}
