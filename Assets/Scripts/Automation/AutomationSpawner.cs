using System;
using System.Collections.Generic;
using Events;
using Player;
using UnityEngine;

namespace Automation
{
    // Lives on the Control Center GameObject. Reconciles the live MiningAutomaton/StorageDrone/
    // FuelDrone GameObjects under it to match UpgradeManager's purchased counts - one function
    // covers both "spawn automatons at the Control Center" on scene load (per the design doc's
    // idle/offline behavior, and after SaveService restores levels) and spawning immediately on a
    // mid-session purchase.
    public class AutomationSpawner : MonoBehaviour
    {
        [SerializeField] private MiningAutomaton automatonPrefab;
        [SerializeField] private StorageDrone storageDronePrefab;
        [SerializeField] private FuelDrone fuelDronePrefab;
        [SerializeField] private PlayerController player;

        private readonly List<MiningAutomaton> automatons = new();
        private readonly List<StorageDrone> storageDrones = new();
        private readonly List<FuelDrone> fuelDrones = new();

        private void Awake()
        {
            if (automatonPrefab == null) Debug.LogError($"{nameof(AutomationSpawner)} on {name} is missing automatonPrefab.");
            if (storageDronePrefab == null) Debug.LogError($"{nameof(AutomationSpawner)} on {name} is missing storageDronePrefab.");
            if (fuelDronePrefab == null) Debug.LogError($"{nameof(AutomationSpawner)} on {name} is missing fuelDronePrefab.");
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) Debug.LogError($"{nameof(AutomationSpawner)} on {name}: no PlayerController found in scene.");
        }

        private void Start() => ReconcileAll();

        private void OnEnable() => GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
        private void OnDisable() => GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);

        private void OnUpgradePurchased(UpgradePurchasedEvent evt)
        {
            var effect = evt.Definition.Effect;
            if (effect == Economy.UpgradeEffect.AutomatonCount || effect == Economy.UpgradeEffect.StorageDroneCount || effect == Economy.UpgradeEffect.FuelDroneCount)
            {
                ReconcileAll();
            }
        }

        private void ReconcileAll()
        {
            var upgrades = Economy.UpgradeManager.Instance;
            Reconcile(automatons, automatonPrefab, upgrades.AutomatonCount, (instance, index) => instance.Configure(index));
            Reconcile(storageDrones, storageDronePrefab, upgrades.StorageDroneCount, (instance, index) => instance.Configure(transform.position, index));
            Reconcile(fuelDrones, fuelDronePrefab, upgrades.FuelDroneCount, (instance, _) => instance.Configure(transform.position, player));
        }

        private void Reconcile<T>(List<T> instances, T prefab, int targetCount, Action<T, int> configure) where T : Component
        {
            if (prefab == null) return;

            while (instances.Count < targetCount)
            {
                var instance = Instantiate(prefab, transform.position, Quaternion.identity);
                configure(instance, instances.Count + 1);
                instances.Add(instance);
            }

            while (instances.Count > targetCount)
            {
                int lastIndex = instances.Count - 1;
                var last = instances[lastIndex];
                instances.RemoveAt(lastIndex);
                if (last != null) Destroy(last.gameObject);
            }
        }
    }
}
